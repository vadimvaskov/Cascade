Shader "Cascade/Sunset/Sky" {
 Properties { _Top("Zenith",Color)=(.10,.15,.38,1) _Horizon("Horizon",Color)=(1,.59,.42,1) _SunDirection("Sun direction",Vector)=(.42,.17,1,0) }
 SubShader { Tags {"Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline"} Cull Off ZWrite Off
 Pass { HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _Top,_Horizon,_SunDirection;
 struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 d:TEXCOORD0;};
 V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.d=i.p.xyz;return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 float fbm(float2 p){return .55*noise(p)+.27*noise(p*2.04)+.13*noise(p*4.11)+.05*noise(p*8.3);}
 half4 frag(V i):SV_Target{float3 d=normalize(i.d);float h=saturate(d.y);float3 col=lerp(_Horizon.rgb,float3(.15,.095,.29),smoothstep(-.03,.24,d.y));col=lerp(col,_Top.rgb,smoothstep(.14,.66,d.y));col=lerp(float3(.13,.095,.29),col,smoothstep(-.24,.03,d.y));float sun=dot(d,normalize(_SunDirection.xyz));col+=float3(1,.43,.14)*pow(saturate(sun),24)*.3;col=lerp(col,float3(5,2.5,.8),smoothstep(.9991,.9995,sun));
 float2 uv=d.xz/(max(.07,abs(d.y))+.12)*1.9;float n=fbm(uv+float2(3,7));float cloud=smoothstep(.47,.74,n)*smoothstep(.01,.11,d.y)*(1-smoothstep(.5,.85,d.y));float3 cc=lerp(float3(.21,.14,.33),float3(.8,.42,.38),saturate(n*1.6-.3));col=lerp(col,cc,cloud*.72);float star=step(.9988,hash(floor(d.xz/(h+.3)*340)))*pow(saturate(h*1.6),3);col+=star*.5;return half4(col,1);}
 ENDHLSL } } }
