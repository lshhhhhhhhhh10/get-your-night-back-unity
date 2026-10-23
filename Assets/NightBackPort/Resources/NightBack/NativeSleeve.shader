Shader "NightBack/NativeSleeve"
{
    Properties { _Color ("Cloth", Color) = (.36,.46,.57,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off
        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert
        #pragma target 3.0
        fixed4 _Color;
        struct Input { float sleeveY; };
        void vert(inout appdata_full v, out Input o) { UNITY_INITIALIZE_OUTPUT(Input, o); o.sleeveY = v.vertex.y; }
        void surf(Input IN, inout SurfaceOutputStandard o)
        { o.Albedo = _Color.rgb; o.Smoothness = .2; o.Metallic = 0; o.Alpha = smoothstep(-.4,.18,IN.sleeveY); }
        ENDCG
    }
    Fallback "Transparent/Diffuse"
}
