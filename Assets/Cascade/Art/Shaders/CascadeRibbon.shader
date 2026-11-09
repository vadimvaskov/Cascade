Shader "Cascade/LuminousRibbon"
{
 Properties { _Gradient("Gradient",Range(0,1))=0 _Gain("Gain",Float)=1 }
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
 Pass {Blend SrcAlpha One ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial) float _Gradient;float _Gain; CBUFFER_END
 float _CascadeSongTime; float _CascadeLight;
 struct A{float4 positionOS:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};struct V{float4 positionCS:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
 V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.color=i.color;o.uv=i.uv;return o;}
 half4 frag(V i):SV_Target {float edge=pow(saturate(1-abs(i.uv.y*2-1)),.7);float wave=.5+.5*sin(i.uv.x*22-_CascadeSongTime*.35);float3 gold=float3(1,.48,.12),violet=float3(.7,.1,1);float3 tint=lerp(i.color.rgb,lerp(gold,violet,pow(wave,2)),_Gradient);return half4(tint*_Gain*(1+_CascadeLight*.6),edge*i.color.a);}
 ENDHLSL
 }}
}
