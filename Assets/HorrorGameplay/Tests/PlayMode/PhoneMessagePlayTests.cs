// Verifica .

#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    public sealed partial class BeforeDemolitionRoadSceneTests
    {
        [UnityTest,Category("PhoneMessagesPlay"),Timeout(120000)]
        public IEnumerator IncomingMessagesOpenAutomaticallyAfterSafeModalAndTheComposerNeverTypes()
        {
            var phone=PhoneMessages.For(game);Assert.That(phone,Is.Not.Null);
            Fast(20);flow.ignoredBuzzSeconds=flow.incomingCallSeconds=100;
            yield return Until(()=>flow.State.phase==RoadRadioPhase.Song,"Vibration before pickup");
            yield return Aim(BeforeDemolitionRoadAction.Phone);yield return KeyPress(Key.E);
            yield return Until(()=>phone.IsOpen,"Picking up the phone opens the waiting conversation",15);
            Assert.That(phone.Acquired,Is.True);Assert.That(phone.BannerVisible,Is.False);
            phone.Close();yield return Until(()=>game.CanControl,"Close initial conversation");
            game.Pause();phone.Trigger(PhoneTrigger.Secretary);yield return new WaitForSecondsRealtime(.15f);
            Assert.That(phone.IsOpen,Is.False);Assert.That(phone.State.received.Any(m=>!m.announced),Is.True);
            game.Resume();yield return Until(()=>phone.IsOpen,"Incoming message automatically opens after pause",15);
            Assert.That(game.hud.crosshair.gameObject.activeSelf,Is.False);
            Assert.That(phone.View.ComposerButton.interactable,Is.False);
            UnityEngine.EventSystems.ExecuteEvents.Execute(phone.View.ComposerButton.gameObject,new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current),UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            phone.ClickComposer();yield return new WaitForSeconds(.4f);
            Assert.That(phone.State.composerUsed,Is.False);
            Assert.That(phone.View.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("o senhor não vai responder")),Is.False);
            yield return KeyPress(Key.F10);Assert.That(game.State,Is.EqualTo(GameState.Paused));yield return null;
            yield return KeyPress(Key.Escape);Assert.That(phone.IsOpen,Is.True);
            Assert.That(game.save.Save(),Is.True);Assert.That(game.save.TryReadCurrent(out var saved),Is.True);
            int count=saved.phone.received.Count;saved.phone.composerUsed=true;
            phone.Restore(null);Assert.That(game.save.RestoreSnapshot(saved),Is.True);game.narrative.InitializeAfterSession(true);yield return null;
            Assert.That(phone.State.received.Count,Is.EqualTo(count));Assert.That(phone.State.composerUsed,Is.True,"Old checkpoints remain compatible");
            Assert.That(phone.OpenDirect(),Is.True);yield return null;yield return CapturePhone("conversation-auto-open");
            phone.ClickComposer();yield return new WaitForSeconds(.4f);
            Assert.That(phone.View.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("o senhor não vai responder")),Is.False);
            phone.Close();yield return Until(()=>game.CanControl,"Close restored phone");
            Assert.That(game.BeginInspection(),Is.True);phone.Trigger(PhoneTrigger.Marta);yield return null;
            Assert.That(phone.IsOpen,Is.False);game.EndInspection();yield return Until(()=>phone.IsOpen,"Inspection defers automatic opening",15);
        }
        IEnumerator CapturePhone(string name)
        {
            Directory.CreateDirectory(Visual);string path=Path.Combine(Visual,name+"-screen.png");
            if(File.Exists(path))File.Delete(path);yield return null;yield return null;ScreenCapture.CaptureScreenshot(path);
            yield return Until(()=>File.Exists(path)&&new FileInfo(path).Length>0,"Phone screenshot",5);
        }
    }

    public sealed partial class BeforeDemolitionSchoolSceneTests
    {
        [UnityTest,Category("PhoneMessagesPlay"),Timeout(120000)]
        public IEnumerator SecretaryEntryDeliversUnknownMessagesAndThePhoneCheckpointKeepsReads()
        {
            var phone=PhoneMessages.For(game);Assert.That(phone,Is.Not.Null);
            game.player.Warp(new Vector3(-16.4f,.65f,28.2f),0);
            yield return Until(()=>game.narrative.HasSignal("bd.school.secretary"),"Actual secretary entry");
            Assert.That(phone.State.received.Any(m=>m.id=="secretary.hello"),Is.True);
            Assert.That(phone.Acquired,Is.True,"A legacy school checkpoint must retain access to the already-owned phone.");
            yield return Until(()=>phone.IsOpen,"School message opens automatically");
            Assert.That(flow.inbox.IsOpen,Is.False);Assert.That(flow.inbox.State.IsDelivered("email.08"),Is.False);
            Assert.That(phone.View.GetComponentsInChildren<UnityEngine.UI.RawImage>().Any(t=>t.texture&&t.texture.name=="foto_porta_secretaria"),Is.True,"The delivered secretary photo replaces the old missing-image placeholder.");
            Assert.That(phone.View.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains(phone.catalog.noSignal)||t.text.Contains("76%")||t.text.Contains("Fechar [")),Is.False,"Status and shortcut hints are hidden.");
            Assert.That(GameClock.Instance.TimeText,Is.EqualTo("03:07"));yield return Press(Key.P);yield return Until(()=>game.CanControl,"P releases school input");
            Assert.That(game.save.Save(),Is.True);Assert.That(game.save.TryReadCurrent(out var saved),Is.True);
            Assert.That(saved.phone.received.Any(m=>m.id=="secretary.hello"&&m.read),Is.True);
        }
    }
    public sealed partial class BeforeDemolitionFourStudentsTests
    {
        [UnityTest,Category("PhoneMessagesPlay"),Timeout(120000)]
        public IEnumerator ActualCorridorRepetitionsTriggerTheTwoMessageGroups()
        {
            yield return EnterG();var loop=game.GetComponent<BeforeDemolitionTwoVoicesFlow>();var phone=PhoneMessages.For(game);
            phone.enabled=true;
            for(int count=1;count<=2;count++)
            {
                yield return Until(()=>{if(phone.IsOpen)phone.Close();return game.CanControl&&!game.narrative.SequencePlaying;},"Corridor allows the next crossing",30);
                yield return new WaitForSeconds(1.4f);
                if(phone.IsOpen)phone.Close();
                var outside=loop.loopVolume.bounds.max+Vector3.right*2;outside.y=loop.returnPose.position.y;
                game.player.Warp(outside,0);Physics.SyncTransforms();yield return null;yield return null;
                game.player.Warp(loop.loopVolume.bounds.center-Vector3.up*.8f,0);Physics.SyncTransforms();
                int expected=count;yield return Until(()=>loop.State.loopCount==expected,"Real loop volume repetition "+count,10);
            }
            foreach(string id in new[]{"loop.left","loop.child","loop.photo"})Assert.That(phone.State.received.Any(m=>m.id==id),Is.True,id);
            Assert.That(phone.State.Has("loop.whistle"),Is.True,"The typing message is delivered or saved pending.");
            yield return Until(()=>{if(phone.IsOpen)phone.Close();return game.CanControl&&!game.narrative.SequencePlaying;},"Open phone after corridor voices",30);
            if(!phone.IsOpen)yield return Press(Key.P);yield return Until(()=>phone.IsOpen,"Corridor phone shortcut");
            yield return Shot("phone-loop-conversation");yield return Press(Key.P);
        }
    }
}
#endif
