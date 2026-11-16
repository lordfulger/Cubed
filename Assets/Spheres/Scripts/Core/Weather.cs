using UnityEngine;

namespace Spheres
{
    public enum WeatherKind { Clear, Rain, Snow, Fog, Storm, Embers, Random }
    public enum TimeOfDay { MapDefault, Dawn, Day, Dusk, Night, Random }

    /// <summary>
    /// Cosmetic weather and time of day for a map. "Random" picks per map from the match's weather seed,
    /// so every machine online sees the same sky without extra messages.
    /// </summary>
    public static class Weather
    {
        public static WeatherKind Current { get; private set; }
        public static TimeOfDay CurrentTime { get; private set; }
        /// <summary>Distance-fog multiplier read by the post stack (1 = normal, higher = thicker).</summary>
        public static float FogDensity { get; private set; } = 1f;
        /// <summary>True while a match is running (menus and the editor stay clear).</summary>
        public static bool Active;

        static GameObject fxRoot;
        static Light sun, fill;
        static float sunBase = 1.1f, nextLightning = 6f, flash;
        static Color sunColorBase;

        public static void RegisterLights(Light sunLight, Light fillLight)
        {
            sun = sunLight;
            fill = fillLight;
            if (sun != null) { sunBase = sun.intensity; sunColorBase = sun.color; }
        }

        static int Hash(string s, int seed)
        {
            int h = 17 + seed * 7919;
            foreach (char c in s ?? "") h = h * 31 + c;
            return h & 0x7fffffff;
        }

        public static void Resolve(MatchSettings s, MapData map, out WeatherKind w, out TimeOfDay t)
        {
            w = s != null ? s.weather : WeatherKind.Clear;
            t = s != null ? s.timeOfDay : TimeOfDay.MapDefault;
            int h = Hash(map != null ? map.name : "", s != null ? s.weatherSeed : 0);
            if (w == WeatherKind.Random)
            {
                // Clear most often.
                WeatherKind[] pool = { WeatherKind.Clear, WeatherKind.Clear, WeatherKind.Rain, WeatherKind.Snow, WeatherKind.Fog, WeatherKind.Storm, WeatherKind.Embers };
                w = pool[h % pool.Length];
            }
            if (t == TimeOfDay.Random) t = (TimeOfDay)(1 + (h / 7) % 4);
        }

        /// <summary>Tint for the map background (sky + fog) before its atmosphere is built.</summary>
        public static Color TintBackground(MapData map, Color bg)
        {
            var gm = GameManager.Instance;
            if (gm == null || !Active) return bg;
            Resolve(gm.Settings, map, out var w, out var t);
            switch (t)
            {
                case TimeOfDay.Dawn: bg = Color.Lerp(bg, new Color(0.32f, 0.16f, 0.22f), 0.55f); break;
                case TimeOfDay.Day: bg = Color.Lerp(bg, new Color(0.25f, 0.42f, 0.62f), 0.6f); break;
                case TimeOfDay.Dusk: bg = Color.Lerp(bg, new Color(0.38f, 0.17f, 0.08f), 0.55f); break;
                case TimeOfDay.Night: bg = Color.Lerp(bg, new Color(0.01f, 0.015f, 0.04f), 0.75f); break;
            }
            if (w == WeatherKind.Storm || w == WeatherKind.Rain) bg = Color.Lerp(bg, new Color(0.1f, 0.11f, 0.13f), 0.45f);
            if (w == WeatherKind.Snow || w == WeatherKind.Fog) bg = Color.Lerp(bg, new Color(0.45f, 0.48f, 0.55f), 0.35f);
            return bg;
        }

        /// <summary>Apply lighting and particles for a freshly built map (in-match only).</summary>
        public static void Apply(MapRuntime map)
        {
            Clear();
            var gm = GameManager.Instance;
            if (map == null || gm == null || !Active || map.EditorMode) { Current = WeatherKind.Clear; CurrentTime = TimeOfDay.MapDefault; return; }
            Resolve(gm.Settings, map.Data, out var w, out var t);
            Current = w;
            CurrentTime = t;

            // Lighting.
            float intensity = sunBase;
            Color sc = sunColorBase == default ? new Color(1f, 0.96f, 0.9f) : sunColorBase;
            float pitch = 52f;
            Color amb = new Color(0.55f, 0.6f, 0.75f);
            switch (t)
            {
                case TimeOfDay.Dawn: sc = new Color(1f, 0.72f, 0.65f); intensity *= 0.8f; pitch = 14f; amb = new Color(0.55f, 0.45f, 0.55f); break;
                case TimeOfDay.Day: sc = new Color(1f, 0.98f, 0.92f); intensity *= 1.15f; pitch = 65f; amb = new Color(0.6f, 0.68f, 0.85f); break;
                case TimeOfDay.Dusk: sc = new Color(1f, 0.55f, 0.3f); intensity *= 0.85f; pitch = 10f; amb = new Color(0.55f, 0.4f, 0.4f); break;
                case TimeOfDay.Night: sc = new Color(0.55f, 0.65f, 1f); intensity *= 0.3f; pitch = 40f; amb = new Color(0.2f, 0.24f, 0.38f); break;
            }
            if (w == WeatherKind.Storm || w == WeatherKind.Rain || w == WeatherKind.Fog) { intensity *= 0.65f; amb *= 0.85f; }
            if (sun != null)
            {
                sun.color = sc;
                sun.intensity = intensity;
                var e = sun.transform.eulerAngles;
                sun.transform.rotation = Quaternion.Euler(pitch, e.y, 0f);
            }
            RenderSettings.ambientSkyColor = amb;
            RenderSettings.ambientEquatorColor = amb * 0.65f;
            FogDensity = w == WeatherKind.Fog ? 3.5f : w == WeatherKind.Storm ? 1.8f : w == WeatherKind.Snow ? 1.5f : w == WeatherKind.Rain ? 1.4f : 1f;
            sunBaseNow = intensity;
            nextLightning = Random.Range(4f, 10f);

            if (!Options.Current.ambientParticles) return;
            fxRoot = new GameObject("Weather");
            fxRoot.transform.SetParent(map.transform, false);
            var b = map.FullBounds;
            switch (w)
            {
                case WeatherKind.Rain: Particles(b, new Color(0.7f, 0.8f, 1f, 0.45f), 26f, 0.6f, 0.035f, 1f, 2.2f, 0f, true); break;
                case WeatherKind.Storm: Particles(b, new Color(0.7f, 0.8f, 1f, 0.5f), 34f, 0.5f, 0.04f, 1.6f, 2.6f, 0.15f, true); break;
                case WeatherKind.Snow: Particles(b, new Color(1f, 1f, 1f, 0.85f), 2.2f, 7f, 0.12f, 0.6f, 1f, 0.6f, false); break;
                case WeatherKind.Embers: Particles(b, new Color(1f, 0.55f, 0.15f, 0.9f), -1.6f, 6f, 0.09f, 0.35f, 1f, 0.8f, false); break;
                case WeatherKind.Fog: Particles(b, new Color(0.85f, 0.88f, 0.95f, 0.06f), 0.1f, 12f, 4f, 0.08f, 1f, 0.3f, false); break;
            }
        }

        static float sunBaseNow = 1.1f;

        static void Particles(Bounds b, Color color, float fallSpeed, float life, float size, float rateScale, float stretch, float wobble, bool streaks)
        {
            var go = new GameObject("WeatherParticles");
            go.transform.SetParent(fxRoot.transform, false);
            go.transform.position = new Vector3(b.center.x, fallSpeed >= 0f ? b.max.y : b.min.y + 2f, b.center.z);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = life;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
            main.startColor = color;
            main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-wobble * 2f, wobble * 2f);
            vel.y = new ParticleSystem.MinMaxCurve(-fallSpeed * 1.1f, -fallSpeed * 0.9f);
            vel.z = new ParticleSystem.MinMaxCurve(-wobble * 2f, wobble * 2f);
            var emission = ps.emission;
            emission.rateOverTime = Mathf.Clamp(b.size.x * b.size.z * rateScale, 30f, 2500f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(b.size.x, fallSpeed == 0.1f ? b.size.y : 0.5f, b.size.z);
            if (wobble > 0f)
            {
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = wobble;
                noise.frequency = 0.3f;
            }
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            var mat = Mats.NewTransparent(Color.white);
            mat.mainTexture = Looks.Glow;
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (streaks)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.02f * stretch;
                r.lengthScale = 1f;
            }
            ps.Simulate(Mathf.Min(life, 6f), true, true);
            ps.Play();
        }

        /// <summary>Per-frame: lightning in storms.</summary>
        public static void Update(float dt)
        {
            if (flash > 0f)
            {
                flash = Mathf.Max(0f, flash - dt * 3f);
                if (sun != null) sun.intensity = sunBaseNow + flash * 2.5f;
            }
            var gm = GameManager.Instance;
            if (Current != WeatherKind.Storm || gm == null || !Active || fxRoot == null) return;
            nextLightning -= dt;
            if (nextLightning > 0f) return;
            nextLightning = Random.Range(5f, 14f);
            flash = 1f;
            if (gm.Map != null)
            {
                var b = gm.Map.FullBounds;
                var p = new Vector3(Random.Range(b.min.x, b.max.x), b.max.y + 10f, Random.Range(b.min.z, b.max.z));
                Fx.LocalOnly = true;
                try { Fx.Light(p, new Color(0.8f, 0.85f, 1f), 150f, 3f, 0.25f); }
                finally { Fx.LocalOnly = false; }
            }
            Sfx.Play("explode", 0.35f, 0.1f);
        }

        public static void Clear()
        {
            if (fxRoot != null) Object.Destroy(fxRoot);
            fxRoot = null;
            FogDensity = 1f;
            flash = 0f;
            if (sun != null) { sun.intensity = sunBase; sun.color = sunColorBase; sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f); }
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.38f, 0.46f);
            Current = WeatherKind.Clear;
        }

        public static string Name(WeatherKind w) => w == WeatherKind.Random ? "Random" : w.ToString();
        public static string Name(TimeOfDay t) => t == TimeOfDay.MapDefault ? "Map default" : t.ToString();
    }
}
