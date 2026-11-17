using System;
using System.Collections.Generic;

namespace Spheres
{
    /// <summary>Where a track plays.</summary>
    public enum MusicMood { Menu, Battle, Pick, Victory, Editor }

    /// <summary>
    /// A procedurally composed song: key, scale, chord progression, groove and instruments. The composer turns it
    /// into a seamless loop with an arrangement (intro, verse, chorus with lead, breakdown, final chorus).
    /// </summary>
    public class SongDef
    {
        public string Name;
        public MusicMood Mood;
        public float Bpm = 120f;
        /// <summary>MIDI note of the tonic in the bass-ish register (e.g. 40 = E2).</summary>
        public int Root = 40;
        public int[] Scale = Scales.Minor;
        /// <summary>Chord roots as scale degrees (0 = tonic).</summary>
        public int[] Progression = { 0, 5, 2, 6 };
        public int BarsPerChord = 2;
        public bool Sevenths;
        /// <summary>four, break, half, dnb, trap, chip, shuffle, soft, none</summary>
        public string Drums = "four";
        /// <summary>eighths, offbeat, sustain, arp, acid, walking, reese, none</summary>
        public string Bass = "eighths";
        /// <summary>up, updown, random, stabs, pluck, none</summary>
        public string Arp = "up";
        /// <summary>supersaw, square, chip, bell, tri, organ, pluck, none</summary>
        public string Lead = "supersaw";
        /// <summary>saw, organ, soft, glass, none</summary>
        public string Pad = "saw";
        public float Swing;
        public int Bars = 32;
        public int Seed = 1;
    }

    public static class Scales
    {
        public static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };
        public static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };
        public static readonly int[] Phrygian = { 0, 1, 3, 5, 7, 8, 10 };
        public static readonly int[] HarmonicMinor = { 0, 2, 3, 5, 7, 8, 11 };
        public static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };
        public static readonly int[] Mixolydian = { 0, 2, 4, 5, 7, 9, 10 };
        public static readonly int[] Lydian = { 0, 2, 4, 6, 7, 9, 11 };
    }

    /// <summary>The built-in soundtrack (all original, generated from these descriptions).</summary>
    public static class Soundtrack
    {
        public static readonly List<SongDef> Songs = new List<SongDef>
        {
            // ---- Battle
            new SongDef { Name = "Round Robin", Mood = MusicMood.Battle, Bpm = 124, Root = 40, Scale = Scales.Minor, Progression = new[] { 0, 5, 2, 6 }, Drums = "four", Bass = "eighths", Arp = "up", Lead = "supersaw", Pad = "saw", Seed = 7 },
            new SongDef { Name = "Bullet Hell Yeah", Mood = MusicMood.Battle, Bpm = 150, Root = 45, Scale = Scales.Minor, Progression = new[] { 0, 6, 5, 4 }, Drums = "chip", Bass = "arp", Arp = "updown", Lead = "chip", Pad = "none", Seed = 21 },
            new SongDef { Name = "Block Party", Mood = MusicMood.Battle, Bpm = 126, Root = 42, Scale = Scales.Dorian, Progression = new[] { 0, 3, 0, 4 }, Sevenths = true, Drums = "four", Bass = "offbeat", Arp = "stabs", Lead = "organ", Pad = "organ", Seed = 33 },
            new SongDef { Name = "Spin Cycle", Mood = MusicMood.Battle, Bpm = 172, Root = 38, Scale = Scales.Minor, Progression = new[] { 0, 0, 5, 6 }, Drums = "dnb", Bass = "reese", Arp = "random", Lead = "bell", Pad = "glass", Seed = 44 },
            new SongDef { Name = "Card Shark", Mood = MusicMood.Battle, Bpm = 140, Root = 37, Scale = Scales.Phrygian, Progression = new[] { 0, 1, 0, 6 }, Drums = "trap", Bass = "acid", Arp = "pluck", Lead = "square", Pad = "soft", Seed = 55 },
            new SongDef { Name = "Overclocked", Mood = MusicMood.Battle, Bpm = 148, Root = 47, Scale = Scales.HarmonicMinor, Progression = new[] { 0, 5, 3, 4 }, Drums = "break", Bass = "acid", Arp = "up", Lead = "supersaw", Pad = "saw", Seed = 66 },
            new SongDef { Name = "Hot Potato", Mood = MusicMood.Battle, Bpm = 134, Root = 43, Scale = Scales.Mixolydian, Progression = new[] { 0, 6, 3, 0 }, Drums = "four", Bass = "arp", Arp = "updown", Lead = "chip", Pad = "soft", Seed = 77 },
            new SongDef { Name = "Last Sphere Standing", Mood = MusicMood.Battle, Bpm = 118, Root = 45, Scale = Scales.Dorian, Progression = new[] { 0, 3, 6, 4 }, Sevenths = true, Drums = "shuffle", Swing = 0.3f, Bass = "walking", Arp = "pluck", Lead = "organ", Pad = "organ", Seed = 88 },
            new SongDef { Name = "Gravity Well", Mood = MusicMood.Battle, Bpm = 100, Root = 41, Scale = Scales.Minor, Progression = new[] { 0, 5, 6, 4 }, Drums = "half", Bass = "reese", Arp = "random", Lead = "bell", Pad = "saw", Seed = 99 },
            new SongDef { Name = "Ricochet", Mood = MusicMood.Battle, Bpm = 136, Root = 40, Scale = Scales.Phrygian, Progression = new[] { 0, 1, 6, 1 }, Drums = "break", Bass = "offbeat", Arp = "up", Lead = "square", Pad = "glass", Seed = 111 },
            new SongDef { Name = "Neon Arena", Mood = MusicMood.Battle, Bpm = 128, Root = 36, Scale = Scales.Minor, Progression = new[] { 0, 3, 5, 6 }, Drums = "four", Bass = "eighths", Arp = "updown", Lead = "supersaw", Pad = "saw", Seed = 122 },
            new SongDef { Name = "Pop Goes the Sphere", Mood = MusicMood.Battle, Bpm = 160, Root = 38, Scale = Scales.Major, Progression = new[] { 0, 4, 5, 3 }, Drums = "chip", Bass = "eighths", Arp = "up", Lead = "chip", Pad = "none", Seed = 133 },
            new SongDef { Name = "Bounce House", Mood = MusicMood.Battle, Bpm = 122, Root = 44, Scale = Scales.Minor, Progression = new[] { 0, 6, 5, 6 }, Sevenths = true, Drums = "four", Bass = "offbeat", Arp = "stabs", Lead = "pluck", Pad = "glass", Seed = 144 },
            new SongDef { Name = "Border Patrol", Mood = MusicMood.Battle, Bpm = 145, Root = 39, Scale = Scales.HarmonicMinor, Progression = new[] { 0, 0, 3, 4 }, Drums = "trap", Bass = "sustain", Arp = "random", Lead = "square", Pad = "saw", Seed = 155 },
            new SongDef { Name = "Final Round", Mood = MusicMood.Battle, Bpm = 176, Root = 41, Scale = Scales.Minor, Progression = new[] { 0, 5, 3, 4 }, Drums = "dnb", Bass = "eighths", Arp = "up", Lead = "supersaw", Pad = "saw", Seed = 166 },

            // ---- Menus
            new SongDef { Name = "Lobby Lights", Mood = MusicMood.Menu, Bpm = 90, Root = 45, Scale = Scales.Minor, Progression = new[] { 0, 5, 2, 6 }, Sevenths = true, Drums = "soft", Bass = "sustain", Arp = "pluck", Lead = "bell", Pad = "saw", Seed = 11 },
            new SongDef { Name = "Floating Islands", Mood = MusicMood.Menu, Bpm = 78, Root = 38, Scale = Scales.Lydian, Progression = new[] { 0, 1, 0, 4 }, Sevenths = true, Drums = "none", Bass = "sustain", Arp = "pluck", Lead = "bell", Pad = "glass", Seed = 12 },
            new SongDef { Name = "Shuffle Up", Mood = MusicMood.Menu, Bpm = 96, Root = 41, Scale = Scales.Major, Progression = new[] { 0, 5, 1, 4 }, Sevenths = true, Drums = "shuffle", Swing = 0.33f, Bass = "walking", Arp = "pluck", Lead = "organ", Pad = "organ", Seed = 13 },
            new SongDef { Name = "Main Menu Moon", Mood = MusicMood.Menu, Bpm = 84, Root = 40, Scale = Scales.Dorian, Progression = new[] { 0, 3, 6, 3 }, Sevenths = true, Drums = "soft", Swing = 0.2f, Bass = "sustain", Arp = "random", Lead = "tri", Pad = "soft", Seed = 14 },

            // ---- Card picks
            new SongDef { Name = "Choose Wisely", Mood = MusicMood.Pick, Bpm = 100, Root = 47, Scale = Scales.Minor, Progression = new[] { 0, 5, 3, 4 }, Drums = "half", Bass = "sustain", Arp = "pluck", Lead = "bell", Pad = "glass", Seed = 21 },
            new SongDef { Name = "Deck Builder", Mood = MusicMood.Pick, Bpm = 104, Root = 43, Scale = Scales.Dorian, Progression = new[] { 0, 3, 0, 4 }, Sevenths = true, Drums = "soft", Swing = 0.28f, Bass = "walking", Arp = "pluck", Lead = "tri", Pad = "soft", Seed = 22 },
            new SongDef { Name = "Draw Phase", Mood = MusicMood.Pick, Bpm = 92, Root = 37, Scale = Scales.Minor, Progression = new[] { 0, 6, 5, 6 }, Sevenths = true, Drums = "soft", Bass = "sustain", Arp = "random", Lead = "bell", Pad = "saw", Seed = 23 },
            new SongDef { Name = "Legendary Pull", Mood = MusicMood.Pick, Bpm = 110, Root = 42, Scale = Scales.Lydian, Progression = new[] { 0, 1, 5, 4 }, Sevenths = true, Drums = "half", Bass = "arp", Arp = "updown", Lead = "bell", Pad = "glass", Seed = 24 },

            // ---- Map editor
            new SongDef { Name = "Blueprint", Mood = MusicMood.Editor, Bpm = 84, Root = 39, Scale = Scales.Major, Progression = new[] { 0, 3, 5, 4 }, Sevenths = true, Drums = "soft", Swing = 0.25f, Bass = "sustain", Arp = "pluck", Lead = "tri", Pad = "soft", Seed = 31 },
            new SongDef { Name = "Sandbox", Mood = MusicMood.Editor, Bpm = 96, Root = 36, Scale = Scales.Major, Progression = new[] { 0, 4, 5, 3 }, Drums = "soft", Bass = "arp", Arp = "pluck", Lead = "chip", Pad = "none", Seed = 32 },
            new SongDef { Name = "Voxel Garden", Mood = MusicMood.Editor, Bpm = 72, Root = 41, Scale = Scales.Lydian, Progression = new[] { 0, 1, 4, 0 }, Sevenths = true, Drums = "none", Bass = "sustain", Arp = "random", Lead = "bell", Pad = "glass", Seed = 33 },

            // ---- Match end
            new SongDef { Name = "Podium", Mood = MusicMood.Victory, Bpm = 120, Root = 46, Scale = Scales.Major, Progression = new[] { 0, 4, 5, 3 }, Drums = "four", Bass = "eighths", Arp = "stabs", Lead = "supersaw", Pad = "saw", Bars = 16, BarsPerChord = 1, Seed = 41 },
            new SongDef { Name = "Champions of the Round", Mood = MusicMood.Victory, Bpm = 132, Root = 38, Scale = Scales.Mixolydian, Progression = new[] { 0, 6, 3, 4 }, Drums = "four", Bass = "arp", Arp = "updown", Lead = "chip", Pad = "organ", Bars = 16, BarsPerChord = 1, Seed = 42 },
        };
    }

    /// <summary>Offline sequencer + synth. Pure C# (no engine calls) so it runs on a worker thread.</summary>
    public static class Composer
    {
        public const int Rate = 22050;
        const double Tau = Math.PI * 2.0;

        static float Mtof(float midi) => 440f * (float)Math.Pow(2.0, (midi - 69f) / 12.0);
        static float Frac(float x) => x - (float)Math.Floor(x);
        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

        class Track
        {
            public readonly float[] Buf;
            public Track(int n) { Buf = new float[n]; }

            /// <summary>Add a note (wraps past the end, so loops are seamless).</summary>
            public void Note(double start, double length, float freq, float vol, Func<float, float, float> osc, float attack, float release)
            {
                int s0 = (int)Math.Round(start * Rate);
                int n = (int)Math.Round((length + release) * Rate);
                float phase = 0f, inc = freq / Rate;
                int len = Buf.Length;
                float lenS = (float)length;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float env = t < attack ? t / attack : t < lenS ? 1f : Math.Max(0f, 1f - (t - lenS) / release);
                    phase += inc;
                    if (phase > 1024f) phase -= 1024f;
                    Buf[((s0 + i) % len + len) % len] += osc(phase, t) * env * vol;
                }
            }
        }

        // ---------------------------------------------------------------- oscillators

        static float Saw(float p) => Frac(p) * 2f - 1f;
        static float Sq(float p) => Frac(p) < 0.5f ? 1f : -1f;
        static float Pulse(float p, float w) => Frac(p) < w ? 1f : -1f;
        static float Tri(float p) => 1f - 4f * Math.Abs(Frac(p) - 0.5f);
        static float Sin(float p) => (float)Math.Sin(p * Tau);

        static Func<float, float, float> Instrument(string name)
        {
            switch (name)
            {
                case "supersaw": return (p, t) => (Saw(p) + Saw(p * 1.0071f) + Saw(p * 0.9932f)) * 0.45f;
                case "square": return (p, t) => Pulse(p, 0.5f - 0.2f * (float)Math.Exp(-t * 3f)) * 0.7f;
                case "chip": return (p, t) => Pulse(p, 0.125f) * 0.6f;
                case "bell": return (p, t) => (float)Math.Sin(p * Tau + 1.6 * Math.Exp(-t * 3.0) * Math.Sin(p * Tau * 3.5)) * (float)Math.Exp(-t * 1.6);
                case "tri": return (p, t) => Tri(p + 0.004f * (float)Math.Sin(t * 33.0) * Math.Min(1f, t * 4f));
                case "organ": return (p, t) => (Sin(p) + 0.5f * Sin(p * 2f) + 0.25f * Sin(p * 3f) + 0.12f * Sin(p * 4f)) * 0.55f;
                case "pluck": return (p, t) => (Tri(p) * 0.6f + Saw(p) * 0.4f) * (float)Math.Exp(-t * 7f);
                default: return (p, t) => Saw(p);
            }
        }

        // ---------------------------------------------------------------- drums

        static void Kick(Track t, double at, float vol, float punch = 1f)
        {
            int s0 = (int)Math.Round(at * Rate), n = (int)(0.32f * Rate);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                phase += (42f + 120f * punch * (float)Math.Exp(-k * 18f)) / Rate;
                t.Buf[(s0 + i) % t.Buf.Length] += Sin(phase) * (1f - k) * (1f - k) * vol;
            }
        }

        static void Noise(Track t, double at, float dur, float vol, float tone, Random rng, float lowpass, float decayPow = 3f)
        {
            int s0 = (int)Math.Round(at * Rate), n = (int)(dur * Rate);
            float y = 0f, a = Clamp01(lowpass), ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                y += a * (x - y);
                ph += tone / Rate;
                float s = (lowpass >= 1f ? x : y) + (tone > 0f ? Sin(ph) * 0.6f * (1f - k) : 0f);
                t.Buf[(s0 + i) % t.Buf.Length] += s * (float)Math.Pow(1f - k, decayPow) * vol;
            }
        }

        static void HighPass(float[] d, float a)
        {
            float prevX = 0f, y = 0f;
            for (int i = 0; i < d.Length; i++) { y = a * (y + d[i] - prevX); prevX = d[i]; d[i] = y; }
        }

        static void LowPass(float[] d, float a)
        {
            float y = d[d.Length - 1];
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < d.Length; i++) { y += a * (d[i] - y); if (pass == 1) d[i] = y; }
        }

        static void Echo(float[] d, double seconds, float feedback)
        {
            int dl = Math.Max(1, (int)Math.Round(seconds * Rate));
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < d.Length; i++) d[i] += d[(i - dl + d.Length * 4) % d.Length] * feedback;
        }

        /// <summary>Sidechain pumping: duck a track right after each kick.</summary>
        static void Duck(float[] d, List<double> kicks, float depth, double release)
        {
            if (kicks.Count == 0) return;
            var gain = new float[d.Length];
            for (int i = 0; i < gain.Length; i++) gain[i] = 1f;
            int rel = (int)(release * Rate);
            foreach (var k in kicks)
            {
                int s0 = (int)Math.Round(k * Rate);
                for (int i = 0; i < rel; i++)
                {
                    int idx = (s0 + i) % d.Length;
                    float g = 1f - depth * (1f - i / (float)rel);
                    if (g < gain[idx]) gain[idx] = g;
                }
            }
            for (int i = 0; i < d.Length; i++) d[i] *= gain[i];
        }

        // ---------------------------------------------------------------- patterns

        static readonly Dictionary<string, (string k, string s, string h)> Grooves = new Dictionary<string, (string, string, string)>
        {
            { "four", ("x...x...x...x...", "....x.......x...", "..x...x...x...x.") },
            { "break", ("x.........x.x...", "....x..x.x..x...", "x.x.x.x.x.x.x.x.") },
            { "half", ("x.....x.........", "........x.......", "x.x.x.x.x.x.x.x.") },
            { "dnb", ("x.........x.....", "....x.......x...", "x.x.x.x.x.x.x.x.") },
            { "trap", ("x......x..x.....", "........x.......", "xxxxxxxxxxxxxxxx") },
            { "chip", ("x...x...x...x...", "....x.......x...", "x.x.x.x.x.x.x.x.") },
            { "shuffle", ("x.....x.x.......", "....x.......x...", "x.x.x.x.x.x.x.x.") },
            { "soft", ("x.......x.......", "............x...", "x...x...x...x...") },
        };

        static bool Hit(string pattern, int step) => pattern != null && step < pattern.Length && pattern[step] == 'x';

        // ---------------------------------------------------------------- rendering

        public static float[] Render(SongDef s)
        {
            var rng = new Random(s.Seed * 7919 + 17);
            double beat = 60.0 / Math.Max(40f, s.Bpm), bar = beat * 4.0, step = beat / 4.0;
            int bars = Math.Max(4, s.Bars);
            int n = (int)Math.Round(bar * bars * Rate);
            var drums = new Track(n); var bass = new Track(n); var arp = new Track(n);
            var pad = new Track(n); var lead = new Track(n); var fx = new Track(n);
            var kicks = new List<double>();
            bool calm = s.Mood == MusicMood.Menu || s.Mood == MusicMood.Editor || s.Mood == MusicMood.Pick;
            bool fanfare = s.Mood == MusicMood.Victory;

            int Deg(int degree)
            {
                int len = s.Scale.Length;
                int oct = (int)Math.Floor(degree / (double)len);
                int idx = ((degree % len) + len) % len;
                return s.Root + 12 * oct + s.Scale[idx];
            }
            int[] Chord(int deg) => s.Sevenths ? new[] { Deg(deg), Deg(deg + 2), Deg(deg + 4), Deg(deg + 6) } : new[] { Deg(deg), Deg(deg + 2), Deg(deg + 4) };
            double Swung(int st) => st % 2 == 1 ? st * step + s.Swing * step : st * step;

            // Lead motif: two bars of rhythm + scale-degree offsets from the chord root, varied per section.
            var motif = BuildMotif(rng, calm);
            var motifB = BuildMotif(new Random(s.Seed * 31 + 5), calm, motif);
            var leadOsc = Instrument(s.Lead);
            var arpOsc = s.Arp == "stabs" ? Instrument(s.Pad == "organ" ? "organ" : "supersaw") : Instrument(s.Lead == "chip" ? "chip" : s.Lead == "bell" ? "bell" : "pluck");
            Func<float, float, float> padOsc;
            switch (s.Pad)
            {
                case "organ": padOsc = Instrument("organ"); break;
                case "glass": padOsc = (p, t) => (Sin(p) + 0.35f * Sin(p * 2.01f) + 0.2f * Tri(p * 0.5f)) * 0.8f; break;
                case "soft": padOsc = (p, t) => Tri(p) * 0.6f + Sin(p * 0.5f) * 0.4f; break;
                default: padOsc = (p, t) => (Saw(p) + Saw(p * 1.006f)) * 0.5f; break;
            }
            Grooves.TryGetValue(s.Drums ?? "", out var groove);
            bool hasDrums = s.Drums != "none" && groove.k != null;
            int prog = Math.Max(1, s.Progression.Length);

            for (int b = 0; b < bars; b++)
            {
                double t0 = b * bar;
                int deg = s.Progression[(b / Math.Max(1, s.BarsPerChord)) % prog];
                int nextDeg = s.Progression[((b + 1) / Math.Max(1, s.BarsPerChord)) % prog];
                var chord = Chord(deg);

                // Arrangement: fraction through the loop decides who plays.
                float f = b / (float)bars;
                bool intro = !fanfare && f < 0.125f;
                bool breakdown = !fanfare && f >= 0.625f && f < 0.75f;
                bool leadOn = fanfare || (calm ? f >= 0.25f && !breakdown : (f >= 0.375f && f < 0.625f) || f >= 0.75f);
                bool fullDrums = hasDrums && !intro && !breakdown;
                bool lastOfSection = !fanfare && (b + 1) % Math.Max(4, bars / 8) == 0;

                // ---- drums
                if (hasDrums)
                {
                    for (int st = 0; st < 16; st++)
                    {
                        double at = t0 + Swung(st);
                        float dv = calm ? 0.45f : 1f;
                        if (Hit(groove.k, st) && (fullDrums || (intro && st % 8 == 0)))
                        {
                            Kick(drums, at, 0.95f * dv, s.Drums == "trap" ? 1.4f : 1f);
                            kicks.Add(at);
                        }
                        if (fullDrums && Hit(groove.s, st))
                        {
                            if (s.Drums == "chip") Noise(drums, at, 0.08f, 0.45f, 0f, rng, 1f, 1.5f);
                            else Noise(drums, at, calm ? 0.12f : 0.2f, 0.55f * dv, calm ? 0f : 185f, rng, calm ? 0.25f : 0.4f);
                            if (s.Drums == "four" || s.Drums == "trap") Noise(drums, at + 0.012, 0.12f, 0.25f * dv, 0f, rng, 0.6f); // clap layer
                        }
                        if ((fullDrums || breakdown) && Hit(groove.h, st))
                        {
                            float hv = (st % 4 == 2 ? 0.28f : 0.18f) * dv * (breakdown ? 0.5f : 1f);
                            Noise(drums, at, s.Drums == "chip" ? 0.02f : 0.04f, hv, 0f, rng, 1f);
                        }
                        // Trap hat rolls at the end of every other bar.
                        if (fullDrums && s.Drums == "trap" && b % 2 == 1 && st >= 12)
                            Noise(drums, at + step * 0.5, 0.025f, 0.14f, 0f, rng, 1f);
                    }
                    if (fullDrums && lastOfSection)
                        for (int r = 0; r < 4; r++) Noise(drums, t0 + 3 * beat + r * step, 0.1f, 0.25f + r * 0.06f, 140f + r * 30f, rng, 0.45f); // fill
                    if (!calm && (b == 0 || (lastOfSection && b + 1 < bars)))
                        Noise(fx, b == 0 ? t0 : t0 + bar, 1.6f, 0.22f, 0f, rng, 0.9f, 1.2f); // crash
                }
                if (breakdown && !calm && b % 4 == 3)
                {
                    // Riser into the drop.
                    fx.Note(t0, bar, 200f, 0.05f, (p, t) => (float)(rng.NextDouble() * 2 - 1) * (float)(t / bar), (float)bar * 0.9f, 0.05f);
                }

                // ---- bass
                int broot = chord[0];
                while (broot > 47) broot -= 12;
                while (broot < 33) broot += 12;
                if (!intro || b >= bars / 16)
                    RenderBass(s, bass, rng, t0, beat, step, broot, Deg(nextDeg), Swung, breakdown);

                // ---- arp
                if (s.Arp != "none" && !(intro && b < bars / 16))
                    RenderArp(s, arp, rng, arpOsc, t0, beat, step, chord, Swung, calm, b);

                // ---- pad
                if (s.Pad != "none")
                    foreach (int c in chord)
                        pad.Note(t0, bar * 0.97, Mtof(c + 12), calm ? 0.07f : 0.055f, padOsc, calm ? 0.6f : 0.25f, calm ? 0.9f : 0.4f);

                // ---- lead
                if (leadOn && s.Lead != "none")
                {
                    var m = (b / 2) % 4 == 3 ? motifB : motif;
                    int half = b % 2;
                    float lv = calm ? 0.17f : 0.2f;
                    int octave = (f >= 0.75f && !calm) ? 36 : 24;
                    foreach (var (st, len, off) in m)
                    {
                        if (st / 16 != half) continue;
                        int note = Deg(deg + off) + octave - 12;
                        lead.Note(t0 + Swung(st % 16), len * step * 0.92, Mtof(note), lv, leadOsc, 0.008f, calm ? 0.35f : 0.12f);
                    }
                }
            }

            // ---- mix
            LowPass(bass.Buf, s.Bass == "acid" ? 0.35f : s.Bass == "reese" ? 0.1f : 0.16f);
            LowPass(pad.Buf, calm ? 0.05f : 0.09f);
            HighPass(drums.Buf, 0.995f);
            HighPass(fx.Buf, 0.98f);
            if (!calm && (s.Drums == "four" || s.Drums == "dnb" || s.Drums == "chip"))
            {
                Duck(pad.Buf, kicks, 0.6f, beat * 0.5);
                Duck(bass.Buf, kicks, s.Bass == "sustain" || s.Bass == "reese" ? 0.7f : 0.35f, beat * 0.4);
                Duck(arp.Buf, kicks, 0.3f, beat * 0.3);
            }
            Echo(arp.Buf, beat * 0.75, calm ? 0.35f : 0.28f);
            Echo(lead.Buf, beat * (calm ? 1.5 : 0.75), calm ? 0.32f : 0.24f);
            return Master((drums, calm ? 0.7f : 0.9f), (bass, 0.75f), (arp, 0.42f), (pad, 0.5f), (lead, 0.55f), (fx, 0.35f));
        }

        static void RenderBass(SongDef s, Track bass, Random rng, double t0, double beat, double step, int root, int next, Func<int, double> swung, bool breakdown)
        {
            Func<float, float, float> saw = (p, t) => Saw(p) * 0.65f + Sq(p * 0.5f) * 0.35f;
            Func<float, float, float> round = (p, t) => Sin(p) + Tri(p) * 0.35f;
            switch (s.Bass)
            {
                case "eighths":
                    for (int e = 0; e < 8; e++)
                        bass.Note(t0 + swung(e * 2), beat * 0.42, Mtof(root + (e % 4 == 3 ? 12 : 0)), breakdown ? 0.25f : 0.5f, saw, 0.004f, 0.05f);
                    break;
                case "offbeat":
                    for (int q = 0; q < 4; q++)
                        bass.Note(t0 + swung(q * 4 + 2), beat * 0.4, Mtof(root), 0.55f, saw, 0.004f, 0.06f);
                    break;
                case "arp":
                {
                    int[] seq = { 0, 7, 12, 7, 0, 7, 12, 19 };
                    for (int e = 0; e < 8; e++)
                        bass.Note(t0 + swung(e * 2), beat * 0.4, Mtof(root + seq[e]), 0.42f, saw, 0.004f, 0.05f);
                    break;
                }
                case "acid":
                    for (int st = 0; st < 16; st++)
                    {
                        if (rng.NextDouble() < 0.3) continue;
                        int note = root + (rng.NextDouble() < 0.25 ? 12 : rng.NextDouble() < 0.15 ? 7 : 0);
                        bass.Note(t0 + swung(st), step * 0.7, Mtof(note), 0.4f, (p, t) => Saw(p) * (float)Math.Exp(-t * 9f) + Saw(p) * 0.3f, 0.002f, 0.03f);
                    }
                    break;
                case "walking":
                {
                    int target = next;
                    while (target > root + 6) target -= 12;
                    while (target < root - 6) target += 12;
                    int[] walk = { root, root + 4 > target ? root + 2 : root + 4, root + 7, target + (target > root ? -1 : 1) };
                    for (int q = 0; q < 4; q++)
                        bass.Note(t0 + q * beat, beat * 0.85, Mtof(walk[q]), 0.5f, round, 0.01f, 0.08f);
                    break;
                }
                case "reese":
                    bass.Note(t0, beat * 3.8, Mtof(root), 0.45f, (p, t) => (Saw(p) + Saw(p * 1.012f)) * 0.5f, 0.02f, 0.2f);
                    break;
                case "none":
                    break;
                default: // sustain
                    bass.Note(t0, beat * 3.7, Mtof(root), 0.45f, round, 0.04f, 0.4f);
                    break;
            }
        }

        static void RenderArp(SongDef s, Track arp, Random rng, Func<float, float, float> osc, double t0, double beat, double step, int[] chord, Func<int, double> swung, bool calm, int b)
        {
            float v = calm ? 0.13f : 0.15f;
            switch (s.Arp)
            {
                case "stabs":
                    for (int q = 0; q < 4; q++)
                        foreach (int c in chord) arp.Note(t0 + swung(q * 4 + 2), step * 1.4, Mtof(c + 12), v * 0.6f, osc, 0.003f, 0.08f);
                    break;
                case "pluck":
                    for (int e = 0; e < 8; e++)
                    {
                        if (rng.NextDouble() < (calm ? 0.35 : 0.2)) continue;
                        int note = chord[(e * 2 + b) % chord.Length] + 12 + (rng.NextDouble() < 0.2 ? 12 : 0);
                        arp.Note(t0 + swung(e * 2), step * 1.2, Mtof(note), v * 1.1f, osc, 0.003f, 0.35f);
                    }
                    break;
                default:
                {
                    int len = chord.Length;
                    for (int st = 0; st < 16; st++)
                    {
                        int idx;
                        if (s.Arp == "updown") { int k = st % (len * 2 - 2); idx = k < len ? k : len * 2 - 2 - k; }
                        else if (s.Arp == "random") idx = rng.Next(len);
                        else idx = st % len;
                        int note = chord[Math.Max(0, Math.Min(len - 1, idx))] + 12 + (st >= 8 ? 12 : 0);
                        if (calm && st % 2 == 1) continue;
                        arp.Note(t0 + swung(st), step * 0.55, Mtof(note), v, osc, 0.002f, 0.07f);
                    }
                    break;
                }
            }
        }

        /// <summary>A two-bar phrase: (start step 0-31, length in steps, scale-degree offset from the chord root).</summary>
        static List<(int st, int len, int off)> BuildMotif(Random rng, bool calm, List<(int st, int len, int off)> rhythmFrom = null)
        {
            var notes = new List<(int, int, int)>();
            if (rhythmFrom != null)
            {
                // Same rhythm, new pitches: an "answer" to the first phrase.
                int prev = 4;
                foreach (var (st, len, _) in rhythmFrom)
                {
                    int off = st % 4 == 0 ? new[] { 0, 2, 4, 7 }[rng.Next(4)] : prev + (rng.Next(2) == 0 ? -1 : 1) * (1 + rng.Next(2));
                    off = Math.Max(-2, Math.Min(9, off));
                    notes.Add((st, len, off));
                    prev = off;
                }
                return notes;
            }
            int[][] cells = calm
                ? new[] { new[] { 4 }, new[] { 8 }, new[] { 2, 2 }, new[] { 6, 2 }, new[] { -4 }, new[] { 3, 1 } }
                : new[] { new[] { 2, 2 }, new[] { 1, 1, 2 }, new[] { 3, 1 }, new[] { 2, 1, 1 }, new[] { 4 }, new[] { -2, 2 }, new[] { 1, 1, 1, 1 }, new[] { -4 } };
            int pos = 0, last = 4;
            while (pos < 32)
            {
                var cell = cells[rng.Next(cells.Length)];
                foreach (int raw in cell)
                {
                    if (pos >= 32) break;
                    int len = Math.Abs(raw);
                    if (raw > 0)
                    {
                        int off = pos % 8 == 0 ? new[] { 0, 2, 4, 4, 7 }[rng.Next(5)] : last + (rng.Next(3) - 1) * (1 + rng.Next(2));
                        off = Math.Max(-2, Math.Min(9, off));
                        notes.Add((pos, Math.Min(len, 32 - pos), off));
                        last = off;
                    }
                    pos += len;
                }
            }
            // End the phrase on a chord tone.
            if (notes.Count > 0)
            {
                var (st, len, _) = notes[notes.Count - 1];
                notes[notes.Count - 1] = (st, len, rng.Next(2) == 0 ? 0 : 4);
            }
            return notes;
        }

        static float[] Master(params (Track t, float gain)[] tracks)
        {
            int n = tracks[0].t.Buf.Length;
            var o = new float[n];
            foreach (var (t, g) in tracks) for (int i = 0; i < n; i++) o[i] += t.Buf[i] * g;
            // Even out loudness between songs (RMS), then soft-limit the peaks.
            double sum = 0;
            for (int i = 0; i < n; i++) { float v = o[i]; if (!float.IsNaN(v)) sum += v * v; }
            float rms = (float)Math.Sqrt(sum / Math.Max(1, n));
            float gain = rms > 1e-6f ? 0.2f / rms : 1f;
            for (int i = 0; i < n; i++)
            {
                float v = o[i];
                o[i] = float.IsNaN(v) ? 0f : (float)Math.Tanh(v * gain / 0.8f) * 0.8f;
            }
            return o;
        }
    }
}
