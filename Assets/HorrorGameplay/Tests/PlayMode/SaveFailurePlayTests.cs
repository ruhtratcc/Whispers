// Verifica SetUp, TearDown, InitialWriteFailureWarnsOnceAndRealPauseRetryRecoversAfterUnlock, BlockedOfficeExitCarriesEquipmentAndBatteryThroughAuthoredRoadTransition, BlockedRoadExitCarriesEquipmentAndBatteryThroughAuthoredSchoolTransition, BlockedSchoolExitCarriesKeysAndBatteryIntoExistingOfficeEnding.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorGame.Tests
{
    /// <summary>Real retry UI and authored scene handoffs while the disposable temporary file is locked.</summary>
    [Category("SaveFailure")]
    public sealed class SaveFailurePlayTests
    {
        readonly Dictionary<string, byte[]> personalFiles = new Dictionary<string, byte[]>();
        readonly Dictionary<string, (bool had, object value)> prefs = new Dictionary<string, (bool, object)>();
        readonly Dictionary<string, int> warnings = new Dictionary<string, int>();
        readonly Dictionary<AudioMixer, Dictionary<string, float>> mixers = new Dictionary<AudioMixer, Dictionary<string, float>>();
        readonly List<StoryBook> runtimeBooks = new List<StoryBook>();
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        bool runInBackground, listenerPaused, cursorVisible;
        float timeScale, listenerVolume;
        CursorLockMode cursorLock;
        UnityEngine.Random.State randomState;
        Keyboard keyboard;
        Mouse mouse;
        FileStream lockedTemporary;
        string disposableName, sourceScene, destinationScene;
        SaveData sourceSeed;
        byte[] sourceBytes;
        Vector3 sourcePosition, destinationSpawn;
        GameSession destination;
        string PersonalPath => Path.Combine(Application.persistentDataPath, "save.json");
        string DisposablePath => Path.Combine(Application.persistentDataPath, disposableName);

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
            timeScale = Time.timeScale; listenerPaused = AudioListener.pause; listenerVolume = AudioListener.volume;
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            randomState = UnityEngine.Random.state;
            CaptureMixers();
            disposableName = "save-failure-" + Guid.NewGuid().ToString("N") + ".json";
            SaveSystem.ClearChapterCarry();
            PlayerPrefs.SetInt("Game.ResumeRequested", 0);
            PlayerPrefs.SetInt("Game.StoryCarryRequested", 0);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground = true;
            Time.timeScale = 1; AudioListener.pause = false;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.one });
            SceneManager.sceneLoaded += PrepareScene;
            Application.logMessageReceived += CountWriteWarning;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= PrepareScene;
            Application.logMessageReceived -= CountWriteWarning;
            // Stop every deferred writer before unlocking files or restoring personal state.
            var loaded = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++) loaded.Add(SceneManager.GetSceneAt(i));
            var cleanup = SceneManager.CreateScene("SaveFailureCleanup-" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(cleanup);
            Time.timeScale = 1; AudioListener.pause = false;
            foreach (Scene scene in loaded)
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            lockedTemporary?.Dispose(); lockedTemporary = null;
            SaveSystem.ClearChapterCarry();
            foreach (var book in runtimeBooks) if (book) UnityEngine.Object.Destroy(book);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (!string.IsNullOrEmpty(disposableName))
                foreach (string suffix in new[] { "", ".bak", ".tmp" })
                    if (File.Exists(DisposablePath + suffix)) File.Delete(DisposablePath + suffix);
            foreach (var file in personalFiles)
                if (file.Value != null) File.WriteAllBytes(file.Key, file.Value);
                else if (File.Exists(file.Key)) File.Delete(file.Key);
            foreach (var pref in prefs)
                if (!pref.Value.had) PlayerPrefs.DeleteKey(pref.Key);
                else if (pref.Value.value is string text) PlayerPrefs.SetString(pref.Key, text);
                else PlayerPrefs.SetInt(pref.Key, (int)pref.Value.value);
            PlayerPrefs.Save();
            foreach (var mixer in mixers)
                if (mixer.Key) foreach (var parameter in mixer.Value) mixer.Key.SetFloat(parameter.Key, parameter.Value);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = runInBackground;
            Time.timeScale = timeScale; AudioListener.pause = listenerPaused; AudioListener.volume = listenerVolume;
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
            UnityEngine.Random.state = randomState;
            personalFiles.Clear(); prefs.Clear(); warnings.Clear(); mixers.Clear(); runtimeBooks.Clear();
            sourceSeed = null; sourceBytes = null; destination = null;
            sourceScene = destinationScene = disposableName = null;
        }

        void CaptureMixers()
        {
            foreach (var mixer in Resources.FindObjectsOfTypeAll<AudioMixer>())
            {
                if (mixers.ContainsKey(mixer)) continue;
                var values = new Dictionary<string, float>();
                foreach (string name in new[] { "MusicVolume", "SFXVolume", "AmbienceVolume", "VoiceVolume" })
                    if (mixer.GetFloat(name, out float value)) values[name] = value;
                mixers[mixer] = values;
            }
        }

        void PrepareScene(Scene scene, LoadSceneMode mode)
        {
            CaptureMixers();
            foreach (var save in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SaveSystem>(true)))
                save.fileName = disposableName;
            foreach (var game in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameSession>(true)))
            {
                if (game.input && game.input.Actions) game.input.Actions.devices = new InputDevice[] { keyboard, mouse };
                if (game.narrative && game.narrative.book)
                {
                    var book = UnityEngine.Object.Instantiate(game.narrative.book);
                    runtimeBooks.Add(book);
                    // Shorten only runtime presentation copies; installed voice clips and StoryBooks stay untouched.
                    foreach (var sequence in book.sequences)
                        foreach (var line in sequence.lines) { line.voice = null; line.seconds = .04f; }
                    game.narrative.book = book;
                    if (game.narrative.transitionView)
                    { game.narrative.transitionView.fadeSeconds = .04f; game.narrative.transitionView.cardSeconds = .06f; }
                }
                if (scene.name == sourceScene && sourceSeed != null)
                {
                    sourceSeed.position = game.player.transform.position;
                    sourceSeed.heading = 77;
                    File.WriteAllText(DisposablePath, JsonUtility.ToJson(sourceSeed, true));
                    sourceBytes = File.ReadAllBytes(DisposablePath);
                    game.narrative.Initialized += () => sourcePosition = game.player.transform.position;
                }
                if (scene.name == destinationScene)
                {
                    destination = game;
                    destinationSpawn = game.player.transform.position;
                    // Authored flow subscribers run first and place the road seat or final office pose.
                    game.narrative.Initialized += game.Pause;
                }
            }
        }

        void CountWriteWarning(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Warning || !condition.StartsWith("Checkpoint could not be saved:", StringComparison.Ordinal)) return;
            string scene = SceneManager.GetActiveScene().name;
            warnings[scene] = WarningCount(scene) + 1;
        }
        int WarningCount(string scene) => warnings.TryGetValue(scene, out int count) ? count : 0;
        void BlockTemporary() => lockedTemporary = new FileStream(DisposablePath + ".tmp", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        void UnblockTemporary() { lockedTemporary.Dispose(); lockedTemporary = null; }
        static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        static IEnumerator Until(Func<bool> condition, string message, float seconds = 40)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }

        static Vector2 Target(UnityEngine.UI.Button button)
        {
            Assert.That(button && button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True, "Retry must be visible and enabled.");
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Assert.That(hits, Is.Not.Empty, "Retry has no pointer target.");
            Assert.That(hits[0].module, Is.TypeOf<UnityEngine.UI.GraphicRaycaster>());
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject), "Retry is covered by another UI element.");
            return point;
        }
        IEnumerator Click(UnityEngine.UI.Button button)
        {
            Vector2 point = Target(button);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return Frames(2);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left, true));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return Frames(2);
        }
        IEnumerator PauseWithEscape(GameSession game)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return Frames(2);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frames(2);
            Assert.That(game.State, Is.EqualTo(GameState.Paused), "The real Escape binding must open pause.");
        }

        static StoryBook InstalledBook()
        {
#if UNITY_EDITOR
            var book = AssetDatabase.LoadAssetAtPath<StoryBook>("Assets/HorrorGameplay/Data/WhispersStory.asset");
            Assert.That(book, Is.Not.Null);
            Assert.That(book.revision, Is.EqualTo(2));
            return book;
#else
            Assert.Ignore("These integration seeds use the installed Editor StoryBook.");
            return null;
#endif
        }

        SaveData ChapterExitSeed(string chapterId)
        {
            var book = InstalledBook();
            int index = book.ChapterIndex(chapterId);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            var progress = new NarrativeProgress { storyRevision = 2, chapterId = chapterId };
            foreach (var chapter in book.chapters.Take(index + 1))
            {
                foreach (var milestone in chapter.milestones)
                {
                    progress.milestones.Add(milestone.id);
                    if (chapterId == "last-exit" && chapter.id == chapterId && milestone.id == "last-exit.outside") break;
                }
                if (chapter.id != "last-exit") progress.completedChapters.Add(chapter.id);
            }
            if (chapterId != "last-exit") progress.pendingChapterId = book.chapters[index + 1].id;
            foreach (var sequence in book.sequences)
                if (string.IsNullOrEmpty(sequence.completionSignal) || progress.HasSignal(sequence.completionSignal)) progress.sequences.Add(sequence.id);
            var seed = new SaveData { scene = book.chapters[index].sceneName, battery = 137.25f, stamina = 5, flashlightOn = false, narrative = progress };
            seed.inventory.Add(new InventoryItem { id = "office_equipment", title = "Equipamentos" });
            seed.inventory.Add(new InventoryItem { id = "save-failure-marker-" + Guid.NewGuid().ToString("N"), title = "Prova de transição" });
            if (chapterId == "last-exit")
            {
                seed.inventory.Add(new InventoryItem { id = "iron_key", title = "Chave de Ferro" });
                seed.inventory.Add(new InventoryItem { id = "room_113_key", title = "Chave da sala 113" });
            }
            return seed;
        }

        [UnityTest]
        public IEnumerator InitialWriteFailureWarnsOnceAndRealPauseRetryRecoversAfterUnlock()
        {
            BlockTemporary();
            yield return SceneManager.LoadSceneAsync("Chapter1Office");
            yield return Until(() => GameSession.Instance && GameSession.Instance.narrative.IsInitialized && GameSession.Instance.save.HasPendingSave,
                "Blocked initial checkpoint did not become pending.");
            yield return null;
            var game = GameSession.Instance;
            Assert.That(game.save.fileName, Is.EqualTo(disposableName));
            Assert.That(game.State, Is.EqualTo(GameState.Playing));
            Assert.That(game.hud.SaveRetryButton, Is.Not.Null);
            Assert.That(game.hud.SaveRetryButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(game.hud.GetComponentsInChildren<TMP_Text>(true).Any(text => text.gameObject.activeInHierarchy
                && text.text.IndexOf("[Esc]", StringComparison.OrdinalIgnoreCase) >= 0), Is.True, "Playing must explain how to reach retry.");
            yield return Frames(20);
            Assert.That(WarningCount("Chapter1Office"), Is.EqualTo(1), "A failed save event must not retry every Update.");
            yield return PauseWithEscape(game);
            yield return Click(game.hud.SaveRetryButton);
            Assert.That(game.save.HasPendingSave, Is.True);
            Assert.That(WarningCount("Chapter1Office"), Is.EqualTo(2), "A blocked retry must make exactly one new write attempt.");
            yield return Frames(20);
            Assert.That(WarningCount("Chapter1Office"), Is.EqualTo(2));
            UnblockTemporary();
            yield return Click(game.hud.SaveRetryButton);
            Assert.That(game.save.HasPendingSave, Is.False);
            Assert.That(game.hud.SaveRetryButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(WarningCount("Chapter1Office"), Is.EqualTo(2));
            Assert.That(SaveSystem.TryRead(DisposablePath, out var saved), Is.True);
            Assert.That(saved.scene, Is.EqualTo("Chapter1Office"));
            Assert.That(saved.narrative.chapterId, Is.EqualTo("prologue"));
            Assert.That(File.Exists(DisposablePath + ".tmp"), Is.False);
        }

        [UnityTest]
        public IEnumerator BlockedOfficeExitCarriesEquipmentAndBatteryThroughAuthoredRoadTransition()
            => CarryThroughFailure("prologue", "RoadJourney", "road");

        [UnityTest]
        public IEnumerator BlockedRoadExitCarriesEquipmentAndBatteryThroughAuthoredSchoolTransition()
            => CarryThroughFailure("road", "SchoolCEEP", "school");

        [UnityTest]
        public IEnumerator BlockedSchoolExitCarriesKeysAndBatteryIntoExistingOfficeEnding()
            => CarryThroughFailure("last-exit", "Chapter1Office", "last-exit");

        IEnumerator CarryThroughFailure(string chapter, string targetScene, string targetChapter)
        {
            sourceSeed = ChapterExitSeed(chapter);
            sourceScene = sourceSeed.scene; destinationScene = targetScene;
            PlayerPrefs.SetInt("Game.ResumeRequested", 1);
            PlayerPrefs.SetInt("Game.StoryCarryRequested", 0);
            PlayerPrefs.SetString("Game.SaveScene", sourceScene);
            BlockTemporary();
            // Only the source is arranged directly. Its installed Director/SchoolSimplifiedFlow loads the destination.
            yield return SceneManager.LoadSceneAsync(sourceScene);
            yield return Until(() => destination && SceneManager.GetActiveScene().name == targetScene && destination.narrative.IsInitialized
                && destination.State == GameState.Paused && destination.save.HasPendingSave, "Blocked source exit did not carry into a paused destination with a retryable checkpoint.");
            Assert.That(WarningCount(sourceScene), Is.EqualTo(1), "The source transition must make one save attempt, with no failure loop.");
            Assert.That(destination.save.fileName, Is.EqualTo(disposableName));
            Assert.That(destination.narrative.Progress.chapterId, Is.EqualTo(targetChapter));
            Assert.That(destination.narrative.Progress.storyRevision, Is.EqualTo(2));
            Assert.That(destination.interaction.inventory.Contains(sourceSeed.inventory[1].id), Is.True);
            Assert.That(destination.interaction.inventory.Contains("office_equipment"), Is.True);
            Assert.That(destination.save.flashlight.BatterySeconds, Is.EqualTo(sourceSeed.battery).Within(.01f));
            Assert.That(destination.narrative.Progress.HasSignal(chapter == "last-exit" ? "last-exit.outside" : sourceSeed.narrative.milestones.Last()), Is.True);
            Assert.That(Vector3.Distance(destination.player.transform.position, sourcePosition), Is.GreaterThan(.5f), "Carry must not restore the source map's position.");
            Vector3 expectedPose = destinationSpawn;
            if (targetScene == "RoadJourney")
            {
                var road = UnityEngine.Object.FindAnyObjectByType<RoadJourneyFlow>();
                Assert.That(road && road.Seated, Is.True);
                expectedPose = road.seatedPose.position - Vector3.up * destination.player.EyeHeight;
            }
            else if (chapter == "last-exit")
            {
                var ending = UnityEngine.Object.FindAnyObjectByType<OfficeEndingFlow>();
                Assert.That(ending && ending.IsEndingActive, Is.True, "Carry must enter the existing office ending, without replaying the prologue.");
                Assert.That(destination.interaction.inventory.Contains("iron_key"), Is.True);
                Assert.That(destination.interaction.inventory.Contains("room_113_key"), Is.True);
                expectedPose = ending.seatedPose.position - Vector3.up * destination.player.EyeHeight;
            }
            Assert.That(Vector3.Distance(destination.player.transform.position, expectedPose), Is.LessThan(.15f), "Destination must retain its own authored spawn or ending pose.");
            Assert.That(File.ReadAllBytes(DisposablePath), Is.EqualTo(sourceBytes), "Failed writes must preserve the source checkpoint bytes.");
            int targetWarnings = WarningCount(targetScene);
            Assert.That(targetWarnings, Is.GreaterThanOrEqualTo(1), "Destination startup must expose its own failed checkpoint for retry.");
            yield return Frames(20);
            Assert.That(WarningCount(targetScene), Is.EqualTo(targetWarnings), "A paused pending checkpoint must not retry every Update.");
            UnblockTemporary();
            yield return Click(destination.hud.SaveRetryButton);
            Assert.That(destination.save.HasPendingSave, Is.False);
            Assert.That(destination.hud.SaveRetryButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(SaveSystem.TryRead(DisposablePath, out var saved), Is.True);
            Assert.That(saved.scene, Is.EqualTo(targetScene), "Retry must persist the destination checkpoint rather than stale source pending data.");
            Assert.That(saved.narrative.chapterId, Is.EqualTo(targetChapter));
            Assert.That(saved.inventory.Any(item => item.id == sourceSeed.inventory[1].id), Is.True);
            Assert.That(saved.battery, Is.EqualTo(sourceSeed.battery).Within(.01f));
            Assert.That(Vector3.Distance(saved.position, expectedPose), Is.LessThan(.15f));
            Assert.That(PlayerPrefs.GetInt("Game.ResumeRequested"), Is.Zero);
            Assert.That(PlayerPrefs.GetInt("Game.StoryCarryRequested"), Is.Zero);
        }
    }
}
