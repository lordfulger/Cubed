// Energy shield bubble: fresnel rim, scrolling bands and a bright hit flash.
Shader "Spheres/Shield"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.4, 0.7, 1, 0.5)
        _RimPower ("Rim power", Float) = 2.2
        _Intensity ("Intensity", Float) = 2.5
        _Flash ("Hit flash", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _Color;
            half _RimPower, _Intensity, _Flash;

            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; float3 wp : TEXCOORD2; };

            v2f vert(appdata_base i)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(i.vertex);
                o.n = UnityObjectToWorldNormal(i.normal);
                o.wp = mul(unity_ObjectToWorld, i.vertex).xyz;
                o.v = normalize(_WorldSpaceCameraPos - o.wp);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half rim = pow(1.0 - saturate(dot(normalize(i.n), normalize(i.v))), _RimPower);
                half bands = 0.75 + 0.25 * sin(i.wp.y * 16.0 - _Time.y * 8.0);
                half hex = 0.85 + 0.15 * sin(i.wp.x * 9.0 + _Time.y * 2.0) * sin(i.wp.z * 9.0 - _Time.y * 2.0);
                half a = saturate(0.12 + rim * bands * hex + _Flash * 0.6) * _Color.a;
                return half4(_Color.rgb * _Intensity * (1.0 + _Flash * 2.0), a);
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
