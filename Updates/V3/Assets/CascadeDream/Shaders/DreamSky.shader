Shader "Cascade/Dream/Sky"
{
 Properties { _Top("Periwinkle zenith",Color)=(.10,.19,.40,1) _Horizon("Peach horizon",Color)=(.88,.51,.39,1) }
 SubShader { Tags {"Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline"} Cull Off ZWrite Off
 Pass { HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _Top,_Horizon;
 struct A{float4 p:POSITION;}; struct V{float4 p:SV_POSITION;float3 d:TEXCOORD0;};
 V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.d=i.p.xyz;return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 k=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(k),hash(k+float2(1,0)),f.x),lerp(hash(k+float2(0,1)),hash(k+1),f.x),f.y);}
 float fbm(float2 p){return .55*noise(p)+.27*noise(p*2.03)+.13*noise(p*4.08)+.05*noise(p*8.2);}
 half4 frag(V i):SV_Target
 {
  float3 d=normalize(i.d);float h=d.y;float3 col=lerp(_Horizon.rgb,_Top.rgb,smoothstep(-.02,.31,h));
  col=lerp(float3(.55,.48,.69),col,smoothstep(-.35,.03,h));
  float sun=dot(d,normalize(float3(.42,.20,1)));col+=float3(1,.52,.26)*pow(saturate(sun),20)*.36;
  col=lerp(col,float3(3.4,2.1,1.15),smoothstep(.9991,.99945,sun));
  float2 p=d.xz/(max(.05,abs(h))+.16)*1.2;float n=fbm(p+float2(1.2,5.5));
  float cloud=smoothstep(.56,.74,n)*smoothstep(.02,.14,h)*(1-smoothstep(.6,.9,h));
  float3 cc=lerp(float3(.52,.51,.73),float3(1.08,.79,.69),smoothstep(.45,.72,n));
  col=lerp(col,cc,cloud*.4);return half4(col,1);
 }
 ENDHLSL } }
}
