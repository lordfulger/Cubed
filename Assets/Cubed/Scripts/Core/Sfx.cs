using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cubed
{
    /// <summary>
    /// Procedurally synthesized sound effects, so the game ships without audio assets.
    /// Mods can replace any sound with <see cref="Register"/>.
    /// </summary>
    public static class Sfx
    {
        const int SampleRate = 44100;
        static AudioSource[] pool;
        static int next;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly System.Random noise = new System.Random(1234);

        public static float Volume = 0.6f;

        public static void Init(GameObject host)
        {
            if (pool != null) return;
            pool = new AudioSource[14];
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

        public static void Play(string id, float volume = 1f, float pitchVariance = 0.08f)
        {
            if (pool == null || Volume <= 0f) return;
            if (!clips.TryGetValue(id, out var clip) || clip == null) return;
            var s = pool[next];
            next = (next + 1) % pool.Length;
            s.pitch = 1f + UnityEngine.Random.Range(-pitchVariance, pitchVariance);
            s.PlayOneShot(clip, volume * Volume);
        }

        static float Noise() => (float)(noise.NextDouble() * 2.0 - 1.0);

        static AudioClip Synth(string name, float duration, Func<float, float, float> wave)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                data[i] = Mathf.Clamp(wave(t, i / (float)n), -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Square(float phase) => Mathf.Repeat(phase, 1f) < 0.5f ? 1f : -1f;
        static float Saw(float phase) => Mathf.Repeat(phase, 1f) * 2f - 1f;
        static float Sine(float phase) => Mathf.Sin(phase * Mathf.PI * 2f);

        static void BuildDefaults()
        {
            Register("shoot", Synth("shoot", 0.12f, (t, k) =>
            {
                float f = Mathf.Lerp(950f, 260f, k);
                return Square(t * f) * 0.25f * Mathf.Pow(1f - k, 2f) + Noise() * 0.08f * (1f - k);
            }));
            Register("hit", Synth("hit", 0.16f, (t, k) =>
                (Sine(t * Mathf.Lerp(220f, 90f, k)) * 0.6f + Noise() * 0.35f) * Mathf.Pow(1f - k, 2.5f)));
            Register("block", Synth("block", 0.32f, (t, k) =>
                (Sine(t * 1250f) * 0.35f + Sine(t * 1870f) * 0.25f + Sine(t * 630f) * 0.2f) * Mathf.Pow(1f - k, 3f)));
            Register("jump", Synth("jump", 0.12f, (t, k) =>
                Sine(t * Mathf.Lerp(280f, 620f, k)) * 0.3f * (1f - k)));
            Register("bounce", Synth("bounce", 0.2f, (t, k) =>
                Sine(t * Mathf.Lerp(180f, 820f, Mathf.Sqrt(k))) * 0.4f * (1f - k)));
            float low = 0f;
            Register("explode", Synth("explode", 0.55f, (t, k) =>
            {
                low = Mathf.Lerp(low, Noise(), 0.08f);
                return (low * 2.2f + Sine(t * 55f) * 0.3f) * Mathf.Pow(1f - k, 1.6f);
            }));
            Register("death", Synth("death", 0.5f, (t, k) =>
                Saw(t * Mathf.Lerp(520f, 70f, k)) * 0.28f * (1f - k)));
            Register("pick", Synth("pick", 0.3f, (t, k) =>
            {
                float f = k < 0.33f ? 523f : k < 0.66f ? 659f : 784f;
                return Sine(t * f) * 0.3f * (1f - Mathf.Repeat(k * 3f, 1f) * 0.6f) * (1f - k * 0.5f);
            }));
            Register("tick", Synth("tick", 0.035f, (t, k) => Square(t * 1800f) * 0.2f * (1f - k)));
            Register("border", Synth("border", 0.25f, (t, k) =>
                (Square(t * 120f) * 0.25f + Noise() * 0.15f) * (1f - k)));
            Register("reload", Synth("reload", 0.22f, (t, k) =>
            {
                bool click = (k > 0.0f && k < 0.12f) || (k > 0.7f && k < 0.82f);
                return click ? Noise() * 0.35f : 0f;
            }));
            Register("zap", Synth("zap", 0.15f, (t, k) => (Saw(t * 90f) * 0.3f + Noise() * 0.25f) * (1f - k)));
            Register("ui", Synth("ui", 0.05f, (t, k) => Sine(t * 900f) * 0.2f * (1f - k)));
            Register("win", Synth("win", 0.8f, (t, k) =>
            {
                float f = k < 0.25f ? 392f : k < 0.5f ? 523f : k < 0.75f ? 659f : 784f;
                return (Sine(t * f) * 0.3f + Sine(t * f * 2f) * 0.08f) * (1f - k * 0.7f);
            }));
        }
    }
}
