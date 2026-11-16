// Spheres post-processing (built-in render pipeline): bloom, ambient occlusion, distance fog,
// filmic tonemapping, color grading, vignette and chromatic aberration.
Shader "Hidden/Spheres/Post"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    sampler2D _AOTex;
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

    float4 _BloomParams;   // x threshold, y knee, z intensity, w unused
    float4 _GradeParams;   // x exposure, y contrast, z saturation, w vignette strength
    float4 _FogParams;     // x start, y end, z density, w height falloff
    float4 _FogColor;
    float4 _AOParams;      // x radius (world), y intensity, z sample count, w max distance
    float4 _Tint;          // rgb tint (gain), a chromatic aberration amount
    float4 _Lift;          // rgb lift (shadows)

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    half3 SafeHDR(half3 c) { return min(c, 65000.0); }

    // Soft-knee threshold so only bright pixels feed the bloom.
    half3 Prefilter(half3 c)
    {
        half br = max(c.r, max(c.g, c.b));
        half knee = _BloomParams.y;
        half soft = clamp(br - _BloomParams.x + knee, 0.0, 2.0 * knee);
        soft = soft * soft / (4.0 * knee + 1e-4);
        half contrib = max(soft, br - _BloomParams.x) / max(br, 1e-4);
        return c * contrib;
    }

    half3 Box4(float2 uv, float2 texel, float spread)
    {
        float4 o = texel.xyxy * float4(-1.0, -1.0, 1.0, 1.0) * spread;
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb
                + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25;
    }

    half4 fragPrefilter(v2f i) : SV_Target
    {
        half3 c = Box4(i.uv, _MainTex_TexelSize.xy, 1.0);
        return half4(Prefilter(SafeHDR(c)), 1.0);
    }

    half4 fragDown(v2f i) : SV_Target
    {
        return half4(Box4(i.uv, _MainTex_TexelSize.xy, 1.0), 1.0);
    }

    half4 fragUp(v2f i) : SV_Target
    {
        return half4(Box4(i.uv, _MainTex_TexelSize.xy, 0.5), 1.0);
    }

    float LinearEyeDepthAt(float2 uv)
    {
        // Explicit LOD: this is sampled inside a dynamic loop (no gradients allowed there).
        return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(uv, 0.0, 0.0)));
    }

    // Normal-free screen-space AO tuned for blocky geometry: occlusion from nearby pixels that are
    // closer to the camera within a world-space range.
    half4 fragAO(v2f i) : SV_Target
    {
        float d = LinearEyeDepthAt(i.uv);
        if (d > _AOParams.w) return half4(1, 1, 1, 1);
        float noise = frac(52.9829189 * frac(dot(i.uv * _ScreenParams.xy, float2(0.06711056, 0.00583715))));
        float radiusUV = _AOParams.x / max(d, 0.5) * 0.6;
        int count = (int)_AOParams.z;
        float occlusion = 0.0;
        [loop]
        for (int k = 0; k < 16; k++)
        {
            if (k >= count) break;
            float t = (k + noise) / count;
            float angle = t * 18.85 + noise * 6.283;
            float2 offset = float2(cos(angle), sin(angle)) * radiusUV * (0.25 + 0.75 * t);
            offset.y *= _ScreenParams.x / _ScreenParams.y;
            float sd = LinearEyeDepthAt(i.uv + offset);
            float diff = d - sd;
            occlusion += saturate(diff / _AOParams.x) * saturate(1.0 - (diff - _AOParams.x) / (_AOParams.x * 2.0)) * (diff > 0.02 ? 1.0 : 0.0);
        }
        float ao = 1.0 - occlusion / max(count, 1);
        return half4(ao, ao, ao, 1);
    }

    half4 fragBlurAO(v2f i) : SV_Target
    {
        float2 t = _MainTex_TexelSize.xy;
        half s = tex2D(_MainTex, i.uv).r * 0.4;
        s += tex2D(_MainTex, i.uv + float2(t.x, t.y) * 1.5).r * 0.15;
        s += tex2D(_MainTex, i.uv + float2(-t.x, t.y) * 1.5).r * 0.15;
        s += tex2D(_MainTex, i.uv + float2(t.x, -t.y) * 1.5).r * 0.15;
        s += tex2D(_MainTex, i.uv + float2(-t.x, -t.y) * 1.5).r * 0.15;
        return half4(s, s, s, 1);
    }

    // Narkowicz ACES fit.
    half3 ACES(half3 x)
    {
        return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
    }

    half4 fragComposite(v2f i) : SV_Target
    {
        float2 uv = i.uv;
        half3 c;
        if (_Tint.a > 0.0)
        {
            float2 dir = (uv - 0.5) * _Tint.a;
            c.r = tex2D(_MainTex, uv - dir).r;
            c.g = tex2D(_MainTex, uv).g;
            c.b = tex2D(_MainTex, uv + dir).b;
        }
        else c = tex2D(_MainTex, uv).rgb;
        c = SafeHDR(c);
        #if defined(UNITY_COLORSPACE_GAMMA)
        c = GammaToLinearSpace(c);
        #endif

        #if defined(SPHERES_AO)
        half ao = tex2D(_AOTex, uv).r;
        c *= lerp(1.0, ao, _AOParams.y);
        #endif

        #if defined(SPHERES_FOG)
        float rawDepth = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(uv, 0.0, 0.0));
        float dist = LinearEyeDepth(rawDepth);
        #if defined(UNITY_REVERSED_Z)
        bool sky = rawDepth <= 0.0001;
        #else
        bool sky = rawDepth >= 0.9999;
        #endif
        if (!sky)
        {
            float f = saturate((dist - _FogParams.x) / max(_FogParams.y - _FogParams.x, 0.01)) * _FogParams.z;
            c = lerp(c, _FogColor.rgb, f);
        }
        #endif

        c += tex2D(_BloomTex, uv).rgb * _BloomParams.z;

        // Grade in linear HDR, then tonemap.
        c *= _GradeParams.x;
        c = c * _Tint.rgb + _Lift.rgb * (1.0 - saturate(c));
        c = ACES(c);
        half luma = dot(c, half3(0.2126, 0.7152, 0.0722));
        c = lerp(luma.xxx, c, _GradeParams.z);
        c = saturate((c - 0.5) * _GradeParams.y + 0.5);

        float2 v = (uv - 0.5) * float2(1.0, 0.85);
        c *= 1.0 - _GradeParams.w * smoothstep(0.25, 0.85, dot(v, v) * 2.2);

        #if !defined(UNITY_COLORSPACE_GAMMA)
        return half4(c, 1.0);
        #else
        return half4(LinearToGammaSpace(c), 1.0);
        #endif
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass // 0 prefilter
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            ENDCG
        }
        Pass // 1 downsample
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDown
            ENDCG
        }
        Pass // 2 upsample (additive onto the larger mip)
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragUp
            ENDCG
        }
        Pass // 3 composite
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            #pragma multi_compile __ SPHERES_AO
            #pragma multi_compile __ SPHERES_FOG
            #pragma target 3.0
            ENDCG
        }
        Pass // 4 ambient occlusion
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragAO
            #pragma target 3.0
            ENDCG
        }
        Pass // 5 AO blur
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlurAO
            ENDCG
        }
    }
    Fallback Off
}
