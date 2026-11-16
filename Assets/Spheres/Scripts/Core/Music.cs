using System;
using System.Threading;
using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// Procedural background music: an upbeat battle loop and a calm menu loop, composed from chord
    /// progressions and synthesized on a background thread at startup. Crossfades by game state.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        public const int Rate = 22050;

        AudioSource a, b;
        AudioClip menuClip, battleClip;
        AudioClip current;
        float fade = 1f;
        static float[] menuData, battleData;
        static volatile bool ready;
        static bool started;

        /// <summary>Mods can swap tracks: (state) => clip or null to use the default.</summary>
        public static Func<GameState, AudioClip> TrackOverride;

        void Awake()
        {
            a = gameObject.AddComponent<AudioSource>();
            b = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { a, b })
            {
                s.loop = true;
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.volume = 0f;
            }
            if (!started)
            {
                started = true;
                var thread = new Thread(() =>
                {
                    try
                    {
                        menuData = Composer.Menu();
                        battleData = Composer.Battle();
                    }
                    catch (Exception) { menuData = battleData = null; }
                    ready = true;
                }) { IsBackground = true, Name = "Spheres music" };
                thread.Start();
            }
        }

        void Update()
        {
            if (ready && menuClip == null && menuData != null)
            {
                menuClip = AudioClip.Create("Spheres Menu", menuData.Length, 1, Rate, false);
                menuClip.SetData(menuData, 0);
                battleClip = AudioClip.Create("Spheres Battle", battleData.Length, 1, Rate, false);
                battleClip.SetData(battleData, 0);
                menuData = battleData = null;
            }
            var gm = GameManager.Instance;
            if (gm == null || menuClip == null) return;

            var state = gm.State;
            bool battle = state == GameState.RoundIntro || state == GameState.Fighting || state == GameState.RoundEnd;
            AudioClip want = TrackOverride?.Invoke(state) ?? (battle ? battleClip : menuClip);

            if (want != current)
            {
                // Swap sources and fade the new one in.
                var t = a; a = b; b = t;
                a.clip = want;
                a.Play();
                current = want;
                fade = 0f;
            }
            fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / 1.5f);
            float vol = gm.Settings.musicVolume * (gm.Paused ? 0.4f : 1f) * (state == GameState.Editor ? 0.5f : 1f);
            a.volume = vol * fade;
            b.volume = vol * (1f - fade);
            if (b.volume <= 0.001f && b.isPlaying) b.Stop();
        }
    }

    /// <summary>A tiny offline sequencer + synth used to render the music loops.</summary>
    public static class Composer
    {
        const int R = MusicPlayer.Rate;

        static float Mtof(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        class Track
        {
            public readonly float[] Buf;
            public Track(int n) { Buf = new float[n]; }

            /// <summary>Add a note. Writes past the end wrap around so the loop is seamless.</summary>
            public void Note(float start, float length, float freq, float vol, Func<float, float, float> osc, float attack, float release)
            {
                int s0 = Mathf.RoundToInt(start * R);
                int n = Mathf.RoundToInt((length + release) * R);
                float phase = 0f;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)R;
                    float env = t < attack ? t / attack : t < length ? 1f : Mathf.Max(0f, 1f - (t - length) / release);
                    phase += freq / R;
                    Buf[(s0 + i) % Buf.Length] += osc(phase, t) * env * vol;
                }
            }
        }

        static float Saw(float p) => (p - Mathf.Floor(p)) * 2f - 1f;
        static float Sq(float p) => (p - Mathf.Floor(p)) < 0.5f ? 1f : -1f;
        static float Tri(float p) => 1f - 4f * Mathf.Abs(p - Mathf.Floor(p) - 0.5f);
        static float Sin(float p) => Mathf.Sin(p * 6.2831853f);

        static void Kick(Track t, float at, float vol)
        {
            int s0 = Mathf.RoundToInt(at * R), n = Mathf.RoundToInt(0.28f * R);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                phase += Mathf.Lerp(140f, 42f, Mathf.Sqrt(k)) / R;
                t.Buf[(s0 + i) % t.Buf.Length] += Sin(phase) * (1f - k) * (1f - k) * vol;
            }
        }

        static void NoiseHit(Track t, float at, float dur, float vol, float tone, System.Random rng, float lowpass)
        {
            int s0 = Mathf.RoundToInt(at * R), n = Mathf.RoundToInt(dur * R);
            float y = 0f, a = Mathf.Clamp01(lowpass);
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                y += a * (x - y);
                float sample = (lowpass >= 1f ? x : y) + (tone > 0f ? Sin(i * tone / R) * 0.5f : 0f);
                t.Buf[(s0 + i) % t.Buf.Length] += sample * Mathf.Pow(1f - k, 3f) * vol;
            }
        }

        static void LowPass(float[] d, float a)
        {
            float y = d[d.Length - 1];
            for (int i = 0; i < d.Length; i++) { y += a * (d[i] - y); d[i] = y; }
        }

        static void Echo(float[] d, float seconds, float feedback)
        {
            int dl = Mathf.RoundToInt(seconds * R);
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < d.Length; i++) d[i] += d[(i - dl + d.Length) % d.Length] * feedback;
        }

        static float[] Master(params (Track t, float gain)[] tracks)
        {
            int n = tracks[0].t.Buf.Length;
            var o = new float[n];
            foreach (var (t, g) in tracks) for (int i = 0; i < n; i++) o[i] += t.Buf[i] * g;
            float peak = 0.001f;
            foreach (var v in o) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < n; i++) o[i] = (float)Math.Tanh(o[i] / peak * 1.2f) * 0.75f;
            return o;
        }

        /// <summary>124 BPM, E minor: four-on-the-floor drums, driving bass, arpeggios, lead in the second half.</summary>
        public static float[] Battle()
        {
            const float bpm = 124f;
            float beat = 60f / bpm, bar = beat * 4f;
            const int bars = 16;
            int n = Mathf.RoundToInt(bar * bars * R);
            var rng = new System.Random(7);
            var drums = new Track(n); var bass = new Track(n); var arp = new Track(n); var pad = new Track(n); var lead = new Track(n);
            // i - VI - III - VII  (Em, C, G, D), each chord two bars.
            int[][] chords = { new[] { 52, 55, 59 }, new[] { 48, 52, 55 }, new[] { 55, 59, 62 }, new[] { 50, 54, 57 } };
            int[] scale = { 64, 66, 67, 69, 71, 72, 74, 76, 78, 79 };

            for (int b = 0; b < bars; b++)
            {
                var chord = chords[(b / 2) % chords.Length];
                float t0 = b * bar;
                for (int q = 0; q < 4; q++)
                {
                    Kick(drums, t0 + q * beat, 0.9f);
                    NoiseHit(drums, t0 + q * beat + beat * 0.5f, 0.05f, 0.25f, 0f, rng, 1f); // hats
                    if (q == 1 || q == 3) NoiseHit(drums, t0 + q * beat, 0.18f, 0.5f, 190f, rng, 0.35f); // snare
                }
                if (b % 4 == 3) for (int s = 0; s < 4; s++) NoiseHit(drums, t0 + 3f * beat + s * beat * 0.25f, 0.1f, 0.3f, 220f, rng, 0.4f); // fill

                // Bass: driving eighths, octave bounce.
                for (int e = 0; e < 8; e++)
                {
                    int note = chord[0] - 12 + (e % 4 == 3 ? 12 : 0);
                    bass.Note(t0 + e * beat * 0.5f, beat * 0.4f, Mtof(note), 0.5f, (p, t) => Saw(p) * 0.6f + Sq(p * 0.5f) * 0.4f, 0.005f, 0.05f);
                }
                // Arpeggio: sixteenths through chord tones.
                for (int s = 0; s < 16; s++)
                {
                    int idx = s % 4 == 3 ? 1 : s % 3;
                    int note = chord[idx] + 12 + (s >= 8 ? 12 : 0);
                    arp.Note(t0 + s * beat * 0.25f, beat * 0.12f, Mtof(note), 0.18f, (p, t) => Sq(p), 0.002f, 0.08f);
                }
                // Pad: sustained chord, detuned saws.
                foreach (int c in chord)
                    pad.Note(t0, bar * 0.95f, Mtof(c + 12), 0.08f, (p, t) => Saw(p) + Saw(p * 1.006f), 0.25f, 0.4f);

                // Lead melody in the second half.
                if (b >= 8)
                {
                    float tt = t0;
                    while (tt < t0 + bar - 0.01f)
                    {
                        float len = rng.NextDouble() < 0.6 ? beat * 0.5f : beat;
                        int note = rng.NextDouble() < 0.6 ? chord[rng.Next(3)] + 24 : scale[rng.Next(scale.Length)];
                        if (rng.NextDouble() < 0.15) { tt += len; continue; }
                        lead.Note(tt, len * 0.9f, Mtof(note), 0.22f, (p, t) => Tri(p + Mathf.Sin(t * 30f) * 0.004f * t * 10f), 0.01f, 0.12f);
                        tt += len;
                    }
                }
            }
            LowPass(bass.Buf, 0.12f);
            LowPass(pad.Buf, 0.08f);
            Echo(arp.Buf, beat * 0.75f, 0.3f);
            Echo(lead.Buf, beat * 0.5f, 0.25f);
            return Master((drums, 0.9f), (bass, 0.8f), (arp, 0.45f), (pad, 0.5f), (lead, 0.55f));
        }

        /// <summary>90 BPM, A minor seventh chords: soft pads, plucked arpeggio, gentle pulse.</summary>
        public static float[] Menu()
        {
            const float bpm = 90f;
            float beat = 60f / bpm, bar = beat * 4f;
            const int bars = 16;
            int n = Mathf.RoundToInt(bar * bars * R);
            var rng = new System.Random(11);
            var pad = new Track(n); var pluck = new Track(n); var drums = new Track(n); var bass = new Track(n);
            int[][] chords = { new[] { 57, 60, 64, 67 }, new[] { 53, 57, 60, 64 }, new[] { 48, 52, 55, 59 }, new[] { 55, 59, 62, 64 } };
            for (int b = 0; b < bars; b++)
            {
                var chord = chords[(b / 2) % chords.Length];
                float t0 = b * bar;
                foreach (int c in chord)
                    pad.Note(t0, bar, Mtof(c), 0.07f, (p, t) => Saw(p) + Saw(p * 1.004f) + Tri(p * 0.5f), 0.8f, 0.9f);
                bass.Note(t0, bar * 0.9f, Mtof(chord[0] - 12), 0.35f, (p, t) => Sin(p) + Tri(p) * 0.3f, 0.05f, 0.4f);
                for (int e = 0; e < 8; e++)
                {
                    if (rng.NextDouble() < 0.2) continue;
                    int note = chord[(e * 2 + b) % chord.Length] + 12;
                    pluck.Note(t0 + e * beat * 0.5f, beat * 0.1f, Mtof(note), 0.16f, (p, t) => Tri(p) * Mathf.Exp(-t * 6f), 0.003f, 0.4f);
                }
                Kick(drums, t0, 0.35f);
                Kick(drums, t0 + beat * 2f, 0.25f);
                for (int s = 0; s < 8; s++) NoiseHit(drums, t0 + s * beat * 0.5f, 0.04f, s % 2 == 0 ? 0.08f : 0.05f, 0f, rng, 1f);
            }
            LowPass(pad.Buf, 0.05f);
            LowPass(bass.Buf, 0.06f);
            Echo(pluck.Buf, beat * 0.75f, 0.35f);
            return Master((pad, 0.7f), (pluck, 0.6f), (drums, 0.5f), (bass, 0.6f));
        }
    }
}
