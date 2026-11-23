// Verifica SetUp, RecorderControlsRemainVisibleAndCollectedTapesPlayFromTheActualSchoolCanvas, TearDown.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    /// <summary>Actual school inspection canvas and recordings, captured through the Game View.</summary>
    [Category("GameplayPolish")]
    public sealed class SchoolPolishPresentationTests
    {
        const string Visual = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/AudioPass20261029/LegacyRecorder";
        GameSession game;
        InspectionManager inspector;
        StoryBook inspectionBook;
        SchoolInspectionCatalog inspectionCatalog;
        string disposable,temporaryDirectory,previousDirectory;
        readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
        readonly Dictionary<string, (bool had, string value)> prefs = new Dictionary<string, (bool, string)>();
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        bool runInBackground;
        string RealSave => SaveSystem.PathForFile("save.json");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousDirectory=SaveSystem.TestSaveDirectory;
            temporaryDirectory=Path.Combine(Path.GetTempPath(),"Whispers-LegacyInspection-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temporaryDirectory);
            SaveSystem.TestSaveDirectory=temporaryDirectory;
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
            disposable = "school-presentation-" + Guid.NewGuid().ToString("N") + ".json";
            PlayerPrefs.SetInt("Game.ResumeRequested", 0); PlayerPrefs.SetInt("Game.StoryCarryRequested", 0);
            SceneManager.sceneLoaded += RedirectSave;
            Time.timeScale = 1; AudioListener.pause = false;
            yield return SceneManager.LoadSceneAsync("SchoolCEEP");
            yield return Until(() => GameSession.Instance && GameSession.Instance.narrative && GameSession.Instance.narrative.IsInitialized
                && InspectionManager.Instance && InspectionManager.Instance.CameraController, 20, "School inspection did not initialize.");
            game = GameSession.Instance; inspector = InspectionManager.Instance;
            inspectionCatalog=UnityEngine.Object.Instantiate(inspector.catalog);
            inspectionCatalog.items=inspectionCatalog.items.Concat(new[]{"hospicio_recorder","hospicio_child_tape","hospicio_director_tape"}.Select(id=>UnityEditor.AssetDatabase.LoadAssetAtPath<InspectionItemData>("Assets/SchoolCEEP/Data/SimplifiedStory20261014/"+id+".asset"))).ToArray();
            inspector.catalog=inspectionCatalog;
            Assert.That(game.save.fileName, Is.EqualTo(disposable));
            var flow = UnityEngine.Object.FindAnyObjectByType<SchoolSimplifiedFlow>();
            Assert.That(flow, Is.Not.Null);
            flow.enabled = false;
            if (flow.sanity) flow.sanity.enabled = false;
            game.narrative.SuspendForBlackout();game.narrative.enabled = false;game.EnableControl();
            game.input.Actions.devices = Array.Empty<InputDevice>();
            yield return Until(() => game.CanControl, 2, "Inspection control did not become available.");
        }
        void RedirectSave(Scene scene, LoadSceneMode mode)
        {
            foreach (var save in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SaveSystem>(true))) save.fileName = disposable;
            foreach(var session in scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<GameSession>(true)))
            {
                // The archived recorder UI contract needs an idle fixture, not a campaign opened without its road checkpoint.
                inspectionBook=ScriptableObject.CreateInstance<StoryBook>();inspectionBook.storyId="inspection-presentation-fixture";
                inspectionBook.chapters=new[]{new StoryChapterDefinition{id="inspection.wait",milestones=new[]{new StoryMilestoneDefinition{id="fixture.wait"}}}};
                session.narrative.book=inspectionBook;session.narrative.startingChapterId="inspection.wait";
            }
        }
        static IEnumerator Until(Func<bool> condition, float seconds, string message)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
        static void VisibleButton(UnityEngine.UI.Button button, bool available)
        {
            Assert.That(button && button.gameObject.activeInHierarchy, Is.True, "The recorder control is absent from the real canvas.");
            Assert.That(button.interactable, Is.EqualTo(available), button.name + " has the wrong tape availability.");
            foreach (var group in button.GetComponentsInParent<CanvasGroup>()) Assert.That(group.alpha, Is.GreaterThan(.95f));
            var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
            Vector2 minimum = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 maximum = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            float width = Mathf.Min(Screen.width, maximum.x) - Mathf.Max(0, minimum.x);
            float height = Mathf.Min(Screen.height, maximum.y) - Mathf.Max(0, minimum.y);
            Assert.That(width, Is.GreaterThan(1), button.name + " has no visible width in the Game View.");
            Assert.That(height, Is.GreaterThan(1), button.name + " has no visible height in the Game View.");
            if (!available) return;
            var events = EventSystem.current;
            Assert.That(events, Is.Not.Null);
            var hits = new List<RaycastResult>();
            events.RaycastAll(new PointerEventData(events) { position = (minimum + maximum) * .5f }, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), "The recorder button has no UI raycast surface.");
            Assert.That(hits[0].gameObject.transform == button.transform || hits[0].gameObject.transform.IsChildOf(button.transform), Is.True,
                button.name + " is covered by another UI element.");
        }
        static IEnumerator Capture(string name)
        {
            Directory.CreateDirectory(Visual);
            string path = Path.Combine(Visual, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            Canvas.ForceUpdateCanvases();
            yield return null;
            ScreenCapture.CaptureScreenshot(path);
            yield return Until(() => File.Exists(path) && new FileInfo(path).Length > 1024, 5,
                "Game View screenshot was not produced. Keep the Game View visible for this presentation test.");
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71, Is.True, "Screenshot is not a PNG.");
        }

        [UnityTest, Timeout(20000)]
        public IEnumerator RecorderControlsRemainVisibleAndCollectedTapesPlayFromTheActualSchoolCanvas()
        {
            var inventory = game.interaction.inventory;
            Assert.That(inventory.Contains("hospicio_child_tape") || inventory.Contains("hospicio_director_tape"), Is.False);
            inventory.Add(new InventoryItem { id = "hospicio_recorder", title = "Gravador antigo", inspectionId = "hospicio_recorder" });
            Assert.That(inspector.Reopen("hospicio_recorder"), Is.True);
            yield return Until(() => inspector.IsOpen && inspector.Alpha > .99f, 2, "Recorder inspection did not become visible.");
            Canvas.ForceUpdateCanvases();
            var ui = game.GetComponentsInChildren<InspectionUI>(true).Single();
            var buttons = ui.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            var first = buttons.Single(button => button.name == "PlayRecording");
            var second = buttons.Single(button => button.name == "PlaySecondTape");
            VisibleButton(first, false); VisibleButton(second, false);
            Assert.That(first.GetComponentInChildren<TMP_Text>().text, Does.Contain("não encontrada"));
            Assert.That(second.GetComponentInChildren<TMP_Text>().text, Does.Contain("não encontrada"));
            yield return Capture("Gravador-vazio");

            inventory.Add(new InventoryItem { id = "hospicio_child_tape", title = "Fita A", inspectionId = "hospicio_child_tape" });
            inventory.Add(new InventoryItem { id = "hospicio_director_tape", title = "Fita B", inspectionId = "hospicio_director_tape" });
            yield return null; Canvas.ForceUpdateCanvases();
            VisibleButton(first, true); VisibleButton(second, true);
            Assert.That(first.GetComponentInChildren<TMP_Text>().text, Does.Contain("Ouvir fita A"));
            Assert.That(second.GetComponentInChildren<TMP_Text>().text, Does.Contain("Ouvir fita B"));
            yield return Capture("Gravador-fitas");
            first.onClick.Invoke();
            yield return null;
            Assert.That(inspector.RecordingPlaying, Is.True);
            Assert.That(inspector.PlayingRecordingId, Is.EqualTo("hospicio_child_tape"));
            second.onClick.Invoke();
            yield return null;
            Assert.That(inspector.RecordingPlaying, Is.True);
            Assert.That(inspector.PlayingRecordingId, Is.EqualTo("hospicio_director_tape"));
            inspector.Cancel(true);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= RedirectSave;
            if (inspector) inspector.Cancel(true);
            Time.timeScale = 1; AudioListener.pause = false;
            var blank = SceneManager.CreateScene("School presentation cleanup " + Guid.NewGuid().ToString("N")); SceneManager.SetActiveScene(blank);
            var school = SceneManager.GetSceneByName("SchoolCEEP"); if (school.isLoaded) yield return SceneManager.UnloadSceneAsync(school);
            foreach (var entry in files)
                if (entry.Value != null) File.WriteAllBytes(entry.Key, entry.Value);
                else if (File.Exists(entry.Key)) File.Delete(entry.Key);
            if (!string.IsNullOrEmpty(disposable)) foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string path = SaveSystem.PathForFile(disposable + suffix); if (File.Exists(path)) File.Delete(path);
            }
            foreach (var entry in prefs)
                if (!entry.Value.had) PlayerPrefs.DeleteKey(entry.Key);
                else if (entry.Key == "Game.SaveScene") PlayerPrefs.SetString(entry.Key, entry.Value.value);
                else PlayerPrefs.SetInt(entry.Key, int.Parse(entry.Value.value));
            PlayerPrefs.Save();
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = runInBackground;
            if(inspectionBook)UnityEngine.Object.Destroy(inspectionBook);
            if(inspectionCatalog)UnityEngine.Object.Destroy(inspectionCatalog);
            SaveSystem.TestSaveDirectory=previousDirectory;
            if(!string.IsNullOrEmpty(temporaryDirectory)&&Directory.Exists(temporaryDirectory))Directory.Delete(temporaryDirectory,true);
        }
    }
}
