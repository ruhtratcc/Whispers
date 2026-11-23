// Verifica .

#if UNITY_EDITOR
using System;using System.Collections;using System.IO;using System.Linq;using NUnit.Framework;using UnityEngine;using UnityEngine.TestTools;using UnityEngine.InputSystem;using UnityEditor;
namespace HorrorGame.Tests{
 public sealed partial class BeforeDemolitionSchoolSceneTests{
 [Serializable]class LockAllocEvidence{public long[] samples;public double meanBytes;public long baselineFrameBytes,allocationProbeBytes;public string scope;}
 const string LockWork="C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/work/Lockpick20261101/";
 LockableDoor Training()=>UnityEngine.Object.FindObjectsByType<LockableDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.training);
 IEnumerator GetPins(){game.player.Warp(new Vector3(-16.4f,.65f,28.2f),0);
 yield return Until(()=>game.narrative.HasSignal("bd.school.secretary"),"Secretary accessible");flow.drawer.Interact(game.interaction);
 yield return Until(()=>flow.drawer.OpenFraction>.8f,"Unrestricted secretary drawer");var pin=UnityEngine.Object.FindObjectsByType<LockpickPickup>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.firstPickup);
 Assert.That(pin.CanInteract(game.interaction),Is.True);
 yield return Aim(pin.transform,new Vector3(-16.565f,.65f,28.0f));if(game.interaction.Target!=pin){var ray=new Ray(game.player.view.playerCamera.transform.position,game.player.view.playerCamera.transform.forward);File.WriteAllText(LockWork+"pickup-ray.txt","state="+game.State+" can="+game.CanControl+" pin="+pin.transform.position+" drawer="+flow.drawer.drawer.position+" eye="+ray.origin+" direction="+ray.direction+"\n"+string.Join("\n",Physics.RaycastAll(ray,4,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance).Select(h=>h.collider.name+" | "+h.distance+" | "+h.collider.transform.position+" | "+h.collider.bounds)));}Assert.That(game.interaction.Target,Is.SameAs(pin),"Hairpin physically reachable, actual="+game.interaction.CurrentPrompt);
 yield return Press(Key.E);
 Assert.That(game.lockpick.Count,Is.EqualTo(2));
 Assert.That(game.lockpick.Acquired,Is.True);
 Assert.That(pin.CanInteract(game.interaction),Is.False);
 yield return Until(()=>game.equipment.CanSwitch,"Equip available");
 Assert.That(game.equipment.RequestSlot(3),Is.True);game.equipment.Advance(.5f);
 Assert.That(game.lockpick.Equipped,Is.True);}
 [UnityTest,Category("Lockpick"),Timeout(120000)]public IEnumerator LockpickNativePickupTrainingHintPauseAndRealUI(){
 var s=game.lockpick;
 Assert.That(s&&s.config&&s.view,Is.True);
 Assert.That(s.Count,Is.Zero);
 Assert.That(game.interaction.inventory.Contains(LockpickSystem.ItemId),Is.False);
 Assert.That(game.equipment.RequestSlot(3),Is.False);
 var locks=UnityEngine.Object.FindObjectsByType<LockableDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None);
 Assert.That(locks.Length,Is.EqualTo(6));
 Assert.That(UnityEngine.Object.FindObjectsByType<InteractiveDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,Is.EqualTo(129));foreach(var door in new[]{flow.secretaryDoor,flow.libraryDoor,flow.administrationDoor})if(door)Assert.That(door.optionalLock,Is.Null);
 Assert.That(flow.drawer.optionalLock,Is.Null);
 var training=Training();
 Assert.That(training.Prompt(game.interaction),Is.EqualTo("Trancada"));foreach(var l in locks){Assert.That(l.IsOptional,Is.True);
 Assert.That(l.GetComponent<WhistleHighlightable>(),Is.Null);}
 yield return GetPins();
 Assert.That(training.Prompt(game.interaction),Is.EqualTo("Arrombar"));yield return Press(Key.Q);game.equipment.Advance(.5f);Assert.That(s.Equipped,Is.False,"Q cycles after obtaining the hairpin");yield return Press(Key.Digit3);game.equipment.Advance(.5f);Assert.That(s.Equipped,Is.True);yield return null;var held=BeforeDemolitionChapterFixture.Private<Transform[]>(game.equipment,"extraVisuals")[2];Assert.That(held&&held.gameObject.activeInHierarchy,Is.True);var planes=GeometryUtility.CalculateFrustumPlanes(game.player.view.playerCamera);Assert.That(held.GetComponentsInChildren<Renderer>().Any(r=>GeometryUtility.TestPlanesAABB(planes,r.bounds)),Is.True,"Held hairpin stays in the FOV 49 view");var whistle=WhistleSystem.For(game);whistle.State.discovered=whistle.State.loreKnown=true;var context=new WhistleContext(whistle,training.transform.position,Vector3.forward,WhistleKind.Tap,0,1,"SchoolCEEP","secretaria");
 Assert.That(training.OfferHint(context),Is.True);
 Assert.That(training.OfferHint(context),Is.False);
 Assert.That(s.Open(training),Is.True);
 Assert.That(game.CanControl,Is.False);
 Assert.That(s.HintRemaining,Is.GreaterThan(1));
 Assert.That(s.view.title.text,Is.Not.Empty);
 Assert.That(s.view.cancelButton.GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo("Cancelar"));
 Assert.That(s.view.cylinder.GetComponent<UnityEngine.UI.Image>().sprite,Is.Not.Null);
 Assert.That(game.equipment.RequestSlot(1),Is.False);
 Assert.That(whistle.BlowTap(),Is.False);var before=s.Count;game.Pause();float elapsed=s.Attempt.Elapsed;s.Advance(.1f,90,true);
 Assert.That(s.Attempt.Elapsed,Is.EqualTo(elapsed));
 Assert.That(s.view.gameObject.activeSelf,Is.False);game.Resume();
 Assert.That(s.view.gameObject.activeSelf,Is.True);s.Advance(.1f,s.Attempt.TargetAngle,false);
 yield return CaptureLockpick(s.view,"training-ui.png");s.Cancel();
 yield return Until(()=>game.CanControl,"Cancel returns control");
 Assert.That(s.Count,Is.EqualTo(before));
 Assert.That(game.CanControl,Is.True);
 Assert.That(s.Open(training),Is.True);
 Assert.That(s.HintRemaining,Is.Zero);for(int i=0;i<1000&&s.IsOpen;i++)s.Advance(.05f,s.Attempt.TargetAngle,true);
 Assert.That(training.unlocked,Is.True);
 Assert.That(training.rewards.activeInHierarchy,Is.True);
 Assert.That(training.drawer.IsOpen,Is.True);
 Assert.That(s.Count,Is.EqualTo(2));
 yield return Until(()=>game.CanControl,"Unlock returns control");
 Assert.That(game.CanControl,Is.True);
 }
 [UnityTest,Category("Lockpick"),Timeout(120000)]public IEnumerator LockpickBreakZeroReservesAssistanceAndAutomaticRemainOptional(){
 yield return GetPins();var s=game.lockpick;var door=Training();
 Assert.That(s.Open(door),Is.True);for(int i=0;i<1000&&s.IsOpen;i++)s.Advance(.05f,s.Attempt.TargetAngle>90?0:180,true);
 yield return Until(()=>game.CanControl,"Empty pins returns control");
 Assert.That(s.Count,Is.Zero);
 Assert.That(s.CountLabel,Is.EqualTo("SEM GRAMPO"));
 Assert.That(game.CanControl,Is.True);
 Assert.That(door.unlocked,Is.False);
 Assert.That(door.breaks,Is.EqualTo(2));
 Assert.That(s.Equipped,Is.True);
 Assert.That(door.Prompt(game.interaction),Is.EqualTo("Sem grampo"));
 Assert.That(flow.drawer.optionalLock,Is.Null);
 Assert.That(flow.libraryDoor.optionalLock,Is.Null,"Main story still uses its library key");
 var loose=UnityEngine.Object.FindObjectsByType<LockpickPickup>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.persistentId=="bd.lockpick.loose-reserve");loose.Interact(game.interaction);
 Assert.That(s.Count,Is.EqualTo(1));LockpickPreferences.Set(true,true);
 Assert.That(s.Open(door),Is.True);for(int i=0;i<300&&s.IsOpen;i++)s.Advance(.05f,0,true);
 Assert.That(door.unlocked,Is.True);
 yield return Until(()=>game.CanControl,"Automatic returns control");
 Assert.That(s.Count,Is.Zero);
 Assert.That(game.CanControl,Is.True);
 Assert.That(game.save.Save(),Is.True);
 Assert.That(game.save.TryReadCurrent(out var data),Is.True);
 Assert.That(data.lockpick.count,Is.Zero);
 Assert.That(data.lockpick.acquired,Is.True);LockpickPreferences.Set(false,false);
 }
 [UnityTest,Category("Lockpick"),Timeout(120000)]public IEnumerator LockpickCheckpointRoundTripLegacyDefaultsAndNativeAllocations(){
 yield return GetPins();var s=game.lockpick;var d=Training();
 Assert.That(s.Open(d),Is.True);s.Cancel();
 yield return Until(()=>game.CanControl,"Cancel returns control");
 Assert.That(game.save.Save(),Is.True);
 Assert.That(game.save.TryReadCurrent(out var baseline),Is.True);
 d.difficulty=LockDifficulty.Medium;Assert.That(s.Open(d),Is.True);for(int i=0;i<200&&s.Attempt.CompletedPins==0;i++)s.Advance(.05f,s.Attempt.TargetAngle,true);Assert.That(d.pinsCompleted,Is.EqualTo(1));s.Cancel();yield return Until(()=>game.CanControl,"Partial lock control");Assert.That(game.save.Save(),Is.True);Assert.That(game.save.TryReadCurrent(out var partial),Is.True);d.pinsCompleted=0;Assert.That(game.save.RestoreSnapshot(partial),Is.True);Assert.That(d.pinsCompleted,Is.EqualTo(1));d.difficulty=LockDifficulty.Easy;Assert.That(game.save.RestoreSnapshot(baseline),Is.True);game.narrative.InitializeAfterSession(true);yield return Until(()=>game.CanControl&&!game.narrative.SequencePlaying,"Loaded checkpoint reinitializes narrative");
 // Warm the same native UI, captions, audio pool and attempt path before recording the complete open/finish/cancel lifecycle.
 var recorder=Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory,"GC Allocated In Frame",16);yield return null;yield return null;long baselineFrame=recorder.LastValue;long probeStart=GC.GetAllocatedBytesForCurrentThread();var probe=new byte[4096];long probeBytes=GC.GetAllocatedBytesForCurrentThread()-probeStart;GC.KeepAlive(probe);long[] allocations=new long[5];for(int run=0;run<6;run++){yield return Until(()=>game.CanControl,"Benchmark control");d.unlocked=false;d.pinsCompleted=d.breaks=0;d.Reconcile();s.Restore(new LockpickState{initialized=true,acquired=true,count=2});long bytes=GC.GetAllocatedBytesForCurrentThread();
 Assert.That(s.Open(d),Is.True,"open benchmark "+run+" equipped="+s.Equipped+" count="+s.Count+" open="+s.IsOpen+" optional="+d.IsOptional+" allowed="+d.ChapterAllowed+" unlocked="+d.unlocked+" state="+game.State+" hidden="+game.player.IsHidden+" seq="+game.narrative.SequencePlaying);for(int i=0;i<400&&s.IsOpen;i++)s.Advance(.05f,s.Attempt.TargetAngle,true);yield return null;long allocated=recorder.LastValue;if(run>0)allocations[run-1]=allocated;
 Assert.That(d.unlocked,Is.True);}
 yield return Until(()=>game.CanControl,"Last benchmark returns control");recorder.Dispose();File.WriteAllText(LockWork+"native-allocation.json",JsonUtility.ToJson(new LockAllocEvidence{samples=allocations,meanBytes=allocations.Average(),baselineFrameBytes=baselineFrame,allocationProbeBytes=probeBytes,scope="Unity ProfilerRecorder GC Allocated In Frame: entire native SchoolCEEP frame containing easy Open/Advance/Complete/Cancel. Includes scene background, TMP/UI/audio/ScheduleSave/test assertions; NOT isolated per-attempt bytes. GC.GetAllocatedBytesForCurrentThread failed positive-control allocation (probe); zero from that API is NOT accepted as zero allocation. GPU/mixer excluded."},true));
 d.hintUsed=true;d.breaks=3;s.state.count=1;
 Assert.That(game.save.Save(),Is.True);
 Assert.That(game.save.TryReadCurrent(out var saved),Is.True);
 Assert.That(saved.objects.Any(o=>o.id==d.persistentId),Is.True);d.unlocked=false;d.hintUsed=false;s.state.count=0;
 Assert.That(game.save.RestoreSnapshot(saved),Is.True);
 Assert.That(d.unlocked,Is.True);
 Assert.That(d.rewards.activeInHierarchy,Is.True);
 Assert.That(d.hintUsed,Is.True);
 Assert.That(s.Count,Is.EqualTo(1));
 Assert.That(s.Equipped,Is.True);
 Assert.That(game.save.RestoreSnapshot(baseline),Is.True);
 Assert.That(d.unlocked,Is.False);
 Assert.That(s.Count,Is.EqualTo(2));baseline.lockpick=null;baseline.inventory.RemoveAll(i=>i.id==LockpickSystem.ItemId);
 Assert.That(game.save.RestoreSnapshot(baseline),Is.True);
 Assert.That(s.Count,Is.Zero);
 Assert.That(s.Acquired,Is.False);
 }
 IEnumerator CaptureLockpick(LockpickView source,string filename){
 var root=new GameObject("Lockpick capture",typeof(RectTransform),typeof(Canvas));root.layer=5;var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;var camGo=new GameObject("UI camera");var camera=camGo.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.cullingMask=1<<5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.04f);canvas.worldCamera=camera;canvas.planeDistance=1;var copy=UnityEngine.Object.Instantiate(source.gameObject,root.transform,false);foreach(var t in copy.GetComponentsInChildren<Transform>(true))t.gameObject.layer=5;var rt=new RenderTexture(1920,1080,24);var old=RenderTexture.active;Texture2D image=null;
 try{rt.Create();camera.targetTexture=rt;Canvas.ForceUpdateCanvases();
 yield return null;UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=rt,isPreview=false});RenderTexture.active=rt;image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
 Assert.That(image.GetPixels32().Count(p=>p.r>100&&p.g>100&&p.b>100),Is.GreaterThan(500));Directory.CreateDirectory("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/Lockpick20261101");File.WriteAllBytes("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/Lockpick20261101/"+filename,image.EncodeToPNG());}
 finally{camera.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.Destroy(rt);if(image)UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(camGo);}
 }
 }
}
#endif





