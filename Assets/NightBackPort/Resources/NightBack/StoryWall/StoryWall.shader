Shader "NightBack/StoryWall"
{
 Properties {
  _Plaster ("Midnight limewash",Color)=(.102,.144,.195,1)
  _PlasterTex ("Shared fine plaster",2D)="white" {}
  _PlasterNormal ("Shared raw plaster normal",2D)="bump" {}
  _Ink ("Warm ivory face",Color)=(.85,.77,.61,1)
  _Side ("Warm ochre sides",Color)=(.40,.285,.155,1)
  _RevealTime ("Story time",Float)=-1
  _Letters ("Letter surface",Float)=0
 }
 SubShader {
  Tags { "RenderType"="Opaque" "Queue"="Geometry" "DisableBatching"="True" }
  // A shallow contact shadow stays attached to the same wall during the reveal.
  // The following pass draws the closed solid above it; thickness is real geometry.
  Pass {
   Blend SrcAlpha OneMinusSrcAlpha
   Cull Back ZWrite Off
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   float _RevealTime,_Letters;
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct v2f {float4 pos:SV_POSITION;float amount:TEXCOORD0;};
   v2f vert(appdata v){
    v2f o;o.amount=smoothstep(0,.24,_RevealTime-v.uv.x)*_Letters;
    float relief=max(0,-v.vertex.z)*o.amount;
    v.vertex.xy+=float2(.32,-.46)*relief;
    v.vertex.z=-.00045;
    o.pos=UnityObjectToClipPos(v.vertex);return o;
   }
   fixed4 frag(v2f i):SV_Target{clip(i.amount-.001);return fixed4(.015,.020,.028,.32*i.amount);}
   ENDCG
  }
  Pass {
   Cull Back ZWrite On
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   sampler2D _PlasterTex,_PlasterNormal;
   fixed4 _Plaster,_Ink,_Side;float _RevealTime,_Letters;
   struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
   struct v2f {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1;float reveal:TEXCOORD2;float role:TEXCOORD3;float3 worldNormal:TEXCOORD4;};
   v2f vert(appdata v){
    v2f o;float amount=1;
    if(_Letters>.5){amount=smoothstep(0,.24,_RevealTime-v.uv.x);v.vertex.z=lerp(-.0006,v.vertex.z,amount);}
    o.pos=UnityObjectToClipPos(v.vertex);o.normal=v.normal;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
    o.worldNormal=UnityObjectToWorldNormal(v.normal);o.reveal=amount;o.role=v.uv.y;return o;
   }
   fixed4 frag(v2f i):SV_Target {
    float3 n=normalize(i.normal);
    float3 wn=normalize(i.worldNormal);
    float2 plane=abs(wn.x)>.7?float2(-i.world.z*sign(wn.x),i.world.y):float2(i.world.x*sign(wn.z),i.world.y);
    float grain=tex2D(_PlasterTex,plane*6.0).r;
    float cloud=tex2D(_PlasterTex,plane*.31+float2(.27,.41)).r;
    float3 bump=tex2D(_PlasterNormal,plane*6.0).xyz*2-1;
    // Low contrast trowel variation and fine grain, at a consistent world scale.
    float textureShade=1+(grain-.82)*.70+(cloud-.82)*.22+dot(bump.xy,float2(-.10,.15));
    float heightShade=lerp(.83,1.06,smoothstep(.22,2.4,i.world.y));
    fixed3 plaster=_Plaster.rgb*textureShade*heightShade;
    if(_Letters<.5)return fixed4(plaster,1);
    clip(i.reveal-.002);
    float face=smoothstep(.25,.92,-n.z);
    float edgeLight=saturate(dot(n,normalize(float3(-.4,.65,-.6))));
    fixed3 pigment=lerp(_Side.rgb*(.62+.62*edgeLight),_Ink.rgb*(.90+.13*edgeLight),face);
    pigment*=lerp(.79,1,saturate(i.role));
    pigment+=pow(edgeLight,8)*.035*(1-face*.6);
    return fixed4(lerp(plaster,pigment,i.reveal),1);
   }
   ENDCG
  }
 }
}
