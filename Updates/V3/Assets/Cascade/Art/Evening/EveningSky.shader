Shader "Cascade/Evening/Sky"
{
 Properties { _Top("Blue dusk",Color)=(.08,.11,.28,1) _Horizon("Lavender sunset",Color)=(.53,.32,.49,1) }
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
  float3 d=normalize(i.d);float h=d.y;
  float3 col=lerp(_Horizon.rgb,_Top.rgb,smoothstep(-.04,.22,h));
  col=lerp(float3(.28,.23,.48),col,smoothstep(-.30,.02,h));
  float sun=dot(d,normalize(float3(.48,.035,1)));
  col+=float3(.65,.27,.15)*pow(saturate(sun),26)*.28;
  col=lerp(col,float3(1.5,.85,.55),smoothstep(.99915,.9995,sun)*.8);
  float2 p=d.xz/(max(.05,abs(h))+.16)*1.2;float n=fbm(p+float2(1.2,5.5));
  // Sparse peach wisps leave most of the evening sky clear. No dark storm layer.
  float cloud=smoothstep(.64,.82,n)*smoothstep(.015,.12,h)*(1-smoothstep(.4,.7,h));
  float3 cc=lerp(float3(.73,.56,.76),float3(1.08,.80,.75),smoothstep(.60,.82,n));
  col=lerp(col,cc,cloud*.42);return half4(col,1);
 }
 ENDHLSL } }
}
