// Verifica .

#if UNITY_EDITOR
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
 public sealed class MirrorReflectionMaskTests
 {
  [UnityTest,Category("MirrorClipping")]
  public IEnumerator ActualGpuClipsAnOversizedReflectionToTheGlassAndHidesItWithTheGlass()
  {
   var pipeline=UniversalRenderPipeline.asset;var resident=pipeline.gpuResidentDrawerMode;var target=RenderTexture.active;
   var root=new GameObject("Isolated mirror GPU proof");root.SetActive(false);
   Material surface=null,red=null,stencil=null;RenderTexture rt=null;Texture2D image=null,hidden=null;
   try
   {
    pipeline.gpuResidentDrawerMode=GPUResidentDrawerMode.Disabled;
    var camera=new GameObject("Proof camera").AddComponent<Camera>();camera.transform.SetParent(root.transform);camera.enabled=false;camera.orthographic=true;camera.orthographicSize=2;camera.aspect=16f/9;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.allowMSAA=false;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
    var glass=GameObject.CreatePrimitive(PrimitiveType.Quad);glass.transform.SetParent(root.transform);glass.transform.position=Vector3.forward*3;glass.transform.localScale=new Vector3(.8f,1.2f,1);
    surface=new Material(Shader.Find("Universal Render Pipeline/Unlit"));surface.SetColor("_BaseColor",Color.black);glass.GetComponent<Renderer>().sharedMaterial=surface;
    var figure=GameObject.CreatePrimitive(PrimitiveType.Cube);figure.transform.SetParent(root.transform);figure.transform.position=Vector3.forward*4;figure.transform.localScale=new Vector3(2,2,.2f);
    red=new Material(Shader.Find("Universal Render Pipeline/Lit"));red.SetColor("_BaseColor",Color.red);red.SetColor("_EmissionColor",Color.red);red.EnableKeyword("_EMISSION");figure.GetComponent<Renderer>().sharedMaterial=red;
    stencil=new Material(Shader.Find("Hidden/Whispers/Mirror Surface Mask"));var mask=root.AddComponent<MirrorReflectionMask>();mask.glass=glass.GetComponent<Renderer>();mask.figure=figure;mask.actorShader=Shader.Find("Whispers/Mirror Reflection Actor");mask.surfaceMask=stencil;
    root.SetActive(true);yield return null;yield return null;Assert.That(mask.IsReady,Is.True);
    rt=new RenderTexture(320,180,24);rt.Create();image=Capture(camera,rt);
    var lo=camera.WorldToViewportPoint(glass.GetComponent<Renderer>().bounds.min);var hi=camera.WorldToViewportPoint(glass.GetComponent<Renderer>().bounds.max);int inside=0,outside=0;
    var pixels=image.GetPixels32();for(int y=0;y<180;y++)for(int x=0;x<320;x++){var c=pixels[y*320+x];if(c.r<30||c.r<c.g*1.5f||c.r<c.b*1.5f)continue;if(x>=lo.x*320-2&&x<=hi.x*320+2&&y>=lo.y*180-2&&y<=hi.y*180+2)inside++;else outside++;}
    var path="C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/SceneRefinements20261105";Directory.CreateDirectory(path);File.WriteAllBytes(path+"/mirror-gpu-mask.png",image.EncodeToPNG());File.WriteAllText(path+"/mirror-gpu-mask.json","{\"insideRedPixels\":"+inside+",\"outsideRedPixels\":"+outside+"}");
    Assert.That(inside,Is.GreaterThan(100),"The reflection must actually be visible behind the glass, not merely hidden everywhere");Assert.That(outside,Is.Zero,"No reflection pixel may escape the real glass mesh");
    glass.GetComponent<Renderer>().enabled=false;yield return null;hidden=Capture(camera,rt);Assert.That(System.Array.FindAll(hidden.GetPixels32(),c=>c.r>30&&c.r>c.g*1.5f&&c.r>c.b*1.5f).Length,Is.Zero,"Broken/hidden glass cannot leave a floating reflection");
   }
   finally{pipeline.gpuResidentDrawerMode=resident;RenderTexture.active=target;Object.Destroy(root);if(rt){rt.Release();Object.Destroy(rt);}if(image)Object.Destroy(image);if(hidden)Object.Destroy(hidden);if(surface)Object.Destroy(surface);if(red)Object.Destroy(red);if(stencil)Object.Destroy(stencil);}
   yield return null;
  }
  static Texture2D Capture(Camera camera,RenderTexture target){RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target,isPreview=false});RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();return image;}
 }
}
#endif
