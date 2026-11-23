// Verifica .

#if UNITY_EDITOR
using System;using System.Collections;using System.IO;using System.Linq;using NUnit.Framework;using TMPro;using UnityEngine;using UnityEngine.TestTools;using UnityEngine.InputSystem;using UnityEngine.UI;
namespace HorrorGame.Tests {
 public sealed partial class BeforeDemolitionSchoolSceneTests {
  [UnityTest,Category("FlowPropFix20261106"),Timeout(180000)]
  public IEnumerator GateClosesInsideRatsStayHiddenAndLegacyLibraryExitAdvances(){
   Assert.That(flow.entranceGate,Is.Not.Null);Assert.That(flow.entranceGate.IsOpen,Is.False);
   game.player.Warp(new Vector3(.56f,.65f,24.7f),0);yield return Until(()=>flow.entranceGate.locked&&!flow.entranceGate.IsOpen,"Gate closes after the player clears its swing");Assert.That(flow.entranceGate.pairedLeaf.IsOpen,Is.False);
   Assert.That(game.save.Save(),Is.True);Assert.That(game.save.TryReadCurrent(out var atGate),Is.True);Assert.That(atGate.objects.Single(o=>o.id==flow.entranceGate.persistentId).json,Does.Contain("\"locked\":true"));
   var phone = PhoneMessages.For(game);
   game.player.Warp(new Vector3(-16.4f, .65f, 28.2f), 0);
   yield return Until(() => game.narrative.HasSignal("bd.school.secretary"), "Secretary entry");
   yield return null;
   // The phone opens itself on arrival and temporarily owns player input.
   phone.Close();
   yield return Until(() => game.CanControl, "Dismiss the arriving phone message");
   yield return SecretaryAndKey();game.player.Warp(new Vector3(41.7f,.05f,55.8f),180);yield return Until(()=>game.narrative.HasSignal("bd.school.library"),"Enter library");
   yield return Inspect(BeforeDemolitionSchoolAction.Report,new Vector3(46.9f,.05f,46.0f));yield return Close();Assert.That(WhistleSystem.For(game).State.loreKnown,Is.False,"The optional mural was not read.");phone.Close();foreach(var receipt in phone.State.received){receipt.announced=receipt.read=true;}yield return Until(()=>game.CanControl,"Close phone");
   var rats=game.GetComponent<RatScurry>();Assert.That(rats.TryRun(),Is.True);var rat=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t=>t.name=="Rato — corrida pelo canto");Assert.That(rat.GetComponentsInChildren<Renderer>(true).All(r=>r.forceRenderingOff),Is.True);Assert.That(rat.GetComponents<AudioSource>().Any(a=>a.isPlaying&&a.spatialBlend==1),Is.True);yield return null;
   game.player.view.playerCamera.transform.LookAt(rat.position);yield return null;Assert.That(rat.GetComponentsInChildren<Renderer>(true).All(r=>r.forceRenderingOff),Is.True,"Facing the source must not reveal the rat.");
   Assert.That(game.narrative.HasSignal("bd.school.exit"),Is.False,"The recording is still required.");
   phone.Close();
   yield return Until(() => game.CanControl, "Dismiss phone before the recording");
   // Start the real inspection interaction. This regression targets recording completion and exiting the room.
   var tape = Target(BeforeDemolitionSchoolAction.Tape).GetComponent<InspectableItem>();
   Assert.That(tape && tape.CanInteract(game.interaction), Is.True);
   tape.Interact(game.interaction);
   yield return Until(() => inspection.IsOpen, "Tape inspection opens");
   var recording = inspection.CurrentItem.recording.clips.Single();
   Assert.That(recording.name, Is.EqualTo("fita_helena_2007"));
   yield return Press(Key.R);
   yield return Until(() => flow.TapeHeard, "Recording completes", recording.length + 8);
   yield return Close();
   Assert.That(WhistleSystem.For(game).State.loreKnown,Is.True);Assert.That(WhistleSystem.For(game).State.discovered,Is.False,"Listening does not blow the whistle for the player.");
   Assert.That(game.save.Save(),Is.True);Assert.That(game.save.TryReadCurrent(out var snapshot),Is.True);var legacy=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(snapshot));legacy.whistle.loreKnown=legacy.whistle.discovered=false;legacy.position=new Vector3(-2.75f,.68f,26.87f);
   Assert.That(game.save.RestoreSnapshot(legacy),Is.True);game.narrative.InitializeAfterSession(true);yield return Until(()=>game.narrative.HasSignal("bd.school.exit"),"Already outside a completed library must advance",15);Assert.That(WhistleSystem.For(game).State.loreKnown,Is.True);Assert.That(game.interaction.inventory.Contains(BeforeDemolitionSchoolFlow.KeyId),Is.True);Assert.That(flow.entranceGate.locked&&!flow.entranceGate.IsOpen,Is.True,"Saved gate stays closed after library restore.");
  }
 }
 public sealed partial class DesktopOfficePlayTests {
  [UnityTest,Category("FlowPropFix20261106"),Timeout(120000)]
  public IEnumerator AllMailRowsFitWithoutScrollingInNormalAndMaximizedWindows(){
   yield return Enter();var mail=pc.OpenApp("mail");yield return null;Canvas.ForceUpdateCanvases();
   for(int mode=0;mode<2;mode++){
    var list=mail.body.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Lista de emails");var scroll=list.GetComponentInChildren<ScrollRect>();var rows=scroll.content.GetComponentsInChildren<Button>().Where(b=>b.transform.parent==scroll.content).ToArray();var expected=pc.inbox.catalog.messages.Count(m=>pc.inbox.State.IsDelivered(m.id)&&(pc.inbox.ChronologicalOfficeReading||m.id!="email.02"||pc.State.Has("trash.email.02")));Assert.That(rows.Length,Is.EqualTo(expected));Assert.That(rows.Length,Is.GreaterThanOrEqualTo(6));Assert.That(scroll.content.rect.height-scroll.viewport.rect.height,Is.LessThanOrEqualTo(2.1f));
    foreach(var row in rows){var corners=new Vector3[4];((RectTransform)row.transform).GetWorldCorners(corners);foreach(var corner in corners){var p=scroll.viewport.InverseTransformPoint(corner);Assert.That(p.y,Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin-1),row.name);Assert.That(p.y,Is.LessThanOrEqualTo(scroll.viewport.rect.yMax+1),row.name);}}
    Assert.That(rows.Last().GetComponentsInChildren<TMP_Text>().Any(t=>!string.IsNullOrWhiteSpace(t.text)),Is.True);Assert.That(pc.ScreenTexture.width,Is.EqualTo(1024));Assert.That(pc.ScreenTexture.height,Is.EqualTo(768));Assert.That(pc.inbox.State.IsRead("email.07"),Is.False);
    if(mode==0){yield return Capture("mail-all-rows-no-scroll",false);mail.ToggleMaximize();yield return null;Canvas.ForceUpdateCanvases();}
   }
  }
 }
 public sealed partial class BeforeDemolitionRoadSceneTests {
  [UnityTest,Category("FlowPropFix20261106"),Timeout(180000)]
  public IEnumerator TorchRestsOnPassengerSeatAndEquipmentBoxIsHidden(){
   var box = flow.driver.carRoot.GetComponentsInChildren<Transform>(true).Single(t => t.name.StartsWith("Caixa de equipamentos —"));
   Assert.That(box.gameObject.activeSelf, Is.False);
   var torch = flow.driver.flashlightPickup.transform;
   Assert.That(torch.localPosition.y, Is.InRange(.30f, .85f));
   Assert.That(torch.localPosition.x, Is.GreaterThan(0));
   Assert.That(torch.GetComponentsInChildren<Renderer>().Any(r => r.enabled), Is.True);
   flow.Advance(3600);
   yield return Until(() => flow.Parked && game.CanControl, "Arrive at school");
   yield return Aim(BeforeDemolitionRoadAction.Flashlight);
   yield return KeyPress(Key.E);
   Assert.That(flow.HasTorch && flow.driver.Seated, Is.True);
  }
 }
}
#endif
