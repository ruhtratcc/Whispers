// Verifica SetUp, WalkThroughOpenDoorAndPressEOnExitCarriesOfficeEquipmentToRoad, CaseNoteAndPenUseTheReaderWithoutEnablingTheOfficeFlashlight, TearDown.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorGame;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("OfficeExitInput")]
    public sealed class OfficeExitInputTests
    {
        readonly Dictionary<string, byte[]> personalFiles = new Dictionary<string, byte[]>();
        readonly Dictionary<string, (bool had, object value)> prefs = new Dictionary<string, (bool, object)>();
        readonly List<StoryBook> books = new List<StoryBook>();
        GameSession game;
        Keyboard keyboard;
        string disposable;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        bool runInBackground, listenerPaused, cursorVisible;
        float timeScale;
        CursorLockMode cursorLock;
        string PersonalPath => Path.Combine(Application.persistentDataPath, "save.json");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                personalFiles[PersonalPath + suffix] = File.Exists(PersonalPath + suffix) ? File.ReadAllBytes(PersonalPath + suffix) : null;
            prefs["Game.SaveScene"] = (PlayerPrefs.HasKey("Game.SaveScene"), PlayerPrefs.GetString("Game.SaveScene", ""));
            foreach (string key in new[] { "Game.ResumeRequested", "Game.StoryCarryRequested" })
                prefs[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key));
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            runInBackground = Application.runInBackground;
            timeScale = Time.timeScale;
            listenerPaused = AudioListener.pause;
            cursorLock = Cursor.lockState;
            cursorVisible = Cursor.visible;
            disposable = "office-exit-input-" + Guid.NewGuid().ToString("N") + ".json";
            SaveSystem.ClearChapterCarry();
            PlayerPrefs.SetInt("Game.ResumeRequested", 0);
            PlayerPrefs.SetInt("Game.StoryCarryRequested", 0);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground = true;
            Time.timeScale = 1;
            AudioListener.pause = false;
            keyboard = InputSystem.AddDevice<Keyboard>();
            Press();
            SceneManager.sceneLoaded += PrepareScene;
            yield return SceneManager.LoadSceneAsync("Chapter1Office");
            yield return Until(() => GameSession.Instance && GameSession.Instance.narrative
                && GameSession.Instance.narrative.IsInitialized && GameSession.Instance.CanControl, "office initialization", 20);
            game = GameSession.Instance;
            Assert.That(game.save.fileName, Is.EqualTo(disposable));
            game.player.view.bobEnabled = game.player.view.swayEnabled = false;
            yield return null;
        }

        void PrepareScene(Scene scene, LoadSceneMode mode)
        {
            foreach (var session in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameSession>(true)))
            {
                if (session.save) session.save.fileName = disposable;
                if (session.input && session.input.Actions) session.input.Actions.devices = new InputDevice[] { keyboard };
                if (!session.narrative || !session.narrative.book) continue;
                var book = UnityEngine.Object.Instantiate(session.narrative.book);
                books.Add(book);
                // These are presentation-only copies; the authored phone milestones and equipment interaction remain intact.
                foreach (var sequence in book.sequences)
                    foreach (var line in sequence.lines) { line.voice = null; line.seconds = .1f; }
                session.narrative.book = book;
                if (session.narrative.transitionView)
                {
                    session.narrative.transitionView.fadeSeconds = .05f;
                    session.narrative.transitionView.cardSeconds = .08f;
                }
            }
        }

        void Press(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        static IEnumerator Until(Func<bool> condition, string message, float seconds = 8)
        {
            float limit = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < limit) yield return null;
            Assert.That(condition(), Is.True, message);
        }
        void Aim(Vector3 feet, Vector3 target)
        {
            Vector3 delta = (target - (feet + Vector3.up * game.player.EyeHeight)).normalized;
            game.player.Warp(feet, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, -Mathf.Asin(delta.y) * Mathf.Rad2Deg);
            Physics.SyncTransforms();
        }
        IEnumerator InteractKey()
        {
            Press(Key.E);
            yield return null;
            yield return null;
            Press();
            yield return null;
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator WalkThroughOpenDoorAndPressEOnExitCarriesOfficeEquipmentToRoad()
        {
            var director = game.narrative;
            var exit = UnityEngine.Object.FindAnyObjectByType<StoryExit>();
            var target = exit.GetComponent<BoxCollider>();
            var door = UnityEngine.Object.FindObjectsByType<InteractiveDoor>().Single();
            var flow = UnityEngine.Object.FindAnyObjectByType<PrologueFlow>();
            Assert.That(target && target.enabled && !target.isTrigger, Is.True, "The ray target must remain a solid collider.");
            Assert.That(target.bounds.max.z, Is.LessThan(-5.05f), "The exit target must align with the vestibule's visible back boundary.");
            flow.firstCallDelay = flow.secondCallDelay = .02f;

            Aim(new Vector3(-2.08f, .05f, -2.1f), door.leafCollider.bounds.center);
            yield return Until(() => game.interaction.Target == door, "The closed door must be selected by the normal ray.");
            yield return InteractKey();
            yield return Until(() => door.IsOpen && Quaternion.Angle(door.hinge.localRotation, Quaternion.identity) > 85,
                "E must open the authored office door.");
            Aim(new Vector3(-2.08f, .05f, -2.55f), target.bounds.center);
            Press(Key.W);
            yield return Until(() => game.player.transform.position.z < -3.8f, "Normal W input must cross the open doorway into the vestibule.");
            Press();
            yield return null;
            Assert.That(game.player.transform.position.z, Is.LessThan(-3.75f));
            Aim(game.player.transform.position, target.bounds.center);
            yield return Until(() => game.interaction.Target == exit, "The visible passage must lead to the reachable narrative exit prompt.");
            StringAssert.Contains(exit.prompt, game.interaction.CurrentPrompt);
            yield return InteractKey();
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Chapter1Office"));
            Assert.That(director.HasSignal("prologue.exit"), Is.False, "Leaving before the calls and equipment must remain blocked by the story.");
            Assert.That(game.hud.notification.text, Is.EqualTo(exit.blockedMessage));

            yield return Until(() => flow.Ringing, "First phone call must ring.");
            Assert.That(flow.Answer(), Is.True);
            yield return Until(() => director.HasSignal("prologue.first-call") && !director.SequencePlaying && game.CanControl, "First call completion.");
            yield return Until(() => flow.Ringing, "Second phone call must ring.");
            Assert.That(flow.Answer(), Is.True);
            yield return Until(() => director.HasSignal("prologue.second-call") && !director.SequencePlaying && game.CanControl, "Second call completion.");
            var equipment = UnityEngine.Object.FindAnyObjectByType<StoryEquipment>();
            Assert.That(equipment.CanInteract(game.interaction), Is.False, "The closed drawer must still hide the equipment.");
            equipment.containingDrawer.Interact(game.interaction);
            yield return Until(() => equipment.CanInteract(game.interaction), "Open drawer must make the equipment available.");
            equipment.Interact(game.interaction);
            yield return Until(() => director.HasSignal("prologue.equipment") && !director.SequencePlaying && game.CanControl,
                "Collected equipment must complete its normal inventory milestone.");
            Assert.That(game.interaction.inventory.Contains("office_equipment"), Is.True);

            bool exitAccepted = false;
            director.SignalAccepted += signal => { if (signal == "prologue.exit") exitAccepted = true; };
            Aim(game.player.transform.position, target.bounds.center);
            yield return Until(() => game.interaction.Target == exit, "Exit must still be reachable after obtaining equipment.");
            yield return InteractKey();
            // No direct StoryExit.Interact, Signal(prologue.exit), or LoadScene call is used for this transition.
            yield return Until(() => SceneManager.GetActiveScene().name == "RoadJourney" && GameSession.Instance
                && GameSession.Instance.narrative && GameSession.Instance.narrative.IsInitialized, "E on the exit must load RoadJourney.", 20);
            Assert.That(exitAccepted, Is.True, "PlayerInteraction must have accepted the actual prologue exit milestone.");
            game = GameSession.Instance;
            Assert.That(game.narrative.Progress.chapterId, Is.EqualTo("road"));
            Assert.That(game.interaction.inventory.Contains("office_equipment"), Is.True);
            Assert.That(game.save.fileName, Is.EqualTo(disposable));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator CaseNoteAndPenUseTheReaderWithoutEnablingTheOfficeFlashlight()
        {
            yield return Until(() => InspectionManager.Instance && InspectionManager.Instance.CameraController, "Office inspector installation.");
            var inspector = InspectionManager.Instance;
            foreach (string id in new[] { "office_case_note", "office_personal_pen" })
            {
                var item = UnityEngine.Object.FindObjectsByType<InspectableItem>().Single(value => value.item && value.item.itemId == id);
                Assert.That(item.CanInteract(game.interaction), Is.True);
                Assert.That(inspector.Begin(item, game.interaction), Is.True);
                yield return Until(() => inspector.Alpha > .99f, "Office inspection fade.");
                var ui = inspector.GetComponentInChildren<InspectionUI>(); ui.ToggleReader();
                var body = ui.transform.Find("IndependentReader/ReaderViewport/ReaderText").GetComponent<TMPro.TMP_Text>();
                Assert.That(body.text, Does.Contain(id == "office_case_note" ? "biblioteca" : "Caneta antiga"));
                Assert.That(game.save.flashlight.IsAvailable || game.save.flashlight.IsOn, Is.False);
                inspector.Back(); inspector.Close();
                yield return Until(() => !inspector.IsOpen && !inspector.IsCompleting && game.CanControl, "Inspection restores the office controls.");
                Assert.That(game.interaction.inventory.Contains(id), Is.True);
                Assert.That(item.visual.activeInHierarchy, Is.True, "Examining this object must leave it in the world.");
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= PrepareScene;
            var loaded = new List<Scene>();
            for (int index = 0; index < SceneManager.sceneCount; index++) loaded.Add(SceneManager.GetSceneAt(index));
            var blank = SceneManager.CreateScene("OfficeExitCleanup-" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(blank);
            Time.timeScale = 1;
            AudioListener.pause = false;
            foreach (var scene in loaded) if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            SaveSystem.ClearChapterCarry();
            foreach (var book in books) if (book) UnityEngine.Object.Destroy(book);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string path = Path.Combine(Application.persistentDataPath, disposable + suffix);
                if (File.Exists(path)) File.Delete(path);
            }
            foreach (var file in personalFiles)
                if (file.Value != null) File.WriteAllBytes(file.Key, file.Value);
                else if (File.Exists(file.Key)) File.Delete(file.Key);
            foreach (var pref in prefs)
                if (!pref.Value.had) PlayerPrefs.DeleteKey(pref.Key);
                else if (pref.Value.value is string text) PlayerPrefs.SetString(pref.Key, text);
                else PlayerPrefs.SetInt(pref.Key, (int)pref.Value.value);
            PlayerPrefs.Save();
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = runInBackground;
            Time.timeScale = timeScale;
            AudioListener.pause = listenerPaused;
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorVisible;
        }
    }
}
