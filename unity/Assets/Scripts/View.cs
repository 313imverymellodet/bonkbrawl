using System.Collections.Generic;
using UnityEngine;

// Renders a SimState. Nothing here feeds back into the simulation, so rollbacks simply re-render.
public class View : MonoBehaviour
{
    public static View I;
    public Camera Cam;
    Transform world, stageRoot;
    int builtStage = -1;
    readonly FView[] fv = new FView[SimState.MaxF];
    readonly GameObject[] items = new GameObject[SimState.MaxI];
    readonly int[] itemKind = new int[SimState.MaxI];
    readonly SpriteRenderer[] beams = new SpriteRenderer[SimState.MaxI];
    readonly SpriteRenderer[] shots = new SpriteRenderer[SimState.MaxS];
    readonly HashSet<long> played = new HashSet<long>();
    int lastPrune;
    float shake, flash;
    Vector3 camPos; float camDist = 16f;
    public Vector3 CamTarget;
    Light sun;
    SpriteRenderer screenFlash;
    public static Sprite Glow, Disc, RingS, Star, Streak;

    public static readonly Color[] SlotColors = { Kit.Hex("#ff4d4d"), Kit.Hex("#3ea8ff"), Kit.Hex("#ffd23f"), Kit.Hex("#5de05d") };

    public void Init(Camera cam)
    {
        I = this; Cam = cam;
        world = new GameObject("World").transform;
        Glow = Spr(Kit.Glow); Disc = Spr(Kit.Disc); RingS = Spr(Kit.Ring); Star = Spr(StarTex()); Streak = Spr(StreakTex());
        FX.Init(world);
        sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.55f; sun.shadowBias = 0.05f; sun.shadowNormalBias = 0.4f;
        for (int i = 0; i < SimState.MaxS; i++)
        {
            var sr = new GameObject("shot").AddComponent<SpriteRenderer>();
            sr.sprite = Streak; sr.transform.SetParent(world, false); sr.gameObject.SetActive(false); sr.sortingOrder = 5;
            shots[i] = sr;
        }
        screenFlash = new GameObject("flash").AddComponent<SpriteRenderer>();
        screenFlash.sprite = Disc; screenFlash.color = new Color(1, 1, 1, 0); screenFlash.sortingOrder = 100;
    }

    static Sprite Spr(Texture2D t) => Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f), t.width);

    static Texture2D StarTex()
    {
        int n = 64; var t = new Texture2D(n, n, TextureFormat.RGBA32, false); var px = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float u = (x + .5f) / n * 2 - 1, v = (y + .5f) / n * 2 - 1;
            float a = Mathf.Atan2(v, u), r = Mathf.Sqrt(u * u + v * v);
            float spike = 0.35f + 0.65f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2f)), 8f);
            px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01((spike - r) * 4f));
        }
        t.SetPixels(px); t.Apply(); return t;
    }
    static Texture2D StreakTex()
    {
        int w = 64, h = 16; var t = new Texture2D(w, h, TextureFormat.RGBA32, false); var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float u = (x + .5f) / w, v = Mathf.Abs((y + .5f) / h - .5f) * 2;
            px[y * w + x] = new Color(1, 1, 1, Mathf.Clamp01((1 - v * v) * Mathf.Clamp01(u * 3f) * 1.2f));
        }
        t.SetPixels(px); t.Apply(); return t;
    }

    // ================================================================ stage
    public void BuildStage(int stage)
    {
        if (builtStage == stage) return;
        if (stageRoot) Destroy(stageRoot.gameObject);
        stageRoot = new GameObject("Stage").transform;
        stageRoot.SetParent(world, false);
        builtStage = stage;
        StageArt.Build(stage, stageRoot, Cam, sun);
    }

    // ================================================================ fighters
    public void Bind(SimState s, string[] names)
    {
        for (int i = 0; i < SimState.MaxF; i++) { if (fv[i] != null) fv[i].Destroy(); fv[i] = null; }
        for (int i = 0; i < s.n; i++) fv[i] = new FView(world, s.f[i].ch, i, names != null && i < names.Length ? names[i] : "");
        for (int i = 0; i < SimState.MaxI; i++) { if (items[i]) Destroy(items[i]); items[i] = null; itemKind[i] = 0; if (beams[i]) beams[i].gameObject.SetActive(false); }
        played.Clear();
        FX.Clear();
        var sd = Stages.All[s.stage];
        camPos = new Vector3(0, 3f, -16f); CamTarget = Vector3.zero;
    }

    public FView Fighter(int i) => fv[i];

    public void Render(SimState s, float dt, bool menu)
    {
        for (int i = 0; i < s.n; i++) fv[i]?.Render(s, i, dt);
        RenderItems(s, dt);
        RenderShots(s);
        FX.Tick(dt, Cam);
        UpdateCamera(s, dt, menu);
    }

    void RenderItems(SimState s, float dt)
    {
        for (int k = 0; k < SimState.MaxI; k++)
        {
            var it = s.it[k];
            if (it.kind != itemKind[k])
            {
                if (items[k]) Destroy(items[k]);
                items[k] = it.kind != 0 ? WeaponModel(it.kind, world, 1.25f) : null;
                itemKind[k] = it.kind;
            }
            if (!items[k]) { if (beams[k]) beams[k].gameObject.SetActive(false); continue; }
            var p = new Vector3(it.x / 1000f, it.y / 1000f + 0.35f, -0.2f);
            float spin = Time.time * (it.thrown == 1 ? 900f : 60f) + k * 40f;
            items[k].transform.position = p + (it.ground == 1 ? Vector3.up * (Mathf.Sin(Time.time * 3f + k) * 0.08f + 0.1f) : Vector3.zero);
            items[k].transform.rotation = it.thrown == 1 ? Quaternion.Euler(0, 0, -spin * Mathf.Sign(it.vx == 0 ? 1 : it.vx)) : Quaternion.Euler(20, spin, 0);
            // beam of light while dropping in, glow once landed
            if (!beams[k]) { beams[k] = new GameObject("beam").AddComponent<SpriteRenderer>(); beams[k].sprite = Glow; beams[k].transform.SetParent(world, false); beams[k].sortingOrder = -1; }
            beams[k].gameObject.SetActive(true);
            bool falling = it.ground == 0 && it.thrown == 0;
            beams[k].transform.position = falling ? new Vector3(p.x, p.y + 4f, 0.3f) : p + new Vector3(0, 0, 0.3f);
            beams[k].transform.localScale = falling ? new Vector3(1.4f, 12f, 1) : Vector3.one * (1.6f + Mathf.Sin(Time.time * 5f) * 0.2f);
            beams[k].color = falling ? new Color(1f, 0.95f, 0.6f, 0.35f) : new Color(1f, 0.85f, 0.3f, 0.45f);
        }
    }

    void RenderShots(SimState s)
    {
        for (int k = 0; k < SimState.MaxS; k++)
        {
            var p = s.sh[k];
            var sr = shots[k];
            if (p.alive == 0) { if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false); continue; }
            sr.gameObject.SetActive(true);
            sr.transform.position = new Vector3(p.x / 1000f, p.y / 1000f, -0.3f);
            bool big = p.kind == 2;
            sr.transform.localScale = new Vector3((big ? 2.4f : 1.4f) * (p.vx >= 0 ? 1 : -1), big ? 0.9f : 0.35f, 1);
            sr.color = big ? Kit.Hex("#ff7ae0") : Kit.Hex("#7df9ff");
            if (Random.value < 0.5f) FX.Spark(sr.transform.position, sr.color, 1, 0.8f, 0.18f);
        }
    }

    public static GameObject WeaponModel(int kind, Transform parent, float scale)
    {
        string path = kind switch
        {
            Weapon.Sword => "Dungeon/weapon-sword", Weapon.Spear => "Dungeon/weapon-spear", Weapon.Pan => "Food/frying-pan",
            Weapon.Blaster => "Blaster/blaster-h", Weapon.Grenade => "Blaster/grenade-a", _ => null,
        };
        if (path == null) return null;
        var holder = new GameObject("weapon");
        holder.transform.SetParent(parent, false);
        var go = Kit.Spawn(path, 1f, holder.transform);
        var b = Kit.WorldBounds(go);
        float size = kind == Weapon.Spear ? 1.45f : kind == Weapon.Sword ? 0.9f : kind == Weapon.Pan ? 0.8f : kind == Weapon.Blaster ? 0.62f : 0.36f;
        float s = size / Mathf.Max(0.01f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
        go.transform.localScale = Vector3.one * s * scale;
        // centre the grip: move the model so its bottom sits at the holder
        var b2 = Kit.WorldBounds(go);
        go.transform.position -= new Vector3(b2.center.x - holder.transform.position.x, 0, b2.center.z - holder.transform.position.z);
        return holder;
    }

    // ================================================================ events
    public void PlayEvents(List<Ev> evs)
    {
        for (int i = 0; i < evs.Count; i++)
        {
            var e = evs[i];
            long key = ((long)e.frame << 20) | ((long)e.type << 12) | ((long)(e.a + 1) & 63) << 6 | ((long)(e.b + 1) & 63);
            if (!played.Add(key)) continue;
            Play(e);
        }
        if (Game.I != null && Game.I.S != null && Game.I.S.Cur.frame - lastPrune > 240)
        {
            lastPrune = Game.I.S.Cur.frame;
            long cut = (long)(lastPrune - 480) << 20;
            played.RemoveWhere(k => k < cut);
            // keep the shared event list short too
            int keepFrom = lastPrune - 300;
            evs.RemoveAll(ev => ev.frame < keepFrom);
        }
    }

    void Play(Ev e)
    {
        var p = new Vector3(e.x / 1000f, e.y / 1000f, -0.4f);
        var s = Game.I.S.Cur;
        switch (e.type)
        {
            case Sim.Ev_Hit:
            {
                int kb = e.v / 16, w = e.v % 16; bool heavy = w >= 8; int weapon = w % 8;
                var col = e.a >= 0 ? SlotColors[e.a] : Color.white;
                int n = 6 + Mathf.Min(14, kb / 25);
                FX.Burst(p, Color.Lerp(Color.white, col, 0.4f), n, 4f + kb / 40f, 0.35f);
                FX.Pop(p, Star, Color.white, 0.8f + kb / 250f, 0.14f);
                if (kb > 180) FX.Shock(p, col, 1.5f + kb / 150f);
                shake = Mathf.Max(shake, Mathf.Clamp01(kb / 420f));
                if (weapon == Weapon.Pan) { Sfx.I.Bonk(kb); UI.I.WorldText(p + Vector3.up * 0.6f, "BONK!", Kit.Hex("#ffd23f"), 1.3f); }
                else if (weapon == Weapon.Sword || weapon == Weapon.Spear) Sfx.I.Slash(kb);
                else Sfx.I.Hit(kb, heavy);
                if (kb > 300) { UI.I.WorldText(p + Vector3.up * 0.9f, kb > 420 ? "SMASH!!" : "CRACK!", Color.white, 1.1f); }
                if (IsLocal(e.b) || IsLocal(e.a)) WebBridge.Vibrate(Mathf.Clamp(kb / 8, 15, 80));
                break;
            }
            case Sim.Ev_KO:
            {
                var sd = Stages.All[s.stage];
                var col = SlotColors[Mathf.Clamp(e.a, 0, 3)];
                // blast at the edge of what the camera can see
                var at = ClampToView(p);
                // the beam shoots from the blast point back across the screen toward the stage
                var dir = (CamTarget - at); dir.z = 0; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.up;
                FX.KO(at, dir, col);
                shake = 1f; flash = 0.6f;
                Sfx.I.KO();
                UI.I.Announce("KO!", col);
                if (IsLocal(e.a)) WebBridge.Vibrate(160);
                break;
            }
            case Sim.Ev_Jump: FX.Dust(p, e.b == 1 ? 6 : 4, e.b == 1); Sfx.I.Jump(e.b == 1); break;
            case Sim.Ev_Land: FX.Dust(p, 3, false); Sfx.I.Land(); break;
            case Sim.Ev_Swing: Sfx.I.Swing(e.v, Moves.Get(e.v, e.b).heavy); break;
            case Sim.Ev_Dodge: Sfx.I.Dodge(); FX.Dust(p + Vector3.up * 0.6f, 3, true); break;
            case Sim.Ev_Pickup: Sfx.I.Pickup(); FX.Pop(p + Vector3.up * 1.2f, RingS, Kit.Hex("#ffd23f"), 1.4f, 0.3f); if (IsLocal(e.a)) UI.I.WorldText(p + Vector3.up * 2f, Weapon.Names[e.b] + "!", Kit.Hex("#ffd23f"), 0.9f); break;
            case Sim.Ev_Throw: Sfx.I.Throw(); break;
            case Sim.Ev_Boom: FX.Explosion(p); shake = 0.9f; flash = 0.35f; Sfx.I.Boom(); break;
            case Sim.Ev_Shot: Sfx.I.Pew(e.b == 2); FX.Pop(p, Glow, e.b == 2 ? Kit.Hex("#ff7ae0") : Kit.Hex("#7df9ff"), e.b == 2 ? 2.2f : 1.2f, 0.12f); break;
            case Sim.Ev_Spawn: FX.Pop(p + Vector3.up * 0.8f, RingS, SlotColors[e.a], 3f, 0.5f); Sfx.I.Spawn(); break;
            case Sim.Ev_Item: Sfx.I.ItemDrop(); break;
            case Sim.Ev_Break: FX.Burst(p + Vector3.up, Color.gray, 8, 3f, 0.4f); break;
            case Sim.Ev_Go: UI.I.Announce("BONK!", Kit.Hex("#ffd23f")); Sfx.I.Go(); break;
            case Sim.Ev_Elim: UI.I.Toast(Game.I.IsLocal(e.a) && Game.I.LocalCount == 1 ? "YOU'RE OUT! WATCH THE FINISH" : Game.I.NameOf(e.a) + " IS OUT!"); break;
            case Sim.Ev_Over: UI.I.Announce("GAME!", Color.white); Sfx.I.GameSet(); flash = 0.5f; break;
        }
    }

    bool IsLocal(int slot) => Game.I != null && Game.I.IsLocal(slot);

    Vector3 ClampToView(Vector3 p)
    {
        var vp = Cam.WorldToViewportPoint(new Vector3(p.x, p.y, 0));
        vp.x = Mathf.Clamp(vp.x, 0.04f, 0.96f); vp.y = Mathf.Clamp(vp.y, 0.06f, 0.94f);
        vp.z = Mathf.Abs(Cam.transform.position.z);
        var w = Cam.ViewportToWorldPoint(vp); w.z = -0.5f;
        return w;
    }

    // ================================================================ camera
    void UpdateCamera(SimState s, float dt, bool menu)
    {
        var sd = Stages.All[s.stage];
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue; int n = 0;
        for (int i = 0; i < s.n; i++)
        {
            var f = s.f[i];
            if (f.st == Sim.St_Dead || f.alive == 0) continue;
            float x = f.x / 1000f, y = f.y / 1000f;
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y + 1.4f); n++;
        }
        if (n == 0) { minX = sd.L / 1000f; maxX = sd.R / 1000f; minY = 0; maxY = 3; }
        // always keep a good part of the stage in shot
        minX = Mathf.Min(minX, sd.L / 3000f); maxX = Mathf.Max(maxX, sd.R / 3000f);
        minY = Mathf.Min(minY, sd.T / 1000f - 1.5f); maxY = Mathf.Max(maxY, sd.T / 1000f + 3.5f);
        float padX = 2.6f, padY = 2.1f;
        float w = maxX - minX + padX * 2, h = maxY - minY + padY * 2;
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        float vt = Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float want = Mathf.Max(w * 0.5f / (vt * aspect), h * 0.5f / vt);
        want = Mathf.Clamp(want, aspect < 0.9f ? 13f : 8.5f, aspect < 0.9f ? 62f : 24f);
        if (menu) want *= 1.05f;
        camDist = Mathf.Lerp(camDist, want, 1f - Mathf.Exp(-dt * 3f));
        var c = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f + 0.4f, 0);
        c.x = Mathf.Clamp(c.x, -sd.BX / 1000f + 6f, sd.BX / 1000f - 6f);
        c.y = Mathf.Clamp(c.y, sd.BB / 1000f + 5f, sd.BT / 1000f - 5f);
        // portrait phones: the bottom third is thumbs, so lift the action into the upper part of the screen
        if (aspect < 0.9f && !menu) c.y -= camDist * vt * 0.32f;
        CamTarget = Vector3.Lerp(CamTarget, c, 1f - Mathf.Exp(-dt * 4f));
        const float pitch = 7f;
        var pos = CamTarget + new Vector3(0, Mathf.Tan(pitch * Mathf.Deg2Rad) * camDist, -camDist);
        shake = Mathf.Max(0, shake - dt * 2.2f);
        var sh = new Vector3(Mathf.PerlinNoise(Time.time * 35f, 0) - .5f, Mathf.PerlinNoise(0, Time.time * 35f) - .5f, 0) * shake * shake * 1.1f;
        Cam.transform.position = pos + sh;
        Cam.transform.rotation = Quaternion.Euler(pitch, 0, 0);
        flash = Mathf.Max(0, flash - dt * 2.5f);
        screenFlash.transform.position = Cam.transform.position + Cam.transform.forward * 1f;
        screenFlash.transform.rotation = Cam.transform.rotation;
        screenFlash.transform.localScale = Vector3.one * 6f;
        screenFlash.color = new Color(1, 1, 1, flash * 0.6f);
    }
}

// ==================================================================== one fighter's look
public class FView
{
    public GameObject Root;
    Transform model, spin, hand;
    Animation anim;
    Renderer[] rends;
    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
    GameObject weapon; int weaponKind;
    TrailRenderer trail;
    SpriteRenderer shadow, chargeGlow, ring;
    string clip = "";
    public int Slot;
    float yaw, tumble, squash = 1f;
    int lastSt = -1, lastGround = 1;
    static readonly int ColorId = Shader.PropertyToID("_Color");
    public Vector3 Head => Root.transform.position + Vector3.up * 1.6f;
    public bool Visible => Root.activeSelf;

    public FView(Transform parent, int ch, int slot, string name)
    {
        Slot = slot;
        var def = Roster.All[ch];
        Root = new GameObject("F" + slot);
        Root.transform.SetParent(parent, false);
        spin = new GameObject("spin").transform; spin.SetParent(Root.transform, false);
        var m = Kit.Spawn(def.model, 1.85f, spin);
        model = m.transform;
        hand = new GameObject("hand").transform; hand.SetParent(model, false);
        hand.localScale = Vector3.one / Mathf.Max(0.01f, model.localScale.x);   // weapons are sized in metres
        anim = m.GetComponentInChildren<Animation>();
        if (anim)
        {
            anim.cullingType = AnimationCullingType.AlwaysAnimate;
            foreach (AnimationState s in anim) s.wrapMode = WrapMode.Loop;
            foreach (var c in new[] { "attack-melee-right", "attack-kick-right", "attack-kick-left", "pick-up", "die" })
                if (anim[c] != null) anim[c].wrapMode = WrapMode.ClampForever;
            anim.Play("idle");
        }
        rends = m.GetComponentsInChildren<Renderer>();
        foreach (var r in rends) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        var col = View.SlotColors[slot];
        trail = Root.AddComponent<TrailRenderer>();
        trail.sharedMaterial = Kit.UnlitAlpha; trail.time = 0.35f; trail.widthMultiplier = 0.7f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
        trail.startColor = Kit.A(col, 0.75f); trail.endColor = Kit.A(col, 0f); trail.emitting = false;
        shadow = new GameObject("shadow").AddComponent<SpriteRenderer>();
        shadow.sprite = View.Disc; shadow.color = new Color(0, 0, 0, 0.35f); shadow.transform.SetParent(parent, false);
        shadow.transform.rotation = Quaternion.Euler(90, 0, 0);
        ring = new GameObject("ring").AddComponent<SpriteRenderer>();
        ring.sprite = View.RingS; ring.color = Kit.A(col, 0.9f); ring.transform.SetParent(parent, false);
        ring.transform.rotation = Quaternion.Euler(90, 0, 0);
        chargeGlow = new GameObject("charge").AddComponent<SpriteRenderer>();
        chargeGlow.sprite = View.Glow; chargeGlow.transform.SetParent(Root.transform, false); chargeGlow.transform.localPosition = new Vector3(0, 0.9f, -0.3f);
        chargeGlow.color = new Color(1, 1, 1, 0);
    }

    public void Destroy() { Object.Destroy(Root); Object.Destroy(shadow.gameObject); Object.Destroy(ring.gameObject); }

    void Anim(string c, float speed = 1f, float fade = 0.1f)
    {
        if (!anim || anim[c] == null) return;
        if (clip != c) { anim.CrossFade(c, fade); clip = c; }
        anim[c].speed = speed;
    }
    void AnimAt(string c, float t01)
    {
        if (!anim || anim[c] == null) return;
        if (clip != c) { anim.CrossFade(c, 0.04f); clip = c; }
        var st = anim[c]; st.speed = 0; st.time = t01 * st.length;
    }

    public void Render(SimState s, int i, float dt)
    {
        var f = s.f[i];
        bool vis = f.st != Sim.St_Dead && f.alive == 1;
        if (Root.activeSelf != vis) Root.SetActive(vis);
        shadow.gameObject.SetActive(vis); ring.gameObject.SetActive(vis);
        if (!vis) { trail.Clear(); return; }
        var pos = new Vector3(f.x / 1000f, f.y / 1000f, 0);
        if (f.hitstop > 0 && f.st == Sim.St_Hitstun) pos += new Vector3(Mathf.Sin(Time.time * 90f) * 0.07f, 0, 0);
        Root.transform.position = pos;

        // facing (turned a little toward the camera so faces read)
        float wantYaw = f.face > 0 ? 112f : -112f;
        yaw = Mathf.MoveTowardsAngle(yaw, wantYaw, dt * 1400f);
        model.localRotation = Quaternion.Euler(0, yaw, 0);

        // squash & stretch on jump/land
        if (f.ground == 1 && lastGround == 0) squash = 0.72f;
        if (f.st == Sim.St_Air && lastSt == Sim.St_Ground && f.vy > 0) squash = 1.25f;
        squash = Mathf.Lerp(squash, 1f, 1f - Mathf.Exp(-dt * 12f));
        spin.localScale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));
        lastGround = f.ground; lastSt = f.st;

        // tumbling when launched hard
        if (f.st == Sim.St_Hitstun && f.launch > 170) tumble += dt * (400f + f.launch) * -f.face;
        else tumble = Mathf.MoveTowards(tumble, Mathf.Round(tumble / 360f) * 360f, dt * 900f);
        spin.localRotation = Quaternion.Euler(0, 0, tumble);
        trail.emitting = f.st == Sim.St_Hitstun && f.launch > 150;
        chargeGlow.transform.localPosition = new Vector3(0, 0.9f, -0.3f);
        if (f.st == Sim.St_Spawn) { chargeGlow.color = Kit.A(View.SlotColors[i], 0.45f + 0.2f * Mathf.Sin(Time.time * 6f)); chargeGlow.transform.localScale = Vector3.one * 3.2f; chargeGlow.transform.localPosition = new Vector3(0, 0.1f, 0.2f); }

        Pose(f, dt);
        Weapon(f);

        // hit flash / invincibility blink / dodge ghost
        float boost = f.flash > 0 ? 1f + f.flash * 0.25f : 1f;
        bool blink = (f.iframes > 0 && f.st != Sim.St_Dodge && (Time.frameCount / 3) % 2 == 0);
        bool ghost = f.st == Sim.St_Dodge;
        var tint = ghost ? new Color(0.6f, 0.85f, 1.6f) : new Color(boost, boost, boost);
        mpb.SetColor(ColorId, tint);
        foreach (var r in rends) { r.SetPropertyBlock(mpb); r.enabled = !blink; }

        // charge glow
        if (f.st == Sim.St_Attack && f.charge > 0 && f.move >= 0 && f.moveT <= 1)
        {
            float k = f.charge / 30f;
            chargeGlow.color = Kit.A(View.SlotColors[i], 0.35f + 0.4f * Mathf.PingPong(Time.time * 8f, 1f));
            chargeGlow.transform.localScale = Vector3.one * (1.5f + k * 2f);
            spin.localPosition = new Vector3(Mathf.Sin(Time.time * 70f) * 0.03f * k, 0, 0);
            if (Random.value < 0.3f) FX.Spark(pos + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.1f, 1.6f), -0.3f), View.SlotColors[i], 1, 1.2f, 0.3f);
        }
        else if (f.st != Sim.St_Spawn) { chargeGlow.color = new Color(1, 1, 1, 0); spin.localPosition = Vector3.zero; }

        // shadow on the surface below + player ring
        var sd = Stages.All[s.stage];
        float gy = Floor(sd, f.x, f.y);
        float hgt = Mathf.Max(0, f.y / 1000f - gy);
        bool over = gy > -99f;
        shadow.gameObject.SetActive(over);
        ring.gameObject.SetActive(over && f.ground == 1);
        if (over)
        {
            shadow.transform.position = new Vector3(pos.x, gy + 0.03f, 0.05f);
            shadow.transform.localScale = Vector3.one * Mathf.Lerp(1.3f, 0.5f, hgt / 8f);
            shadow.color = new Color(0, 0, 0, Mathf.Lerp(0.4f, 0.08f, hgt / 8f));
            ring.transform.position = new Vector3(pos.x, gy + 0.04f, 0.05f);
            ring.transform.localScale = Vector3.one * 1.4f;
        }
    }

    static float Floor(StageDef sd, int x, int y)
    {
        float best = -100f;
        if (x >= sd.L && x <= sd.R && y >= sd.T - 5) best = sd.T / 1000f;
        for (int k = 0; k < sd.soft.Length; k += 3)
            if (x >= sd.soft[k] && x <= sd.soft[k + 1] && y >= sd.soft[k + 2] - 5 && sd.soft[k + 2] / 1000f > best) best = sd.soft[k + 2] / 1000f;
        return best;
    }

    void Pose(Fighter f, float dt)
    {
        switch (f.st)
        {
            case Sim.St_Ground:
                if (Mathf.Abs(f.vx) > 20) Anim("sprint", Mathf.Clamp(Mathf.Abs(f.vx) / 90f, 0.6f, 1.6f) * 1.3f);
                else Anim("idle");
                break;
            case Sim.St_Land: Anim("crouch", 2f, 0.05f); break;
            case Sim.St_Air:
            case Sim.St_Spawn:
                Anim(f.vy > 0 ? "jump" : "fall", 1f, 0.15f); break;
            case Sim.St_Hitstun: Anim("fall", 1.5f, 0.05f); break;
            case Sim.St_Dodge: Anim("crouch", 1.5f, 0.05f); break;
            case Sim.St_Attack:
            {
                var m = Moves.Get(f.weapon, f.move < 0 ? 0 : f.move);
                string c = m.anim == 1 ? "attack-kick-right" : m.anim == 2 ? "attack-kick-left" : "attack-melee-right";
                // startup plays the wind-up half of the clip, the active frames snap through the hit
                float t;
                if (f.moveT < m.startup) t = 0.05f + 0.4f * f.moveT / Mathf.Max(1, m.startup);
                else if (f.moveT < m.startup + m.active) t = 0.45f + 0.25f * (f.moveT - m.startup) / Mathf.Max(1, m.active);
                else t = 0.7f + 0.3f * (f.moveT - m.startup - m.active) / Mathf.Max(1, m.total - m.startup - m.active);
                AnimAt(c, t);
                if (m.anim == 3 && f.moveT >= m.startup && f.moveT < m.startup + m.active + 4) tumble += dt * 1500f * -f.face;
                break;
            }
        }
    }

    void Weapon(Fighter f)
    {
        if (f.weapon != weaponKind)
        {
            if (weapon) Object.Destroy(weapon);
            weapon = f.weapon != 0 ? View.WeaponModel(f.weapon, hand, 1f) : null;
            weaponKind = f.weapon;
        }
        if (!weapon) return;
        // held in the leading hand; swings through an arc during melee attacks
        float angle = 35f;
        if (f.st == Sim.St_Attack && f.move >= 0)
        {
            var m = Moves.Get(f.weapon, f.move);
            if (f.weapon == global::Weapon.Blaster || f.weapon == global::Weapon.Grenade) angle = 90f;
            else if (f.moveT < m.startup) angle = Mathf.Lerp(35f, -110f, f.moveT / (float)Mathf.Max(1, m.startup));
            else if (f.moveT < m.startup + m.active + 2) angle = Mathf.Lerp(-110f, 120f, (f.moveT - m.startup) / (float)(m.active + 2));
            else angle = Mathf.Lerp(120f, 35f, (f.moveT - m.startup - m.active) / (float)Mathf.Max(1, m.total - m.startup - m.active));
            if (m.anim == 1 || m.anim == 2) angle = 20f;
        }
        else if (f.weapon == global::Weapon.Blaster) angle = 90f;
        weapon.transform.localPosition = new Vector3(0.36f, 0.66f, 0.12f);
        weapon.transform.localRotation = Quaternion.Euler(angle, 0, 0) * (f.weapon == global::Weapon.Blaster ? Quaternion.Euler(-90, 0, 0) : Quaternion.identity);
    }
}

// ==================================================================== lightweight particles
public static class FX
{
    class P { public SpriteRenderer sr; public Vector3 v; public float life, max, size, grow, grav, spin; public Color c; public bool on; public Vector2 ar = Vector2.one; }
    static readonly List<P> pool = new List<P>();
    static Transform root;

    public static void Init(Transform parent)
    {
        root = new GameObject("FX").transform; root.SetParent(parent, false);
        for (int i = 0; i < 260; i++)
        {
            var sr = new GameObject("p").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root, false); sr.sortingOrder = 10; sr.gameObject.SetActive(false);
            pool.Add(new P { sr = sr });
        }
    }

    public static void Clear() { foreach (var p in pool) { p.on = false; p.sr.gameObject.SetActive(false); } }

    static P Get()
    {
        foreach (var p in pool) if (!p.on) return p;
        return null;
    }

    public static void Emit(Vector3 at, Sprite spr, Color c, Vector3 v, float life, float size, float grow = 0, float grav = 0, float spin = 0)
    {
        var p = Get(); if (p == null) return;
        p.on = true; p.sr.gameObject.SetActive(true); p.sr.sprite = spr; p.sr.color = c;
        p.sr.transform.position = at; p.v = v; p.life = p.max = life; p.size = size; p.grow = grow; p.grav = grav; p.spin = spin; p.c = c; p.ar = Vector2.one;
        p.sr.transform.localScale = Vector3.one * size; p.sr.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 360f));
    }

    public static void Tick(float dt, Camera cam)
    {
        foreach (var p in pool)
        {
            if (!p.on) continue;
            p.life -= dt;
            if (p.life <= 0) { p.on = false; p.sr.gameObject.SetActive(false); continue; }
            p.v.y -= p.grav * dt;
            p.sr.transform.position += p.v * dt;
            p.size += p.grow * dt;
            p.sr.transform.localScale = new Vector3(Mathf.Max(0.01f, p.size) * p.ar.x, Mathf.Max(0.01f, p.size) * p.ar.y, 1);
            p.sr.transform.Rotate(0, 0, p.spin * dt);
            float k = p.life / p.max;
            p.sr.color = Kit.A(p.c, p.c.a * Mathf.Clamp01(k * 1.6f));
        }
    }

    public static void Spark(Vector3 at, Color c, int n, float speed, float life)
    {
        for (int i = 0; i < n; i++) Emit(at, View.Glow, c, Random.insideUnitCircle * speed, life, Random.Range(0.25f, 0.5f), -0.5f);
    }
    public static void Burst(Vector3 at, Color c, int n, float speed, float life)
    {
        for (int i = 0; i < n; i++)
        {
            var d = (Vector3)Random.insideUnitCircle.normalized * speed * Random.Range(0.5f, 1.2f);
            Emit(at, View.Star, c, d, life * Random.Range(0.7f, 1.2f), Random.Range(0.25f, 0.55f), -0.6f, 6f, Random.Range(-400f, 400f));
        }
    }
    public static void Pop(Vector3 at, Sprite spr, Color c, float size, float life) => Emit(at, spr, c, Vector3.zero, life, size * 0.4f, size * 3.5f);
    public static void Shock(Vector3 at, Color c, float size) => Emit(at, View.RingS, Kit.A(c, 0.9f), Vector3.zero, 0.3f, 0.4f, size * 6f);
    public static void Dust(Vector3 at, int n, bool ring)
    {
        if (ring) Emit(at + Vector3.up * 0.1f, View.RingS, new Color(1, 1, 1, 0.7f), Vector3.zero, 0.25f, 0.4f, 5f);
        for (int i = 0; i < n; i++)
            Emit(at + new Vector3(Random.Range(-0.3f, 0.3f), 0.1f, -0.2f), View.Disc, new Color(0.9f, 0.88f, 0.84f, 0.55f), new Vector3(Random.Range(-2.2f, 2.2f), Random.Range(0.3f, 1.2f), 0), 0.45f, Random.Range(0.4f, 0.7f), 1.2f);
    }
    public static void Explosion(Vector3 at)
    {
        Emit(at, View.Glow, new Color(1f, 0.85f, 0.4f, 1f), Vector3.zero, 0.35f, 1f, 16f);
        Emit(at, View.RingS, new Color(1f, 0.6f, 0.2f, 1f), Vector3.zero, 0.4f, 0.5f, 22f);
        for (int i = 0; i < 18; i++)
        {
            var d = (Vector3)Random.insideUnitCircle * 9f;
            Emit(at, View.Disc, Color.Lerp(new Color(1f, 0.55f, 0.1f), new Color(0.3f, 0.3f, 0.3f), Random.value), d, Random.Range(0.4f, 0.9f), Random.Range(0.6f, 1.2f), 1.5f, -2f);
        }
    }
    public static void KO(Vector3 at, Vector3 dir, Color c)
    {
        // a column of light shooting back toward the stage, plus a shockwave
        var ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        for (int i = 0; i < 3; i++)
        {
            var p = Get(); if (p == null) break;
            p.on = true; p.sr.gameObject.SetActive(true); p.sr.sprite = View.Streak; p.c = i == 0 ? Color.white : Kit.A(c, 0.9f); p.sr.color = p.c;
            p.sr.transform.position = at + dir * (3f + i); p.v = Vector3.zero; p.life = p.max = 0.7f - i * 0.1f; p.size = 1f; p.grow = 0; p.grav = 0; p.spin = 0;
            p.sr.transform.rotation = Quaternion.Euler(0, 0, ang);
            p.ar = new Vector2(14f - i * 3f, 2.2f - i * 0.5f);
        }
        Emit(at, View.RingS, c, Vector3.zero, 0.6f, 0.5f, 30f);
        Emit(at, View.Glow, Color.white, Vector3.zero, 0.4f, 2f, 20f);
        for (int i = 0; i < 24; i++) Emit(at, View.Star, Color.Lerp(c, Color.white, Random.value * 0.5f), (Vector3)Random.insideUnitCircle * 14f + dir * 6f, Random.Range(0.4f, 0.9f), Random.Range(0.3f, 0.7f), -0.3f, 4f, Random.Range(-500f, 500f));
    }
}
