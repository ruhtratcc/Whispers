Shader "Hidden/Whispers/Mirror Surface Mask"
{
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+20" }
  Pass
  {
   Name "MirrorMask"
   Tags { "LightMode"="SRPDefaultUnlit" }
   ColorMask 0
   ZWrite Off
   ZTest LEqual
   Cull Off
   Offset -1,-1
   Stencil { Ref 8 ReadMask 8 WriteMask 8 Comp Always Pass Replace }
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Input { float4 positionOS:POSITION; };
   struct Output { float4 positionCS:SV_POSITION; };
   Output Vert(Input v){Output o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);return o;}
   half4 Frag(Output i):SV_Target{return 0;}
   ENDHLSL
  }
 }
}
