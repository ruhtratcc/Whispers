// Verifica .

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    // Shared isolated runtime fixture; native scene/save-validator coverage belongs to the integrated scene suite.
    internal sealed class BeforeDemolitionChapterFixture
    {
        public GameObject root;
        public GameSession game;
        public NarrativeDirector director;
        public StoryBook book;
        public Inventory inventory;
        public FirstPersonMotor player;
        public Camera camera;
        public BeforeDemolitionWhistle whistle;
        public ChapterTransitionView transition;
        Scene scene, priorScene;
        string directory, oldDirectory;
        float oldScale;
        bool oldAudio, oldCursorVisible;
        CursorLockMode oldCursorLock;
        readonly Dictionary<string, (bool exists, int value)> flags = new Dictionary<string, (bool, int)>();

        public BeforeDemolitionChapterFixture(string chapter, StoryMilestoneDefinition[] milestones, bool audio = false)
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None), Is.Empty,
                "Start these tests from an isolated blank scene.");
            oldDirectory = SaveSystem.TestSaveDirectory;
            directory = Path.Combine(Path.GetTempPath(), "Whispers-RemainingPlay-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); SaveSystem.TestSaveDirectory = directory; TutorialPreferences.Set(TutorialMode.Off);
            foreach (string suffix in new[] { "ResumeRequested", "StoryCarryRequested" })
            {
                string key = "BeforeDemolition." + suffix;
                flags[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key)); PlayerPrefs.SetInt(key, 0);
            }
            oldScale = Time.timeScale; oldAudio = AudioListener.pause;
            oldCursorVisible = Cursor.visible; oldCursorLock = Cursor.lockState;
            Time.timeScale = 1; AudioListener.pause = false;
            priorScene = SceneManager.GetActiveScene();
            scene = SceneManager.CreateScene("Remaining chapter fixture " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(scene);
            root = new GameObject("Isolated new-campaign fixture"); root.SetActive(false);
            game = root.AddComponent<GameSession>(); game.enabled = false;
            director = root.AddComponent<NarrativeDirector>(); inventory = root.AddComponent<Inventory>();
            var body = Child("Player"); player = body.AddComponent<FirstPersonMotor>(); player.enabled = false;
            var eye = Child("Eyes", body.transform); eye.transform.localPosition = Vector3.up * 1.64f;
            var view = eye.AddComponent<FirstPersonCamera>(); view.enabled = false; view.motor = player;
            camera = eye.AddComponent<Camera>(); camera.fieldOfView = 49; eye.AddComponent<AudioListener>();
            view.playerCamera = camera; player.view = view;
            var interaction = body.AddComponent<PlayerInteraction>(); interaction.enabled = false;
            interaction.inventory = inventory; interaction.motor = player; interaction.view = camera;
            game.player = player; game.interaction = interaction; game.narrative = director;
            director.game = game; director.inventory = inventory; director.startingChapterId = chapter;
            book = ScriptableObject.CreateInstance<StoryBook>(); book.storyId = BeforeDemolitionCampaign.StoryId;
            book.revision = BeforeDemolitionCampaign.Revision;
            book.chapters = new[] { new StoryChapterDefinition { id = chapter, heading = "CAPÍTULO", title = "Fixture", milestones = milestones } };
            book.sequences = new[] {
                Caption(BeforeDemolitionMirrorFlow.OfferSequence), Caption(BeforeDemolitionMirrorFlow.RefuseSequence),
                Caption(BeforeDemolitionMirrorFlow.SignSequence), Caption("bd.loop.voice"), Caption("bd.loop.phone")
            };
            director.book = book;
            whistle = Child("Whistle").AddComponent<BeforeDemolitionWhistle>(); whistle.game = game;
            inventory.Add(new InventoryItem { id = BeforeDemolitionCampaign.WhistleItemId });
            var card = Child("Final presentation"); transition = card.AddComponent<ChapterTransitionView>();
            transition.game = game; transition.group = card.AddComponent<CanvasGroup>();
            transition.heading = Label("Title", card.transform); transition.body = Label("Body", card.transform);
            transition.returnButton = Child("Return", card.transform).AddComponent<UnityEngine.UI.Button>();
            transition.fadeSeconds = transition.cardSeconds = transition.endingTitleSeconds = .02f;
            director.transitionView = transition;
            if (audio)
            {
                Assert.That(UnityEngine.Object.FindObjectsByType<AudioManager>(FindObjectsSortMode.None), Is.Empty,
                    "Audio fixture must not replace a running audio manager.");
                var audioManager=root.AddComponent<AudioManager>();audioManager.enabled=false;
                audioManager.cues=UnityEditor.AssetDatabase.LoadAssetAtPath<AudioCueLibrary>("Assets/Audio/AudioCues.asset");
                Assert.That(audioManager.cues,Is.Not.Null,"The audio fixture uses the imported recordings, never a synthesized fallback.");
            }
            Assert.That(Path.GetDirectoryName(SaveSystem.PathForFile(BeforeDemolitionCampaign.CheckpointFile)),
                Is.EqualTo(directory).IgnoreCase, "The profile override must exist before scene Awake.");
        }
        static StorySequenceDefinition Caption(string id) => new StorySequenceDefinition {
            id = id, lockPlayer = true, lines = new[] { new StoryLine { text = "Uma frase.", seconds = .1f } }
        };
        public GameObject Child(string name, Transform parent = null)
        {
            var item = new GameObject(name, typeof(RectTransform)); item.transform.SetParent(parent ? parent : root.transform, false); return item;
        }
        public TMP_Text Label(string name, Transform parent) => Child(name, parent).AddComponent<TextMeshProUGUI>();
        public BoxCollider Volume(string name, Vector3 center, Vector3 size)
        {
            var volume = Child(name).AddComponent<BoxCollider>(); volume.isTrigger = true;
            volume.transform.position = center; volume.size = size; return volume;
        }
        public IEnumerator Activate()
        {
            root.SetActive(true); director.InitializeAfterSession(false);
            yield return null; yield return null; Physics.SyncTransforms();
            yield return Until(() => director.IsInitialized && game.CanControl && !director.SequencePlaying, "Fixture chapter must finish its initial caption.");
            Assert.That(director.IsInitialized && game.CanControl, Is.True);
            Assert.That(SaveSystem.TestSaveDirectory, Is.EqualTo(directory));
        }
        public static IEnumerator Until(Func<bool> condition, string message, float timeout = 4)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
        public static T Private<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name); return (T)field.GetValue(target);
        }
        public static void Click(UnityEngine.UI.Button button)
        {
            Assert.That(EventSystem.current, Is.Not.Null);
            Assert.That(button && button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }
        public IEnumerator Dispose()
        {
            if (root) UnityEngine.Object.Destroy(root);
            if (book) UnityEngine.Object.Destroy(book);
            yield return null;
            if (priorScene.IsValid() && priorScene.isLoaded) SceneManager.SetActiveScene(priorScene);
            else SceneManager.SetActiveScene(SceneManager.CreateScene("Remaining cleanup " + Guid.NewGuid().ToString("N")));
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            SaveSystem.ClearChapterCarry(); SaveSystem.TestSaveDirectory = oldDirectory;
            foreach (var flag in flags)
                if (flag.Value.exists) PlayerPrefs.SetInt(flag.Key, flag.Value.value); else PlayerPrefs.DeleteKey(flag.Key);
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
            Time.timeScale = oldScale; AudioListener.pause = oldAudio;
            Cursor.lockState = oldCursorLock; Cursor.visible = oldCursorVisible;
        }
    }

    [Category("BeforeDemolitionMirror")]
    public sealed partial class BeforeDemolitionMirrorTests
    {
        BeforeDemolitionChapterFixture f;
        BeforeDemolitionMirrorFlow flow;
        int seenCount, completedCount;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            f = new BeforeDemolitionChapterFixture(BeforeDemolitionMirrorFlow.ChapterId, new[] {
                new StoryMilestoneDefinition { id = BeforeDemolitionMirrorFlow.SeenSignal },
                new StoryMilestoneDefinition { id = BeforeDemolitionMirrorFlow.BrokenSignal },
                new StoryMilestoneDefinition { id = BeforeDemolitionMirrorFlow.ChoiceSignal },
                new StoryMilestoneDefinition { id = BeforeDemolitionMirrorFlow.FinishSignal }
            });
            flow = f.Child("Mirror interaction").AddComponent<BeforeDemolitionMirrorFlow>();
            flow.game = f.game; flow.whistle = f.whistle;
            flow.mirrorVolume = f.Volume("Approach", new Vector3(0, 1, 0), new Vector3(5, 4, 5));
            flow.mirrorPlane = f.Child("Mirror plane").transform; flow.mirrorPlane.position = new Vector3(0, 1.64f, 2);
            flow.glassVisual = f.Child("Glass"); flow.revealVisual = f.Child("Wall and four bags");
            flow.reflectionVisual = f.Child("Seated reflection"); flow.reflectionVisual.transform.position = new Vector3(0, 0, 4);
            flow.reflectionHead = f.Child("Head", flow.reflectionVisual.transform).transform;
            flow.helenaVisual = f.Child("Seated Helena"); flow.smileVisual = f.Child("Smile", flow.reflectionHead);
            flow.chalkTeethVisual = f.Child("Chalk teeth", flow.reflectionHead);
            flow.daylight = f.Child("Dawn").AddComponent<Light>();
            f.director.SignalAccepted += id => { if (id == BeforeDemolitionMirrorFlow.SeenSignal) seenCount++; };
            f.director.onChapterCompleted.AddListener(id => completedCount++);
            seenCount = completedCount = 0;
            yield return f.Activate();
        }
        void NearScare()
        {
            // Accelerate only the unit fixture's wait; the real Update still owns volume and gaze gates.
            var age = typeof(BeforeDemolitionMirrorFlow).GetField("mirrorAge", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(age, Is.Not.Null); age.SetValue(flow, 7.99f);
        }
        IEnumerator ChoiceReady()
        {
            f.player.Warp(Vector3.zero,0);f.camera.transform.SetPositionAndRotation(new Vector3(0,1.64f,0),Quaternion.identity);
            Physics.SyncTransforms();NearScare();
            yield return BeforeDemolitionChapterFixture.Until(()=>flow.endingView&&flow.endingView.IsOpen,"Approach and offer open the choice without blowing the whistle.");
            Assert.That(flow.Data.stage,Is.EqualTo(2));Assert.That(seenCount,Is.EqualTo(1));
            Assert.That(flow.glassVisual.activeSelf,Is.True);Assert.That(flow.revealVisual.activeSelf,Is.False);
            Assert.That(flow.smileVisual.activeSelf,Is.False);Assert.That(flow.chalkTeethVisual.activeSelf,Is.False);
            Assert.That(BeforeDemolitionChapterFixture.Private<int>(f.player.view,"impulseSequence"),Is.Zero);
        }
        [UnityTest]public IEnumerator GazeAndVolumeGateOfferWithoutScareAndWhistleCannotBreakIt()
        {
            flow.Choose(BeforeDemolitionEndingChoice.Refuse);Assert.That(flow.Data.stage,Is.Zero);Assert.That(flow.BreakMirror(),Is.False);
            f.player.Warp(new Vector3(20,0,0),0);Physics.SyncTransforms();NearScare();yield return null;Assert.That(flow.Data.stage,Is.Zero);
            f.player.Warp(Vector3.zero,0);f.camera.transform.rotation=Quaternion.Euler(0,180,0);Physics.SyncTransforms();yield return null;Assert.That(flow.Data.stage,Is.Zero);
            flow.WhistleTap();flow.WhistleHold(10);Assert.That(flow.Data.stage,Is.Zero);
            yield return ChoiceReady();Assert.That(flow.BreakMirror(),Is.False);
            Assert.That(flow.endingView.SignButton.GetComponentInChildren<TMP_Text>().text,Is.EqualTo("Ficar no lugar deles"));
            Assert.That(flow.endingView.RefuseButton.GetComponentInChildren<TMP_Text>().text,Is.EqualTo("Não confio em você"));
        }
        [UnityTest]public IEnumerator PausingTheChoiceHidesAndRestoresItsModalWithoutChoosing()
        {
            yield return ChoiceReady();f.game.Pause();yield return null;
            Assert.That(flow.endingView.GetComponent<Canvas>().enabled,Is.False);
            BeforeDemolitionChapterFixture.Click(flow.endingView.SignButton);Assert.That(flow.Data.choice,Is.EqualTo(BeforeDemolitionEndingChoice.None));
            f.game.Resume();yield return null;Assert.That(flow.endingView.GetComponent<Canvas>().enabled,Is.True);
            yield return new WaitForSecondsRealtime(.2f);Assert.That(flow.Data.choice,Is.EqualTo(BeforeDemolitionEndingChoice.None));
        }
        [UnityTest]public IEnumerator RefuseBreaksOnlyAfterChoiceAndReturnWithoutWhistleRecoversSafeMarker()
        {
            yield return ChoiceReady();BeforeDemolitionChapterFixture.Click(flow.endingView.RefuseButton);
            Assert.That(flow.Data.choice,Is.EqualTo(BeforeDemolitionEndingChoice.Refuse));
            Assert.That(flow.StrikeActive,Is.True);Assert.That(flow.glassVisual.activeSelf,Is.True);Assert.That(flow.revealVisual.activeSelf,Is.False);
            yield return BeforeDemolitionChapterFixture.Until(()=>!flow.StrikeActive,"Glass breaks at impact, after the swing.");
            Assert.That(flow.glassVisual.activeSelf,Is.False);Assert.That(flow.revealVisual.activeSelf,Is.True);
            flow.Choose(BeforeDemolitionEndingChoice.Sign);Assert.That(flow.Data.choice,Is.EqualTo(BeforeDemolitionEndingChoice.Refuse));
            yield return BeforeDemolitionChapterFixture.Until(()=>f.game.CanControl&&!f.director.SequencePlaying,"Recoil and refusal finish before return.",6);
            f.inventory.Remove(BeforeDemolitionCampaign.WhistleItemId);Assert.That(flow.Prompt(f.game.interaction),Is.EqualTo("Aproximar"));
            Assert.That(flow.ReturnWhistle(),Is.True);Assert.That(flow.ReturnWhistle(),Is.False);
            yield return BeforeDemolitionChapterFixture.Until(()=>flow.GoodEndingReady,"Thanks finish before morning transition.",8);
            Assert.That(f.inventory.Contains(BeforeDemolitionCampaign.WhistleItemId),Is.True);
            string json=flow.CaptureState();int impulses=BeforeDemolitionChapterFixture.Private<int>(f.player.view,"impulseSequence");
            flow.RestoreState(json);yield return null;Assert.That(BeforeDemolitionChapterFixture.Private<int>(f.player.view,"impulseSequence"),Is.EqualTo(impulses));
            Assert.That(flow.GoodEndingReady,Is.True);flow.CompleteEnding();
            yield return BeforeDemolitionChapterFixture.Until(()=>f.director.Progress.endingSeen,"Completion happens exactly once after returned whistle.");
            flow.CompleteEnding();Assert.That(completedCount,Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator BadEndingRestoresWithoutScareAndShowsFourCardsBeforeCredits()
        {
            yield return ChoiceReady();flow.endingView.cardFadeSeconds=.01f;flow.endingView.badCardSeconds=.02f;flow.endingView.badPhotoSeconds=.02f;flow.endingView.badCreditsSeconds=.02f;
            BeforeDemolitionChapterFixture.Click(flow.endingView.SignButton);
            yield return BeforeDemolitionChapterFixture.Until(()=>flow.endingView.IsOpen,"Chosen bad ending opens.");
            string json=flow.CaptureState();flow.endingView.Close();flow.RestoreState(json);
            yield return BeforeDemolitionChapterFixture.Until(()=>flow.endingView.IsOpen,"Restoration resumes selected presentation.");
            Assert.That(flow.Data.choice,Is.EqualTo(BeforeDemolitionEndingChoice.Sign));Assert.That(flow.glassVisual.activeSelf,Is.True);
            Assert.That(BeforeDemolitionChapterFixture.Private<int>(f.player.view,"impulseSequence"),Is.Zero);
            yield return BeforeDemolitionChapterFixture.Until(()=>flow.endingView.ContinueButton.gameObject.activeSelf,"Cards and credits precede finish.",10);
            BeforeDemolitionChapterFixture.Click(flow.endingView.ContinueButton);
            yield return BeforeDemolitionChapterFixture.Until(()=>f.director.Progress.endingSeen,"Only selected branch completes.");
            Assert.That(completedCount,Is.EqualTo(1));Assert.That(flow.Data.apitoReturned,Is.False);
        }

        [TestCase("{}")]
        [TestCase("{\"version\":1,\"stage\":5,\"choice\":0}")]
        [TestCase("{\"version\":1,\"stage\":2,\"choice\":1}")]
        [TestCase("{\"version\":1,\"stage\":3,\"choice\":0}")]
        public void InvalidMirrorStatesFailClosed(string json)
        { Assert.Throws<ArgumentException>(() => BeforeDemolitionMirrorFlow.State.Parse(json)); }

        [UnityTearDown]
        public IEnumerator TearDown() { if (f != null) yield return f.Dispose(); f = null; }
    }
}
#endif
