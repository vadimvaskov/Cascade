Shader "Cascade/Backdrop"
{
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Background" }
 Pass { ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
 V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
 half4 frag(V i):SV_Target {float2 p=(i.uv-.5)*float2(1,1.8);float halo=exp(-dot(p,p)*5);float lower=exp(-dot(p-float2(0,-.3),p-float2(0,-.3))*18);return half4(lerp(float3(.003,.0005,.010),float3(.013,.001,.031),halo)+lower*float3(.004,0,.011),1);}
 ENDHLSL
 }}
}
