// Verifica SetUp, BlockedDoorHasNoMovementSoundOrNoiseUntilItCanMove, ClosingLatchWaitsForTheLeafAndRestoredDoorAndDrawerStaySilent, TearDown.

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    public sealed class AudioPolishPlayTests
    {
        GameObject fixturesRoot;
        AudioManager audio;
        SoundSet movement, latch;
        AudioClip movementClip, latchClip;
        readonly List<Object> assets = new List<Object>();
        float originalScale, originalVolume;
        bool originalPause;
        GameObject observedDoor;
        int doorNoise;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalScale = Time.timeScale;
            originalPause = AudioListener.pause;
            originalVolume = AudioListener.volume;
            Time.timeScale = 1;
            AudioListener.pause = false;
            fixturesRoot = new GameObject("Isolated audio regression fixtures");
            fixturesRoot.transform.position = new Vector3(1000, 1000, 1000);
            audio = AudioManager.Instance;
            if (!audio)
                audio = Child("Temporary audio pool").AddComponent<AudioManager>();
            bool listenerExists = false;
            foreach (var listener in Object.FindObjectsByType<AudioListener>())
                listenerExists |= listener.isActiveAndEnabled;
            if (!listenerExists) Child("Temporary listener").AddComponent<AudioListener>();
            movement = Sound("Movement fixture", 3, out movementClip);
            latch = Sound("Latch fixture", .5f, out latchClip);
            NoiseSystem.Emitted += ObserveNoise;
            yield return null;
        }

        GameObject Child(string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(fixturesRoot.transform, false);
            return obj;
        }

        SoundSet Sound(string name, float seconds, out AudioClip clip)
        {
            // Silence is intentional: fixture clips exercise scheduling without adding game assets.
            clip = AudioClip.Create(name, Mathf.CeilToInt(seconds * 22050), 1, 22050, false);
            var set = ScriptableObject.CreateInstance<SoundSet>();
            set.clips = new[] { clip };
            set.pitch = Vector2.one;
            set.volume = Vector2.one;
            assets.Add(set);
            assets.Add(clip);
            return set;
        }

        void ObserveNoise(NoiseEvent noise)
        {
            if (noise.source == observedDoor && noise.kind == NoiseKind.Door) doorNoise++;
        }

        InteractiveDoor Door()
        {
            var hinge = Child("Door hinge");
            var leaf = new GameObject("Leaf");
            leaf.transform.SetParent(hinge.transform, false);
            leaf.transform.localPosition = new Vector3(.5f, 1, 0);
            var collider = leaf.AddComponent<BoxCollider>();
            collider.size = new Vector3(1, 2, .1f);
            var door = hinge.AddComponent<InteractiveDoor>();
            door.hinge = hinge.transform;
            door.leafCollider = collider;
            door.openAngle = 90;
            door.speed = 45;
            door.openSound = movement;
            door.closeSound = movement;
            door.latchSound = latch;
            observedDoor = door.gameObject;
            doorNoise = 0;
            return door;
        }

        int Playing(AudioClip clip)
        {
            int count = 0;
            foreach (var source in audio.GetComponentsInChildren<AudioSource>(true))
                if (source.clip == clip && source.isPlaying) count++;
            return count;
        }

        void StopFixtureVoices()
        {
            if (!audio) return;
            foreach (var source in audio.GetComponentsInChildren<AudioSource>(true))
                if (source.clip == movementClip || source.clip == latchClip)
                {
                    source.Stop();
                    source.clip = null;
                }
        }

        [UnityTest]
        public IEnumerator BlockedDoorHasNoMovementSoundOrNoiseUntilItCanMove()
        {
            var door = Door();
            var blocker = Child("Player-sized obstruction");
            blocker.layer = 2;
            blocker.transform.localPosition = new Vector3(.5f, 1, 0);
            var collision = blocker.AddComponent<BoxCollider>();
            collision.size = new Vector3(2, 3, 2);
            door.playerMask = 1 << 2;
            Physics.SyncTransforms();
            door.Interact(null);
            yield return new WaitForSeconds(.12f);
            Assert.Less(Quaternion.Angle(door.hinge.localRotation, Quaternion.identity), .01f);
            Assert.AreEqual(0, doorNoise, "An obstructed leaf must not generate movement noise.");
            Assert.AreEqual(0, Playing(movementClip), "A click on an obstructed door must not start its creak.");
            Assert.AreEqual(0, Playing(latchClip));

            collision.enabled = false;
            Physics.SyncTransforms();
            // The test enumerator can resume before this MonoBehaviour's next Update.
            // Also avoid Quaternion.Angle rounding very small first-frame rotations to zero.
            float motionDeadline = Time.realtimeSinceStartup + 1;
            while (Quaternion.Angle(door.hinge.localRotation, Quaternion.identity) < .1f && Time.realtimeSinceStartup < motionDeadline)
                yield return null;
            Assert.Greater(Quaternion.Angle(door.hinge.localRotation, Quaternion.identity), .1f, "The released leaf must start moving within one second.");
            Assert.AreEqual(1, doorNoise);
            Assert.AreEqual(1, Playing(movementClip));
            Assert.AreEqual(0, Playing(latchClip));
        }

        [UnityTest]
        public IEnumerator ClosingLatchWaitsForTheLeafAndRestoredDoorAndDrawerStaySilent()
        {
            var door = Door();
            door.RestoreState("{\"open\":true,\"locked\":false}");
            Assert.AreEqual(0, Playing(movementClip));
            Assert.AreEqual(0, Playing(latchClip));
            door.Interact(null);
            yield return null;
            Assert.Greater(Quaternion.Angle(door.hinge.localRotation, Quaternion.identity), .01f);
            Assert.AreEqual(1, Playing(movementClip));
            Assert.AreEqual(0, Playing(latchClip), "The latch must wait until the leaf reaches the frame.");

            float deadline = Time.realtimeSinceStartup + 4;
            while (Playing(latchClip) == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.Less(Quaternion.Angle(door.hinge.localRotation, Quaternion.identity), .01f);
            Assert.AreEqual(1, Playing(latchClip));
            Assert.AreEqual(1, doorNoise);
            StopFixtureVoices();

            var drawer = Child("Drawer").AddComponent<InteractiveDrawer>();
            drawer.drawer = drawer.transform;
            drawer.localTravel = new Vector3(0, 0, .4f);
            drawer.openSound = movement;
            drawer.closeSound = movement;
            drawer.endSound = latch;
            drawer.RestoreState("{\"open\":true}");
            door.RestoreState("{\"open\":true,\"locked\":false}");
            yield return null;
            Assert.IsTrue(door.IsOpen);
            Assert.That(drawer.drawer.localPosition.z, Is.EqualTo(.4f).Within(.001f));
            drawer.RestoreState("{\"open\":false}");
            door.RestoreState("{\"open\":false,\"locked\":true}");
            yield return null;
            Assert.IsTrue(door.locked);
            Assert.IsFalse(door.IsOpen);
            Assert.That(drawer.drawer.localPosition.z, Is.EqualTo(0).Within(.001f));
            Assert.AreEqual(1, doorNoise, "Loading a save must not create interaction noise.");
            Assert.AreEqual(0, Playing(movementClip));
            Assert.AreEqual(0, Playing(latchClip));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NoiseSystem.Emitted -= ObserveNoise;
            StopFixtureVoices();
            if (fixturesRoot) Object.Destroy(fixturesRoot);
            foreach (var asset in assets) if (asset) Object.Destroy(asset);
            assets.Clear();
            yield return null;
            Time.timeScale = originalScale;
            AudioListener.pause = originalPause;
            AudioListener.volume = originalVolume;
        }
    }
}
