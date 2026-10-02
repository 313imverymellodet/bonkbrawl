using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI : MonoBehaviour
{
    public static UI I;
    Canvas canvas; CanvasScaler scaler;
    RectTransform root, hud, screens, tags, floats, touch;
    Font F => Kit.Font;
    public static readonly Color Ink = Kit.Hex("#1b1030"), Cream = Kit.Hex("#fff6e5"), Pink = Kit.Hex("#ff3d7f"), Gold = Kit.Hex("#ffd23f"), Cyan = Kit.Hex("#3ec7ff"), Lime = Kit.Hex("#7cf56a");
    public bool Paused { get; private set; }

    // ---- touch
    RectTransform joyBase, joyKnob; int joyId = -99; Vector2 joyOrigin, joyVec; bool joyMouse;
    const float JoyR = 120f;
    readonly Dictionary<int, HoldButton> btn = new Dictionary<int, HoldButton>();
    readonly Dictionary<int, int> btnLatch = new Dictionary<int, int>();
    bool touchSeen;

    // ---- hud
    class Card { public RectTransform rt; public Text pct, name; public Image face, ring, weapon; public Image[] stocks = new Image[5]; public int lastDmg; public float bump; }
    readonly Card[] cards = new Card[4];
    Transform pauseBtn;
    Text bigText, toastText, rankText;
    float bigT, toastT;
    readonly List<(int slot, Text t, Image arrow)> nameTags = new List<(int, Text, Image)>();
    string selectMode = "cpu";

    static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    public static Sprite Icon(string n) { if (!icons.TryGetValue(n, out var s)) icons[n] = s = Resources.Load<Sprite>("Icons/" + n); return s; }
    static Sprite disc, ring, star, arrowDown;

    public void Init()
    {
        I = this;
        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>().pixelDragThreshold = 2; es.AddComponent<StandaloneInputModule>();
        canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920);
        gameObject.AddComponent<GraphicRaycaster>();
        root = (RectTransform)transform;
        disc = Spr(Kit.Disc); ring = Spr(Kit.Ring); star = View.Star; arrowDown = Spr(Kit.Arrow);
        tags = Fill("tags", root);
        floats = Fill("floats", root);
        BuildHud();
        BuildTouch();
        screens = Fill("screens", root);
        bigText = Txt(root, "", 250, new Vector2(.5f, .6f), Vector2.zero, Cream, TextAnchor.MiddleCenter, 1600);
        bigText.fontStyle = FontStyle.Italic; Outline(bigText, 8); bigText.gameObject.SetActive(false);
        toastText = Txt(root, "", 40, new Vector2(.5f, 1), new Vector2(0, -300), Cream, TextAnchor.MiddleCenter, 1400);
        Outline(toastText, 3); toastText.gameObject.SetActive(false);
    }

    static Sprite Spr(Texture2D t) => Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f));

    // ---------------------------------------------------------------- building blocks
    RectTransform Rect(string n, Transform p, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }
    RectTransform Fill(string n, Transform p)
    {
        var rt = Rect(n, p, Vector2.zero, Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; return rt;
    }
    RectTransform Box(Transform p, Vector2 anchor, Vector2 pos, Vector2 size, Color c, bool ray = false)
    {
        var rt = Rect("box", p, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>(); img.sprite = Kit.RoundedSprite; img.type = Image.Type.Sliced; img.color = c; img.raycastTarget = ray;
        return rt;
    }
    Image Img(Transform p, Sprite s, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = Rect("img", p, anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>(); img.sprite = s; img.preserveAspect = true; img.raycastTarget = false; return img;
    }
    Text Txt(Transform p, string s, int size, Vector2 anchor, Vector2 pos, Color c, TextAnchor align = TextAnchor.MiddleCenter, float w = 700)
    {
        size = Mathf.Max(size, 28);   // readable floor (the fighter grid labels are the tightest fit at 21-24)
        var rt = Rect("txt", p, anchor, pos, new Vector2(w, size * 1.4f));
        var t = rt.gameObject.AddComponent<Text>();
        t.font = F; t.fontSize = size; t.fontStyle = FontStyle.Normal; t.alignment = align; t.color = c; t.text = s;
        t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
    static void Outline(Text t, float d) { var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0.05f, 0.02f, 0.1f, 0.9f); o.effectDistance = new Vector2(d, -d); }
    Button Btn(Transform p, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color bg, Color fg, Action onClick, int fs = 48)
    {
        Box(p, anchor, pos + new Vector2(0, -10), size, Color.Lerp(bg, Color.black, 0.5f));
        var rt = Box(p, anchor, pos, size, bg, true);
        var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = rt.GetComponent<Image>();
        b.onClick.AddListener(() => { Sfx.I.Click(); onClick(); });
        rt.gameObject.AddComponent<Press>().Sink = 9;   // the face drops onto its shadow
        var t = Txt(rt, label, fs, new Vector2(.5f, .5f), Vector2.zero, fg, TextAnchor.MiddleCenter, size.x);
        t.fontStyle = FontStyle.Italic;
        return b;
    }

    // ---------------------------------------------------------------- HUD
    void BuildHud()
    {
        hud = Fill("hud", root);
        for (int i = 0; i < 4; i++)
        {
            var c = new Card();
            c.rt = Box(hud, new Vector2(.5f, 1), Vector2.zero, new Vector2(250, 150), new Color(0.05f, 0.03f, 0.1f, 0.55f));
            c.ring = Img(c.rt, disc, new Vector2(0, .5f), new Vector2(62, 8), new Vector2(104, 104));
            c.face = Img(c.rt, null, new Vector2(0, .5f), new Vector2(62, 12), new Vector2(110, 110));
            c.pct = Txt(c.rt, "0%", 64, new Vector2(1, .5f), new Vector2(-72, 18), Color.white, TextAnchor.MiddleCenter, 150);
            c.pct.fontStyle = FontStyle.Italic; Outline(c.pct, 3);
            c.name = Txt(c.rt, "", 22, new Vector2(.5f, 0), new Vector2(0, 16), Cream, TextAnchor.MiddleCenter, 240);
            Outline(c.name, 2);
            for (int k = 0; k < 5; k++) { c.stocks[k] = Img(c.rt, disc, new Vector2(1, .5f), new Vector2(-122 + k * 26, -30), new Vector2(20, 20)); }
            c.weapon = Img(c.rt, null, new Vector2(0, 1), new Vector2(108, -14), new Vector2(46, 46));
            c.rt.gameObject.SetActive(false);
            cards[i] = c;
        }
        pauseBtn = Rect("pause", hud, new Vector2(1, 1), new Vector2(-70, -70), new Vector2(90, 90));
        Btn(pauseBtn, "II", new Vector2(.5f, .5f), Vector2.zero, new Vector2(90, 90), new Color(0, 0, 0, 0.35f), Color.white, TogglePause, 40);
        hud.gameObject.SetActive(false);
    }

    void BuildTouch()
    {
        touch = Fill("touch", root);
        joyBase = (RectTransform)Img(touch, ring, Vector2.zero, Vector2.zero, new Vector2(JoyR * 2.2f, JoyR * 2.2f)).transform;
        joyBase.GetComponent<Image>().color = new Color(1, 1, 1, 0.4f);
        joyKnob = (RectTransform)Img(joyBase, disc, new Vector2(.5f, .5f), Vector2.zero, new Vector2(120, 120)).transform;
        joyKnob.GetComponent<Image>().color = new Color(1, 1, 1, 0.7f);
        joyBase.gameObject.SetActive(false);
        // right-hand button cluster
        TouchBtn(In.Light, "ATTACK", new Vector2(-190, 300), 210, Pink);
        TouchBtn(In.Heavy, "HEAVY", new Vector2(-400, 190), 170, Kit.Hex("#ff8a3d"));
        TouchBtn(In.Jump, "JUMP", new Vector2(-190, 80), 170, Cyan);
        TouchBtn(In.Dodge, "DODGE", new Vector2(-400, 400), 140, Kit.Hex("#9a7bff"));
        TouchBtn(In.Throw, "THROW", new Vector2(-190, 520), 120, Gold);
        touch.gameObject.SetActive(false);
    }

    void TouchBtn(int bit, string label, Vector2 pos, float size, Color c)
    {
        var r = Rect(label, touch, new Vector2(1, 0), pos, new Vector2(size, size));
        var img = r.gameObject.AddComponent<Image>(); img.sprite = disc; img.color = Kit.A(c, 0.55f);
        var hb = r.gameObject.AddComponent<HoldButton>();
        hb.OnDown = () => { btnLatch[bit] = 1; touchSeen = true; };
        var t = Txt(r, label, Mathf.RoundToInt(size * 0.17f), new Vector2(.5f, .5f), Vector2.zero, Color.white); Outline(t, 2);
        btn[bit] = hb;
    }

    public int TouchInput(bool pressesOnly)
    {
        int v = 0;
        foreach (var kv in btn)
        {
            if (pressesOnly) { if (btnLatch.TryGetValue(kv.Key, out var l) && l > 0) { v |= kv.Key; btnLatch[kv.Key] = 0; } }
            else if (kv.Value.Held) v |= kv.Key;
        }
        if (!pressesOnly)
        {
            if (joyVec.x < -0.4f) v |= In.L;
            if (joyVec.x > 0.4f) v |= In.R;
            if (joyVec.y > 0.55f) v |= In.U;
            if (joyVec.y < -0.55f) v |= In.D;
        }
        return v;
    }

    public void ShowHud(bool on)
    {
        hud.gameObject.SetActive(on);
        bool mobile = Application.isMobilePlatform || touchSeen;
        touch.gameObject.SetActive(on && mobile && (Game.I == null || Game.I.LocalCount <= 1));
        if (!on) { foreach (var n in nameTags) { if (n.t) Destroy(n.t.gameObject); if (n.arrow) Destroy(n.arrow.gameObject); } nameTags.Clear(); }
        else BuildTags();
    }

    void BuildTags()
    {
        foreach (var n in nameTags) { if (n.t) Destroy(n.t.gameObject); if (n.arrow) Destroy(n.arrow.gameObject); }
        nameTags.Clear();
        var s = Game.I.S.Cur;
        for (int i = 0; i < s.n; i++)
        {
            var col = View.SlotColors[i];
            string label = Game.I.IsLocal(i) ? (Game.I.LocalCount > 1 ? "P" + (i + 1) : "YOU") : Game.I.NameOf(i).StartsWith("CPU") ? "CPU" : Game.I.NameOf(i);
            var t = Txt(tags, label, 30, Vector2.zero, Vector2.zero, col, TextAnchor.MiddleCenter, 300);
            Outline(t, 2);
            var a = Img(tags, arrowDown, Vector2.zero, Vector2.zero, new Vector2(34, 34)); a.color = col;
            nameTags.Add((i, t, a));
        }
    }

    public void UpdateHud(SimState s, Game.Mode mode)
    {
        if (!hud.gameObject.activeSelf) return;
        float aspect = (float)UnityEngine.Screen.width / Mathf.Max(1, UnityEngine.Screen.height);
        bool landscape = aspect > 1.1f;
        float spacing = landscape ? 300f : 262f, cw = landscape ? 280f : 250f;
        // portrait: tuck the pause button under the damage cards
        ((RectTransform)pauseBtn).anchoredPosition = landscape ? new Vector2(-70, -70) : new Vector2(-70, -235);
        for (int i = 0; i < 4; i++)
        {
            var c = cards[i];
            bool on = i < s.n;
            c.rt.gameObject.SetActive(on);
            if (!on) continue;
            var f = s.f[i];
            var def = Roster.All[f.ch];
            c.rt.anchoredPosition = new Vector2((i - (s.n - 1) * 0.5f) * spacing * (landscape ? 1.3f : 1f), landscape ? -120 : -95);
            c.rt.sizeDelta = new Vector2(cw, 150);
            float sc = landscape ? 1.3f : s.n >= 4 ? 0.9f : 1f;
            c.rt.localScale = Vector3.one * sc;
            if (!landscape) c.rt.anchoredPosition = new Vector2((i - (s.n - 1) * 0.5f) * (s.n >= 4 ? 238f : 262f), -95);
            c.pct.fontSize = f.dmg >= 100 ? 54 : 64;
            if (c.face.sprite == null || c.face.sprite.name != def.id) { c.face.sprite = Icon(def.id); }
            c.ring.color = View.SlotColors[i];
            c.name.text = Game.I.NameOf(i);
            bool dead = f.alive == 0;
            c.pct.text = dead ? "OUT" : f.dmg + "%";
            if (f.dmg != c.lastDmg) { if (f.dmg > c.lastDmg) c.bump = 1f; c.lastDmg = f.dmg; }
            c.bump = Mathf.Max(0, c.bump - Time.unscaledDeltaTime * 4f);
            c.pct.rectTransform.localScale = Vector3.one * (1f + c.bump * 0.35f);
            c.pct.rectTransform.anchoredPosition = new Vector2(-72 + (c.bump > 0 ? Mathf.Sin(Time.time * 80f) * 8f * c.bump : 0), 18);
            c.pct.color = dead ? Color.gray : DmgColor(f.dmg);
            for (int k = 0; k < 5; k++) { c.stocks[k].gameObject.SetActive(k < 3); c.stocks[k].color = k < f.stocks ? View.SlotColors[i] : new Color(1, 1, 1, 0.15f); }
            c.weapon.gameObject.SetActive(f.weapon != 0);
            if (f.weapon != 0) c.weapon.sprite = Icon("w" + f.weapon);
            c.face.color = dead ? new Color(1, 1, 1, 0.35f) : Color.white;
        }
        // name tags
        var cam = View.I.Cam;
        foreach (var (slot, t, a) in nameTags)
        {
            var fv = View.I.Fighter(slot);
            bool vis = fv != null && fv.Visible;
            t.gameObject.SetActive(vis); a.gameObject.SetActive(vis);
            if (!vis) continue;
            var sp = cam.WorldToScreenPoint(fv.Head);
            // off-screen fighters: pin the tag to the edge with the arrow pointing at them
            float m = 70f * canvas.scaleFactor;
            var clamped = new Vector3(Mathf.Clamp(sp.x, m, UnityEngine.Screen.width - m), Mathf.Clamp(sp.y, m, UnityEngine.Screen.height - m * 2.2f), 0);
            bool off = (clamped - new Vector3(sp.x, sp.y, 0)).sqrMagnitude > 4f;
            t.rectTransform.position = clamped + Vector3.up * 44f * canvas.scaleFactor;
            a.rectTransform.position = clamped + Vector3.up * 14f * canvas.scaleFactor;
            a.rectTransform.localRotation = off ? Quaternion.Euler(0, 0, Mathf.Atan2(sp.y - clamped.y, sp.x - clamped.x) * Mathf.Rad2Deg + 90f) : Quaternion.identity;
            a.rectTransform.localScale = Vector3.one * (off ? 1.6f : 1f);
        }
        // countdown
        if (s.frame < Sim.Go)
        {
            int n = 3 - s.frame / 60;
            string txt = n.ToString();
            if (bigText.text != txt && n >= 1) { Announce(txt, n == 1 ? Gold : Cream, 0.9f); Sfx.I.Beep(); }
        }
        if (s.over == 0 && s.frame > Sim.Go + Sim.TimeLimit - 600)
        {
            int left = (Sim.Go + Sim.TimeLimit - s.frame) / 60;
            if (left <= 10 && s.frame % 60 == 0) Announce(left.ToString(), Pink, 0.6f);
        }
    }

    static Color DmgColor(int d)
    {
        if (d < 50) return Color.Lerp(Color.white, Kit.Hex("#ffe27a"), d / 50f);
        if (d < 100) return Color.Lerp(Kit.Hex("#ffe27a"), Kit.Hex("#ff8a3d"), (d - 50) / 50f);
        if (d < 160) return Color.Lerp(Kit.Hex("#ff8a3d"), Kit.Hex("#ff2d2d"), (d - 100) / 60f);
        return Color.Lerp(Kit.Hex("#ff2d2d"), Kit.Hex("#8a0f2a"), Mathf.Clamp01((d - 160) / 80f));
    }

    public void Announce(string s, Color c, float t = 1.1f)
    {
        bigText.text = s; bigText.color = c; bigT = t; bigText.gameObject.SetActive(true);
    }
    public void Toast(string s) { toastText.text = s; toastT = 2.6f; toastText.gameObject.SetActive(true); }

    public void WorldText(Vector3 world, string s, Color c, float scale) => StartCoroutine(FloatCo(world, s, c, scale));
    IEnumerator FloatCo(Vector3 world, string s, Color c, float scale)
    {
        var t = Txt(floats, s, Mathf.RoundToInt(56 * scale), Vector2.zero, Vector2.zero, c, TextAnchor.MiddleCenter, 800);
        t.fontStyle = FontStyle.Italic; Outline(t, 4);
        float k = 0; float rot = UnityEngine.Random.Range(-12f, 12f);
        while (k < 1f)
        {
            k += Time.unscaledDeltaTime / 0.9f;
            t.rectTransform.position = View.I.Cam.WorldToScreenPoint(world) + Vector3.up * k * 90f * canvas.scaleFactor;
            t.rectTransform.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.3f, 1.3f, k / 0.15f) : Mathf.Lerp(1.3f, 1f, (k - 0.15f) * 3f));
            t.rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
            t.color = Kit.A(c, Mathf.Clamp01((1 - k) * 3));
            yield return null;
        }
        Destroy(t.gameObject);
    }

    // ---------------------------------------------------------------- screens
    RectTransform Screen(bool dim = true, float alpha = 0.72f)
    {
        foreach (Transform c in screens) Destroy(c.gameObject);
        var s = Fill("screen", screens);
        if (dim) { var img = s.gameObject.AddComponent<Image>(); img.color = new Color(0.06f, 0.03f, 0.12f, alpha); }
        return s;
    }
    public void CloseScreens() { foreach (Transform c in screens) Destroy(c.gameObject); Paused = false; }

    IEnumerator Pop(RectTransform r, float delay = 0)
    {
        r.localScale = Vector3.zero;
        float k = -delay / 0.28f;
        while (k < 1f) { k += Time.unscaledDeltaTime / 0.28f; r.localScale = Vector3.one * Kit.EaseOutBack(Mathf.Clamp01(k)); yield return null; }
        r.localScale = Vector3.one;
    }
    IEnumerator Pulse(Transform t) { while (t) { t.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.04f); yield return null; } }
    IEnumerator Wobble(Transform t, float amp) { while (t) { t.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.unscaledTime * 2.3f) * amp); yield return null; } }

    Text Logo(Transform p, float y, int size)
    {
        var holder = Rect("logo", p, new Vector2(.5f, 1), new Vector2(0, y), new Vector2(1200, size * 2.2f));
        var a = Txt(holder, "BONK", size, new Vector2(.5f, .5f), new Vector2(-8, size * 0.5f - 8), Kit.Hex("#7a1238"), TextAnchor.MiddleCenter, 1200); a.fontStyle = FontStyle.Italic;
        var b = Txt(holder, "BONK", size, new Vector2(.5f, .5f), new Vector2(0, size * 0.5f), Gold, TextAnchor.MiddleCenter, 1200); b.fontStyle = FontStyle.Italic; Outline(b, 6);
        var c = Txt(holder, "BRAWL", size, new Vector2(.5f, .5f), new Vector2(-8, -size * 0.45f - 8), Kit.Hex("#1a1060"), TextAnchor.MiddleCenter, 1200); c.fontStyle = FontStyle.Italic;
        var d = Txt(holder, "BRAWL", size, new Vector2(.5f, .5f), new Vector2(0, -size * 0.45f), Pink, TextAnchor.MiddleCenter, 1200); d.fontStyle = FontStyle.Italic; Outline(d, 6);
        StartCoroutine(Wobble(holder, 3f));
        return b;
    }

    public void ShowTitle()
    {
        ShowHud(false);
        var s = Screen(true, 0.35f);
        var g = Game.I;
        Logo(s, -330, 210);
        var tag = Txt(s, "PARTY PLATFORM FIGHTER  -  ONLINE WITH ROLLBACK", 32, new Vector2(.5f, 1), new Vector2(0, -600), Cream, TextAnchor.MiddleCenter, 1100); Outline(tag, 2);
        var fight = Btn(s, "FIGHT!", new Vector2(.5f, 0), new Vector2(0, 720), new Vector2(680, 190), Pink, Color.white, () => ShowSelect("cpu"), 92);
        StartCoroutine(Pulse(fight.transform));
        Btn(s, "ONLINE", new Vector2(.5f, 0), new Vector2(-175, 525), new Vector2(330, 140), Cyan, Ink, () => ShowSelect("online"), 54);
        Btn(s, "2 PLAYERS", new Vector2(.5f, 0), new Vector2(175, 525), new Vector2(330, 140), Gold, Ink, () => ShowSelect("2p"), 46);
        Btn(s, "LEADERBOARD", new Vector2(.5f, 0), new Vector2(0, 375), new Vector2(680, 110), new Color(1, 1, 1, 0.18f), Gold, () => WebBridge.ShowBoard(), 42);
        Btn(s, "HOW TO PLAY", new Vector2(.5f, 0), new Vector2(-175, 245), new Vector2(330, 100), new Color(1, 1, 1, 0.14f), Cream, ShowHowTo, 34);
        Btn(s, g.Save.muted ? "SOUND OFF" : "SOUND ON", new Vector2(.5f, 0), new Vector2(175, 245), new Vector2(330, 100), new Color(1, 1, 1, 0.14f), Cream, () => { g.Save.muted = !g.Save.muted; Sfx.I.SetMuted(g.Save.muted); g.Persist(); ShowTitle(); }, 34);
        if (g.Save.played > 0) Txt(s, g.Save.played + " FIGHTS   -   " + g.Save.won + " WINS   -   " + g.Save.kos + " KOs", 30, new Vector2(.5f, 0), new Vector2(0, 150), Kit.A(Cream, 0.8f), TextAnchor.MiddleCenter, 900);
    }

    // ---------------------------------------------------------------- select
    public void ShowSelect(string mode)
    {
        selectMode = mode;
        var s = Screen(true, 0.8f);
        var g = Game.I;
        bool twoP = mode == "2p";
        Txt(s, twoP ? "PLAYER 1  -  CHOOSE" : "CHOOSE YOUR FIGHTER", 64, new Vector2(.5f, 1), new Vector2(0, -130), Gold, TextAnchor.MiddleCenter, 1200).fontStyle = FontStyle.Italic;
        // big preview
        var def = Roster.All[g.Save.ch];
        var prev = Box(s, new Vector2(.5f, 1), new Vector2(0, -420), new Vector2(940, 400), new Color(1, 1, 1, 0.08f));
        Img(prev, Icon(def.id), new Vector2(0, .5f), new Vector2(210, 0), new Vector2(360, 360));
        var nm = Txt(prev, def.name, 84, new Vector2(0, .5f), new Vector2(640, 90), Color.white, TextAnchor.MiddleLeft, 520); nm.fontStyle = FontStyle.Italic; Outline(nm, 4);
        Txt(prev, def.cls + " FIGHTER", 36, new Vector2(0, .5f), new Vector2(640, 5), def.cls == "HEAVY" ? Kit.Hex("#ff8a3d") : def.cls == "LIGHT" ? Cyan : Lime, TextAnchor.MiddleLeft, 520);
        Txt(prev, "FROM " + def.from, 28, new Vector2(0, .5f), new Vector2(640, -55), Kit.A(Cream, 0.7f), TextAnchor.MiddleLeft, 520);
        Stat(prev, "POWER", Mathf.InverseLerp(80, 124, def.weight), -110);
        Stat(prev, "SPEED", Mathf.InverseLerp(84, 116, def.speed), -150);
        // roster grid: more columns and smaller cells as the cast grows
        int n = Roster.All.Length, cols = n <= 10 ? 5 : n <= 18 ? 6 : 7;
        float px = n <= 10 ? 190f : 1010f / cols, cw = px - 12f, py = n <= 10 ? 200f : cw + 14f;
        float gy = n <= 10 ? -710f : -700f;
        for (int i = 0; i < Roster.All.Length; i++)
        {
            int idx = i;
            var r = Roster.All[i];
            bool sel = i == g.Save.ch, sel2 = twoP && i == g.Save.ch2;
            var cell = Box(s, new Vector2(.5f, 1), new Vector2((i % cols - (cols - 1) / 2f) * px, gy - (i / cols) * py), new Vector2(cw, cw + 10f), sel ? Pink : sel2 ? Cyan : r.from == "SPOOKTOBER" ? Kit.A(Kit.Hex("#ff7a1a"), 0.22f) : new Color(1, 1, 1, 0.12f), true);
            Img(cell, Icon(r.id), new Vector2(.5f, .5f), new Vector2(0, cw * 0.08f), new Vector2(cw * 0.85f, cw * 0.85f));
            Txt(cell, r.name, cw < 170 ? 21 : 24, new Vector2(.5f, 0), new Vector2(0, 16), Color.white, TextAnchor.MiddleCenter, (int)cw);
            var b = cell.gameObject.AddComponent<Button>(); b.targetGraphic = cell.GetComponent<Image>();
            b.onClick.AddListener(() =>
            {
                Sfx.I.Click();
                if (twoP && sel && idx != g.Save.ch) { }
                g.Save.ch = idx; g.Persist(); ShowSelect(selectMode);
            });
        }
        float y = gy - ((n - 1) / cols) * py - cw / 2f - 140f;
        if (twoP)
        {
            Txt(s, "PLAYER 2", 34, new Vector2(.5f, 1), new Vector2(-330, y), Cyan, TextAnchor.MiddleCenter, 300);
            Btn(s, "<", new Vector2(.5f, 1), new Vector2(-120, y), new Vector2(90, 90), new Color(1, 1, 1, 0.18f), Color.white, () => { g.Save.ch2 = (g.Save.ch2 + Roster.All.Length - 1) % Roster.All.Length; g.Persist(); ShowSelect(selectMode); }, 50);
            Txt(s, Roster.All[g.Save.ch2].name, 40, new Vector2(.5f, 1), new Vector2(60, y), Color.white, TextAnchor.MiddleCenter, 260);
            Btn(s, ">", new Vector2(.5f, 1), new Vector2(240, y), new Vector2(90, 90), new Color(1, 1, 1, 0.18f), Color.white, () => { g.Save.ch2 = (g.Save.ch2 + 1) % Roster.All.Length; g.Persist(); ShowSelect(selectMode); }, 50);
            y -= 120;
        }
        if (mode != "online")
        {
            // stage
            string stageName = g.Save.stage < 0 ? "RANDOM STAGE" : Stages.All[g.Save.stage].name;
            Btn(s, "<", new Vector2(.5f, 1), new Vector2(-380, y), new Vector2(90, 90), new Color(1, 1, 1, 0.18f), Color.white, () => { g.Save.stage = g.Save.stage <= -1 ? Stages.All.Length - 1 : g.Save.stage - 1; g.Persist(); ShowSelect(selectMode); }, 50);
            Txt(s, stageName, 42, new Vector2(.5f, 1), new Vector2(0, y), Gold, TextAnchor.MiddleCenter, 600);
            Btn(s, ">", new Vector2(.5f, 1), new Vector2(380, y), new Vector2(90, 90), new Color(1, 1, 1, 0.18f), Color.white, () => { g.Save.stage = g.Save.stage >= Stages.All.Length - 1 ? -1 : g.Save.stage + 1; g.Persist(); ShowSelect(selectMode); }, 50);
            y -= 115;
            // cpus + difficulty
            int maxBots = twoP ? 2 : 3, minBots = twoP ? 0 : 1;
            Btn(s, "CPUs: " + g.Save.bots, new Vector2(.5f, 1), new Vector2(-230, y), new Vector2(400, 100), new Color(1, 1, 1, 0.18f), Color.white, () => { g.Save.bots = g.Save.bots >= maxBots ? minBots : g.Save.bots + 1; g.Persist(); ShowSelect(selectMode); }, 40);
            string[] lv = { "", "EASY", "NORMAL", "HARD" };
            Btn(s, lv[Mathf.Clamp(g.Save.botLv, 1, 3)], new Vector2(.5f, 1), new Vector2(230, y), new Vector2(400, 100), new Color(1, 1, 1, 0.18f), g.Save.botLv == 3 ? Pink : g.Save.botLv == 1 ? Lime : Gold, () => { g.Save.botLv = g.Save.botLv >= 3 ? 1 : g.Save.botLv + 1; g.Persist(); ShowSelect(selectMode); }, 40);
            if (twoP && g.Save.bots > 2) g.Save.bots = 2;
            if (!twoP && g.Save.bots < 1) g.Save.bots = 1;
        }
        var go = Btn(s, mode == "online" ? "FIND A MATCH" : "BRAWL!", new Vector2(.5f, 0), new Vector2(0, 260), new Vector2(680, 170), Pink, Color.white, () =>
        {
            if (mode == "online") { CloseScreens(); Game.I.OpenOnline(); ShowTitle(); }
            else Game.I.StartLocal(mode == "2p");
        }, 70);
        StartCoroutine(Pulse(go.transform));
        Btn(s, "BACK", new Vector2(.5f, 0), new Vector2(0, 110), new Vector2(360, 100), new Color(1, 1, 1, 0.14f), Cream, ShowTitle, 38);
    }

    void Stat(Transform p, string label, float v, float y)
    {
        Txt(p, label, 24, new Vector2(0, .5f), new Vector2(460, y), Kit.A(Cream, 0.7f), TextAnchor.MiddleLeft, 140);
        var bg = Box(p, new Vector2(0, .5f), new Vector2(720, y), new Vector2(300, 18), new Color(1, 1, 1, 0.15f));
        var f = Box(bg, new Vector2(0, .5f), Vector2.zero, new Vector2(300 * Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(v)), 18), Gold);
        f.pivot = new Vector2(0, .5f);
    }

    public void ShowHowTo()
    {
        var s = Screen(true, 0.88f);
        Txt(s, "HOW TO BONK", 96, new Vector2(.5f, 1), new Vector2(0, -170), Gold, TextAnchor.MiddleCenter, 1200).fontStyle = FontStyle.Italic;
        (string head, string body)[] rows =
        {
            ("KNOCK THEM OFF", "Hits raise damage %. The higher it gets, the further they fly.\nLaunch rivals past the edge of the screen to take a stock."),
            ("ATTACK + DIRECTION", "Neutral, side, up and down each do something different,\non the ground and in the air."),
            ("HEAVY = SIGNATURE", "Hold HEAVY to charge. UP + HEAVY is your recovery -\nuse it to get back when you're knocked off."),
            ("JUMP x3 + DODGE", "Two extra jumps in the air. DODGE makes you untouchable\nfor a moment - dodge a heavy, then punish it."),
            ("WEAPONS FALL FROM THE SKY", "THROW picks them up (or throws them!): sword, spear,\nfrying pan, blaster, grenade. The pan goes BONK."),
            ("CONTROLS", "Keys: WASD move, SPACE jump, J attack, K heavy,\nL dodge, H throw.  Gamepads & touch work too."),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            var row = Box(s, new Vector2(.5f, 1), new Vector2(0, -350 - i * 205), new Vector2(980, 185), new Color(1, 1, 1, 0.08f));
            Txt(row, rows[i].head, 40, new Vector2(0, 1), new Vector2(490, -38), i % 2 == 0 ? Pink : Cyan, TextAnchor.MiddleCenter, 940).fontStyle = FontStyle.Italic;
            var t = Txt(row, rows[i].body, 30, new Vector2(0, .5f), new Vector2(490, -22), Cream, TextAnchor.MiddleCenter, 940);
            t.lineSpacing = 1.1f;
            StartCoroutine(Pop(row, 0.05f * i));
        }
        Btn(s, "LET'S BRAWL", new Vector2(.5f, 0), new Vector2(0, 170), new Vector2(600, 150), Pink, Color.white, () =>
        {
            Game.I.Save.howto = true; Game.I.Persist();
            if (Game.I.M == Game.Mode.Menu) ShowTitle(); else CloseScreens();
        }, 56);
    }

    // window lost focus: pause local matches (online ones keep going)
    public void PauseIfLocal() { if (Game.I.M == Game.Mode.Local && !Paused && hudOn) TogglePause(); }
    bool hudOn => pauseBtn && pauseBtn.gameObject.activeInHierarchy;

    void TogglePause()
    {
        if (Paused) { CloseScreens(); return; }
        var s = Screen(true, 0.7f);
        bool online = Game.I.M == Game.Mode.Online;
        Paused = !online;
        Txt(s, online ? "MENU" : "PAUSED", 120, new Vector2(.5f, 1), new Vector2(0, -520), Cream).fontStyle = FontStyle.Italic;
        if (online) Txt(s, "The fight goes on while you're here!", 36, new Vector2(.5f, 1), new Vector2(0, -640), Cream);
        Btn(s, "RESUME", new Vector2(.5f, .5f), new Vector2(0, 120), new Vector2(600, 160), Lime, Ink, CloseScreens, 60);
        if (!online) Btn(s, "RESTART", new Vector2(.5f, .5f), new Vector2(0, -70), new Vector2(600, 130), new Color(1, 1, 1, 0.18f), Cream, () => { CloseScreens(); Game.I.Rematch(); }, 46);
        Btn(s, "QUIT FIGHT", new Vector2(.5f, .5f), new Vector2(0, -240), new Vector2(600, 130), new Color(1, 1, 1, 0.18f), Pink, () => { CloseScreens(); Game.I.Quit(); }, 46);
    }

    public void ShowResults(SimState c, string[] names, bool online)
    {
        ShowHud(false);
        var s = Screen(true, 0.68f);
        int win = c.winner;
        var g = Game.I;
        string title = win < 0 ? "DRAW!" : g.IsLocal(win) ? (g.LocalCount > 1 ? "P" + (win + 1) + " WINS!" : "YOU WIN!") : Roster.All[c.f[win].ch].name + " WINS!";
        var t = Txt(s, title, 130, new Vector2(.5f, 1), new Vector2(0, -250), win >= 0 && g.IsLocal(win) ? Gold : Cream, TextAnchor.MiddleCenter, 1400);
        t.fontStyle = FontStyle.Italic; Outline(t, 6);
        StartCoroutine(Pop(t.rectTransform));
        // placings
        var order = new List<int>();
        for (int i = 0; i < c.n; i++) order.Add(i);
        order.Sort((a, b) => (c.f[a].place == 0 ? 9 : c.f[a].place).CompareTo(c.f[b].place == 0 ? 9 : c.f[b].place));
        for (int k = 0; k < order.Count; k++)
        {
            int i = order[k]; var f = c.f[i];
            var row = Box(s, new Vector2(.5f, 1), new Vector2(0, -470 - k * 170), new Vector2(960, 150), g.IsLocal(i) ? Kit.A(View.SlotColors[i], 0.45f) : new Color(1, 1, 1, 0.09f));
            var pl = Txt(row, "#" + Mathf.Max(1, f.place), 64, new Vector2(0, .5f), new Vector2(70, 0), k == 0 ? Gold : Cream, TextAnchor.MiddleCenter, 120); pl.fontStyle = FontStyle.Italic;
            Img(row, Icon(Roster.All[f.ch].id), new Vector2(0, .5f), new Vector2(210, 0), new Vector2(130, 130));
            var nmT = Txt(row, i < names.Length ? names[i] : "", 36, new Vector2(0, .5f), new Vector2(430, 22), Color.white, TextAnchor.MiddleLeft, 240); nmT.horizontalOverflow = HorizontalWrapMode.Wrap; nmT.resizeTextForBestFit = true; nmT.resizeTextMinSize = 20; nmT.resizeTextMaxSize = 36; nmT.rectTransform.sizeDelta = new Vector2(240, 50);
            Txt(row, Roster.All[f.ch].name, 24, new Vector2(0, .5f), new Vector2(430, -28), Kit.A(Cream, 0.7f), TextAnchor.MiddleLeft, 240);
            Txt(row, f.kos + " KO  " + f.falls + " FALLS  " + f.dealt + " DMG", 28, new Vector2(1, .5f), new Vector2(-24, 0), Cream, TextAnchor.MiddleRight, 360).rectTransform.pivot = new Vector2(1, .5f);
            StartCoroutine(Pop(row, 0.15f + 0.08f * k));
        }
        rankText = Txt(s, "", 34, new Vector2(.5f, 0), new Vector2(0, 640), Gold, TextAnchor.MiddleCenter, 1000);
        var again = Btn(s, "REMATCH", new Vector2(.5f, 0), new Vector2(0, 480), new Vector2(620, 160), Pink, Color.white, () => g.Rematch(), 64);
        StartCoroutine(Pulse(again.transform));
        var share = Btn(s, "SHARE", new Vector2(.5f, 0), new Vector2(-320, 300), new Vector2(290, 120), new Color(1, 1, 1, 0.18f), Cream, () => { }, 42);
        share.gameObject.AddComponent<ShareOnPress>().Text = () => g.ShareText();
        Btn(s, online ? "LEAVE" : "FIGHTERS", new Vector2(.5f, 0), new Vector2(0, 300), new Vector2(290, 120), new Color(1, 1, 1, 0.18f), Cream, () => { if (online) g.Quit(); else { g.Quit(); ShowSelect(g.LocalCount > 1 ? "2p" : "cpu"); } }, 38);
        Btn(s, "MENU", new Vector2(.5f, 0), new Vector2(320, 300), new Vector2(290, 120), new Color(1, 1, 1, 0.18f), Cream, () => g.Quit(), 42);
    }

    public void SetRank(Game.RankMsg m)
    {
        if (!rankText || m == null || m.rank <= 0) return;
        rankText.text = "ONLINE WINS: " + m.wins + "   -   WORLD RANK #" + m.rank + " OF " + m.total;
    }

    // ---------------------------------------------------------------- per frame
    void Update()
    {
        float aspect = (float)UnityEngine.Screen.width / Mathf.Max(1, UnityEngine.Screen.height);
        scaler.matchWidthOrHeight = aspect > 0.75f ? 1f : 0f;
        float udt = Time.unscaledDeltaTime;
        if (bigT > 0)
        {
            bigT -= udt;
            float k = 1f - bigT;
            bigText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, Mathf.Clamp01(k * 5f));
            bigText.color = Kit.A(bigText.color, Mathf.Clamp01(bigT * 3f));
            if (bigT <= 0) bigText.gameObject.SetActive(false);
        }
        if (toastT > 0) { toastT -= udt; toastText.color = Kit.A(toastText.color, Mathf.Clamp01(toastT * 2f)); if (toastT <= 0) toastText.gameObject.SetActive(false); }
        UpdateJoystick();
        if (Input.touchCount > 0 && !touchSeen) { touchSeen = true; if (hud.gameObject.activeSelf) ShowHud(true); }
    }

    void UpdateJoystick()
    {
        Vector2? pos = null;
        bool live = touch.gameObject.activeSelf;
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                bool left = t.position.x < UnityEngine.Screen.width * 0.5f;
                if (joyId == -99 && t.phase == TouchPhase.Began && left && !OverUI(t.fingerId)) { joyId = t.fingerId; joyOrigin = t.position; joyMouse = false; }
                if (t.fingerId == joyId) { if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) joyId = -99; else pos = t.position; }
            }
        }
        else if (live)
        {
            var mp = (Vector2)Input.mousePosition;
            bool left = mp.x < UnityEngine.Screen.width * 0.5f;
            if (Input.GetMouseButtonDown(0) && left && !OverUI(-1)) { joyId = -1; joyOrigin = mp; joyMouse = true; }
            if (joyMouse && joyId == -1) { if (Input.GetMouseButton(0)) pos = mp; else joyId = -99; }
        }
        if (pos.HasValue && live)
        {
            float r = JoyR * canvas.scaleFactor;
            var d = pos.Value - joyOrigin;
            if (d.magnitude > r) { joyOrigin += d.normalized * (d.magnitude - r); d = pos.Value - joyOrigin; }
            joyVec = d / r;
            joyBase.gameObject.SetActive(true); joyBase.position = joyOrigin; joyKnob.anchoredPosition = d / canvas.scaleFactor;
        }
        else { joyVec = Vector2.zero; joyBase.gameObject.SetActive(false); if (!pos.HasValue) joyId = -99; }
    }

    static bool OverUI(int id)
    {
        if (!EventSystem.current) return false;
        return id < 0 ? EventSystem.current.IsPointerOverGameObject() : EventSystem.current.IsPointerOverGameObject(id);
    }
}

public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    readonly HashSet<int> ids = new HashSet<int>();
    public bool Held => ids.Count > 0;
    public Action OnDown;
    public void OnPointerDown(PointerEventData e) { ids.Add(e.pointerId); transform.localScale = Vector3.one * 0.9f; OnDown?.Invoke(); }
    public void OnPointerUp(PointerEventData e) { ids.Remove(e.pointerId); if (ids.Count == 0) transform.localScale = Vector3.one; }
    void OnDisable() { ids.Clear(); transform.localScale = Vector3.one; }
}
