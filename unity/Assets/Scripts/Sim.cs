using System;
using System.Collections.Generic;

// Deterministic fixed-point maths. No floats anywhere in the simulation.
public static class FM
{
    static readonly int[] COS = {1024,1024,1023,1023,1022,1020,1018,1016,1014,1011,1008,1005,1002,998,994,989,984,979,974,968,962,956,949,943,935,928,920,912,904,896,887,878,868,859,849,839,828,818,807,796,784,773,761,749,737,724,711,698,685,672,658,644,630,616,602,587,573,558,543,527,512,496,481,465,449,433,416,400,384,367,350,333,316,299,282,265,248,230,213,195,178,160,143,125,107,89,71,54,36,18,0,-18,-36,-54,-71,-89,-107,-125,-143,-160,-178,-195,-213,-230,-248,-265,-282,-299,-316,-333,-350,-367,-384,-400,-416,-433,-449,-465,-481,-496,-512,-527,-543,-558,-573,-587,-602,-616,-630,-644,-658,-672,-685,-698,-711,-724,-737,-749,-761,-773,-784,-796,-807,-818,-828,-839,-849,-859,-868,-878,-887,-896,-904,-912,-920,-928,-935,-943,-949,-956,-962,-968,-974,-979,-984,-989,-994,-998,-1002,-1005,-1008,-1011,-1014,-1016,-1018,-1020,-1022,-1023,-1023,-1024,-1024,-1024,-1023,-1023,-1022,-1020,-1018,-1016,-1014,-1011,-1008,-1005,-1002,-998,-994,-989,-984,-979,-974,-968,-962,-956,-949,-943,-935,-928,-920,-912,-904,-896,-887,-878,-868,-859,-849,-839,-828,-818,-807,-796,-784,-773,-761,-749,-737,-724,-711,-698,-685,-672,-658,-644,-630,-616,-602,-587,-573,-558,-543,-527,-512,-496,-481,-465,-449,-433,-416,-400,-384,-367,-350,-333,-316,-299,-282,-265,-248,-230,-213,-195,-178,-160,-143,-125,-107,-89,-71,-54,-36,-18,0,18,36,54,71,89,107,125,143,160,178,195,213,230,248,265,282,299,316,333,350,367,384,400,416,433,449,465,481,496,512,527,543,558,573,587,602,616,630,644,658,672,685,698,711,724,737,749,761,773,784,796,807,818,828,839,849,859,868,878,887,896,904,912,920,928,935,943,949,956,962,968,974,979,984,989,994,998,1002,1005,1008,1011,1014,1016,1018,1020,1022,1023,1023,1024};
    static readonly int[] SIN = {0,18,36,54,71,89,107,125,143,160,178,195,213,230,248,265,282,299,316,333,350,367,384,400,416,433,449,465,481,496,512,527,543,558,573,587,602,616,630,644,658,672,685,698,711,724,737,749,761,773,784,796,807,818,828,839,849,859,868,878,887,896,904,912,920,928,935,943,949,956,962,968,974,979,984,989,994,998,1002,1005,1008,1011,1014,1016,1018,1020,1022,1023,1023,1024,1024,1024,1023,1023,1022,1020,1018,1016,1014,1011,1008,1005,1002,998,994,989,984,979,974,968,962,956,949,943,935,928,920,912,904,896,887,878,868,859,849,839,828,818,807,796,784,773,761,749,737,724,711,698,685,672,658,644,630,616,602,587,573,558,543,527,512,496,481,465,449,433,416,400,384,367,350,333,316,299,282,265,248,230,213,195,178,160,143,125,107,89,71,54,36,18,0,-18,-36,-54,-71,-89,-107,-125,-143,-160,-178,-195,-213,-230,-248,-265,-282,-299,-316,-333,-350,-367,-384,-400,-416,-433,-449,-465,-481,-496,-512,-527,-543,-558,-573,-587,-602,-616,-630,-644,-658,-672,-685,-698,-711,-724,-737,-749,-761,-773,-784,-796,-807,-818,-828,-839,-849,-859,-868,-878,-887,-896,-904,-912,-920,-928,-935,-943,-949,-956,-962,-968,-974,-979,-984,-989,-994,-998,-1002,-1005,-1008,-1011,-1014,-1016,-1018,-1020,-1022,-1023,-1023,-1024,-1024,-1024,-1023,-1023,-1022,-1020,-1018,-1016,-1014,-1011,-1008,-1005,-1002,-998,-994,-989,-984,-979,-974,-968,-962,-956,-949,-943,-935,-928,-920,-912,-904,-896,-887,-878,-868,-859,-849,-839,-828,-818,-807,-796,-784,-773,-761,-749,-737,-724,-711,-698,-685,-672,-658,-644,-630,-616,-602,-587,-573,-558,-543,-527,-512,-496,-481,-465,-449,-433,-416,-400,-384,-367,-350,-333,-316,-299,-282,-265,-248,-230,-213,-195,-178,-160,-143,-125,-107,-89,-71,-54,-36,-18};
    public static int Cos(int deg) { deg %= 360; if (deg < 0) deg += 360; return COS[deg]; }
    public static int Sin(int deg) { deg %= 360; if (deg < 0) deg += 360; return SIN[deg]; }
    public static int Abs(int v) => v < 0 ? -v : v;
    public static int Sign(int v) => v > 0 ? 1 : v < 0 ? -1 : 0;
    public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
    public static int Toward(int v, int target, int step) => v < target ? Math.Min(v + step, target) : Math.Max(v - step, target);
    public static int ISqrt(long v)
    {
        if (v <= 0) return 0;
        long r = (long)Math.Sqrt(v);   // double sqrt is correctly rounded (IEEE), then fixed up exactly
        while (r * r > v) r--;
        while ((r + 1) * (r + 1) <= v) r++;
        return (int)r;
    }
}

public struct Fighter
{
    public int x, y, vx, vy, face;
    public int dmg, stocks, st, stT, move, moveT, charge;
    public int jumps, ground, plat, hitstun, iframes, hitstop, dodgeCd, airDodge, recov;
    public int weapon, ammo, respawnT, dropT, hitMask, lastHitter, lastHitT;
    public int kos, falls, dealt, prevIn, curIn, alive, place, ch, bot, botT, botIn, botHold, flash, fastFall, landT, dodgeDur, spawnT, launch;
}

public struct Item { public int kind, x, y, vx, vy, t, owner, thrown, ground; }
public struct Shot { public int alive, x, y, vx, vy, t, owner, dmg, kb, grow, hw, hh, kind; }

public struct Ev { public int frame, type, a, b, x, y, v; }

public class SimState
{
    public const int MaxF = 4, MaxI = 8, MaxS = 24;
    public int frame, n, over, winner, itemT, stage, overFrame;
    public uint rng;
    public Fighter[] f = new Fighter[MaxF];
    public Item[] it = new Item[MaxI];
    public Shot[] sh = new Shot[MaxS];

    public void CopyFrom(SimState o)
    {
        frame = o.frame; n = o.n; over = o.over; winner = o.winner; itemT = o.itemT; stage = o.stage; overFrame = o.overFrame; rng = o.rng;
        Array.Copy(o.f, f, MaxF); Array.Copy(o.it, it, MaxI); Array.Copy(o.sh, sh, MaxS);
    }

    public int Rand(int n)
    {
        rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
        return (int)(rng % (uint)n);
    }

    public uint Hash()
    {
        uint h = 2166136261;
        void H(int v) { unchecked { h = (h ^ (uint)v) * 16777619; } }
        H(frame); H(over); H(winner); H((int)rng);
        for (int i = 0; i < n; i++) { var a = f[i]; H(a.x); H(a.y); H(a.vx); H(a.vy); H(a.dmg); H(a.stocks); H(a.st); H(a.moveT); H(a.weapon); }
        for (int i = 0; i < MaxI; i++) { H(it[i].kind); H(it[i].x); H(it[i].y); }
        return h;
    }
}

public static class Sim
{
    public const int Go = 180;                   // countdown frames before the fight starts
    public const int TimeLimit = 60 * 60 * 4;    // 4 minutes
    public const int HalfW = 380, Height = 1300;
    public const int St_Ground = 0, St_Air = 1, St_Attack = 2, St_Hitstun = 3, St_Dodge = 4, St_Dead = 5, St_Spawn = 6, St_Land = 7;
    public const int Ev_Hit = 1, Ev_KO = 2, Ev_Jump = 3, Ev_Land = 4, Ev_Swing = 5, Ev_Dodge = 6, Ev_Pickup = 7, Ev_Throw = 8, Ev_Boom = 9,
        Ev_Shot = 10, Ev_Spawn = 11, Ev_Item = 12, Ev_Break = 13, Ev_Over = 14, Ev_Go = 15, Ev_Elim = 16;

    public static readonly List<Ev> Events = new List<Ev>();
    static void E(SimState s, int type, int a, int b, int x, int y, int v = 0) => Events.Add(new Ev { frame = s.frame, type = type, a = a, b = b, x = x, y = y, v = v });

    // ------------------------------------------------------------------ setup
    public static void Setup(SimState s, int stage, int[] chars, int[] bots, uint seed, int stocks = 3)
    {
        s.frame = 0; s.over = 0; s.winner = -1; s.stage = stage; s.n = chars.Length; s.overFrame = 0;
        s.rng = seed == 0 ? 0x9E3779B9u : seed;
        s.itemT = Go + 360;
        var sd = Stages.All[stage];
        for (int i = 0; i < SimState.MaxF; i++) s.f[i] = default;
        for (int i = 0; i < SimState.MaxI; i++) s.it[i] = default;
        for (int i = 0; i < SimState.MaxS; i++) s.sh[i] = default;
        for (int i = 0; i < s.n; i++)
        {
            ref var a = ref s.f[i];
            a.ch = chars[i]; a.bot = bots[i]; a.alive = 1; a.stocks = stocks; a.lastHitter = -1;
            a.x = sd.spawn[i]; a.y = sd.T; a.face = a.x < 0 ? 1 : -1; a.st = St_Ground; a.ground = 1; a.plat = 0;
            a.jumps = 2; a.move = -1;
            if (sd.spawn[i] < sd.L || sd.spawn[i] > sd.R) { a.y = Soft(sd, a.x); a.plat = SoftIndex(sd, a.x) + 1; }
        }
    }

    static int Soft(StageDef sd, int x) { for (int k = 0; k < sd.soft.Length; k += 3) if (x >= sd.soft[k] && x <= sd.soft[k + 1]) return sd.soft[k + 2]; return sd.T; }
    static int SoftIndex(StageDef sd, int x) { for (int k = 0; k < sd.soft.Length; k += 3) if (x >= sd.soft[k] && x <= sd.soft[k + 1]) return k / 3; return -1; }

    // ------------------------------------------------------------------ step
    public static void Step(SimState s, int[] input)
    {
        s.frame++;
        if (s.frame == Go) E(s, Ev_Go, 0, 0, 0, 0);
        bool live = s.frame >= Go && s.over == 0;
        var sd = Stages.All[s.stage];
        for (int i = 0; i < s.n; i++)
        {
            ref var a = ref s.f[i];
            int inp = a.bot > 0 ? Bot(s, i) : input[i];
            if (!live) inp = 0;
            a.curIn = inp;
        }
        for (int i = 0; i < s.n; i++) if (live || s.frame < Go) UpdateFighter(s, i, sd, live);
        if (live)
        {
            Hits(s);
            Shots(s, sd);
            Items(s, sd);
            SpawnItems(s, sd);
            CheckOver(s);
        }
        else if (s.over > 0) { for (int i = 0; i < s.n; i++) Settle(s, i, sd); }
        for (int i = 0; i < s.n; i++) s.f[i].prevIn = s.f[i].curIn;
    }

    // after GAME!, everyone just drifts to a stop
    static void Settle(SimState s, int i, StageDef sd)
    {
        ref var a = ref s.f[i];
        if (a.st == St_Dead) return;
        a.vx = a.vx * 9 / 10;
        if (a.ground == 0) a.vy = Math.Max(a.vy - 6, -150);
        Move(s, i, sd);
    }

    static void UpdateFighter(SimState s, int i, StageDef sd, bool live)
    {
        ref var a = ref s.f[i];
        if (a.alive == 0) return;
        var d = Roster.All[a.ch];
        int inp = a.curIn, press = inp & ~a.prevIn;
        int dirX = ((inp & In.R) != 0 ? 1 : 0) - ((inp & In.L) != 0 ? 1 : 0);
        if (a.flash > 0) a.flash--;
        if (a.iframes > 0) a.iframes--;
        if (a.dodgeCd > 0) a.dodgeCd--;
        if (a.dropT > 0) a.dropT--;
        if (a.hitstop > 0) { a.hitstop--; return; }
        if (!live) return;
        a.stT++;

        int run = 108 * d.speed / 100, airSpd = 96 * d.speed / 100, grav = Math.Max(5, d.grav - sd.gravMinus);
        int j1 = 218 * d.jump / 100, j2 = 196 * d.jump / 100;

        switch (a.st)
        {
            case St_Dead:
                if (a.respawnT > 0 && --a.respawnT == 0)
                {
                    a.st = St_Spawn; a.stT = 0; a.x = 0; a.y = sd.T + 6200; a.vx = a.vy = 0; a.dmg = 0; a.iframes = 170; a.spawnT = 130;
                    a.jumps = 2; a.airDodge = 0; a.recov = 0; a.weapon = 0; a.ground = 0; a.plat = -1;
                    E(s, Ev_Spawn, i, 0, a.x, a.y);
                }
                return;
            case St_Spawn:
                if (--a.spawnT <= 0 || (press & ~0) != 0) { a.st = St_Air; a.stT = 0; }
                else { a.vx = 0; a.vy = 0; return; }
                break;
        }

        // ---- actions available when free
        bool free = a.st == St_Ground || a.st == St_Air;
        if (free)
        {
            if (dirX != 0 && a.st == St_Ground) a.face = dirX;
            if ((press & In.Throw) != 0) { ThrowOrPick(s, i); }
            else if ((press & In.Dodge) != 0 && a.dodgeCd == 0 && (a.ground == 1 || a.airDodge == 0)) StartDodge(s, i, inp);
            else if ((press & (In.Light | In.Heavy)) != 0) StartAttack(s, i, inp, (press & In.Heavy) != 0);
            else if ((press & In.Jump) != 0)
            {
                if (a.ground == 1) { a.vy = j1; a.ground = 0; a.st = St_Air; a.jumps = 2; a.plat = -1; a.fastFall = 0; E(s, Ev_Jump, i, 0, a.x, a.y); }
                else if (a.jumps > 0) { a.vy = j2; a.jumps--; a.fastFall = 0; E(s, Ev_Jump, i, 1, a.x, a.y); }
            }
        }
        else if (a.st == St_Land && a.stT >= a.landT) { a.st = St_Ground; a.stT = 0; }

        // ---- per-state motion
        switch (a.st)
        {
            case St_Ground:
            case St_Land:
                if (a.st == St_Ground && dirX != 0) a.vx = FM.Toward(a.vx, dirX * run, 22);
                else a.vx = FM.Toward(a.vx, 0, 20);
                // drop through a soft platform
                if (a.st == St_Ground && a.plat > 0 && (press & In.D) != 0) { a.dropT = 12; a.ground = 0; a.st = St_Air; a.y -= 2; a.plat = -1; a.jumps = 2; }
                break;
            case St_Air:
                if (dirX != 0) a.vx = FM.Toward(a.vx, dirX * airSpd, 10); else a.vx = FM.Toward(a.vx, 0, 3);
                if ((press & In.D) != 0 && a.vy < 60) a.fastFall = 1;
                Gravity(ref a, grav);
                break;
            case St_Attack:
                AttackTick(s, i, inp, press, dirX, grav, airSpd);
                break;
            case St_Hitstun:
                a.vx = a.vx * 97 / 100 + dirX * 2;
                a.vy = a.vy * 97 / 100;
                a.vy -= grav;
                if (a.vy < -190) a.vy = -190;
                if (--a.hitstun <= 0) { a.st = a.ground == 1 ? St_Ground : St_Air; a.stT = 0; a.launch = 0; }
                break;
            case St_Dodge:
                if (a.ground == 0)
                {
                    if (a.stT >= a.dodgeDur) { a.vx /= 3; a.vy /= 3; a.st = St_Air; a.stT = 0; }
                }
                else
                {
                    a.vx = FM.Toward(a.vx, 0, a.dodgeDur > 16 ? 0 : 9);
                    if (a.stT >= a.dodgeDur) { a.st = St_Ground; a.stT = 0; a.vx = 0; }
                }
                break;
        }

        Move(s, i, sd);

        // ---- blast zones
        if (a.x < -sd.BX || a.x > sd.BX || a.y < sd.BB || a.y > sd.BT) KO(s, i);
    }

    static void Gravity(ref Fighter a, int grav)
    {
        a.vy -= grav;
        int max = a.fastFall == 1 ? -250 : -165;
        if (a.fastFall == 1 && a.vy > -250) a.vy = -250;
        if (a.vy < max) a.vy = max;
    }

    // ------------------------------------------------------------------ movement + stage collision
    static void Move(SimState s, int i, StageDef sd)
    {
        ref var a = ref s.f[i];
        int px = a.x, py = a.y;
        a.x += a.vx; a.y += a.vy;
        bool wasGround = a.ground == 1;
        // main platform: solid block
        bool overMain = a.x >= sd.L && a.x <= sd.R;
        if (py >= sd.T && a.y < sd.T && overMain && a.st != St_Dead) Land(s, i, sd.T, 0);
        else if (a.y < sd.T && a.y + Height > sd.B)
        {
            // pushed out of the sides
            if (a.x + HalfW > sd.L && a.x - HalfW < sd.R)
            {
                if (py + Height <= sd.B) { a.y = sd.B - Height; if (a.vy > 0) a.vy = 0; }
                else if (px <= sd.L) { a.x = sd.L - HalfW; if (a.vx > 0) a.vx = 0; }
                else if (px >= sd.R) { a.x = sd.R + HalfW; if (a.vx < 0) a.vx = 0; }
            }
        }
        // soft platforms: land from above only
        if (a.vy <= 0 && a.dropT == 0 && a.st != St_Dead)
            for (int k = 0; k < sd.soft.Length; k += 3)
            {
                int top = sd.soft[k + 2];
                if (py >= top && a.y < top && a.x >= sd.soft[k] && a.x <= sd.soft[k + 1]) { Land(s, i, top, k / 3 + 1); break; }
            }
        // walked off an edge?
        if (wasGround && a.ground == 1)
        {
            bool still = a.plat == 0 ? (a.x >= sd.L && a.x <= sd.R && a.y == sd.T) : a.plat > 0 && a.x >= sd.soft[(a.plat - 1) * 3] && a.x <= sd.soft[(a.plat - 1) * 3 + 1];
            if (!still)
            {
                a.ground = 0; a.plat = -1;
                if (a.st == St_Ground || a.st == St_Land) { a.st = St_Air; a.stT = 0; }
            }
        }
    }

    static void Land(SimState s, int i, int top, int plat)
    {
        ref var a = ref s.f[i];
        a.y = top; a.plat = plat;
        if (a.st == St_Hitstun && a.vy < -150 && a.hitstun > 6)
        {
            // hard landing while launched: bounce
            a.vy = -a.vy * 35 / 100; a.vx = a.vx * 70 / 100; a.y = top + 1;
            return;
        }
        a.vy = 0;
        bool was = a.ground == 1;
        a.ground = 1; a.jumps = 2; a.airDodge = 0; a.recov = 0; a.fastFall = 0;
        if (was) return;
        if (a.st == St_Attack)
        {
            var m = Moves.Get(a.weapon, a.move);
            if (m.air || m.meteor)
            {
                a.st = St_Land; a.stT = 0; a.landT = m.meteor ? 16 : 7; a.move = -1;
            }
        }
        else if (a.st == St_Air) { a.st = St_Land; a.stT = 0; a.landT = 3; }
        else if (a.st == St_Hitstun) { a.st = St_Land; a.stT = 0; a.landT = 10; a.hitstun = 0; }
        else if (a.st == St_Dodge) { a.st = St_Ground; a.stT = 0; }
        E(s, Ev_Land, i, 0, a.x, a.y);
    }

    // ------------------------------------------------------------------ attacks
    static void StartAttack(SimState s, int i, int inp, bool heavy)
    {
        ref var a = ref s.f[i];
        int dirX = ((inp & In.R) != 0 ? 1 : 0) - ((inp & In.L) != 0 ? 1 : 0);
        bool up = (inp & In.U) != 0, down = (inp & In.D) != 0, air = a.ground == 0;
        if (a.weapon == Weapon.Grenade) { ThrowOrPick(s, i); return; }
        int m;
        if (a.weapon == Weapon.Blaster && !down && !up) m = heavy ? Moves.Blast : Moves.Shoot;
        else if (!heavy)
        {
            if (air) m = down ? Moves.DAir : up ? Moves.UAir : dirX != 0 ? Moves.SAir : Moves.NAir;
            else m = down ? Moves.DLight : dirX != 0 ? Moves.SLight : up ? Moves.ULight : Moves.Jab;
        }
        else
        {
            if (air) m = down ? Moves.DAirHeavy : (up && a.recov == 0) ? Moves.UHeavy : Moves.SHeavy;
            else m = down ? Moves.DHeavy : up ? Moves.UHeavy : Moves.SHeavy;
        }
        if (dirX != 0) a.face = dirX;
        if (m == Moves.UHeavy) a.recov = 1;
        a.st = St_Attack; a.stT = 0; a.move = m; a.moveT = 0; a.charge = 0; a.hitMask = 0;
        E(s, Ev_Swing, i, m, a.x, a.y, a.weapon);
    }

    static void AttackTick(SimState s, int i, int inp, int press, int dirX, int grav, int airSpd)
    {
        ref var a = ref s.f[i];
        var m = Moves.Get(a.weapon, a.move);
        // charge heavies while the button is held (up to 30 frames)
        if (m.heavy && a.moveT == 1 && (inp & In.Heavy) != 0 && a.charge < 30) { a.charge++; if (a.ground == 1) a.vx = FM.Toward(a.vx, 0, 20); else { a.vy = Math.Max(a.vy - 3, -60); } return; }
        a.moveT++;
        if (a.moveT == m.startup)
        {
            if (m.selfVx != 0) a.vx = a.face * m.selfVx;
            if (m.selfVy != 0) { a.vy = m.selfVy; a.ground = 0; a.plat = -1; }
            if (m.meteor) { a.vy = -270; a.vx = a.vx / 2; }
            if (m.shot > 0) FireShot(s, i, m);
        }
        if (a.ground == 1) a.vx = FM.Toward(a.vx, 0, m.selfVx != 0 && a.moveT < m.startup + m.active ? 3 : 14);
        else
        {
            if (!m.meteor || a.moveT < m.startup)
            {
                if (dirX != 0) a.vx = FM.Toward(a.vx, dirX * airSpd, 5);
                if (!(m.selfVy != 0 && a.moveT < m.startup + m.active)) Gravity(ref a, grav);
            }
        }
        if (m.meteor && a.moveT >= m.startup + m.active) { a.move = -1; a.st = St_Air; a.stT = 0; return; }
        if (a.moveT >= m.total) { a.move = -1; a.st = a.ground == 1 ? St_Ground : St_Air; a.stT = 0; }
    }

    static void FireShot(SimState s, int i, MoveDef m)
    {
        ref var a = ref s.f[i];
        if (a.ammo <= 0) return;
        int slot = -1;
        for (int k = 0; k < SimState.MaxS; k++) if (s.sh[k].alive == 0) { slot = k; break; }
        if (slot < 0) return;
        int chargeMul = 100 + a.charge * 45 / 30;
        s.sh[slot] = new Shot
        {
            alive = 1, x = a.x + a.face * m.hx, y = a.y + m.hy, vx = a.face * (m.shot == 2 ? 330 : 430), vy = 0, t = 0, owner = i,
            dmg = m.dmg * chargeMul / 100, kb = m.kbBase * chargeMul / 100, grow = m.kbGrow, hw = m.hw, hh = m.hh, kind = m.shot,
        };
        a.ammo -= m.shot == 2 ? 3 : 1;
        E(s, Ev_Shot, i, m.shot, s.sh[slot].x, s.sh[slot].y);
        if (a.ammo <= 0) { a.weapon = 0; E(s, Ev_Break, i, Weapon.Blaster, a.x, a.y); }
    }

    // ------------------------------------------------------------------ dodges
    static void StartDodge(SimState s, int i, int inp)
    {
        ref var a = ref s.f[i];
        int dx = ((inp & In.R) != 0 ? 1 : 0) - ((inp & In.L) != 0 ? 1 : 0);
        int dy = ((inp & In.U) != 0 ? 1 : 0) - ((inp & In.D) != 0 ? 1 : 0);
        a.st = St_Dodge; a.stT = 0; a.move = -1;
        if (a.ground == 1)
        {
            if (dx != 0) { a.vx = dx * 175; a.dodgeDur = 17; a.iframes = 15; }
            else { a.vx = 0; a.dodgeDur = 20; a.iframes = 17; }
            a.dodgeCd = 42;
        }
        else
        {
            int sp = dx != 0 && dy != 0 ? 136 : 190;
            a.vx = dx * sp; a.vy = dy * sp; a.dodgeDur = 16; a.iframes = 14; a.airDodge = 1; a.dodgeCd = 26; a.fastFall = 0;
        }
        E(s, Ev_Dodge, i, 0, a.x, a.y);
    }

    // ------------------------------------------------------------------ hits
    static bool Overlap(int ax0, int ay0, int ax1, int ay1, int bx0, int by0, int bx1, int by1) => ax0 < bx1 && ax1 > bx0 && ay0 < by1 && ay1 > by0;

    static void Hits(SimState s)
    {
        for (int i = 0; i < s.n; i++)
        {
            ref var a = ref s.f[i];
            if (a.st != St_Attack || a.move < 0 || a.hitstop > 0) continue;
            var m = Moves.Get(a.weapon, a.move);
            if (m.shot > 0) continue;
            if (a.moveT < m.startup || a.moveT >= m.startup + m.active) continue;
            int cx = a.x + a.face * m.hx, cy = a.y + m.hy;
            for (int j = 0; j < s.n; j++)
            {
                if (j == i || (a.hitMask & (1 << j)) != 0) continue;
                ref var b = ref s.f[j];
                if (b.alive == 0 || b.st == St_Dead || b.st == St_Spawn || b.iframes > 0) continue;
                if (!Overlap(cx - m.hw, cy - m.hh, cx + m.hw, cy + m.hh, b.x - HalfW, b.y, b.x + HalfW, b.y + Height)) continue;
                a.hitMask |= 1 << j;
                int chargeMul = 100 + a.charge * 45 / 30;
                int dir = m.bothSides ? (b.x >= a.x ? 1 : -1) : a.face;
                ApplyHit(s, i, j, m.dmg * chargeMul / 100, m.kbBase * chargeMul / 100, m.kbGrow, m.angle, dir, a.weapon, m.heavy);
            }
        }
    }

    static void ApplyHit(SimState s, int att, int j, int dmg, int kbBase, int grow, int angle, int dir, int weapon, bool heavy)
    {
        ref var b = ref s.f[j];
        b.dmg = Math.Min(999, b.dmg + dmg);
        int kb = kbBase + b.dmg * grow * 118 / 1000;
        kb = kb * 100 / Roster.All[b.ch].weight;
        int vx = dir * FM.Cos(angle) * kb / 1024, vy = FM.Sin(angle) * kb / 1024;
        Launch(s, att, j, vx, vy, kb, dmg, weapon, heavy);
    }

    static void Launch(SimState s, int att, int j, int vx, int vy, int kb, int dmg, int weapon, bool heavy)
    {
        ref var b = ref s.f[j];
        if (b.ground == 1 && vy <= 0 && kb > 110) vy = 40 - vy / 2;   // big hits pop you off the floor (spikes bounce)
        b.vx = vx; b.vy = vy;
        b.st = St_Hitstun; b.stT = 0; b.move = -1; b.hitstun = 7 + kb / 8; b.fastFall = 0;
        if (vy > 0) { b.ground = 0; b.plat = -1; }
        int stop = 3 + dmg / 3 + (heavy ? 2 : 0);
        b.hitstop = stop; b.flash = 8; b.launch = kb;
        b.airDodge = 0;
        if (att >= 0 && att != j)
        {
            s.f[att].hitstop = stop;
            s.f[att].dealt += dmg;
            b.lastHitter = att; b.lastHitT = s.frame;
        }
        E(s, Ev_Hit, att, j, b.x, b.y + Height / 2, kb * 16 + Math.Min(15, weapon + (heavy ? 8 : 0)));
    }

    // ------------------------------------------------------------------ KO
    static void KO(SimState s, int i)
    {
        ref var a = ref s.f[i];
        E(s, Ev_KO, i, a.lastHitter, a.x, a.y);
        a.stocks--; a.falls++;
        if (a.lastHitter >= 0 && a.lastHitter != i && s.frame - a.lastHitT < 540) s.f[a.lastHitter].kos++;
        a.st = St_Dead; a.stT = 0; a.vx = a.vy = 0; a.weapon = 0; a.lastHitter = -1; a.hitstop = 0; a.launch = 0;
        if (a.stocks <= 0)
        {
            int left = 0; for (int k = 0; k < s.n; k++) if (s.f[k].alive == 1 && s.f[k].stocks > 0) left++;
            a.place = left + 1; a.alive = 0; a.respawnT = 0;
            E(s, Ev_Elim, i, a.place, a.x, a.y);
        }
        else a.respawnT = 100;
    }

    static void CheckOver(SimState s)
    {
        int left = 0, last = -1;
        for (int i = 0; i < s.n; i++) if (s.f[i].alive == 1 && s.f[i].stocks > 0) { left++; last = i; }
        bool timeUp = s.frame >= Go + TimeLimit;
        if (left <= 1 || timeUp)
        {
            if (timeUp && left > 1)
            {
                // most stocks, then least damage
                last = -1;
                for (int i = 0; i < s.n; i++)
                {
                    if (s.f[i].alive == 0) continue;
                    if (last < 0 || s.f[i].stocks > s.f[last].stocks || (s.f[i].stocks == s.f[last].stocks && s.f[i].dmg < s.f[last].dmg)) last = i;
                }
                // rank the rest
                for (int p = 2; p <= s.n; p++)
                {
                    int best = -1;
                    for (int i = 0; i < s.n; i++)
                    {
                        if (i == last || s.f[i].alive == 0 || s.f[i].place > 0) continue;
                        if (best < 0 || s.f[i].stocks > s.f[best].stocks || (s.f[i].stocks == s.f[best].stocks && s.f[i].dmg < s.f[best].dmg)) best = i;
                    }
                    if (best >= 0) s.f[best].place = p;
                }
            }
            s.over = 1; s.winner = last; s.overFrame = s.frame;
            if (last >= 0) s.f[last].place = 1;
            E(s, Ev_Over, last, 0, 0, 0);
        }
    }

    // ------------------------------------------------------------------ weapons + items
    static void ThrowOrPick(SimState s, int i)
    {
        ref var a = ref s.f[i];
        if (a.weapon > 0)
        {
            int slot = FreeItem(s);
            if (slot >= 0)
            {
                int up = (a.curIn & In.U) != 0 ? 1 : 0, down = (a.curIn & In.D) != 0 ? 1 : 0;
                s.it[slot] = new Item
                {
                    kind = a.weapon, x = a.x + a.face * 400, y = a.y + 900, owner = i, thrown = 1, t = 0,
                    vx = a.face * (up == 1 ? 180 : down == 1 ? 200 : 330), vy = up == 1 ? 260 : down == 1 ? -220 : 70,
                };
                E(s, Ev_Throw, i, a.weapon, a.x, a.y);
            }
            a.weapon = 0; a.ammo = 0;
            return;
        }
        int best = -1, bd = 1100;
        for (int k = 0; k < SimState.MaxI; k++)
        {
            ref var it = ref s.it[k];
            if (it.kind == 0 || (it.thrown == 1 && it.owner == i && it.t < 20)) continue;
            int dd = FM.Abs(it.x - a.x) + FM.Abs(it.y - (a.y + 400)) / 2;
            if (dd < bd) { bd = dd; best = k; }
        }
        if (best < 0) return;
        a.weapon = s.it[best].kind; a.ammo = a.weapon == Weapon.Blaster ? 12 : 0;
        E(s, Ev_Pickup, i, a.weapon, a.x, a.y);
        s.it[best] = default;
    }

    static int FreeItem(SimState s) { for (int k = 0; k < SimState.MaxI; k++) if (s.it[k].kind == 0) return k; return -1; }

    static void Items(SimState s, StageDef sd)
    {
        for (int k = 0; k < SimState.MaxI; k++)
        {
            ref var it = ref s.it[k];
            if (it.kind == 0) continue;
            it.t++;
            int py = it.y;
            it.x += it.vx; it.y += it.vy;
            if (it.ground == 0) { it.vy -= it.thrown == 1 ? 6 : 4; if (it.vy < -150) it.vy = -150; }
            // land on platforms
            if (it.vy <= 0)
            {
                bool landed = false;
                if (py >= sd.T && it.y < sd.T && it.x >= sd.L && it.x <= sd.R) { it.y = sd.T; landed = true; }
                for (int q = 0; q < sd.soft.Length && !landed; q += 3)
                    if (py >= sd.soft[q + 2] && it.y < sd.soft[q + 2] && it.x >= sd.soft[q] && it.x <= sd.soft[q + 1]) { it.y = sd.soft[q + 2]; landed = true; }
                if (landed) { it.vy = 0; it.vx = it.vx / 3; it.ground = 1; if (it.kind == Weapon.Grenade && it.thrown == 1) { Explode(s, k); continue; } it.thrown = 0; }
            }
            if (it.ground == 1) it.vx = FM.Toward(it.vx, 0, 12);
            // thrown weapons hit whoever they touch
            if (it.thrown == 1)
            {
                for (int j = 0; j < s.n; j++)
                {
                    if (j == it.owner) continue;
                    ref var b = ref s.f[j];
                    if (b.alive == 0 || b.st == St_Dead || b.st == St_Spawn || b.iframes > 0) continue;
                    if (!Overlap(it.x - 350, it.y - 300, it.x + 350, it.y + 300, b.x - HalfW, b.y, b.x + HalfW, b.y + Height)) continue;
                    if (it.kind == Weapon.Grenade) { Explode(s, k); break; }
                    ApplyHit(s, it.owner, j, 8, 62, 7, 40, it.vx >= 0 ? 1 : -1, it.kind, false);
                    it.thrown = 0; it.vx = -it.vx / 4; it.vy = 90;
                    break;
                }
                if (it.kind == Weapon.Grenade && it.kind != 0 && it.t > 80) { Explode(s, k); continue; }
                if (it.t > 50 && it.kind != Weapon.Grenade) it.thrown = 0;
            }
            if (it.kind != 0 && (it.y < sd.BB || it.x < -sd.BX || it.x > sd.BX)) it = default;
        }
    }

    static void Explode(SimState s, int k)
    {
        ref var it = ref s.it[k];
        int ex = it.x, ey = it.y + 300, owner = it.owner;
        E(s, Ev_Boom, owner, 0, ex, ey);
        it = default;
        for (int j = 0; j < s.n; j++)
        {
            ref var b = ref s.f[j];
            if (b.alive == 0 || b.st == St_Dead || b.st == St_Spawn || b.iframes > 0) continue;
            int dx = b.x - ex, dy = b.y + Height / 2 - ey;
            long d2 = (long)dx * dx + (long)dy * dy;
            if (d2 > 2000L * 2000L) continue;
            int len = Math.Max(1, FM.ISqrt(d2));
            dy += 900;   // explosions lift
            int len2 = Math.Max(1, FM.ISqrt((long)dx * dx + (long)dy * dy));
            b.dmg = Math.Min(999, b.dmg + 16);
            int kb = (96 + b.dmg * 17 / 10) * 100 / Roster.All[b.ch].weight;
            Launch(s, owner, j, dx * kb / len2, dy * kb / len2, kb, 16, Weapon.Grenade, true);
        }
    }

    static void Shots(SimState s, StageDef sd)
    {
        for (int k = 0; k < SimState.MaxS; k++)
        {
            ref var p = ref s.sh[k];
            if (p.alive == 0) continue;
            p.t++; p.x += p.vx; p.y += p.vy;
            if (p.t > 70 || p.x < -sd.BX || p.x > sd.BX) { p.alive = 0; continue; }
            for (int j = 0; j < s.n; j++)
            {
                if (j == p.owner) continue;
                ref var b = ref s.f[j];
                if (b.alive == 0 || b.st == St_Dead || b.st == St_Spawn || b.iframes > 0) continue;
                if (!Overlap(p.x - p.hw, p.y - p.hh, p.x + p.hw, p.y + p.hh, b.x - HalfW, b.y, b.x + HalfW, b.y + Height)) continue;
                ApplyHit(s, p.owner, j, p.dmg, p.kb, p.grow, p.kind == 2 ? 32 : 18, p.vx >= 0 ? 1 : -1, Weapon.Blaster, p.kind == 2);
                p.alive = 0;
                break;
            }
        }
    }

    static void SpawnItems(SimState s, StageDef sd)
    {
        if (s.frame < s.itemT) return;
        int count = 0; for (int k = 0; k < SimState.MaxI; k++) if (s.it[k].kind != 0) count++;
        s.itemT = s.frame + 400 + s.Rand(320);
        if (count >= 3) return;
        int slot = FreeItem(s); if (slot < 0) return;
        int roll = s.Rand(100);
        int kind = roll < 24 ? Weapon.Sword : roll < 44 ? Weapon.Spear : roll < 64 ? Weapon.Pan : roll < 84 ? Weapon.Blaster : Weapon.Grenade;
        int x = sd.L + 900 + s.Rand(sd.R - sd.L - 1800);
        s.it[slot] = new Item { kind = kind, x = x, y = sd.T + 9000, vx = 0, vy = -30, owner = -1 };
        E(s, Ev_Item, kind, slot, x, sd.T);
    }

    // ------------------------------------------------------------------ deterministic bots
    // bot level 1..3 (easy..hard); decisions only use the shared RNG, so every peer computes the same inputs.
    static int Bot(SimState s, int i)
    {
        ref var a = ref s.f[i];
        var sd = Stages.All[s.stage];
        int lv = a.bot;
        if (a.st == St_Dead || a.st == St_Spawn) { a.botIn = 0; return 0; }
        int held = a.botIn & (In.L | In.R | In.U | In.D);
        if (a.botHold > 0) { a.botHold--; return a.botIn; }
        if (--a.botT > 0) return held;
        a.botT = lv == 1 ? 14 + s.Rand(10) : lv == 2 ? 7 + s.Rand(6) : 3 + s.Rand(3);
        int input = 0;

        // target: nearest standing opponent
        int t = -1, td = int.MaxValue;
        for (int j = 0; j < s.n; j++)
        {
            if (j == i || s.f[j].alive == 0 || s.f[j].st == St_Dead) continue;
            int d = FM.Abs(s.f[j].x - a.x) + FM.Abs(s.f[j].y - a.y);
            if (d < td) { td = d; t = j; }
        }

        // recovery first
        bool off = a.x < sd.L - 100 || a.x > sd.R + 100 || (a.y < sd.T - 50 && a.ground == 0);
        if (off && a.st != St_Hitstun)
        {
            int toward = a.x < 0 ? In.R : In.L;
            input |= toward;
            if (a.vy < 20 && a.y < sd.T + 1800)
            {
                if (a.jumps > 0 && (a.prevIn & In.Jump) == 0) input |= In.Jump;
                else if (a.recov == 0 && a.y < sd.T - 300) input |= In.U | In.Heavy;
                else if (a.airDodge == 0 && a.y < sd.T - 2200 && a.dodgeCd == 0) input |= In.Dodge | In.U;
            }
            a.botT = 2;
            a.botIn = input;
            return input;
        }
        if (a.st == St_Hitstun) { a.botIn = a.x < 0 ? In.R : In.L; return a.botIn; }
        if (t < 0) { a.botIn = 0; return 0; }
        ref var b = ref s.f[t];
        int dx = b.x - a.x, dy = b.y - a.y;
        int adx = FM.Abs(dx);

        // grab a weapon lying nearby
        if (a.weapon == 0)
        {
            for (int k = 0; k < SimState.MaxI; k++)
            {
                var it = s.it[k];
                if (it.kind == 0 || it.thrown == 1 || it.ground == 0) continue;
                int idx = it.x - a.x;
                if (FM.Abs(idx) < 3200 && FM.Abs(it.y - a.y) < 800 && (adx > 2500 || lv == 1))
                {
                    if (FM.Abs(idx) < 700 && (a.prevIn & In.Throw) == 0) { a.botIn = In.Throw; return In.Throw; }
                    a.botIn = idx > 0 ? In.R : In.L; return a.botIn;
                }
            }
        }

        // defend: dodge an incoming heavy
        if (b.st == St_Attack && b.moveT < 8 && adx < 2000 && FM.Abs(dy) < 1500 && a.dodgeCd == 0)
        {
            int chance = lv == 1 ? 8 : lv == 2 ? 30 : 55;
            if (s.Rand(100) < chance) { input = In.Dodge | (s.Rand(2) == 0 ? (dx > 0 ? In.L : In.R) : 0); a.botIn = input; return input; }
        }

        int face = dx >= 0 ? In.R : In.L;
        // ranged weapons
        if (a.weapon == Weapon.Blaster && FM.Abs(dy) < 700 && adx < 9000 && (a.prevIn & In.Light) == 0)
        {
            a.botIn = face | In.Light; a.botT = 10; return a.botIn;
        }
        if (a.weapon == Weapon.Grenade && adx < 5500 && adx > 1200) { a.botIn = face | In.Throw; return a.botIn; }

        int reach = a.weapon == Weapon.Spear ? 1600 : a.weapon == Weapon.Sword || a.weapon == Weapon.Pan ? 1250 : 1000;
        bool free = a.st == St_Ground || a.st == St_Air;
        if (adx < reach + 300 && FM.Abs(dy) < 1300 && free && (a.prevIn & (In.Light | In.Heavy)) == 0)
        {
            int kill = lv == 3 ? 85 : lv == 2 ? 110 : 150;
            bool heavy = b.dmg >= kill ? s.Rand(100) < 70 : s.Rand(100) < (lv == 1 ? 15 : 22);
            if (a.ground == 0)
                input = dy > 500 ? In.U : dy < -500 ? In.D : face;
            else
                input = dy > 900 ? In.U : face;
            input |= heavy ? In.Heavy : In.Light;
            if (heavy) a.botHold = lv == 3 ? 4 + s.Rand(10) : s.Rand(8);
            a.botIn = input;
            return input;
        }
        // approach
        if (adx > reach - 200) input |= face;
        else if (adx < 300 && s.Rand(3) == 0) input |= dx > 0 ? In.L : In.R;    // make some space
        if (dy > 1400 && adx < 3500 && (a.prevIn & In.Jump) == 0 && (a.ground == 1 || a.jumps > 0) && s.Rand(100) < 55) input |= In.Jump;
        if (dy < -1400 && a.plat > 0 && s.Rand(100) < 40) input |= In.D;
        // stay on stage
        if ((a.x < sd.L + 700 && (input & In.L) != 0) || (a.x > sd.R - 700 && (input & In.R) != 0)) input &= ~(In.L | In.R);
        a.botIn = input;
        return input;
    }
}
