// Verifica .

#if UNITY_EDITOR
using System;using System.Collections;using System.IO;using System.Linq;using NUnit.Framework;using UnityEngine;using UnityEngine.TestTools;using UnityEngine.InputSystem;using UnityEditor;
namespace HorrorGame.Tests {
 public sealed partial class BeforeDemolitionSchoolSceneTests {
  [UnityTest,Category("Immersion20261106"),Timeout(180000)]
  public IEnumerator FloorAnalysisFourDistinctTracksAndVisibleRatPreserveCheckpoint(){
   var phone=PhoneMessages.For(game);phone.Close();foreach(var m in phone.State.received){m.announced=m.read=true;}
   game.player.Warp(new Vector3(-16.4f,.65f,28.2f),0);yield return Until(()=>game.narrative.HasSignal("bd.school.secretary"),"Secretary arrival");
   phone.Close();foreach(var m in phone.State.received){m.announced=m.read=true;}yield return Until(()=>game.CanControl,"Phone closed");
   yield return Inspect(BeforeDemolitionSchoolAction.Cups,new Vector3(-16.4f,.65f,28.2f));
   Assert.That(inspection.IsOpen,Is.False);Assert.That(game.interaction.inventory.Contains("bd.cups"),Is.True);Assert.That(game.narrative.HasSignal("bd.school.cups"),Is.True);
   var e=game.GetComponent<SecretaryEvidence>();Assert.That(e.footprints.Length,Is.EqualTo(4));Assert.That(e.footprints.Select(r=>r.sharedMaterials[0].mainTexture).Distinct().Count(),Is.EqualTo(4));
   var alphas=e.footprints[0].sharedMaterials.Select(m=>m.GetColor("_BaseColor").a).ToArray();Assert.That(alphas.Distinct().Count(),Is.EqualTo(4));
   var whistle=WhistleSystem.For(game);whistle.State.cooldownRemaining=0;Assert.That(whistle.BlowTap(),Is.True);Assert.That(e.PulseVisible,Is.True);Assert.That(e.footprints[0].sharedMaterials.Select(m=>m.GetColor("_BaseColor").a),Is.EqualTo(alphas));
   var rats=game.GetComponent<RatScurry>();Assert.That(rats.routes.Length,Is.GreaterThanOrEqualTo(6));Assert.That(rats.TryRun(),Is.True);yield return null;
   var rat=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t=>t.name=="Rato — corrida pelo canto");var position=rat.position;Assert.That(rat.GetComponentInChildren<Animation>().isPlaying,Is.True);Assert.That(rat.GetComponents<AudioSource>().Any(s=>s.isPlaying&&s.spatialBlend==1),Is.True);
   yield return new WaitForSeconds(.15f);Assert.That(Vector3.Distance(position,rat.position),Is.GreaterThan(.1f));
   game.Pause();position=rat.position;yield return new WaitForSecondsRealtime(.15f);Assert.That(rat.position,Is.EqualTo(position));game.Resume();yield return Until(()=>!rats.Running,"Rat leaves the corner",4);
   Assert.That(game.save.Save(),Is.True);Assert.That(game.save.TryReadCurrent(out var snapshot),Is.True);Assert.That(game.save.RestoreSnapshot(snapshot),Is.True);game.narrative.InitializeAfterSession(true);yield return null;yield return null;Assert.That(game.narrative.HasSignal("bd.school.cups"),Is.True);Assert.That(inspection.IsOpen,Is.False);
  }
 }
 public sealed partial class BeforeDemolitionRoadSceneTests {
  [UnityTest,Category("Immersion20261106"),Timeout(180000)]
  public IEnumerator F8SkipUsesNormalSignalsAndCarriesEquipmentIntoSchool(){
   var terminal=game.GetComponent<ChapterCommandTerminal>();Assert.That(terminal,Is.Not.Null);yield return KeyPress(Key.F8);Assert.That(terminal.IsOpen,Is.True);Assert.That(game.State,Is.EqualTo(GameState.Inspecting));
   var entry=terminal.GetComponentsInChildren<TMPro.TMP_InputField>(true).Single();entry.onSubmit.Invoke("nope");Assert.That(terminal.IsOpen,Is.True);entry.onSubmit.Invoke("  SKIP  ");yield return Until(()=>game.narrative.HasSignal("bd.road.school-gate")||GameSession.Instance&&GameSession.Instance.gameObject.scene.name=="SchoolCEEP","Skip accepted",15);
   yield return Until(()=>GameSession.Instance&&GameSession.Instance.gameObject.scene.name=="SchoolCEEP"&&GameSession.Instance.narrative.IsInitialized&&GameSession.Instance.CanControl,"School after skip",120);
   var school=GameSession.Instance;Assert.That(school.narrative.Progress.chapterId,Is.EqualTo(BeforeDemolitionCampaign.SchoolChapterId));Assert.That(school.equipment.HasFlashlight,Is.True);Assert.That(school.interaction.inventory.Contains(BeforeDemolitionCampaign.WhistleItemId),Is.True);Assert.That(Path.GetDirectoryName(school.save.SavePath),Is.EqualTo(temporary).IgnoreCase);
   Assert.That(school.narrative.Progress.completedChapters,Does.Contain(BeforeDemolitionCampaign.RoadChapterId));
  }
 }
 public sealed partial class BeforeDemolitionMirrorTests {
  [UnityTest,Category("Immersion20261106")]
  public IEnumerator FlashlightSwingKeepsGlassUntilImpactAndCleansUpOnRestore(){
   var torch=f.player.gameObject.AddComponent<FlashlightController>();torch.enabled=false;torch.view=f.camera;torch.handRig=f.Child("Test torch",f.player.transform).transform;var mesh=GameObject.CreatePrimitive(PrimitiveType.Cube);mesh.transform.SetParent(torch.handRig,false);mesh.transform.localScale=new Vector3(.04f,.04f,.22f);torch.handRig.gameObject.SetActive(false);
   yield return ChoiceReady();BeforeDemolitionChapterFixture.Click(flow.endingView.RefuseButton);Assert.That(flow.StrikeActive,Is.True);Assert.That(flow.glassVisual.activeSelf,Is.True);Assert.That(f.camera.transform.Find("Lanterna — golpe no espelho"),Is.Not.Null);
   f.game.Pause();yield return new WaitForSecondsRealtime(.7f);Assert.That(flow.StrikeActive,Is.True);f.game.Resume();yield return BeforeDemolitionChapterFixture.Until(()=>!flow.StrikeActive,"Strike reaches impact");Assert.That(flow.glassVisual.activeSelf,Is.False);yield return null;Assert.That(f.camera.transform.Find("Lanterna — golpe no espelho"),Is.Null);
   flow.RestoreState(flow.CaptureState());Assert.That(flow.StrikeActive,Is.False);Assert.That(flow.Data.choice,Is.EqualTo(BeforeDemolitionEndingChoice.Refuse));
  }
 }
}
#endif
