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
    [Category("BeforeDemolitionSchool")]
    public sealed partial class BeforeDemolitionSchoolSceneTests
    {
        string directory,oldDirectory,oldScene;bool hadScene,guarded,background,oldAudio;float timeScale;
        InputSettings.BackgroundBehavior oldBackground;InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        readonly Dictionary<string,(bool,int)> flags=new Dictionary<string,(bool,int)>();Keyboard keyboard,oldKeyboard;
        GameSession game;BeforeDemolitionSchoolFlow flow;InspectionManager inspection;
        const string Prefix="BeforeDemolition.";
        const string Visual="C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/TrocaCopos20261103/SchoolRegression";
        [UnitySetUp]public IEnumerator SetUp()
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None),Is.Empty,"Start from the isolated blank scene.");
            directory=Path.Combine(Path.GetTempPath(),"Whispers-SchoolPlay-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);oldDirectory=SaveSystem.TestSaveDirectory;SaveSystem.TestSaveDirectory=directory; TutorialPreferences.Set(TutorialMode.Off);guarded=true;
            foreach(string suffix in new[]{"ResumeRequested","StoryCarryRequested"}){flags[suffix]=(PlayerPrefs.HasKey(Prefix+suffix),PlayerPrefs.GetInt(Prefix+suffix));PlayerPrefs.SetInt(Prefix+suffix,0);}
            hadScene=PlayerPrefs.HasKey(Prefix+"SaveScene");oldScene=PlayerPrefs.GetString(Prefix+"SaveScene");
            oldKeyboard=Keyboard.current;oldBackground=InputSystem.settings.backgroundBehavior;oldEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;background=Application.runInBackground;oldAudio=AudioListener.pause;timeScale=Time.timeScale;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;Application.runInBackground=true;Time.timeScale=1;AudioListener.pause=false;
            // Creating the simulated keyboard before IgnoreFocus can leave it disabled in an unfocused Editor.
            keyboard=InputSystem.AddDevice<Keyboard>();if(!keyboard.enabled)InputSystem.EnableDevice(keyboard);
            File.WriteAllText(Path.Combine(directory,BeforeDemolitionCampaign.CheckpointFile),JsonUtility.ToJson(CompletedRoad()));PlayerPrefs.SetInt(Prefix+"StoryCarryRequested",1);SceneManager.sceneLoaded+=Loaded;
            yield return Load();
        }
        SaveData CompletedRoad()
        {
            var emails=EmailInboxState.Create(AssetDatabase.LoadAssetAtPath<EmailCatalog>("Assets/HorrorGameplay/Data/BeforeDemolition/BeforeDemolitionOfficeEmails.asset"));
            var cat=AssetDatabase.LoadAssetAtPath<EmailCatalog>("Assets/HorrorGameplay/Data/BeforeDemolition/BeforeDemolitionOfficeEmails.asset");emails.Deliver(cat,"email.08");foreach(string id in new[]{"email.01","email.06","email.07"})emails.MarkRead(cat,id);
            var d=new SaveData{scene="RoadJourney",battery=240,flashlightOn=true,stamina=100,narrative=new NarrativeProgress{storyId=BeforeDemolitionCampaign.StoryId,storyRevision=3,chapterId=BeforeDemolitionCampaign.RoadChapterId,pendingChapterId=BeforeDemolitionCampaign.SchoolChapterId,
                milestones=new List<string>{BeforeDemolitionCampaign.ReadySignal,BeforeDemolitionCampaign.ExitSignal},completedChapters=new List<string>{BeforeDemolitionCampaign.OfficeChapterId,BeforeDemolitionCampaign.RoadChapterId}},inventory=new List<InventoryItem>{new InventoryItem{id=BeforeDemolitionCampaign.WhistleItemId,note=true,inspectionId=BeforeDemolitionCampaign.WhistleItemId},new InventoryItem{id=BeforeDemolitionRoadFlow.EmailDeliveryItemId}}};
            d.narrative.milestones.AddRange(BeforeDemolitionCampaign.RoadSignals);
            foreach (string id in emails.readIds)
            {
                var message = cat.Find(id);
                d.inventory.Add(new InventoryItem
                {
                    id = "bd.note." + id, title = "Email — " + message.subject, text = message.PlainText,
                    note = true, chapterId = BeforeDemolitionCampaign.OfficeChapterId, inspectionSeen = true
                });
            }
            d.objects.Add(new SavedObject{id=BeforeDemolitionRoadFlow.StateId,json=JsonUtility.ToJson(new BeforeDemolitionRoadState{phase=RoadRadioPhase.Parked,emails=emails})});return d;
        }
        void Loaded(Scene s,LoadSceneMode mode)
        {foreach(var g in s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameSession>(true))){Assert.That(BeforeDemolitionCampaign.Is(g.narrative.book),Is.True);g.input.Actions.devices=new InputDevice[]{keyboard};g.narrative.transitionView.fadeSeconds=.03f;g.narrative.transitionView.cardSeconds=.05f;}}
        IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/SchoolCEEP/Scenes/SchoolCEEP.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return Until(()=>GameSession.Instance&&GameSession.Instance.CanControl&&GameSession.Instance.narrative.IsInitialized,"School initialization",35);
            game=GameSession.Instance;flow=game.GetComponent<BeforeDemolitionSchoolFlow>();inspection=game.GetComponent<InspectionManager>();
            Assert.That(flow&&flow.IsActive,Is.True);Assert.That(game.narrative.Progress.chapterId,Is.EqualTo(BeforeDemolitionCampaign.SchoolChapterId));Assert.That(Path.GetDirectoryName(game.save.SavePath),Is.EqualTo(directory).IgnoreCase);
            game.player.view.bobEnabled=game.player.view.swayEnabled=false;yield return null;yield return null;
            yield return Until(()=>inspection&&inspection.CameraController&&!inspection.IsCompleting,"Inspection system ready");
            Assert.That(game.player.view.playerCamera.fieldOfView,Is.EqualTo(49).Within(.2f));
        }
        static IEnumerator Until(Func<bool> condition,string message,float timeout=10){float t=Time.realtimeSinceStartup;while(!condition()&&Time.realtimeSinceStartup-t<timeout)yield return null;Assert.That(condition(),Is.True,message);}
        IEnumerator Press(Key k){InputSystem.QueueStateEvent(keyboard,new KeyboardState(k));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;}
        BeforeDemolitionSchoolInteraction Target(BeforeDemolitionSchoolAction action)=>UnityEngine.Object.FindObjectsByType<BeforeDemolitionSchoolInteraction>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(t=>t.action==action);
        IEnumerator Aim(Transform target,Vector3 feet)
        {
            game.player.Warp(feet,0);Physics.SyncTransforms();yield return null;
            var collider=target.GetComponentsInChildren<Collider>().FirstOrDefault(c=>c.enabled&&!c.isTrigger);Vector3 point=collider?collider.bounds.center:target.position;var delta=point-game.player.view.playerCamera.transform.position;
            game.player.Warp(feet,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg);Physics.SyncTransforms();yield return null;yield return null;
        }
        IEnumerator Inspect(BeforeDemolitionSchoolAction action,Vector3 feet)
        {
            var t=Target(action);yield return Aim(t.transform,feet);yield return Until(()=>game.interaction.Target is InspectableItem i&&i.originalInteraction==t,"Raycast targets "+action+"; actual="+game.interaction.CurrentPrompt,3);yield return Press(Key.E);
            if(action==BeforeDemolitionSchoolAction.Cups){Assert.That(inspection.IsOpen,Is.False);Assert.That(game.narrative.HasSignal("bd.school.cups"),Is.True);yield break;}
            yield return Until(()=>inspection.IsOpen,"Inspection opens for "+action+"; state="+game.State+", control="+game.CanControl+", completing="+inspection.IsCompleting+", stage="+(bool)inspection.CameraController+", current="+game.interaction.CurrentPrompt);yield return new WaitForSeconds(.3f);
        }
        IEnumerator Close(){inspection.Close();yield return Until(()=>!inspection.IsOpen&&game.CanControl,"Inspection closes");yield return null;yield return null;}
        void CaptureInspection(string name)
        {
            Directory.CreateDirectory(Visual);var target=inspection.Texture;Assert.That(target,Is.Not.Null);var previous=RenderTexture.active;var pixels=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            try{RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Visual,name+".png"),pixels.EncodeToPNG());}
            finally{RenderTexture.active=previous;UnityEngine.Object.Destroy(pixels);}
        }
        IEnumerator SecretaryAndKey()
        {
            game.player.Warp(new Vector3(-16.4f,.65f,28.2f),0);yield return Until(()=>game.narrative.HasSignal("bd.school.secretary"),"Secretary entry");
            Assert.That(flow.secretaryDoor.IsOpen&&!flow.secretaryDoor.locked,Is.True);Assert.That(flow.libraryDoor.locked,Is.True);
            yield return null;
            var secretaryPhone = PhoneMessages.For(game);
            if (secretaryPhone.IsOpen) secretaryPhone.Close();
            yield return Until(()=>game.CanControl,"Dismiss the secretary arrival notification");
            yield return Inspect(BeforeDemolitionSchoolAction.Cups,new Vector3(-16.4f,.65f,28.2f));Assert.That(inspection.IsOpen,Is.False,"Tracks stay on the floor.");yield return Close();Assert.That(game.narrative.HasSignal("bd.school.cups"),Is.True);
            var handle = flow.drawer.drawer.Find("Gaveta de madeira — móvel");
            Assert.That(handle,Is.Not.Null);
            yield return Aim(handle,new Vector3(-16.8f,.65f,28.1f));yield return Until(()=>game.interaction.Target==flow.drawer,"Drawer is physically reachable; actual="+game.interaction.CurrentPrompt,3);yield return Press(Key.E);
            yield return Until(()=>flow.drawer.OpenFraction>=.8f,"Drawer opened");
            yield return Inspect(BeforeDemolitionSchoolAction.Lunchbox,new Vector3(-16.8f,.65f,28.1f));yield return Close();
            yield return ReadSecretaryPhoneGate();Assert.That(flow.keyVisual.activeInHierarchy,Is.True);yield return Inspect(BeforeDemolitionSchoolAction.Key,new Vector3(-16.8f,.65f,28.1f));yield return Close();
            Assert.That(game.interaction.inventory.Contains(BeforeDemolitionSchoolFlow.KeyId),Is.True);Assert.That(flow.keyVisual.activeSelf,Is.False);
        }
        IEnumerator Report()
        {
            yield return SecretaryAndKey();game.player.Warp(new Vector3(41.7f,.05f,55.8f),180);yield return Until(()=>game.narrative.HasSignal("bd.school.library"),"Library entry");
            var tutorials = game.tutorials;
            while (tutorials && (tutorials.BlocksWhistle || tutorials.IsOpen))
            {
                yield return Until(() => tutorials.IsOpen, "Library tutorial appears", 15);
                tutorials.Close();
                yield return Until(() => game.CanControl, "Dismiss the library tutorial");
            }
            yield return ReadWhistleLoreFromMural();yield return Aim(Target(BeforeDemolitionSchoolAction.Report).transform,new Vector3(46.9f,.05f,48));Directory.CreateDirectory(Visual);ScreenCapture.CaptureScreenshot(Path.Combine(Visual,"mural-biblioteca-screen.png"));yield return null;yield return null;
            yield return Inspect(BeforeDemolitionSchoolAction.Report,new Vector3(46.9f,.05f,45.15f));yield return Close();Assert.That(flow.ReportTaken&&!flow.TapeHeard&&flow.libraryDoor.locked&&!flow.libraryDoor.IsOpen,Is.True);
        }
        [UnityTest,Timeout(120000)]public IEnumerator PhysicalInteractionsReachTheTapeAndOnlyItsFullPlaybackOpensTheExit()
        {
            yield return Report();Assert.That(flow.PhoneRinging,Is.True);
            yield return Aim(Target(BeforeDemolitionSchoolAction.Phone).transform,new Vector3(43.75f,.05f,54.2f));yield return Until(()=>game.interaction.Target==Target(BeforeDemolitionSchoolAction.Phone),"Phone target");yield return Press(Key.E);Assert.That(flow.State.phoneAnswered&&!flow.PhoneRinging,Is.True);
            var messages=PhoneMessages.For(game);
            Assert.That(messages.State.triggers.Contains(PhoneTrigger.LibraryWarning),Is.True);
            yield return Until(()=>messages.IsOpen,"Incoming warning opens the phone automatically");
            Assert.That(messages.State.received.Single(m=>m.id=="library.warning").read,Is.True);
            Assert.That(messages.View.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="não responde ela"&&t.font==messages.catalog.childFont),Is.True);
            yield return Press(Key.P);yield return Until(()=>game.CanControl,"Close text messages before the tape");
            yield return Inspect(BeforeDemolitionSchoolAction.Tape,new Vector3(43.05f,.05f,54.2f));Assert.That(inspection.HasRecording(0),Is.True);yield return Press(Key.R);yield return Until(()=>inspection.RecordingPlaying,"Tape playback starts");
            Assert.That(inspection.RecordingText,Does.EndWith("Se alguém ouvir isso, o apito está com ele."));yield return new WaitForSeconds(.3f);Assert.That(flow.TapeHeard,Is.False);yield return Close();Assert.That(flow.libraryDoor.locked&&!flow.TapeHeard,Is.True);Assert.That(game.save.Save(),Is.True,"Incomplete tape can still be saved");
            yield return Inspect(BeforeDemolitionSchoolAction.Tape,new Vector3(43.05f,.05f,54.2f));
            var recording = inspection.CurrentItem.recording.clips.Single();
            yield return Press(Key.R);game.Pause();yield return new WaitForSecondsRealtime(1.2f);Assert.That(flow.TapeHeard,Is.False,"A paused tape cannot complete");game.Resume();
            yield return Until(()=>flow.TapeHeard,"Playback reaches the actual completion callback",recording.length+8);yield return Close();Assert.That(flow.libraryDoor.locked,Is.False);
            game.player.Warp(new Vector3(41.7f,.05f,59.2f),0);yield return Until(()=>game.narrative.Progress.pendingChapterId=="bd.four-students","Chapter complete");Assert.That(game.narrative.Progress.endingSeen,Is.False);Assert.That(game.save.Save(),Is.True);
        }
        [UnityTest,Timeout(120000)]public IEnumerator RestoringTheReportCheckpointKeepsTheTrapAndDoesNotReplayTheAnsweredCall()
        {
            yield return Report();flow.Interact(BeforeDemolitionSchoolAction.Phone,game.interaction);Assert.That(game.save.Save(),Is.True);Assert.That(game.save.TryReadCurrent(out var checkpoint),Is.True);
            var sanity=UnityEngine.Object.FindAnyObjectByType<SchoolSanityController>();sanity.enabled=false;
            Assert.That(game.save.RestoreSnapshot(checkpoint),Is.True);game.narrative.InitializeAfterSession(true);yield return null;yield return null;
            Assert.That(flow.State.phoneAnswered,Is.True);Assert.That(flow.PhoneRinging,Is.False);Assert.That(flow.libraryDoor.locked&&!flow.libraryDoor.IsOpen,Is.True);Assert.That(flow.reportVisual.activeSelf,Is.False);Assert.That(flow.keyVisual.activeSelf,Is.False);
            Assert.That(game.interaction.inventory.items.Count(i=>i.id==BeforeDemolitionSchoolFlow.ReportId),Is.EqualTo(1));
        }
        [UnityTest,Timeout(120000)]public IEnumerator TheKeyUnlocksBothPhysicalLeavesAndTheCapsuleCanWalkIntoTheLibrary()
        {
            yield return SecretaryAndKey();yield return Aim(flow.libraryDoor.leafCollider.transform,new Vector3(41.7f,.05f,60));
            yield return Until(()=>game.interaction.Target==flow.libraryDoor||game.interaction.Target==flow.libraryDoor.pairedLeaf,"Library door receives normal input");yield return Press(Key.E);
            yield return Until(()=>flow.libraryDoor.IsOpen&&flow.libraryDoor.pairedLeaf.IsOpen&&!flow.libraryDoor.locked&&!flow.libraryDoor.pairedLeaf.locked,"Both leaves unlock");yield return new WaitForSeconds(1.4f);
            for(int i=0;i<40;i++){game.player.Controller.Move(Vector3.back*.1f+Vector3.down*.025f);if(i%4==3)yield return null;}
            Assert.That(game.player.transform.position.z,Is.LessThan(57),"Doorway clears the actual player capsule");yield return Until(()=>game.narrative.HasSignal("bd.school.library"),"Walking enters the library");
        }
        [UnityTest,Timeout(120000)]public IEnumerator DarknessBlackoutReturnsToTheLatestCheckpointWithProgressPreserved()
        {
            yield return SecretaryAndKey();Assert.That(game.save.Save(),Is.True);var sanity=UnityEngine.Object.FindAnyObjectByType<SchoolSanityController>();Assert.That(sanity.IsSchoolActive,Is.True);sanity.blackoutSeconds=.15f;sanity.wakeSeconds=.15f;
            game.player.Warp(new Vector3(0,.7f,22),0);game.save.flashlight.Restore(0,false);sanity.darknessGraceSeconds=0;sanity.emptyBatteryLossPerSecond=100;sanity.darknessLossPerSecond=100;sanity.sanity=.01f;sanity.AdvanceSanity(.1f,false);yield return Until(()=>game.IsBlackingOut,"Empty-battery darkness causes a blackout");yield return Until(()=>game.CanControl&&!game.IsBlackingOut,"Blackout restores checkpoint");
            Assert.That(game.State,Is.EqualTo(GameState.Playing));Assert.That(sanity.sanity,Is.InRange(55,65));Assert.That(game.narrative.HasSignal("bd.school.key"),Is.True);Assert.That(game.interaction.inventory.Contains(BeforeDemolitionSchoolFlow.KeyId),Is.True);Assert.That(Vector3.Distance(game.player.transform.position,new Vector3(-16.8f,.65f,28.1f)),Is.LessThan(1));
        }
        [UnityTearDown]public IEnumerator TearDown()
        {
            if(!guarded)yield break;SceneManager.sceneLoaded-=Loaded;InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            var loaded=new List<Scene>();for(int i=0;i<SceneManager.sceneCount;i++)loaded.Add(SceneManager.GetSceneAt(i));var blank=SceneManager.CreateScene("School cleanup "+Guid.NewGuid().ToString("N"));SceneManager.SetActiveScene(blank);Time.timeScale=1;AudioListener.pause=false;
            foreach(var s in loaded)if(s.IsValid()&&s.isLoaded)yield return SceneManager.UnloadSceneAsync(s);
            SaveSystem.ClearChapterCarry();SaveSystem.TestSaveDirectory=oldDirectory;foreach(var pair in flags)if(pair.Value.Item1)PlayerPrefs.SetInt(Prefix+pair.Key,pair.Value.Item2);else PlayerPrefs.DeleteKey(Prefix+pair.Key);if(hadScene)PlayerPrefs.SetString(Prefix+"SaveScene",oldScene);else PlayerPrefs.DeleteKey(Prefix+"SaveScene");
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditorInput;Application.runInBackground=background;Time.timeScale=timeScale;AudioListener.pause=oldAudio;
            foreach(string file in Directory.GetFiles(directory))File.Delete(file);Directory.Delete(directory);guarded=false;
        }
    }
}
#endif


