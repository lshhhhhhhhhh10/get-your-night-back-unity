Shader "NightBack/FirstNightWallText"
{
    Properties {
        _MainTex ("Font atlas", 2D) = "white" {}
        _MinX ("Line left", Float) = 0
        _MaxX ("Line right", Float) = 1
        _Reveal ("Writing progress", Range(0,1)) = 0
        _Opacity ("Near-surface visibility", Range(0,1)) = 0
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        Lighting Off ZWrite Off ZTest LEqual Cull Back
        Blend SrcAlpha OneMinusSrcAlpha
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _MinX, _MaxX, _Reveal, _Opacity;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float2 local:TEXCOORD1; fixed4 color:COLOR; };
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.local=v.vertex.xy; o.color=v.color; return o; }
            fixed4 frag(v2f i):SV_Target {
                float x=(i.local.x-_MinX)/max(.0001,_MaxX-_MinX);
                // A moving, slightly uneven wet edge writes through each glyph, then settles.
                float edge=_Reveal*1.12-.06-x+.009*sin(i.local.y*170+x*43);
                float written=smoothstep(-.035,.035,edge)*step(.0001,_Reveal);
                float wet=(1-smoothstep(.02,.13,edge))*written*(1-step(.999,_Reveal));
                fixed4 c=i.color; c.rgb=lerp(c.rgb,float3(1,.94,.78),wet*.65);
                c.a*=tex2D(_MainTex,i.uv).a*written*_Opacity;
                return c;
            }
            ENDCG
        }
    }
}
