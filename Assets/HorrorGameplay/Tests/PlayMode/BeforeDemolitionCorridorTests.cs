// Verifica .

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("BeforeDemolitionCorridor")]
    public sealed class BeforeDemolitionCorridorTests
    {
        BeforeDemolitionChapterFixture f;
        BeforeDemolitionTwoVoicesFlow flow;
        Keyboard keyboard, oldKeyboard;
        InputSettings.BackgroundBehavior oldBackground;
        InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        bool oldRunInBackground, inputGuard;
        int blows;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldKeyboard = Keyboard.current; keyboard = InputSystem.AddDevice<Keyboard>();
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            oldRunInBackground = Application.runInBackground; inputGuard = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground = true;
            if(!keyboard.enabled)InputSystem.EnableDevice(keyboard);
            f = new BeforeDemolitionChapterFixture(BeforeDemolitionTwoVoicesFlow.ChapterId, new[] {
                new StoryMilestoneDefinition { id = "bd.loop.escaped" },
                new StoryMilestoneDefinition { id = "bd.loop.tape" },
                new StoryMilestoneDefinition { id = "bd.loop.mirror" }
            }, true);
            f.book.chapters = new[] {
                new StoryChapterDefinition { id = BeforeDemolitionCampaign.OfficeChapterId,
                    milestones = new[] { new StoryMilestoneDefinition { id = "fixture.office.wait" } } }, f.book.chapters[0]
            };
            flow = f.Child("Two voices").AddComponent<BeforeDemolitionTwoVoicesFlow>();
            flow.game = f.game; flow.whistle = f.whistle;
            flow.loopVolume = f.Volume("Beautiful voice loop", new Vector3(0, 1, 5), new Vector3(2, 3, 2));
            flow.correctRoomVolume = f.Volume("Quiet side room", new Vector3(5, 1, 0), new Vector3(2, 3, 2));
            flow.mirrorApproach = f.Volume("Mirror approach", new Vector3(10, 1, 0), new Vector3(2, 3, 2));
            flow.returnPose = f.Child("Loop return").transform; flow.returnPose.position = Vector3.zero;
            flow.quietAnchor = f.Child("Quiet echo").transform; flow.quietAnchor.position = new Vector3(0, 1.64f, 8);
            flow.voiceAnchor = f.Child("Helena voice").transform; flow.voiceAnchor.position = new Vector3(0, 1.64f, 5);
            flow.sideDoor = f.Child("Side door").AddComponent<InteractiveDoor>();
            flow.mirrorDoor = f.Child("Mirror door").AddComponent<InteractiveDoor>();
            flow.occupiedVisuals = Enumerable.Range(0, 4).Select(i => f.Child("Occupied desk " + i)).ToArray();
            flow.roomLabels = Enumerable.Range(0, 4).Select(i => f.Label("Door label " + i, f.root.transform)).ToArray();
            blows = 0; f.whistle.Blown += () => blows++;
            yield return f.Activate();
            Assert.That(flow.IsActive, Is.True);
        }
        IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator QRequiresTheWhistleAndAnAuthorizedChapter()
        {
            f.inventory.items.RemoveAll(item => item.id == BeforeDemolitionCampaign.WhistleItemId);
            yield return Press(Key.Q); Assert.That(blows, Is.Zero, "No owned whistle means no Q action.");
            f.inventory.Add(new InventoryItem { id = BeforeDemolitionCampaign.WhistleItemId });
            yield return Press(Key.Q); Assert.That(blows, Is.EqualTo(1));
            f.whistle.CancelPending();
            var progress = f.director.CaptureProgress(); progress.chapterId = BeforeDemolitionCampaign.OfficeChapterId;
            f.director.RestoreProgress(progress); f.director.InitializeAfterSession(true); yield return null; yield return null;
            Assert.That(f.whistle.CanUse, Is.False);
            yield return Press(Key.Q); Assert.That(blows, Is.EqualTo(1), "The office cannot use corridor or mirror actions.");
        }

        [UnityTest]
        public IEnumerator ThreeLoopsStayBoundedAndOnlyEnteringTheQuietSideRoomEscapes()
        {
            for (int expected = 1; expected <= 3; expected++)
            {
                yield return new WaitForSeconds(1.3f);
                f.player.Warp(new Vector3(0, 0, 5), 0); Physics.SyncTransforms();
                yield return BeforeDemolitionChapterFixture.Until(() => flow.State.loopCount == expected, "Entering the wrong route adds one loop.");
                Assert.That(Vector3.Distance(f.player.transform.position, flow.returnPose.position), Is.LessThan(.01f));
                Assert.That(flow.occupiedVisuals.Count(item => item.activeSelf), Is.EqualTo(expected));
            }
            Assert.That(flow.roomLabels.All(label => label.text == "João"), Is.True);
            yield return new WaitForSeconds(1.3f);
            f.player.Warp(new Vector3(0, 0, 5), 0); Physics.SyncTransforms(); yield return null; yield return null;
            Assert.That(flow.State.loopCount, Is.EqualTo(3)); Assert.That(flow.Escaped, Is.False);
            Assert.That(Vector3.Distance(f.player.transform.position, new Vector3(0, 0, 5)), Is.LessThan(.01f), "The fourth entry must not keep teleporting the player.");
            f.camera.transform.rotation = Quaternion.LookRotation(flow.quietAnchor.position - f.camera.transform.position);
            f.whistle.CancelPending(); Assert.That(f.whistle.Blow(), Is.True);
            yield return new WaitForSeconds(.65f);
            Assert.That(flow.Escaped, Is.False, "A whistle direction cannot replace actually reaching the side room.");
            Assert.That(f.director.NextSignal, Is.EqualTo("bd.loop.escaped"));
            f.player.Warp(new Vector3(5, 0, 0), 0); Physics.SyncTransforms();
            yield return BeforeDemolitionChapterFixture.Until(() => flow.Escaped, "Physical entry into the quiet room accepts escape.");
            Assert.That(flow.mirrorDoor.locked, Is.True, "The mirror remains gated by the full recording.");
            Assert.That(f.whistle.enableEcho, Is.False);
            Assert.That(f.inventory.Contains(BeforeDemolitionCampaign.WhistleItemId), Is.True);
        }

        [UnityTest]
        public IEnumerator WrongDirectionHasNoEchoAndTheCorrectEchoWaitsThroughPause()
        {
            f.camera.transform.SetPositionAndRotation(new Vector3(0, 1.64f, 0), Quaternion.Euler(0, 180, 0));
            Assert.That(f.whistle.Blow(), Is.True);
            Assert.That(BeforeDemolitionChapterFixture.Private<float>(f.whistle, "echoDelay"), Is.LessThan(0));
            yield return new WaitForSeconds(.65f);
            Assert.That(BeforeDemolitionChapterFixture.Private<AudioSource>(f.whistle, "echo"), Is.Null);
            f.whistle.CancelPending(); f.camera.transform.rotation = Quaternion.identity;
            Assert.That(f.whistle.Blow(), Is.True);
            Assert.That(BeforeDemolitionChapterFixture.Private<float>(f.whistle, "echoDelay"), Is.GreaterThan(.4f));
            Assert.That(BeforeDemolitionChapterFixture.Private<AudioSource>(f.whistle, "echo"), Is.Null);
            yield return new WaitForSeconds(.15f);
            f.game.Pause(); float remaining = BeforeDemolitionChapterFixture.Private<float>(f.whistle, "echoDelay");
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(BeforeDemolitionChapterFixture.Private<float>(f.whistle, "echoDelay"), Is.EqualTo(remaining).Within(.0001f));
            Assert.That(BeforeDemolitionChapterFixture.Private<AudioSource>(f.whistle, "echo"), Is.Null);
            f.game.Resume();
            yield return BeforeDemolitionChapterFixture.Until(() => BeforeDemolitionChapterFixture.Private<AudioSource>(f.whistle, "echo") != null, "The correct echo arrives after its remaining delay.");
            var echo = BeforeDemolitionChapterFixture.Private<AudioSource>(f.whistle, "echo");
            Assert.That(echo.clip, Is.Not.Null); Assert.That(echo.spatialBlend, Is.EqualTo(1));
            Assert.That(Vector3.Distance(echo.transform.position, flow.quietAnchor.position), Is.LessThan(.01f));
        }

        [UnityTest]
        public IEnumerator SerializedLoopCountRestoresItsDesksAndRejectsImpossibleCounts()
        {
            flow.RestoreState("{\"version\":1,\"loopCount\":3}");
            string json = flow.CaptureState();
            Assert.That(BeforeDemolitionTwoVoicesFlow.ValidState(json), Is.True);
            flow.RestoreState("{\"version\":1,\"loopCount\":0}"); Assert.That(flow.State.loopCount, Is.Zero);
            flow.RestoreState(json); yield return null;
            Assert.That(flow.State.loopCount, Is.EqualTo(3));
            Assert.That(flow.occupiedVisuals.Count(item => item.activeSelf), Is.EqualTo(3));
            Assert.That(flow.roomLabels.All(label => label.text == "João"), Is.True);
            foreach (string invalid in new[] { "{}", "{\"version\":1,\"loopCount\":-1}", "{\"version\":1,\"loopCount\":4}" })
            {
                Assert.That(BeforeDemolitionTwoVoicesFlow.ValidState(invalid), Is.False);
                Assert.Throws<ArgumentException>(() => flow.RestoreState(invalid));
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (keyboard != null && keyboard.added) { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; }
            if (f != null) yield return f.Dispose(); f = null;
            if (inputGuard)
            {
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (oldKeyboard != null && oldKeyboard.added) oldKeyboard.MakeCurrent();
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
                Application.runInBackground = oldRunInBackground; inputGuard = false;
            }
        }
    }
}
#endif
