// Verifica AmbientClockDoesNotTickOutsideSchool, PauseFreezesSchoolClockAndSingleFaultRestoresFixture.

using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorGame.Tests
{
    [Category("SchoolOpening")]
    public sealed class SchoolAtmosphereEditTests
    {
        [Test]
        public void AmbientClockDoesNotTickOutsideSchool()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            scene.name = "SchoolAtmosphereOtherFixture";
            var root = new GameObject("Scene-only sound fixture");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var atmosphere = root.AddComponent<SchoolHorrorAtmosphere>();
                atmosphere.AdvanceAtmosphere(60, false);
                Assert.Zero(atmosphere.AmbientClock);
                Assert.Zero(atmosphere.CueCount);
                Assert.False(atmosphere.LightFaultActive);
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void PauseFreezesSchoolClockAndSingleFaultRestoresFixture()
        {
            var scene = SceneManager.GetSceneByName("SchoolCEEP");
            bool created = !scene.IsValid();
            if (created) { scene = EditorSceneManager.NewPreviewScene(); scene.name = "SchoolCEEP"; }
            var root = new GameObject("School soft-light fault fixture");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var atmosphere = root.AddComponent<SchoolHorrorAtmosphere>();
                var origin = new GameObject("Background fixture");
                origin.transform.SetParent(root.transform); origin.transform.position = Vector3.right * 6;
                var light = origin.AddComponent<Light>(); light.intensity = 4;
                atmosphere.listener = root.transform;
                atmosphere.flickerLights = new[] { light }; atmosphere.fixtureBaseIntensity = new[] { 4f };
                atmosphere.AdvanceAtmosphere(2, false);
                atmosphere.AdvanceAtmosphere(60, true);
                Assert.That(atmosphere.AmbientClock, Is.EqualTo(2));
                atmosphere.ReactToEvent(1);
                Assert.True(atmosphere.LightFaultActive);
                atmosphere.AdvanceAtmosphere(.2f, false);
                Assert.That(light.intensity, Is.InRange(2.88f, 4f));
                float dipped = light.intensity;
                atmosphere.AdvanceAtmosphere(60, true);
                Assert.That(light.intensity, Is.EqualTo(dipped));
                atmosphere.AdvanceAtmosphere(1, false);
                Assert.That(light.intensity, Is.EqualTo(4));
                Assert.False(atmosphere.LightFaultActive);
            }
            finally { Object.DestroyImmediate(root); if (created) EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
