// Verifica SetUp, LibraryKeysTapesBlackoutMirrorAndSchoolExitUseTheSavedSixChapterRoute, DarknessDeathRetriesTheLastSchoolCheckpointWithTimeToFindBatteries, IndependentReaderKeepsEvidenceLegibleAtAllZoomsAndEscClosesLayersInOrder, LegacyEarlyCheckpointFromUpperCorridorCanDescendWithoutCompletingTape, LegacyEarlyCheckpointInside113CanReturnToGroundFloorWithoutCompletingTape, FigureQuestionPrecedesDisappearanceAndNeitherReactionRepeatsAfterRestore, TearDown.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    /// <summary>The saved school, actual inspection UI and checkpoint recovery; all writes use a disposable save.</summary>
    [Category("SimplifiedStory")]
    [Category("GameplayPolish")]
    public sealed class SchoolSimplifiedSceneTests
    {
        GameSession game;
        SchoolSimplifiedFlow flow;
        InspectionManager inspector;
        StoryBook runtimeBook;
        string disposable;
        string step;
        readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
        readonly Dictionary<string, (bool had, string value)> prefs = new Dictionary<string, (bool, string)>();
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        bool runInBackground;
        string RealSave => Path.Combine(Application.persistentDataPath, "save.json");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                files[RealSave + suffix] = File.Exists(RealSave + suffix) ? File.ReadAllBytes(RealSave + suffix) : null;
            foreach (string key in new[] { "Game.SaveScene", "Game.ResumeRequested", "Game.StoryCarryRequested" })
                prefs[key] = (PlayerPrefs.HasKey(key), key == "Game.SaveScene" ? PlayerPrefs.GetString(key) : PlayerPrefs.GetInt(key).ToString());
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            runInBackground = Application.runInBackground;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground = true;
            disposable = "simplified-scene-" + Guid.NewGuid().ToString("N") + ".json";
            PlayerPrefs.SetInt("Game.ResumeRequested", 0);
            PlayerPrefs.SetInt("Game.StoryCarryRequested", 0);
            SceneManager.sceneLoaded += RedirectSave;
            Time.timeScale = 1; AudioListener.pause = false;
            yield return SceneManager.LoadSceneAsync("SchoolCEEP");
            yield return Until(() => GameSession.Instance && GameSession.Instance.narrative && GameSession.Instance.narrative.IsInitialized, 20);
            game = GameSession.Instance;
            flow = UnityEngine.Object.FindAnyObjectByType<SchoolSimplifiedFlow>();
            Assert.That(flow, Is.Not.Null, "Install the simplified school pass before running this fixture.");
            Assert.That(game.save.fileName, Is.EqualTo(disposable));
            Assert.That(game.narrative.book.revision, Is.EqualTo(2));
            Assert.That(game.narrative.book.chapters.Length, Is.EqualTo(6));
            runtimeBook = UnityEngine.Object.Instantiate(game.narrative.book);
            foreach (var sequence in runtimeBook.sequences)
                foreach (var line in sequence.lines) line.seconds = .08f;
            game.narrative.book = runtimeBook;
            if (game.narrative.transitionView) { game.narrative.transitionView.fadeSeconds = .06f; game.narrative.transitionView.cardSeconds = .08f; }
            game.input.Actions.devices = Array.Empty<InputDevice>();
            game.player.view.bobEnabled = game.player.view.swayEnabled = false;
            yield return Until(() => InspectionManager.Instance && InspectionManager.Instance.CameraController);
            inspector = InspectionManager.Instance;
            yield return Ready();
        }

        void RedirectSave(Scene scene, LoadSceneMode mode)
        {
            foreach (var save in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SaveSystem>(true)))
                save.fileName = disposable;
            if (scene.name == "Chapter1Office" && runtimeBook)
                foreach (var director in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NarrativeDirector>(true)))
                {
                    director.book = runtimeBook;
                    if (director.transitionView) { director.transitionView.fadeSeconds = .06f; director.transitionView.cardSeconds = .08f; }
                }
        }

        IEnumerator Until(Func<bool> condition, float seconds = 30)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            if (condition()) yield break;
            Assert.Fail("Simplified scene timed out at " + (game && game.narrative ? game.narrative.Progress.chapterId + "/" + game.narrative.NextSignal + ", step=" + step + ", sequence=" + game.narrative.ActiveSequenceId + ", queued=" + game.narrative.SequencePlaying + ", state=" + game.State + ", control=" + game.CanControl + ", blackout=" + game.IsBlackingOut + ", inspect=" + (inspector ? inspector.IsOpen + "/" + inspector.IsCompleting : "unavailable") + ", scale=" + Time.timeScale : "initialization"));
        }
        IEnumerator Ready() => Until(() => game && game.CanControl && !game.narrative.SequencePlaying && !inspector.IsOpen && !inspector.IsCompleting);

        void Aim(Vector3 feet, Vector3 target)
        {
            Vector3 delta = (target - (feet + Vector3.up * game.player.EyeHeight)).normalized;
            game.player.Warp(feet, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, -Mathf.Asin(delta.y) * Mathf.Rad2Deg);
            Physics.SyncTransforms();
        }

        void Enter(BoxCollider volume)
        {
            Assert.That(volume, Is.Not.Null);
            Vector3 center = volume.transform.TransformPoint(volume.center);
            Assert.That(Physics.Raycast(center + Vector3.up * .2f, Vector3.down, out var floor, 4,
                ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore), Is.True, volume.name + " has no supported floor.");
            game.player.Warp(floor.point + Vector3.up * .045f, 0);
            Physics.SyncTransforms();
        }

        Vector3 SupportedSightline(Vector3 target, float[] distances, SimplifiedSchoolClue clue = null)
        {
            var candidates = new List<Vector3>();
            int mask = ~(LayerMask.GetMask("Player") | (1 << 31));
            float radius = game.player.Controller.radius, height = game.player.standingHeight;
            foreach (float distance in distances)
                for (int i = 0; i < 32; i++)
                {
                    float angle = i * Mathf.PI / 16;
                    Vector3 point = target + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
                    if (!Physics.Raycast(point + Vector3.up * .75f, Vector3.down, out var ground, 4, mask, QueryTriggerInteraction.Ignore)
                        || ground.normal.y < .75f) continue;
                    Vector3 feet = ground.point + Vector3.up * .045f;
                    if (Physics.CheckCapsule(feet + Vector3.up * (radius + .02f), feet + Vector3.up * (height - radius - .02f), radius, mask, QueryTriggerInteraction.Ignore)) continue;
                    Vector3 eye = feet + Vector3.up * game.player.EyeHeight, delta = target - eye;
                    bool hit = Physics.Raycast(eye, delta.normalized, out var obstacle, delta.magnitude, mask, QueryTriggerInteraction.Ignore);
                    if (clue ? (!hit || obstacle.collider.GetComponentInParent<SimplifiedSchoolClue>() != clue)
                        : (hit && obstacle.distance < delta.magnitude - .16f)) continue;
                    candidates.Add(feet);
                }
            Assert.That(candidates, Is.Not.Empty, "No supported sightline to " + (clue ? clue.itemId : target.ToString()));
            float lowest = candidates.Min(point => point.y);
            return candidates.Where(point => point.y < lowest + .12f).OrderBy(point => Vector3.Distance(point, target)).First();
        }

        IEnumerator Inspect(string id, bool play = false, bool flip = false)
        {
            step = "Inspect " + id;
            yield return Ready();
            var clue = flow.clues.Single(item => item && item.itemId == id);
            Assert.That(clue.CanInteract(game.interaction), Is.True, id);
            var item = clue.GetComponent<InspectableItem>();
            Assert.That(item && item.item && item.item.model, Is.True, id + " inspection model is missing.");
            Aim(SupportedSightline(clue.transform.position, new[] { 1.1f, 1.4f, 1.7f }, clue), clue.transform.position);
            yield return null; yield return null;
            Assert.That(game.interaction.Target == clue || game.interaction.Target == item, Is.True, id + " must be reached by the player ray.");
            Assert.That(inspector.Begin(item, game.interaction), Is.True);
            yield return Until(() => inspector.IsOpen && inspector.Alpha > .9f);
            Assert.That(game.State, Is.EqualTo(GameState.Inspecting));
            if (flip) { inspector.Flip(); yield return Until(() => inspector.BackVisible); }
            if (play)
            {
                Assert.That(inspector.CurrentItem.canPlay, Is.True);
                inspector.PlayRecording();
                Assert.That(game.narrative.HasSequence(id + ".listened"), Is.False, "Starting a tape cannot reveal its conclusion.");
                Assert.That(game.narrative.NextSignal, Is.EqualTo(id == "hospicio_child_tape" ? "hospicio.child-tape" : "hospicio.director-tape"),
                    "Listening cannot advance the world before inspection closes.");
                game.Pause(); yield return new WaitForSecondsRealtime(.12f); game.Resume();
                float recordingSeconds = inspector.CurrentItem.recording.clips.Max(clip => clip.length);
                yield return Until(() => game.narrative.HasSequence(id + ".listened"), recordingSeconds + 3);
            }
            inspector.Close();
            yield return Until(() => !inspector.IsOpen && !inspector.IsCompleting && game.interaction.inventory.Contains(id));
            yield return Ready();
            Assert.That(game.interaction.inventory.items.Single(entry => entry.id == id).inspectionId, Is.EqualTo(id));
        }

        [UnityTest, Timeout(360000)]
        public IEnumerator LibraryKeysTapesBlackoutMirrorAndSchoolExitUseTheSavedSixChapterRoute()
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<EnemyBrain>().Length, Is.Zero);
            Enter(flow.libraryVolume);
            yield return Until(() => game.narrative.HasSignal("library.book-fell"));
            yield return Inspect("library_newspaper");
            yield return Inspect("library_class_photo", flip: true);
            yield return Inspect("iron_key");
            Assert.That(game.narrative.NextSignal, Is.EqualTo("library.passage-opened"));
            flow.libraryDoor.Interact(game.interaction);
            yield return Until(() => game.narrative.Progress.chapterId == "left-behind");
            Enter(flow.hospicioVolume);
            yield return Until(() => game.narrative.HasSignal("hospicio.arrived"));
            yield return Inspect("hospicio_recorder");
            yield return Inspect("hospicio_child_tape", play: true);
            yield return Until(() => game.narrative.NextSignal == "hospicio.register");
            // The newly accepted tape milestone can queue its spoken reaction after pickup.
            yield return Ready();
            Assert.That(game.save.Save(), Is.True);
            var sanity = flow.sanity;
            Assert.That(sanity, Is.Not.Null);
            sanity.blackoutSeconds = sanity.wakeSeconds = .08f;
            step = "blackout recovery";
            sanity.sanity = 0; sanity.AdvanceSanity(.01f, false);
            Assert.That(game.IsBlackingOut, Is.True);
            yield return Until(() => !game.IsBlackingOut && game.CanControl);
            Assert.That(game.narrative.Progress.chapterId, Is.EqualTo("left-behind"));
            Assert.That(game.narrative.NextSignal, Is.EqualTo("hospicio.register"));
            Assert.That(game.narrative.HasSequence("hospicio_child_tape.listened"), Is.True);
            Assert.That(game.interaction.inventory.Contains("iron_key"), Is.True);
            Assert.That(game.interaction.inventory.Contains("hospicio_child_tape"), Is.True);
            Assert.That(sanity.sanity, Is.GreaterThan(0));
            yield return Inspect("optional_class_note");
            Assert.That(game.narrative.NextSignal, Is.EqualTo("hospicio.register"), "Optional evidence must not replace the main objective.");
            yield return Inspect("hospicio_register");
            Vector3 watching = SupportedSightline(flow.figureLook.position, new[] { 8f, 10f, 13f });
            Aim(watching, flow.figureLook.position);
            yield return Until(() => flow.figureVisual.activeSelf && flow.Observes(flow.figureLook, 22, 28));
            yield return new WaitForSeconds(.4f);
            Aim(watching, watching + Vector3.right * 10);
            yield return Until(() => game.narrative.HasSignal("hospicio.figure"));
            Assert.That(flow.figureVisual.activeSelf, Is.False);
            yield return Inspect("hospicio_director_tape", play: true);
            yield return Inspect("room_113_key");
            yield return Until(() => game.narrative.Progress.chapterId == "memories");
            Assert.That(flow.stairGate.locked, Is.False);
            Assert.That(flow.room113Door.locked, Is.False);
            Enter(flow.upstairsVolume);
            yield return Until(() => game.narrative.HasSignal("memories.upstairs"));
            Enter(flow.corridorVolume);
            yield return Until(() => game.narrative.HasSignal("memories.corridor"));
            yield return Ready();
            flow.room113Door.SetOpen(true, true);
            var mirror = flow.mirror;
            Assert.That(mirror && mirror.surface && mirror.reflectionPlane && mirror.reflectionProxy && mirror.smile, Is.True);
            Aim(SupportedSightline(mirror.reflectionPlane.position, new[] { 1.8f, 2.2f, 2.6f }), mirror.reflectionPlane.position);
            yield return Until(() => game.narrative.HasSignal("memories.mirror"), 8);
            var reflected = mirror.surface.sharedMaterial.GetTexture("_BaseMap") as RenderTexture;
            Assert.That(reflected && reflected.IsCreated(), Is.True, "The mirror must render a real reflection, not only complete its timer.");
            var previousTarget = RenderTexture.active;
            var image = new Texture2D(reflected.width, reflected.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = reflected;
                image.ReadPixels(new Rect(0, 0, reflected.width, reflected.height), 0, 0);
                image.Apply();
                Assert.That(image.GetPixels32().Any(pixel => pixel.r > 5 || pixel.g > 5 || pixel.b > 5), Is.True, "Reflection cannot be entirely black.");
                string output = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/StoryFullUpdate20261014/Visual";
                Directory.CreateDirectory(output);
                File.WriteAllBytes(output + "/Mirror-reflection.png", image.EncodeToPNG());
            }
            finally { RenderTexture.active = previousTarget; UnityEngine.Object.Destroy(image); }
            yield return Inspect("optional_empty_seat");
            Assert.That(game.narrative.NextSignal, Is.EqualTo("memories.truth"));
            yield return Inspect("student_113_record");
            yield return Until(() => game.narrative.Progress.chapterId == "last-exit");
            Enter(flow.downstairsVolume);
            yield return Until(() => game.narrative.HasSignal("last-exit.stairs"));
            Enter(flow.returnVolume);
            yield return Until(() => game.narrative.HasSignal("last-exit.loop"));
            yield return Ready();
            Enter(flow.entranceVolume);
            yield return Until(() => game.narrative.HasSignal("last-exit.entrance-reached"));
            Assert.That(flow.entryDoor.locked, Is.False);
            Assert.That(flow.entryDoor.IsOpen, Is.True);
            Assert.That(game.narrative.NextSignal, Is.EqualTo("last-exit.outside"));
            Assert.That(game.interaction.inventory.Contains("student_113_record"), Is.True);
            Assert.That(game.save.Save(), Is.True);
            Assert.That(SaveSystem.TryRead(game.save.SavePath, out var checkpoint), Is.True);
            Assert.That(checkpoint.narrative.HasSignal("memories.truth"), Is.True);
            Assert.That(checkpoint.narrative.HasSequence("hospicio_director_tape.listened"), Is.True);
            Enter(flow.outsideVolume);
            yield return Until(() => SceneManager.GetActiveScene().name == "Chapter1Office" && GameSession.Instance && GameSession.Instance.narrative.IsInitialized, 20);
            game = GameSession.Instance;
            Assert.That(game.save.flashlight.IsAvailable, Is.False, "Returning to the office leaves the school equipment out of view.");
            Assert.That(game.save.flashlight.handRig.gameObject.activeInHierarchy, Is.False);
            var endingProps = UnityEngine.Object.FindAnyObjectByType<OfficeEndingFlow>();
            Assert.That(endingProps.endingCaseNote.activeInHierarchy && endingProps.penChalk.activeInHierarchy, Is.True);
            Assert.That(endingProps.initialCaseNote.activeSelf, Is.False);
            Assert.That(Vector3.Distance(endingProps.personalPen.position, endingProps.endingPenPose.position), Is.LessThan(.001f));
            var captureType = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("HorrorGame.Editor.PresentationCapture")).First(value => value != null);
            string endingImage = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RemainingPhases20261026/Phase5/Visual/Office-return-note-and-pen.png";
            captureType.GetMethod("Capture").Invoke(null, new object[] { SceneManager.GetActiveScene(), game.player.view.playerCamera, endingImage, 1280, 720, null, true });
            yield return Until(() => game.narrative.Progress.endingSeen, 12);
            Assert.That(game.narrative.Progress.chapterId, Is.EqualTo("last-exit"));
            Assert.That(game.narrative.HasSignal("last-exit.awake"), Is.True);
            Assert.That(game.narrative.HasSignal("last-exit.phone-ringing"), Is.True);
            Assert.That(game.interaction.inventory.Contains("student_113_record"), Is.True);
            Assert.That(game.interaction.inventory.Contains("hospicio_child_tape"), Is.True);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<OfficeEndingFlow>().IsEndingActive, Is.True);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator DarknessDeathRetriesTheLastSchoolCheckpointWithTimeToFindBatteries()
        {
            Enter(flow.libraryVolume);
            yield return Until(() => game.narrative.HasSignal("library.book-fell"));
            yield return Inspect("library_newspaper");
            yield return Ready();
            var sanity = flow.sanity;
            var lamp = game.save.flashlight;
            lamp.Restore(0, false);
            sanity.sanity = 14;
            Assert.That(game.save.Save(), Is.True);
            string savedBytes = File.ReadAllText(game.save.SavePath);
            Vector3 savedPosition = game.player.transform.position;
            sanity.blackoutSeconds = .08f;
            sanity.AdvanceSanity(100, false);
            yield return Until(() => game.State == GameState.GameOver);
            Assert.That(game.IsBlackingOut, Is.False, "Retry must be available after the final fade.");
            Assert.That(File.ReadAllText(game.save.SavePath), Is.EqualTo(savedBytes), "Death must not replace the checkpoint.");
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(game.hud.restartButton.gameObject.activeInHierarchy && game.hud.restartButton.IsInteractable(), Is.True);
            var buttonRect = (RectTransform)game.hud.restartButton.transform;
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                { position = RectTransformUtility.WorldToScreenPoint(null, buttonRect.TransformPoint(buttonRect.rect.center)) };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == game.hud.restartButton, Is.True,
                "Retry button at " + pointer.position + " is covered by " + string.Join(", ", hits.Select(hit => hit.gameObject.name)));
            var previousGame = game;
            game.hud.restartButton.onClick.Invoke();
            yield return Until(() => GameSession.Instance && GameSession.Instance != previousGame
                && GameSession.Instance.narrative.IsInitialized && GameSession.Instance.CanControl);
            game = GameSession.Instance;
            flow = UnityEngine.Object.FindAnyObjectByType<SchoolSimplifiedFlow>();
            inspector = InspectionManager.Instance;
            Assert.That(game.save.fileName, Is.EqualTo(disposable));
            Assert.That(game.narrative.Progress.chapterId, Is.EqualTo("school"));
            Assert.That(game.interaction.inventory.Contains("library_newspaper"), Is.True);
            Assert.That(Vector3.Distance(game.player.transform.position, savedPosition), Is.LessThan(.15f));
            Assert.That(game.save.flashlight.BatterySeconds, Is.Zero, "Retry restores sanity, not free batteries.");
            Assert.That(flow.sanity.sanity, Is.GreaterThanOrEqualTo(59.9f));
            Assert.That(flow.sanity.DarknessSeconds, Is.LessThan(1));
            float recovered = flow.sanity.sanity;
            flow.sanity.AdvanceSanity(19, false);
            Assert.That(flow.sanity.sanity, Is.EqualTo(recovered).Within(.001f));
            Assert.That(game.State, Is.EqualTo(GameState.Playing));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator IndependentReaderKeepsEvidenceLegibleAtAllZoomsAndEscClosesLayersInOrder()
        {
            var ui = inspector.GetComponentInChildren<InspectionUI>(true);
            Assert.That(ui, Is.Not.Null);
            var reader = ui.transform.Find("IndependentReader");
            Assert.That(reader, Is.Not.Null, "The reader must be independent of the fading inspection chrome.");
            Assert.That(reader.parent, Is.EqualTo(ui.transform));
            var body = reader.Find("ReaderViewport/ReaderText").GetComponent<TMPro.TextMeshProUGUI>();
            var scroll = reader.Find("ReaderViewport").GetComponent<UnityEngine.UI.ScrollRect>();
            var chrome = ui.transform.Find("InspectionChrome").GetComponent<CanvasGroup>();
            var captureType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("HorrorGame.Editor.PresentationCapture")).FirstOrDefault(type => type != null);
            Assert.That(captureType, Is.Not.Null, "The editor capture helper must be available for visual proof.");
            var capture = captureType.GetMethod("Capture", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.That(capture, Is.Not.Null);
            const string visual = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RemainingPhases20261026/Phase2/Visual";
            Directory.CreateDirectory(visual);

            foreach (string id in new[] { "library_newspaper", "hospicio_child_tape", "hospicio_director_tape", "student_113_record" })
            {
                step = "Independent reader " + id;
                var data = inspector.catalog.Find(id);
                Assert.That(data && data.model, Is.True, id + " must use its current authored inspection asset.");
                game.interaction.inventory.Add(new InventoryItem { id = id, title = data.title, note = true, inspectionId = id });
                Assert.That(inspector.Reopen(id), Is.True, "Collected evidence must reopen through Anotações.");
                yield return Until(() => inspector.Alpha > .99f);
                var audioButton = ui.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button => button.name == "PlayRecording");
                Assert.That(audioButton.gameObject.activeSelf, Is.EqualTo(data.canPlay), "Reader support must preserve the existing tape controls.");

                foreach (float zoom in new[] { .65f, .68f, .8f, 1f })
                {
                    float desired = Mathf.Lerp(1.35f, .48f, zoom);
                    inspector.CameraController.Zoom((-inspector.CameraController.Camera.transform.localPosition.z - desired) / .12f);
                    inspector.CameraController.Advance(1, false);
                    yield return null;
                    Assert.That(inspector.CameraController.Zoom01, Is.EqualTo(zoom).Within(.002f));
                    if (!ui.ReaderOpen) ui.ToggleReader();
                    Canvas.ForceUpdateCanvases();
                    body.ForceMeshUpdate();
                    yield return null;
                    Assert.That(ui.ReaderOpen && body.gameObject.activeInHierarchy, Is.True);
                    Assert.That(body.text, Is.Not.Empty, id + " cannot have an empty reader.");
                    Assert.That(body.textInfo.characterCount, Is.GreaterThan(0));
                    Assert.That(body.canvasRenderer.GetInheritedAlpha(), Is.GreaterThan(.9f),
                        "Reader text cannot inherit the disappearing chrome alpha at zoom " + zoom);
                    Assert.That(chrome.alpha, Is.LessThan(zoom >= .68f ? .001f : .05f));
                    string path = visual + "/" + id + "-zoom-" + Mathf.RoundToInt(zoom * 100) + ".png";
                    capture.Invoke(null, new object[] { SceneManager.GetActiveScene(), game.player.view.playerCamera, path, 1280, 720, null, true });
                    Assert.That(File.Exists(path) && new FileInfo(path).Length > 1000, Is.True, "Expected a rendered, nonempty PNG.");
                }

                float beforeBack = inspector.CameraController.Zoom01;
                inspector.Back();
                yield return null;
                Assert.That(ui.ReaderOpen, Is.False, "First Esc must close only the reader.");
                Assert.That(inspector.IsOpen && !inspector.IsCompleting, Is.True);
                Assert.That(inspector.CameraController.Zoom01, Is.EqualTo(beforeBack).Within(.002f), "Closing the reader must preserve zoom.");
                inspector.Back();
                yield return Until(() => inspector.PresentationZoom01 <= .01f && !inspector.FullscreenZoom);
                Assert.That(inspector.IsOpen && !inspector.IsCompleting, Is.True, "Second Esc must return from zoom without storing the item.");
                inspector.Back();
                yield return Until(() => !inspector.IsOpen && !inspector.IsCompleting);
                Assert.That(game.State, Is.EqualTo(GameState.Paused), "Closing evidence reopened from Anotações must return to the notebook.");
                Assert.That(game.hud.inventoryPanel.activeSelf, Is.True);
                game.Resume();
                yield return Ready();
            }

            var originalCatalog = inspector.catalog;
            var longItem = UnityEngine.Object.Instantiate(originalCatalog.Find("library_newspaper"));
            var longCatalog = UnityEngine.Object.Instantiate(originalCatalog);
            try
            {
                longItem.frontText = string.Join("\n\n", Enumerable.Range(1, 35).Select(index => "Frente " + index + ": registro de teste de leitura longa, sem alterar o documento original."));
                longItem.backText = string.Join("\n\n", Enumerable.Range(1, 35).Select(index => "Verso " + index + ": observação de teste para conferir rolagem e retorno ao início."));
                longCatalog.items = originalCatalog.items.Select(item => item && item.itemId == longItem.itemId ? longItem : item).ToArray();
                inspector.catalog = longCatalog;
                Assert.That(inspector.Reopen(longItem.itemId), Is.True);
                yield return Until(() => inspector.Alpha > .99f);
                ui.ToggleReader();
                foreach (bool back in new[] { false, true })
                {
                    var side = reader.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button => button.name == (back ? "ReadBack" : "ReadFront"));
                    side.onClick.Invoke();
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                    Assert.That(body.text, Does.Contain(back ? "Verso 35" : "Frente 35"));
                    Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height + 100), "Long evidence needs a scrollable text field.");
                    Assert.That(scroll.verticalNormalizedPosition, Is.GreaterThan(.99f), "Changing sides must start at the top.");
                    scroll.verticalNormalizedPosition = 0;
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                    Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(.01f), "The last paragraph must be reachable by scrolling.");
                    Assert.That(body.rectTransform.anchoredPosition.y, Is.GreaterThan(100));
                }
                inspector.Back();
                Assert.That(inspector.IsOpen && !ui.ReaderOpen, Is.True);
                inspector.Close();
                yield return Until(() => !inspector.IsOpen && !inspector.IsCompleting);
                Assert.That(game.State, Is.EqualTo(GameState.Paused));
                Assert.That(game.hud.inventoryPanel.activeSelf, Is.True);
                game.Resume();
            }
            finally
            {
                inspector.catalog = originalCatalog;
                UnityEngine.Object.Destroy(longCatalog);
                UnityEngine.Object.Destroy(longItem);
            }
        }

        [Serializable]
        sealed class LegacyReturnStep
        {
            public Vector3 target, actual;
            public float errorXZ, errorY;
            public int moves;
            public bool passed;
        }
        [Serializable]
        sealed class LegacyReturnResult
        {
            public string caseName, chapter, stairId, roomId;
            public Vector3 requestedStart, restoredStart;
            public bool tapeComplete, stairActive, stairLocked, roomLocked, roomOpen, passed;
            public LegacyReturnStep[] steps;
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator LegacyEarlyCheckpointFromUpperCorridorCanDescendWithoutCompletingTape()
        {
            yield return ProbeLegacyUpperReturn(false);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator LegacyEarlyCheckpointInside113CanReturnToGroundFloorWithoutCompletingTape()
        {
            yield return ProbeLegacyUpperReturn(true);
        }

        IEnumerator ProbeLegacyUpperReturn(bool inside113)
        {
            step = inside113 ? "Legacy early checkpoint inside room113" : "Legacy early checkpoint in upper corridor";
            Assert.That(game.save.fileName, Is.EqualTo(disposable));
            Assert.That(SaveSystem.TryRead(game.save.SavePath, out var checkpoint), Is.True);
            var progress = new NarrativeProgress { storyRevision = 2, chapterId = "left-behind" };
            progress.completedChapters.AddRange(new[] { "prologue", "road", "school" });
            foreach (var milestone in game.narrative.book.Chapter("school").milestones) progress.milestones.Add(milestone.id);
            checkpoint.narrative = progress;
            checkpoint.inventory = new List<InventoryItem> {
                new InventoryItem { id = "office_equipment" }, new InventoryItem { id = "iron_key" }, new InventoryItem { id = "room_113_key" }
            };
            checkpoint.objects.RemoveAll(record => record.id == flow.stairGate.persistentId || record.id == flow.room113Door.persistentId);
            foreach (var door in new[] { flow.stairGate, flow.room113Door })
                checkpoint.objects.Add(new SavedObject { id = door.persistentId, json = "{\"open\":true,\"locked\":false}" });
            Vector3 doorway = flow.room113Door.leafCollider.bounds.center;
            Vector3 inward = flow.room113Door.leafCollider.transform.forward;
            inward.y = 0; inward.Normalize();
            if (inward.x < 0) inward = -inward;
            Assert.That(inward.x, Is.GreaterThan(.8f), "The current room113 interior is east of the upper corridor.");
            Vector3 start = inside113 ? doorway + inward * 1.05f : new Vector3(-15.4f, 3.84f, 61.5f);
            start.y = 3.85f;
            int mask = ~(LayerMask.GetMask("Player") | (1 << 31));
            Physics.SyncTransforms();
            var floor = Physics.RaycastAll(start + Vector3.up * .25f, Vector3.down, 1, mask, QueryTriggerInteraction.Ignore)
                .Where(hit => hit.normal.y > .75f && Mathf.Abs(hit.point.y - 3.81f) < .12f).OrderByDescending(hit => hit.point.y).FirstOrDefault();
            Assert.That(floor.collider, Is.Not.Null, "The requested legacy position must have real upper-floor support.");
            checkpoint.position = new Vector3(start.x, floor.point.y + .04f, start.z);
            checkpoint.heading = 180; checkpoint.pitch = 0;
            var controller = game.player.Controller;
            Assert.That(Physics.CheckCapsule(checkpoint.position + Vector3.up * (controller.radius + .02f),
                checkpoint.position + Vector3.up * (game.player.standingHeight - controller.radius - .02f), controller.radius, mask,
                QueryTriggerInteraction.Ignore), Is.False, "The legacy position must begin outside furniture and walls.");
            game.BeginBlackout("");
            Assert.That(game.save.RestoreSnapshot(checkpoint), Is.True, "This early room-key snapshot was valid under SAVE003 and must not be silently rejected.");
            game.EndBlackout();
            game.narrative.InitializeAfterSession(true);
            yield return Ready();
            Physics.SyncTransforms();
            Assert.That(game.narrative.HasSignal("hospicio.director-tape"), Is.False);
            Assert.That(game.interaction.inventory.Contains("room_113_key"), Is.True);
            if (inside113) flow.room113Door.Interact(game.interaction); // Try the ordinary door contract from the room side; never force it open.
            var result = new LegacyReturnResult {
                caseName = inside113 ? "inside113" : "upperCorridor", chapter = game.narrative.Progress.chapterId,
                requestedStart = checkpoint.position, restoredStart = game.player.transform.position,
                tapeComplete = game.narrative.HasSignal("hospicio.director-tape"), stairId = flow.stairGate.persistentId,
                roomId = flow.room113Door.persistentId, stairActive = flow.stairGate.gameObject.activeInHierarchy,
                stairLocked = flow.stairGate.locked, roomLocked = flow.room113Door.locked, roomOpen = flow.room113Door.IsOpen
            };
            var targets = new List<Vector3>();
            if (inside113)
            {
                var outside = doorway - inward * 1.05f; outside.y = 3.81f;
                targets.Add(outside);
                targets.Add(new Vector3(-15.46f, 3.81f, outside.z));
            }
            targets.AddRange(new[] {
                new Vector3(-15.46f,3.81f,56.7f), new Vector3(-26.68f,3.81f,56.7f), new Vector3(-26.68f,3.81f,56.9f),
                new Vector3(-26.68f,1.905f,63.32f), new Vector3(-28.953f,1.905f,63.32f), new Vector3(-28.953f,0,56.9f),
                new Vector3(-28.953f,0,56.3f), new Vector3(-15.46f,0,56.3f)
            });
            var records = new List<LegacyReturnStep>();
            bool motorEnabled = game.player.enabled, flowEnabled = flow.enabled;
            game.player.enabled = false; flow.enabled = false;
            try
            {
                if (game.player.transform.position.y < 1) result.passed = true; // A compatibility restore may already relocate the player to safe ground.
                else
                {
                    foreach (var target in targets)
                    {
                        var delta = target - game.player.transform.position; delta.y = 0;
                        int maximum = Mathf.CeilToInt(delta.magnitude / .04f) + 80, moves = 0;
                        while (delta.magnitude > .025f && moves < maximum)
                        {
                            controller.Move(delta.normalized * Mathf.Min(.04f, delta.magnitude) + Vector3.down * .08f);
                            delta = target - game.player.transform.position; delta.y = 0; moves++;
                        }
                        for (int i = 0; i < 3; i++) controller.Move(Vector3.down * .08f);
                        Vector3 actual = game.player.transform.position;
                        float errorXZ = Vector2.Distance(new Vector2(actual.x, actual.z), new Vector2(target.x, target.z));
                        float errorY = Mathf.Abs(actual.y - target.y);
                        var record = new LegacyReturnStep { target = target, actual = actual, moves = moves,
                            errorXZ = errorXZ, errorY = errorY, passed = errorXZ < .08f && errorY < .2f };
                        records.Add(record);
                        if (!record.passed) break;
                        yield return null;
                    }
                    result.passed = records.Count == targets.Count && records.All(record => record.passed)
                        && game.player.transform.position.y < 1;
                }
            }
            finally
            {
                result.steps = records.ToArray();
                const string output = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RemainingPhases20261026/Phase3";
                Directory.CreateDirectory(output);
                File.WriteAllText(output + "/legacy-return-" + result.caseName + ".json", JsonUtility.ToJson(result, true));
                game.player.enabled = motorEnabled; flow.enabled = flowEnabled;
            }
            Assert.That(result.passed, Is.True, "A valid early-key checkpoint must retain a physical route back downstairs: " + JsonUtility.ToJson(result));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator FigureQuestionPrecedesDisappearanceAndNeitherReactionRepeatsAfterRestore()
        {
            Assert.That(game.save.Save(), Is.True);
            Assert.That(SaveSystem.TryRead(game.save.SavePath, out var snapshot), Is.True);
            var progress = new NarrativeProgress { chapterId = "left-behind", storyRevision = 2 };
            progress.completedChapters.AddRange(new[] { "prologue", "road", "school" });
            progress.milestones.AddRange(game.narrative.book.Chapter("school").milestones.Select(mark => mark.id));
            progress.milestones.AddRange(new[] { "hospicio.arrived", "hospicio.recorder", "hospicio.child-tape", "hospicio.register" });
            snapshot.narrative = progress;
            snapshot.inventory = new List<InventoryItem> { new InventoryItem { id = "iron_key" }, new InventoryItem { id = "hospicio_recorder" } };
            game.BeginBlackout(""); Assert.That(game.save.RestoreSnapshot(snapshot), Is.True); game.EndBlackout();
            game.narrative.InitializeAfterSession(true); yield return Ready();
            int questions = 0, absences = 0;
            game.narrative.SequenceStarted += id => {
                if (id == "hospicio.figure-seen-caption") { questions++; Assert.That(flow.figureVisual.activeSelf, Is.True); }
                if (id == "hospicio.figure-caption") { absences++; Assert.That(flow.figureVisual.activeSelf, Is.False); }
            };
            var watching = SupportedSightline(flow.figureLook.position, new[] { 8f, 10f, 13f });
            Aim(watching, flow.figureLook.position);
            yield return Until(() => questions == 1 && game.narrative.HasSignal("hospicio.figure-seen"));
            Assert.That(game.narrative.HasSignal("hospicio.figure"), Is.False);
            Assert.That(game.save.Save(), Is.True);
            Assert.That(SaveSystem.TryRead(game.save.SavePath, out var afterQuestion), Is.True);
            game.BeginBlackout(""); Assert.That(game.save.RestoreSnapshot(afterQuestion), Is.True); game.EndBlackout();
            game.narrative.InitializeAfterSession(true);
            Aim(watching, watching + Vector3.right * 10);
            yield return Until(() => absences == 1 && !game.narrative.SequencePlaying);
            Assert.That(questions, Is.EqualTo(1));
            Assert.That(game.narrative.HasSignal("hospicio.figure"), Is.True);
            Assert.That(game.save.Save(), Is.True);
            Assert.That(SaveSystem.TryRead(game.save.SavePath, out var afterAbsence), Is.True);
            game.BeginBlackout(""); Assert.That(game.save.RestoreSnapshot(afterAbsence), Is.True); game.EndBlackout();
            game.narrative.InitializeAfterSession(true);
            yield return new WaitForSeconds(.4f);
            Assert.That(questions, Is.EqualTo(1)); Assert.That(absences, Is.EqualTo(1));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= RedirectSave;
            Time.timeScale = 1; AudioListener.pause = false;
            var blank = SceneManager.CreateScene("Simplified school cleanup " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(blank);
            foreach (string name in new[] { "SchoolCEEP", "Chapter1Office", "RoadJourney" })
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            if (runtimeBook) UnityEngine.Object.Destroy(runtimeBook);
            foreach (var entry in files)
                if (entry.Value != null) File.WriteAllBytes(entry.Key, entry.Value);
                else if (File.Exists(entry.Key)) File.Delete(entry.Key);
            if (!string.IsNullOrEmpty(disposable)) foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string path = Path.Combine(Application.persistentDataPath, disposable + suffix);
                if (File.Exists(path)) File.Delete(path);
            }
            foreach (var entry in prefs)
                if (!entry.Value.had) PlayerPrefs.DeleteKey(entry.Key);
                else if (entry.Key == "Game.SaveScene") PlayerPrefs.SetString(entry.Key, entry.Value.value);
                else PlayerPrefs.SetInt(entry.Key, int.Parse(entry.Value.value));
            PlayerPrefs.Save();
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = runInBackground;
        }
    }
}
