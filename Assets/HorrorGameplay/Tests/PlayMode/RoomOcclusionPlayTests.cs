// Verifica SetUp, SolidWallMufflesAndOpenPathRecoversWithoutChangingGainAndPauseFreezesFilter, TriggersAreIgnoredAndReusedTwoDimensionalVoiceStaysClean, TearDown.

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    public sealed class RoomOcclusionPlayTests
    {
        Scene previousScene, fixtureScene;
        GameObject root;
        AudioManager audio;
        AudioClip clip;
        AudioSource source;
        readonly List<AudioListener> mutedListeners = new List<AudioListener>();
        float previousScale, previousVolume;
        bool previousPause;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousScale = Time.timeScale;
            previousVolume = AudioListener.volume;
            previousPause = AudioListener.pause;
            Time.timeScale = 1;
            AudioListener.pause = false;
            previousScene = SceneManager.GetActiveScene();
            var road = SceneManager.GetSceneByName("RoadJourney");
            if (!road.IsValid() || !road.isLoaded) road = fixtureScene = SceneManager.CreateScene("RoadJourney");
            SceneManager.SetActiveScene(road);
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener.isActiveAndEnabled)
                {
                    mutedListeners.Add(listener);
                    listener.enabled = false;
                }
            root = new GameObject("Room occlusion isolated fixtures");
            root.transform.position = new Vector3(10000, 10000, 10000);
            root.AddComponent<AudioListener>();
            audio = AudioManager.Instance;
            if (!audio)
            {
                var manager = new GameObject("Temporary room audio pool");
                manager.transform.SetParent(root.transform, false);
                audio = manager.AddComponent<AudioManager>();
            }
            clip = AudioClip.Create("Silent acoustic test", 441000, 1, 44100, false);
            yield return null;
        }

        GameObject Wall()
        {
            var wall = new GameObject("Solid wall between emitter and listener");
            wall.transform.SetParent(root.transform, false);
            wall.transform.localPosition = new Vector3(2, 0, 0);
            wall.AddComponent<BoxCollider>().size = new Vector3(.2f, 4, 4);
            Physics.SyncTransforms();
            return wall;
        }

        [UnityTest]
        public IEnumerator SolidWallMufflesAndOpenPathRecoversWithoutChangingGainAndPauseFreezesFilter()
        {
            var wall = Wall();
            source = audio.PlayClip(clip, root.transform.position + Vector3.right * 4, .37f);
            Assert.IsNotNull(source);
            var filter = source.GetComponent<AudioLowPassFilter>();
            Assert.IsNotNull(filter);
            yield return new WaitForSeconds(.65f);
            Assert.IsTrue(filter.enabled);
            Assert.Less(filter.cutoffFrequency, 4000);
            Assert.That(source.volume, Is.EqualTo(.37f).Within(.001f));

            AudioListener.pause = true;
            float pausedCutoff = filter.cutoffFrequency;
            wall.SetActive(false);
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(filter.cutoffFrequency, Is.EqualTo(pausedCutoff).Within(.01f));
            AudioListener.pause = false;
            yield return new WaitForSeconds(1);
            Assert.IsFalse(filter.enabled, "An open path must remove the pooled low-pass effect.");
            Assert.That(source.volume, Is.EqualTo(.37f).Within(.001f));
        }

        [UnityTest]
        public IEnumerator TriggersAreIgnoredAndReusedTwoDimensionalVoiceStaysClean()
        {
            var wall = Wall().GetComponent<BoxCollider>();
            wall.isTrigger = true;
            Physics.SyncTransforms();
            source = audio.PlayClip(clip, root.transform.position + Vector3.right * 4, .42f);
            Assert.IsNotNull(source);
            var filter = source.GetComponent<AudioLowPassFilter>();
            yield return new WaitForSeconds(.4f);
            Assert.IsFalse(filter.enabled, "A gameplay trigger is acoustically transparent.");

            wall.isTrigger = false;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.65f);
            Assert.IsTrue(filter.enabled);
            Assert.Less(filter.cutoffFrequency, 4000);
            // Reserve other free voices so this checks reuse even in a loaded gameplay scene.
            for (int i = 0; i < 64; i++)
                if (!audio.PlayClip(clip, root.transform.position, .01f, 1, AudioChannel.Voice, false)) break;
            source.Stop();
            var reused = audio.PlayClip(clip, source.transform.position, .61f, 1, AudioChannel.Voice, false);
            Assert.AreSame(source, reused, "The free first pooled voice should be reused.");
            Assert.IsFalse(filter.enabled, "Reuse must clear the old spatial filter immediately.");
            yield return new WaitForSeconds(.4f);
            Assert.IsFalse(filter.enabled, "Two-dimensional dialogue must bypass room occlusion.");
            Assert.That(reused.volume, Is.EqualTo(.61f).Within(.001f));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (audio)
                foreach (var voice in audio.GetComponentsInChildren<AudioSource>(true))
                    if (voice.clip == clip) { voice.Stop(); voice.clip = null; }
            if (root) Object.Destroy(root);
            if (clip) Object.Destroy(clip);
            foreach (var listener in mutedListeners) if (listener) listener.enabled = true;
            mutedListeners.Clear();
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (fixtureScene.IsValid() && fixtureScene.isLoaded) yield return SceneManager.UnloadSceneAsync(fixtureScene);
            Time.timeScale = previousScale;
            AudioListener.pause = previousPause;
            AudioListener.volume = previousVolume;
        }
    }
}
