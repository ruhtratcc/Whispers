Shader "Whispers/Whistle Outline"
{
 Properties { _SenseColor("Colour",Color)=(1,.7,.3,1) _SenseAlpha("Opacity",Float)=0 _OutlineWidth("Width",Float)=.012 _Pattern("Category",Float)=1 _ThroughWall("Occluded opacity",Float)=0 }
 SubShader{
 Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+30" "RenderType"="Transparent"}
 Pass{
 Name "Interactable outline and x-ray" Tags{"LightMode"="SRPDefaultUnlit"}
 Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 float4 _SenseColor;float _SenseAlpha,_OutlineWidth,_Pattern,_ThroughWall;
 struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;};
 struct V{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float eyeDepth:TEXCOORD1;};
 V vert(A a){V o;o.positionWS=TransformObjectToWorld(a.positionOS.xyz)+TransformObjectToWorldNormal(a.normalOS)*_OutlineWidth;o.positionCS=TransformWorldToHClip(o.positionWS);o.eyeDepth=-TransformWorldToView(o.positionWS).z;return o;}
 float pattern(float3 p){if(_Pattern<.5)return lerp(.35,1,step(.45,frac(p.y*24)));if(_Pattern>1.5)return lerp(.2,1,step(.35,frac((p.x+p.z)*7)));return 1;}
 half4 frag(V i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target{
  bool front=IS_FRONT_VFACE(face,true,false);
  float rawDepth=SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS));
  float sceneDepth=LinearEyeDepth(rawDepth,_ZBufferParams);
  bool hidden=i.eyeDepth>sceneDepth+.025;
  // One URP pass: depth decides each pixel, including partially occluded objects.
  if(hidden){if(!front||_ThroughWall<=0)discard;return half4(_SenseColor.rgb,_SenseAlpha*_ThroughWall*pattern(i.positionWS));}
  if(front)discard;
  return half4(_SenseColor.rgb,_SenseAlpha*pattern(i.positionWS));
 }
 ENDHLSL
 }
 }
}
