// Verifica .

#if UNITY_EDITOR
using System.Collections;using NUnit.Framework;using UnityEngine;using UnityEngine.TestTools;
namespace HorrorGame.Tests{
 public sealed partial class BeforeDemolitionRoadSceneTests{
  [UnityTest,Category("Whistle"),Timeout(90000)]public IEnumerator WhistleNativeRoadDisabledInCarAndAllowedOutside(){var tool=WhistleSystem.For(game);Assert.That(tool,Is.Not.Null);Assert.That(flow.driver.Seated,Is.True);Assert.That(tool.CanUse||tool.BlowTap(),Is.False);Fast(.2f);yield return Until(()=>flow.Parked,"Arrival",20);yield return Aim(BeforeDemolitionRoadAction.Flashlight);yield return KeyPress(UnityEngine.InputSystem.Key.E);yield return OpenMessages();Assert.That(tool.CanUse||tool.BlowTap(),Is.False,"The actual phone modal blocks the whistle.");yield return CloseMessages();yield return Aim(BeforeDemolitionRoadAction.Exit);yield return KeyPress(UnityEngine.InputSystem.Key.E);yield return Until(()=>!flow.driver.Seated&&tool.CanUse,"Whistle allowed only after leaving the car");Assert.That(tool.BlowTap(),Is.True);Assert.That(flow.radio.RadioSource.isPlaying,Is.True,"Haunted radio continues outside the car until school entry.");}
 }
}
#endif
