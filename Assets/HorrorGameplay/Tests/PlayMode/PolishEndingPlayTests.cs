// Verifica .

#if UNITY_EDITOR
using System;using System.Collections;using NUnit.Framework;using UnityEditor;using UnityEngine;using UnityEngine.TestTools;using UnityEngine.Rendering;
namespace HorrorGame.Tests {
public sealed partial class BeforeDemolitionMirrorTests {
 [Serializable] sealed class CameraTrace {public Vector3 cameraPosition,cameraEuler,seatPosition,seatEuler,screenViewport;public float distance,angle;public int frame;}
 void CaptureEpilogue(string name){var old=RenderTexture.active;var rt=new RenderTexture(1280,720,24);rt.Create();Texture2D image=null;try{RenderPipeline.SubmitRenderRequest(f.camera,new RenderPipeline.StandardRequest{destination=rt,isPreview=true});RenderTexture.active=rt;image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();System.IO.File.WriteAllBytes("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/Passe20261103/"+name+".png",image.EncodeToPNG());}finally{RenderTexture.active=old;rt.Release();UnityEngine.Object.Destroy(rt);if(image)UnityEngine.Object.Destroy(image);}}
 [UnityTest,Category("Polish20261103Epilogue"),Timeout(90000)]
 public IEnumerator GoodEndingUsesOriginalOfficeTelevisionAndActualEmailBeforeOneCompletion(){
 // Match the native player camera; the default 30cm clip plane hides the CRT at 23cm.
 f.camera.nearClipPlane=.04f;
 var config=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<EndingPresentationConfig>("Assets/HorrorGameplay/Data/Polish20261103/Endings.asset"));
 var e=f.root.AddComponent<EndingEpilogue>();e.game=f.game;e.mirror=flow;e.config=config;e.morningOfficePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HorrorGameplay/Data/Polish20261103/MorningOffice.prefab");
 config.goodPhoneLines=Array.Empty<StoryLine>();config.morningNews=new StoryLine{text="Noticiário provisório",seconds=.05f};config.eyeSeconds=.02f;config.morningSeconds=12;config.creditsSeconds=.02f;config.cardFadeSeconds=.02f;
 try{
 yield return ChoiceReady();BeforeDemolitionChapterFixture.Click(flow.endingView.RefuseButton);
 yield return BeforeDemolitionChapterFixture.Until(()=>f.game.CanControl&&!f.director.SequencePlaying,"Return after recoil.",6);Assert.That(flow.ReturnWhistle(),Is.True);
 yield return BeforeDemolitionChapterFixture.Until(()=>e.Stage=="Morning","Morning camera actually advances.",12);
 yield return new WaitForSecondsRealtime(4.9f);CaptureEpilogue("morning-window");
 yield return new WaitForSecondsRealtime(4f);CaptureEpilogue("morning-tv");
 yield return BeforeDemolitionChapterFixture.Until(()=>e.Stage=="EmailNotice","The incoming email is announced before camera movement.",8);
 var notice=BeforeDemolitionChapterFixture.Private<GameObject>(e,"mailNotice");
 Assert.That(notice.activeInHierarchy,Is.True);
 Assert.That(notice.GetComponentInChildren<TMPro.TMP_Text>().text,Does.Contain("Novo e-mail"));
 Assert.That(e.Computer.IsSeated,Is.False);
 Assert.That(config.emailSound,Is.Not.Null);
 yield return BeforeDemolitionChapterFixture.Until(()=>e.Stage=="Email"&&e.Computer&&e.Computer.Ready,"Morning reaches original PC.",35);
 yield return BeforeDemolitionChapterFixture.Until(()=>e.Computer.inbox.CurrentMessageId=="ending.helena.reply","Final email opens on the physical display.",8);
 yield return null;yield return null;CaptureEpilogue("morning-email");
 var target=e.Computer.seatCameraPose;var surface=e.Computer.screenSurface;var viewport=f.camera.WorldToViewportPoint(surface.position);
 System.IO.File.WriteAllText("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/Passe20261103/epilogue-camera-runtime.json",JsonUtility.ToJson(new CameraTrace {cameraPosition=f.camera.transform.position,cameraEuler=f.camera.transform.eulerAngles,seatPosition=target.position,seatEuler=target.eulerAngles,distance=Vector3.Distance(f.camera.transform.position,target.position),angle=Quaternion.Angle(f.camera.transform.rotation,target.rotation),screenViewport=viewport,frame=Time.frameCount},true));
 Assert.That(Vector3.Distance(f.camera.transform.position,target.position),Is.LessThan(.03f),"Actual player camera must reach the PC seat, not only advance the UI.");
 Assert.That(Quaternion.Angle(f.camera.transform.rotation,target.rotation),Is.LessThan(.15f),"Actual camera must face the monitor.");
 Assert.That(viewport.z,Is.GreaterThan(f.camera.nearClipPlane),"CRT must not be clipped out of the image.");Assert.That(Mathf.Abs(viewport.x-.5f),Is.LessThan(.12f));Assert.That(Mathf.Abs(viewport.y-.5f),Is.LessThan(.12f));
 Assert.That(EndingEpilogue.Active,Is.True);Assert.That(e.MorningOffice,Is.Not.Null);Assert.That(e.MorningOffice.GetComponentsInChildren<GameSession>(true),Is.Empty);
 Assert.That(BeforeDemolitionChapterFixture.Private<TMPro.TMP_Text>(e.Computer,"tray").text,Is.EqualTo("08:12"),"Morning desktop time must match the final email.");
 var crt = Array.Find(e.MorningOffice.GetComponentsInChildren<MeshRenderer>(true),
     renderer => renderer.transform.parent && renderer.transform.parent.name == "TV — television_02 Poly Haven reutilizada");
 Assert.That(crt, Is.Not.Null);
 var television = crt.GetComponent<MeshFilter>().sharedMesh;
 Assert.That(television.subMeshCount, Is.EqualTo(2));
 Assert.That(television.GetIndexCount(1), Is.EqualTo(512u * 3));
 Assert.That(crt.sharedMaterials[1].GetTexture("_BaseMap").name, Is.EqualTo("Noticiario"));
 Assert.That(e.MorningOffice.transform.Find("TV — noticiário"), Is.Null);
 Assert.That(e.Computer.inbox.catalog.Find("ending.helena.reply").subject,Is.EqualTo("Pode me responder agora."));
 Assert.That(e.SelectReply(0),Is.False,"Reply remains locked until the email is read.");
 yield return BeforeDemolitionChapterFixture.Until(()=>e.Computer.inbox.State.IsRead("ending.helena.reply"),"Visible actual app finishes reading.",8);
 yield return null;yield return null;CaptureEpilogue("morning-email-read");
 Assert.That(e.SelectReply(1),Is.True);Assert.That(e.SelectReply(2),Is.False);
 yield return new WaitForSecondsRealtime(2f);CaptureEpilogue("morning-reply");
 yield return BeforeDemolitionChapterFixture.Until(()=>f.director.Progress.endingSeen,"Credits finish chosen ending once.",10);
 Assert.That(e.Stage,Is.EqualTo("Complete"));Assert.That(EndingEpilogue.Active,Is.False);Assert.That(completedCount,Is.EqualTo(1));Assert.That(f.camera.fieldOfView,Is.EqualTo(49));
 }finally{UnityEngine.Object.Destroy(e);UnityEngine.Object.Destroy(config);}
 }
}
}
#endif
