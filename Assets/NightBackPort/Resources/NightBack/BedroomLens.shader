Shader "NightBack/BedroomLens"
{
 Properties { _MainTex("Scene",2D)="white"{} _Strength("Strength",Range(0,1))=1 }
 SubShader { Cull Off ZWrite Off ZTest Always
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex; float4 _MainTex_TexelSize;
 UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
 float _Strength;
 fixed4 frag(v2f_img i):SV_Target
 {
   float depth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.uv));
   float blur=smoothstep(1.9,3.7,depth)*_Strength;
   float2 d=_MainTex_TexelSize.xy*blur*1.65;
   float3 color=tex2D(_MainTex,i.uv).rgb*.40;
   color+=(tex2D(_MainTex,i.uv+d).rgb+tex2D(_MainTex,i.uv-d).rgb+tex2D(_MainTex,i.uv+float2(d.x,-d.y)).rgb+tex2D(_MainTex,i.uv+float2(-d.x,d.y)).rgb)*.15;
   float2 p=i.uv-.5;
   float vignette=1-_Strength*.22*smoothstep(.13,.59,length(p));
   color*=vignette;
   return fixed4(color,1);
 }
 ENDCG } }
}
