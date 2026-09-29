// BONK BRAWL definitions. Everything the simulation reads is an int (millimetres, frames at 60 Hz),
// so two browsers running the same inputs produce bit-identical matches (required for rollback netcode).
public static class In
{
    public const int L = 1, R = 2, U = 4, D = 8, Jump = 16, Light = 32, Heavy = 64, Dodge = 128, Throw = 256;
}

public class FighterDef
{
    public string id, name, model, from;
    public int weight;       // knockback taken = kb * 100 / weight
    public int speed;        // % of base run/air speed
    public int grav;         // gravity mm/frame^2
    public int jump;         // % of base jump
    public int color;        // 0xRRGGBB
    public string cls;       // LIGHT / MEDIUM / HEAVY
}

public static class Roster
{
    // A crossover cast from every game in the collection.
    public static readonly FighterDef[] All =
    {
        new FighterDef { id = "chef",     name = "CHEF",     model = "Mini/character-male-e",       from = "ORDER UP!",   weight = 100, speed = 100, grav = 8, jump = 100, color = 0xff5a3c, cls = "MEDIUM" },
        new FighterDef { id = "spark",    name = "SPARK",    model = "Mini/character-female-b",     from = "SPACE DINER", weight = 88,  speed = 110, grav = 7, jump = 106, color = 0xffc53d, cls = "LIGHT" },
        new FighterDef { id = "keeper",   name = "KEEPER",   model = "GraveChars/character-keeper", from = "GRAVE SHIFT", weight = 102, speed = 98,  grav = 8, jump = 100, color = 0x7cffb2, cls = "MEDIUM" },
        new FighterDef { id = "zombie",   name = "ZOMBIE",   model = "GraveChars/character-zombie", from = "GRAVE SHIFT", weight = 116, speed = 90,  grav = 9, jump = 94,  color = 0x5de05d, cls = "HEAVY" },
        new FighterDef { id = "skeleton", name = "BONES",    model = "GraveChars/character-skeleton",from = "GRAVE SHIFT", weight = 86,  speed = 112, grav = 7, jump = 108, color = 0xe8e0d0, cls = "LIGHT" },
        new FighterDef { id = "vampire",  name = "NOCTIS",   model = "GraveChars/character-vampire",from = "GRAVE SHIFT", weight = 100, speed = 104, grav = 8, jump = 102, color = 0xc0304a, cls = "MEDIUM" },
        new FighterDef { id = "orc",      name = "GRUNK",    model = "Dungeon/character-orc",       from = "GRAVE SHIFT", weight = 120, speed = 88,  grav = 9, jump = 92,  color = 0x8fbf3a, cls = "HEAVY" },
        new FighterDef { id = "knight",   name = "SIR BONK", model = "Dungeon/character-human",     from = "NEW!",        weight = 106, speed = 96,  grav = 8, jump = 98,  color = 0x46b8ff, cls = "MEDIUM" },
        new FighterDef { id = "racer",    name = "NITRO",    model = "Mini/character-male-c",       from = "CITY RUSH",   weight = 94,  speed = 108, grav = 8, jump = 102, color = 0xff3d7f, cls = "LIGHT" },
        new FighterDef { id = "ghost",    name = "BOO",      model = "GraveChars/character-ghost",  from = "GRAVE SHIFT", weight = 84,  speed = 102, grav = 6, jump = 110, color = 0xb8e6ff, cls = "LIGHT" },
    };
}

public class StageDef
{
    public string id, name, from;
    public int L, R, T, B;                 // main platform (solid)
    public int[] soft;                     // x0, x1, y  triples (pass-through)
    public int BX, BT, BB;                 // blast zones
    public int[] spawn;                    // x per slot
    public int gravMinus;                  // low-gravity stages
}

public static class Stages
{
    public static readonly StageDef[] All =
    {
        new StageDef { id = "rooftop", name = "NEON ROOFTOP", from = "CITY RUSH", L = -7200, R = 7200, T = 0, B = -3200,
            soft = new[] { -5200, -2200, 2700, 2200, 5200, 2700, -1500, 1500, 5100 }, BX = 15500, BT = 12500, BB = -9000, spawn = new[] { -4600, 4600, -1800, 1800 } },
        new StageDef { id = "graveyard", name = "MOONLIT CRYPT", from = "GRAVE SHIFT", L = -6400, R = 6400, T = 0, B = -3000,
            soft = new[] { -4300, -1400, 2500, 1400, 4300, 2500 }, BX = 14500, BT = 11800, BB = -8500, spawn = new[] { -4200, 4200, -1500, 1500 } },
        new StageDef { id = "moonbase", name = "MOON BASE", from = "SPACE DINER", L = -5600, R = 5600, T = 0, B = -2600,
            soft = new[] { -9200, -6600, 1300, 6600, 9200, 1300, -1600, 1600, 3000 }, BX = 16500, BT = 13000, BB = -9000, spawn = new[] { -3800, 3800, -7900, 7900 }, gravMinus = 1 },
        new StageDef { id = "kitchen", name = "THE PASS", from = "ORDER UP!", L = -8000, R = 8000, T = 0, B = -3400,
            soft = new[] { -2200, 2200, 3100 }, BX = 16000, BT = 12500, BB = -9000, spawn = new[] { -5000, 5000, -2000, 2000 } },
    };
}

public static class Weapon
{
    public const int None = 0, Sword = 1, Spear = 2, Pan = 3, Blaster = 4, Grenade = 5;
    public static readonly string[] Names = { "", "SWORD", "SPEAR", "FRYING PAN", "BLASTER", "GRENADE" };
}

public class MoveDef
{
    public int startup, active, total;
    public int hx, hy, hw, hh;           // hitbox centre offset (forward, up from feet) and half size
    public int dmg, kbBase, kbGrow, angle;
    public int selfVx, selfVy;
    public bool heavy, air, bothSides, recovery, meteor, follow;
    public int shot;                     // 1 blaster shot, 2 charged blast
    public int anim;                     // 0 punch, 1 kick, 2 kick-left, 3 spin
}

public static class Moves
{
    public const int Jab = 0, SLight = 1, DLight = 2, ULight = 3, NAir = 4, SAir = 5, DAir = 6, UAir = 7,
        SHeavy = 8, UHeavy = 9, DHeavy = 10, DAirHeavy = 11, Shoot = 12, Blast = 13, Count = 14;

    static readonly MoveDef[,] table = new MoveDef[6, Count];

    public static MoveDef Get(int weapon, int move) => table[weapon < 0 || weapon > 5 ? 0 : weapon, move];

    static MoveDef M(int su, int ac, int tot, int hx, int hy, int hw, int hh, int dmg, int kb, int grow, int ang, int anim = 0)
        => new MoveDef { startup = su, active = ac, total = tot, hx = hx, hy = hy, hw = hw, hh = hh, dmg = dmg, kbBase = kb, kbGrow = grow, angle = ang, anim = anim };

    static Moves()
    {
        var fist = new MoveDef[Count];
        fist[Jab] = M(4, 3, 16, 560, 760, 380, 260, 4, 42, 4, 30);
        fist[SLight] = M(7, 4, 20, 640, 720, 460, 280, 7, 52, 9, 35); fist[SLight].selfVx = 150;
        fist[DLight] = M(6, 4, 20, 560, 230, 520, 230, 6, 50, 8, 78, 1);
        fist[ULight] = M(5, 5, 18, 180, 1560, 460, 420, 6, 50, 9, 88);
        fist[NAir] = M(5, 7, 20, 0, 700, 720, 560, 6, 45, 8, 45, 3); fist[NAir].air = true;
        fist[SAir] = M(6, 5, 20, 620, 660, 460, 300, 8, 50, 10, 40, 1); fist[SAir].air = true;
        fist[DAir] = M(9, 5, 24, 120, -80, 400, 330, 9, 38, 10, -70, 1); fist[DAir].air = true;
        fist[UAir] = M(5, 5, 18, 100, 1560, 520, 380, 7, 45, 9, 85); fist[UAir].air = true;
        fist[SHeavy] = M(15, 5, 36, 760, 740, 540, 360, 16, 88, 21, 38); fist[SHeavy].selfVx = 130; fist[SHeavy].heavy = true;
        fist[UHeavy] = M(8, 12, 32, 180, 1300, 460, 640, 12, 70, 16, 82, 3); fist[UHeavy].selfVy = 240; fist[UHeavy].selfVx = 45; fist[UHeavy].heavy = true; fist[UHeavy].recovery = true; fist[UHeavy].follow = true;
        fist[DHeavy] = M(13, 6, 34, 0, 320, 1150, 320, 14, 76, 18, 55, 2); fist[DHeavy].bothSides = true; fist[DHeavy].heavy = true;
        fist[DAirHeavy] = M(10, 60, 90, 0, 150, 460, 420, 13, 56, 17, -65, 1); fist[DAirHeavy].air = true; fist[DAirHeavy].meteor = true; fist[DAirHeavy].heavy = true; fist[DAirHeavy].follow = true;
        fist[Shoot] = M(4, 1, 14, 700, 820, 200, 120, 5, 34, 4, 18); fist[Shoot].shot = 1;
        fist[Blast] = M(12, 1, 30, 700, 820, 340, 240, 13, 70, 16, 30); fist[Blast].shot = 2; fist[Blast].heavy = true;
        for (int m = 0; m < Count; m++) { table[0, m] = fist[m]; table[Weapon.Blaster, m] = fist[m]; table[Weapon.Grenade, m] = fist[m]; }
        // melee weapons: longer reach, harder hits, slower wind-up
        Derive(Weapon.Sword, fist, 138, 3, 108, 1, 118);
        Derive(Weapon.Spear, fist, 170, 2, 100, 3, 125);
        Derive(Weapon.Pan, fist, 120, 4, 124, 3, 110);
    }

    static void Derive(int w, MoveDef[] src, int rangePct, int dmgAdd, int kbPct, int startupAdd, int sizePct)
    {
        for (int m = 0; m < Count; m++)
        {
            var s = src[m];
            var d = new MoveDef
            {
                startup = s.startup + startupAdd + (s.heavy ? 1 : 0), active = s.active + 1, total = s.total + startupAdd + 2,
                hx = s.hx * rangePct / 100, hy = s.hy, hw = s.hw * sizePct / 100, hh = s.hh * (100 + (sizePct - 100) / 2) / 100,
                dmg = s.dmg + dmgAdd + (s.heavy ? 2 : 0), kbBase = s.kbBase * kbPct / 100, kbGrow = s.kbGrow * kbPct / 100 + (w == Weapon.Pan && s.heavy ? 5 : 0),
                angle = s.angle, selfVx = s.selfVx, selfVy = s.selfVy, heavy = s.heavy, air = s.air, bothSides = s.bothSides,
                recovery = s.recovery, meteor = s.meteor, follow = s.follow, shot = s.shot, anim = s.anim,
            };
            table[w, m] = d;
        }
    }
}
