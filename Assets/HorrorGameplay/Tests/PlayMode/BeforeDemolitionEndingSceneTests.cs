// Verifica .

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace HorrorGame.Tests
{
    public sealed partial class BeforeDemolitionFourStudentsTests
    {
        const string EndingVisual="C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/WhistleSchoolDiscovery20261031/Endings";
        StoryBook temporaryBook;
        IEnumerator Press(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;}
        IEnumerator Shot(string name)
        {Directory.CreateDirectory(EndingVisual);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(EndingVisual,name+".png"));yield return null;yield return null;}
        IEnumerator AimAt(Vector3 point,Vector3 feet)
        {game.player.Warp(feet,0);Physics.SyncTransforms();yield return null;var d=point-game.player.view.playerCamera.transform.position;game.player.Warp(feet,Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);Physics.SyncTransforms();yield return null;yield return null;}
        [UnityTest,Category("BeforeDemolitionFinalPhoto"),Timeout(120000)]
        public IEnumerator CaptureTheEmptyDeskForTheFinalEmail()
        {
            var desk=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t=>t.name=="Carteira vazia — JOÃO");
            game.hud.gameObject.SetActive(false);game.save.flashlight.Restore(240,true);
            yield return AimAt(desk.position+Vector3.up*.65f,desk.position+new Vector3(0,.05f,1.55f));
            yield return new WaitForSeconds(.4f);yield return Shot("final-email-desk");
            Assert.That(File.Exists(Path.Combine(EndingVisual,"final-email-desk.png")),Is.True);
        }
        [UnityTest,Timeout(120000)]public IEnumerator BiaRecorderStartsInTheRoomWithoutGrantingFullPlaybackOrGathering()
        {
            game.player.Warp(flow.studentVisuals[3].transform.position+new Vector3(1,-.85f,0),0);Physics.SyncTransforms();
            yield return Until(()=>game.interaction.inventory.Contains("bd.event.bia-preview"),"The room recorder starts without interaction");
            Assert.That(flow.TapeHeard||flow.Gathered,Is.False);
            Assert.That(game.interaction.inventory.Contains(BeforeDemolitionFourStudentsFlow.BiaTapeId),Is.False);
            Assert.That(game.save.Save(),Is.True,"Preview marker remains a legal checkpoint");
        }
        IEnumerator EnterG()
        {
            temporaryBook=UnityEngine.Object.Instantiate(game.narrative.book);game.narrative.book=temporaryBook;
            foreach(var sequence in temporaryBook.sequences)foreach(var line in sequence.lines)if(!line.voice)line.seconds=.08f;
            foreach(string id in BeforeDemolitionFourStudentsFlow.StudentItemIds)Add(id);
            Add(BeforeDemolitionFourStudentsFlow.BiaTapeHeardId);Add(BeforeDemolitionFourStudentsFlow.StairKeyId);
            var p=SchoolPrefix();p.chapterId=BeforeDemolitionCampaign.TwoVoicesChapterId;p.completedChapters.Add(BeforeDemolitionCampaign.FourChapterId);
            p.milestones.AddRange(new[]{"bd.four.gathered","bd.four.key","bd.four.upstairs"});p.sequences.Add("bd.four.intercom");
            game.player.Warp(new Vector3(-15.45f,3.86f,63.3f),0);game.narrative.RestoreProgress(p);game.narrative.InitializeAfterSession(true);
            yield return Until(()=>game.CanControl&&!game.narrative.SequencePlaying,"G starts and its first Helena line finishes");
            Assert.That(game.save.Save(),Is.True,"Actual G0 world is a valid persisted checkpoint");
            AudioPassSceneAudit.Check(game,"05-two-voices");
        }
        IEnumerator ReachTheMirror()
        {
            yield return EnterG();var learned=WhistleSystem.For(game);learned.State.loreKnown=learned.State.discovered=learned.State.schoolHesitated=true;var g=game.GetComponent<BeforeDemolitionTwoVoicesFlow>();
            g.sideDoor.Interact(game.interaction);yield return new WaitForSeconds(1.4f);
            game.player.Warp(new Vector3(-15.4f,3.86f,71.59568f),270);Physics.SyncTransforms();
            for(int i=0;i<42;i++){game.player.Controller.Move(Vector3.left*.1f+Vector3.down*.02f);if(i%3==2)yield return null;}
            Assert.That(game.player.transform.position.x,Is.LessThan(-17.8f),"The actual capsule crosses the open west doorway");
            yield return Until(()=>g.Escaped,"West room entry accepts escape");Assert.That(g.tapeItem.gameObject.activeInHierarchy,Is.True);
            yield return AimAt(g.tapeItem.transform.position,new Vector3(-20.3f,3.86f,69.4f));
            yield return Until(()=>game.interaction.Target==g.tapeItem,"Tape3 receives a real raycast: "+game.interaction.CurrentPrompt,3);
            yield return Press(Key.E);yield return Until(()=>inspection.IsOpen,"Tape3 ordinary inspection");
            var recording = inspection.CurrentItem.recording.clips.Single();
            bool finished = false;
            void Complete(string id) => finished |= id == BeforeDemolitionTwoVoicesFlow.TapeId;
            inspection.RecordingCompleted += Complete;
            try
            {
                yield return Press(Key.R);yield return Until(()=>inspection.RecordingPlaying,"Tape3 starts");
                yield return Until(()=>finished,"Tape3 emits its completion event",recording.length+8);
            }
            finally { inspection.RecordingCompleted -= Complete; }
            yield return Close();yield return Until(()=>g.TapeHeard,"Tape3 full playback commits after pickup");
            Assert.That(g.mirrorDoor.locked,Is.False);Assert.That(game.save.Save(),Is.True);
            g.mirrorDoor.Interact(game.interaction);yield return new WaitForSeconds(1.4f);
            game.player.Warp(new Vector3(-15.3f,3.86f,73.68596f),90);Physics.SyncTransforms();
            for(int i=0;i<42;i++){game.player.Controller.Move(Vector3.right*.1f+Vector3.down*.02f);if(i%3==2)yield return null;}
            Assert.That(game.player.transform.position.x,Is.GreaterThan(-13.0f),"The actual capsule crosses the mirror-room doorway");
            var h=UnityEngine.Object.FindAnyObjectByType<BeforeDemolitionMirrorFlow>();
            yield return AimAt(h.transform.position,new Vector3(h.transform.position.x,3.86f,h.transform.position.z-1.53f));
            yield return Until(()=>h.IsActive&&game.CanControl,"G hands off to H without reloading the school",12);
            AudioPassSceneAudit.Check(game,"06-mirror");
            yield return Shot("mirror-before");yield return Until(()=>h.Data.stage==1,"The real watched mirror produces its single scare",12);
            yield return Shot("mirror-smile");
            yield return Until(()=>game.interaction.Target==h,"Mirror is reachable by ordinary E raycast: "+game.interaction.CurrentPrompt,3);
            yield return Press(Key.E);Assert.That(h.Data.stage,Is.EqualTo(1),"E only explains the long blow now.");
            var tool=WhistleSystem.For(game);Assert.That(tool.BlowTap(),Is.True);yield return new WaitForSeconds(.7f);Assert.That(h.Data.stage,Is.EqualTo(1),"Tap never breaks the mirror.");tool.State.cooldownRemaining=0;tool.ToggleEquipped();
            var mouse=InputSystem.AddDevice<Mouse>();try{game.input.Actions.devices=new InputDevice[]{keyboard,mouse};InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Middle));yield return Until(()=>h.Data.stage==2,"Sustained whistle breaks the actual mirror",6);InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;}
            finally{game.input.Actions.devices=new InputDevice[]{keyboard};if(mouse.added)InputSystem.RemoveDevice(mouse);}
            yield return Until(()=>h.endingView&&h.endingView.IsOpen,"Offer finishes and opens the real choice",10);
            Assert.That(h.helenaVisual.activeInHierarchy,Is.True);Assert.That(h.finalPhoto,Is.Not.Null,"Final mail requires the authored photo");
            yield return Shot("mirror-choice");
        }
        IEnumerator CompleteBranch(BeforeDemolitionEndingChoice choice)
        {
            yield return ReachTheMirror();var h=UnityEngine.Object.FindAnyObjectByType<BeforeDemolitionMirrorFlow>();
            BeforeDemolitionChapterFixture.Click(choice==BeforeDemolitionEndingChoice.Refuse?h.endingView.RefuseButton:h.endingView.SignButton);
            yield return Until(()=>h.Data.stage==3&&h.endingView&&h.endingView.IsOpen,"Selected branch reaches its epilogue",10);
            var phone=PhoneMessages.For(game);string finalId=choice==BeforeDemolitionEndingChoice.Refuse?"ending.refuse":"ending.sign";
            Assert.That(phone.State.received.Count(m=>m.id==finalId),Is.EqualTo(1));
            BeforeDemolitionChapterFixture.Click(h.endingView.MessageButton);
            yield return Until(()=>phone.IsOpen,"The epilogue can display its actual final text conversation");
            Assert.That(phone.View.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text==phone.catalog.sender||t.text.Contains(phone.catalog.sender)),Is.True);
            yield return Press(Key.P);yield return Until(()=>!phone.IsOpen,"Return to the epilogue without releasing its inspection lock");
            Assert.That(game.State,Is.EqualTo(GameState.Inspecting));Assert.That(h.endingView.IsOpen,Is.True);
            Assert.That(h.Data.choice,Is.EqualTo(choice));Assert.That(game.save.Save(),Is.True,"Selected branch saves to disposable disk");
            Assert.That(game.save.TryReadCurrent(out var saved),Is.True);Assert.That(saved.narrative.endingSeen,Is.False);
            h.endingView.Close();PlayerPrefs.SetInt(Prefix+"ResumeRequested",1);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Single));
            yield return Until(()=>GameSession.Instance&&GameSession.Instance.narrative.IsInitialized,"Actual scene reload",35);
            game=GameSession.Instance;h=UnityEngine.Object.FindAnyObjectByType<BeforeDemolitionMirrorFlow>();
            yield return Until(()=>h.endingView&&h.endingView.IsOpen,"Saved epilogue reopens without replaying the scare",12);
            Assert.That(h.Data.choice,Is.EqualTo(choice));Assert.That(h.Data.stage,Is.EqualTo(3));Assert.That(h.glassVisual.activeSelf,Is.False);
            Assert.That(h.daylight.enabled,Is.EqualTo(choice==BeforeDemolitionEndingChoice.Refuse));
            yield return Shot("ending-"+choice.ToString().ToLowerInvariant());
            BeforeDemolitionChapterFixture.Click(h.endingView.ContinueButton);
            yield return Until(()=>game.narrative.Progress.endingSeen,"Director finishes the chosen branch");
            Assert.That(game.narrative.Progress.completedChapters.Count,Is.EqualTo(6));Assert.That(game.save.TryReadCurrent(out var finished),Is.True);
            Assert.That(finished.narrative.endingSeen,Is.True);Assert.That(finished.narrative.pendingChapterId,Is.Empty);
            yield return null;AudioPassSceneAudit.Check(game,"final-"+choice.ToString().ToLowerInvariant());
        }
        [UnityTest,Category("BeforeDemolitionEndingScene"),Timeout(120000)]public IEnumerator ActualSchoolRefuseEndingSurvivesSceneReload(){yield return CompleteBranch(BeforeDemolitionEndingChoice.Refuse);}
        [UnityTest,Category("BeforeDemolitionEndingScene"),Timeout(120000)]public IEnumerator ActualSchoolSignEndingSurvivesSceneReload(){yield return CompleteBranch(BeforeDemolitionEndingChoice.Sign);}
        [UnityTearDown]public IEnumerator ReleaseTemporaryBook(){if(temporaryBook)UnityEngine.Object.Destroy(temporaryBook);yield return null;}
    }
}
#endif


