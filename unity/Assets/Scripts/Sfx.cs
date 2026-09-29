using UnityEngine;

// All audio is synthesized at boot: zero audio files.
public class Sfx : MonoBehaviour
{
    public static Sfx I;
    const int SR = 22050;
    const float TAU = Mathf.PI * 2f;
    AudioSource[] voices; int next;
    AudioSource music;
    AudioClip hitL, hitM, hitH, bonk, slash, ko, crowd, jump, djump, land, swish, swishHeavy, dodge, pickup, throwC, boom, pew, blast, spawn, drop, go, beep, click, fanfare, lose, game;
    public bool Muted { get; private set; }
    System.Random rnd = new System.Random(5);
    float N() => (float)(rnd.NextDouble() * 2 - 1);
    float lastHit, lastLand;

    void Awake()
    {
        I = this;
        voices = new AudioSource[14];
        for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; }
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true; music.volume = 0.22f; music.playOnAwake = false;
        Build();
        music.clip = Music();
    }

    public void SetMuted(bool m) { Muted = m; AudioListener.volume = m ? 0 : 1; }
    public void Unlock() { }
    public void Music(bool on) { if (on) { if (!music.isPlaying) music.Play(); } else music.Stop(); }

    void Play(AudioClip c, float vol, float pitch = 1f)
    {
        var s = voices[next]; next = (next + 1) % voices.Length;
        s.pitch = pitch; s.PlayOneShot(c, vol);
    }

    public void Hit(int kb, bool heavy)
    {
        if (Time.unscaledTime - lastHit < 0.03f) return; lastHit = Time.unscaledTime;
        var c = kb > 260 || heavy ? hitH : kb > 120 ? hitM : hitL;
        Play(c, Mathf.Clamp(0.35f + kb / 600f, 0.35f, 0.9f), Random.Range(0.92f, 1.08f));
        if (kb > 330) Play(crowd, 0.35f);
    }
    public void Bonk(int kb) { Play(bonk, 0.8f, Random.Range(0.95f, 1.1f)); if (kb > 250) Play(hitH, 0.5f); if (kb > 330) Play(crowd, 0.35f); }
    public void Slash(int kb) { Play(slash, 0.55f, Random.Range(0.95f, 1.1f)); Play(kb > 200 ? hitM : hitL, 0.4f); }
    public void KO() { Play(ko, 0.9f); Play(crowd, 0.55f, Random.Range(0.95f, 1.05f)); }
    public void Jump(bool dbl) => Play(dbl ? djump : jump, 0.28f, Random.Range(0.95f, 1.08f));
    public void Land() { if (Time.unscaledTime - lastLand < 0.08f) return; lastLand = Time.unscaledTime; Play(land, 0.25f, Random.Range(0.9f, 1.1f)); }
    public void Swing(int weapon, bool heavy) => Play(heavy ? swishHeavy : swish, heavy ? 0.4f : 0.22f, weapon == Weapon.Pan ? 0.8f : weapon == Weapon.Spear ? 0.9f : Random.Range(1f, 1.15f));
    public void Dodge() => Play(dodge, 0.3f);
    public void Pickup() => Play(pickup, 0.5f);
    public void Throw() => Play(throwC, 0.45f);
    public void Boom() => Play(boom, 0.85f);
    public void Pew(bool big) => Play(big ? blast : pew, big ? 0.6f : 0.35f, Random.Range(0.95f, 1.05f));
    public void Spawn() => Play(spawn, 0.45f);
    public void ItemDrop() => Play(drop, 0.4f);
    public void Go() => Play(go, 0.7f);
    public void Beep() => Play(beep, 0.5f);
    public void Click() => Play(click, 0.4f);
    public void GameSet() => Play(game, 0.8f);
    public void Fanfare(bool win) => Play(win ? fanfare : lose, 0.7f);

    static AudioClip Clip(string n, float[] d) { var c = AudioClip.Create(n, d.Length, 1, SR, false); c.SetData(d, 0); return c; }
    delegate float Gen(float t, float dt);
    static float[] R(float dur, Gen g)
    {
        int n = (int)(SR * dur); var d = new float[n]; float dt = 1f / SR;
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(g(i * dt, dt) * Mathf.Clamp01((n - i) / (SR * 0.01f)), -1, 1);
        return d;
    }
    float[] Arp(float[] notes, float step, float tail, float vol)
    {
        float ph = 0;
        return R(step * notes.Length + tail, (t, dt) =>
        {
            int k = Mathf.Min((int)(t / step), notes.Length - 1);
            ph += TAU * notes[k] * dt;
            float lt = t - k * step;
            float saw = 2f * (ph / TAU % 1f) - 1f;
            return (Mathf.Sin(ph) * 0.7f + saw * 0.3f) * Mathf.Exp(-lt * (k < notes.Length - 1 ? 9 : 2.5f)) * vol;
        });
    }

    // a punchy impact: noise crack + pitched thump
    float[] Impact(float dur, float f0, float f1, float noise, float decay)
    {
        float ph = 0, lp = 0;
        return R(dur, (t, dt) =>
        {
            lp += (N() - lp) * Mathf.Lerp(0.9f, 0.1f, t / dur);
            ph += TAU * Mathf.Lerp(f0, f1, Mathf.Sqrt(t / dur)) * dt;
            float body = Mathf.Sin(ph) + 0.4f * Mathf.Sin(ph * 2.01f);
            return (body * 0.7f + lp * noise) * Mathf.Exp(-t * decay) * (t < 0.004f ? t / 0.004f : 1f);
        });
    }

    void Build()
    {
        hitL = Clip("hitL", Impact(0.12f, 520, 180, 0.8f, 30));
        hitM = Clip("hitM", Impact(0.2f, 360, 90, 1.0f, 18));
        hitH = Clip("hitH", Impact(0.45f, 220, 45, 1.2f, 8));
        float ph = 0;
        bonk = Clip("bonk", R(0.7f, (t, dt) =>
        {
            // cartoon frying-pan ring: inharmonic metal partials
            float env = Mathf.Exp(-t * 5f);
            return (Mathf.Sin(TAU * 523f * t) * 0.5f + Mathf.Sin(TAU * 1247f * t) * 0.3f + Mathf.Sin(TAU * 2213f * t) * 0.18f + Mathf.Sin(TAU * 3301f * t) * 0.1f) * env
                + Mathf.Sin(TAU * Mathf.Lerp(180, 70, t * 4f) * t) * Mathf.Exp(-t * 20f) * 0.6f;
        }));
        float lp = 0;
        slash = Clip("slash", R(0.25f, (t, dt) => { lp += (N() - lp) * Mathf.Lerp(0.2f, 0.9f, t / 0.25f); return lp * Mathf.Sin(t / 0.25f * Mathf.PI) * 0.8f + Mathf.Sin(TAU * 2600 * t) * Mathf.Exp(-t * 20) * 0.25f; }));
        lp = 0; ph = 0;
        ko = Clip("ko", R(1.6f, (t, dt) => { lp += (N() - lp) * Mathf.Lerp(0.8f, 0.03f, t / 1.6f); ph += TAU * (40 + 120 * Mathf.Exp(-t * 6)) * dt; return (lp * 1.2f + Mathf.Sin(ph) * 0.9f) * Mathf.Exp(-t * 2.2f); }));
        // crowd: band-passed noise swell with a few "voices"
        lp = 0; float hp = 0;
        crowd = Clip("crowd", R(1.8f, (t, dt) =>
        {
            float n = N(); lp += (n - lp) * 0.25f; float band = lp - hp; hp += (lp - hp) * 0.02f;
            float env = Mathf.Sin(Mathf.Clamp01(t / 1.8f) * Mathf.PI) * (0.8f + 0.2f * Mathf.Sin(t * 13f));
            return band * env * 1.6f;
        }));
        ph = 0;
        jump = Clip("jump", R(0.16f, (t, dt) => { ph += TAU * Mathf.Lerp(300, 700, t / 0.16f) * dt; return Mathf.Sin(ph) * Mathf.Exp(-t * 14) * 0.6f; }));
        ph = 0;
        djump = Clip("djump", R(0.2f, (t, dt) => { ph += TAU * Mathf.Lerp(500, 1100, t / 0.2f) * dt; return (Mathf.Sin(ph) * 0.5f + N() * 0.15f) * Mathf.Exp(-t * 12); }));
        land = Clip("land", Impact(0.1f, 160, 60, 0.6f, 35));
        lp = 0;
        swish = Clip("swish", R(0.16f, (t, dt) => { lp += (N() - lp) * Mathf.Lerp(0.1f, 0.6f, t / 0.16f); return lp * Mathf.Sin(t / 0.16f * Mathf.PI) * 1.1f; }));
        lp = 0;
        swishHeavy = Clip("swishH", R(0.32f, (t, dt) => { lp += (N() - lp) * Mathf.Lerp(0.05f, 0.4f, t / 0.32f); return lp * Mathf.Sin(t / 0.32f * Mathf.PI) * 1.4f; }));
        lp = 0;
        dodge = Clip("dodge", R(0.22f, (t, dt) => { lp += (N() - lp) * 0.5f; return lp * Mathf.Sin(t / 0.22f * Mathf.PI) * 0.7f + Mathf.Sin(TAU * Mathf.Lerp(900, 1500, t / 0.22f) * t) * 0.12f * Mathf.Exp(-t * 8); }));
        pickup = Clip("pickup", Arp(new[] { 659.25f, 987.77f, 1318.5f }, 0.05f, 0.3f, 0.45f));
        lp = 0;
        throwC = Clip("throw", R(0.25f, (t, dt) => { lp += (N() - lp) * 0.3f; return lp * Mathf.Exp(-t * 9) + Mathf.Sin(TAU * Mathf.Lerp(700, 250, t * 4f) * t) * 0.2f * Mathf.Exp(-t * 10); }));
        lp = 0; ph = 0;
        boom = Clip("boom", R(1.1f, (t, dt) => { lp += (N() - lp) * Mathf.Lerp(0.95f, 0.02f, t / 1.1f); ph += TAU * (38 + 90 * Mathf.Exp(-t * 10)) * dt; return (lp * 1.3f + Mathf.Sin(ph) * 0.8f) * Mathf.Exp(-t * 3.2f); }));
        ph = 0;
        pew = Clip("pew", R(0.14f, (t, dt) => { ph += TAU * Mathf.Lerp(1800, 400, t / 0.14f) * dt; return (Mathf.Sign(Mathf.Sin(ph)) * 0.25f + Mathf.Sin(ph) * 0.35f) * Mathf.Exp(-t * 20); }));
        ph = 0; lp = 0;
        blast = Clip("blast", R(0.5f, (t, dt) => { ph += TAU * Mathf.Lerp(900, 120, t / 0.5f) * dt; lp += (N() - lp) * 0.4f; return (Mathf.Sin(ph) * 0.6f + lp * 0.5f) * Mathf.Exp(-t * 5); }));
        spawn = Clip("spawn", Arp(new[] { 523.25f, 783.99f, 1046.5f, 1567.98f }, 0.06f, 0.5f, 0.35f));
        ph = 0;
        drop = Clip("drop", R(0.6f, (t, dt) => { ph += TAU * Mathf.Lerp(1400, 300, t / 0.6f) * dt; return Mathf.Sin(ph) * 0.3f * Mathf.Exp(-t * 3); }));
        go = Clip("go", Arp(new[] { 783.99f, 1046.5f, 1567.98f }, 0.07f, 0.6f, 0.5f));
        beep = Clip("beep", R(0.18f, (t, dt) => (Mathf.Sin(TAU * 880 * t) * 0.5f + (Mathf.Sin(TAU * 880 * t) > 0 ? 0.12f : -0.12f)) * Mathf.Min(1, (0.18f - t) * 25)));
        ph = 0;
        click = Clip("click", R(0.04f, (t, dt) => { ph += TAU * 1300 * dt; return Mathf.Sin(ph) * Mathf.Exp(-t * 90) * 0.6f; }));
        fanfare = Clip("fanfare", Arp(new[] { 523.25f, 523.25f, 659.25f, 783.99f, 659.25f, 783.99f, 1046.5f, 1318.5f }, 0.1f, 1.4f, 0.4f));
        lose = Clip("lose", Arp(new[] { 392f, 369.99f, 349.23f, 293.66f }, 0.2f, 1f, 0.4f));
        lp = 0;
        game = Clip("game", R(1.2f, (t, dt) => { lp += (N() - lp) * 0.2f; return (Mathf.Sin(TAU * 98 * t) * 0.6f + Mathf.Sin(TAU * 146.8f * t) * 0.4f + lp * 0.5f) * Mathf.Exp(-t * 2.5f); }));
    }

    // 150 bpm fight track in E minor: driving 8th bass, big snare, stabs, lead riff every other phrase.
    AudioClip Music()
    {
        float bpm = 150f, beat = 60f / bpm;
        int bars = 8; float dur = beat * 4 * bars;
        int n = (int)(SR * dur); var d = new float[n];
        float[] roots = { 82.41f, 65.41f, 73.42f, 61.74f };   // E C D B
        float[][] chords = { new[] { 329.63f, 392f, 493.88f }, new[] { 261.63f, 329.63f, 392f }, new[] { 293.66f, 369.99f, 440f }, new[] { 246.94f, 311.13f, 369.99f } };
        float[] riff = { 659.25f, 0, 587.33f, 659.25f, 783.99f, 659.25f, 587.33f, 493.88f, 523.25f, 0, 493.88f, 440f, 493.88f, 587.33f, 659.25f, 0 };
        float hp = 0, blp = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR, bt = t / beat;
            int bi = (int)bt, barIdx = bi / 4, bar = barIdx % 4;
            float ib = (bt - bi) * beat;
            float nz = N();
            float kick = (bi % 2 == 0 || (bi % 4 == 3 && ib > beat * 0.5f)) ? Mathf.Sin(TAU * (48 + 110 * Mathf.Exp(-ib * 38)) * ib) * Mathf.Exp(-ib * 9) * 0.6f : 0;
            float snare = (bi % 2 == 1) ? (nz * 0.55f + Mathf.Sin(TAU * 185 * ib) * 0.35f) * Mathf.Exp(-ib * 14) * 0.32f : 0;
            float e8 = bt * 2; int e8i = (int)e8; float i8 = (e8 - e8i) * beat / 2;
            float e16 = bt * 4; int e16i = (int)e16; float i16 = (e16 - e16i) * beat / 4;
            float hat = (nz - hp) * Mathf.Exp(-i16 * 60) * (e16i % 2 == 1 ? 0.07f : 0.035f); hp = nz;
            float bf = roots[bar] * (e8i % 4 == 3 ? 2f : 1f);
            float saw = 2f * ((bf * t) % 1f) - 1f;
            blp += (saw - blp) * 0.18f;
            float bass = blp * Mathf.Exp(-i8 * 5) * 0.36f;
            float stab = 0;
            if (e8i % 8 == 0 || e8i % 8 == 3 || e8i % 8 == 6) foreach (var f in chords[bar]) { float s = 2f * ((f * t) % 1f) - 1f; stab += s; }
            stab *= 0.035f * Mathf.Exp(-i8 * 10);
            float lead = 0;
            if (barIdx >= 4)
            {
                float lf = riff[e8i % 16];
                if (lf > 0) { float sq = Mathf.Sin(TAU * lf * t) > 0 ? 1f : -1f; lead = (sq * 0.4f + Mathf.Sin(TAU * lf * t) * 0.6f) * Mathf.Exp(-i8 * 4) * 0.07f; }
            }
            float duck = 1f - 0.45f * Mathf.Exp(-ib * 11);
            d[i] = Mathf.Clamp((kick + snare + hat + (bass + stab + lead) * duck) * 0.8f, -1, 1);
        }
        return Clip("music", d);
    }
}
