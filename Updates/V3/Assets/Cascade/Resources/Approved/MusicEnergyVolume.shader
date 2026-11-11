Shader "Cascade/Music energy volume"
{
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent"}
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   Blend SrcAlpha One
   ZWrite Off
   ZTest LEqual
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A {float4 p:POSITION;float3 n:NORMAL;half4 color:COLOR;};
   struct V {float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 world:TEXCOORD1;half4 color:COLOR;};
   V vert(A i){V o;o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.n=TransformObjectToWorldNormal(i.n);o.color=i.color;return o;}
   half4 frag(V i):SV_Target
   {
    half facing=saturate(abs(dot(normalize(i.n),GetWorldSpaceNormalizeViewDir(i.world))));
    // Soft silhouettes expose depth and the nested filaments, instead of a flat solid beam.
    return half4(i.color.rgb*2.4h,i.color.a*pow(facing,1.4h));
   }
   ENDHLSL
  }
 }
}
