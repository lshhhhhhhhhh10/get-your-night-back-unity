Shader "NightBack/NativeSurface"
{
    Properties
    {
        _Color ("Source color", Color) = (1,1,1,1)
        _MainTex ("Source texture", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = .2
        _Metallic ("Metallic", Range(0,1)) = 0
        _EmissionColor ("Emission", Color) = (0,0,0,1)
        _UseVertexColor ("Source vertex colors", Float) = 0
        _BumpMap ("Linear raw normal", 2D) = "bump" {}
        _BumpScale ("Normal scale", Float) = 1
        _UseNormal ("Use raw normal", Float) = 0
        _Cutoff ("Cutout threshold", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Source face side", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull [_Cull]
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _MainTex, _BumpMap;
        fixed4 _Color, _EmissionColor;
        half _Glossiness, _Metallic, _UseVertexColor, _BumpScale, _UseNormal, _Cutoff;
        struct Input { float2 uv_MainTex; float2 uv_BumpMap; fixed4 color : COLOR; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color * lerp(fixed4(1,1,1,1), IN.color, _UseVertexColor);
            clip(c.a - _Cutoff);
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = _EmissionColor.rgb;
            o.Alpha = c.a;
            if (_UseNormal > .5)
            {
                float3 n = tex2D(_BumpMap, IN.uv_BumpMap).xyz * 2 - 1;
                // Native meshes invert V from glTF's top-left UV origin.
                n.y = -n.y;
                n.xy *= _BumpScale;
                o.Normal = normalize(n);
            }
        }
        ENDCG
    }
    FallBack "Diffuse"
}
