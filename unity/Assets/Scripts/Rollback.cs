using System;
using System.Collections.Generic;
using System.Text;

// GGPO-style rollback over the WebSocket relay.
// Every peer runs the same deterministic Sim. Local inputs are applied after a small delay and sent to everyone;
// remote inputs we don't have yet are predicted (repeat the last known one). When the real input arrives and
// differs, we rewind to that frame, restore the saved state, and re-simulate up to the present in one go.
public class Session
{
    public const int Ring = 64;              // frames of state / input history
    public const int MaxAhead = 10;          // never run further than this past confirmed remote input
    public readonly SimState Cur = new SimState();
    readonly SimState[] saved = new SimState[Ring];
    readonly int[] savedFrame = new int[Ring];
    readonly int[,] inputs = new int[SimState.MaxF, Ring];
    readonly int[,] inputFrame = new int[SimState.MaxF, Ring];   // which frame the stored input belongs to
    readonly int[,] used = new int[SimState.MaxF, Ring];
    readonly int[] contig = new int[SimState.MaxF];               // highest frame with all inputs known for a slot
    readonly bool[] remote = new bool[SimState.MaxF];
    public readonly List<int> LocalSlots = new List<int>();
    public int N, Delay;
    public bool Online;
    int rollbackFrom = int.MaxValue;
    public int Rollbacks, MaxRollback, Stalls;
    public bool Desync, LogHashes;
    public string DesyncInfo = "";

    // outgoing batch
    readonly StringBuilder outBuf = new StringBuilder();
    int outFirst = -1; readonly List<int> outVals = new List<int>();
    public Action<string> Send;

    // time sync
    readonly int[] remoteFrame = new int[SimState.MaxF];
    readonly float[] remoteFrameAt = new float[SimState.MaxF];
    int skipCounter;

    readonly Dictionary<int, uint> myHash = new Dictionary<int, uint>();
    readonly Dictionary<int, uint> theirHash = new Dictionary<int, uint>();
    int lastHashSent;

    public Session(int stage, int[] chars, int[] bots, uint seed, bool online, int[] localSlots, int delay)
    {
        for (int i = 0; i < Ring; i++) { saved[i] = new SimState(); savedFrame[i] = -1; }
        Sim.Events.Clear();
        Sim.Setup(Cur, stage, chars, bots, seed);
        N = chars.Length; Online = online; Delay = online ? delay : 0;
        LocalSlots.AddRange(localSlots);
        for (int s = 0; s < SimState.MaxF; s++)
        {
            for (int k = 0; k < Ring; k++) inputFrame[s, k] = -1;
            remote[s] = online && s < N && bots[s] == 0 && !LocalSlots.Contains(s);
            contig[s] = remote[s] ? Delay : int.MaxValue / 2;   // frames up to the delay are known to be empty
        }
        Save();
    }

    public int Frame => Cur.frame;

    int MinRemote()
    {
        int m = int.MaxValue;
        for (int s = 0; s < N; s++) if (remote[s]) m = Math.Min(m, contig[s]);
        return m;
    }

    // ---------------------------------------------------------------- inputs
    void Store(int slot, int frame, int value)
    {
        int k = frame % Ring;
        inputs[slot, k] = value; inputFrame[slot, k] = frame;
    }

    bool Known(int slot, int frame) => inputFrame[slot, frame % Ring] == frame;

    public void AddLocal(int slot, int value)
    {
        int f = Cur.frame + 1 + Delay;
        if (Known(slot, f)) return;
        Store(slot, f, value);
        if (Online)
        {
            if (outFirst < 0) outFirst = f;
            outVals.Add(value);
        }
    }

    public void OnRemoteInput(int slot, int firstFrame, int[] vals, int theirFrame, float now)
    {
        if (slot < 0 || slot >= N || !remote[slot]) return;
        remoteFrame[slot] = theirFrame; remoteFrameAt[slot] = now;
        for (int i = 0; i < vals.Length; i++)
        {
            int f = firstFrame + i;
            if (f <= Cur.frame - Ring + 4) continue;   // far too old
            if (Known(slot, f)) continue;
            Store(slot, f, vals[i]);
            if (f <= Cur.frame && used[slot, f % Ring] != vals[i]) rollbackFrom = Math.Min(rollbackFrom, f);
        }
        while (Known(slot, contig[slot] + 1)) contig[slot]++;
    }

    // a peer left: from the frame after its last input, a bot drives its fighter (same frame on every peer)
    public void OnLeft(int slot)
    {
        if (slot < 0 || slot >= N || !remote[slot]) return;
        int from = contig[slot] + 1;
        for (int f = from; f < from + 3; f++) Store(slot, f, 1 << 12);   // bot-conversion flag
        remote[slot] = false;
        contig[slot] = int.MaxValue / 2;
        if (from <= Cur.frame) rollbackFrom = Math.Min(rollbackFrom, from);
    }

    // ---------------------------------------------------------------- stepping
    public bool Stalled => Online && Cur.frame + 1 > MinRemote() + MaxAhead;

    // Called at 60 Hz. Returns false if we had to wait for the network this tick.
    public bool Tick(float now)
    {
        if (rollbackFrom <= Cur.frame) Rewind();
        if (Stalled) { Stalls++; Flush(); return false; }
        // gentle time sync: if we're well ahead of everyone's clock, idle one tick in eight
        if (Online)
        {
            int ahead = int.MaxValue;
            for (int s = 0; s < N; s++) if (remote[s]) ahead = Math.Min(ahead, Cur.frame - (remoteFrame[s] + (int)((now - remoteFrameAt[s]) * 60f)));
            if (ahead != int.MaxValue && ahead > 2 && (++skipCounter % 8) == 0) { Flush(); return false; }
        }
        Advance();
        Flush();
        Hashes();
        return true;
    }

    void Advance()
    {
        int f = Cur.frame + 1;
        var inp = new int[SimState.MaxF];
        for (int s = 0; s < N; s++)
        {
            int v = Known(s, f) ? inputs[s, f % Ring] : Predict(s, f);
            used[s, f % Ring] = v;
            inp[s] = v;
        }
        ApplyFlags(inp);
        Sim.Step(Cur, inp);
        Save();
    }

    // predicted input: keep holding the same directions, never invent button presses
    int Predict(int slot, int frame)
    {
        for (int f = frame - 1; f >= Math.Max(0, frame - Ring + 2); f--)
            if (Known(slot, f)) return inputs[slot, f % Ring] & (In.L | In.R | In.U | In.D | In.Heavy);
        return 0;
    }

    void ApplyFlags(int[] inp)
    {
        for (int s = 0; s < N; s++)
            if ((inp[s] & (1 << 12)) != 0) { if (Cur.f[s].bot == 0) Cur.f[s].bot = 2; inp[s] &= ~(1 << 12); }
    }

    void Save()
    {
        int k = Cur.frame % Ring;
        saved[k].CopyFrom(Cur); savedFrame[k] = Cur.frame;
    }

    void Rewind()
    {
        int target = Cur.frame;
        int from = rollbackFrom;
        rollbackFrom = int.MaxValue;
        int k = (from - 1) % Ring;
        if (from - 1 < 0 || savedFrame[k] != from - 1) { Desync = true; DesyncInfo = "rollback too deep"; return; }
        Cur.CopyFrom(saved[k]);
        // events of the frames we are about to redo get regenerated
        Sim.Events.RemoveAll(e => e.frame >= from);
        Rollbacks++;
        MaxRollback = Math.Max(MaxRollback, target - from + 1);
        while (Cur.frame < target) Advance();
    }

    // ---------------------------------------------------------------- network io
    void Flush()
    {
        if (!Online || outFirst < 0 || Send == null) return;
        outBuf.Clear();
        outBuf.Append("{\"t\":\"i\",\"f\":").Append(outFirst).Append(",\"c\":").Append(Cur.frame).Append(",\"k\":[");
        for (int i = 0; i < outVals.Count; i++) { if (i > 0) outBuf.Append(','); outBuf.Append(outVals[i]); }
        outBuf.Append("]}");
        Send(outBuf.ToString());
        outFirst = -1; outVals.Clear();
    }

    // every half second, compare a hash of a fully-confirmed frame with the other peers
    void Hashes()
    {
        if (!Online) return;
        int confirmed = Math.Min(MinRemote(), Cur.frame);
        int F = confirmed - confirmed % 30;
        if (F <= lastHashSent || F <= 0) return;
        int k = F % Ring;
        if (savedFrame[k] != F) return;
        lastHashSent = F;
        uint h = saved[k].Hash();
        myHash[F] = h;
        if (theirHash.TryGetValue(F, out var th) && th != h) { Desync = true; DesyncInfo = "hash mismatch @" + F; }
        Send?.Invoke("{\"t\":\"h\",\"f\":" + F + ",\"h\":" + h + "}");
        if (LogHashes && F % 300 == 0) UnityEngine.Debug.Log("[HASH] f=" + F + " h=" + h + " rb=" + Rollbacks + " maxrb=" + MaxRollback + " stalls=" + Stalls + " desync=" + Desync);
        if (myHash.Count > 40) myHash.Remove(F - 1200);
    }

    public void OnRemoteHash(int frame, uint h)
    {
        theirHash[frame] = h;
        if (myHash.TryGetValue(frame, out var mine) && mine != h) { Desync = true; DesyncInfo = "hash mismatch @" + frame; }
        if (theirHash.Count > 60) theirHash.Remove(frame - 1800);
    }
}
