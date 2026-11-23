// Verifica .

#if UNITY_EDITOR
using System;using System.Collections;using System.IO;using System.Linq;using NUnit.Framework;using UnityEngine;using UnityEngine.InputSystem;using UnityEngine.TestTools;
namespace HorrorGame.Tests
{
 public sealed partial class BeforeDemolitionSchoolSceneTests
 {
  const string SecretaryProof="C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/TrocaCopos20261103/";
  IEnumerator EvidenceShot(string name){yield return new WaitForSeconds(.4f);ScreenCapture.CaptureScreenshot(SecretaryProof+name+".png");yield return null;yield return null;}
  IEnumerator ReadSecretaryPhoneGate()
  {
   if (PhoneProgression.CanAdvance(game,"bd.school.key")) yield break;
   var phone=PhoneMessages.For(game);yield return Press(Key.P);yield return Until(()=>phone.IsOpen,"Read required secretary messages");yield return null;yield return null;yield return Press(Key.P);yield return Until(()=>game.CanControl,"Close phone after the existing message gate");Assert.That(PhoneProgression.CanAdvance(game,"bd.school.key"),Is.True);
  }
  [UnityTest,Category("SecretaryEvidence"),Timeout(120000)]
  public IEnumerator FootprintsFirstTapPagesAndLibraryKeySurviveAnOldCheckpoint()
  {
   var evidence=flow.GetComponent<SecretaryEvidence>();var whistle=WhistleSystem.For(game);Assert.That(evidence,Is.Not.Null);Assert.That(whistle.State.loreKnown,Is.False);
   game.player.Warp(new Vector3(-16.4f,.65f,28.2f),0);yield return Until(()=>game.narrative.HasSignal("bd.school.secretary"),"Secretary reached");
   evidence.pulseSeconds=1.2f;Assert.That(whistle.BlowTap(),Is.True);Assert.That(evidence.PulseVisible&&evidence.KeyHighlighted,Is.True);Assert.That(evidence.keyEcho.gameObject.activeInHierarchy,Is.True);Assert.That(game.hud.subtitle.text,Is.EqualTo(whistle.config.tapCaption),"No claim that nothing happened while the tracks are glowing.");Assert.That(whistle.HasResponded(SecretaryEvidence.FirstTapMarker),Is.True);Assert.That(whistle.State.loreKnown,Is.False,"The local evidence response must not unlock the later mural/lore.");
   Assert.That(evidence.footprints[0].sharedMaterial.GetColor("_EmissionColor").r,Is.GreaterThan(0));Assert.That(evidence.footprints[0].sharedMaterial.IsKeywordEnabled("_EMISSION"),Is.True);Assert.That(UnityEditor.AssetDatabase.GetAssetPath(evidence.footprints[0].sharedMaterial),Is.Empty,"Pulse owns one disposable material, never modifies the source asset.");
   yield return Aim(evidence.footprintLookPoint,new Vector3(-16.4f,.65f,28.2f));yield return EvidenceShot("footprints-first-whistle");yield return new WaitForSeconds(1.4f);Assert.That(evidence.PulseVisible||evidence.KeyHighlighted,Is.False);whistle.State.cooldownRemaining=0;Assert.That(whistle.BlowTap(),Is.True);Assert.That(evidence.PulseVisible,Is.False,"One saved first-tap response, not a permanent duplicate key.");
   yield return Aim(evidence.clock,new Vector3(-16.3f,.65f,28.4f));yield return EvidenceShot("clock-0307");
   yield return Inspect(BeforeDemolitionSchoolAction.Cups,new Vector3(-16.4f,.65f,28.2f));yield return Close();Assert.That(game.narrative.HasSignal("bd.school.cups"),Is.True,"Legacy flag remains the same.");
   yield return Aim(flow.drawer.transform,new Vector3(-16.8f,.65f,28.1f));yield return Until(()=>game.interaction.Target==flow.drawer,"Reach preserved drawer");yield return Press(Key.E);yield return Until(()=>flow.drawer.OpenFraction>=.8f,"Drawer opens");
   yield return Inspect(BeforeDemolitionSchoolAction.Lunchbox,new Vector3(-16.8f,.65f,28.1f));int viewed=0;Action<string,int> record=(id,index)=>{if(id=="bd.lunchbox")viewed++;};inspection.PageViewed+=record;
   var ui=UnityEngine.Object.FindAnyObjectByType<InspectionUI>();ui.ToggleReader();yield return null;Assert.That(ui.ReaderOpen,Is.True);Assert.That(ui.PageIndex,Is.Zero);var next=ui.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="ReadBack");for(int i=0;i<2;i++){Assert.That(next.interactable,Is.True);next.onClick.Invoke();yield return null;}Assert.That(ui.PageIndex,Is.EqualTo(2));Assert.That(next.interactable,Is.False);Assert.That(viewed,Is.EqualTo(3));var page=ui.GetComponentsInChildren<UnityEngine.UI.RawImage>().Single(x=>x.name=="DrawingPage");Assert.That(page.texture.name,Is.EqualTo("inspect_lunchbox_draw_03"));yield return EvidenceShot("lunchbox-page-today");ui.CloseReader();inspection.PageViewed-=record;yield return Close();
   yield return ReadSecretaryPhoneGate();yield return Until(()=>whistle.HasResponded("bark.secretary.today.0"),"João reacts after reading the dated drawing",12);Assert.That(flow.keyVisual.activeInHierarchy,Is.True);yield return Inspect(BeforeDemolitionSchoolAction.Key,new Vector3(-16.8f,.65f,28.1f));
   ui.ToggleReader();yield return null;Assert.That(ui.GetComponentsInChildren<UnityEngine.UI.RawImage>(true).Single(x=>x.name=="DrawingPage").gameObject.activeSelf,Is.False,"A normal item must restore the original text reader after the drawing pages.");var text=ui.GetComponentsInChildren<TMPro.TMP_Text>().Single(x=>x.name=="ReaderText");Assert.That(text.gameObject.activeInHierarchy,Is.True);var reverse=ui.GetComponentsInChildren<UnityEngine.UI.Button>().Single(x=>x.name=="ReadBack");Assert.That(reverse.GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo("Verso"));Assert.That(reverse.interactable,Is.True);reverse.onClick.Invoke();Assert.That(text.text,Is.EqualTo(inspection.CurrentItem.backText));ui.CloseReader();yield return Close();Assert.That(game.interaction.inventory.Contains(BeforeDemolitionSchoolFlow.KeyId),Is.True);
   Assert.That(game.save.Save(),Is.True);Assert.That(game.save.TryReadCurrent(out var checkpoint),Is.True);var old=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(checkpoint));var note=old.inventory.Single(x=>x.id=="bd.cups");note.title="Quatro copos gelados";note.text="Quatro copos de suco. Ainda estão gelados. Um tem pequenas marcas de dentes na borda. Ninguém devia ter servido isso hoje.";int previousCount=note.inspectionCount;
   Assert.That(game.save.RestoreSnapshot(old),Is.True);game.narrative.InitializeAfterSession(true);yield return null;yield return null;Assert.That(game.interaction.inventory.Contains(BeforeDemolitionSchoolFlow.KeyId),Is.True);Assert.That(game.narrative.HasSignal("bd.school.cups")&&game.narrative.HasSignal("bd.school.lunchbox")&&game.narrative.HasSignal("bd.school.key"),Is.True);Assert.That(game.interaction.inventory.items.Single(x=>x.id=="bd.cups").title,Is.EqualTo("Pegadas pequenas na poeira"));Assert.That(game.interaction.inventory.items.Single(x=>x.id=="bd.cups").inspectionCount,Is.EqualTo(previousCount));Assert.That(whistle.HasResponded(SecretaryEvidence.FirstTapMarker),Is.True);Assert.That(flow.keyVisual.activeSelf,Is.False);
   File.WriteAllText(SecretaryProof+"play-evidence.json","{\"firstTap\":true,\"pages\":3,\"libraryKey\":true,\"legacyFlags\":true,\"noteMigration\":true,\"personalSaveTouched\":false}");
  }
 }
}
#endif
