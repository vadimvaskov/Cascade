Shader "Cascade/Sunset/Fa ORM" {
 Properties { _BaseMap("Base color",2D)="white"{} _BaseColor("Tint",Color)=(1,1,1,1) [Normal]_BumpMap("Normal",2D)="bump"{} _ORM("Occlusion Roughness Metallic",2D)="white"{} _BumpScale("Normal strength",Range(0,2))=.65 _Smoothness("Smoothness scale",Range(0,1))=.8 _Cutoff("Alpha cutoff",Range(0,1))=.5 }
 SubShader { Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
 Pass { Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
 #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
 #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);TEXTURE2D(_ORM);SAMPLER(sampler_ORM);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseMap_ST,_BaseColor;float _BumpScale,_Smoothness,_Cutoff;
 CBUFFER_END
 struct A{float4 p:POSITION;float3 n:NORMAL;float4 t:TANGENT;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float4 t:TEXCOORD2;float2 uv:TEXCOORD3;float fog:TEXCOORD4;};
 V vert(A i){V o;VertexPositionInputs v=GetVertexPositionInputs(i.p.xyz);o.p=v.positionCS;o.w=v.positionWS;o.n=TransformObjectToWorldNormal(i.n);o.t=float4(TransformObjectToWorldDir(i.t.xyz),i.t.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(i.uv,_BaseMap);o.fog=ComputeFogFactor(o.p.z);return o;}
 half4 frag(V i):SV_Target{half4 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;half3 orm=SAMPLE_TEXTURE2D(_ORM,sampler_ORM,i.uv).rgb;half3 nt=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);half3 n=normalize(i.n),t=normalize(i.t.xyz),b=cross(n,t)*i.t.w;InputData input=(InputData)0;input.positionWS=i.w;input.normalWS=normalize(t*nt.x+b*nt.y+n*nt.z);input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.w);input.shadowCoord=TransformWorldToShadowCoord(i.w);input.bakedGI=SampleSH(input.normalWS);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);input.shadowMask=half4(1,1,1,1);SurfaceData s=(SurfaceData)0;s.albedo=base.rgb;s.alpha=1;s.metallic=orm.b;s.smoothness=(1-orm.g)*_Smoothness;s.occlusion=lerp(1,orm.r,.75);s.normalTS=nt;s.specular=half3(.04,.04,.04);half4 color=UniversalFragmentPBR(input,s);color.rgb=MixFog(color.rgb,i.fog);return color;}
 ENDHLSL }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 UsePass "Universal Render Pipeline/Lit/DepthNormals"
 } }
