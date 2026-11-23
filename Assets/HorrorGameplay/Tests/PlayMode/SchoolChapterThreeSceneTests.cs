// Verifica SetUp, TearDown, ChapterThreeCarryJournalAndAutomaticBlackoutKeepLatestCheckpoint, CompletedRoadPresentsItsCardsThenLoadsTheRealChapterThree, UnpoweredEventReturnsToDarknessOnPauseWithoutRepeatedNarrativeShock.

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
    /// <summary>Real SchoolCEEP assembly and interactions, staged with safe runtime warps, never asset edits.</summary>
    [Category("SchoolChapterThreeScene"), Explicit("Historical seven-chapter scene. Replaced by SchoolSimplifiedSceneTests for the installed six-chapter campaign.")]
    public sealed class SchoolChapterThreeSceneTests
    {
        GameSession game;
        SchoolOpeningFlow flow;
        Keyboard keyboard;
        InputSettings.BackgroundBehavior originalBackground;
        InputSettings.EditorInputBehaviorInPlayMode originalEditorInput;
        bool originalRunInBackground;
        string testSaveName, savedScene;
        bool hadSavedScene, hadResume, hadCarry;
        int resume, carry;
        byte[] save, backup, temporary;
        string RealSavePath => Path.Combine(Application.persistentDataPath, "save.json");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            save = ReadFile(RealSavePath);
            backup = ReadFile(RealSavePath + ".bak");
            temporary = ReadFile(RealSavePath + ".tmp");
            hadSavedScene = PlayerPrefs.HasKey("Game.SaveScene");
            hadResume = PlayerPrefs.HasKey("Game.ResumeRequested");
            hadCarry = PlayerPrefs.HasKey("Game.StoryCarryRequested");
            savedScene = PlayerPrefs.GetString("Game.SaveScene", "");
            resume = PlayerPrefs.GetInt("Game.ResumeRequested", 0);
            carry = PlayerPrefs.GetInt("Game.StoryCarryRequested", 0);
            PlayerPrefs.SetInt("Game.ResumeRequested", 0);
            PlayerPrefs.SetInt("Game.StoryCarryRequested", 1);
            testSaveName = "school-opening-fixture-" + Guid.NewGuid().ToString("N") + ".json";
            var fixtureBook = ScriptableObject.CreateInstance<StoryBook>(); StoryContent.Populate(fixtureBook);
            var carryProgress = new NarrativeProgress { chapterId = "road", pendingChapterId = "school" };
            foreach (var chapter in fixtureBook.chapters.Take(2))
                foreach (var milestone in chapter.milestones) carryProgress.milestones.Add(milestone.id);
            carryProgress.completedChapters.AddRange(new[] { "prologue", "road" });
            carryProgress.sequences.Add("road.arrival-call");
            var carryData = new SaveData { scene = "RoadJourney", position = new Vector3(99, 0, 99),
                battery = 137, flashlightOn = true, narrative = carryProgress,
                inventory = new List<InventoryItem> { new InventoryItem { id="office_equipment", title="Equipamento do escritório" },
                    new InventoryItem { id="office_photo", title="Fotografia de casa", note=true, text="Uma lembrança anterior." } } };
            File.WriteAllText(Path.Combine(Application.persistentDataPath,testSaveName),JsonUtility.ToJson(carryData));
            UnityEngine.Object.Destroy(fixtureBook);
            originalBackground = InputSystem.settings.backgroundBehavior;
            originalEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            originalRunInBackground = Application.runInBackground;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground = true;
            Time.timeScale = 1;
            AudioListener.pause = false;
            // sceneLoaded precedes Start: even the initial automatic checkpoint goes to the disposable file.
            SceneManager.sceneLoaded += RedirectInitialSave;
            yield return SceneManager.LoadSceneAsync("SchoolCEEP");
            yield return Until(() => GameSession.Instance && GameSession.Instance.narrative
                && GameSession.Instance.narrative.IsInitialized, 12);
            game = GameSession.Instance;
            flow = UnityEngine.Object.FindAnyObjectByType<SchoolOpeningFlow>();
            Assert.That(flow, Is.Not.Null);
            Assert.That((flow.observationMask.value & LayerMask.GetMask("Player")), Is.Zero, "Observation must ignore João's own body collider.");
            Assert.That(flow.director, Is.SameAs(game.narrative));
            Assert.That(game.save.fileName, Is.EqualTo(testSaveName));
            keyboard = InputSystem.AddDevice<Keyboard>();
            game.input.Actions.devices = new InputDevice[] { keyboard };
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            game.player.view.bobEnabled = false;
            game.player.view.swayEnabled = false;
            yield return null;
            yield return null;
            Assert.That(game.narrative.Progress.chapterId, Is.EqualTo("school"));
            Assert.That(game.narrative.NextSignal, Is.EqualTo("school.entered"), "Spawn must remain outside the entry trigger.");
        }

        void RedirectInitialSave(Scene loaded, LoadSceneMode mode)
        {
            if (loaded.name != "SchoolCEEP" && loaded.name != "RoadJourney") return;
            foreach (var system in loaded.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SaveSystem>(true)))
                system.fileName = testSaveName;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= RedirectInitialSave;
            Time.timeScale = 1;
            AudioListener.pause = false;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = originalBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput;
            Application.runInBackground = originalRunInBackground;
            // Remove the gameplay scene before restoring prefs, so no deferred save can write afterwards.
            var loaded = SceneManager.GetSceneByName("SchoolCEEP");
            var blank = SceneManager.GetSceneByName("School-opening-scene-cleanup");
            if (!blank.IsValid() || !blank.isLoaded) blank = SceneManager.CreateScene("School-opening-scene-cleanup");
            SceneManager.SetActiveScene(blank);
            if (loaded.isLoaded) yield return SceneManager.UnloadSceneAsync(loaded);
            var road=SceneManager.GetSceneByName("RoadJourney");
            if(road.isLoaded)yield return SceneManager.UnloadSceneAsync(road);
            RestoreFile(RealSavePath, save);
            RestoreFile(RealSavePath + ".bak", backup);
            RestoreFile(RealSavePath + ".tmp", temporary);
            if (!string.IsNullOrEmpty(testSaveName))
                foreach (string suffix in new[] { "", ".bak", ".tmp" })
                {
                    string path = Path.Combine(Application.persistentDataPath, testSaveName + suffix);
                    if (File.Exists(path)) File.Delete(path);
                }
            if (hadSavedScene) PlayerPrefs.SetString("Game.SaveScene", savedScene); else PlayerPrefs.DeleteKey("Game.SaveScene");
            if (hadResume) PlayerPrefs.SetInt("Game.ResumeRequested", resume); else PlayerPrefs.DeleteKey("Game.ResumeRequested");
            if (hadCarry) PlayerPrefs.SetInt("Game.StoryCarryRequested", carry); else PlayerPrefs.DeleteKey("Game.StoryCarryRequested");
            PlayerPrefs.Save();
        }

        static byte[] ReadFile(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
        static void RestoreFile(string path, byte[] original)
        {
            if (original != null) File.WriteAllBytes(path, original);
            else if (File.Exists(path)) File.Delete(path);
        }
        IEnumerator Until(Func<bool> condition, float seconds = 8)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            var session=game?game:GameSession.Instance;
            string state=session&&session.narrative?session.narrative.NextSignal+", state="+session.State+", control="+session.CanControl+", sequence="+session.narrative.SequencePlaying:"initializing";
            string sight=flow&&flow.playerCamera?", board="+flow.Observes(flow.boardAnchor,flow.boardReadDistance)+", corridor="+flow.Observes(flow.corridorLookAnchor,flow.corridorLookDistance)+", ghost="+flow.Observes(flow.ghostAnchor,flow.ghostApproachDistance+2)+", eye="+flow.playerCamera.transform.position+", forward="+flow.playerCamera.transform.forward:"";
            Assert.That(condition(), Is.True, "Real school interaction did not finish before its deadline: "+state+sight);
        }

        int WorldMask => ~LayerMask.GetMask("Player");
        bool ClearRay(Vector3 eye, Vector3 target, SchoolOpeningClue clue = null)
        {
            Vector3 direction = target - eye;
            if (!Physics.Raycast(eye, direction.normalized, out var hit, direction.magnitude, WorldMask, QueryTriggerInteraction.Ignore)) return clue == null;
            if (clue) return hit.collider.GetComponentInParent<SchoolOpeningClue>() == clue;
            return hit.distance >= direction.magnitude - .15f;
        }

        Vector3 SafeFeet(Vector3 target, float[] radii, SchoolOpeningClue clue = null, Vector3? secondTarget = null)
        {
            var candidates = new List<Vector3>();
            float radius = game.player.Controller.radius;
            float height = game.player.standingHeight;
            foreach (float distance in radii)
                for (int i = 0; i < 32; i++)
                {
                    float angle = i * Mathf.PI / 16;
                    Vector3 candidate = target + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
                    if (!Physics.Raycast(candidate + Vector3.up * .75f, Vector3.down, out var ground, 4, WorldMask, QueryTriggerInteraction.Ignore)
                        || ground.normal.y < .75f) continue;
                    Vector3 feet = ground.point + Vector3.up * .045f;
                    if (Physics.CheckCapsule(feet + Vector3.up * (radius + .02f), feet + Vector3.up * (height - radius - .02f),
                        radius, WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    Vector3 eye = feet + Vector3.up * game.player.EyeHeight;
                    if (clue && Vector3.Distance(eye, target) > 2.5f) continue;
                    if (!ClearRay(eye, target, clue)) continue;
                    if (secondTarget.HasValue && !ClearRay(eye, secondTarget.Value)) continue;
                    candidates.Add(feet);
                }
            Assert.That(candidates.Count, Is.GreaterThan(0), "No supported capsule position can see " + (clue ? clue.title : target.ToString()) + ".");
            // A table is never selected as a floor when the real room floor is available.
            float floor = candidates.Min(candidate => candidate.y);
            return candidates.Where(candidate => candidate.y < floor + .12f).OrderBy(candidate => Vector3.Distance(candidate, target)).First();
        }

        void WarpAim(Vector3 feet, Vector3 target)
        {
            Vector3 delta = target - (feet + Vector3.up * game.player.EyeHeight);
            float heading = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(delta.normalized.y) * Mathf.Rad2Deg;
            game.player.Warp(feet, heading, pitch);
            Physics.SyncTransforms();
        }

        IEnumerator ReadActualClue(SchoolOpeningClue clue)
        {
            Assert.That(clue.CanInteract(game.interaction), Is.True, "The narrative stage must allow this clue.");
            Vector3 feet = SafeFeet(clue.transform.position, new[] { 1.2f, 1.5f, 1.8f }, clue);
            WarpAim(feet, clue.transform.position);
            yield return null;
            yield return null;
            Assert.That(game.interaction.Target, Is.SameAs(clue), "The actual player raycast must reach " + clue.title + ".");
            game.interaction.Target.Interact(game.interaction);
            yield return null;
        }

        [UnityTest] [Category("BlackoutCheckpoint")]
        public IEnumerator ChapterThreeCarryJournalAndAutomaticBlackoutKeepLatestCheckpoint()
        {
            var sanity=UnityEngine.Object.FindAnyObjectByType<SchoolSanityController>();
            Assert.That(sanity && sanity.enabledForSchool && sanity.HasChapterStart, Is.True);
            Assert.That(game.narrative.CurrentChapter.heading, Is.EqualTo("CAPÍTULO 3"));
            Assert.That(game.narrative.book.Chapter("left-behind").heading, Is.EqualTo("CAPÍTULO 4"));
            Assert.That(game.narrative.autoLoadNextChapter, Is.False);
            Assert.That(game.interaction.inventory.Contains("office_equipment"), Is.True);
            Assert.That(game.interaction.inventory.Contains("office_photo"), Is.True);
            Assert.That(game.save.flashlight.BatterySeconds, Is.EqualTo(137).Within(1));
            Assert.That(game.player.transform.position.x, Is.Not.EqualTo(99));
            Assert.That(flow.schoolLights.All(light => !light || !light.enabled), Is.True);
            Assert.That(UnityEngine.Object.FindObjectsByType<EnemyBrain>().Length, Is.Zero);
            Assert.That(sanity.anchors.Length, Is.EqualTo(6));
            Assert.That(UnityEngine.Object.FindAnyObjectByType<SchoolMemoriesFlow>().memoryAnchor.activeSelf, Is.False);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<SchoolTerraceFlow>().memoryAnchor.activeSelf, Is.False);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<SchoolLastExitFlow>().memoryAnchor.activeSelf, Is.False);
            Assert.That(sanity.heartbeat, Is.Not.Null);
            game.hud.ShowInventory(); yield return null;
            StringAssert.Contains("OBJETIVO",game.hud.inventoryText.text);
            StringAssert.Contains(game.hud.objective.text,game.hud.inventoryText.text);
            game.Resume(); yield return null;
            Vector3 center=flow.entryVolume.transform.TransformPoint(flow.entryVolume.center);
            Assert.That(Physics.Raycast(center,Vector3.down,out var floor,3,WorldMask,QueryTriggerInteraction.Ignore),Is.True);
            game.player.Warp(floor.point+Vector3.up*.045f,0); Physics.SyncTransforms();
            yield return Until(()=>game.narrative.HasSignal("school.entered"));
            yield return Until(()=>game.CanControl && !game.narrative.SequencePlaying);
            game.interaction.inventory.Add(new InventoryItem {id="school_documents",title="Pista salva",note=true,
                text="Anotação preservada.",inspectionId="school_documents",inspectionSeen=true,inspectionCount=2});
            Assert.That(game.save.Save(),Is.True);
            Assert.That(SaveSystem.TryRead(game.save.SavePath,out var checkpoint),Is.True);
            var session=game;
            sanity.sanity=0; sanity.AdvanceSanity(.1f,false);
            Assert.That(sanity.BlackingOut,Is.True);
            Assert.That(game.State,Is.EqualTo(GameState.BlackingOut));
            Assert.That(game.IsBlackingOut,Is.True);
            Assert.That(game.save.Save(),Is.False,"Zero sanity cannot overwrite the latest checkpoint.");
            sanity.AdvanceSanity(sanity.blackoutSeconds+.1f,false);
            StringAssert.Contains("Você nunca deveria ter voltado",game.hud.subtitle.text);
            yield return Until(()=>game.State==GameState.Playing && !sanity.BlackingOut,8);
            Assert.That(GameSession.Instance,Is.SameAs(session),"Blackout restores the current scene in place.");
            Assert.That(game.hud.gameOverPanel.activeSelf,Is.False);
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("SchoolCEEP"));
            Assert.That(game.State,Is.EqualTo(GameState.Playing));
            Assert.That(JsonUtility.ToJson(game.narrative.CaptureProgress()),Is.EqualTo(JsonUtility.ToJson(checkpoint.narrative)));
            Assert.That(Vector3.Distance(game.player.transform.position,checkpoint.position),Is.LessThan(.10f));
            Assert.That(sanity.sanity,Is.EqualTo(60).Within(.2f));
            Assert.That(sanity.HasChapterStart,Is.True);
            Assert.That(game.interaction.inventory.Contains("office_equipment"),Is.True);
            Assert.That(game.interaction.inventory.Contains("office_photo"),Is.True);
            var note=game.interaction.inventory.items.Single(item=>item.id=="school_documents");
            Assert.That(note.text,Is.EqualTo("Anotação preservada."));
            Assert.That(note.inspectionSeen,Is.True);
            Assert.That(note.inspectionCount,Is.EqualTo(2));
            Assert.That(game.narrative.HasSignal("road.school-gate"),Is.True);
            Assert.That(game.narrative.HasSequence("road.arrival-call"),Is.True);
            Assert.That(game.save.flashlight.BatterySeconds,Is.EqualTo(checkpoint.battery).Within(1));
            Assert.That(flow.entryDoor.IsOpen || flow.entryDoor.pairedLeaf.IsOpen,Is.False);
            Assert.That(flow.schoolLights.All(light=>!light||!light.enabled),Is.True);
            Assert.That(game.save.fileName,Is.EqualTo(testSaveName));
        }

        [UnityTest]
        public IEnumerator CompletedRoadPresentsItsCardsThenLoadsTheRealChapterThree()
        {
            PlayerPrefs.SetInt("Game.ResumeRequested",0);PlayerPrefs.SetInt("Game.StoryCarryRequested",0);
            var incoming=game.interaction.inventory.items.Select(item=>new InventoryItem
                {id=item.id,title=item.title,text=item.text,note=item.note}).ToList();
            yield return SceneManager.LoadSceneAsync("RoadJourney");
            yield return Until(()=>GameSession.Instance && GameSession.Instance.narrative
                && GameSession.Instance.narrative.IsInitialized,12);
            game=GameSession.Instance;
            Assert.That(game.narrative.autoLoadNextChapter,Is.True);
            Assert.That(game.save.fileName,Is.EqualTo(testSaveName));
            var progress=new NarrativeProgress {chapterId="road"};
            foreach(var chapter in game.narrative.book.chapters.Take(2))
                foreach(var milestone in chapter.milestones)progress.milestones.Add(milestone.id);
            progress.completedChapters.Add("prologue");
            progress.sequences.Add("road.arrival-call");
            game.interaction.inventory.Restore(incoming);game.save.flashlight.Restore(137,true);
            var roadSession=game;
            game.narrative.RestoreProgress(progress);game.narrative.InitializeAfterSession(true);
            yield return Until(()=>GameSession.Instance && GameSession.Instance!=roadSession
                && GameSession.Instance.gameObject.scene.name=="SchoolCEEP"
                && GameSession.Instance.narrative.IsInitialized,18);
            game=GameSession.Instance;flow=UnityEngine.Object.FindAnyObjectByType<SchoolOpeningFlow>();
            Assert.That(game.narrative.NextSignal,Is.EqualTo("school.entered"));
            Assert.That(game.interaction.inventory.Contains("office_equipment"),Is.True);
            Assert.That(game.interaction.inventory.Contains("office_photo"),Is.True);
            Assert.That(game.save.flashlight.BatterySeconds,Is.EqualTo(137).Within(1));
            Assert.That(UnityEngine.Object.FindAnyObjectByType<SchoolSanityController>().HasChapterStart,Is.True);
            Assert.That(game.narrative.autoLoadNextChapter,Is.False);
        }

        [UnityTest]
        public IEnumerator UnpoweredEventReturnsToDarknessOnPauseWithoutRepeatedNarrativeShock()
        {
            var mood=UnityEngine.Object.FindAnyObjectByType<SchoolHorrorAtmosphere>();
            var sanity=UnityEngine.Object.FindAnyObjectByType<SchoolSanityController>();
            mood.ReactToEvent(.5f);
            Assert.That(mood.LightFaultActive,Is.True);
            game.Pause(); mood.AdvanceAtmosphere(.1f,true);
            Assert.That(mood.LightFaultActive,Is.False);
            Assert.That(flow.schoolLights.All(light=>!light||!light.enabled),Is.True);
            float initial=sanity.sanity;
            sanity.AdvanceSanity(600,true);
            Assert.That(sanity.sanity,Is.EqualTo(initial));
            game.Resume(); yield return null;
        }
    }
}
