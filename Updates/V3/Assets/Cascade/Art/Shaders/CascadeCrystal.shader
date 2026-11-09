Shader "Cascade/FacetedCrystal"
{
    Properties
    {
        _BaseColor ("Crystal tint",Color) = (.56,.055,.85,1)
        _Glow ("Crystal brilliance",Range(0,3)) = 1
        _Pulse ("Musical pulse",Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Glow,_Pulse;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float3 positionWS:TEXCOORD1;float4 color:COLOR; };
            Varyings vert(Attributes i)
            {
                Varyings o;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.color=i.color;return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float3 n=normalize(i.normalWS),v=GetWorldSpaceNormalizeViewDir(i.positionWS);
                float3 nv=TransformWorldToViewDir(n,true);
                float3 light=normalize(float3(-.55,.75,.55));
                float front=saturate(dot(n,v));
                float face=saturate(dot(nv,light));
                float edge=pow(1-front,2.8);
                float3 tint=max(_BaseColor.rgb,float3(.001,.001,.001));
                // Facet normals are intentionally not smoothed by the mesh.
                float3 c=tint*(.10+face*.48)*i.color.rgb;
                c+=tint*edge*(.5+.35*_Glow);
                float highlight=pow(saturate(dot(nv,normalize(float3(-.22,.62,.75)))),32);
                c+=lerp(tint,float3(.82,.65,1),.35)*highlight*2.0;
                float blueFace=pow(saturate(dot(nv,normalize(float3(.65,-.15,.65)))),10);
                c+=float3(.12,.055,.42)*blueFace*.7;
                c*=.9+_Glow*.2+_Pulse*.15;
                return half4(c,1);
            }
            ENDHLSL
        }
    }
}
