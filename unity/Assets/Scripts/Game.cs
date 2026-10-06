using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData
{
    public int ch, ch2 = 3, stage = -1, bots = 1, botLv = 2;
    public bool muted, howto;
    public int played, won, kos;
}

// BONK BRAWL — flow, local input, online glue. The fight itself is the deterministic Sim inside a rollback Session.
public class Game : MonoBehaviour
{
    public static Game I;
    public enum Mode { Menu, Local, Online, Results }
    public Mode M = Mode.Menu;
    public SaveData Save;
    public Session S;
    public bool Dev, AutoPlay, AutoDrive;
    public string[] Names = new string[0];
    readonly List<int> localSlots = new List<int>();
    public int OnlineSlot = -1;
    float acc;
    bool resultsShown, reported;
    int attractRound;

    // latched presses so a quick tap between two ticks still counts
    readonly int[] latch = new int[2];

    void Awake()
    {
        I = this;
        Application.targetFrameRate = -1;
        QualitySettings.shadowDistance = 45f; QualitySettings.shadowCascades = 1;
        QualitySettings.shadowResolution = ShadowResolution.Medium; QualitySettings.antiAliasing = 2;
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLInput.captureAllKeyboardInput = false;
#endif
        var url = Application.absoluteURL;
        Dev = url.Contains("dev=1") && (url.Contains("://localhost") || url.Contains("://127.0.0.1"));   // cheats never on the live site
        AutoPlay = url.Contains("bot=1"); AutoDrive = Dev && url.Contains("autodrive=1");
        DevCam.Install(Dev);
        var json = PlayerPrefs.GetString("bb_save", "");
        try { Save = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SaveData>(json); } catch { Save = null; }
        if (Save == null || (Dev && url.Contains("fresh=1"))) Save = new SaveData();

        gameObject.AddComponent<Sfx>();
        Sfx.I.SetMuted(Save.muted);
        new GameObject("WebBridge").AddComponent<WebBridge>();
        var cam = Camera.main;
        cam.fieldOfView = 34f; cam.nearClipPlane = 0.5f; cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        gameObject.AddComponent<View>().Init(cam);
        new GameObject("UI").AddComponent<UI>().Init();
        StartAttract();
        UI.I.ShowTitle();
        if (!Save.howto) UI.I.ShowHowTo();
        WebBridge.Ready();
        if (Dev) { int k = url.IndexOf("stage="); if (k >= 0 && k + 6 < url.Length && char.IsDigit(url[k + 6])) Save.stage = url[k + 6] - '0'; }
        if (AutoPlay) { Save.bots = 3; StartLocal(false); }
    }

    void OnApplicationFocus(bool f) { if (!f && UI.I) UI.I.PauseIfLocal(); }

    public void Persist() { PlayerPrefs.SetString("bb_save", JsonUtility.ToJson(Save)); PlayerPrefs.Save(); }

    public string NameOf(int slot) => slot >= 0 && slot < Names.Length ? Names[slot] : "P" + (slot + 1);
    public bool IsLocal(int slot) => localSlots.Contains(slot);
    public int LocalCount => localSlots.Count;
    public int LocalSlot(int i) => i < localSlots.Count ? localSlots[i] : -1;

    // ================================================================ matches
    void Begin(Session s, string[] names, List<int> locals, Mode m)
    {
        S = s; Names = names; localSlots.Clear(); localSlots.AddRange(locals); M = m;
        acc = 0; resultsShown = false; reported = false; latch[0] = latch[1] = 0;
        View.I.BuildStage(s.Cur.stage);
        View.I.Bind(s.Cur, names);
        Time.timeScale = 1;
    }

    public void StartAttract()
    {
        var r = new System.Random(Environment.TickCount + attractRound++);
        int stage = r.Next(Stages.All.Length);
        int n = 2 + r.Next(3);
        var chars = new int[n]; var bots = new int[n]; var names = new string[n];
        for (int i = 0; i < n; i++) { chars[i] = r.Next(Roster.All.Length); bots[i] = 2 + r.Next(2); names[i] = Roster.All[chars[i]].name; }
        Begin(new Session(stage, chars, bots, (uint)r.Next(1, int.MaxValue), false, new int[0], 0), names, new List<int>(), Mode.Menu);
        UI.I.ShowHud(false);
    }

    public int PickStage() => Save.stage >= 0 ? Save.stage : UnityEngine.Random.Range(0, Stages.All.Length);

    public void StartLocal(bool twoPlayers)
    {
        WebBridge.Event(twoPlayers ? "fight_local_2p" : "fight_local");
        int humans = twoPlayers ? 2 : 1;
        int n = Mathf.Clamp(humans + Save.bots, 2, 4);
        var chars = new int[n]; var bots = new int[n]; var names = new string[n];
        var used = new List<int>();
        for (int i = 0; i < n; i++)
        {
            if (i == 0) chars[i] = Save.ch;
            else if (i == 1 && twoPlayers) chars[i] = Save.ch2;
            else { int c; do { c = UnityEngine.Random.Range(0, Roster.All.Length); } while (used.Contains(c) && used.Count < Roster.All.Length); chars[i] = c; }
            used.Add(chars[i]);
            bots[i] = i < humans ? 0 : Save.botLv;
            names[i] = i < humans ? (twoPlayers ? "P" + (i + 1) : "YOU") : "CPU " + Roster.All[chars[i]].name;
        }
        if (AutoPlay) bots[0] = 3;
        var locals = new List<int> { 0 }; if (twoPlayers) locals.Add(1);
        Begin(new Session(PickStage(), chars, bots, (uint)UnityEngine.Random.Range(1, int.MaxValue), false, locals.ToArray(), 0), names, locals, Mode.Local);
        UI.I.CloseScreens();
        UI.I.ShowHud(true);
        Sfx.I.Music(true);
        WebBridge.Gameplay(true);
        WebBridge.Event(twoPlayers ? "fight_2p" : "fight_cpu", n);
    }

    public void Rematch()
    {
        if (M == Mode.Online || OnlineSlot >= 0) { WebBridge.NetSend("{\"t\":\"rematch\"}"); UI.I.Toast("WAITING FOR THE OTHERS..."); return; }
        StartLocal(localSlots.Count > 1);
    }

    public void Quit()
    {
        Time.timeScale = 1;
        if (M == Mode.Online || OnlineSlot >= 0) WebBridge.NetLeave();
        OnlineSlot = -1;
        Sfx.I.Music(false);
        WebBridge.Gameplay(false);
        StartAttract();
        UI.I.ShowTitle();
    }

    public void OpenOnline() { WebBridge.NetOpen(Save.ch); }

    // ================================================================ frame loop
    void Update()
    {
        if (Input.anyKeyDown || Input.touchCount > 0) Sfx.I.Unlock();
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        // latch presses for local players
        for (int p = 0; p < 2; p++) latch[p] |= ReadInput(p, true) & (In.Jump | In.Light | In.Heavy | In.Dodge | In.Throw);

        if (S != null && !(M == Mode.Local && UI.I.Paused))
        {
            acc += dt;
            int ticks = 0;
            while (acc >= 1f / 60f && ticks < 5)
            {
                acc -= 1f / 60f; ticks++;
                for (int p = 0; p < localSlots.Count; p++)
                {
                    int v = ReadInput(p, false) | latch[p];
                    latch[p] = 0;
                    S.AddLocal(localSlots[p], v);
                }
                S.Tick(Time.realtimeSinceStartup);
            }
            if (acc > 0.2f) acc = 0;   // tab was hidden: don't try to catch up in one go
        }
        if (S == null) return;
        View.I.Render(S.Cur, dt, M == Mode.Menu);
        View.I.PlayEvents(Sim.Events);
        UI.I.UpdateHud(S.Cur, M);

        var c = S.Cur;
        if (c.over == 1)
        {
            if (M == Mode.Menu && c.frame - c.overFrame > 150) StartAttract();
            else if ((M == Mode.Local || M == Mode.Online) && !resultsShown && c.frame - c.overFrame > 140) ShowResults();
        }
        if (S.Desync && Dev) UI.I.Toast("DESYNC: " + S.DesyncInfo);
    }

    void ShowResults()
    {
        resultsShown = true;
        var c = S.Cur;
        int me = localSlots.Count > 0 ? localSlots[0] : -1;
        Save.played++;
        if (me >= 0 && c.winner == me) Save.won++;
        if (me >= 0) Save.kos += c.f[me].kos;
        if (me >= 0) WebBridge.Event("hats_end", c.f[me].hatMax);
        Persist();
        if (M == Mode.Online && !reported) { reported = true; WebBridge.NetSend("{\"t\":\"result\",\"w\":" + c.winner + "}"); }
        Sfx.I.Music(false);
        Sfx.I.Fanfare(me >= 0 && c.winner == me);
        WebBridge.Gameplay(false);
        UI.I.ShowResults(c, Names, M == Mode.Online);
        if (AutoPlay) Invoke(nameof(AutoAgain), 6f);
    }
    void AutoAgain() { Save.stage = -1; StartLocal(false); }

    // ================================================================ input
    // p = local player index (0 or 1)
    public int ReadInput(int p, bool pressesOnly)
    {
        if (AutoDrive && p == 0 && S != null) return pressesOnly ? 0 : Monkey();
        int v = 0;
        if (p == 0)
        {
            if (Input.GetKey(KeyCode.A)) v |= In.L;
            if (Input.GetKey(KeyCode.D)) v |= In.R;
            if (Input.GetKey(KeyCode.W)) v |= In.U;
            if (Input.GetKey(KeyCode.S)) v |= In.D;
            if (localSlots.Count < 2)
            {
                if (Input.GetKey(KeyCode.LeftArrow)) v |= In.L;
                if (Input.GetKey(KeyCode.RightArrow)) v |= In.R;
                if (Input.GetKey(KeyCode.UpArrow)) v |= In.U;
                if (Input.GetKey(KeyCode.DownArrow)) v |= In.D;
            }
            if (Key(pressesOnly, KeyCode.Space)) v |= In.Jump;
            if (Key(pressesOnly, KeyCode.J) || Key(pressesOnly, KeyCode.Z)) v |= In.Light;
            if (Key(pressesOnly, KeyCode.K) || Key(pressesOnly, KeyCode.X)) v |= In.Heavy;
            if (Key(pressesOnly, KeyCode.L) || Key(pressesOnly, KeyCode.LeftShift) || Key(pressesOnly, KeyCode.C)) v |= In.Dodge;
            if (Key(pressesOnly, KeyCode.H) || Key(pressesOnly, KeyCode.E) || Key(pressesOnly, KeyCode.V)) v |= In.Throw;
            v |= UI.I.TouchInput(pressesOnly);
        }
        else
        {
            if (Input.GetKey(KeyCode.LeftArrow)) v |= In.L;
            if (Input.GetKey(KeyCode.RightArrow)) v |= In.R;
            if (Input.GetKey(KeyCode.UpArrow)) v |= In.U;
            if (Input.GetKey(KeyCode.DownArrow)) v |= In.D;
            if (Key(pressesOnly, KeyCode.RightShift) || Key(pressesOnly, KeyCode.Keypad0)) v |= In.Jump;
            if (Key(pressesOnly, KeyCode.Period) || Key(pressesOnly, KeyCode.Keypad1)) v |= In.Light;
            if (Key(pressesOnly, KeyCode.Slash) || Key(pressesOnly, KeyCode.Keypad2)) v |= In.Heavy;
            if (Key(pressesOnly, KeyCode.Comma) || Key(pressesOnly, KeyCode.Keypad3)) v |= In.Dodge;
            if (Key(pressesOnly, KeyCode.Semicolon) || Key(pressesOnly, KeyCode.Keypad4)) v |= In.Throw;
        }
        v |= Pad(p + 1, pressesOnly);
        return v;
    }

    // dev: a scrappy random player for online soak tests (plain input, so it never touches the sim's RNG)
    int monkeyT, monkeyIn;
    int Monkey()
    {
        if (--monkeyT > 0) return monkeyIn & (In.L | In.R | In.U | In.D);
        monkeyT = UnityEngine.Random.Range(4, 14);
        var c = S.Cur; int me = localSlots.Count > 0 ? localSlots[0] : 0; var a = c.f[me];
        int t = -1, best = int.MaxValue;
        for (int j = 0; j < c.n; j++) { if (j == me || c.f[j].alive == 0) continue; int d = Mathf.Abs(c.f[j].x - a.x); if (d < best) { best = d; t = j; } }
        var sd = Stages.All[c.stage];
        int v = 0;
        if (a.x < sd.L + 500) v |= In.R; else if (a.x > sd.R - 500) v |= In.L;
        else if (t >= 0) v |= c.f[t].x > a.x ? In.R : In.L;
        if (a.y < sd.T - 200 && UnityEngine.Random.value < 0.6f) v |= UnityEngine.Random.value < 0.7f ? In.Jump : In.U | In.Heavy;
        float r = UnityEngine.Random.value;
        if (best < 1400 && r < 0.5f) v |= UnityEngine.Random.value < 0.75f ? In.Light : In.Heavy;
        else if (r < 0.08f) v |= In.Jump;
        else if (r < 0.12f) v |= In.Dodge;
        else if (r < 0.16f) v |= In.Throw;
        monkeyIn = v;
        return v;
    }

    static bool Key(bool down, KeyCode k) => down ? Input.GetKeyDown(k) : Input.GetKey(k);

    // standard-mapping gamepads: A jump, X light, B heavy, Y throw, bumpers/triggers dodge, stick or d-pad to move
    static int Pad(int joy, bool down)
    {
        int v = 0;
        KeyCode B(int b) => (KeyCode)((int)KeyCode.Joystick1Button0 + (joy - 1) * 20 + b);
        bool K(int b) => down ? Input.GetKeyDown(B(b)) : Input.GetKey(B(b));
        if (K(0)) v |= In.Jump;
        if (K(2)) v |= In.Light;
        if (K(1)) v |= In.Heavy;
        if (K(3)) v |= In.Throw;
        if (K(4) || K(5) || K(6) || K(7)) v |= In.Dodge;
        if (!down)
        {
            float x = 0, y = 0;
            try { x = Input.GetAxisRaw("J" + joy + "X"); y = Input.GetAxisRaw("J" + joy + "Y"); } catch { }
            if (x < -0.45f || Input.GetKey(B(14))) v |= In.L;
            if (x > 0.45f || Input.GetKey(B(15))) v |= In.R;
            if (y > 0.5f || Input.GetKey(B(12))) v |= In.U;
            if (y < -0.5f || Input.GetKey(B(13))) v |= In.D;
        }
        return v;
    }

    // ================================================================ online (messages from brawl.js)
    [Serializable] class Head { public string t; }
    [Serializable] public class NetPlayer { public string id, name; public int ch, slot; }
    [Serializable] class StartMsg { public string t; public int stage, you, bots, botLv; public uint seed; public NetPlayer[] players; }
    [Serializable] class InMsg { public string t; public int s, f, c; public int[] k; }
    [Serializable] class HashMsg { public string t; public int s, f; public uint h; }
    [Serializable] class SlotMsg { public string t; public int s; }

    public void OnNet(string json)
    {
        Head h;
        try { h = JsonUtility.FromJson<Head>(json); } catch { return; }
        if (h == null) return;
        switch (h.t)
        {
            case "i":
                if (S != null && M == Mode.Online) { var m = JsonUtility.FromJson<InMsg>(json); S.OnRemoteInput(m.s, m.f, m.k ?? new int[0], m.c, Time.realtimeSinceStartup); }
                break;
            case "h":
                if (S != null && M == Mode.Online) { var m = JsonUtility.FromJson<HashMsg>(json); S.OnRemoteHash(m.f, m.h); }
                break;
            case "start": OnStart(JsonUtility.FromJson<StartMsg>(json)); break;
            case "left":
                if (S != null && M == Mode.Online)
                {
                    var m = JsonUtility.FromJson<SlotMsg>(json);
                    S.OnLeft(m.s);
                    UI.I.Toast(NameOf(m.s) + " LEFT - A CPU TAKES OVER");
                }
                break;
            case "solo":
                UI.I.Toast("NO ONE ONLINE RIGHT NOW - FIGHTING CPUs");
                StartLocal(false);
                break;
            case "end":
                if (M == Mode.Online && !resultsShown) { UI.I.Toast("CONNECTION LOST"); OnlineSlot = -1; Quit(); }
                break;
        }
    }

    void OnStart(StartMsg m)
    {
        int humans = m.players.Length;
        int n = Mathf.Clamp(humans + m.bots, 2, 4);
        var chars = new int[n]; var bots = new int[n]; var names = new string[n];
        foreach (var p in m.players) { chars[p.slot] = Mathf.Clamp(p.ch, 0, Roster.All.Length - 1); names[p.slot] = p.slot == m.you ? "YOU" : p.name; }
        var r = new System.Random((int)(m.seed & 0x7fffffff));
        for (int i = humans; i < n; i++) { chars[i] = r.Next(Roster.All.Length); bots[i] = Mathf.Clamp(m.botLv, 1, 3); names[i] = "CPU " + Roster.All[chars[i]].name; }
        OnlineSlot = m.you;
        var s = new Session(Mathf.Clamp(m.stage, 0, Stages.All.Length - 1), chars, bots, m.seed == 0 ? 1u : m.seed, true, new[] { m.you }, 2);
        s.Send = WebBridge.NetSend;
        s.LogHashes = Dev;
        Begin(s, names, new List<int> { m.you }, Mode.Online);
        UI.I.CloseScreens();
        UI.I.ShowHud(true);
        Sfx.I.Music(true);
        WebBridge.Gameplay(true);
        WebBridge.Event("fight_online", humans);
    }

    [Serializable] public class RankMsg { public int rank, total, wins; public string error; }
    public void OnRank(string json) { var m = JsonUtility.FromJson<RankMsg>(json); if (m != null) UI.I.SetRank(m); }

    public string ShareText()
    {
        var c = S.Cur;
        int me = localSlots.Count > 0 ? localSlots[0] : 0;
        var f = c.f[me];
        string res = c.winner == me ? "WON" : "placed #" + Mathf.Max(1, f.place);
        return "BONK BRAWL: I " + res + " as " + Roster.All[f.ch].name + " with a tower of " + f.hatMax + (f.hatMax == 1 ? " hat" : " hats") + " (" + f.kos + " KOs). Come steal them!";
    }
}
