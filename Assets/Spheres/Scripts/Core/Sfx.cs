using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spheres
{
    /// <summary>
    /// Procedurally synthesized sound effects (layered oscillators, filtered noise, small reverb tails),
    /// so the game ships without audio assets. Mods can replace any sound with <see cref="Register"/>.
    /// </summary>
    public static class Sfx
    {
        const int SampleRate = 44100;
        static AudioSource[] pool;
        static int next;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly System.Random noise = new System.Random(1234);

        public static float Volume = 0.6f;

        /// <summary>Reset statics when "Enter Play Mode Options" skips domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pool = null;
            next = 0;
            Player.All.Clear();
            Bullet.Active.Clear();
            PlayerCamera.Active.Clear();
        }

        public static void Init(GameObject host)
        {
            if (pool != null) return;
            pool = new AudioSource[24];
            for (int i = 0; i < pool.Length; i++)
            {
                var s = host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                pool[i] = s;
            }
            BuildDefaults();
        }

        /// <summary>Add or replace a named sound.</summary>
        public static void Register(string id, AudioClip clip)
        {
            if (!string.IsNullOrEmpty(id) && clip != null) clips[id] = clip;
        }

        /// <summary>Play a non-positional sound.</summary>
        public static void Play(string id, float volume = 1f, float pitchVariance = 0.08f)
        {
            if (Net.NetSession.Mirroring && Net.NetSession.MirroredSounds.Contains(id)) Net.NetSession.Instance.MirrorSound(id, volume, null);
            PlayLocal(id, volume, pitchVariance, 0f);
        }

        /// <summary>Play a sound at a world position: panned and attenuated relative to the nearest player camera.</summary>
        public static void PlayAt(string id, Vector3 position, float volume = 1f, float pitchVariance = 0.08f)
        {
            if (Net.NetSession.Mirroring && Net.NetSession.MirroredSounds.Contains(id)) Net.NetSession.Instance.MirrorSound(id, volume, position);
            Spatialize(position, out float gain, out float pan);
            PlayLocal(id, volume * gain, pitchVariance, pan);
        }

        static void Spatialize(Vector3 position, out float gain, out float pan)
        {
            gain = 1f;
            pan = 0f;
            Transform listener = null;
            float best = float.MaxValue;
            int cams = 0;
            foreach (var c in PlayerCamera.Active)
            {
                if (c == null || !c.isActiveAndEnabled) continue;
                cams++;
                float d = (c.transform.position - position).sqrMagnitude;
                if (d < best) { best = d; listener = c.transform; }
            }
            if (listener == null)
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.OverviewCamera != null && gm.OverviewCamera.enabled) listener = gm.OverviewCamera.transform;
            }
            if (listener == null) return;
            Vector3 local = listener.InverseTransformPoint(position);
            float dist = local.magnitude;
            gain = Mathf.Clamp(1f / (1f + dist / 20f), 0.18f, 1f);
            // Panning is confusing with several split-screen viewports, so only pan for a single view.
            if (cams <= 1) pan = Mathf.Clamp(local.x / (dist + 4f), -0.75f, 0.75f);
        }

        static void PlayLocal(string id, float volume, float pitchVariance, float pan)
        {
            if (pool == null || Volume <= 0f || volume <= 0.001f) return;
            if (!clips.TryGetValue(id, out var clip) || clip == null) return;
            var s = pool[next];
            next = (next + 1) % pool.Length;
            s.pitch = 1f + UnityEngine.Random.Range(-pitchVariance, pitchVariance);
            s.panStereo = pan;
            s.PlayOneShot(clip, volume * Volume);
        }

        // ------------------------------------------------------------------ synthesis toolkit

        static float Noise() => (float)(noise.NextDouble() * 2.0 - 1.0);
        static float Square(float phase) => Mathf.Repeat(phase, 1f) < 0.5f ? 1f : -1f;
        static float Saw(float phase) => Mathf.Repeat(phase, 1f) * 2f - 1f;
        static float Tri(float phase) => 1f - 4f * Mathf.Abs(Mathf.Repeat(phase + 0.25f, 1f) - 0.5f);
        static float Sine(float phase) => Mathf.Sin(phase * Mathf.PI * 2f);
        static float Env(float k, float attack, float power) => k < attack ? k / attack : Mathf.Pow(1f - (k - attack) / (1f - attack), power);

        /// <summary>Synthesizes a clip. wave(t, k) gets seconds and normalized progress. Phase-accumulated pitch sweeps use Sweep().</summary>
        static float[] Render(float duration, Func<float, float, float> wave)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = wave(i / (float)SampleRate, i / (float)n);
            return data;
        }

        static void LowPass(float[] d, float cutoffHz)
        {
            float rc = 1f / (2f * Mathf.PI * cutoffHz), a = (1f / SampleRate) / (rc + 1f / SampleRate);
            float y = 0f;
            for (int i = 0; i < d.Length; i++) { y += a * (d[i] - y); d[i] = y; }
        }

        static void HighPass(float[] d, float cutoffHz)
        {
            float rc = 1f / (2f * Mathf.PI * cutoffHz), a = rc / (rc + 1f / SampleRate);
            float prevX = 0f, y = 0f;
            for (int i = 0; i < d.Length; i++) { y = a * (y + d[i] - prevX); prevX = d[i]; d[i] = y; }
        }

        /// <summary>Tiny feedback-delay reverb: adds a sense of space without a convolution engine.</summary>
        static float[] Reverb(float[] d, float tail, float mix)
        {
            int extra = Mathf.RoundToInt(tail * SampleRate);
            var o = new float[d.Length + extra];
            Array.Copy(d, o, d.Length);
            int[] delays = { 1116, 1356, 1617, 1813 };
            foreach (int dl in delays)
                for (int i = dl; i < o.Length; i++) o[i] += o[i - dl] * 0.33f * mix;
            return o;
        }

        static float[] Mix(params float[][] layers)
        {
            int n = 0;
            foreach (var l in layers) n = Mathf.Max(n, l.Length);
            var o = new float[n];
            foreach (var l in layers) for (int i = 0; i < l.Length; i++) o[i] += l[i];
            return o;
        }

        static AudioClip Clip(string name, float[] data, float gain = 1f)
        {
            float peak = 0.0001f;
            foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            float g = gain / peak;
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i] * g, -1f, 1f);
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Oscillator with an exponential pitch sweep (phase accumulated so sweeps stay smooth).</summary>
        static float[] Sweep(float duration, float f0, float f1, Func<float, float> osc, float attack, float decayPower)
        {
            int n = Mathf.RoundToInt(duration * SampleRate);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float f = f0 * Mathf.Pow(f1 / f0, k);
                phase += f / SampleRate;
                d[i] = osc(phase) * Env(k, attack, decayPower);
            }
            return d;
        }

        static float[] NoiseBurst(float duration, float attack, float power)
        {
            return Render(duration, (t, k) => Noise() * Env(k, attack, power));
        }

        // ------------------------------------------------------------------ sound design

        static void BuildDefaults()
        {
            // Gunshot: noise crack + falling square + sub thump.
            var crack = NoiseBurst(0.06f, 0.01f, 3f); HighPass(crack, 1800f);
            var body = Sweep(0.14f, 900f, 180f, Square, 0.01f, 2.5f); LowPass(body, 3500f);
            var thump = Sweep(0.12f, 140f, 50f, Sine, 0.01f, 2f);
            Register("shoot", Clip("shoot", Mix(Scale(crack, 0.7f), Scale(body, 0.35f), Scale(thump, 0.6f)), 0.8f));

            // Hit: meaty thwack.
            var hitN = NoiseBurst(0.12f, 0.005f, 3f); LowPass(hitN, 2500f);
            var hitT = Sweep(0.16f, 260f, 80f, Sine, 0.005f, 2.5f);
            Register("hit", Clip("hit", Mix(Scale(hitN, 0.8f), hitT), 0.8f));

            // Hit marker: crisp high tick for the shooter.
            Register("hitmark", Clip("hitmark", Mix(Sweep(0.05f, 2600f, 1900f, Tri, 0.01f, 3f), Scale(HighPassed(NoiseBurst(0.03f, 0.01f, 3f), 4000f), 0.3f)), 0.5f));

            // Block: shimmering metallic ring with a reverb tail.
            var ring = Render(0.35f, (t, k) => (Sine(t * 1250f) * 0.5f + Sine(t * 1873f) * 0.35f + Sine(t * 2617f) * 0.2f + Sine(t * 631f) * 0.3f) * Env(k, 0.01f, 3f));
            Register("block", Clip("block", Reverb(ring, 0.3f, 0.8f), 0.7f));

            Register("jump", Clip("jump", Mix(Sweep(0.14f, 260f, 640f, Tri, 0.05f, 2f), Scale(HighPassed(NoiseBurst(0.1f, 0.3f, 2f), 2500f), 0.25f)), 0.5f));
            Register("land", Clip("land", Mix(Sweep(0.12f, 120f, 55f, Sine, 0.01f, 2f), Scale(LowPassed(NoiseBurst(0.1f, 0.01f, 3f), 900f), 0.8f)), 0.6f));
            Register("step", Clip("step", Mix(Sweep(0.05f, 180f, 90f, Sine, 0.01f, 3f), Scale(LowPassed(NoiseBurst(0.04f, 0.01f, 3f), 1400f), 0.6f)), 0.4f));

            // Bounce: springy boing with vibrato.
            var boing = Render(0.32f, (t, k) => Sine(t * (300f + 420f * Mathf.Sqrt(k)) + Mathf.Sin(t * 40f) * 0.6f) * Env(k, 0.02f, 2f));
            Register("bounce", Clip("bounce", boing, 0.7f));

            // Explosion: low boom, rumble and crackle.
            var boomN = NoiseBurst(0.9f, 0.005f, 2.2f); LowPass(boomN, 450f); LowPass(boomN, 450f);
            var boomT = Sweep(0.6f, 90f, 32f, Sine, 0.005f, 1.6f);
            var crackle = Render(0.5f, (t, k) => (noise.NextDouble() < 0.02 ? Noise() : 0f) * (1f - k));
            Register("explode", Clip("explode", Reverb(Mix(Scale(boomN, 1.4f), boomT, Scale(crackle, 0.5f)), 0.4f, 0.5f), 0.95f));

            // Death: shatter (noisy chimes cascading down).
            var shatter = Render(0.6f, (t, k) =>
            {
                float f = Mathf.Lerp(2400f, 500f, k);
                return (Sine(t * f) * 0.3f + Sine(t * f * 1.5f) * 0.2f + Noise() * 0.25f * (1f - k)) * Env(k, 0.005f, 2f);
            });
            Register("death", Clip("death", Reverb(shatter, 0.4f, 0.6f), 0.8f));

            Register("revive", Clip("revive", Reverb(Arpeggio(new[] { 523f, 659f, 784f, 1047f }, 0.08f, Tri), 0.4f, 0.7f), 0.7f));
            Register("pick", Clip("pick", Reverb(Arpeggio(new[] { 659f, 784f, 988f, 1319f }, 0.06f, Square), 0.3f, 0.6f), 0.55f));
            Register("deal", Clip("deal", Mix(HighPassed(Render(0.18f, (t, k) => Noise() * Env(k, 0.4f, 2f)), 2500f)), 0.35f));
            Register("hover", Clip("hover", Sweep(0.05f, 1400f, 1700f, Sine, 0.02f, 2f), 0.3f));
            Register("ui", Clip("ui", Mix(Sweep(0.05f, 900f, 700f, Tri, 0.01f, 3f)), 0.35f));
            Register("tick", Clip("tick", Render(0.12f, (t, k) => (Square(t * 880f) * 0.3f + Sine(t * 1760f) * 0.3f) * Env(k, 0.01f, 3f)), 0.5f));
            Register("go", Clip("go", Reverb(Mix(Render(0.45f, (t, k) => (Saw(t * 440f) + Saw(t * 443f) + Saw(t * 660f)) * 0.3f * Env(k, 0.02f, 1.5f))), 0.4f, 0.6f), 0.7f));
            Register("border", Clip("border", Mix(Render(0.3f, (t, k) => Square(t * 110f + Mathf.Sin(t * 70f) * 2f) * 0.4f * Env(k, 0.01f, 2f)), Scale(HighPassed(NoiseBurst(0.25f, 0.01f, 2f), 3000f), 0.4f)), 0.6f));
            Register("reload", Clip("reload", Render(0.28f, (t, k) =>
            {
                bool c1 = k < 0.08f, c2 = k > 0.62f && k < 0.72f;
                return (c1 || c2 ? Noise() * 0.6f + Square(t * (c1 ? 1800f : 2400f)) * 0.3f : 0f);
            }), 0.45f));
            Register("zap", Clip("zap", Render(0.18f, (t, k) => (Saw(t * (120f + Noise() * 30f)) * 0.5f + Noise() * 0.4f) * Env(k, 0.01f, 2f)), 0.55f));
            Register("heartbeat", Clip("heartbeat", Mix(Sweep(0.12f, 70f, 45f, Sine, 0.02f, 2f), Pad(Sweep(0.12f, 65f, 42f, Sine, 0.02f, 2f), 0.18f)), 0.6f));
            Register("win", Clip("win", Reverb(Arpeggio(new[] { 392f, 523f, 659f, 784f, 1047f }, 0.11f, Square), 0.6f, 0.7f), 0.7f));
        }

        static float[] Scale(float[] d, float g) { for (int i = 0; i < d.Length; i++) d[i] *= g; return d; }
        static float[] LowPassed(float[] d, float hz) { LowPass(d, hz); return d; }
        static float[] HighPassed(float[] d, float hz) { HighPass(d, hz); return d; }

        static float[] Pad(float[] d, float leadSeconds)
        {
            int lead = Mathf.RoundToInt(leadSeconds * SampleRate);
            var o = new float[d.Length + lead];
            Array.Copy(d, 0, o, lead, d.Length);
            return o;
        }

        static float[] Arpeggio(float[] notes, float step, Func<float, float> osc)
        {
            int stepN = Mathf.RoundToInt(step * SampleRate);
            int noteLen = Mathf.RoundToInt(step * 3f * SampleRate);
            var o = new float[stepN * (notes.Length - 1) + noteLen];
            for (int n = 0; n < notes.Length; n++)
                for (int i = 0; i < noteLen; i++)
                {
                    float t = i / (float)SampleRate, k = i / (float)noteLen;
                    o[n * stepN + i] += osc(t * notes[n]) * 0.35f * Env(k, 0.02f, 2.5f);
                }
            return o;
        }
    }
}
