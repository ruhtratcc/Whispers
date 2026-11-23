// Verifica .

#if UNITY_EDITOR
using System.Collections;using NUnit.Framework;using UnityEngine;using UnityEngine.TestTools;using UnityEngine.InputSystem;
namespace HorrorGame.Tests{public sealed partial class BeforeDemolitionRoadSceneTests{
[UnityTest,Category("Polish20261103Native"),Timeout(90000)]public IEnumerator RadioDistanceAttenuatesAndOnlyCrossingGateStartsFade(){
 Fast(.2f);yield return Until(()=>flow.Parked,"Arrival",20);yield return Aim(BeforeDemolitionRoadAction.Flashlight);yield return KeyPress(Key.E);yield return OpenMessages();yield return CloseMessages();yield return Aim(BeforeDemolitionRoadAction.Exit);yield return KeyPress(Key.E);yield return Until(()=>!flow.driver.Seated&&game.CanControl,"Exit");
 var anchor=flow.radio.radioAnchor.position;game.player.Warp(anchor+Vector3.back*2,0);flow.radio.Tick(true,.1f);float near=flow.radio.RadioSource.volume;
 game.player.Warp(anchor+Vector3.right*27,0);flow.radio.Tick(true,.1f);Assert.That(flow.radio.RadioSource.volume,Is.LessThan(near));Assert.That(flow.radio.RadioSource.rolloffMode,Is.EqualTo(AudioRolloffMode.Logarithmic));
 var gate=flow.driver.gate.transform;game.player.Warp(gate.position-gate.forward,0);flow.Advance(.01f);flow.radio.Tick(true,2);Assert.That(flow.radio.EntryFaded,Is.False,"Standing one metre before the gate is outside.");Assert.That(flow.radio.RadioSource.isPlaying,Is.True);
 game.player.Warp(gate.position+gate.forward*.2f,0);flow.Advance(.01f);flow.radio.Tick(true,1);game.DisableControl();Assert.That(flow.radio.EntryFaded,Is.True);Assert.That(flow.radio.RadioSource.isPlaying,Is.False);
}
}}
#endif
