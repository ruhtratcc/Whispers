// Verifica SetUp, TearDown, InspectionRestoresControlsCollectsOnceRevisitsAndAbortsOnBlackout, InspectionRespondsToKeyboardMouseAndExitWithoutPausing, SchoolFallVolumeCallbackIgnoresPauseAndRecoversWithoutASaveSystem, SchoolFallReturnsToLatestCheckpointWithoutRestartingChapter, SchoolFallWithUnavailableCheckpointKeepsCurrentProgressAtTheAuthoredSpawn, SchoolFallRejectsAStoredFallingPoseWithoutGameOverOrSaveLoss, RealSchoolOpeningSupportsGateCluesSightlinesAndBlackout.

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
    [Category("SchoolOpeningScene"), Explicit("Historical seven-chapter scene. Replaced by SchoolSimplifiedSceneTests for the installed six-chapter campaign.")]
    public sealed class SchoolOpeningSceneTests
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
            PlayerPrefs.SetInt("Game.StoryCarryRequested", 0);
            testSaveName = "school-opening-fixture-" + Guid.NewGuid().ToString("N") + ".json";
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
            if (loaded.name != "SchoolCEEP") return;
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
            var inspection = InspectionManager.Instance;
            if (inspection && inspection.IsOpen)
            {
                yield return WaitForInspection(() => inspection.Alpha >= .99f, "The clue must finish its entrance animation.");
                inspection.Close();
                yield return WaitForInspection(() => !inspection.IsCompleting, "The original clue must commit after inspection closes.");
            }
            yield return null;
        }

        IEnumerator WaitForInspection(Func<bool> condition, string message, float seconds = 3)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }

        [UnityTest, Category("ItemInspection"), Category("BlackoutCheckpoint"), Category("BlackoutInspection")]
        public IEnumerator InspectionRestoresControlsCollectsOnceRevisitsAndAbortsOnBlackout()
        {
            var manager = InspectionManager.Instance;
            Assert.That(manager, Is.Not.Null, "The school must install its inspection manager.");
            yield return WaitForInspection(() => manager.CameraController && game.CanControl,
                "The manager and player must be ready before opening an item.");
            var originalCatalog = manager.catalog;
            Assert.That(originalCatalog && originalCatalog.Find("school_photograph"), Is.True,
                "The shipped school photograph must provide the visual fixture.");
            var data = UnityEngine.Object.Instantiate(originalCatalog.Find("school_photograph"));
            data.hideFlags = HideFlags.DontSave;
            data.itemId = "fixture_inspection_photo";
            data.stressPerSecond = 0;
            data.pickupSound = data.whisper = data.recording = null;
            var catalog = UnityEngine.Object.Instantiate(originalCatalog);
            catalog.hideFlags = HideFlags.DontSave;
            catalog.items = originalCatalog.items.Concat(new[] { data }).ToArray();
            manager.catalog = catalog;
            var source = new GameObject("Inspection fixture source");
            source.transform.SetParent(game.transform, false);
            var visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
            visual.name = "Fixture paper visual";
            visual.transform.SetParent(source.transform, false);
            UnityEngine.Object.Destroy(visual.GetComponent<Collider>());
            var item = source.AddComponent<InspectableItem>();
            item.item = data;
            item.visual = visual;
            int completed = 0;
            item.onInspectionFinished.AddListener(() => completed++);
            string progress = JsonUtility.ToJson(game.narrative.CaptureProgress());
            var flashlight = game.save.flashlight;
            bool flashlightWasEnabled = flashlight.enabled;
            bool lampWasEnabled = flashlight.lamp && flashlight.lamp.enabled;
            bool handWasVisible = flashlight.handRig && flashlight.handRig.gameObject.activeSelf;
            Vector3 feet = game.player.transform.position;
            Vector3 eye = game.player.view.playerCamera.transform.position;
            try
            {
                var sanity = UnityEngine.Object.FindAnyObjectByType<SchoolSanityController>();
                Assert.That(sanity && sanity.IsSchoolActive, Is.True, "The real school's sanity system must own inspection stress.");
                float beforeExplorationStress = sanity.sanity;
                sanity.ApplyInspectionStress(1);
                Assert.That(sanity.sanity, Is.EqualTo(beforeExplorationStress), "Inspection stress must not apply in normal exploration.");
                Assert.That(manager.Begin(item, game.interaction), Is.True, "An uncollected generic item must open.");
                Assert.That(game.State, Is.EqualTo(GameState.Inspecting));
                Assert.That(Time.timeScale, Is.EqualTo(1), "Inspection must not freeze the world.");
                Assert.That(game.CanControl, Is.False);
                Assert.That(game.input.Actions.FindActionMap("Gameplay").enabled, Is.False);
                Assert.That(flashlight.enabled, Is.False, "Inspection suspends the flashlight component and battery drain.");
                Assert.That(flashlight.lamp.enabled, Is.False);
                Assert.That(!flashlight.handRig || !flashlight.handRig.gameObject.activeSelf, Is.True);
                Assert.That(manager.CameraController.Camera.cullingMask, Is.EqualTo(1 << LayerMask.NameToLayer("Inspect")));
                Assert.That(game.player.view.playerCamera.cullingMask & (1 << LayerMask.NameToLayer("Inspect")), Is.Zero);
                Assert.That(manager.Texture, Is.Not.Null);
                Assert.That(manager.Texture.width, Is.LessThanOrEqualTo(2048));
                Assert.That(manager.BackVisible, Is.False, "A photograph must open on its readable front.");
                yield return WaitForInspection(() => manager.Alpha >= .99f, "Inspection must ease into place.");
                Assert.That(Vector3.Distance(game.player.transform.position, feet), Is.LessThan(.01f), "The player body must stay put.");
                Assert.That(Vector3.Distance(game.player.view.playerCamera.transform.position, eye), Is.LessThan(.02f), "The player camera must remain still.");
                manager.Flip();
                yield return WaitForInspection(() => manager.BackVisible, "Flip must expose the photograph's distinct back.");
                manager.CameraController.Zoom(10000);
                manager.CameraController.Advance(1, false);
                Assert.That(manager.CameraController.Zoom01, Is.EqualTo(1).Within(.005f), "Zoom in must clamp.");
                Assert.That(-manager.CameraController.Camera.transform.localPosition.z, Is.EqualTo(.48f).Within(.005f));
                manager.CameraController.Zoom(-10000);
                manager.CameraController.Advance(1, false);
                Assert.That(manager.CameraController.Zoom01, Is.EqualTo(0).Within(.005f), "Zoom out must clamp.");
                Assert.That(-manager.CameraController.Camera.transform.localPosition.z, Is.EqualTo(1.35f).Within(.005f));

                game.Pause();
                Assert.That(game.State, Is.EqualTo(GameState.Paused));
                Assert.That(manager.CameraController.Camera.enabled, Is.False, "Pause must hide the inspection camera.");
                float beforePausedStress = sanity.sanity;
                sanity.ApplyInspectionStress(1);
                Assert.That(sanity.sanity, Is.EqualTo(beforePausedStress), "Inspection stress must stop while paused.");
                game.Resume();
                Assert.That(game.State, Is.EqualTo(GameState.Inspecting), "Resume must return to the same item.");
                Assert.That(manager.CameraController.Camera.enabled, Is.True);
                game.Pause();
                manager.Cancel(true);
                Assert.That(game.State, Is.EqualTo(GameState.Paused), "Cancelling a paused item must not unpause the menu.");
                game.Resume();
                yield return WaitForInspection(() => game.CanControl, "Cancelled inspection must restore exploration.");
                Assert.That(game.interaction.inventory.Contains(data.itemId), Is.False, "Cancellation must not collect the item.");
                Assert.That(completed, Is.Zero, "Cancellation must not fire completion hooks.");
                Assert.That(source.activeSelf && visual.GetComponent<Renderer>().enabled, Is.True);
                Assert.That(flashlight.enabled, Is.EqualTo(flashlightWasEnabled));
                Assert.That(flashlight.lamp.enabled, Is.EqualTo(lampWasEnabled));
                Assert.That(!flashlight.handRig || flashlight.handRig.gameObject.activeSelf == handWasVisible, Is.True);
                Assert.That(manager.Texture, Is.Null, "The temporary render texture must be released.");
                Assert.That(JsonUtility.ToJson(game.narrative.CaptureProgress()), Is.EqualTo(progress));

                Assert.That(manager.Begin(item, game.interaction), Is.True);
                yield return WaitForInspection(() => manager.Alpha >= .99f, "The item must reopen after cancellation.");
                manager.Close();
                yield return WaitForInspection(() => !manager.IsCompleting, "Collection must finish once after the exit animation.");
                var saved = game.interaction.inventory.items.Single(entry => entry.id == data.itemId);
                Assert.That(saved.inspectionId, Is.EqualTo(data.itemId));
                Assert.That(saved.inspectionCount, Is.EqualTo(1));
                Assert.That(saved.inspectionSeen, Is.False, "New collected items keep their discreet notebook marker.");
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(source.activeSelf, Is.False, "Collected source must disappear from the world.");
                Assert.That(JsonUtility.ToJson(game.narrative.CaptureProgress()), Is.EqualTo(progress), "Generic collection must not advance school milestones.");

                var roundTrip = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData
                {
                    scene = "SchoolCEEP", inventory = new List<InventoryItem> { saved }
                }));
                Assert.That(roundTrip.inventory[0].inspectionId, Is.EqualTo(data.itemId));
                Assert.That(roundTrip.inventory[0].inspectionCount, Is.EqualTo(1));
                var legacy = JsonUtility.FromJson<SaveData>("{\"version\":2,\"scene\":\"SchoolCEEP\",\"inventory\":[{\"id\":\"school_photograph\",\"title\":\"Fotografia\",\"note\":true}]}");
                Assert.That(legacy.inventory[0].id, Is.EqualTo("school_photograph"));
                Assert.That(legacy.inventory[0].inspectionSeen, Is.False);
                Assert.That(legacy.inventory[0].inspectionCount, Is.Zero);

                yield return WaitForInspection(() => game.CanControl, "The notebook must open after the collection frame.");
                game.hud.ShowInventory();
                Assert.That(game.State, Is.EqualTo(GameState.Paused));
                Assert.That(manager.Reopen(data.itemId), Is.True, "A saved item must be inspectable from Anotações.");
                yield return WaitForInspection(() => manager.IsOpen && manager.Alpha >= .99f, "The saved item must reappear in 3D.");
                manager.Close();
                yield return WaitForInspection(() => !manager.IsCompleting && game.State == GameState.Paused,
                    "Closing a notebook revisit must return to the paused notebook.");
                Assert.That(game.hud.inventoryPanel.activeSelf, Is.True);
                Assert.That(saved.inspectionCount, Is.EqualTo(2));
                Assert.That(saved.inspectionSeen, Is.True);
                Assert.That(completed, Is.EqualTo(1), "Revisiting must not refire the world completion hook.");
                Assert.That(game.interaction.inventory.items.Count(entry => entry.id == data.itemId), Is.EqualTo(1));
                Assert.That(JsonUtility.ToJson(game.narrative.CaptureProgress()), Is.EqualTo(progress));

                game.Resume();
                yield return WaitForInspection(() => game.CanControl, "Resume after notes must restore normal input.");
                Assert.That(game.save.Save(), Is.True);
                Assert.That(SaveSystem.TryRead(game.save.SavePath, out var latest), Is.True);
                Assert.That(manager.Reopen(data.itemId), Is.True);
                float before = sanity.sanity;
                sanity.ApplyInspectionStress(float.NaN);
                sanity.ApplyInspectionStress(float.PositiveInfinity);
                sanity.ApplyInspectionStress(-1);
                sanity.ApplyInspectionStress(0);
                Assert.That(sanity.sanity, Is.EqualTo(before), "Invalid stress must be rejected.");
                sanity.sanity = .1f;
                sanity.ApplyInspectionStress(1);
                Assert.That(sanity.BlackingOut, Is.True);
                Assert.That(manager.IsOpen || manager.IsCompleting, Is.False, "Zero sanity must abort inspection immediately.");
                Assert.That(game.State, Is.EqualTo(GameState.BlackingOut));
                Assert.That(game.CanControl, Is.False);
                Assert.That(manager.Texture, Is.Null);
                Assert.That(saved.inspectionCount, Is.EqualTo(2), "Blackout must not count an unfinished inspection.");
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(game.save.Save(), Is.False, "The aborted inspection cannot replace its healthy checkpoint.");
                sanity.AdvanceSanity(sanity.blackoutSeconds + .1f, false);
                yield return WaitForInspection(() => game.CanControl && !sanity.BlackingOut,
                    "Blackout must automatically wake at the latest checkpoint.", 8);
                Assert.That(game.hud.gameOverPanel.activeSelf, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1));
                Assert.That(game.CanControl, Is.True);
                Assert.That(game.input.Actions.FindActionMap("Gameplay").enabled, Is.True);
                Assert.That(flashlight.enabled, Is.EqualTo(flashlightWasEnabled));
                Assert.That(manager.IsOpen || manager.IsCompleting, Is.False);
                Assert.That(JsonUtility.ToJson(game.narrative.CaptureProgress()), Is.EqualTo(progress));
                var restored = game.interaction.inventory.items.Single(entry => entry.id == data.itemId);
                Assert.That(restored.inspectionCount, Is.EqualTo(2));
                Assert.That(restored.inspectionSeen, Is.True);
                Assert.That(restored.inspectionId, Is.EqualTo(data.itemId));
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(Vector3.Distance(game.player.transform.position, latest.position), Is.LessThan(.10f));
                Assert.That(sanity.sanity, Is.EqualTo(60).Within(.2f));
            }
            finally
            {
                if (manager) { manager.Cancel(true); manager.catalog = originalCatalog; }
                if (source) UnityEngine.Object.Destroy(source);
                UnityEngine.Object.Destroy(catalog);
                UnityEngine.Object.Destroy(data);
            }
        }

        [UnityTest, Category("ItemInspectionInput")]
        public IEnumerator InspectionRespondsToKeyboardMouseAndExitWithoutPausing()
        {
            var manager = InspectionManager.Instance;
            Assert.That(manager, Is.Not.Null, "The school must install inspection before testing its real input actions.");
            yield return WaitForInspection(() => manager.CameraController && game.CanControl,
                "Inspection and player must be ready for input.");
            var originalCatalog = manager.catalog;
            Assert.That(originalCatalog && originalCatalog.Find("school_photograph"), Is.True);
            var data = UnityEngine.Object.Instantiate(originalCatalog.Find("school_photograph"));
            data.hideFlags = HideFlags.DontSave;
            data.itemId = "fixture_inspection_input";
            data.stressPerSecond = 0;
            data.pickupSound = data.whisper = data.recording = null;
            var catalog = UnityEngine.Object.Instantiate(originalCatalog);
            catalog.hideFlags = HideFlags.DontSave;
            catalog.items = originalCatalog.items.Concat(new[] { data }).ToArray();
            manager.catalog = catalog;
            var source = new GameObject("Inspection input fixture");
            source.transform.SetParent(game.transform, false);
            var item = source.AddComponent<InspectableItem>();
            item.item = data;
            int completed = 0;
            item.onInspectionFinished.AddListener(() => completed++);
            var mouse = InputSystem.AddDevice<Mouse>();
            int cameraCount = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude).Length;
            string progress = JsonUtility.ToJson(game.narrative.CaptureProgress());
            Vector2 previewPoint = new Vector2(Screen.width * .5f, Screen.height * .58f);
            try
            {
                Assert.That(Screen.width > 0 && Screen.height > 0, Is.True, "The preview requires a real Game view size.");
                Assert.That(manager.Begin(item, game.interaction), Is.True);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = previewPoint });
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null;
                Assert.That(manager.IsOpen && !manager.IsCompleting, Is.True,
                    "The interaction that opened the item must not close it on the entrance frame.");
                Assert.That(game.State, Is.EqualTo(GameState.Inspecting));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return WaitForInspection(() => manager.Alpha >= .99f, "The photo must finish opening before manipulation.");

                // Queue edge events for the normal player-loop input update; do not invoke the manager directly.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return WaitForInspection(() => manager.BackVisible, "The F input action must turn the photograph over.");
                Assert.That(game.State, Is.EqualTo(GameState.Inspecting));
                float zoomBefore = manager.CameraController.Zoom01;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = previewPoint, scroll = new Vector2(0, 120) });
                yield return null;
                yield return WaitForInspection(() => manager.CameraController.Zoom01 > zoomBefore + .05f,
                    "Mouse wheel input must move the item closer.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = previewPoint });
                yield return null;

                var clone = manager.CameraController.Camera.transform.parent.Find(data.title + " (inspection only)");
                Assert.That(clone, Is.Not.Null, "The visual clone must belong to the isolated stage.");
                var ui = manager.GetComponentInChildren<InspectionUI>(true);
                Assert.That(ui && ui.CanDragAt(previewPoint), Is.True, "The centre of the item preview must accept drag input.");
                Quaternion rotationBefore = clone.localRotation;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = previewPoint }.WithButton(MouseButton.Left, true));
                yield return null;
                InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(72, 32));
                yield return null;
                yield return WaitForInspection(() => Quaternion.Angle(rotationBefore, clone.localRotation) > 2,
                    "Held left button plus mouse delta must rotate the 3D item.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = previewPoint });
                yield return null;
                var flipButton = ui.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button => button.name == "Flip");
                Vector2 buttonPoint = RectTransformUtility.WorldToScreenPoint(null, flipButton.transform.position);
                Assert.That(ui.CanDragAt(buttonPoint), Is.False, "A control button's area must not rotate the item.");

                InputSystem.QueueStateEvent(mouse, new MouseState { position = previewPoint, scroll = new Vector2(0, 2400) });
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = previewPoint });
                yield return WaitForInspection(() => manager.FullscreenZoom && manager.PresentationZoom01 > .99f,
                    "Wheel zoom must expand the item to the whole viewport.");
                yield return new WaitForSeconds(.25f);
                var chrome = ui.transform.Find("InspectionChrome").GetComponent<CanvasGroup>();
                Assert.That(chrome.alpha, Is.LessThan(.01f), "Fullscreen zoom must fade title, footer and buttons.");
                Assert.That(chrome.interactable || chrome.blocksRaycasts, Is.False, "Invisible controls must not block paper panning.");
                Assert.That(ui.CanDragAt(buttonPoint), Is.True, "The hidden button area must become part of the paper viewport.");
                var preview = ui.GetComponentInChildren<UnityEngine.UI.RawImage>(true);
                var canvasRect = (RectTransform)ui.transform;
                Assert.That(preview.rectTransform.rect.width, Is.EqualTo(canvasRect.rect.width).Within(3));
                Assert.That(preview.rectTransform.rect.height, Is.EqualTo(canvasRect.rect.height).Within(3));
                Assert.That((float)manager.Texture.width / manager.Texture.height,
                    Is.EqualTo(manager.CameraController.Camera.aspect).Within(.003f));
                Assert.That(Mathf.Max(manager.Texture.width, manager.Texture.height), Is.LessThanOrEqualTo(2048));

                Quaternion panningRotation = clone.localRotation;
                Vector2 panBefore = manager.CameraController.PanOffset;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = previewPoint }.WithButton(MouseButton.Left, true));
                yield return null;
                InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(30, 110));
                yield return null;
                yield return WaitForInspection(() => Vector2.Distance(panBefore, manager.CameraController.PanOffset) > .005f,
                    "Dragging a fullscreen paper must move the viewing position.");
                Assert.That(Quaternion.Angle(panningRotation, clone.localRotation), Is.LessThan(.2f),
                    "Fullscreen panning must preserve the manually chosen paper angle.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = previewPoint });
                yield return null;

                var cachedTexture = manager.Texture;
                foreach (float nearBoundary in new[] { .67f, .69f, .67f, .49f })
                {
                    float targetDistance = Mathf.Lerp(1.35f, .48f, nearBoundary);
                    manager.CameraController.Zoom((-manager.CameraController.Camera.transform.localPosition.z - targetDistance) / .12f);
                    manager.CameraController.Advance(1, false);
                    Assert.That(manager.FullscreenZoom, Is.True, "Hysteresis must prevent fullscreen mode chattering at the entry boundary.");
                    Assert.That(manager.Texture, Is.SameAs(cachedTexture), "Boundary jitter must not repeatedly allocate render textures.");
                }
                manager.CameraController.Zoom(-10000);
                manager.CameraController.Advance(1, false);
                yield return null;
                Assert.That(manager.FullscreenZoom, Is.False);
                Assert.That(chrome.alpha, Is.GreaterThan(.99f), "Zoom out must restore the normal interface.");
                Assert.That(chrome.interactable && chrome.blocksRaycasts, Is.True);
                Assert.That(manager.CameraController.PanOffset.magnitude, Is.LessThan(.001f));
                manager.CameraController.Zoom(10000);
                manager.CameraController.Advance(1, false);
                yield return null;
                game.Pause();
                Assert.That(manager.CameraController.Camera.enabled, Is.False);
                game.Resume();
                yield return null;
                Assert.That(manager.FullscreenZoom && manager.CameraController.Camera.enabled, Is.True,
                    "Pause and resume must preserve the fullscreen inspection.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                Assert.That(manager.IsOpen && !manager.IsCompleting, Is.True,
                    "The first Escape must return zoom instead of collecting the item.");
                Assert.That(game.State, Is.EqualTo(GameState.Inspecting));
                Assert.That(completed, Is.Zero);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                yield return WaitForInspection(() => !manager.IsOpen && !manager.IsCompleting,
                    "A second Escape must exit, including during the smooth zoom return.");
                Assert.That(game.State, Is.EqualTo(GameState.Playing), "Escape must not also open the pause menu.");
                Assert.That(game.CanControl, Is.True);
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(manager.Texture, Is.Null);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude).Length, Is.EqualTo(cameraCount),
                    "The exit must destroy its extra render camera.");
                Assert.That(game.interaction.inventory.items.Single(entry => entry.id == data.itemId).inspectionCount, Is.EqualTo(1));

                game.hud.ShowInventory();
                Assert.That(manager.Reopen(data.itemId), Is.True);
                yield return WaitForInspection(() => manager.IsOpen && manager.Alpha >= .99f,
                    "The saved photograph must reopen from Anotações.");
                Assert.That(manager.FullscreenZoom, Is.False, "Notebook revisits must start at normal zoom.");
                Assert.That(manager.CameraController.PanOffset, Is.EqualTo(Vector2.zero));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null;
                yield return WaitForInspection(() => !manager.IsOpen && !manager.IsCompleting && game.State == GameState.Paused,
                    "E must finish a saved-item revisit and return to Anotações.");
                Assert.That(game.hud.inventoryPanel.activeSelf, Is.True);
                Assert.That(completed, Is.EqualTo(1), "E on a revisit must not repeat the world callback.");
                Assert.That(game.interaction.inventory.items.Single(entry => entry.id == data.itemId).inspectionCount, Is.EqualTo(2));
                Assert.That(manager.Texture, Is.Null);
                Assert.That(JsonUtility.ToJson(game.narrative.CaptureProgress()), Is.EqualTo(progress));
            }
            finally
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                if (manager) { manager.Cancel(true); manager.catalog = originalCatalog; }
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                if (source) UnityEngine.Object.Destroy(source);
                UnityEngine.Object.Destroy(catalog);
                UnityEngine.Object.Destroy(data);
            }
        }

        [UnityTest, Category("MapFallRecovery")]
        public IEnumerator SchoolFallVolumeCallbackIgnoresPauseAndRecoversWithoutASaveSystem()
        {
            var callback = new GameObject("Fall recovery callback fixture");
            var zone = callback.AddComponent<BoxCollider>(); zone.isTrigger = true;
            var events = callback.AddComponent<EventZone>(); events.oneShot = false;
            events.onEnter.AddListener(game.RecoverFromFall);
            var originalSave = game.save;
            Vector3 spawn = game.player.transform.position;
            try
            {
                game.save = null;
                game.Pause();
                game.player.Warp(spawn + Vector3.down, 10);
                Vector3 pausedPosition = game.player.transform.position;
                events.onEnter.Invoke();
                Assert.That(game.State, Is.EqualTo(GameState.Paused));
                Assert.That(game.player.transform.position, Is.EqualTo(pausedPosition));
                game.Resume();
                for (int i = 0; i < 2; i++)
                {
                    game.player.Warp(spawn + Vector3.down, 10);
                    events.onEnter.Invoke();
                    Assert.That(game.State, Is.EqualTo(GameState.Playing));
                    Assert.That(game.IsBlackingOut, Is.False);
                    Assert.That(Vector3.Distance(game.player.transform.position, spawn), Is.LessThan(.15f));
                }
            }
            finally
            {
                game.save = originalSave;
                UnityEngine.Object.Destroy(callback);
            }
            yield return null;
            Assert.That(game.CanControl, Is.True);
        }

        [UnityTest, Category("MapFallRecovery")]
        public IEnumerator SchoolFallReturnsToLatestCheckpointWithoutRestartingChapter()
        {
            var sanity = UnityEngine.Object.FindAnyObjectByType<SchoolSanityController>();
            Assert.That(sanity, Is.Not.Null);
            Assert.That(game.narrative.NextSignal, Is.EqualTo("school.entered"), "Recovery must also work before the entry event.");
            game.interaction.inventory.Add(new InventoryItem { id = "fall.saved.note", title = "Registro", text = "Anotação preservada", note = true, inspectionSeen = true, inspectionCount = 2 });
            sanity.sanity = 71;
            game.player.Warp(game.player.transform.position + Vector3.right * .25f, 12, 6);
            Assert.That(game.save.Save(), Is.True);
            Assert.That(game.save.TryReadCurrent(out var checkpoint), Is.True);
            byte[] checkpointBytes = File.ReadAllBytes(game.save.SavePath);
            game.interaction.inventory.Add(new InventoryItem { id = "fall.unsaved.note", title = "Depois do checkpoint" });
            sanity.sanity = 19;
            game.player.Warp(checkpoint.position + Vector3.down * 40, 80, -20);
            yield return Until(() => game.player.transform.position.y > -25);
            Assert.That(game.State, Is.EqualTo(GameState.Playing));
            Assert.That(game.IsBlackingOut, Is.False);
            Assert.That(Vector3.Distance(game.player.transform.position, checkpoint.position), Is.LessThan(.05f));
            Assert.That(JsonUtility.ToJson(game.narrative.CaptureProgress()), Is.EqualTo(JsonUtility.ToJson(checkpoint.narrative)));
            Assert.That(game.interaction.inventory.Contains("fall.unsaved.note"), Is.False);
            var note = game.interaction.inventory.items.Single(item => item.id == "fall.saved.note");
            Assert.That(note.text, Is.EqualTo("Anotação preservada"));
            Assert.That(note.inspectionSeen && note.inspectionCount == 2, Is.True);
            Assert.That(sanity.sanity, Is.EqualTo(71).Within(.001f));
            Assert.That(File.ReadAllBytes(game.save.SavePath), Is.EqualTo(checkpointBytes), "The falling pose must not overwrite the checkpoint.");
            yield return null;
            Assert.That(game.CanControl, Is.True);
            game.Pause(); Assert.That(game.State, Is.EqualTo(GameState.Paused));
            game.Resume(); Assert.That(game.State, Is.EqualTo(GameState.Playing));
        }

        [UnityTest, Category("MapFallRecovery")]
        public IEnumerator SchoolFallWithUnavailableCheckpointKeepsCurrentProgressAtTheAuthoredSpawn()
        {
            Vector3 spawn = game.player.transform.position;
            game.interaction.inventory.Add(new InventoryItem { id = "fall.current.note", title = "Atual", text = "Não perder", note = true });
            string progress = JsonUtility.ToJson(game.narrative.CaptureProgress());
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(game.save.SavePath + suffix)) File.Delete(game.save.SavePath + suffix);
            typeof(SaveSystem).GetField("lastCheckpoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(game.save, null);
            Assert.That(game.save.TryReadCurrent(out _), Is.False);
            game.player.Warp(spawn + Vector3.down * 40, 30);
            yield return Until(() => game.player.transform.position.y > -25);
            Assert.That(game.State, Is.EqualTo(GameState.Playing));
            Assert.That(Vector3.Distance(game.player.transform.position, spawn), Is.LessThan(.15f));
            Assert.That(game.interaction.inventory.Contains("fall.current.note"), Is.True);
            Assert.That(JsonUtility.ToJson(game.narrative.CaptureProgress()), Is.EqualTo(progress));
            Assert.That(File.Exists(game.save.SavePath), Is.False, "Unavailable recovery must not manufacture a replacement checkpoint.");
        }

        [UnityTest, Category("MapFallRecovery")]
        public IEnumerator SchoolFallRejectsAStoredFallingPoseWithoutGameOverOrSaveLoss()
        {
            Vector3 spawn = game.player.transform.position;
            Assert.That(game.save.TryReadCurrent(out var invalidPosition), Is.True);
            invalidPosition.position.y = -30;
            Assert.That(game.save.SaveSnapshot(invalidPosition), Is.True);
            byte[] checkpointBytes = File.ReadAllBytes(game.save.SavePath);
            game.interaction.inventory.Add(new InventoryItem { id = "fall.current.note", title = "Atual", note = true });
            game.player.Warp(spawn + Vector3.down * 40, 30);
            yield return Until(() => game.player.transform.position.y > -25);
            Assert.That(game.State, Is.EqualTo(GameState.Playing));
            Assert.That(Vector3.Distance(game.player.transform.position, spawn), Is.LessThan(.15f));
            Assert.That(game.interaction.inventory.Contains("fall.current.note"), Is.True);
            Assert.That(File.ReadAllBytes(game.save.SavePath), Is.EqualTo(checkpointBytes));
        }

        [UnityTest]
        public IEnumerator RealSchoolOpeningSupportsGateCluesSightlinesAndBlackout()
        {
            Assert.That(flow.entryDoor.IsOpen && flow.entryDoor.pairedLeaf.IsOpen, Is.True);
            Assert.That(flow.drawingClue.CanInteract(game.interaction), Is.False);
            Vector3 triggerCenter = flow.entryVolume.transform.TransformPoint(flow.entryVolume.center);
            Assert.That(Physics.Raycast(triggerCenter, Vector3.down, out var entryFloor, 3, WorldMask, QueryTriggerInteraction.Ignore), Is.True);
            game.player.Warp(entryFloor.point + Vector3.up * .045f, 0);
            Physics.SyncTransforms();
            yield return Until(() => game.narrative.HasSignal("school.entered"));
            yield return Until(() => Quaternion.Angle(flow.entryDoor.hinge.localRotation, Quaternion.identity) < .5f);
            Assert.That(flow.entryDoor.locked && flow.entryDoor.pairedLeaf.locked, Is.True);
            flow.entryDoor.pairedLeaf.Interact(game.interaction);
            Assert.That(game.narrative.HasSignal("school.entry-locked"), Is.True);
            flow.entryDoor.Interact(game.interaction);
            Assert.That(flow.entryDoor.IsOpen || flow.entryDoor.pairedLeaf.IsOpen, Is.False);

            // Open real classroom/office doors temporarily, as the player may do. Entry remains locked.
            // This fixture proves authored objects and sightlines; it does not claim a continuous walk-through.
            foreach (var door in UnityEngine.Object.FindObjectsByType<InteractiveDoor>())
                if (door != flow.entryDoor && door != flow.entryDoor.pairedLeaf)
                { door.SetLocked(false); door.SetOpen(true, true); }
            Physics.SyncTransforms();
            yield return ReadActualClue(flow.documentsClue);
            Assert.That(game.State, Is.EqualTo(GameState.Playing), "3D inspection must not reopen the obsolete 2D note modal.");
            Assert.That(game.interaction.inventory.items.Single(item => item.id == "school_documents").text,
                Is.EqualTo(flow.documentsClue.body));
            yield return null;
            yield return ReadActualClue(flow.photographClue);
            yield return Until(() => game.narrative.HasSequence("school.photo-inscription") && game.State == GameState.Playing, 8);
            Assert.That(flow.boardMessage.activeSelf, Is.True);

            Vector3 boardFeet = SafeFeet(flow.boardAnchor.position, new[] { 1.5f, 2f, 2.8f, 3.6f },
                null, flow.corridorLookAnchor.position);
            WarpAim(boardFeet, flow.boardAnchor.position);
            yield return Until(() => game.narrative.HasSequence("school.blackboard-inscription") && game.State == GameState.Playing, 8);
            WarpAim(boardFeet, flow.corridorLookAnchor.position);
            yield return Until(() => game.narrative.HasSignal("school.blackboard-vanished"));
            Assert.That(flow.boardMessage.activeSelf, Is.False);
            Assert.That(flow.ghostVisual.activeSelf, Is.True);

            Vector3 ghostFeet = SafeFeet(flow.ghostAnchor.position, new[] { .9f, 1.2f, 1.5f });
            WarpAim(ghostFeet, flow.ghostAnchor.position);
            yield return Until(() => game.narrative.HasSignal("school.ghost-approached"));
            Assert.That(flow.ghostVisual.activeSelf, Is.False);
            yield return ReadActualClue(flow.drawingClue);
            Assert.That(game.interaction.inventory.Contains("school_drawing"), Is.True);
            yield return Until(() => game.State == GameState.StoryScreen, 10);
            Assert.That(game.narrative.Progress.milestones.Count(id => id.StartsWith("school.")), Is.EqualTo(9));
            Assert.That(flow.schoolLights.All(light => !light || !light.enabled), Is.True);
            Assert.That(game.narrative.Progress.chapterId, Is.EqualTo("school"));
            Assert.That(game.narrative.Progress.pendingChapterId, Is.EqualTo("left-behind"));
            Assert.That(game.narrative.book.Chapter("left-behind").sceneName, Is.EqualTo("SchoolCEEP"));
            Assert.That(game.narrative.continueSameSceneChapters, Is.True);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("SchoolCEEP"));
            Assert.That(game.save.Save(), Is.True);
            Assert.That(SaveSystem.TryRead(game.save.SavePath, out var completed), Is.True);
            Assert.That(completed.narrative.HasSignal("school.blackout"), Is.True);
            Assert.That(completed.inventory.Any(item => item.id == "school_drawing"), Is.True);
        }
    }
}
