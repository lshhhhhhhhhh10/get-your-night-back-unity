Shader "NightBack/Bedroom Baked" {
    Properties {
        _MainTex ("Blender baked color and light", 2D) = "white" {}
        _Color ("Time of night", Color) = (1,1,1,1)
        _EmissionColor ("Interaction highlight", Color) = (0,0,0,0)
    }
    SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass {
            Cull Back ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color, _EmissionColor;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct Output { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            Output vert(Input v) { Output o; o.pos=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);return o; }
            fixed4 frag(Output i):SV_Target { fixed4 col=tex2D(_MainTex,i.uv);return fixed4(col.rgb*_Color.rgb+_EmissionColor.rgb,1); }
            ENDCG
        }
    }
}
