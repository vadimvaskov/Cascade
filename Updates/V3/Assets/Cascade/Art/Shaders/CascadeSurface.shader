Shader "Cascade/VelvetEnamel"
{
    Properties
    {
        _BaseColor ("Enamel colour", Color) = (.022,.004,.047,1)
        _EdgeColor ("Edge sheen", Color) = (.12,.025,.18,1)
        _Glow ("Edge light", Range(0,2)) = .3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor,_EdgeColor;
                float _Glow;
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
                float edge=pow(1-saturate(abs(dot(n,v))),3);
                float studio=saturate(dot(n,normalize(float3(-.5,.7,-.8))));
                // Keep the cavity dark, with one broad reflection across the bevel
                // rather than a bright outline on every individual triangle.
                float shade=saturate(i.color.r);
                float3 c=_BaseColor.rgb*(.55+studio*.65)*i.color.rgb;
                float sheen=(edge*.36+studio*.035)*(.28+studio*.72)*(.3+_Glow);
                c+=_EdgeColor.rgb*sheen*lerp(.45,1,shade);
                return half4(c,1);
            }
            ENDHLSL
        }
    }
}
