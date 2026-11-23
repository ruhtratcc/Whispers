// Verifica SetUp, TearDown, ScareRollRecoversAndSmallLandingDoesNotEraseIt, PauseFreezesScareAndSignal, CutscenePoseRemainsUsableDuringScareAndRecovers.

using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("GameplayCompletion")]
    public sealed class CameraScarePlayTests
    {
        GameObject root;
        FirstPersonCamera view;
        float originalScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalScale = Time.timeScale;
            Time.timeScale = 1;
            root = new GameObject("Isolated scare camera fixture");
            root.transform.position = new Vector3(1000, 1000, 1000);
            var motor = root.AddComponent<FirstPersonMotor>();
            motor.enabled = false;
            var cameraObject = new GameObject("Fixture camera");
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            view = cameraObject.AddComponent<FirstPersonCamera>();
            view.motor = motor;
            view.playerCamera = camera;
            view.bobEnabled = view.swayEnabled = false;
            motor.view = view;
            view.Snap();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = originalScale;
            if (root) Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ScareRollRecoversAndSmallLandingDoesNotEraseIt()
        {
            view.Shake(.075f, .7f);
            float before = ReadFloat("shakeStrength");
            float fear = view.Fear01;
            view.Shake(.01f, .15f);
            Assert.That(ReadFloat("shakeStrength"), Is.GreaterThanOrEqualTo(before * .99f));
            Assert.That(view.Fear01, Is.EqualTo(fear).Within(.0001f));
            yield return new WaitForSeconds(1.6f);
            Assert.That(view.Fear01, Is.LessThan(.003f));
            Assert.That(Quaternion.Angle(view.transform.rotation, root.transform.rotation), Is.LessThan(.2f));
        }

        [UnityTest]
        public IEnumerator PauseFreezesScareAndSignal()
        {
            var effect = view.gameObject.AddComponent<VintageCameraEffect>();
            effect.prologueSceneName = "A Different Fixture Scene";
            effect.ConfigureForChapter(1);
            view.Shake(.08f, .8f);
            yield return null;
            Time.timeScale = 0;
            var rotation = view.transform.rotation;
            float fear = view.Fear01, clock = effect.SignalTime;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(Quaternion.Angle(rotation, view.transform.rotation), Is.LessThan(.001f));
            Assert.That(view.Fear01, Is.EqualTo(fear).Within(.000001f));
            Assert.That(effect.SignalTime, Is.EqualTo(clock));
            Time.timeScale = 1;
        }

        [UnityTest]
        public IEnumerator CutscenePoseRemainsUsableDuringScareAndRecovers()
        {
            var pose = new GameObject("Fixture cutscene pose");
            pose.transform.SetParent(root.transform, false);
            pose.transform.localPosition = new Vector3(.2f, 1.4f, .3f);
            pose.transform.localRotation = Quaternion.Euler(8, 35, 0);
            view.OverridePose(pose.transform);
            view.Shake(.09f, .6f);
            Assert.True(view.HasOverride);
            Assert.That(view.Fear01, Is.GreaterThan(.1f));
            yield return new WaitForSeconds(1.6f);
            Assert.That(Vector3.Distance(view.transform.position, pose.transform.position), Is.LessThan(.015f));
            Assert.That(Quaternion.Angle(view.transform.rotation, pose.transform.rotation), Is.LessThan(.2f));
            view.ClearOverride();
            view.Snap();
            Assert.False(view.HasOverride);
            Assert.Zero(view.Fear01);
        }

        float ReadFloat(string field) => (float)typeof(FirstPersonCamera)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
    }
}
