// Additive HDR glow for halos, sparks, flashes and energy effects (feeds the bloom).
Shader "Cubed/Glow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 1
        _Soft ("Edge softness (spheres)", Range(0, 4)) = 1.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Color;
            half _Intensity;
            half _Soft;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 v : TEXCOORD2; };

            v2f vert(appdata i)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(i.vertex);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                o.n = UnityObjectToWorldNormal(i.normal);
                o.v = normalize(_WorldSpaceCameraPos - mul(unity_ObjectToWorld, i.vertex).xyz);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half facing = abs(dot(normalize(i.n), normalize(i.v)));
                half edge = _Soft > 0.001 ? pow(facing, _Soft) : 1.0;  // fade sphere edges for a soft glow
                half4 t = tex2D(_MainTex, i.uv);
                return half4(_Color.rgb * _Intensity * t.rgb, saturate(_Color.a * t.a * edge));
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
