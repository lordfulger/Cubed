using UnityEngine;

namespace Cubed
{
    /// <summary>
    /// Image effects for every game camera: HDR bloom, ambient occlusion, distance fog, ACES tonemapping,
    /// color grading, vignette and (Ultra) chromatic aberration. Scales with the Quality option and turns
    /// itself off if the shader isn't supported, so the game never breaks because of it.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CubedPostFX : MonoBehaviour
    {
        static Shader shader;
        static bool shaderChecked, shaderOk;
        Material mat;
        Camera cam;
        readonly RenderTexture[] mips = new RenderTexture[8];

        public static float BloomIntensity = 0.65f;
        public static float Exposure = 1.05f;
        public static float Contrast = 1.07f;
        public static float Saturation = 1.12f;
        public static float Vignette = 0.38f;

        public static void Attach(Camera c)
        {
            if (c == null) return;
            c.allowHDR = true;
            c.allowMSAA = true;
            if (c.GetComponent<CubedPostFX>() == null) c.gameObject.AddComponent<CubedPostFX>();
        }

        static bool ShaderReady()
        {
            if (!shaderChecked)
            {
                shaderChecked = true;
                shader = Shader.Find("Hidden/Cubed/Post");
                shaderOk = shader != null && shader.isSupported;
                if (!shaderOk) Debug.LogWarning("[Cubed] Post-processing shader unavailable; effects disabled.");
            }
            return shaderOk;
        }

        void OnEnable() => cam = GetComponent<Camera>();

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            var o = Options.Current;
            if (!o.postFx || o.quality <= 0 || !ShaderReady())
            {
                Graphics.Blit(src, dst);
                return;
            }
            if (mat == null) mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            bool high = o.quality >= 2;
            bool ultra = o.quality >= 3;
            bool depth = high;
            if (depth) cam.depthTextureMode |= DepthTextureMode.Depth;

            // ---- Bloom: prefilter, downsample chain, additive upsample.
            var fmt = src.format;
            int w = Mathf.Max(1, src.width / 2), h = Mathf.Max(1, src.height / 2);
            int iterations = Mathf.Clamp(Mathf.FloorToInt(Mathf.Log(Mathf.Min(w, h), 2f)) - 2, 1, ultra ? 7 : 5);
            float flash = o.reduceFlashes ? 0.55f : 1f;
            mat.SetVector("_BloomParams", new Vector4(1.0f, 0.5f, BloomIntensity * flash, 0f));
            mips[0] = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(src, mips[0], mat, 0);
            for (int i = 1; i < iterations; i++)
            {
                w = Mathf.Max(1, w / 2);
                h = Mathf.Max(1, h / 2);
                mips[i] = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(mips[i - 1], mips[i], mat, 1);
            }
            for (int i = iterations - 1; i > 0; i--)
            {
                Graphics.Blit(mips[i], mips[i - 1], mat, 2);
                RenderTexture.ReleaseTemporary(mips[i]);
                mips[i] = null;
            }
            mat.SetTexture("_BloomTex", mips[0]);

            // ---- Ambient occlusion (High/Ultra).
            RenderTexture ao = null;
            if (high)
            {
                var aoFmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;
                int aw = ultra ? src.width : src.width / 2, ah = ultra ? src.height : src.height / 2;
                var raw = RenderTexture.GetTemporary(aw, ah, 0, aoFmt);
                mat.SetVector("_AOParams", new Vector4(0.9f, 0.85f, ultra ? 14f : 8f, 90f));
                Graphics.Blit(src, raw, mat, 4);
                ao = RenderTexture.GetTemporary(aw, ah, 0, aoFmt);
                Graphics.Blit(raw, ao, mat, 5);
                RenderTexture.ReleaseTemporary(raw);
                mat.SetTexture("_AOTex", ao);
                mat.EnableKeyword("CUBED_AO");
            }
            else mat.DisableKeyword("CUBED_AO");

            // ---- Distance fog tinted to the map's horizon color.
            var map = GameManager.ActiveMap;
            if (high && map != null)
            {
                float size = Mathf.Max(map.FullBounds.size.x, map.FullBounds.size.z);
                mat.SetVector("_FogParams", new Vector4(size * 0.8f, size * 4.5f, 0.45f, 0f));
                var fc = map.FogColor;
                mat.SetColor("_FogColor", QualitySettings.activeColorSpace == ColorSpace.Linear ? fc.linear : fc);
                mat.EnableKeyword("CUBED_FOG");
            }
            else mat.DisableKeyword("CUBED_FOG");

            // ---- Grade + tonemap.
            mat.SetVector("_GradeParams", new Vector4(Exposure, Contrast, Saturation, Vignette));
            mat.SetVector("_Tint", new Vector4(1.02f, 1f, 0.98f, ultra && !o.reduceFlashes ? 0.0022f : 0f));
            mat.SetVector("_Lift", new Vector4(0.004f, 0.006f, 0.014f, 0f));
            Graphics.Blit(src, dst, mat, 3);

            RenderTexture.ReleaseTemporary(mips[0]);
            mips[0] = null;
            if (ao != null) RenderTexture.ReleaseTemporary(ao);
        }
    }
}
