// Verifica SetUp, TearDown, FullLampDoesNotConsumeTheReplacementAndAnEmptyLampTurnsOnAfterTheSwap, NearbyBatteriesAllowSlightlyImpreciseAimButNeverThroughAWall.

using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HorrorGame.Tests
{
    [Category("GameplayCompletion")]
    [Category("GameplayPolish")]
    public sealed class BatteryInteractionTests
    {
        GameObject root;
        FirstPersonMotor motor;
        FlashlightController flashlight;
        PlayerInteraction interaction;
        CollectibleItem battery;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Battery interaction fixture");
            motor = root.AddComponent<FirstPersonMotor>(); motor.enabled = false;
            motor.GetComponent<CharacterController>().enabled = false;
            flashlight = root.AddComponent<FlashlightController>();
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false); camera.enabled = false;
            interaction = root.AddComponent<PlayerInteraction>(); interaction.enabled = false;
            interaction.motor = motor; interaction.view = camera;
            var item = new GameObject("Replacement batteries"); item.transform.SetParent(root.transform, false);
            item.transform.position = new Vector3(.15f, 0, 2);
            item.AddComponent<BoxCollider>().size = new Vector3(.04f, .1f, .04f);
            battery = item.AddComponent<CollectibleItem>(); battery.kind = CollectibleKind.Battery; battery.batterySeconds = 90;
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void FullLampDoesNotConsumeTheReplacementAndAnEmptyLampTurnsOnAfterTheSwap()
        {
            flashlight.Restore(flashlight.capacitySeconds, false);
            battery.Interact(interaction);
            Assert.That(battery.gameObject.activeSelf, Is.True);
            flashlight.enabled = false;
            battery.Interact(interaction);
            Assert.That(battery.gameObject.activeSelf, Is.True);
            flashlight.enabled = true;
            StringAssert.Contains("carregada", battery.Prompt(interaction));
            flashlight.Restore(0, false);
            StringAssert.Contains("Trocar pilhas", battery.Prompt(interaction));
            battery.Interact(interaction);
            Assert.That(flashlight.BatterySeconds, Is.EqualTo(90));
            Assert.That(flashlight.IsOn, Is.True);
            Assert.That(battery.gameObject.activeSelf, Is.False);
            StringAssert.Contains("\"collected\":true", battery.CaptureState());
            battery.RestoreState("{\"collected\":false}");
            Assert.That(battery.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void NearbyBatteriesAllowSlightlyImpreciseAimButNeverThroughAWall()
        {
            RefreshTarget();
            Assert.That(interaction.Target, Is.SameAs(battery));
            StringAssert.Contains("[E]", interaction.CurrentPrompt);
            var wall = new GameObject("Opaque wall"); wall.transform.SetParent(root.transform, false);
            wall.transform.position = new Vector3(.075f, 0, 1);
            wall.AddComponent<BoxCollider>().size = new Vector3(1, 2, .1f);
            Physics.SyncTransforms();
            RefreshTarget();
            Assert.That(interaction.Target, Is.Null);
        }

        void RefreshTarget() => typeof(PlayerInteraction).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(interaction, null);
    }
}
