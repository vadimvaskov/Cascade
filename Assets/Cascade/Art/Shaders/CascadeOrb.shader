Shader "Cascade/MusicalGlass"
{
    Properties
    {
        _BaseColor ("Glass tint", Color) = (0.015, 0.48, 1, 1)
        _Glow ("Luminous intensity", Range(0,3)) = 1.0
        _Charge ("Faceting: charge", Range(1,4)) = 1
        _Pulse ("Musical pulse", Range(0,1)) = 0
        _Reveal ("Appearance", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "MusicalGlass"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Glow, _Charge, _Pulse, _Reveal;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v,o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            float gaussian(float2 p,float2 centre,float2 radius)
            {
                float2 d=(p-centre)/radius;
                return exp(-dot(d,d)*2.0);
            }
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n=normalize(i.normalWS);
                float3 v=GetWorldSpaceNormalizeViewDir(i.positionWS);
                float facing=saturate(dot(n,v));
                float3 nv=TransformWorldToViewDir(n,true);
                float2 p=nv.xy;
                float r=length(p);
                float angle=atan2(p.y,p.x);
                float3 tint=max(_BaseColor.rgb,float3(.001,.001,.001));
                // The centre is optically dark. Most visible energy belongs to
                // the shell and reflected arcs, rather than the whole surface.
                float fresnel=pow(1.0-facing,4.5);
                float pixel=max(.008,fwidth(r)*.7);
                float rimRadius=.963+.004*sin(angle*5.0+.8)+.002*sin(angle*11.0);
                float rim=exp(-pow((r-rimRadius)/max(.013,pixel),2.0));
                float rimEnergy=.24+.76*pow(.5+.5*sin(angle*2.0+.45),2.0);
                float3 c=tint*(.021+.068*pow(1.0-facing,1.1));
                c+=tint*(fresnel*.55+rim*rimEnergy*2.5);
                float innerShell=exp(-pow((r-.884)/max(.016,pixel),2.0));
                c+=tint*innerShell*(.08+.2*pow(saturate(-p.y),2));

                // Two offset curved internal reflections suggest a thick glass
                // shell. They do not use time or repetitive water-like ripples.
                float d1=length((p-float2(-.035,-.065))*float2(1.06,1.0));
                float reflected1=exp(-pow((d1-.79)/.026,2.0));
                float lowerGate=smoothstep(-.15,.55,-p.y)*(1-smoothstep(.94,1.0,r));
                float d2=length((p-float2(.16,.065))*float2(1.1,.93));
                float reflected2=exp(-pow((d2-.73)/.020,2.0));
                float sideGate=smoothstep(.0,.55,p.x)*smoothstep(.43,.64,r);
                c+=tint*(reflected1*lowerGate*.9+reflected2*sideGate*.65);
                float mist=gaussian(p,float2(.12,-.55),float2(.48,.20));
                c+=tint*mist*.095;

                // A curved studio strip on the upper left, with a softly graded
                // shoulder and just one small crisp source inside that strip.
                float2 direction=normalize(p+float2(.0001,.0002));
                float arcGate=pow(saturate(dot(direction,normalize(float2(-.53,.85)))),7.0);
                float stripRadius=length((p-float2(.035,-.045))*float2(1.02,.97));
                float strip=exp(-pow((stripRadius-.86)/.023,2.0))*arcGate;
                float stripSoft=exp(-pow((stripRadius-.86)/.075,2.0))*arcGate;
                float hotspot=gaussian(p,float2(-.39,.66),float2(.12,.055));
                float companion=gaussian(p,float2(.37,.51),float2(.065,.035));
                float glint=gaussian(p,float2(-.65,-.54),float2(.035,.085));
                c+=lerp(tint,float3(.84,.96,1),.9)*(strip*2.7+stripSoft*.34);
                c+=float3(.94,.99,1)*(hotspot*1.6+companion*.55+glint*1.2);
                float oppositeGate=pow(saturate(dot(direction,normalize(float2(.62,-.78)))),15);
                c+=lerp(tint,float3(.75,.88,1),.35)*rim*oppositeGate*.75;

                // Angular shell reflections make the green and amber charges
                // distinct while their dark inner volume remains quiet.
                float faceting=saturate((_Charge-1)*.42);
                float facet=pow(saturate(cos(angle*(_Charge>3.5?10.0:8.0)+.5)),18.0);
                c+=tint*facet*innerShell*faceting*.5;
                c*=.9+_Glow*.16+_Pulse*.10;
                c+=tint*rim*_Pulse*.32;
                c*=lerp(.5,1,_Reveal);
                return half4(c,1);
            }
            ENDHLSL
        }
    }
}
