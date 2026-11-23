// Verifica SetUp, OnlyNaturalCompletionCountsAndListenerOrSourcePauseCannotComplete, StopCloseAndCancelDoNotCountAsHeard, RecorderShowsBothTapeControlsButOnlyCollectedTapesCanPlay, TearDown.

using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("GameplayPolish")]
    public sealed class TapeRecordingCompletionTests
    {
        GameObject hidden, audible;
        InspectionManager inspector;
        AudioSource source;
        AudioClip clip;
        SoundSet recording;
        InspectionItemData item;
        InspectionItemData recorder, directorTape;
        SchoolInspectionCatalog catalog;
        GameObject panel;
        int completed, started;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        void Poll() => typeof(InspectionManager).GetMethod("AdvanceRecording", Private).Invoke(inspector, null);
        void Set(string field, object value) => typeof(InspectionManager).GetField(field, Private).SetValue(inspector, value);

        [SetUp]
        public void SetUp()
        {
            completed = started = 0;
            hidden = new GameObject("Inactive tape inspector fixture"); hidden.SetActive(false);
            var game = hidden.AddComponent<GameSession>(); game.enabled = false;
            typeof(GameSession).GetProperty("State").SetValue(game, GameState.Inspecting);
            inspector = hidden.AddComponent<InspectionManager>(); inspector.enabled = false; inspector.game = game;
            audible = new GameObject("Real tape playback fixture");
            if (!UnityEngine.Object.FindAnyObjectByType<AudioListener>()) audible.AddComponent<AudioListener>();
            source = audible.AddComponent<AudioSource>(); source.playOnAwake = false;
            clip = AudioClip.Create("Tape completion fixture", 17640, 1, 44100, false);
            recording = ScriptableObject.CreateInstance<SoundSet>(); recording.clips = new[] { clip };
            recording.pitch = recording.volume = Vector2.one;
            item = ScriptableObject.CreateInstance<InspectionItemData>(); item.itemId = "hospicio_child_tape"; item.canPlay = true; item.recording = recording;
            typeof(InspectionManager).GetProperty("IsOpen").SetValue(inspector, true);
            typeof(InspectionManager).GetProperty("CurrentItem").SetValue(inspector, item);
            Set("sound", source); Set("controls", new InputActionMap("Tape fixture"));
            inspector.RecordingStarted += id => started++;
            inspector.RecordingCompleted += id => { Assert.That(id, Is.EqualTo(item.itemId)); completed++; };
            Time.timeScale = 1; AudioListener.pause = false;
        }

        IEnumerator PollFor(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) { Poll(); yield return null; }
        }

        [UnityTest]
        public IEnumerator OnlyNaturalCompletionCountsAndListenerOrSourcePauseCannotComplete()
        {
            inspector.PlayRecording();
            Assert.That(started, Is.EqualTo(1)); Assert.That(completed, Is.Zero);
            yield return PollFor(.08f);
            AudioListener.pause = true;
            yield return PollFor(.5f);
            Assert.That(completed, Is.Zero, "Pausing the listener must not mark the tape as heard.");
            AudioListener.pause = false;
            yield return PollFor(.04f);
            source.Pause();
            yield return PollFor(.5f);
            Assert.That(completed, Is.Zero, "Pausing the source must not mark the tape as heard.");
            source.UnPause();
            yield return PollFor(.6f);
            Assert.That(completed, Is.EqualTo(1));
            yield return PollFor(.1f);
            Assert.That(completed, Is.EqualTo(1), "Completion fires once per successful playback.");
        }

        [UnityTest]
        public IEnumerator StopCloseAndCancelDoNotCountAsHeard()
        {
            inspector.PlayRecording(); yield return PollFor(.08f);
            inspector.PlayRecording(); yield return PollFor(.5f);
            Assert.That(completed, Is.Zero, "Stopping with OUVIR must not complete the recording.");
            inspector.PlayRecording(); yield return PollFor(.08f);
            inspector.Close(); yield return PollFor(.5f);
            Assert.That(completed, Is.Zero, "Closing inspection must not complete the recording.");
            Set("closing", false);
            inspector.PlayRecording(); yield return PollFor(.08f);
            inspector.Cancel(false); yield return PollFor(.5f);
            Assert.That(completed, Is.Zero, "Blackout/cancel must not complete the recording.");
        }

        [UnityTest]
        public IEnumerator RecorderShowsBothTapeControlsButOnlyCollectedTapesCanPlay()
        {
            var game = inspector.game;
            game.interaction = hidden.AddComponent<PlayerInteraction>(); game.interaction.enabled = false;
            game.interaction.inventory = hidden.AddComponent<Inventory>();
            recorder = ScriptableObject.CreateInstance<InspectionItemData>();
            recorder.itemId = "hospicio_recorder"; recorder.title = "Gravador antigo";
            recorder.recording = recording; // Legacy generic sound must not masquerade as an inserted tape.
            directorTape = ScriptableObject.CreateInstance<InspectionItemData>();
            directorTape.itemId = "hospicio_director_tape"; directorTape.canPlay = true; directorTape.recording = recording;
            catalog = ScriptableObject.CreateInstance<SchoolInspectionCatalog>(); catalog.items = new[] { recorder, item, directorTape }; inspector.catalog = catalog;
            typeof(InspectionManager).GetProperty("CurrentItem").SetValue(inspector, recorder);
            panel = new GameObject("Recorder controls fixture", typeof(RectTransform));
            var ui = panel.AddComponent<InspectionUI>(); ui.Initialize(inspector, null);
            ui.Present(recorder, null, 1, false, false, 1);
            UnityEngine.UI.Button first = null, second = null;
            foreach (var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            { if (button.name == "PlayRecording") first = button; if (button.name == "PlaySecondTape") second = button; }
            Assert.That(first && first.gameObject.activeSelf, Is.True);
            Assert.That(second && second.gameObject.activeSelf, Is.True);
            Assert.That(first.interactable || second.interactable, Is.False);
            inspector.PlayRecording();
            Assert.That(started, Is.Zero); Assert.That(source.clip, Is.Null);
            game.interaction.inventory.Add(new InventoryItem { id = item.itemId });
            ui.Present(recorder, null, 1, false, false, 1);
            Assert.That(first.interactable, Is.True); Assert.That(second.interactable, Is.False);
            first.onClick.Invoke();
            Assert.That(started, Is.EqualTo(1));
            Assert.That(inspector.PlayingRecordingId, Is.EqualTo(item.itemId));
            Assert.That(source.GetComponent<AudioLowPassFilter>().cutoffFrequency, Is.EqualTo(5200));
            yield return PollFor(.6f);
            Assert.That(completed, Is.EqualTo(1), "Playing from the recorder must complete the tape's story ID, not the recorder ID.");
            game.interaction.inventory.Add(new InventoryItem { id = directorTape.itemId });
            ui.Present(recorder, null, 1, false, false, 1);
            Assert.That(second.interactable, Is.True);
            second.onClick.Invoke();
            Assert.That(inspector.PlayingRecordingId, Is.EqualTo(directorTape.itemId));
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1; AudioListener.pause = false;
            if (inspector) inspector.Cancel(false);
            if (panel) UnityEngine.Object.DestroyImmediate(panel);
            if (audible) UnityEngine.Object.DestroyImmediate(audible);
            if (hidden) UnityEngine.Object.DestroyImmediate(hidden);
            if (item) UnityEngine.Object.DestroyImmediate(item);
            if (recording) UnityEngine.Object.DestroyImmediate(recording);
            if (clip) UnityEngine.Object.DestroyImmediate(clip);
            if (recorder) UnityEngine.Object.DestroyImmediate(recorder);
            if (directorTape) UnityEngine.Object.DestroyImmediate(directorTape);
            if (catalog) UnityEngine.Object.DestroyImmediate(catalog);
        }
    }
}
