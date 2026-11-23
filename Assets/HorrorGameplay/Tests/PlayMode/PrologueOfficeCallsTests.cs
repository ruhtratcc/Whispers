// Verifica SetUp, TearDown, AuthoredOfficeCallsCheckpointInterruptionResumeAndRoadRestartStayInCurrentScene.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    /// <summary>Only the new office phone flow, at authored caption timings. No door/map tests.</summary>
    [Category("GameplayCompletion")]
    [Category("GameplayPolish")]
    public sealed class PrologueOfficeCallsTests
    {
        byte[] save, backup;
        string saveScene;
        int resume, carry;
        string SavePath => Path.Combine(Application.persistentDataPath, "save.json");
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            save = File.Exists(SavePath) ? File.ReadAllBytes(SavePath) : null;
            backup = File.Exists(SavePath + ".bak") ? File.ReadAllBytes(SavePath + ".bak") : null;
            saveScene = PlayerPrefs.GetString("Game.SaveScene", "");
            resume = PlayerPrefs.GetInt("Game.ResumeRequested", 0); carry = PlayerPrefs.GetInt("Game.StoryCarryRequested", 0);
            PlayerPrefs.SetInt("Game.ResumeRequested", 0); PlayerPrefs.SetInt("Game.StoryCarryRequested", 0);
            Time.timeScale = 1; AudioListener.pause = false;
            yield return SceneManager.LoadSceneAsync("Chapter1Office");
            yield return Ready();
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1; AudioListener.pause = false;
            yield return SceneManager.LoadSceneAsync("MainMenu");
            Restore(SavePath, save); Restore(SavePath + ".bak", backup);
            PlayerPrefs.SetString("Game.SaveScene", saveScene);
            PlayerPrefs.SetInt("Game.ResumeRequested", resume); PlayerPrefs.SetInt("Game.StoryCarryRequested", carry); PlayerPrefs.Save();
        }
        static void Restore(string path, byte[] original)
        { if (original != null) File.WriteAllBytes(path, original); else if (File.Exists(path)) File.Delete(path); }
        static IEnumerator Ready()
        {
            float deadline = Time.realtimeSinceStartup + 8;
            while ((!GameSession.Instance || !GameSession.Instance.narrative || !GameSession.Instance.narrative.IsInitialized)
                && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(GameSession.Instance && GameSession.Instance.narrative && GameSession.Instance.narrative.IsInitialized, Is.True);
            yield return null;
        }
        static IEnumerator Until(System.Func<bool> condition, float seconds = 5)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Office phone condition did not finish.");
        }
        [UnityTest] [Category("BlackoutCheckpoint")]
        public IEnumerator AuthoredOfficeCallsCheckpointInterruptionResumeAndRoadRestartStayInCurrentScene()
        {
            var game = GameSession.Instance;
            Assert.That(game.save.flashlight.IsAvailable, Is.False, "The torch is collected later in the car.");
            Assert.That(game.save.flashlight.IsOn, Is.False);
            Assert.That(game.save.flashlight.handRig.gameObject.activeInHierarchy, Is.False);
            game.save.flashlight.Toggle();
            Assert.That(game.save.flashlight.IsOn, Is.False, "The office cannot toggle unavailable equipment.");
            var director = game.narrative;
            var flow = Object.FindAnyObjectByType<PrologueFlow>();
            Assert.That(flow, Is.Not.Null); Assert.That(flow.telephone, Is.Not.Null);
            Assert.That(flow.telephone.ringSound, Is.Not.Null, "Assign the recorded telephone SoundSet.");
            Assert.That(flow.telephone.pickupSound, Is.Not.Null, "Assign receiver pickup Foley.");
            Assert.That(flow.telephone.hangupSound, Is.Not.Null, "Assign receiver placement Foley.");
            flow.firstCallDelay = .05f; flow.secondCallDelay = .05f;
            yield return Until(() => flow.Ringing);
            Assert.That(flow.Answer(), Is.True);
            yield return Until(() => director.ActiveSequenceId == "prologue.call-one");
            Assert.That(game.State, Is.EqualTo(GameState.Cutscene));
            Assert.That(game.save.Save(), Is.True);
            Assert.That(SaveSystem.TryRead(SavePath, out var interrupted), Is.True);
            Assert.That(interrupted.narrative.HasSignal("prologue.first-call"), Is.False);
            PlayerPrefs.SetInt("Game.ResumeRequested", 1);
            yield return SceneManager.LoadSceneAsync("Chapter1Office");
            yield return Ready();
            game = GameSession.Instance; director = game.narrative; flow = Object.FindAnyObjectByType<PrologueFlow>();
            Assert.That(director.HasSequence("prologue.call-one"), Is.False);
            flow.firstCallDelay = .05f; flow.secondCallDelay = .05f;
            yield return Until(() => flow.Ringing);
            Assert.That(flow.Answer(), Is.True);
            var observed = new HashSet<string>();
            float deadline = Time.realtimeSinceStartup + 48;
            while (!director.HasSequence("prologue.call-one") && Time.realtimeSinceStartup < deadline)
            { if (game.hud && game.hud.subtitle) observed.Add(game.hud.subtitle.text); yield return null; }
            Assert.That(director.HasSequence("prologue.call-one"), Is.True);
            foreach (var line in director.book.Sequence("prologue.call-one").lines)
                if (!string.IsNullOrEmpty(line.text)) Assert.That(observed.Contains(string.IsNullOrEmpty(line.speaker) ? line.text : line.speaker + ": " + line.text), Is.True, line.text);
            yield return Until(() => flow.Ringing);
            Assert.That(flow.AvailableSequence, Is.EqualTo("prologue.call-two"));
            Assert.That(flow.Answer(), Is.True);
            observed.Clear(); deadline = Time.realtimeSinceStartup + 12;
            while (!director.HasSequence("prologue.call-two") && Time.realtimeSinceStartup < deadline)
            { if (game.hud && game.hud.subtitle) observed.Add(game.hud.subtitle.text); yield return null; }
            Assert.That(director.HasSequence("prologue.call-two"), Is.True);
            Assert.That(observed.Contains("Voz infantil: João..."), Is.True);
            yield return null;
            Assert.That(game.save.Save(), Is.True);
            Assert.That(SaveSystem.TryRead(SavePath, out var completed), Is.True);
            Assert.That(completed.narrative.HasSequence("prologue.call-one"), Is.True);
            Assert.That(completed.narrative.HasSequence("prologue.call-two"), Is.True);
            var equipment = Object.FindAnyObjectByType<StoryEquipment>();
            Assert.That(equipment, Is.Not.Null);
            Assert.That(equipment.containingDrawer, Is.Not.Null);
            Assert.That(equipment.transform.parent, Is.EqualTo(equipment.containingDrawer.drawer));
            Assert.That(equipment.CanInteract(game.interaction), Is.False, "Closed drawer must hide equipment interaction.");
            equipment.containingDrawer.Interact(game.interaction);
            Assert.That(equipment.CanInteract(game.interaction), Is.False, "Opening starts at zero actual travel.");
            yield return Until(() => equipment.CanInteract(game.interaction));
            yield return Until(() => equipment.containingDrawer.OpenFraction > .99f);
            Physics.SyncTransforms();
            Vector3 eye = equipment.transform.position + new Vector3(0, .9f, .75f);
            Assert.That(Physics.Raycast(eye, (equipment.transform.position - eye).normalized, out var itemHit, 2.6f), Is.True);
            Assert.That(itemHit.collider.GetComponentInParent<StoryEquipment>(), Is.EqualTo(equipment), "Opened drawer contents must be reachable by the interaction ray.");
            equipment.Interact(game.interaction);
            Assert.That(game.interaction.inventory.Contains("office_equipment"), Is.True);
            Assert.That(equipment.gameObject.activeSelf, Is.False);
            PlayerPrefs.SetInt("Game.ResumeRequested", 0); PlayerPrefs.SetInt("Game.StoryCarryRequested", 0);
            yield return SceneManager.LoadSceneAsync("RoadJourney");
            yield return Ready();
            game = GameSession.Instance;
            Assert.That(game.narrative.Progress.chapterId, Is.EqualTo("road"));
            game.Kill();
            Assert.That(game.State, Is.EqualTo(GameState.GameOver), "Generic Kill still owns its separate GameOver path.");
            game.RestartCheckpoint();
            yield return Until(() => GameSession.Instance && GameSession.Instance != game);
            yield return Ready();
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("RoadJourney"));
            Assert.That(GameSession.Instance.narrative.Progress.chapterId, Is.EqualTo("road"));
            Assert.That(GameSession.Instance.State, Is.EqualTo(GameState.Playing));
        }
    }
}
