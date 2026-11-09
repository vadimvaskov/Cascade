Shader "Cascade/SoftGlow"
{
 Properties { _BaseColor("Color",Color)=(.5,.05,1,1) }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
 Pass {Blend SrcAlpha One ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial) float4 _BaseColor; CBUFFER_END
 struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color;return o;}
 half4 frag(V i):SV_Target {float d=length((i.uv-.5)*2);float a=pow(saturate(1-d),2.8);return half4(_BaseColor.rgb*i.color.rgb,a*_BaseColor.a*i.color.a);}
 ENDHLSL
 }}
}
