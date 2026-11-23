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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("BeforeDemolitionFourStudents")]
    public sealed partial class BeforeDemolitionFourStudentsTests
    {
        const string ScenePath = "Assets/SchoolCEEP/Scenes/SchoolCEEP.unity", Prefix = "BeforeDemolition.";
        string directory, oldDirectory, oldSaveScene;
        bool guarded, hadSaveScene, oldBackground, oldAudio, oldCursorVisible;
        float oldTimeScale;
        CursorLockMode oldCursorLock;
        InputSettings.BackgroundBehavior inputBackground;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        readonly Dictionary<string, (bool had, int value)> preferences = new Dictionary<string, (bool, int)>();
        Keyboard keyboard, previousKeyboard;
        GameSession game;
        BeforeDemolitionFourStudentsFlow flow;
        InspectionManager inspection;
        List<InventoryItem> schoolInventory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None), Is.Empty,
                "Run from the isolated blank scene, before campaign Awake.");
            string scene = File.ReadAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ScenePath));
            string flowGuid = AssetDatabase.AssetPathToGUID("Assets/HorrorGameplay/Scripts/Narrative/BeforeDemolitionFourStudentsFlow.cs");
            string bookGuid = AssetDatabase.AssetPathToGUID("Assets/HorrorGameplay/Data/BeforeDemolition/BeforeDemolitionStory.asset");
            Assert.That(!string.IsNullOrEmpty(flowGuid) && scene.Contains("guid: " + flowGuid), Is.True, "Author F before loading.");
            Assert.That(!string.IsNullOrEmpty(bookGuid) && scene.Contains("guid: " + bookGuid), Is.True, "Bind the dedicated campaign before Awake.");

            directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Whispers-FourStudents-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            oldDirectory = SaveSystem.TestSaveDirectory;
            oldTimeScale = Time.timeScale; oldAudio = AudioListener.pause; oldBackground = Application.runInBackground;
            oldCursorLock = Cursor.lockState; oldCursorVisible = Cursor.visible;
            inputBackground = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousKeyboard = Keyboard.current;
            foreach (string suffix in new[] { "ResumeRequested", "StoryCarryRequested" })
                preferences[suffix] = (PlayerPrefs.HasKey(Prefix + suffix), PlayerPrefs.GetInt(Prefix + suffix));
            hadSaveScene = PlayerPrefs.HasKey(Prefix + "SaveScene"); oldSaveScene = PlayerPrefs.GetString(Prefix + "SaveScene");
            guarded = true;
            // Redirect before scene loading can execute GameSession.Awake or any checkpoint callback.
            SaveSystem.TestSaveDirectory = directory; TutorialPreferences.Set(TutorialMode.Off); SaveSystem.ClearChapterCarry();
            PlayerPrefs.SetInt(Prefix + "ResumeRequested", 0); PlayerPrefs.SetInt(Prefix + "StoryCarryRequested", 1);
            File.WriteAllText(Path.Combine(directory, BeforeDemolitionCampaign.CheckpointFile), JsonUtility.ToJson(CompletedRoad()));
            Time.timeScale = 1; AudioListener.pause = false; Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); SceneManager.sceneLoaded += Loaded;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return Until(() => GameSession.Instance && GameSession.Instance.CanControl
                && GameSession.Instance.narrative.IsInitialized, "School initializes from disposable road carry", 35);
            game = GameSession.Instance; flow = game.GetComponent<BeforeDemolitionFourStudentsFlow>(); inspection = InspectionManager.Instance;
            Assert.That(flow && inspection, Is.True);
            Assert.That(Path.GetDirectoryName(game.save.SavePath), Is.EqualTo(directory).IgnoreCase);
            Assert.That(game.narrative.Progress.chapterId, Is.EqualTo(BeforeDemolitionCampaign.SchoolChapterId));
            Assert.That(game.narrative.continueSameSceneChapters, Is.True, "E must hand off to F without reloading its school state.");
            game.player.view.bobEnabled = game.player.view.swayEnabled = false;
            var sanity = UnityEngine.Object.FindAnyObjectByType<SchoolSanityController>();
            if (sanity) sanity.enabled = false; // These contracts isolate progression; blackout has its own suite.
            schoolInventory = game.interaction.inventory.items.Select(CloneItem).ToList();
            foreach (string id in new[] { BeforeDemolitionSchoolFlow.KeyId, BeforeDemolitionSchoolFlow.ReportId,
                BeforeDemolitionSchoolFlow.TapeId, BeforeDemolitionSchoolFlow.TapeHeardId })
                schoolInventory.Add(new InventoryItem { id = id, inspectionId = id, title = id });
            game.interaction.inventory.Restore(schoolInventory.Select(CloneItem).ToList());
            var completed = SchoolPrefix(); completed.chapterId = BeforeDemolitionCampaign.SchoolChapterId;
            completed.pendingChapterId = BeforeDemolitionCampaign.FourChapterId;
            game.narrative.RestoreProgress(completed); game.narrative.InitializeAfterSession(true);
            yield return Until(() => flow.IsActive && game.CanControl && inspection.CameraController && !inspection.IsCompleting,
                "Completed E enters the authored F chapter", 12);
            Assert.That(game.player.view.playerCamera.fieldOfView, Is.EqualTo(49).Within(.2f));
            Assert.That(flow.stairDoor && flow.upstairsEntry && flow.stairKeyVisual && flow.items, Is.True);
            Assert.That(flow.stairDoor.locked, Is.True);
        }

        void Loaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var session in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameSession>(true)))
            {
                Assert.That(BeforeDemolitionCampaign.Is(session.narrative.book), Is.True);
                // Progression fixtures isolate phone modals; the phone suites exercise automatic opening.
                var phone = PhoneMessages.For(session); if (phone) phone.enabled = false;
                session.input.Actions.devices = new InputDevice[] { keyboard };
                if (session.narrative.transitionView)
                {
                    session.narrative.transitionView.fadeSeconds = .01f;
                    session.narrative.transitionView.cardSeconds = .02f;
                }
            }
        }

        SaveData CompletedRoad()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EmailCatalog>("Assets/HorrorGameplay/Data/BeforeDemolition/BeforeDemolitionOfficeEmails.asset");
            var emails = EmailInboxState.Create(catalog); emails.Deliver(catalog, "email.08");
            foreach (string id in new[] { "email.01", "email.06", "email.07" }) emails.MarkRead(catalog, id);
            var data = new SaveData { scene = "RoadJourney", stamina = 100, battery = 240, flashlightOn = true,
                narrative = new NarrativeProgress { storyId = BeforeDemolitionCampaign.StoryId, storyRevision = BeforeDemolitionCampaign.Revision,
                    chapterId = BeforeDemolitionCampaign.RoadChapterId, pendingChapterId = BeforeDemolitionCampaign.SchoolChapterId,
                    milestones = new List<string> { BeforeDemolitionCampaign.ReadySignal, BeforeDemolitionCampaign.ExitSignal },
                    completedChapters = new List<string> { BeforeDemolitionCampaign.OfficeChapterId, BeforeDemolitionCampaign.RoadChapterId } },
                inventory = new List<InventoryItem> { new InventoryItem { id = BeforeDemolitionCampaign.WhistleItemId, inspectionId = BeforeDemolitionCampaign.WhistleItemId },
                    new InventoryItem { id = BeforeDemolitionRoadFlow.EmailDeliveryItemId } }
            };
            data.narrative.milestones.AddRange(BeforeDemolitionCampaign.RoadSignals);
            foreach (string id in emails.readIds) data.inventory.Add(new InventoryItem { id = "bd.note." + id, note = true });
            data.objects.Add(new SavedObject { id = BeforeDemolitionRoadFlow.StateId,
                json = JsonUtility.ToJson(new BeforeDemolitionRoadState { phase = RoadRadioPhase.Parked, emails = emails }) });
            return data;
        }

        static NarrativeProgress SchoolPrefix()
        {
            var progress = new NarrativeProgress { storyId = BeforeDemolitionCampaign.StoryId, storyRevision = BeforeDemolitionCampaign.Revision,
                chapterId = BeforeDemolitionCampaign.FourChapterId,
                milestones = new List<string> { BeforeDemolitionCampaign.ReadySignal, BeforeDemolitionCampaign.ExitSignal },
                completedChapters = new List<string> { BeforeDemolitionCampaign.OfficeChapterId,
                    BeforeDemolitionCampaign.RoadChapterId, BeforeDemolitionCampaign.SchoolChapterId } };
            progress.milestones.AddRange(BeforeDemolitionCampaign.RoadSignals);
            progress.milestones.AddRange(BeforeDemolitionCampaign.SchoolSignals); return progress;
        }

        static InventoryItem CloneItem(InventoryItem item) => JsonUtility.FromJson<InventoryItem>(JsonUtility.ToJson(item));
        void Add(string id) => game.interaction.inventory.Add(new InventoryItem { id = id, title = id, inspectionId = id });
        BeforeDemolitionFourStudentsInteraction Target(BeforeDemolitionFourStudentsAction action) =>
            UnityEngine.Object.FindObjectsByType<BeforeDemolitionFourStudentsInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(target => target.flow == flow && target.action == action);
        static IEnumerator Until(Func<bool> predicate, string message, float seconds = 10)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, message);
        }
        IEnumerator Inspect(BeforeDemolitionFourStudentsAction action)
        {
            var proxy = Target(action); var item = proxy.GetComponent<InspectableItem>();
            Assert.That(item && item.originalInteraction == proxy && item.item && item.item.model, Is.True,
                "The authored object must retain the ordinary inspection proxy: " + action);
            Assert.That(item.CanInteract(game.interaction), Is.True, action.ToString());
            item.Interact(game.interaction);
            yield return Until(() => inspection.IsOpen && inspection.CurrentItem == item.item, "Inspect " + action);
            yield return new WaitForSeconds(.15f);
        }
        IEnumerator Close()
        {
            inspection.Close();
            yield return Until(() => !inspection.IsOpen && !inspection.IsCompleting, "Inspection finishes its original interaction");
            if (game.State == GameState.Paused) game.Resume(); // Reopening from Anotações returns to that modal.
            yield return Until(() => game.CanControl || game.narrative.SequencePlaying, "Inspection restores gameplay");
            yield return null;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EveryMissingStudentKeepsTheIntercomAndStairKeyLocked()
        {
            string[] names = { "Marta", "Caio", "Luan", "Bia" };
            for (int missing = 0; missing < 4; missing++)
            {
                game.interaction.inventory.Restore(schoolInventory.Select(CloneItem).ToList());
                game.narrative.RestoreProgress(SchoolPrefix()); game.narrative.InitializeAfterSession(true);
                for (int i = 0; i < 4; i++) if (i != missing) Add(BeforeDemolitionFourStudentsFlow.StudentItemIds[i]);
                if (missing != 3) Add(BeforeDemolitionFourStudentsFlow.BiaTapeHeardId);
                yield return null; yield return null;
                Assert.That(flow.Gathered || flow.IntercomCompleted || flow.HasStairKey, Is.False, names[missing]);
                Assert.That(flow.stairDoor.locked && !flow.stairKeyVisual.activeSelf, Is.True);
                Assert.That(Target(BeforeDemolitionFourStudentsAction.StairKey).CanInteract(game.interaction), Is.False);
                Assert.That(game.hud.objective.text, Does.Contain(names[missing]));
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PartialAndPausedBiaPlaybackCannotSkipIntercomKeyPickupOrTheActualUpstairsVolume()
        {
            AudioPassSceneAudit.Check(game,"04-four-students");
            foreach (var action in new[] { BeforeDemolitionFourStudentsAction.Marta, BeforeDemolitionFourStudentsAction.Caio,
                BeforeDemolitionFourStudentsAction.Luan }) { yield return Inspect(action); yield return Close(); }
            var bia = Target(BeforeDemolitionFourStudentsAction.Bia);
            game.player.Warp(bia.transform.position + Vector3.right * .8f - Vector3.up * .5f, 0);
            yield return Until(() => game.hud.subtitle.text.StartsWith("Gravador"), "The room plays the preview");
            yield return Inspect(BeforeDemolitionFourStudentsAction.Bia);
            Assert.That(game.hud.subtitleGroup.alpha, Is.Zero, "The room preview must not remain behind the inspection.");
            Assert.That(game.hud.subtitle.text, Does.Not.StartWith("Gravador"));
            var recording = inspection.CurrentItem;
            Assert.That(recording.captionOnlyRecording, Is.False);
            Assert.That(inspection.HasRecording(0), Is.True);
            var clip = recording.recording.clips.Single();
            Assert.That(clip.name, Is.EqualTo("wp_mix_fita_bia"));
            inspection.PlayRecording(); yield return Until(() => inspection.RecordingPlaying, "Bia starts playing");
            yield return new WaitForSeconds(.15f); yield return Close();
            Assert.That(game.interaction.inventory.Contains(BeforeDemolitionFourStudentsFlow.BiaTapeId), Is.True);
            Assert.That(flow.TapeHeard || flow.Gathered || flow.HasStairKey, Is.False);
            Assert.That(game.save.Save(), Is.True, "An interrupted tape remains a legal checkpoint.");

            Assert.That(inspection.Reopen(BeforeDemolitionFourStudentsFlow.BiaTapeId), Is.True);
            yield return Until(() => inspection.IsOpen, "Bia reopens from inventory");
            inspection.PlayRecording();
            var tapeSource = BeforeDemolitionChapterFixture.Private<AudioSource>(inspection, "sound");
            yield return Until(() => tapeSource.isPlaying && tapeSource.timeSamples > 0, "The recorded voice is audible");
            game.Pause();
            int pausedAt = tapeSource.timeSamples;
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(tapeSource.timeSamples, Is.EqualTo(pausedAt), "Pause freezes the recorded voice's sample cursor.");
            Assert.That(flow.TapeHeard || flow.Gathered, Is.False, "Pause cannot finish a recording.");
            Assert.That(Target(BeforeDemolitionFourStudentsAction.StairKey).CanInteract(game.interaction), Is.False);
            game.Resume();
            yield return Until(() => flow.TapeHeard, "Natural recording completion produces heard proof", clip.length + 8);
            Assert.That(flow.Gathered, Is.False, "The intercom waits until inspection closes.");
            yield return Close();
            var phoneMessages = PhoneMessages.For(game);
            yield return Until(() => phoneMessages.State.Has("route.four.hint"), "The route message is delivered");
            Assert.That(flow.Gathered, Is.False, "The route message still needs to be read.");
            Assert.That(phoneMessages.OpenDirect(), Is.True);
            yield return null;
            phoneMessages.Close();
            yield return Until(() => flow.Gathered && game.CanControl, "All evidence and the read route message complete the investigation");
            if (!flow.IntercomCompleted)
            {
                Assert.That(flow.HasStairKey || flow.stairKeyVisual.activeSelf, Is.False);
                Assert.That(Target(BeforeDemolitionFourStudentsAction.StairKey).CanInteract(game.interaction), Is.False);
            }
            game.player.Warp(new Vector3(0, .7f, 52), 0);
            yield return Until(() => game.narrative.GetComponentsInChildren<AudioSource>().Any(source => source.isPlaying && source.clip && source.clip.name == "intercom_helena"), "The real intercom plays Helena's line");
            yield return Until(() => flow.IntercomCompleted && game.CanControl, "Intercom actually finishes", 35);
            Assert.That(flow.stairKeyVisual.activeSelf && !flow.HasStairKey && flow.stairDoor.locked, Is.True);
            Target(BeforeDemolitionFourStudentsAction.StairKey).Interact(game.interaction);
            yield return null;
            Assert.That(flow.HasStairKey && game.narrative.HasSignal(BeforeDemolitionFourStudentsFlow.KeySignal), Is.True);
            Assert.That(flow.stairKeyVisual.activeSelf, Is.False);
            Assert.That(game.narrative.HasSignal(BeforeDemolitionFourStudentsFlow.UpstairsSignal), Is.False);
            flow.stairDoor.Interact(game.interaction);
            Assert.That(!flow.stairDoor.locked && flow.stairDoor.IsOpen, Is.True);
            if (flow.stairDoor.pairedLeaf) Assert.That(!flow.stairDoor.pairedLeaf.locked && flow.stairDoor.pairedLeaf.IsOpen, Is.True);
            Assert.That(flow.upstairsStairTop,Is.Not.Null);
            game.player.Warp(flow.upstairsStairTop.bounds.center - Vector3.up * .8f, 0); Physics.SyncTransforms();
            yield return Until(() => game.narrative.HasSignal(BeforeDemolitionFourStudentsFlow.UpstairsSignal), "Authored upper entry advances F");
            yield return Until(() => game.narrative.Progress.chapterId == BeforeDemolitionCampaign.TwoVoicesChapterId, "F enters G", 12);
            Assert.That(flow.upstairsBackBlock.activeInHierarchy,Is.True);
            Assert.That(game.narrative.Progress.completedChapters, Does.Contain(BeforeDemolitionCampaign.FourChapterId));
            Assert.That(game.narrative.Progress.endingSeen, Is.False);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RestoringAnEarlierCheckpointRebuildsVisibilityAndDoesNotReplayCaioPickup()
        {
            yield return Inspect(BeforeDemolitionFourStudentsAction.Marta); yield return Close();
            Assert.That(game.save.Save(), Is.True);
            Assert.That(game.save.TryReadCurrent(out var checkpoint), Is.True);
            bool lightEnabled = flow.caioLight && flow.caioLight.enabled;
            yield return Inspect(BeforeDemolitionFourStudentsAction.Caio); yield return Close();
            yield return null;
            bool pausedLight = flow.caioLight && flow.caioLight.enabled;
            game.Pause(); yield return new WaitForSecondsRealtime(1.4f);
            if (flow.caioLight) Assert.That(flow.caioLight.enabled, Is.EqualTo(pausedLight), "Pause freezes Caio's brief light cue.");
            Assert.That(Target(BeforeDemolitionFourStudentsAction.Luan).CanInteract(game.interaction), Is.False);
            game.Resume();
            Assert.That(game.save.RestoreSnapshot(checkpoint), Is.True);
            game.narrative.InitializeAfterSession(true);
            yield return Until(() => game.CanControl && flow.IsActive, "F checkpoint restores");
            Assert.That(game.interaction.inventory.items.Count(item => item.id == BeforeDemolitionFourStudentsFlow.MartaNotebookId), Is.EqualTo(1));
            Assert.That(game.interaction.inventory.Contains(BeforeDemolitionFourStudentsFlow.CaioCapId), Is.False);
            Assert.That(flow.studentVisuals[0].activeSelf, Is.False);
            Assert.That(flow.studentVisuals[1].activeSelf, Is.True);
            Assert.That(flow.Gathered || flow.TapeHeard || flow.HasStairKey || flow.stairKeyVisual.activeSelf, Is.False);
            if (flow.caioLight) Assert.That(flow.caioLight.enabled, Is.EqualTo(lightEnabled), "Restoring cancels the transient without replay.");
            Assert.That(flow.roomDoors.All(door => !door || !door.locked), Is.True);
            Assert.That(flow.stairDoor.locked, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!guarded) yield break;
            SceneManager.sceneLoaded -= Loaded;
            var loaded = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++) loaded.Add(SceneManager.GetSceneAt(i));
            var blank = SceneManager.CreateScene("Four students cleanup " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(blank); Time.timeScale = 1; AudioListener.pause = false;
            foreach (var scene in loaded) if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            SaveSystem.ClearChapterCarry(); SaveSystem.TestSaveDirectory = oldDirectory;
            foreach (var pair in preferences)
                if (pair.Value.had) PlayerPrefs.SetInt(Prefix + pair.Key, pair.Value.value); else PlayerPrefs.DeleteKey(Prefix + pair.Key);
            if (hadSaveScene) PlayerPrefs.SetString(Prefix + "SaveScene", oldSaveScene); else PlayerPrefs.DeleteKey(Prefix + "SaveScene");
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            InputSystem.settings.backgroundBehavior = inputBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Application.runInBackground = oldBackground; Time.timeScale = oldTimeScale; AudioListener.pause = oldAudio;
            Cursor.lockState = oldCursorLock; Cursor.visible = oldCursorVisible;
            if (Directory.Exists(directory)) { foreach (string file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
            guarded = false;
        }
    }
}
#endif



