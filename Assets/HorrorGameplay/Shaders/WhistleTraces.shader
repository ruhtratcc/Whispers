Shader "Whispers/Whistle Traces"
{
 Properties{_SenseColor("Colour",Color)=(.8,.9,.8,1) _SenseAlpha("Opacity",Float)=0}
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent"}
 Pass{Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float4 _SenseColor;float _SenseAlpha;struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;};V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);return o;}half4 frag(V i):SV_Target{return half4(_SenseColor.rgb,_SenseAlpha);}
 ENDHLSL }
 }
}
