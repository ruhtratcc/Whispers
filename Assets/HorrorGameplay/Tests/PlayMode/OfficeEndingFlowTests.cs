// Verifica SetUp, DeskRingAndBlackEndingFollowExitAndFreezeDuringPause, RingCheckpointSkipsWakeAndPrologueDoesNotActivateTheEnding, TearDown.

using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("SimplifiedStory")]
    public sealed class OfficeEndingFlowTests
    {
        Scene scene;
        GameObject root;
        StoryBook book;
        GameSession game;
        NarrativeDirector director;
        OfficeEndingFlow flow;
        Camera camera;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            scene = SceneManager.CreateScene("Office ending fixture " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(scene);
            foreach (var existing in UnityEngine.Object.FindObjectsByType<GameSession>())
                UnityEngine.Object.Destroy(existing.gameObject);
            yield return null;
            root = new GameObject("Office ending isolated fixture");
            root.SetActive(false);
            game = root.AddComponent<GameSession>();
            game.enabled = false;
            director = root.AddComponent<NarrativeDirector>();
            director.game = game;
            director.startingChapterId = "last-exit";
            director.autoLoadNextChapter = false;
            game.narrative = director;
            book = ScriptableObject.CreateInstance<StoryBook>();
            book.chapters = new[] {
                new StoryChapterDefinition { id = "prologue", milestones = new[] { new StoryMilestoneDefinition { id = "prologue.first-call" } } },
                new StoryChapterDefinition { id = "last-exit", milestones = new[] {
                    new StoryMilestoneDefinition { id = "last-exit.outside" },
                    new StoryMilestoneDefinition { id = "last-exit.awake" },
                    new StoryMilestoneDefinition { id = "last-exit.phone-ringing" },
                    new StoryMilestoneDefinition { id = "last-exit.ending" }
                } }
            };
            book.sequences = new[] {
                Sequence("simplified.office-awake", "last-exit.awake"),
                Sequence("simplified.office-ring", "last-exit.phone-ringing"),
                Sequence("ending", "last-exit.ending")
            };
            director.book = book;
            var body = new GameObject("Player");
            body.transform.SetParent(root.transform);
            body.AddComponent<CharacterController>();
            var motor = body.AddComponent<FirstPersonMotor>();
            motor.enabled = false;
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform);
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            motor.view = cameraObject.AddComponent<FirstPersonCamera>();
            motor.view.motor = motor;
            motor.view.playerCamera = camera;
            game.player = motor;
            flow = root.AddComponent<OfficeEndingFlow>();
            flow.director = director;
            flow.player = motor;
            flow.telephone = root.AddComponent<StoryTelephone>();
            var pose = new GameObject("Desk eye pose");
            pose.transform.SetParent(root.transform);
            pose.transform.SetPositionAndRotation(new Vector3(1, 1.3f, 2), Quaternion.Euler(15, 30, 0));
            flow.seatedPose = pose.transform;
            root.SetActive(true);
            yield return null;
        }

        static StorySequenceDefinition Sequence(string id, string signal) => new StorySequenceDefinition {
            id = id, completionSignal = signal, lockPlayer = true,
            lines = new[] { new StoryLine { text = id, seconds = .12f } }
        };

        void Resume(string chapter, params string[] milestones)
        {
            director.RestoreProgress(new NarrativeProgress { chapterId = chapter, milestones = new List<string>(milestones) });
            director.InitializeAfterSession(true);
        }

        static IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 4;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True);
        }

        [UnityTest]
        public IEnumerator DeskRingAndBlackEndingFollowExitAndFreezeDuringPause()
        {
            int wakeCalls = 0;
            director.SequenceStarted += id => { if (id == "simplified.office-awake") wakeCalls++; };
            Resume("last-exit", "last-exit.outside");
            Assert.That(flow.IsEndingActive, Is.True);
            Assert.That(flow.player.view.HasOverride, Is.True);
            yield return Until(() => director.ActiveSequenceId == "simplified.office-ring");
            Assert.That(flow.telephone.IsRinging, Is.True);
            game.Pause();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(director.HasSignal("last-exit.phone-ringing"), Is.False);
            game.Resume();
            yield return Until(() => director.Progress.endingSeen);
            Assert.That(wakeCalls, Is.EqualTo(1));
            Assert.That(director.HasSignal("last-exit.ending"), Is.True);
            Assert.That(flow.telephone.IsRinging, Is.False);
            Assert.That(camera.cullingMask, Is.Zero);
            Assert.That(camera.backgroundColor, Is.EqualTo(Color.black));
        }

        [UnityTest]
        public IEnumerator RingCheckpointSkipsWakeAndPrologueDoesNotActivateTheEnding()
        {
            int wakeCalls = 0;
            director.SequenceStarted += id => { if (id == "simplified.office-awake") wakeCalls++; };
            Resume("prologue");
            yield return null;
            Assert.That(flow.IsEndingActive, Is.False);
            Assert.That(flow.telephone.IsRinging, Is.False);
            int mask = camera.cullingMask;
            Resume("last-exit", "last-exit.outside", "last-exit.awake");
            yield return Until(() => director.Progress.endingSeen);
            Assert.That(wakeCalls, Is.Zero, "Returning to an office checkpoint must not replay the wake-up.");
            flow.enabled = false;
            Assert.That(camera.cullingMask, Is.EqualTo(mask));
            Assert.That(flow.player.view.HasOverride, Is.False);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            if (root) UnityEngine.Object.Destroy(root);
            if (book) UnityEngine.Object.Destroy(book);
            yield return null;
            var cleanup = SceneManager.GetSceneByName("Office ending cleanup");
            if (!cleanup.IsValid() || !cleanup.isLoaded) cleanup = SceneManager.CreateScene("Office ending cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
