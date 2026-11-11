Shader "Cascade/Evening/Velvet foliage"
{
 Properties { _BaseMap("Painted variation",2D)="white"{} _BaseColor("Petal color",Color)=(1,1,1,1) _Cutoff("Cutoff",Range(0,1))=.5 _Cull("Cull",Float)=2 }
 SubShader
 {
  Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile _ _LIGHT_LAYERS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST,_BaseColor;
   CBUFFER_END
   float _DreamTime;
   struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
   struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;float fog:TEXCOORD3;};
   V vert(A i)
   {
    V o;float3 p=i.p.xyz;float weight=saturate((p.y-.6)*.25);
    p.x+=sin(_DreamTime*.85+p.z*.35+p.x*.19)*.055*weight;
    p.z+=cos(_DreamTime*.7+p.x*.3)*.03*weight;
    o.world=TransformObjectToWorld(p);o.p=TransformWorldToHClip(o.world);o.n=TransformObjectToWorldNormal(i.n);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);o.fog=ComputeFogFactor(o.p.z);return o;
   }
   half3 WrappedLight(half3 n,Light light)
   {
    half diffuse=saturate((dot(n,light.direction)+.65)/1.65);
    return light.color*diffuse*light.distanceAttenuation*lerp(.65,1,light.shadowAttenuation);
   }
   half4 frag(V i):SV_Target
   {
    half3 n=normalize(i.n);Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
    half3 painted=lerp(half3(1,1,1),SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb,.45);
    half3 base=painted*_BaseColor.rgb;
    half3 illumination=SampleSH(n)*.8+WrappedLight(n,light);
    #if defined(_ADDITIONAL_LIGHTS)
    uint count=GetAdditionalLightsCount();
    for(uint index=0;index<count;index++)
    {
     Light localLight=GetAdditionalLight(index,i.world);
     #if defined(_LIGHT_LAYERS)
     if(!IsMatchingLightLayer(localLight.layerMask,GetMeshRenderingLayer()))continue;
     #endif
     illumination+=WrappedLight(n,localLight)*.65;
    }
    #endif
    half rim=pow(1-saturate(dot(n,normalize(GetWorldSpaceViewDir(i.world)))),3)*.09;
    half3 color=base*illumination+base*light.color*rim;
    return half4(MixFog(color,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
