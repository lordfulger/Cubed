using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cubed
{
    /// <summary>Per-machine preferences (video, accessibility, feel). Match rules live in MatchSettings.</summary>
    [Serializable]
    public class Options
    {
        const string PrefsKey = "cubed.options.v1";

        // Video
        public int resolutionIndex = -1;          // -1 = desktop resolution
        public int windowMode = 0;                // 0 borderless fullscreen, 1 exclusive fullscreen, 2 windowed
        public bool vsync = true;
        public int fpsCap = 144;                  // 0 = unlimited
        public int quality = 2;                   // 0 low, 1 medium, 2 high, 3 ultra
        public float fovOffset;
        public bool ambientParticles = true;

        // Feel & accessibility
        public float uiScale = 1f;
        public float screenShake = 1f;
        public bool reduceFlashes;
        public int colorPalette;                  // 0 default, 1 colorblind-safe, 2 high contrast
        public bool damageNumbers = true;
        public float rumble = 1f;
        public bool holdToFire = true;

        static Options current;
        public static Options Current => current ?? (current = Load());

        public static readonly string[] QualityNames = { "Low", "Medium", "High", "Ultra" };
        public static readonly string[] WindowModes = { "Borderless", "Fullscreen", "Windowed" };
        public static readonly string[] PaletteNames = { "Default", "Colorblind-safe", "High contrast" };

        public void Save()
        {
            try { PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(this)); PlayerPrefs.Save(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static Options Load()
        {
            try
            {
                if (PlayerPrefs.HasKey(PrefsKey))
                {
                    var o = JsonUtility.FromJson<Options>(PlayerPrefs.GetString(PrefsKey));
                    if (o != null) return o;
                }
            }
            catch (Exception e) { Debug.LogException(e); }
            return new Options();
        }

        public static Resolution[] Resolutions
        {
            get
            {
                var list = new List<Resolution>();
                foreach (var r in Screen.resolutions)
                    if (!list.Exists(x => x.width == r.width && x.height == r.height)) list.Add(r);
                return list.ToArray();
            }
        }

        /// <summary>Apply everything that has an immediate engine-side effect.</summary>
        public void Apply(bool applyDisplay)
        {
            QualitySettings.vSyncCount = vsync ? 1 : 0;
            Application.targetFrameRate = vsync ? -1 : fpsCap <= 0 ? -1 : fpsCap;
            switch (quality)
            {
                case 0:
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.antiAliasing = 0;
                    QualitySettings.pixelLightCount = 1;
                    break;
                case 1:
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowResolution = ShadowResolution.Medium;
                    QualitySettings.shadowCascades = 1;
                    QualitySettings.antiAliasing = 2;
                    QualitySettings.pixelLightCount = 3;
                    break;
                case 2:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.High;
                    QualitySettings.shadowCascades = 2;
                    QualitySettings.antiAliasing = 4;
                    QualitySettings.pixelLightCount = 6;
                    break;
                default:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
                    QualitySettings.shadowCascades = 4;
                    QualitySettings.antiAliasing = 8;
                    QualitySettings.pixelLightCount = 10;
                    break;
            }
            QualitySettings.shadowDistance = quality >= 2 ? 90f : 50f;
            Teams.SetPalette(colorPalette);
            Rumble.Strength = rumble;

            if (!applyDisplay) return;
            var mode = windowMode == 0 ? FullScreenMode.FullScreenWindow : windowMode == 1 ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
            var res = Resolutions;
            if (resolutionIndex >= 0 && resolutionIndex < res.Length)
                Screen.SetResolution(res[resolutionIndex].width, res[resolutionIndex].height, mode);
            else
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, mode);
        }
    }

    /// <summary>Small stacked notifications (controller hot-plug, achievements, mod warnings).</summary>
    public static class Toasts
    {
        struct Toast { public string Title, Body; public Color Color; public float Time; }
        static readonly List<Toast> toasts = new List<Toast>();

        public static void Show(string title, string body = "", Color? color = null)
        {
            toasts.Add(new Toast { Title = title, Body = body ?? "", Color = color ?? UIKit.Accent, Time = UnityEngine.Time.unscaledTime });
            if (toasts.Count > 5) toasts.RemoveAt(0);
            Sfx.Play("ui", 0.5f, 0f);
        }

        public static void Draw()
        {
            float now = UnityEngine.Time.unscaledTime;
            float y = UIKit.Size.y - 110f;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                float age = now - t.Time;
                if (age > 5f) { toasts.RemoveAt(i); continue; }
                float slide = Mathf.Pow(1f - Mathf.Clamp01(age * 4f), 3f) * 340f + Mathf.Pow(Mathf.Clamp01(age - 4.5f) * 2f, 2f) * 340f;
                var r = new Rect(UIKit.Size.x - 360f + slide, y - 64f, 340f, 60f);
                UIKit.Fill(r, new Color(0.06f, 0.07f, 0.1f, 0.92f));
                UIKit.Fill(new Rect(r.x, r.y, 5f, r.height), t.Color);
                UIKit.Text(new Rect(r.x + 14f, r.y + 6f, r.width - 20f, 24f), "<b>" + t.Title + "</b>", UIKit.Label, Color.white);
                UIKit.Text(new Rect(r.x + 14f, r.y + 32f, r.width - 20f, 22f), t.Body, UIKit.Small, UIKit.Muted);
                y -= 70f;
            }
        }
    }
}
