// Verifica .

#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    public sealed partial class BeforeDemolitionRoadSceneTests
    {
        [UnityTest,Category("PhonePickup"),Timeout(120000)]
        public IEnumerator PhoneCannotBePickedBeforeBuzzAndNotificationsWaitForOwnership()
        {
            var messages=PhoneMessages.For(game);Assert.That(messages.Acquired,Is.False);Assert.That(flow.phoneModel,Is.Not.Null);
            Assert.That(flow.CanInteract(BeforeDemolitionRoadAction.Phone),Is.False);Assert.That(messages.OpenDirect(),Is.False);
            yield return KeyPress(Key.P);Assert.That(messages.IsOpen||messages.BannerVisible,Is.False);
            Fast(20);flow.ignoredBuzzSeconds=flow.incomingCallSeconds=100;
            yield return Until(()=>flow.State.phase==RoadRadioPhase.Song,"First message vibrates the phone");
            Assert.That(flow.radio.VibrationCount,Is.GreaterThan(0));Assert.That(flow.CanInteract(BeforeDemolitionRoadAction.Phone),Is.True);
            yield return new WaitForSeconds(.5f);Assert.That(messages.BannerVisible,Is.False,"Queued texts are not shown before pickup");
            Assert.That(game.save.Save(),Is.True);Assert.That(game.save.TryReadCurrent(out var beforePickup),Is.True);Assert.That(beforePickup.phone.acquired,Is.False);
            yield return Aim(BeforeDemolitionRoadAction.Phone);yield return CapturePhone("ready-model-before-pickup");
            yield return KeyPress(Key.E);yield return Until(()=>messages.Acquired,"Physical E pickup");
            Assert.That(messages.IsOpen,Is.False,"Collecting reveals the banner without forcing the app open");
            yield return Until(()=>messages.BannerVisible,"Banner becomes eligible only after pickup");
            int vibrations=flow.radio.VibrationCount;flow.radio.Vibrate();yield return null;
            Assert.That(flow.radio.VibrationCount,Is.EqualTo(vibrations),"An owned phone uses normal notifications, never pickup vibration.");
            messages.BannerAnnounced();
            var audio=AudioManager.Instance;var notification=messages.catalog.notification;
            Assert.That(audio.GetComponentsInChildren<AudioSource>(true).Any(s=>s.isPlaying&&notification.clips.Contains(s.clip)),Is.True,"The banner plays the ordinary notification sound.");
            Assert.That(audio.GetComponentsInChildren<AudioSource>(true).Any(s=>s.isPlaying&&messages.catalog.vibration.clips.Contains(s.clip)),Is.False,"No pooled vibration accompanies a banner.");
            Assert.That(flow.phoneModel.activeSelf,Is.False);Assert.That(flow.phone.GetComponentsInChildren<Collider>(true).All(c=>!c.enabled),Is.True);
            Assert.That(game.save.Save(),Is.True);Assert.That(game.save.TryReadCurrent(out var afterPickup),Is.True);Assert.That(afterPickup.phone.acquired,Is.True);
            Assert.That(game.save.RestoreSnapshot(beforePickup),Is.True);game.narrative.InitializeAfterSession(true);yield return null;
            Assert.That(messages.Acquired||messages.BannerVisible,Is.False);Assert.That(flow.phoneModel.activeSelf,Is.True);
            Assert.That(game.save.RestoreSnapshot(afterPickup),Is.True);game.narrative.InitializeAfterSession(true);yield return null;
            Assert.That(messages.Acquired,Is.True);Assert.That(flow.phoneModel.activeSelf,Is.False);
            yield return KeyPress(Key.P);yield return Until(()=>messages.IsOpen,"Owned phone opens with P");
            messages.View.ResetWindowPosition();
            Assert.That(messages.View.Window.GetComponent<UnityEngine.UI.Image>(),Is.Null,"No black outer panel remains");
            Assert.That(((RectTransform)messages.View.Window.Find("Tela")).sizeDelta,Is.EqualTo(messages.View.Window.sizeDelta));
            yield return CapturePhone("borderless-phone");
        }
    }
}
#endif
