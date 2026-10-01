using UnityEngine;

// Visuals for each stage. Collision lives in Defs (StageDef); this only has to line up with it.
public static class StageArt
{
    static Transform root;

    public static void Build(int stage, Transform parent, Camera cam, Light sun)
    {
        root = parent;
        var sd = Stages.All[stage];
        float L = sd.L / 1000f, R = sd.R / 1000f, T = sd.T / 1000f, B = sd.B / 1000f;
        switch (sd.id)
        {
            case "rooftop": Rooftop(sd, L, R, T, B, cam, sun); break;
            case "graveyard": Graveyard(sd, L, R, T, B, cam, sun); break;
            case "moonbase": Moonbase(sd, L, R, T, B, cam, sun); break;
            case "hollow": Hollow(sd, L, R, T, B, cam, sun); break;
            default: Kitchen(sd, L, R, T, B, cam, sun); break;
        }
    }

    // ---------------------------------------------------------------- helpers
    public static Mesh BoxMesh(float w, float h, float d)
    {
        var m = new Mesh();
        float x = w / 2, y = h / 2, z = d / 2;
        Vector3[] c = { new Vector3(-x, -y, -z), new Vector3(x, -y, -z), new Vector3(x, y, -z), new Vector3(-x, y, -z), new Vector3(-x, -y, z), new Vector3(x, -y, z), new Vector3(x, y, z), new Vector3(-x, y, z) };
        int[][] faces = { new[] { 0, 3, 2, 1 }, new[] { 5, 6, 7, 4 }, new[] { 4, 7, 3, 0 }, new[] { 1, 2, 6, 5 }, new[] { 3, 7, 6, 2 }, new[] { 4, 0, 1, 5 } };
        Vector3[] n = { Vector3.back, Vector3.forward, Vector3.left, Vector3.right, Vector3.up, Vector3.down };
        var v = new Vector3[24]; var nn = new Vector3[24]; var uv = new Vector2[24]; var tri = new int[36];
        for (int f = 0; f < 6; f++)
        {
            for (int k = 0; k < 4; k++) { v[f * 4 + k] = c[faces[f][k]]; nn[f * 4 + k] = n[f]; }
            float uw = f < 2 ? w : f < 4 ? d : w, vh = f < 4 ? h : d;
            uv[f * 4] = new Vector2(0, 0); uv[f * 4 + 1] = new Vector2(0, vh); uv[f * 4 + 2] = new Vector2(uw, vh); uv[f * 4 + 3] = new Vector2(uw, 0);
            int b = f * 4, t = f * 6;
            tri[t] = b; tri[t + 1] = b + 1; tri[t + 2] = b + 2; tri[t + 3] = b; tri[t + 4] = b + 2; tri[t + 5] = b + 3;
        }
        m.vertices = v; m.normals = nn; m.uv = uv; m.triangles = tri; m.RecalculateBounds();
        return m;
    }

    static Material Mat(Color c, float gloss = 0.15f, Texture tex = null, Color? emit = null)
    {
        var m = new Material(Shader.Find("Standard")) { color = c };
        if (tex) { m.mainTexture = tex; }
        m.SetFloat("_Glossiness", gloss);
        if (emit.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emit.Value); }
        return m;
    }

    static GameObject Box(string name, Vector3 center, Vector3 size, Material mat, bool shadows = true)
    {
        var go = Kit.MeshObject(name, BoxMesh(size.x, size.y, size.z));
        go.transform.SetParent(root, false);
        go.transform.localPosition = center;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    static GameObject Prop(string path, float height, Vector3 pos, float yaw = 0, bool fitWidth = false)
    {
        var go = Kit.Spawn(path, 1f, root, pos, yaw);
        var b = Kit.WorldBounds(go);
        float s = fitWidth ? height / Mathf.Max(0.01f, b.size.x) : height / Mathf.Max(0.01f, b.size.y);
        go.transform.localScale = Vector3.one * s;
        return go;
    }

    static void Sky(Color top, Color bottom, Camera cam, float z = 70f)
    {
        cam.backgroundColor = bottom;
        var t = new Texture2D(1, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++) t.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
        t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
        var q = Kit.MeshObject("sky", Kit.BuildQuad(1f, 1f));
        q.transform.SetParent(root, false);
        q.transform.localPosition = new Vector3(0, 8f, z);
        q.transform.localScale = new Vector3(260f, 90f, 1);
        var m = new Material(Kit.UnlitAlpha) { mainTexture = t, color = Color.white };
        q.GetComponent<MeshRenderer>().sharedMaterial = m;
    }

    static void GlowSprite(Vector3 at, float size, Color c, int order = -5)
    {
        var s = new GameObject("glow").AddComponent<SpriteRenderer>();
        s.sprite = View.Glow; s.color = c; s.sortingOrder = order;
        s.transform.SetParent(root, false); s.transform.localPosition = at; s.transform.localScale = Vector3.one * size;
    }

    static void Stars(int n, float z, Color c, int seed)
    {
        var rng = new System.Random(seed);
        for (int i = 0; i < n; i++)
            GlowSprite(new Vector3((float)rng.NextDouble() * 180f - 90f, (float)rng.NextDouble() * 50f + 2f, z), 0.4f + (float)rng.NextDouble() * 0.7f, Kit.A(c, 0.4f + (float)rng.NextDouble() * 0.6f), -8);
    }

    static void Light(Light sun, Color c, float intensity, Vector3 euler, Color sky, Color eq, Color gnd, Color fog, float fogStart, float fogEnd)
    {
        sun.color = c; sun.intensity = intensity; sun.transform.rotation = Quaternion.Euler(euler);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.ambientGroundColor = gnd;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = fog;
        RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
    }

    static void Softs(StageDef sd, Material top, Material edge, float thick = 0.3f, float depth = 2f)
    {
        for (int k = 0; k < sd.soft.Length; k += 3)
        {
            float x0 = sd.soft[k] / 1000f, x1 = sd.soft[k + 1] / 1000f, y = sd.soft[k + 2] / 1000f;
            Box("soft", new Vector3((x0 + x1) / 2, y - thick / 2, 0), new Vector3(x1 - x0, thick, depth), top);
            if (edge) Box("softEdge", new Vector3((x0 + x1) / 2, y - thick - 0.04f, -depth / 2), new Vector3(x1 - x0, 0.08f, 0.08f), edge, false);
        }
    }

    static Texture2D Stripes(Color a, Color b, int n = 32)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) t.SetPixel(x, y, ((x + y) / (n / 4)) % 2 == 0 ? a : b);
        t.Apply(); t.wrapMode = TextureWrapMode.Repeat; t.filterMode = FilterMode.Point;
        return t;
    }

    static Texture2D Bricks(Color a, Color mortar)
    {
        int n = 64; var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            int row = y / 16; int xx = (x + (row % 2) * 16) % 32;
            bool m = y % 16 < 2 || xx < 2;
            float noise = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.12f;
            t.SetPixel(x, y, m ? mortar : Color.Lerp(a, a * 0.8f, noise * 4f));
        }
        t.Apply(); t.wrapMode = TextureWrapMode.Repeat;
        return t;
    }

    // ---------------------------------------------------------------- NEON ROOFTOP (City Rush)
    static void Rooftop(StageDef sd, float L, float R, float T, float B, Camera cam, Light sun)
    {
        Sky(Kit.Hex("#1a0f3d"), Kit.Hex("#ff7a59"), cam);
        GlowSprite(new Vector3(10f, 4f, 60f), 40f, new Color(1f, 0.6f, 0.4f, 0.55f));
        Stars(40, 66f, Color.white, 3);
        Light(sun, Kit.Hex("#ffd0a8"), 0.95f, new Vector3(35, -40, 0), Kit.Hex("#7f6fb8"), Kit.Hex("#8a5a78"), Kit.Hex("#2a1830"), Kit.Hex("#7a4270"), 16f, 62f);   // dusk haze: the skyline sits back, fighters pop
        var concrete = Mat(Kit.Hex("#6d7083"), 0.1f, Bricks(Kit.Hex("#6d7083"), Kit.Hex("#50526a")));
        concrete.mainTextureScale = new Vector2(0.5f, 0.5f);
        Box("roof", new Vector3((L + R) / 2, (T + B) / 2 - 3f, 0), new Vector3(R - L, T - B + 6f, 3f), concrete);
        var neon = Mat(Color.black, 0.5f, null, Kit.Hex("#ff3db7") * 2.2f);
        var neon2 = Mat(Color.black, 0.5f, null, Kit.Hex("#2ee6ff") * 2.2f);
        Box("neon", new Vector3((L + R) / 2, T - 0.06f, -1.52f), new Vector3(R - L, 0.12f, 0.06f), neon, false);
        Box("neonL", new Vector3(L - 0.03f, T - 2f, -1.52f), new Vector3(0.1f, 4f, 0.06f), neon2, false);
        Box("neonR", new Vector3(R + 0.03f, T - 2f, -1.52f), new Vector3(0.1f, 4f, 0.06f), neon2, false);
        Box("ledge", new Vector3((L + R) / 2, T + 0.1f, 1.35f), new Vector3(R - L, 0.3f, 0.3f), Mat(Kit.Hex("#8a8da0")));
        // billboards as soft platforms
        Softs(sd, Mat(Kit.Hex("#23213a"), 0.4f), neon, 0.35f, 1.8f);
        for (int k = 0; k < sd.soft.Length; k += 3)
        {
            float x0 = sd.soft[k] / 1000f, x1 = sd.soft[k + 1] / 1000f, y = sd.soft[k + 2] / 1000f;
            Box("pole", new Vector3((x0 + x1) / 2, (y + T) / 2, 0.8f), new Vector3(0.18f, y - T, 0.18f), Mat(Kit.Hex("#3a3a50")));
            Box("sign", new Vector3((x0 + x1) / 2, y + 0.9f, 0.95f), new Vector3((x1 - x0) * 0.9f, 1.4f, 0.1f), Mat(Kit.Hex("#15122a"), 0.6f, null, (k % 2 == 0 ? Kit.Hex("#ff3db7") : Kit.Hex("#7c5cff")) * 0.8f), false);
        }
        Prop("Blaster/crate-medium", 0.9f, new Vector3(L + 1.2f, T, 1.1f), 20);
        Prop("City/detail-parasol-a", 2.2f, new Vector3(R - 1.4f, T, 1f));
        // skyline
        string[] towers = { "City/building-skyscraper-a", "City/building-skyscraper-b", "City/building-skyscraper-c", "City/building-skyscraper-d", "City/building-skyscraper-e", "City/building-h", "City/building-k" };
        var rng = new System.Random(7);
        for (int i = 0; i < 22; i++)
        {
            float x = -75f + i * 7f + (float)rng.NextDouble() * 2f;
            float z = 34f + (float)rng.NextDouble() * 28f;
            Prop(towers[rng.Next(towers.Length)], 18f + (float)rng.NextDouble() * 24f, new Vector3(x, -26f, z), rng.Next(4) * 90f);
        }
    }

    // ---------------------------------------------------------------- MOONLIT CRYPT (Grave Shift)
    static void Graveyard(StageDef sd, float L, float R, float T, float B, Camera cam, Light sun)
    {
        Sky(Kit.Hex("#0b0a1f"), Kit.Hex("#2b3b6b"), cam);
        GlowSprite(new Vector3(-12f, 16f, 58f), 12f, new Color(0.95f, 0.95f, 1f, 1f));
        GlowSprite(new Vector3(-12f, 16f, 59f), 34f, new Color(0.6f, 0.7f, 1f, 0.35f));
        Stars(60, 66f, Color.white, 5);
        Light(sun, Kit.Hex("#cdd6ff"), 0.85f, new Vector3(40, 30, 0), Kit.Hex("#46506e"), Kit.Hex("#2f3542"), Kit.Hex("#1a1a24"), Kit.Hex("#1c2340"), 25f, 80f);
        var stone = Mat(Kit.Hex("#7b7a92"), 0.05f, Bricks(Kit.Hex("#7b7a92"), Kit.Hex("#55546a")));
        stone.mainTextureScale = new Vector2(0.4f, 0.4f);
        Box("crypt-base", new Vector3((L + R) / 2, (T + B) / 2 - 2f, 0), new Vector3(R - L, T - B + 4f, 3f), stone);
        Box("grass", new Vector3((L + R) / 2, T - 0.05f, 0), new Vector3(R - L + 0.2f, 0.14f, 3.1f), Mat(Kit.Hex("#3b5a34"), 0.05f));
        Softs(sd, Mat(Kit.Hex("#7a5230"), 0.1f), null, 0.28f, 1.6f);
        for (int k = 0; k < sd.soft.Length; k += 3)
        {
            float x0 = sd.soft[k] / 1000f, x1 = sd.soft[k + 1] / 1000f, y = sd.soft[k + 2] / 1000f;
            Box("chain", new Vector3(x0 + 0.2f, y + 4f, 0), new Vector3(0.06f, 8f, 0.06f), Mat(Kit.Hex("#3a3a44")));
            Box("chain", new Vector3(x1 - 0.2f, y + 4f, 0), new Vector3(0.06f, 8f, 0.06f), Mat(Kit.Hex("#3a3a44")));
        }
        Prop("Graveyard/crypt-large", 5f, new Vector3(0, T - 1.5f, 11f), 180);
        string[] stones = { "Graveyard/gravestone-round", "Graveyard/gravestone-cross", "Graveyard/gravestone-bevel", "Graveyard/gravestone-decorative", "Graveyard/gravestone-broken" };
        for (int i = 0; i < 6; i++) Prop(stones[i % stones.Length], 0.9f + i % 2 * 0.3f, new Vector3(L + 0.9f + i * (R - L - 1.8f) / 5f, T, 1.1f), 180 + (i * 17) % 30 - 15);
        Prop("Graveyard/pumpkin-carved", 0.6f, new Vector3(L + 2.4f, T, 0.9f), 190);
        Prop("Graveyard/lightpost-single", 3.2f, new Vector3(R - 0.6f, T, 1.2f), 180);
        GlowSprite(new Vector3(R - 0.6f, T + 3.1f, 1f), 3f, new Color(1f, 0.8f, 0.4f, 0.6f), 1);
        Prop("Graveyard/iron-fence", 1.4f, new Vector3(L + 1.5f, T, 1.45f), 0, false);
        var rng = new System.Random(11);
        for (int i = 0; i < 28; i++)
            Prop(rng.Next(3) == 0 ? "Graveyard/pine-crooked" : "Graveyard/pine", 6f + (float)rng.NextDouble() * 6f, new Vector3(-55f + i * 4f, -5f, 20f + (float)rng.NextDouble() * 22f), rng.Next(360));
    }

    // ---------------------------------------------------------------- HAUNTED HOLLOW (Spooktober)
    static void TopAt(GameObject go, float y)
    {
        var b = Kit.WorldBounds(go);
        go.transform.position += new Vector3(0, y - b.max.y, 0);
    }

    static void Hollow(StageDef sd, float L, float R, float T, float B, Camera cam, Light sun)
    {
        Sky(Kit.Hex("#14062b"), Kit.Hex("#6a2c55"), cam);
        // a fat harvest moon
        GlowSprite(new Vector3(14f, 15f, 58f), 13f, Kit.Hex("#ffb35a"));
        GlowSprite(new Vector3(14f, 15f, 59f), 38f, new Color(1f, 0.55f, 0.2f, 0.35f));
        Stars(70, 66f, Kit.Hex("#ffd9f0"), 31);
        Light(sun, Kit.Hex("#ffc89a"), 0.9f, new Vector3(38, -35, 0), Kit.Hex("#6a4a8a"), Kit.Hex("#4a2f4f"), Kit.Hex("#1f1420"), Kit.Hex("#2a1238"), 26f, 85f);

        // the hill: soil block under a row of graveyard tiles
        var soil = Mat(Kit.Hex("#3b2433"), 0.05f, Kit.Noise(64, Kit.Hex("#3b2433"), Kit.Hex("#2a1824"), 0.1f, 2, 4));
        Box("soil", new Vector3((L + R) / 2, (T + B) / 2 - 0.3f, 0), new Vector3(R - L, T - B - 0.6f, 2.9f), soil);
        float tw = (R - L) / 4f;
        for (int i = 0; i < 4; i++)
        {
            var tile = Prop("Spooky/tileLarge_graveyard", tw, new Vector3(L + tw * (i + 0.5f), T, 0), i * 90, true);
            TopAt(tile, T);
        }
        Box("rim", new Vector3((L + R) / 2, T - 0.05f, -1.47f), new Vector3(R - L, 0.1f, 0.06f), Mat(Color.black, 0.3f, null, Kit.Hex("#ff7a1a") * 1.6f), false);
        GlowSprite(new Vector3((L + R) / 2, B - 0.5f, 0), 9f, new Color(0.6f, 0.2f, 1f, 0.45f), 2);

        // floating plank platforms, lit by jack-o'-lanterns
        Softs(sd, Mat(Kit.Hex("#5a3424"), 0.1f), Mat(Color.black, 0.4f, null, Kit.Hex("#ff8a2a") * 1.8f), 0.3f, 1.7f);
        for (int k = 0; k < sd.soft.Length; k += 3)
        {
            float x0 = sd.soft[k] / 1000f, x1 = sd.soft[k + 1] / 1000f, y = sd.soft[k + 2] / 1000f;
            Prop("Spooky/jackolantern_small", 0.5f, new Vector3(x0 + 0.35f, y, 0.55f), 170);
            GlowSprite(new Vector3(x0 + 0.35f, y + 0.3f, 0.3f), 1.6f, new Color(1f, 0.55f, 0.15f, 0.6f), 1);
            GlowSprite(new Vector3((x0 + x1) / 2, y - 0.6f, 0), 2.6f, new Color(1f, 0.45f, 0.1f, 0.45f), 2);
        }

        // set dressing along the back of the hill (z > 0 keeps it behind the fighters)
        Prop("Spooky/shrine", 2.0f, new Vector3(0.6f, T, 1.25f), 180);
        Prop("Spooky/cauldron", 0.9f, new Vector3(L + 1.4f, T, 0.9f), 200);
        GlowSprite(new Vector3(L + 1.4f, T + 0.9f, 0.6f), 2.4f, new Color(0.4f, 1f, 0.3f, 0.6f), 1);
        Prop("Spooky/candyBucket", 0.55f, new Vector3(L + 2.4f, T, 1.0f), 170);
        Prop("Spooky/jackolantern_big", 0.85f, new Vector3(R - 1.6f, T, 0.95f), 190);
        GlowSprite(new Vector3(R - 1.6f, T + 0.45f, 0.5f), 2.4f, new Color(1f, 0.55f, 0.15f, 0.65f), 1);
        Prop("Spooky/pumpkinLarge", 0.6f, new Vector3(R - 2.6f, T, 1.15f), 30);
        Prop("Spooky/pumpkinSmall", 0.4f, new Vector3(-2.3f, T, 1.2f), 80);
        Prop("Spooky/candleBundle", 0.45f, new Vector3(1.6f, T, 1.15f), 0);
        GlowSprite(new Vector3(1.6f, T + 0.5f, 0.9f), 1.2f, new Color(1f, 0.8f, 0.4f, 0.6f), 1);
        Prop("Spooky/gravestone", 1.0f, new Vector3(-3.4f, T, 1.2f), 185);
        Prop("Spooky/gravestone", 0.85f, new Vector3(3.3f, T, 1.25f), 172);
        Prop("Spooky/coffinA_bottom", 0.55f, new Vector3(-4.6f, T, 1.15f), 95);
        Prop("Spooky/lampPost", 3.0f, new Vector3(L + 0.4f, T, 1.25f), 180);
        GlowSprite(new Vector3(L + 0.4f, T + 2.8f, 1f), 3f, new Color(1f, 0.75f, 0.4f, 0.6f), 1);
        Prop("Spooky/lampPost", 3.0f, new Vector3(R - 0.4f, T, 1.25f), 180);
        GlowSprite(new Vector3(R - 0.4f, T + 2.8f, 1f), 3f, new Color(1f, 0.75f, 0.4f, 0.6f), 1);

        // the hollow: crooked trees, stray graves and lanterns fading into fog
        var rng = new System.Random(23);
        string[] trees = { "Spooky/treeA_graveyard", "Spooky/treeB_graveyard", "Spooky/treeC_graveyard", "Spooky/treeD_graveyard" };
        for (int i = 0; i < 26; i++)
            Prop(trees[rng.Next(trees.Length)], 5f + (float)rng.NextDouble() * 6f, new Vector3(-52f + i * 4f, -6f, 16f + (float)rng.NextDouble() * 24f), rng.Next(360));
        for (int i = 0; i < 10; i++)
        {
            var at = new Vector3(-30f + i * 6.5f + (float)rng.NextDouble() * 2f, -5.5f, 12f + (float)rng.NextDouble() * 6f);
            Prop("Spooky/jackolantern_big", 1.4f, at, 160 + rng.Next(40));
            GlowSprite(at + new Vector3(0, 0.8f, -0.6f), 4f, new Color(1f, 0.5f, 0.12f, 0.5f), -4);
        }
        var ground = Kit.MeshObject("hollow-ground", Kit.BuildQuad(1f, 1f));
        ground.transform.SetParent(root, false); ground.transform.localPosition = new Vector3(0, -6f, 40f); ground.transform.localRotation = Quaternion.Euler(90, 0, 0);
        ground.transform.localScale = new Vector3(200f, 80f, 1f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = Mat(Kit.Hex("#140a18"), 0.02f);
    }

    // ---------------------------------------------------------------- MOON BASE (Space Diner)
    static void Moonbase(StageDef sd, float L, float R, float T, float B, Camera cam, Light sun)
    {
        Sky(Kit.Hex("#03040d"), Kit.Hex("#1b1f45"), cam);
        Stars(120, 66f, Color.white, 9);
        var planet = Kit.MeshObject("planet", Kit.SphereMesh);
        planet.transform.SetParent(root, false); planet.transform.localPosition = new Vector3(22f, 18f, 55f); planet.transform.localScale = Vector3.one * 26f;
        planet.GetComponent<MeshRenderer>().sharedMaterial = Mat(Kit.Hex("#ff8a5c"), 0.1f, Kit.Noise(128, Kit.Hex("#ff8a5c"), Kit.Hex("#c0503a"), 0.08f, 3, 6));
        GlowSprite(new Vector3(22f, 18f, 56f), 44f, new Color(1f, 0.5f, 0.3f, 0.25f));
        Light(sun, Kit.Hex("#fff4e8"), 1.05f, new Vector3(30, -50, 0), Kit.Hex("#6d7aa8"), Kit.Hex("#4a4f6e"), Kit.Hex("#22222e"), Kit.Hex("#141735"), 35f, 100f);
        var metal = Mat(Kit.Hex("#b9c2d0"), 0.45f);
        Box("deck", new Vector3((L + R) / 2, (T + B) / 2, 0), new Vector3(R - L, T - B, 3f), metal);
        Box("hazard", new Vector3((L + R) / 2, T - 0.08f, -1.52f), new Vector3(R - L, 0.16f, 0.06f), Mat(Color.white, 0.3f, Stripes(Kit.Hex("#ffc53d"), Kit.Hex("#222222"))), false);
        Box("under", new Vector3((L + R) / 2, B - 0.8f, 0), new Vector3((R - L) * 0.6f, 1.6f, 2f), Mat(Kit.Hex("#5a6072"), 0.3f));
        GlowSprite(new Vector3((L + R) / 2, B - 1.8f, 0), 6f, new Color(0.4f, 0.8f, 1f, 0.8f), 2);
        var pad = Mat(Kit.Hex("#8f98aa"), 0.5f);
        Softs(sd, pad, Mat(Color.black, 0.5f, null, Kit.Hex("#46b8ff") * 2f), 0.4f, 1.8f);
        for (int k = 0; k < sd.soft.Length; k += 3)
        {
            float x0 = sd.soft[k] / 1000f, x1 = sd.soft[k + 1] / 1000f, y = sd.soft[k + 2] / 1000f;
            GlowSprite(new Vector3((x0 + x1) / 2, y - 0.7f, 0), 2.8f, new Color(0.3f, 0.8f, 1f, 0.7f), 2);
        }
        Prop("Space/satelliteDish_large", 3.4f, new Vector3(L - 2.5f, T - 2.6f, 8f), 200);
        Prop("Space/hangar_roundA", 3.6f, new Vector3(R + 1.5f, T - 2.6f, 12f), 180);
        Prop("Space/rover", 0.9f, new Vector3(2.6f, T, 1.25f), 160);
        var rng = new System.Random(13);
        string[] far = { "Space/rock_largeA", "Space/rock_largeB", "Space/rock_crystalsLargeA", "Space/meteor_detailed", "Space/structure_detailed", "Space/rocket_baseA" };
        for (int i = 0; i < 12; i++)
            Prop(far[rng.Next(far.Length)], 3f + (float)rng.NextDouble() * 6f, new Vector3(-60f + i * 11f, -16f, 40f + (float)rng.NextDouble() * 18f), rng.Next(360));
        var ground = Kit.MeshObject("moon", Kit.BuildQuad(1f, 1f));
        ground.transform.SetParent(root, false); ground.transform.localPosition = new Vector3(0, -16f, 40f); ground.transform.localRotation = Quaternion.Euler(90, 0, 0);
        ground.transform.localScale = new Vector3(160f, 60f, 1);
        ground.GetComponent<MeshRenderer>().sharedMaterial = Mat(Kit.Hex("#8c8fa3"), 0.05f, Kit.Noise(128, Kit.Hex("#8c8fa3"), Kit.Hex("#5e6173"), 0.06f, 5, 12));
    }

    // ---------------------------------------------------------------- THE PASS (Order Up!)
    static void Kitchen(StageDef sd, float L, float R, float T, float B, Camera cam, Light sun)
    {
        Sky(Kit.Hex("#ffd9a8"), Kit.Hex("#ff9a6b"), cam);
        Light(sun, Kit.Hex("#fff1dc"), 0.9f, new Vector3(50, -25, 0), Kit.Hex("#b8b0c8"), Kit.Hex("#a08a7a"), Kit.Hex("#4a3a30"), Kit.Hex("#ffb987"), 30f, 90f);
        var tiles = Kit.Tiles(64, Kit.Hex("#fff3e0"), Kit.Hex("#ffe2c4"), Kit.Hex("#e8c49e"));
        tiles.wrapMode = TextureWrapMode.Repeat;
        var wallMat = Mat(Color.white, 0.2f, tiles); wallMat.mainTextureScale = new Vector2(20f, 8f);
        Box("wall", new Vector3(0, 6f, 9f), new Vector3(70f, 30f, 0.5f), wallMat, false);
        Box("counterTop", new Vector3((L + R) / 2, T - 0.2f, 0), new Vector3(R - L + 0.3f, 0.4f, 3.2f), Mat(Kit.Hex("#eef1f4"), 0.55f));
        var wood = Mat(Kit.Hex("#c77a45"), 0.2f);
        Box("cabinets", new Vector3((L + R) / 2, (T - 0.4f + B) / 2, 0.1f), new Vector3(R - L, T - 0.4f - B, 2.9f), wood);
        for (int i = 0; i < 8; i++)
        {
            float x = L + (i + 0.5f) * (R - L) / 8f;
            Box("door", new Vector3(x, (T - 0.4f + B) / 2, -1.36f), new Vector3((R - L) / 8f - 0.25f, (T - 0.4f - B) - 0.5f, 0.08f), Mat(Kit.Hex("#e0925a"), 0.25f), false);
            Box("knob", new Vector3(x + 0.6f, T - 1.2f, -1.42f), new Vector3(0.25f, 0.08f, 0.08f), Mat(Kit.Hex("#d9dde3"), 0.8f), false);
        }
        // the soft platform is a giant plate
        for (int k = 0; k < sd.soft.Length; k += 3)
        {
            float x0 = sd.soft[k] / 1000f, x1 = sd.soft[k + 1] / 1000f, y = sd.soft[k + 2] / 1000f;
            var plate = Prop("Food/plate", x1 - x0 + 0.6f, new Vector3((x0 + x1) / 2, y - 0.25f, 0), 0, true);
            Box("stand", new Vector3((x0 + x1) / 2, (y + T) / 2, 0.3f), new Vector3(0.3f, y - T, 0.3f), Mat(Kit.Hex("#d9dde3"), 0.8f));
        }
        Prop("Food/burger-cheese", 1.4f, new Vector3(L + 1.2f, T, 1.35f), 30);
        Prop("Food/soda-can", 1.2f, new Vector3(R - 0.9f, T, 1.35f), 0);
        Prop("Food/cake-birthday", 1.0f, new Vector3(R - 2.4f, T, 1.4f), 0);
        Prop("Furniture/kitchenFridgeLarge", 14f, new Vector3(-14f, -8f, 7f), 180);
        Prop("Furniture/kitchenStove", 9f, new Vector3(13f, -8f, 7f), 180);
        Prop("Furniture/hoodModern", 5f, new Vector3(13f, 5f, 8f), 180);
        GlowSprite(new Vector3(0, 12f, 8.5f), 30f, new Color(1f, 0.95f, 0.8f, 0.35f));
    }
}
