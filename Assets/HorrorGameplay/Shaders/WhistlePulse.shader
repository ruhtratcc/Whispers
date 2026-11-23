Shader "Hidden/Whispers/Whistle Pulse"
{
 SubShader{Tags{"RenderPipeline"="UniversalPipeline"}
 Pass{ZWrite Off ZTest Always Cull Off
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
 TEXTURE2D_X_FLOAT(_WhistleDepth);float4 _WhistleOrigin;float4 _WhistleRing;
 half4 Frag(Varyings i):SV_Target{
  half4 colour=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,i.texcoord);
  float raw=SAMPLE_TEXTURE2D_X(_WhistleDepth,sampler_PointClamp,i.texcoord).r;
  #if !UNITY_REVERSED_Z
  raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
  #endif
  float3 world=ComputeWorldSpacePosition(i.texcoord,raw,UNITY_MATRIX_I_VP);
  float distanceToPlayer=distance(world,_WhistleOrigin.xyz);
  float band=(1-smoothstep(_WhistleRing.x*.3,_WhistleRing.x,abs(distanceToPlayer-_WhistleOrigin.w)))*_WhistleRing.y;
  colour.rgb+=band*half3(.10,.15,.14);return colour;
 }
 ENDHLSL }
 }
}
