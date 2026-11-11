Shader "Cascade/Sunset/Waterfall" {
 Properties { _Tint("Tint",Color)=(.25,.7,1,1) _Speed("Flow speed",Float)=1 }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 Pass { HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _Tint;float _Speed,_SunsetTime;
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float fog:TEXCOORD1;};
 V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.fog=ComputeFogFactor(o.p.z);return o;}
 half4 frag(V i):SV_Target{float u=i.uv.x,v=i.uv.y;float s=sin(u*112+sin(v*5+_SunsetTime*1.1)*.3)*.5+.5;float bands=pow(s,9);float flow=.5+.5*sin(v*19+_SunsetTime*_Speed*9+sin(u*147)*3);float rim=smoothstep(0,.17,u)*smoothstep(0,.17,1-u);float bottom=smoothstep(0,.3,v);float3 col=lerp(_Tint.rgb*.6,float3(.65,.82,1),bands*.7+flow*.15);col=MixFog(col,i.fog);return half4(col, rim*bottom*(.36+.35*bands));}
 ENDHLSL } } }
