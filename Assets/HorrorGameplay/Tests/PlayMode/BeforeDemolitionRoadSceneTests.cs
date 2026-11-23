// Verifica .

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HorrorGame.Tests
{
    [Category("BeforeDemolitionRoad")]
    public sealed partial class BeforeDemolitionRoadSceneTests
    {
        const string ScenePath="Assets/RoadChapter/Scenes/RoadJourney.unity";
        const string Prefix="BeforeDemolition.";
        const string Visual="C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RoadMix20261030/Road";
        [Serializable] sealed class OfficeState{public EmailInboxState emails;}
        string temporary,previousDirectory,oldSaveScene;
        bool guarded,hadSaveScene,background,oldAudio,cursorVisible;
        float timeScale;
        CursorLockMode cursorLock;
        InputSettings.BackgroundBehavior oldBackground;
        InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        readonly Dictionary<string,(bool,int)> oldFlags=new Dictionary<string,(bool,int)>();
        Keyboard keyboard,previousKeyboard;
        GameSession game;
        BeforeDemolitionRoadFlow flow;
        int delivered,read;
        readonly List<AudioClip> transientClips=new List<AudioClip>();

        [UnitySetUp]public IEnumerator SetUp()
        {
            guarded=false;oldFlags.Clear();delivered=read=0;
            Assert.That(UnityEngine.Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None),Is.Empty,"Launch from an isolated blank scene.");
            string raw=File.ReadAllText(ScenePath);
            string flowGuid=AssetDatabase.AssetPathToGUID("Assets/HorrorGameplay/Scripts/Narrative/BeforeDemolitionRoadFlow.cs");
            string bookGuid=AssetDatabase.AssetPathToGUID("Assets/HorrorGameplay/Data/BeforeDemolition/BeforeDemolitionStory.asset");
            Assert.That(raw.Contains("guid: "+flowGuid)&&raw.Contains("guid: "+bookGuid)&&raw.Contains("externalControl: 1"),Is.True,"Stop before Awake if the new profile is not authored.");
            previousDirectory=SaveSystem.TestSaveDirectory;timeScale=Time.timeScale;background=Application.runInBackground;oldAudio=AudioListener.pause;
            cursorLock=Cursor.lockState;cursorVisible=Cursor.visible;oldBackground=InputSystem.settings.backgroundBehavior;oldEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;previousKeyboard=Keyboard.current;
            foreach(string suffix in new[]{"ResumeRequested","StoryCarryRequested"})oldFlags[suffix]=(PlayerPrefs.HasKey(Prefix+suffix),PlayerPrefs.GetInt(Prefix+suffix,0));
            hadSaveScene=PlayerPrefs.HasKey(Prefix+"SaveScene");oldSaveScene=PlayerPrefs.GetString(Prefix+"SaveScene","");guarded=true;
            temporary=Path.Combine(Path.GetTempPath(),"Whispers-RoadScene-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temporary);SaveSystem.TestSaveDirectory=temporary; TutorialPreferences.Set(TutorialMode.Off);SaveSystem.ClearChapterCarry();
            File.WriteAllText(SaveSystem.PathForFile(BeforeDemolitionCampaign.CheckpointFile),JsonUtility.ToJson(CompletedOffice()));
            PlayerPrefs.SetInt(Prefix+"ResumeRequested",0);PlayerPrefs.SetInt(Prefix+"StoryCarryRequested",1);
            Time.timeScale=1;AudioListener.pause=false;Application.runInBackground=true;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard=InputSystem.AddDevice<Keyboard>();Press();SceneManager.sceneLoaded+=Loaded;
            yield return LoadRoad();
        }
        SaveData CompletedOffice()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<EmailCatalog>("Assets/HorrorGameplay/Data/BeforeDemolition/BeforeDemolitionOfficeEmails.asset");
            var emails=EmailInboxState.Create(catalog);foreach(string id in new[]{"email.01","email.06","email.07"})emails.MarkRead(catalog,id);
            var data=new SaveData{scene="Chapter1Office",position=new Vector3(-1.28f,.04f,-2.45f),battery=240,stamina=100,
                narrative=new NarrativeProgress{storyId=BeforeDemolitionCampaign.StoryId,storyRevision=3,chapterId=BeforeDemolitionCampaign.OfficeChapterId,pendingChapterId=BeforeDemolitionCampaign.RoadChapterId,
                    milestones=new List<string>{BeforeDemolitionCampaign.ReadySignal,BeforeDemolitionCampaign.ExitSignal},completedChapters=new List<string>{BeforeDemolitionCampaign.OfficeChapterId}},
                inventory=new List<InventoryItem>{new InventoryItem{id=BeforeDemolitionCampaign.WhistleItemId,title="Apito do Luan",inspectionId=BeforeDemolitionCampaign.WhistleItemId,note=true}},
                objects=new List<SavedObject>{new SavedObject{id=BeforeDemolitionCampaign.FlowStateId,json=JsonUtility.ToJson(new OfficeState{emails=emails})}}};
            foreach(string id in emails.readIds)data.inventory.Add(new InventoryItem{id="bd.note."+id,title=id,text=catalog.Find(id).PlainText,note=true});return data;
        }
        void Loaded(Scene scene,LoadSceneMode mode)
        {
            foreach(var session in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameSession>(true)))
            {
                Assert.That(session.narrative&&BeforeDemolitionCampaign.Is(session.narrative.book),Is.True);
                if(session.input&&session.input.Actions)session.input.Actions.devices=new InputDevice[]{keyboard};
                if(session.narrative.transitionView){session.narrative.transitionView.fadeSeconds=.03f;session.narrative.transitionView.cardSeconds=.05f;}
                var road=session.GetComponent<BeforeDemolitionRoadFlow>();
                if(road&&road.inbox){road.inbox.MessageDelivered+=id=>delivered++;road.inbox.ReadFlagRaised+=id=>read++;}
            }
        }
        IEnumerator LoadRoad()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Single));
            yield return Until(()=>GameSession.Instance&&GameSession.Instance.narrative.IsInitialized&&GameSession.Instance.CanControl,"Road initialized",25);
            game=GameSession.Instance;flow=game.GetComponent<BeforeDemolitionRoadFlow>();
            Assert.That(flow&&flow.IsActive&&flow.driver.Seated,Is.True);Assert.That(Path.GetDirectoryName(game.save.SavePath),Is.EqualTo(temporary).IgnoreCase);
            Assert.That(game.narrative.Progress.chapterId,Is.EqualTo(BeforeDemolitionCampaign.RoadChapterId));
            Assert.That(game.interaction.inventory.Contains(BeforeDemolitionCampaign.WhistleItemId),Is.True);
            foreach(string id in new[]{"email.01","email.06","email.07"})Assert.That(flow.inbox.State.IsRead(id),Is.True);
            game.player.view.bobEnabled=game.player.view.swayEnabled=false;yield return null;
        }
        static IEnumerator Until(Func<bool> predicate,string message,float timeout=12)
        {float start=Time.realtimeSinceStartup;while(!predicate()&&Time.realtimeSinceStartup-start<timeout)yield return null;Assert.That(predicate(),Is.True,message);}
        void Press(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        // Match the established input fixture: retain the press across two player-loop updates.
        IEnumerator KeyPress(Key key){Press(key);yield return null;yield return null;Press();yield return null;yield return null;}
        BeforeDemolitionRoadInteraction Target(BeforeDemolitionRoadAction action)=>UnityEngine.Object.FindObjectsByType<BeforeDemolitionRoadInteraction>(FindObjectsSortMode.None).Single(t=>t.action==action);
        IEnumerator Aim(BeforeDemolitionRoadAction action)
        {
            var target=Target(action);
            if(flow.driver.Seated)flow.driver.GlanceAtExternalPhone(target.transform,1);
            else
            {
                Vector3 center=target.GetComponentsInChildren<Collider>().First(c=>c.enabled).bounds.center;
                Vector3 delta=center-game.player.view.playerCamera.transform.position;
                game.player.Warp(game.player.transform.position,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg);
            }
            Physics.SyncTransforms();yield return Until(()=>game.interaction.Target is BeforeDemolitionRoadInteraction t&&t.action==action,"Ray targets "+action,4);
        }
        void Fast(float song=4)
        {flow.radio.news=null;flow.radio.newsSeconds=.15f;flow.radio.announcementSeconds=.12f;flow.radio.silenceSeconds=.15f;flow.radio.songSeconds=song;flow.glanceSeconds=.12f;}
        static void Click(Button button)
        {
            Assert.That(button&&button.IsActive()&&button.IsInteractable(),Is.True);Canvas.ForceUpdateCanvases();
            var rect=(RectTransform)button.transform;var canvas=button.GetComponentInParent<Canvas>();
            var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Assert.That(hits,Is.Not.Empty);
            var handler=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);Assert.That(handler,Is.EqualTo(button.gameObject));
            ExecuteEvents.Execute(handler,data,ExecuteEvents.pointerClickHandler);
        }
        IEnumerator OpenMessages()
        {
            flow.incomingCallSeconds=100;flow.ignoredBuzzSeconds=100;
            var messages=PhoneMessages.For(game);
            if(!messages.Acquired)
            {
                yield return Aim(BeforeDemolitionRoadAction.Phone);yield return KeyPress(Key.E);
                yield return Until(()=>messages.Acquired,"E picks up the vibrating phone");yield return null;
            }
            if(!messages.IsOpen&&!flow.Glancing)yield return KeyPress(Key.P);
            yield return Until(()=>messages.IsOpen,"Messages open after the camera glance");
            Assert.That(messages.State.received.Any(m=>m.id=="road.01"&&m.read),Is.True);
            Assert.That(flow.inbox.IsOpen,Is.False,"Emails are available only on the office PC.");
        }
        IEnumerator CloseMessages(){var messages=PhoneMessages.For(game);if(messages.IsOpen)yield return KeyPress(Key.Escape);yield return Until(()=>game.CanControl,"Phone releases input");}
        void Capture(string name)
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("HorrorGame.Editor.PresentationCapture")).First(t=>t!=null);
            type.GetMethod("Capture",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{SceneManager.GetActiveScene(),game.player.view.playerCamera,Path.Combine(Visual,name+".png"),1280,720,null,true});
        }

        [UnityTest]public IEnumerator MovingCarKeepsTheCameraInsideAndBothRadioButtonsReturnToTheSameStation()
        {
            Assert.That(GameClock.Instance.State.minute,Is.InRange(1590,1592),"An old office save gets the road's safe default.");
            Assert.That(flow.radio.newsSeconds,Is.EqualTo(75));Assert.That(flow.radio.silenceSeconds,Is.EqualTo(6));Assert.That(game.player.view.playerCamera.fieldOfView,Is.EqualTo(49));
            float first=flow.DriveProgress;yield return new WaitForSeconds(.15f);Assert.That(flow.DriveProgress,Is.GreaterThan(first));
            Assert.That(Vector3.Distance(game.player.view.playerCamera.transform.position,flow.driver.seatedPose.position),Is.LessThan(.06f));
            Assert.That(game.player.enabled||game.player.Controller.enabled,Is.False);Assert.That(flow.HasTorch||game.save.flashlight.IsOn,Is.False);
            foreach(var action in new[]{BeforeDemolitionRoadAction.RadioPower,BeforeDemolitionRoadAction.RadioStation})
            {yield return Aim(action);yield return KeyPress(Key.E);Assert.That(game.hud.notification.text,Does.Contain("mesma estação"));Assert.That(flow.State.phase,Is.EqualTo(RoadRadioPhase.News));Assert.That(flow.driver.Seated,Is.True);}
            Capture("radio-controls-and-car");Assert.That(delivered,Is.Zero);
        }
        [UnityTest]public IEnumerator RoadTextsArriveOnceAndTheRealPhoneSupportsReadCallAndPause()
        {
            Fast(8);flow.incomingCallSeconds=100;flow.ignoredBuzzSeconds=100;
            yield return Until(()=>flow.State.phase==RoadRadioPhase.Song,"Radio reaches the notification");
            Assert.That(GameClock.Instance.State.locked,Is.False);Assert.That(PhoneMessages.For(game).State.received.All(m=>m.minute==1627),Is.True);
            Assert.That(delivered,Is.Zero);Assert.That(flow.inbox.State.IsRead("email.08"),Is.False);
            yield return OpenMessages();Assert.That(flow.IncomingCall,Is.True);Assert.That(read,Is.Zero);
            Capture("phone-text-messages");
            string screen=Path.Combine(Visual,"phone-text-messages-screen.png");if(File.Exists(screen))File.Delete(screen);ScreenCapture.CaptureScreenshot(screen);
            yield return Until(()=>File.Exists(screen),"Real Game View capture",3);yield return CloseMessages();
            yield return KeyPress(Key.P);yield return Until(()=>flow.CallView.IsOpen,"P answers the owned phone's unnamed call");
            Assert.That(flow.State.callAnswered,Is.True);Capture("phone-unnamed-call");
            float age=flow.State.phaseAge;game.Pause();yield return null;yield return new WaitForSecondsRealtime(.12f);
            Assert.That(flow.State.phaseAge,Is.EqualTo(age).Within(.001f));game.Resume();yield return null;
            yield return KeyPress(Key.Escape);yield return Until(()=>game.CanControl,"Call close restores control");
            Assert.That(game.save.Save(),Is.True);Assert.That(game.interaction.inventory.items.Any(i=>i.id=="bd.note.email.08"),Is.False);
        }
        [UnityTest]public IEnumerator IgnoringTheNotificationMovesThePhoneOnlyOffscreenAndRestoreDoesNotReplayBuzz()
        {
            Fast(6);flow.ignoredBuzzSeconds=.15f;flow.incomingCallSeconds=100;
            yield return Until(()=>flow.State.phase==RoadRadioPhase.Song,"Notification stage");
            var away=new GameObject("Temporary look away from the dashboard");
            away.transform.position=flow.driver.playerCamera.transform.position+flow.driver.carRoot.TransformDirection(new Vector3(-4,2,1));
            flow.driver.GlanceAtExternalPhone(away.transform,1);UnityEngine.Object.Destroy(away);yield return null;
            var phoneViewport=flow.driver.playerCamera.WorldToViewportPoint(flow.phone.position);
            Assert.That(phoneViewport.z<0||phoneViewport.x<0||phoneViewport.x>1||phoneViewport.y<0||phoneViewport.y>1,Is.True,"Look away from the dashboard before testing an unseen movement.");
            yield return Until(()=>flow.State.phoneMoved,"Ignored phone moves toward the hand");
            Assert.That(flow.radio.VibrationCount,Is.GreaterThanOrEqualTo(2));Assert.That(flow.phone.localPosition,Is.EqualTo(flow.phoneNearHandLocal));
            Assert.That(game.save.Save(),Is.True);Assert.That(SaveSystem.TryReadCheckpoint(game.save.SavePath,out var snapshot,"RoadJourney",BeforeDemolitionCampaign.StoryId),Is.True);
            snapshot.clock=null;GameClock.Instance.Restore(new GameClockState());
            int buzz=flow.radio.VibrationCount;Assert.That(game.save.RestoreSnapshot(snapshot),Is.True);game.narrative.InitializeAfterSession(true);yield return null;
            Assert.That(GameClock.Instance.State.locked,Is.False,"The car clock stays separate from the deliberately future message stamps.");
            Assert.That(flow.radio.VibrationCount,Is.EqualTo(buzz));Assert.That(delivered,Is.Zero);Assert.That(flow.State.phoneMoved,Is.True);
            PlayerPrefs.SetInt(Prefix+"ResumeRequested",1);PlayerPrefs.SetInt(Prefix+"StoryCarryRequested",0);yield return LoadRoad();
            Assert.That(flow.State.phoneMoved,Is.True);Assert.That(flow.radio.VibrationCount,Is.Zero);Assert.That(delivered,Is.Zero);
        }
        [UnityTest]public IEnumerator TwoSecondRadioSilencePrecedesArrivalAndTorchPickupDoesNotExitTheCar()
        {
            Fast(.6f);flow.radio.silenceSeconds=2;
            yield return Until(()=>flow.State.phase==RoadRadioPhase.Silence,"Silence begins");
            Assert.That(flow.radio.RadioSource.clip,Is.Null);yield return new WaitForSeconds(.3f);
            Assert.That(flow.State.phase,Is.EqualTo(RoadRadioPhase.Silence));
            yield return Until(()=>flow.Parked,"Parked after the song");
            Assert.That(flow.driver.driveHum.isPlaying,Is.False);
            Assert.That(flow.radio.PhoneSource.isPlaying,Is.False);
            Assert.That(flow.radio.RadioSource.clip,Is.SameAs(flow.radio.hauntedChant),"The parked radio fades at the school gate.");
            Assert.That(flow.driver.windowLight.enabled&&flow.driver.windowVisual.activeInHierarchy,Is.True);Assert.That(flow.driver.windowAnchor.position.y,Is.LessThan(3.2f));
            yield return Aim(BeforeDemolitionRoadAction.Flashlight);yield return KeyPress(Key.E);Assert.That(flow.HasTorch&&flow.driver.Seated,Is.True);
            Assert.That(game.save.flashlight.IsAvailable,Is.True);Assert.That(flow.Exited,Is.False);
            yield return OpenMessages();yield return CloseMessages();
            yield return Aim(BeforeDemolitionRoadAction.Exit);yield return KeyPress(Key.E);yield return null;
            Assert.That(flow.Exited&&!flow.driver.Seated&&game.player.Controller.enabled&&game.player.enabled,Is.True);
            Capture("arrival-outside-car");
            game.player.Warp(game.player.transform.position,0);Press(Key.W);yield return new WaitForSeconds(.3f);
            Assert.That(game.CanControl&&game.input.Move.y>0,Is.True,"Walking input after pickup/exit: state="+game.State+" move="+game.input.Move+" position="+game.player.transform.position);
            yield return Until(()=>game.player.transform.position.z>=402,"Walk clear of the car and the side boundary",25);
            Press();yield return null;
            var gate = flow.driver.gate.transform;
            Vector3 toGate = gate.position - game.player.transform.position;
            game.player.Warp(game.player.transform.position, Mathf.Atan2(toGate.x,toGate.z)*Mathf.Rad2Deg);
            Press(Key.W);
            float handoffStart=Time.realtimeSinceStartup;int handoffFrame=Time.frameCount;
            while(!(SceneManager.GetActiveScene().name=="SchoolCEEP"&&GameSession.Instance&&GameSession.Instance.CanControl)&&Time.realtimeSinceStartup-handoffStart<90)yield return null;
            Directory.CreateDirectory(Visual);var session=GameSession.Instance;
            File.WriteAllText(Path.Combine(Visual,"handoff-diagnostic.txt"),"scene="+SceneManager.GetActiveScene().name+" state="+(session?session.State.ToString():"none")+" frames="+(Time.frameCount-handoffFrame)+" time="+(Time.realtimeSinceStartup-handoffStart)+" background="+Application.runInBackground+" scale="+Time.timeScale+" chapter="+(session&&session.narrative?session.narrative.Progress.chapterId:"none")+" sequence="+(session&&session.narrative&&session.narrative.SequencePlaying));
            Press();
            Assert.That(SceneManager.GetActiveScene().name=="SchoolCEEP"&&session&&session.CanControl,Is.True,"Road hands off to the new secretary chapter; see handoff-diagnostic.txt");
            Assert.That(GameClock.Instance.TimeText,Is.EqualTo("03:07"));Assert.That(GameClock.Instance.State.locked,Is.True);
            game=GameSession.Instance;
            Assert.That(game.narrative.Progress.chapterId,Is.EqualTo(BeforeDemolitionCampaign.SchoolChapterId));Assert.That(game.narrative.Progress.endingSeen,Is.False);
            Assert.That(game.GetComponent<BeforeDemolitionSchoolFlow>().IsActive,Is.True);
            Assert.That(game.interaction.inventory.Contains(BeforeDemolitionCampaign.WhistleItemId),Is.True);
            Assert.That(game.save.flashlight.IsAvailable,Is.True);
            Assert.That(UnityEngine.Object.FindObjectsByType<SchoolSimplifiedFlow>(FindObjectsInactive.Include,FindObjectsSortMode.None).All(f=>!f.enabled),Is.True);
        }
        [UnityTest]public IEnumerator FailedRoadWriteKeepsThePendingSnapshotAndRetryRestoresMessages()
        {
            Fast(6);flow.incomingCallSeconds=100;flow.ignoredBuzzSeconds=100;yield return Until(()=>flow.State.phase==RoadRadioPhase.Song,"Email delivered");
            yield return OpenMessages();yield return CloseMessages();yield return null;
            Assert.That(game.save.Save(),Is.True);byte[] before=File.ReadAllBytes(game.save.SavePath);
            string blocked=game.save.SavePath+".tmp";if(File.Exists(blocked))File.Delete(blocked);Directory.CreateDirectory(blocked);
            LogAssert.Expect(LogType.Warning,new Regex("Checkpoint could not be saved:"));Assert.That(game.save.Save(),Is.False);
            Assert.That(game.save.HasPendingSave,Is.True);CollectionAssert.AreEqual(before,File.ReadAllBytes(game.save.SavePath));
            Directory.Delete(blocked);Assert.That(game.save.RetryPendingSave(),Is.True);Assert.That(game.save.HasPendingSave,Is.False);
            Assert.That(SaveSystem.TryReadCheckpoint(game.save.SavePath,out var data,"RoadJourney",BeforeDemolitionCampaign.StoryId),Is.True);
            Assert.That(data.phone.received.Count(m=>m.id=="road.01"&&m.read),Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator ThreeIdenticalAnnouncementsChangeVoiceThenSilenceAndTheCallHasItsOwnFilteredAudio()
        {
            flow.radio.musicInterlude=false; // Compatibility path for the archived announcement sequence.
            var silent=AudioClip.Create("Silent audio-routing fixture",22050,1,22050,false);transientClips.Add(silent);
            Fast(8);flow.radio.newsSeconds=.4f;flow.radio.announcementSeconds=1.05f;flow.radio.silenceSeconds=2;flow.radio.announcement=silent;flow.radio.nurserySong=silent;
            flow.incomingCallSeconds=100;flow.ignoredBuzzSeconds=100;
            foreach(var phase in new[]{RoadRadioPhase.FirstAnnouncement,RoadRadioPhase.SecondAnnouncement,RoadRadioPhase.ThirdAnnouncement})
            {
                yield return Until(()=>flow.State.phase==phase,"Observe announcement "+phase);
                Assert.That(game.hud.subtitle.text,Is.EqualTo("Rádio: "+BeforeDemolitionContent.RadioAnnouncement));
                Assert.That(flow.radio.RadioSource.clip,Is.SameAs(silent));
                Assert.That(flow.radio.RadioSource.pitch,Is.EqualTo(phase==RoadRadioPhase.ThirdAnnouncement?.65f:1).Within(.001f));
            }
            yield return Until(()=>flow.State.phase==RoadRadioPhase.Silence,"Actual audio stops before the song");
            Assert.That(flow.radio.RadioSource.isPlaying,Is.False);Assert.That(flow.radio.RadioSource.clip,Is.Null);
            yield return new WaitForSeconds(.35f);Assert.That(flow.State.phase,Is.EqualTo(RoadRadioPhase.Silence));
            yield return Until(()=>flow.State.phase==RoadRadioPhase.Song,"Song source starts");
            yield return OpenMessages();yield return CloseMessages();yield return KeyPress(Key.P);
            yield return Until(()=>flow.CallView.IsOpen,"Call is answered");
            Assert.That(flow.radio.PhoneSource.isPlaying&&flow.radio.RadioSource.isPlaying,Is.True);
            Assert.That(flow.radio.PhoneSource.GetComponent<AudioHighPassFilter>().cutoffFrequency,Is.EqualTo(320));
            Assert.That(flow.radio.RadioSource.GetComponent<AudioHighPassFilter>().cutoffFrequency,Is.EqualTo(220));
            game.Pause();yield return null;yield return null;int sample=flow.radio.PhoneSource.timeSamples;
            yield return new WaitForSecondsRealtime(.12f);Assert.That(flow.radio.PhoneSource.timeSamples,Is.EqualTo(sample));game.Resume();yield return null;
            Assert.That(flow.radio.PhoneSource.isPlaying,Is.True);yield return KeyPress(Key.Escape);
        }
        [UnityTest]
        public IEnumerator RecordedRadioUsesCueTimesAndPlaysJoaoBetweenStations()
        {
            var radio = flow.radio;
            Assert.That(radio.news && radio.news.name == "wp_mix_radio_noticias", Is.True);
            Assert.That(radio.RadioSource.clip, Is.SameAs(radio.news));
            Assert.That(radio.RadioSource.isPlaying, Is.True);
            Assert.That(radio.RadioSource.loop, Is.False);
            Assert.That(game.hud.subtitle.text, Does.Contain("boletim da madrugada"));
            radio.RadioSource.timeSamples = Mathf.RoundToInt(7.5f * radio.news.frequency);
            flow.Advance(7.5f);
            Assert.That(game.hud.subtitle.text, Does.Contain("manutenção"));
            radio.RadioSource.timeSamples = Mathf.RoundToInt(56.5f * radio.news.frequency);
            flow.Advance(49f);
            Assert.That(game.hud.subtitle.text, Does.Contain("Marta, Caio, Luan e Bia"));
            flow.Advance(radio.Duration(RoadRadioPhase.News) - flow.State.phaseAge + radio.musicReactionDelay + .2f);
            Assert.That(flow.State.phase, Is.EqualTo(RoadRadioPhase.FirstAnnouncement));
            Assert.That(game.hud.subtitle.text, Does.Contain("cabeça"));
            var reaction = radio.GetComponentsInChildren<AudioSource>().Single(source => source.clip == radio.musicReactionVoices[0]);
            Assert.That(reaction.isPlaying, Is.True);
            Assert.That(reaction.outputAudioMixerGroup, Is.SameAs(AudioManager.Instance.voiceGroup));
            yield return null;
        }

        [UnityTest]public IEnumerator AuthoredTorchOnlySurvivesVisibilityRestoreAndThePhoneSitsOnTheDashboard()
        {
            yield return null;
            var pickup=flow.driver.flashlightPickup;
            pickup.SetVisible(false);pickup.SetVisible(true);pickup.SetVisible(true);
            var renderers=pickup.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            Assert.That(renderers.Length,Is.EqualTo(2),"The current flashlight has a body and lens, not overlapping retired models.");
            Assert.That(renderers.All(r=>AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh).Contains("small_plastic_torch")),Is.True);
            Assert.That(flow.phone.localPosition.z,Is.EqualTo(.60f).Within(.01f));
            Assert.That(flow.phone.localPosition.y,Is.InRange(.97f,1.01f));
            Assert.That(flow.driver.flashlight.handRig.gameObject.activeInHierarchy,Is.False,"No second torch held before collection.");
            flow.driver.GlanceAtExternalPhone(flow.phone,1);yield return null;yield return null;
            ScreenCapture.CaptureScreenshot("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/work/RoadMix20261030/road-dashboard-fixed.png");yield return null;yield return null;
            flow.driver.GlanceAtExternalPhone(pickup.transform,1);yield return null;yield return null;
            ScreenCapture.CaptureScreenshot("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/work/RoadMix20261030/road-torch-fixed.png");yield return null;yield return null;
        }
        [UnityTest]public IEnumerator MusicStationsPowerOffAndCantigaPreservePauseAndCheckpointState()
        {
            flow.radio.news=null;flow.radio.newsSeconds=.12f;flow.radio.announcementSeconds=20;flow.radio.silenceSeconds=1.2f;flow.radio.songSeconds=12;
            yield return Until(()=>flow.State.phase==RoadRadioPhase.FirstAnnouncement,"News gives way to real music");
            Assert.That(flow.radio.musicInterlude,Is.True);Assert.That(flow.radio.RadioSource.clip,Is.SameAs(flow.radio.musicTracks[0]));
            yield return Until(()=>flow.State.phaseAge>=flow.radio.musicReactionDelay+.1f,"João reacts after hearing the song");
            Assert.That(game.hud.subtitle.text,Does.Contain("cabeça"));
            float progress=flow.DriveProgress;
            yield return Aim(BeforeDemolitionRoadAction.RadioStation);yield return KeyPress(Key.E);
            Assert.That(flow.State.phase,Is.EqualTo(RoadRadioPhase.SecondAnnouncement));Assert.That(flow.radio.RadioSource.clip,Is.SameAs(flow.radio.musicTracks[1]));
            Assert.That(flow.DriveProgress,Is.GreaterThanOrEqualTo(progress));
            game.Pause();yield return null;float age=flow.State.phaseAge;yield return new WaitForSecondsRealtime(.1f);Assert.That(flow.State.phaseAge,Is.EqualTo(age));game.Resume();yield return null;
            yield return Aim(BeforeDemolitionRoadAction.RadioPower);
            float beforePower = flow.DriveProgress;
            yield return KeyPress(Key.E);
            Assert.That(flow.State.phase, Is.EqualTo(RoadRadioPhase.Song));
            Assert.That(flow.State.skippedMusic && flow.State.radioSwitchedOff, Is.True);
            Assert.That(flow.radio.RadioSource.clip, Is.SameAs(flow.radio.hauntedChant));
            Assert.That(flow.radio.RadioSource.isPlaying && !flow.radio.RestartStaticSource.isPlaying, Is.True);
            Assert.That(flow.DriveProgress, Is.InRange(beforePower, beforePower + .05f));
            Assert.That(flow.radio.Duration(flow.State.phase, flow.State.skippedMusic), Is.EqualTo(14));
            Assert.That(game.save.Save(),Is.True);Assert.That(SaveSystem.TryReadCheckpoint(game.save.SavePath,out var checkpoint,"RoadJourney",BeforeDemolitionCampaign.StoryId),Is.True);
            Assert.That(game.save.RestoreSnapshot(checkpoint),Is.True);game.narrative.InitializeAfterSession(true);yield return null;
            Assert.That(flow.State.skippedMusic && flow.State.radioSwitchedOff, Is.True);
            Assert.That(flow.radio.RadioSource.clip, Is.SameAs(flow.radio.hauntedChant));
            Assert.That(flow.radio.RadioSource.isPlaying && !flow.radio.RestartStaticSource.isPlaying, Is.True);
        }

        [UnityTest]public IEnumerator CityDriveHasWorkingHeadlightsALongerRouteAndSubtlePausedSteering()
        {
            Assert.That(Vector3.Distance(flow.driver.driveStart.position,flow.driver.driveEnd.position),Is.EqualTo(1884).Within(.1f));
            Assert.That(flow.radio.announcementSeconds,Is.EqualTo(24));Assert.That(flow.radio.musicTracks.All(c=>c&&c.length>24),Is.True);
            var town=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t=>t.name=="Cidade — casas vistas de relance");Assert.That(town.childCount,Is.EqualTo(36));
            Assert.That(town.Cast<Transform>().All(h=>h.Find("Fachada fechada")&&h.Find("Parede dos fundos")&&h.Find("Lateral 1")&&h.Find("Lateral 2")&&h.Find("Cobertura")),Is.True,"Each background home has a closed shell.");
            var heads=flow.driver.carRoot.GetComponentsInChildren<Light>().Where(l=>l.type==LightType.Spot).ToArray();Assert.That(heads.Length,Is.EqualTo(2));
            foreach(var head in heads){Assert.That(head.enabled&&head.intensity>=200&&head.range>=60,Is.True);Physics.SyncTransforms();Assert.That(Physics.Raycast(head.transform.position,head.transform.forward,out var ground,30,~LayerMask.GetMask("Player"),QueryTriggerInteraction.Ignore),Is.True);Assert.That(ground.transform.name,Is.EqualTo("Estrada — asfalto"));}
            var wheel=flow.driver.steeringWheel;Assert.That(wheel,Is.Not.Null);Quaternion start=wheel.localRotation;
            yield return new WaitForSeconds(.7f);Assert.That(Quaternion.Angle(start,wheel.localRotation),Is.InRange(.1f,4));
            Assert.That(flow.driver.driveHum.isPlaying&&flow.driver.driveHum.loop,Is.True);Assert.That(flow.driver.driveHum.volume,Is.InRange(.55f,.65f));
            game.Pause();yield return null;Quaternion paused=wheel.localRotation;yield return new WaitForSecondsRealtime(.15f);Assert.That(Quaternion.Angle(paused,wheel.localRotation),Is.LessThan(.001f));game.Resume();yield return null;
            Vector3 savedCarPosition=flow.driver.carRoot.position;
            Assert.That(game.save.Save(),Is.True);Assert.That(SaveSystem.TryReadCheckpoint(game.save.SavePath,out var save,"RoadJourney",BeforeDemolitionCampaign.StoryId),Is.True,"The longer negative-Z route remains a valid checkpoint.");
            Assert.That(game.save.RestoreSnapshot(save),Is.True);game.narrative.InitializeAfterSession(true);yield return null;
            Assert.That(Vector3.Distance(flow.driver.carRoot.position,savedCarPosition),Is.LessThan(1));
            Directory.CreateDirectory("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/work/RoadMix20261030");
            ScreenCapture.CaptureScreenshot("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/work/RoadMix20261030/city-drive-forward.png");yield return null;yield return null;
            var target=new GameObject("Temporary city view target");target.transform.position=flow.driver.carRoot.TransformPoint(new Vector3(12,2,22));flow.driver.GlanceAtExternalPhone(target.transform,1);UnityEngine.Object.Destroy(target);yield return null;yield return null;
            ScreenCapture.CaptureScreenshot("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/work/RoadMix20261030/city-drive-side.png");yield return null;yield return null;
        }
        [UnityTest]public IEnumerator EachMusicStationHasTwoCommentsAndRestoringDoesNotReplayEither()
        {
            string[] first={"essa não","essa é pior","De novo esse cara"};
            string[] second={"tocando nas outras","rádio normal","sem rádio mesmo"};
            for(int i=0;i<3;i++)
            {
                var phase=RoadRadioPhase.FirstAnnouncement+i;
                flow.radio.ApplyPhase(phase,0);flow.radio.ApplyPhase(phase,flow.radio.musicReactionDelay+.1f);Assert.That(game.hud.subtitle.text,Does.Contain(first[i]));
                flow.radio.ApplyPhase(phase,flow.radio.musicFollowupDelay+.1f);Assert.That(game.hud.subtitle.text,Does.Contain(second[i]));
                flow.radio.ApplyPhase(phase,flow.radio.musicFollowupDelay+1,true);game.hud.ShowSubtitle("restored",10);flow.radio.ApplyPhase(phase,flow.radio.musicFollowupDelay+2);Assert.That(game.hud.subtitle.text,Is.EqualTo("restored"));
            }
            yield return null;
        }
        [UnityTest]public IEnumerator RadioSwitchesItselfOffReturnsWithStaticResistsPowerAndStopsAtSchool()
        {
            Fast(8);flow.radio.silenceSeconds=1;flow.radio.restartStaticSeconds=.7f;flow.radio.hauntedAttemptDelay=5;
            yield return Until(()=>flow.State.phase==RoadRadioPhase.Silence,"Radio automatically switches itself off after the three tracks");
            Assert.That(flow.State.radioSwitchedOff,Is.True);Assert.That(flow.radio.RadioSource.isPlaying,Is.False);Assert.That(game.hud.subtitle.text,Does.Contain("Até o rádio cansou"));
            yield return Until(()=>flow.State.phase==RoadRadioPhase.Song,"Radio returns by itself");
            Assert.That(flow.radio.RestartStaticSource.clip,Is.Not.Null);Assert.That(flow.radio.RestartStaticSource.isPlaying,Is.True);Assert.That(flow.radio.RadioSource.clip,Is.Null);
            game.Pause();yield return null;yield return null;int staticSample=flow.radio.RestartStaticSource.timeSamples;float age=flow.State.phaseAge;yield return new WaitForSecondsRealtime(.15f);Assert.That(flow.State.phaseAge,Is.EqualTo(age));Assert.That(flow.radio.RestartStaticSource.timeSamples,Is.EqualTo(staticSample));game.Resume();yield return null;
            yield return Until(()=>flow.radio.RadioSource.isPlaying&&flow.radio.RadioSource.clip==flow.radio.nurserySong,"The strange music follows the startup static");
            Assert.That(flow.radio.RestartStaticSource.isPlaying,Is.False);
            yield return Aim(BeforeDemolitionRoadAction.RadioPower);yield return KeyPress(Key.E);
            Assert.That(flow.State.hauntedPowerTried,Is.True);Assert.That(flow.State.phase,Is.EqualTo(RoadRadioPhase.Song));Assert.That(flow.radio.RadioSource.isPlaying,Is.True);Assert.That(game.hud.subtitle.text,Does.Contain("não desliga"));
            Assert.That(game.save.Save(),Is.True);Assert.That(SaveSystem.TryReadCheckpoint(game.save.SavePath,out var saved,"RoadJourney",BeforeDemolitionCampaign.StoryId),Is.True);Assert.That(game.save.RestoreSnapshot(saved),Is.True);game.narrative.InitializeAfterSession(true);yield return null;
            Assert.That(flow.State.hauntedPowerTried,Is.True);Assert.That(flow.radio.RadioSource.isPlaying,Is.True);
            yield return Until(()=>flow.Parked,"The song only stops when the car reaches school",14);
            Assert.That(flow.radio.RadioSource.isPlaying||flow.radio.RestartStaticSource.isPlaying,Is.False);Assert.That(flow.radio.RadioSource.clip,Is.Null);Assert.That(flow.driver.carRoot.position.z,Is.EqualTo(384).Within(.01f));
        }
        [UnityTest]public IEnumerator RoadMedianSidewalksAndReadyLampModelsAreAlignedAndTheAutomaticAttemptIsSaved()
        {
            var all=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);var road=all.Single(t=>t.name=="Estrada — asfalto");var marks=all.Where(t=>t.name=="Estrada — faixa gasta").ToArray();
            Assert.That(marks.All(t=>Mathf.Abs(t.position.x-road.position.x)<.001f&&Quaternion.Angle(t.rotation,Quaternion.identity)<.01f),Is.True);
            Assert.That(flow.driver.carRoot.position.x-road.position.x,Is.EqualTo(1.3f).Within(.001f),"Car stays centered in its lane");
            var poles=all.Single(t=>t.name=="Postes — modelos Poly Haven");Assert.That(poles.childCount,Is.EqualTo(25));Assert.That(poles.Cast<Transform>().All(t=>t.GetComponentInChildren<LODGroup>()&&t.GetComponentsInChildren<MeshFilter>().Any(m=>AssetDatabase.GetAssetPath(m.sharedMesh).Contains("street_lamp_01"))),Is.True);
            Assert.That(poles.GetComponentsInChildren<Light>().All(l=>l.intensity<=55&&l.range<=19),Is.True);Assert.That(flow.driver.driveHum.volume,Is.GreaterThan(.3f));
            Fast(4);flow.radio.restartStaticSeconds=.2f;flow.radio.hauntedAttemptDelay=.8f;
            yield return Until(()=>flow.State.hauntedPowerTried,"João tries the button even if the player never clicks it");Assert.That(flow.State.phase,Is.EqualTo(RoadRadioPhase.Song));Assert.That(flow.radio.RadioSource.isPlaying,Is.True);
            Assert.That(game.save.Save(),Is.True);Assert.That(SaveSystem.TryReadCheckpoint(game.save.SavePath,out var saved,"RoadJourney",BeforeDemolitionCampaign.StoryId),Is.True);Assert.That(game.save.RestoreSnapshot(saved),Is.True);game.narrative.InitializeAfterSession(true);yield return null;
            game.hud.ShowSubtitle("restore sem replay",10);yield return null;yield return null;Assert.That(game.hud.subtitle.text,Is.EqualTo("restore sem replay"));
        }
        [UnityTest]public IEnumerator EveryAutomaticAndManualMusicChangeHasTuningAudioButRestoreDoesNot()
        {
            Fast(8);flow.radio.announcementSeconds=.8f;
            yield return Until(()=>flow.State.phase==RoadRadioPhase.FirstAnnouncement,"Automatic first station");
            Assert.That(flow.radio.TuningSource.clip,Is.Not.Null);Assert.That(flow.radio.TuningSource.isPlaying,Is.True);Assert.That(flow.radio.RadioSource.volume,Is.LessThan(.1f),"Brief duck lets the station switch be heard");
            yield return Aim(BeforeDemolitionRoadAction.RadioStation);yield return KeyPress(Key.E);
            Assert.That(flow.State.phase,Is.EqualTo(RoadRadioPhase.SecondAnnouncement));Assert.That(flow.radio.TuningSource.isPlaying,Is.True);
            yield return Until(()=>flow.State.phase==RoadRadioPhase.ThirdAnnouncement,"Automatic next station");Assert.That(flow.radio.TuningSource.isPlaying,Is.True);
            game.Pause();yield return null;yield return null;int sample=flow.radio.TuningSource.timeSamples;yield return new WaitForSecondsRealtime(.12f);Assert.That(flow.radio.TuningSource.timeSamples,Is.EqualTo(sample));game.Resume();yield return null;
            Assert.That(game.save.Save(),Is.True);Assert.That(SaveSystem.TryReadCheckpoint(game.save.SavePath,out var saved,"RoadJourney",BeforeDemolitionCampaign.StoryId),Is.True);Assert.That(game.save.RestoreSnapshot(saved),Is.True);game.narrative.InitializeAfterSession(true);yield return null;
            Assert.That(flow.State.phase,Is.EqualTo(RoadRadioPhase.ThirdAnnouncement));Assert.That(flow.radio.TuningSource.isPlaying,Is.False,"Loading an already selected station does not click or hiss again");
        }
        [UnityTest]public IEnumerator EngineUsesTheLeveledRecordingWithHeadroomAndPersistsUnderTheMusic()
        {
            var engine=flow.driver.driveHum;Assert.That(AssetDatabase.GetAssetPath(engine.clip),Does.EndWith("car_interior_audible.wav"));
            float[] samples=new float[engine.clip.samples*engine.clip.channels];Assert.That(engine.clip.GetData(samples,0),Is.True);
            double energy=0;float peak=0;foreach(float value in samples){energy+=value*value;peak=Mathf.Max(peak,Mathf.Abs(value));}
            double rmsDb=20*Math.Log10(Math.Sqrt(energy/samples.Length));Assert.That(rmsDb,Is.InRange(-22,-19));Assert.That(peak,Is.LessThan(.5f));Assert.That(engine.volume,Is.EqualTo(.6f).Within(.001f));
            Fast(5);flow.radio.announcementSeconds=2;
            yield return Until(()=>flow.State.phase==RoadRadioPhase.FirstAnnouncement,"Engine stays present when music starts");yield return new WaitForSeconds(.4f);
            Assert.That(engine.isPlaying,Is.True);Assert.That(flow.radio.RadioSource.isPlaying,Is.True);Assert.That(flow.radio.RadioSource.volume,Is.EqualTo(.27f).Within(.01f));Assert.That(engine.outputAudioMixerGroup.name,Is.EqualTo("SFX"));
        }
        [UnityTearDown]public IEnumerator TearDown()
        {
            if(!guarded)yield break;SceneManager.sceneLoaded-=Loaded;
            if(flow){PhoneMessages.For(game)?.Close();flow.inbox.Close();if(flow.CallView)flow.CallView.Close();}
            var loaded=new List<Scene>();for(int i=0;i<SceneManager.sceneCount;i++)loaded.Add(SceneManager.GetSceneAt(i));
            var blank=SceneManager.CreateScene("Road cleanup "+Guid.NewGuid().ToString("N"));SceneManager.SetActiveScene(blank);Time.timeScale=1;AudioListener.pause=false;
            foreach(var s in loaded)if(s.IsValid()&&s.isLoaded)yield return SceneManager.UnloadSceneAsync(s);
            foreach(var clip in transientClips)if(clip)UnityEngine.Object.Destroy(clip);transientClips.Clear();
            SaveSystem.ClearChapterCarry();SaveSystem.TestSaveDirectory=previousDirectory;
            foreach(var pair in oldFlags)if(pair.Value.Item1)PlayerPrefs.SetInt(Prefix+pair.Key,pair.Value.Item2);else PlayerPrefs.DeleteKey(Prefix+pair.Key);
            if(hadSaveScene)PlayerPrefs.SetString(Prefix+"SaveScene",oldSaveScene);else PlayerPrefs.DeleteKey(Prefix+"SaveScene");PlayerPrefs.Save();
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(previousKeyboard!=null&&previousKeyboard.added)previousKeyboard.MakeCurrent();
            InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditorInput;Application.runInBackground=background;Time.timeScale=timeScale;AudioListener.pause=oldAudio;Cursor.lockState=cursorLock;Cursor.visible=cursorVisible;
            foreach(string file in Directory.GetFiles(temporary))File.Delete(file);foreach(string folder in Directory.GetDirectories(temporary))Directory.Delete(folder);Directory.Delete(temporary);
        }
    }
}
#endif

