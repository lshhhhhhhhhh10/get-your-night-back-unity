Shader "NightBack/TitleVolume"
{
 Properties { _Face ("Cream face", Color) = (0.957,0.867,0.722,1) _Side ("Warm brown sides", Color) = (0.40,0.33,0.25,1) }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Cull Back ZWrite On Blend SrcAlpha OneMinusSrcAlpha
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   fixed4 _Face, _Side;
   struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
   struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float3 localNormal:TEXCOORD1; float eyeDepth:TEXCOORD2; };
   v2f vert(appdata v) { v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.localNormal=v.normal;o.eyeDepth=-UnityObjectToViewPos(v.vertex).z;return o; }
   fixed4 frag(v2f i):SV_Target
   {
    float3 n=normalize(i.normal);float face=smoothstep(.55,.98,-normalize(i.localNormal).z);
    float key=saturate(dot(n,normalize(float3(-.35,.8,.55))));
    fixed3 side=_Side.rgb*(.65+.50*key);
    fixed3 front=_Face.rgb*(.95+.05*key);
    // World-space geometry stays fixed; lighting changes across its real bevels and side walls.
    fixed3 color=lerp(side,front,face);
    float alpha=smoothstep(_ProjectionParams.y+.01,_ProjectionParams.y+.14,i.eyeDepth);
    clip(alpha-.002);
    return fixed4(color,alpha);
   }
   ENDCG
  }
 }
}
