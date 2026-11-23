// Verifica .

#if UNITY_EDITOR
using System.Collections;using System.IO;using System.Linq;using NUnit.Framework;using UnityEngine;using UnityEngine.TestTools;using UnityEngine.InputSystem;
namespace HorrorGame.Tests
{
 public sealed partial class BeforeDemolitionSchoolSceneTests
 {
  IEnumerator ReadWhistleLoreFromMural(){var item=UnityEngine.Object.FindObjectsByType<WhistleLoreClue>(FindObjectsInactive.Include).Single(c=>c.name.StartsWith("Anotação de Helena")).GetComponent<InspectableItem>();yield return Aim(item.transform,new Vector3(47.2f,.05f,45.35f));yield return Until(()=>game.interaction.Target==item,"Read the required whistle note through normal interaction");yield return Press(Key.E);yield return Until(()=>inspection.IsOpen,"Whistle note inspection");yield return Close();Assert.That(WhistleSystem.For(game).State.loreKnown,Is.True);}

  [UnityTest,Category("WhistleDiscovery"),Timeout(120000)]
  public IEnumerator LoreInspectionUnlocksSchoolDiscoveryHighlightsAndCups(){
   var container=UnityEngine.Object.FindObjectsByType<WhistleHighlightable>(FindObjectsInactive.Include).Single(h=>h.GetComponent<CollectibleItem>()&&h.GetComponent<CollectibleItem>().itemId=="whistle.school.battery.secretary");Assert.That(container.ContentsConcealed,Is.False,"X-ray reveals the battery even inside a closed drawer.");var tool=WhistleSystem.For(game);var sense=game.GetComponent<WhistleSense>();Assert.That(tool.State.discovered||tool.State.loreKnown,Is.False);Assert.That(tool.BlowTap(),Is.True);Assert.That(tool.State.tension,Is.Zero);Assert.That(tool.VisualAge,Is.Zero);Assert.That(sense.ActiveCount,Is.Zero);
   yield return SecretaryAndKey();Assert.That(container.ContentsConcealed,Is.False,"Opening drawer switches to actual battery.");Assert.That(tool.State.secretaryWhispered,Is.True);Assert.That(tool.State.discovered,Is.False);
   var clues=UnityEngine.Object.FindObjectsByType<WhistleLoreClue>(FindObjectsInactive.Include);Assert.That(clues.Length,Is.EqualTo(3));var clue=clues.Single(c=>c.name.StartsWith("Anotação de Helena"));var item=clue.GetComponent<InspectableItem>();Assert.That(item.item.frontText,Is.EqualTo("O Luan tinha um apito. Era o sinal deles. Quando a luz acabava, ele apitava e os outros respondiam. Se eu estivesse lá, eu teria ouvido."));
   yield return Aim(clue.transform,new Vector3(47.2f,.05f,45.35f));yield return Until(()=>game.interaction.Target==item,"Lore is reachable by real raycast: "+game.interaction.CurrentPrompt);yield return Press(Key.E);yield return Until(()=>inspection.IsOpen,"Lore ordinary inspection");yield return Close();Assert.That(tool.HasFlag(WhistleSystem.LoreFlag),Is.True);Assert.That(game.interaction.inventory.Contains(item.item.itemId),Is.True);Assert.That(tool.State.discovered,Is.False);
   tool.State.cooldownRemaining=0;Assert.That(tool.BlowTap(),Is.True);Assert.That(tool.SupernaturalActive,Is.True);var profile=BeforeDemolitionChapterFixture.Private<UnityEngine.Rendering.VolumeProfile>(tool,"pulseProfile");profile.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out var grade);game.atmosphere.volume.sharedProfile.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out var originalGrade);Assert.That(grade.saturation.value,Is.LessThan(originalGrade.saturation.value),"Pulse must desaturate relative to the existing school grade.");Assert.That(game.GetComponent<WhistleSchoolDiscovery>().answerAnchors.All(t=>Vector3.Distance(t.position,tool.PulseOrigin)<5),Is.True);Assert.That(sense.ActiveCount,Is.GreaterThan(0));Assert.That(sense.BufferSaturated,Is.False);Assert.That(tool.VisualAge,Is.GreaterThan(3));yield return new WaitForSeconds(.25f);
   string dir="C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/WhistleXray20261031";Directory.CreateDirectory(dir);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(dir+"/library-discovery.png");yield return null;
   Assert.That(game.save.Save(),Is.True);Assert.That(SaveSystem.TryReadCheckpoint(game.save.SavePath,out var saved,"SchoolCEEP",BeforeDemolitionCampaign.StoryId),Is.True);Assert.That(saved.whistle.discovered&&saved.whistle.loreKnown,Is.True);tool.Restore(saved.whistle);Assert.That(tool.SupernaturalActive,Is.True);
   var evidence=flow.GetComponent<SecretaryEvidence>();Assert.That(evidence.footprints.Length,Is.EqualTo(4));Assert.That(saved.whistle.responders.Contains(SecretaryEvidence.FirstTapMarker),Is.True,"The earlier local evidence response is saved without unlocking the global lore early.");Assert.That(UnityEngine.Object.FindObjectsByType<WhistleResponder>(FindObjectsInactive.Include).Any(r=>r.responderId=="whistle.school.cups"),Is.False,"The retired glass response must never return after the library discovery.");

  }
 }
}
#endif
