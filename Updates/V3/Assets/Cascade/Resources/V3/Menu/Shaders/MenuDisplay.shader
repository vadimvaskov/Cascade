Shader "Cascade/Menu sRGB display"
{
 Properties {[PerRendererData] _MainTex("Composited menu",2D)="white"{}}
 SubShader
 {
  Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
  Cull Off Lighting Off ZWrite Off ZTest Always Blend One Zero
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
   struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
   sampler2D _MainTex;
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
   float4 frag(v2f i):SV_Target
   {
    float4 c=tex2D(_MainTex,i.uv);
    #ifndef UNITY_COLORSPACE_GAMMA
    c.rgb=GammaToLinearSpace(c.rgb);
    #endif
    return float4(c.rgb,1);
   }
   ENDCG
  }
 }
}
