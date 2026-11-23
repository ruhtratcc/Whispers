// Verifica SetUp, TearDown, ActualCluesStationaryPresencesWallAndDoorFinishOnlyChapterFour, RealCheckpointAndAutomaticBlackoutKeepLatestChapterFourProgress, ActualChapterThreeFinalDrawingHandsOffInPlaceWithoutReloadOrLostEquipment.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    /// <summary>
    /// Real saved SchoolCEEP objects and raycasts. Supported warps stage the authored
    /// investigation; these tests do not claim a continuous walk through the campus.
    /// Checkpoints use a disposable file and original preferences/files are restored.
    /// </summary>
    [Category("SchoolChapterFourScene"), Explicit("Historical seven-chapter scene. Replaced by SchoolSimplifiedSceneTests for the installed six-chapter campaign.")]
    public sealed class SchoolChapterFourSceneTests
    {
        GameSession game;
        SchoolLeftBehindFlow flow;
        SchoolOpeningFlow opening;
        SchoolSanityController sanity;
        Keyboard keyboard;
        InputSettings.BackgroundBehavior originalBackground;
        InputSettings.EditorInputBehaviorInPlayMode originalEditorInput;
        bool originalRunInBackground;
        string testSaveName, savedScene;
        bool hadSavedScene, hadResume, hadCarry;
        int resume, carry;
        byte[] save, backup, temporary;
        readonly List<StoryBook> clonedBooks=new List<StoryBook>();
        readonly HashSet<GameSession> clonedSessions=new HashSet<GameSession>();
        string RealSavePath => Path.Combine(Application.persistentDataPath, "save.json");
        int WorldMask => ~LayerMask.GetMask("Player");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            save=ReadFile(RealSavePath); backup=ReadFile(RealSavePath+".bak"); temporary=ReadFile(RealSavePath+".tmp");
            hadSavedScene=PlayerPrefs.HasKey("Game.SaveScene"); hadResume=PlayerPrefs.HasKey("Game.ResumeRequested");
            hadCarry=PlayerPrefs.HasKey("Game.StoryCarryRequested");
            savedScene=PlayerPrefs.GetString("Game.SaveScene", ""); resume=PlayerPrefs.GetInt("Game.ResumeRequested",0);
            carry=PlayerPrefs.GetInt("Game.StoryCarryRequested",0);
            PlayerPrefs.SetInt("Game.ResumeRequested",0); PlayerPrefs.SetInt("Game.StoryCarryRequested",0);
            testSaveName="school-chapter-four-fixture-"+Guid.NewGuid().ToString("N")+".json";
            originalBackground=InputSystem.settings.backgroundBehavior;
            originalEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            originalRunInBackground=Application.runInBackground;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground=true; Time.timeScale=1; AudioListener.pause=false;
            SceneManager.sceneLoaded+=RedirectInitialSave;
            yield return SceneManager.LoadSceneAsync("SchoolCEEP");
            yield return Until(()=>GameSession.Instance && GameSession.Instance.narrative && GameSession.Instance.narrative.IsInitialized,15);
            BindScene();
            Assert.That(flow,Is.Not.Null,"The authorized Chapter 4 must be installed in the real school.");
            Assert.That(flow.director,Is.SameAs(game.narrative));
            Assert.That(game.save.fileName,Is.EqualTo(testSaveName));
            keyboard=InputSystem.AddDevice<Keyboard>(); game.input.Actions.devices=new InputDevice[]{keyboard};
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update();
            ConfigureRuntimeTimings();
            game.interaction.inventory.Restore(IncomingItems()); game.save.flashlight.Restore(137,true);
            yield return null; yield return null;
        }

        void BindScene()
        {
            game=GameSession.Instance;
            flow=UnityEngine.Object.FindAnyObjectByType<SchoolLeftBehindFlow>();
            opening=UnityEngine.Object.FindAnyObjectByType<SchoolOpeningFlow>();
            sanity=UnityEngine.Object.FindAnyObjectByType<SchoolSanityController>();
        }
        void ConfigureRuntimeTimings()
        {
            game.player.view.bobEnabled=false; game.player.view.swayEnabled=false;
            game.narrative.transitionView.fadeSeconds=.12f; game.narrative.transitionView.cardSeconds=.20f;
            // Each loaded session owns a disposable book, including after blackout reload.
            // Shortening captions must never mutate the shared asset, even in memory.
            if(clonedSessions.Add(game))
            {
                var clone=UnityEngine.Object.Instantiate(game.narrative.book);
                clone.name=game.narrative.book.name+" — ChapterFour scene fixture";
                // Keep this fixture focused on Chapter 4's final cards. Chapter 5's
                // scene tests separately exercise the real in-place continuation.
                clone.Chapter("memories").sceneName="";
                clone.hideFlags=HideFlags.DontSave;
                clonedBooks.Add(clone);
                game.narrative.book=clone;
            }
            foreach(var sequence in game.narrative.book.sequences)
                foreach(var line in sequence.lines??Array.Empty<StoryLine>()) if(line!=null)line.seconds=.12f;
        }
        static List<InventoryItem> IncomingItems()=>new List<InventoryItem>
        {
            new InventoryItem{id="office_equipment",title="Equipamento do escritório"},
            new InventoryItem{id="office_photo",title="Foto de casa",text="Uma lembrança anterior.",note=true},
            new InventoryItem{id="school_documents",title="Registro do capítulo 3",note=true},
            new InventoryItem{id="school_photograph",title="Turma do capítulo 3",note=true},
            new InventoryItem{id="school_drawing",title="Desenho do capítulo 3",note=true}
        };
        void RedirectInitialSave(Scene loaded,LoadSceneMode mode)
        {
            if(loaded.name!="SchoolCEEP")return;
            foreach(var system in loaded.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<SaveSystem>(true)))
                system.fileName=testSaveName;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded-=RedirectInitialSave; Time.timeScale=1; AudioListener.pause=false;
            if(keyboard!=null && keyboard.added)InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior=originalBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode=originalEditorInput;
            Application.runInBackground=originalRunInBackground;
            var loaded=SceneManager.GetSceneByName("SchoolCEEP");
            var blank=SceneManager.GetSceneByName("School-chapter-four-cleanup");
            if(!blank.IsValid() || !blank.isLoaded)blank=SceneManager.CreateScene("School-chapter-four-cleanup");
            SceneManager.SetActiveScene(blank); if(loaded.isLoaded)yield return SceneManager.UnloadSceneAsync(loaded);
            foreach(var clone in clonedBooks)if(clone)UnityEngine.Object.Destroy(clone);
            clonedBooks.Clear();clonedSessions.Clear();
            RestoreFile(RealSavePath,save); RestoreFile(RealSavePath+".bak",backup); RestoreFile(RealSavePath+".tmp",temporary);
            if(!string.IsNullOrEmpty(testSaveName))foreach(string suffix in new[]{"",".bak",".tmp"})
            {string path=Path.Combine(Application.persistentDataPath,testSaveName+suffix);if(File.Exists(path))File.Delete(path);}
            if(hadSavedScene)PlayerPrefs.SetString("Game.SaveScene",savedScene);else PlayerPrefs.DeleteKey("Game.SaveScene");
            if(hadResume)PlayerPrefs.SetInt("Game.ResumeRequested",resume);else PlayerPrefs.DeleteKey("Game.ResumeRequested");
            if(hadCarry)PlayerPrefs.SetInt("Game.StoryCarryRequested",carry);else PlayerPrefs.DeleteKey("Game.StoryCarryRequested");
            PlayerPrefs.Save();
        }
        static byte[] ReadFile(string path)=>File.Exists(path)?File.ReadAllBytes(path):null;
        static void RestoreFile(string path,byte[] original)
        {if(original!=null)File.WriteAllBytes(path,original);else if(File.Exists(path))File.Delete(path);}
        IEnumerator Until(Func<bool> condition,float seconds=12)
        {
            float deadline=Time.realtimeSinceStartup+seconds;
            while(!condition() && Time.realtimeSinceStartup<deadline)yield return null;
            var session=game?game:GameSession.Instance;
            string state=session && session.narrative?session.narrative.NextSignal+", state="+session.State
                +", control="+session.CanControl+", sequence="+session.narrative.SequencePlaying:"initializing";
            Assert.That(condition(),Is.True,"Chapter 4 interaction deadline: "+state);
        }

        NarrativeProgress PreviousChapters()
        {
            var progress=new NarrativeProgress{chapterId="left-behind"};
            foreach(var chapter in game.narrative.book.chapters.Take(3))
            {
                foreach(var milestone in chapter.milestones)progress.milestones.Add(milestone.id);
                progress.completedChapters.Add(chapter.id);
            }
            progress.sequences.AddRange(new[]{"road.arrival-call","school.photo-inscription","school.blackboard-inscription","school.drawing-inscription"});
            return progress;
        }
        void StageChapterFour()
        {
            // Stage only prior narrative. Current chapter clues are still read through the player raycast.
            foreach(var door in UnityEngine.Object.FindObjectsByType<InteractiveDoor>())
                if(door!=opening.entryDoor && door!=opening.entryDoor.pairedLeaf && door!=flow.destinationDoor)
                {door.SetLocked(false);door.SetOpen(true,true);}
            opening.entryDoor.SetLocked(true);opening.entryDoor.SetOpen(false,true);
            opening.entryDoor.pairedLeaf.SetLocked(true);opening.entryDoor.pairedLeaf.SetOpen(false,true);
            Physics.SyncTransforms();
            Vector3 feet=SupportedFeet(new Vector3(-65.62f,.6f,50)); WarpAim(feet,new Vector3(-65.62f,2.2f,30));
            game.interaction.inventory.Restore(IncomingItems());game.save.flashlight.Restore(137,true);
            sanity.sanity=87;
            game.narrative.RestoreProgress(PreviousChapters()); game.narrative.InitializeAfterSession(true);
            Assert.That(game.narrative.NextSignal,Is.EqualTo("left-behind.documents-found"));
            Assert.That(flow.IsChapterActive,Is.True);
            Assert.That(flow.destinationDoor.persistentId,Is.EqualTo("school.door.0.58"));
            Assert.That(flow.destinationDoor.locked,Is.True);
            Assert.That(sanity.anchors.Length,Is.EqualTo(6),"Chapter-owned anchors are installed without activating the next chapter's recovery.");
            Assert.That(UnityEngine.Object.FindAnyObjectByType<SchoolMemoriesFlow>().memoryAnchor.activeSelf,Is.False);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<SchoolTerraceFlow>().memoryAnchor.activeSelf,Is.False);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<SchoolLastExitFlow>().memoryAnchor.activeSelf,Is.False);
            Assert.That(sanity.PrepareChapterRestart(out var start),Is.True);
            Assert.That(start.narrative.chapterId,Is.EqualTo("left-behind"));
            Assert.That(start.inventory.Any(item=>item.id=="school_drawing"),Is.True);
        }

        Vector3 SupportedFeet(Vector3 point)
        {
            Assert.That(Physics.Raycast(point+Vector3.up*1.1f,Vector3.down,out var hit,4,WorldMask,QueryTriggerInteraction.Ignore),Is.True);
            Assert.That(hit.normal.y,Is.GreaterThan(.75f));
            Vector3 feet=hit.point+Vector3.up*.045f;
            Assert.That(CapsuleClear(feet),Is.True,"Staged feet must fit the real player volume: "+feet);
            return feet;
        }
        bool CapsuleClear(Vector3 feet)
        {
            float radius=game.player.Controller.radius,height=game.player.standingHeight;
            return !Physics.CheckCapsule(feet+Vector3.up*(radius+.02f),feet+Vector3.up*(height-radius-.02f),
                radius,WorldMask,QueryTriggerInteraction.Ignore);
        }
        bool ClearRay(Vector3 eye,Vector3 target,Component clue=null)
        {
            Vector3 direction=target-eye;
            if(!Physics.Raycast(eye,direction.normalized,out var hit,direction.magnitude,WorldMask,QueryTriggerInteraction.Ignore))return clue==null;
            if(clue is SchoolLeftBehindClue left)return hit.collider.GetComponentInParent<SchoolLeftBehindClue>()==left;
            if(clue is SchoolOpeningClue initial)return hit.collider.GetComponentInParent<SchoolOpeningClue>()==initial;
            return hit.distance>=direction.magnitude-.15f;
        }
        Vector3 SafeFeet(Vector3 target,float[] radii,Component clue=null)
        {
            var candidates=new List<Vector3>();
            foreach(float distance in radii)for(int i=0;i<32;i++)
            {
                float angle=i*Mathf.PI/16;
                Vector3 candidate=target+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*distance;
                if(!Physics.Raycast(candidate+Vector3.up*.75f,Vector3.down,out var floor,4,WorldMask,QueryTriggerInteraction.Ignore)
                    || floor.normal.y<.75f)continue;
                Vector3 feet=floor.point+Vector3.up*.045f;if(!CapsuleClear(feet))continue;
                Vector3 eye=feet+Vector3.up*game.player.EyeHeight;
                if(clue && Vector3.Distance(eye,target)>2.5f)continue;
                if(ClearRay(eye,target,clue))candidates.Add(feet);
            }
            Assert.That(candidates.Count,Is.GreaterThan(0),"No supported player position sees "+(clue?clue.name:target.ToString()));
            float ground=candidates.Min(point=>point.y);
            return candidates.Where(point=>point.y<ground+.12f).OrderBy(point=>Vector3.Distance(point,target)).First();
        }
        void WarpAim(Vector3 feet,Vector3 target)
        {
            Vector3 delta=target-(feet+Vector3.up*game.player.EyeHeight);
            game.player.Warp(feet,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,-Mathf.Asin(delta.normalized.y)*Mathf.Rad2Deg);
            Physics.SyncTransforms();
        }
        IEnumerator ReadActualClue(SchoolLeftBehindClue clue)
        {
            Assert.That(clue.CanInteract(game.interaction),Is.True,"Narrative must permit "+clue.name);
            WarpAim(SafeFeet(clue.transform.position,new[]{1.2f,1.5f,1.8f},clue),clue.transform.position);
            yield return null;yield return null;
            Assert.That(game.interaction.Target,Is.SameAs(clue),"Actual player raycast must hit "+clue.name);
            game.interaction.Target.Interact(game.interaction);yield return null;
            var inspection=UnityEngine.Object.FindAnyObjectByType<InspectionManager>();
            Assert.That(inspection && inspection.IsOpen,Is.True,"The actual clue opens its 3D inspection.");
            inspection.Close();
            yield return Until(()=>!inspection.IsOpen && !inspection.IsCompleting);
        }
        IEnumerator FindKey()
        {
            yield return ReadActualClue(flow.reportClue);
            Assert.That(game.State,Is.EqualTo(GameState.Playing));
            var report=game.interaction.inventory.items.Single(item=>item.id=="old_disappearance_report");
            StringAssert.Contains("João",report.text);
            Assert.That(report.inspectionCount,Is.EqualTo(1));
            yield return null;
            yield return ReadActualClue(flow.photographClue);
            yield return Until(()=>game.narrative.HasSequence("left-behind.photo-inscription") && game.CanControl && !game.narrative.SequencePlaying);
            yield return ReadActualClue(flow.keyClue);
            yield return Until(()=>game.narrative.NextSignal=="left-behind.demon-appeared");
            Assert.That(game.interaction.inventory.Contains("old_key"),Is.True);
        }
        Vector3 DemonTarget=>flow.demonVisual.position;
        IEnumerator FirstPresence()
        {
            Vector3 point=flow.returnVolume.transform.TransformPoint(flow.returnVolume.center);
            WarpAim(SupportedFeet(point),flow.distantPose.position);
            yield return Until(()=>game.narrative.HasSignal("left-behind.demon-appeared") && flow.demonVisual.gameObject.activeSelf);
            Assert.That(flow.demonVisual.GetComponentsInChildren<Collider>(true).Length,Is.Zero);
            Assert.That(flow.demonVisual.GetComponentsInChildren<EnemyBrain>(true).Length,Is.Zero);
        }
        IEnumerator WalkReturnCorridor()
        {
            // This local return is continuous W input against the real CharacterController.
            // Earlier scene stages still use the explicitly documented supported warps.
            Vector3 start=game.player.transform.position;
            Assert.That(start.z,Is.EqualTo(31).Within(.10f));
            WarpAim(start,start+Vector3.forward*20+Vector3.up*game.player.EyeHeight);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));InputSystem.Update();
            float deadline=Time.realtimeSinceStartup+25;
            try
            {
                while(game.player.transform.position.z<39 && Time.realtimeSinceStartup<deadline)
                {
                    Assert.That(Mathf.Abs(game.player.transform.position.x-start.x),Is.LessThan(.25f),"The real return walk must stay within the straight corridor.");
                    yield return null;
                }
            }
            finally
            {InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();}
            Vector3 end=game.player.transform.position;
            Assert.That(end.z,Is.GreaterThanOrEqualTo(39),"W input was blocked before the corridor relief point.");
            Assert.That(end.z-start.z,Is.GreaterThanOrEqualTo(7.9f));
            Assert.That(Mathf.Abs(end.x-start.x),Is.LessThan(.25f));
            Assert.That(CapsuleClear(end+Vector3.up*.045f),Is.True,"The final walking capsule must remain clear of corridor walls and doors.");
            Assert.That(Physics.Raycast(end+Vector3.up*.3f,Vector3.down,out var floor,.7f,WorldMask,QueryTriggerInteraction.Ignore),Is.True);
            Assert.That(floor.normal.y,Is.GreaterThan(.75f));
        }

        [UnityTest]
        public IEnumerator ActualCluesStationaryPresencesWallAndDoorFinishOnlyChapterFour()
        {
            StageChapterFour();yield return FindKey();yield return FirstPresence();
            Vector3 first=flow.demonVisual.transform.position;Quaternion rotation=flow.demonVisual.transform.rotation;
            game.Pause(); flow.AdvanceFlow(90);
            Assert.That(flow.demonVisual.transform.position,Is.EqualTo(first));
            Assert.That(flow.demonVisual.transform.rotation,Is.EqualTo(rotation));
            Assert.That(game.narrative.HasSignal("left-behind.chase-started"),Is.False);
            Assert.That(flow.demonVisual.gameObject.activeSelf,Is.True,"Pause cannot consume the presence.");
            game.Resume();yield return null;
            // No pursuit while João looks at the apparition: it remains at one authored pose.
            WarpAim(SupportedFeet(new Vector3(-65.62f,.6f,31)),DemonTarget);
            yield return null;
            Assert.That(flow.demonVisual.transform.position,Is.EqualTo(first));
            WarpAim(SupportedFeet(new Vector3(-65.62f,.6f,31)),new Vector3(-65.62f,2.1f,20));
            yield return Until(()=>!flow.demonVisual.gameObject.activeSelf);
            yield return Until(()=>game.narrative.HasSignal("left-behind.chase-started") && flow.demonVisual.gameObject.activeSelf);
            Assert.That(Vector3.Distance(flow.demonVisual.transform.position,flow.closerPose.position),Is.LessThan(.01f));
            Vector3 second=flow.demonVisual.transform.position;
            WarpAim(SupportedFeet(new Vector3(-65.62f,.6f,31)),DemonTarget);yield return null;
            Assert.That(flow.demonVisual.transform.position,Is.EqualTo(second));
            WarpAim(SupportedFeet(new Vector3(-65.62f,.6f,31)),new Vector3(-65.62f,2.1f,20));
            yield return Until(()=>!flow.demonVisual.gameObject.activeSelf);
            yield return WalkReturnCorridor();
            yield return Until(()=>game.narrative.HasSignal("left-behind.safe-place"));
            WarpAim(SafeFeet(flow.wallAnchor.position,new[]{1.5f,2f,2.5f}),flow.wallAnchor.position);
            yield return Until(()=>game.narrative.HasSequence("left-behind.wall-inscription") && game.CanControl && !game.narrative.SequencePlaying);
            Assert.That(game.narrative.NextSignal,Is.EqualTo("left-behind.locked-area-opened"));
            var door=flow.destinationDoor;var target=door.leafCollider.bounds.center;
            Vector3 approach=SupportedFeet(new Vector3(-65.62372f,.6f,43.65583f));
            WarpAim(approach,target);yield return null;yield return null;
            Assert.That(game.interaction.Target,Is.SameAs(door),"The key must reach the actual existing door through the player's raycast.");
            game.interaction.Target.Interact(game.interaction);
            yield return Until(()=>game.State==GameState.StoryScreen);
            yield return Until(()=>game.narrative.transitionView.returnButton.gameObject.activeSelf);
            Assert.That(door.locked,Is.False);Assert.That(door.IsOpen,Is.True);
            Assert.That(game.narrative.Progress.milestones.Count(id=>id.StartsWith("left-behind.")),Is.EqualTo(8));
            Assert.That(game.narrative.Progress.chapterId,Is.EqualTo("left-behind"));
            Assert.That(game.narrative.Progress.pendingChapterId,Is.EqualTo("memories"));
            Assert.That(game.narrative.book.Chapter("memories").sceneName,Is.Empty);
            StringAssert.Contains("CAPÍTULO 4 CONCLUÍDO",game.narrative.transitionView.heading.text);
            StringAssert.Contains("CAPÍTULO 5",game.narrative.transitionView.body.text);
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("SchoolCEEP"));
            Assert.That(UnityEngine.Object.FindObjectsByType<EnemyBrain>().Length,Is.Zero);
        }

        [UnityTest] [Category("BlackoutCheckpoint"), Category("BlackoutInspection")]
        public IEnumerator RealCheckpointAndAutomaticBlackoutKeepLatestChapterFourProgress()
        {
            StageChapterFour();
            Assert.That(sanity.PrepareChapterRestart(out var baseline),Is.True);
            yield return FindKey();
            // Eligibility is saved separately from observation. João may leave the
            // trigger before noticing the silhouette, then reload outside its bounds.
            Vector3 away=new Vector3(-65.62f,2.2f,20);
            WarpAim(SupportedFeet(new Vector3(-65.62f,.6f,32)),away);
            yield return Until(()=>flow.demonVisual.gameObject.activeSelf);
            Assert.That(game.narrative.HasSignal("left-behind.demon-appeared"),Is.False);
            WarpAim(SupportedFeet(new Vector3(-65.62f,.6f,36)),away);
            Assert.That(flow.returnVolume.bounds.Contains(game.player.transform.position+Vector3.up),Is.False);
            Assert.That(flow.Observes(flow.distantPose,flow.apparitionDistance),Is.False);
            int eligibleAccepted=0;Action<string> eligibleHandler=id=>eligibleAccepted++;
            game.narrative.SignalAccepted+=eligibleHandler;
            Assert.That(game.save.Save(),Is.True);float eligibleSanity=sanity.sanity;
            Assert.That(game.save.LoadCurrent(),Is.True);game.narrative.InitializeAfterSession(true);
            Assert.That(flow.demonVisual.gameObject.activeSelf,Is.True,"An unseen eligible presence must survive reload outside the return trigger.");
            Assert.That(flow.returnVolume.bounds.Contains(game.player.transform.position+Vector3.up),Is.False);
            Assert.That(game.narrative.NextSignal,Is.EqualTo("left-behind.demon-appeared"));
            Assert.That(game.narrative.HasSignal("left-behind.demon-appeared"),Is.False);
            Assert.That(eligibleAccepted,Is.Zero,"Restoring eligibility must not accept an observation or replay an apparition shock.");
            Assert.That(sanity.sanity,Is.EqualTo(eligibleSanity).Within(.01f));
            game.narrative.SignalAccepted-=eligibleHandler;
            WarpAim(SupportedFeet(new Vector3(-65.62f,.6f,36)),flow.distantPose.position);
            yield return Until(()=>game.narrative.HasSignal("left-behind.demon-appeared") && flow.demonVisual.gameObject.activeSelf);
            int accepted=0;Action<string> acceptedHandler=id=>accepted++;
            game.narrative.SignalAccepted+=acceptedHandler;
            Assert.That(game.save.Save(),Is.True);float savedSanity=sanity.sanity;
            Assert.That(SaveSystem.TryRead(game.save.SavePath,out var checkpoint),Is.True);
            Assert.That(checkpoint.objects.Any(item=>item.id==flow.persistentId),Is.True);
            Assert.That(game.save.LoadCurrent(),Is.True);game.narrative.InitializeAfterSession(true);
            Assert.That(game.narrative.NextSignal,Is.EqualTo("left-behind.chase-started"));
            Assert.That(accepted,Is.Zero,"Checkpoint reconstruction must not replay revelations or apparition shocks.");
            Assert.That(sanity.sanity,Is.EqualTo(savedSanity).Within(.01f));
            Assert.That(game.interaction.inventory.Contains("old_key"),Is.True);
            Assert.That(flow.reportClue.CanInteract(game.interaction),Is.False);
            Assert.That(flow.photographClue.CanInteract(game.interaction),Is.False);
            Assert.That(flow.keyClue.CanInteract(game.interaction),Is.False);
            game.narrative.SignalAccepted-=acceptedHandler;
            yield return Until(()=>game.CanControl && !game.narrative.SequencePlaying);
            Assert.That(game.save.Save(),Is.True);
            Assert.That(SaveSystem.TryRead(game.save.SavePath,out checkpoint),Is.True);
            var session=game;
            sanity.sanity=0; sanity.AdvanceSanity(.1f,false);
            Assert.That(sanity.BlackingOut,Is.True);
            Assert.That(game.State,Is.EqualTo(GameState.BlackingOut));
            Assert.That(game.save.Save(),Is.False);
            sanity.AdvanceSanity(sanity.blackoutSeconds+.1f,false);
            StringAssert.Contains("Você nunca deveria ter voltado",game.hud.subtitle.text);
            yield return Until(()=>game.State==GameState.Playing && !sanity.BlackingOut,8);
            Assert.That(GameSession.Instance,Is.SameAs(session));
            Assert.That(game.hud.gameOverPanel.activeSelf,Is.False);
            Assert.That(game.save.fileName,Is.EqualTo(testSaveName));
            Assert.That(game.narrative.Progress.chapterId,Is.EqualTo("left-behind"));
            Assert.That(game.narrative.NextSignal,Is.EqualTo("left-behind.chase-started"));
            Assert.That(JsonUtility.ToJson(game.narrative.CaptureProgress()),Is.EqualTo(JsonUtility.ToJson(checkpoint.narrative)));
            Assert.That(game.narrative.HasSignal("school.blackout"),Is.True);
            Assert.That(game.narrative.HasSequence("school.drawing-inscription"),Is.True);
            Assert.That(game.narrative.Progress.completedChapters.Contains("school"),Is.True);
            foreach(string item in new[]{"office_equipment","office_photo","school_documents","school_photograph","school_drawing"})
                Assert.That(game.interaction.inventory.Contains(item),Is.True,"Blackout must retain saved item "+item);
            Assert.That(game.interaction.inventory.Contains("old_key"),Is.True);
            Assert.That(game.save.flashlight.BatterySeconds,Is.EqualTo(checkpoint.battery).Within(1));
            Assert.That(Vector3.Distance(game.player.transform.position,checkpoint.position),Is.LessThan(.10f));
            Assert.That(sanity.sanity,Is.EqualTo(60).Within(.2f));
            Assert.That(flow.destinationDoor.locked,Is.True);Assert.That(flow.demonVisual.gameObject.activeSelf,Is.True);
            Assert.That(flow.reportClue.CanInteract(game.interaction),Is.False);
        }

        [UnityTest]
        public IEnumerator ActualChapterThreeFinalDrawingHandsOffInPlaceWithoutReloadOrLostEquipment()
        {
            // Prior Chapter 3 milestones are staged; its actual drawing interaction,
            // blackout, cards and next-chapter handoff run through production code.
            var progress=PreviousChapters();progress.chapterId="school";progress.completedChapters.Remove("school");
            progress.milestones.RemoveAll(id=>id=="school.drawing" || id=="school.blackout");
            progress.sequences.Remove("school.drawing-inscription");
            game.interaction.inventory.Restore(IncomingItems().Where(item=>item.id!="school_drawing").ToList());
            game.save.flashlight.Restore(137,true);
            foreach(var door in UnityEngine.Object.FindObjectsByType<InteractiveDoor>())
                if(door!=opening.entryDoor && door!=opening.entryDoor.pairedLeaf){door.SetLocked(false);door.SetOpen(true,true);}
            game.narrative.RestoreProgress(progress);game.narrative.InitializeAfterSession(true);Physics.SyncTransforms();
            var drawing=opening.drawingClue;
            WarpAim(SafeFeet(drawing.transform.position,new[]{1.2f,1.5f,1.8f},drawing),drawing.transform.position);
            yield return null;yield return null;
            Assert.That(game.interaction.Target,Is.SameAs(drawing));
            var session=game;Vector3 before=game.player.transform.position;float battery=game.save.flashlight.BatterySeconds;
            game.interaction.Target.Interact(game.interaction);yield return null;
            var inspection=UnityEngine.Object.FindAnyObjectByType<InspectionManager>();
            Assert.That(inspection && inspection.IsOpen,Is.True,"The actual clue opens its 3D inspection.");
            inspection.Close();
            yield return Until(()=>!inspection.IsOpen && !inspection.IsCompleting);
            yield return Until(()=>game.narrative.Progress.chapterId=="left-behind" && game.CanControl,15);
            Assert.That(GameSession.Instance,Is.SameAs(session),"An in-place chapter change must not reload SchoolCEEP.");
            Assert.That(Vector3.Distance(game.player.transform.position,before),Is.LessThan(.10f));
            Assert.That(game.interaction.inventory.Contains("school_drawing"),Is.True);
            Assert.That(game.interaction.inventory.Contains("office_equipment"),Is.True);
            Assert.That(game.save.flashlight.BatterySeconds,Is.EqualTo(battery).Within(4));
            Assert.That(game.narrative.Progress.completedChapters.Contains("school"),Is.True);
            Assert.That(game.narrative.NextSignal,Is.EqualTo("left-behind.documents-found"));
            Assert.That(sanity.PrepareChapterRestart(out var start),Is.True);
            Assert.That(start.narrative.chapterId,Is.EqualTo("left-behind"));
            Assert.That(start.inventory.Any(item=>item.id=="school_drawing"),Is.True);
            Assert.That(Vector3.Distance(start.position,before),Is.LessThan(.10f));
        }
    }
}
