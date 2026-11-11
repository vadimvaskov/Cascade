Shader "Cascade/Dream/Water"
{
 Properties { _Tint("Pearlescent aqua",Color)=(.4,.8,.88,1) }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 Pass { HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _Tint;float _DreamTime;
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float fog:TEXCOORD1;};
 V vert(A i){V o;float3 p=i.p.xyz;p.z+=sin(i.uv.y*9+_DreamTime*1.5)*.035;o.p=TransformObjectToHClip(p);o.uv=i.uv;o.fog=ComputeFogFactor(o.p.z);return o;}
 half4 frag(V i):SV_Target{float u=i.uv.x,v=i.uv.y;float ribbons=.5+.5*sin(u*42+sin(v*6+_DreamTime*1.7)*.6);float silk=pow(ribbons,4);float flow=.5+.5*sin(v*32+_DreamTime*8+sin(u*38)*2);float edge=smoothstep(0,.18,u)*smoothstep(0,.18,1-u);float bottom=smoothstep(0,.25,v);float3 col=lerp(_Tint.rgb,float3(.88,.94,1),silk*.6+flow*.12);return half4(MixFog(col,i.fog),edge*bottom*(.55+.35*silk));}
 ENDHLSL } }
}
