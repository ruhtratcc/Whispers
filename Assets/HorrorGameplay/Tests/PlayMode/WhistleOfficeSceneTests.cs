// Verifica .

#if UNITY_EDITOR
using System.Collections;using System.Linq;using NUnit.Framework;using UnityEngine;using UnityEngine.TestTools;
namespace HorrorGame.Tests
{
 public sealed partial class BeforeDemolitionOfficeSceneTests
 {
  [UnityTest,Category("Whistle"),Category("WhistleDiscovery"),Timeout(90000)]
  public IEnumerator WhistleNativeOfficeIsOnlyAnOrdinaryWhistle(){yield return OpenComputerWithE();yield return ReadEmail("email.06");yield return CloseInboxWithEscape();yield return OpenDrawerAndInspectWhistleWithE();var s=WhistleSystem.For(game);Assert.That(s&&s.CanUse,Is.True);var responders=UnityEngine.Object.FindObjectsByType<WhistleResponder>(FindObjectsInactive.Include);Assert.That(responders.All(r=>!r.enabled),Is.True);int responses=0;foreach(var r in responders)r.onResponse.AddListener(()=>responses++);Assert.That(s.BlowTap(),Is.True);yield return new WaitForSeconds(1.5f);Assert.That(responses,Is.Zero);Assert.That(s.State.triedOutside&&s.State.plainCommentPlayed,Is.True);Assert.That(s.State.discovered||s.State.loreKnown,Is.False);Assert.That(s.State.tension,Is.Zero);Assert.That(s.VisualAge,Is.Zero);Assert.That(s.State.inputHintShown,Is.False);Assert.That(game.save.Save(),Is.True);Assert.That(SaveSystem.TryReadCheckpoint(game.save.SavePath,out var saved,"Chapter1Office",BeforeDemolitionCampaign.StoryId),Is.True);Assert.That(saved.whistle.triedOutside,Is.True);yield return OpenComputerWithE();Assert.That(s.CanUse,Is.False);yield return CloseInboxWithEscape();}
 }
}
#endif
