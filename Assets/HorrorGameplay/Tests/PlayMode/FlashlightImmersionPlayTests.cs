// Verifica NearbyWallsReduceGlareAndTheDistantBeamRecoversWithoutRecharging.

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("FlashlightImmersion")]
    [Category("GameplayPolish")]
    public sealed class FlashlightImmersionPlayTests
    {
        [UnityTest]
        public IEnumerator NearbyWallsReduceGlareAndTheDistantBeamRecoversWithoutRecharging()
        {
            float previousScale = Time.timeScale;
            var root = new GameObject("Temporary flashlight exposure fixture");
            root.SetActive(false);
            root.transform.position = Vector3.one * 2000;
            var cameraObject = new GameObject("Fixture camera");
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            var rig = new GameObject("Fixture hand");
            rig.transform.SetParent(root.transform, false);
            var lamp = rig.AddComponent<Light>();
            lamp.type = LightType.Spot; lamp.intensity = 90;
            var controller = root.AddComponent<FlashlightController>();
            controller.view = camera; controller.handRig = rig.transform; controller.lamp = lamp;
            controller.capacitySeconds = 100; controller.startsOn = true; controller.nearSurfaceDimming = true;
            var wall = new GameObject("Nearby physical wall");
            wall.transform.position = root.transform.position + Vector3.forward * 2.5f;
            wall.AddComponent<BoxCollider>().size = new Vector3(4, 4, .2f);
            try
            {
                Time.timeScale = 1;
                root.SetActive(true); Physics.SyncTransforms();
                yield return new WaitForSeconds(.3f);
                Assert.That(lamp.intensity, Is.InRange(3f, 24f), "Close inspection must preserve texture detail.");
                float before = controller.BatterySeconds;
                wall.SetActive(false); Physics.SyncTransforms();
                yield return new WaitForSeconds(.7f);
                Assert.That(lamp.intensity, Is.GreaterThan(85), "A clear distant sightline keeps the original beam.");
                Assert.That(controller.BatterySeconds, Is.LessThanOrEqualTo(before), "Exposure does not recharge equipment.");
                Time.timeScale = 0;
                float pausedIntensity = lamp.intensity;
                yield return null; yield return null;
                Assert.That(lamp.intensity, Is.EqualTo(pausedIntensity).Within(.001f));
                Time.timeScale = 1; controller.Toggle();
                Assert.That(lamp.enabled, Is.False);
                Assert.That(controller.IsOn, Is.False);
            }
            finally
            {
                Time.timeScale = previousScale;
                Object.DestroyImmediate(root); Object.DestroyImmediate(wall);
            }
        }
    }
}
