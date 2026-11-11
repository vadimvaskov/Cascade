Shader "Cascade/Evening/Lantern glass"
{
 Properties { _BaseColor("Warm glass",Color)=(1,.74,.45,1) _Glow("Glow",Range(0,8))=3.8 }
 SubShader {
  Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
  Pass {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor;float _Glow;
   CBUFFER_END
   float _CascadeWorldPulse;
   struct A {float4 p:POSITION;float3 n:NORMAL;};
   struct V {float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 world:TEXCOORD1;float fog:TEXCOORD2;};
   V vert(A i){V o;o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.n=TransformObjectToWorldNormal(i.n);o.fog=ComputeFogFactor(o.p.z);return o;}
   half4 frag(V i):SV_Target {
    float facing=pow(saturate(abs(dot(normalize(i.n),GetWorldSpaceNormalizeViewDir(i.world)))),.6);
    float3 glass=lerp(_BaseColor.rgb,float3(1,.70,.32),facing*.55);
    return half4(MixFog(glass*_Glow*lerp(.65,1,facing)*(1+clamp(_CascadeWorldPulse,0,.5)),i.fog),1);
   }
   ENDHLSL
  }
 }
}
