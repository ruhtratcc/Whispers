// Verifica .

#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Collections;using System.Reflection;using NUnit.Framework;using UnityEngine;using UnityEngine.TestTools;using UnityEditor;
namespace HorrorGame.Tests
{
 public sealed partial class BeforeDemolitionFourStudentsTests
 {
  const string CorridorOutput="C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/CorridorDemon20261105";
  IEnumerator NativeCorridorReady()
  {
   var p=SchoolPrefix();p.chapterId=BeforeDemolitionTwoVoicesFlow.ChapterId;p.completedChapters.Add(BeforeDemolitionFourStudentsFlow.ChapterId);p.milestones.AddRange(new[]{BeforeDemolitionFourStudentsFlow.GatheredSignal,BeforeDemolitionFourStudentsFlow.KeySignal,BeforeDemolitionFourStudentsFlow.UpstairsSignal});p.sequences.Add(BeforeDemolitionFourStudentsFlow.IntercomSequenceId);p.sequences.Add("bd.loop.voice");
   foreach(var id in BeforeDemolitionFourStudentsFlow.StudentItemIds)Add(id);Add(BeforeDemolitionFourStudentsFlow.BiaTapeHeardId);Add(BeforeDemolitionFourStudentsFlow.StairKeyId);
   game.narrative.RestoreProgress(p);game.narrative.InitializeAfterSession(true);var c=game.GetComponent<BeforeDemolitionTwoVoicesFlow>();c.RestoreState("{\"version\":1,\"loopCount\":0}");
   game.player.Warp(new Vector3(-26.68f,3.85f,57.96f),0);Physics.SyncTransforms();
   yield return null;yield return null;
   var atmosphere = game.GetComponent<CorridorAtmosphere>();
   Assert.That(atmosphere.IsApplied, Is.False, "The stair landing retains the normal school geometry.");
   Assert.That(atmosphere.modulesRoot.activeSelf, Is.False);
   Assert.That(RenderSettings.fog, Is.True);
   Assert.That(RenderSettings.fogColor, Is.EqualTo(Color.black));
   Assert.That(game.player.view.playerCamera.farClipPlane, Is.LessThanOrEqualTo(atmosphere.settings.visibility + 2));
   CorridorCapture("chapter-five-stairs");
   Assert.That(atmosphere.bindings.Any(binding => binding.target && binding.outside != binding.original
       && binding.target.sharedMesh == binding.outside), Is.False, "The corridor does not substitute meshes before entry.");
   Assert.That(c.corridorEndBlock.activeSelf, Is.False);
   game.player.Warp(c.returnPose.position+Vector3.forward*4,0);Physics.SyncTransforms();yield return Until(()=>game.CanControl&&!game.narrative.SequencePlaying&&game.GetComponent<CorridorAtmosphere>().IsApplied,"New C5 atmosphere activates on the actual school",20);
  }
  void CorridorCapture(string name)
  {
   Directory.CreateDirectory(CorridorOutput);var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("HorrorGame.Editor.PresentationCapture")).First(t=>t!=null);type.GetMethod("Capture",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{UnityEngine.SceneManagement.SceneManager.GetActiveScene(),game.player.view.playerCamera,Path.Combine(CorridorOutput,name+".png"),1280,720,null,true});
  }
  [UnityTest,Category("CorridorDemonNative"),Timeout(240000)]
  public IEnumerator NativeModulesWrapBothWaysRestoreCheckpointAndRevealOnlyOffscreenMarks()
  {
   yield return NativeCorridorReady();var c=game.GetComponent<BeforeDemolitionTwoVoicesFlow>();var a=game.GetComponent<CorridorAtmosphere>();var v=game.GetComponent<DemonVoice>();
   var motion=typeof(FirstPersonMotor).GetField("horizontal",BindingFlags.NonPublic|BindingFlags.Instance);var turn=typeof(FirstPersonMotor).GetField("targetYaw",BindingFlags.NonPublic|BindingFlags.Instance);motion.SetValue(game.player,new Vector3(.3f,0,1));turn.SetValue(game.player,12f);game.player.WrapTo(game.player.transform.position+Vector3.forward*.05f);Assert.That((Vector3)motion.GetValue(game.player),Is.EqualTo(new Vector3(.3f,0,1)));Assert.That((float)turn.GetValue(game.player),Is.EqualTo(12f));Assert.That(a.modules.Length,Is.EqualTo(5));Assert.That(a.bindings.Length,Is.GreaterThan(0));Assert.That(a.bindings.All(b=>b.target.sharedMesh==b.outside),Is.True);Assert.That(c.occupiedVisuals,Is.Empty);Assert.That(c.watchedHeads,Is.Empty);
   yield return new WaitForSeconds(.7f);Assert.That(RenderSettings.fogColor,Is.EqualTo(Color.black));Assert.That(RenderSettings.fogMode,Is.EqualTo(FogMode.Exponential));Assert.That(game.save.flashlight.lamp.range,Is.LessThanOrEqualTo(9));Assert.That(game.save.flashlight.lamp.shadows,Is.EqualTo(LightShadows.None));Assert.That(game.player.view.playerCamera.fieldOfView,Is.EqualTo(49).Within(.01f));
   foreach(var module in a.modules){int index=Mathf.RoundToInt(a.moduleOrigin.InverseTransformPoint(module.root.position).z/a.settings.moduleLength);foreach(var light in module.lights)Assert.That(light.intensity,Is.EqualTo(Math.Abs(index)<=1?a.settings.lightIntensity:0).Within(.01f));Assert.That(module.bufferDoors.activeSelf,Is.EqualTo(index!=0));}
   for(int i=0;i<5;i++){
    game.player.Warp(c.returnPose.position+new Vector3(.1f,0,a.settings.moduleLength+.15f),17,8);Physics.SyncTransforms();int expected=i+1;yield return Until(()=>c.TraversalCount==expected,"Every forward crossing wraps, including after the third",10);
    Assert.That(a.moduleOrigin.InverseTransformPoint(game.player.transform.position).z,Is.EqualTo(.15f).Within(.06f));Assert.That(game.player.transform.eulerAngles.y,Is.EqualTo(17).Within(.02f));Assert.That(game.player.Pitch,Is.EqualTo(8).Within(.02f));Assert.That(game.player.transform.position.x,Is.EqualTo(c.returnPose.position.x+.1f).Within(.03f));
   }
   game.player.Warp(c.returnPose.position+Vector3.back*.15f,180,-4);Physics.SyncTransforms();yield return Until(()=>c.TraversalCount==6,"Backward wrap also advances the psychological stage",10);Assert.That(a.moduleOrigin.InverseTransformPoint(game.player.transform.position).z,Is.EqualTo(a.settings.moduleLength-.15f).Within(.06f));Assert.That(game.player.transform.eulerAngles.y,Is.EqualTo(180).Within(.02f));Assert.That(c.State.loopCount,Is.EqualTo(3));Assert.That(v.MaskSlip,Is.EqualTo(1));
   Assert.That(game.save.Save(),Is.True);Assert.That(game.save.TryReadCurrent(out var saved),Is.True);c.RestoreState("{\"version\":1,\"loopCount\":0}");Assert.That(game.save.RestoreSnapshot(saved),Is.True);game.narrative.InitializeAfterSession(true);yield return null;yield return null;Assert.That(c.TraversalCount,Is.EqualTo(6));Assert.That(v.MaskSlip,Is.EqualTo(1));
   c.RestoreState("{\"version\":1,\"loopCount\":0}");yield return null;var mark=a.marks.First(m=>m.threshold==1&&m.renderer.name.StartsWith("Contagem")&&Mathf.Abs(m.renderer.bounds.center.z-(c.returnPose.position.z+1.1f))<.2f);
   game.player.Warp(c.returnPose.position+new Vector3(0,0,2.5f),0);game.player.enabled=false;game.player.view.enabled=false;var eye=game.player.view.playerCamera.transform;eye.rotation=Quaternion.LookRotation(mark.renderer.bounds.center-eye.position);yield return null;c.State.wrapCount=1;yield return null;yield return null;Assert.That(mark.applied,Is.EqualTo(-1),"Visible marks must not pop into existence");eye.rotation=Quaternion.LookRotation(Vector3.right);yield return null;yield return null;Assert.That(mark.applied,Is.EqualTo(0),"The new tally commits only outside view");
   game.player.enabled=true;game.player.view.enabled=true;var look=Quaternion.LookRotation(mark.renderer.bounds.center-eye.position).eulerAngles;game.player.Warp(game.player.transform.position,look.y,Mathf.DeltaAngle(0,look.x));yield return new WaitForSeconds(.3f);CorridorCapture("corridor-tally-close");game.player.Warp(c.returnPose.position+Vector3.forward*4,0);yield return new WaitForSeconds(.3f);CorridorCapture("corridor-forward");game.player.Warp(c.returnPose.position+Vector3.forward*9,180);yield return new WaitForSeconds(.3f);CorridorCapture("corridor-backward");
   foreach(var receipt in PhoneMessages.For(game).State.received)if(receipt!=null)receipt.read=true;var scratch=a.marks.Single(m=>m.renderer.name=="Arranhões na folha móvel — "+c.sideDoor.persistentId);Assert.That(Vector3.Distance(scratch.renderer.bounds.center,c.sideDoor.leafCollider.bounds.center),Is.LessThan(.7f),"Scratch is on the actual leaf, not at a copied local coordinate");var offset=c.sideDoor.hinge.InverseTransformPoint(scratch.renderer.transform.position);c.sideDoor.SetOpen(true,false);yield return new WaitForSeconds(1.4f);Assert.That(Vector3.Distance(scratch.renderer.transform.position,c.sideDoor.hinge.TransformPoint(offset)),Is.LessThan(.001f));game.player.Warp(c.correctRoomVolume.bounds.center-Vector3.up*.8f,90);Physics.SyncTransforms();yield return Until(()=>c.Escaped&&!a.IsApplied,"The correct room restores original school visuals",15);Assert.That(a.modulesRoot.activeSelf,Is.False);Assert.That(a.bindings.All(b=>b.target.sharedMesh==b.original),Is.True);Assert.That(game.player.view.playerCamera.farClipPlane,Is.GreaterThan(14));
  }
  [UnityTest,Category("CorridorDemonNative"),Timeout(240000)]
  public IEnumerator RealImportedClipUsesMixerFallbackDelayedLayersPauseAndSingleCaption()
  {
   yield return NativeCorridorReady();var v=game.GetComponent<DemonVoice>();var original=v.config;var config=UnityEngine.Object.Instantiate(original);v.config=config;var c=game.GetComponent<BeforeDemolitionTwoVoicesFlow>();var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/HorrorGameplay/Audio/VoiceDubbing20261023/JOA-69.wav");Assert.That(clip,Is.Not.Null);clip.LoadAudioData();yield return Until(()=>clip.loadState==AudioDataLoadState.Loaded,"Imported recording loads",15);
   var line=new DemonLine{id="fixture.real-recording",text="Fala de teste.",mask=clip,captionSeconds=.5f};config.lines=new[]{line};try{
    v.SetMaskSlip(.5f);v.Play(line,c.voiceAnchor);Assert.That(v.LastPlan.fallback,Is.True);Assert.That(v.MaskSource.isPlaying,Is.True);Assert.That(v.LowSource.isPlaying,Is.False);Assert.That(config.lowGroup.audioMixer.GetFloat(config.pitchParameter,out float pitch),Is.True);Assert.That(pitch,Is.EqualTo(.56f).Within(.001f));
    game.Pause();yield return new WaitForSecondsRealtime(.2f);Assert.That(v.LowStartedAt,Is.EqualTo(-1));game.Resume();yield return Until(()=>v.LowStartedAt>=0,"Delayed low layer starts after resume",4);Assert.That(v.LowSource.outputAudioMixerGroup,Is.EqualTo(config.lowGroup));Assert.That(v.MaskSource.volume+v.LowSource.volume,Is.LessThanOrEqualTo(config.voiceCeiling+.0001f));Assert.That(v.LowSource.spatialBlend,Is.EqualTo(1));
    yield return null;Assert.That(Vector3.Distance(c.voiceAnchor.position,game.player.view.playerCamera.transform.position),Is.EqualTo(config.voiceDistance).Within(.03f));
    v.StopSpeech();v.SetMaskSlip(1);v.Play(line,c.voiceAnchor);Assert.That(v.MaskSource.isPlaying,Is.False);Assert.That(v.LowSource.isPlaying,Is.True);Assert.That(v.LowSource.pitch,Is.EqualTo(config.slowPitch));
    v.StopSpeech();v.QueueCorridor();yield return Until(()=>game.hud.subtitle.text=="Helena: Fala de teste.","One shared subtitle represents both audio layers",5);Assert.That(game.hud.subtitle.text.Split(new[]{"Fala de teste."},StringSplitOptions.None).Length,Is.EqualTo(2));v.StopSpeech();line.mirror=true;line.mirrorTransition=true;line.mirrorSlip=0;line.captionSeconds=.6f;v.Play(line,c.voiceAnchor);Assert.That(v.MaskSource.volume,Is.EqualTo(config.maskGain));Assert.That(v.LowSource.volume,Is.Zero);
    // Mirror progression is driven by the same live Update, after leaving the corridor chapter.
    var p=game.narrative.CaptureProgress();p.chapterId=BeforeDemolitionMirrorFlow.ChapterId;p.sequences.Add(BeforeDemolitionMirrorFlow.OfferSequence);game.narrative.RestoreProgress(p);game.narrative.InitializeAfterSession(true);v.Play(line,c.voiceAnchor);yield return Until(()=>v.MaskSlip>=.8f,"Mirror offer slips from mask to the low voice",clip.length+8);Assert.That(v.MaskSource.isPlaying,Is.False);Assert.That(v.LowSource.volume,Is.GreaterThan(0));v.StopSpeech();
   }finally{v.StopSpeech();v.config=original;UnityEngine.Object.Destroy(config);}
  }

  [UnityTest, Timeout(120000)]
  public IEnumerator RecordedHelenaLayersAndLastTapeAdvance()
  {
   yield return NativeCorridorReady();
   var corridor = game.GetComponent<BeforeDemolitionTwoVoicesFlow>();
   var voice = game.GetComponent<DemonVoice>();
   corridor.State.wrapCount = 1;
   voice.QueueCorridor();
   yield return Until(() => voice.MaskStartedAt >= 0 && voice.LowStartedAt >= 0, "Both Helena layers play");
   Assert.That(voice.LastPlan.fallback, Is.True);
   Assert.That(voice.MaskSource.clip.name, Does.StartWith("demon_corridor_"));
   Assert.That(voice.LowSource.clip, Is.SameAs(voice.MaskSource.clip));
   Assert.That(voice.LowSource.outputAudioMixerGroup, Is.SameAs(voice.config.lowGroup));
   voice.StopSpeech();

   foreach (var receipt in PhoneMessages.For(game).State.received) receipt.read = true;
   game.player.Warp(corridor.correctRoomVolume.bounds.center - Vector3.up * .8f, 90);
   Physics.SyncTransforms();
   yield return Until(() => corridor.Escaped && game.CanControl, "Enter the side room");
   Assert.That(corridor.tapeItem.CanInteract(game.interaction), Is.True);
   corridor.tapeItem.Interact(game.interaction);
   yield return Until(() => inspection.IsOpen, "Open the last tape");
   var clip = inspection.CurrentItem.recording.clips.Single();
   Assert.That(clip.name, Is.EqualTo("fita_helena_2018"));
   bool finished = false;
   void Complete(string id) => finished |= id == BeforeDemolitionTwoVoicesFlow.TapeId;
   inspection.RecordingCompleted += Complete;
   try
   {
    inspection.PlayRecording();
    yield return Until(() => inspection.RecordingPlaying, "Start the 2018 recording");
    Assert.That(corridor.TapeHeard, Is.False);
    yield return Until(() => finished, "The recording emits its completion event", clip.length + 8);
   }
   finally { inspection.RecordingCompleted -= Complete; }
   yield return Close();
   yield return Until(() => corridor.TapeHeard && !corridor.mirrorDoor.locked, "Full playback opens the mirror route");
  }
 }
}
#endif







