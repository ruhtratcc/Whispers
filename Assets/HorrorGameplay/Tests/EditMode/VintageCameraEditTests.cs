// Verifica OfficeCannotReceiveFilterEvenAfterChapterIndexChanges, NarrativeChapterGateEnablesRoadAndDisablesPrologue, SchoolTapePresetIsExplicitAndCannotBleedIntoAnotherScene, SchoolTrackingFreezesDuringPauseAndUsesBoundedBriefFaults, ExplicitRoadGradeCannotLeakIntoMenuOrChapterZero, HorrorGradeStillExcludesTheOfficeAndFreezesWithGameplay, ExhaustionWarpIsBoundedAndCannotReachTheOfficeOrAnotherChapter, CompositeColourIsOptInAndDoesNotScheduleRepeatedTrackingFaults.

using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;

namespace HorrorGame.Tests
{
    [Category("GameplayCompletion")]
    [Category("GameplayPolish")]
    public sealed class VintageCameraEditTests
    {
        [Test]
        public void OfficeCannotReceiveFilterEvenAfterChapterIndexChanges()
        {
            var root = new GameObject("Office gate fixture");
            try
            {
                var effect = root.AddComponent<VintageCameraEffect>();
                effect.prologueSceneName = root.scene.name;
                effect.ConfigureForChapter(3);
                Assert.False(effect.ChapterAllowed);
                Assert.Zero(effect.EffectiveIntensity);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void NarrativeChapterGateEnablesRoadAndDisablesPrologue()
        {
            var root = new GameObject("Chapter gate fixture");
            try
            {
                var effect = root.AddComponent<VintageCameraEffect>();
                effect.prologueSceneName = "ExplicitlyDifferentPrologueFixture";
                effect.ConfigureForChapter(0);
                Assert.Zero(effect.EffectiveIntensity);
                effect.ConfigureForChapter(1);
                Assert.True(effect.ChapterAllowed);
                Assert.That(effect.EffectiveIntensity, Is.GreaterThan(.5f));
                effect.enabled = false;
                Assert.Zero(effect.EffectiveIntensity);
                effect.enabled = true;
                effect.ConfigureForChapter(-1);
                Assert.Zero(effect.EffectiveIntensity);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SchoolTapePresetIsExplicitAndCannotBleedIntoAnotherScene()
        {
            var other = EditorSceneManager.NewPreviewScene();
            other.name = "VintageOtherChapterFixture";
            var root = new GameObject("School preset gate fixture");
            SceneManager.MoveGameObjectToScene(root, other);
            try
            {
                var effect = root.AddComponent<VintageCameraEffect>();
                Assert.False(effect.schoolTreatment);
                Assert.That(effect.intensity, Is.EqualTo(.72f));
                effect.ConfigureSchoolTreatment();
                effect.ConfigureForChapter(2);
                Assert.False(effect.SchoolTreatmentAllowed);
                effect.PulseInterference();
                Assert.Zero(effect.Interference);
                var parameters = typeof(VintageCameraEffect).GetMethod("GetParameters", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(effect, new object[] { 1280, 720 }) as MaterialPropertyBlock;
                Assert.That(parameters.GetVector(Shader.PropertyToID("_VintageTape")), Is.EqualTo(Vector4.zero));
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(other); }
        }

        [Test]
        public void SchoolTrackingFreezesDuringPauseAndUsesBoundedBriefFaults()
        {
            var school = SceneManager.GetSceneByName("SchoolCEEP");
            bool created = !school.IsValid();
            if (created) { school = EditorSceneManager.NewPreviewScene(); school.name = "SchoolCEEP"; }
            var root = new GameObject("School signal pause fixture");
            SceneManager.MoveGameObjectToScene(root, school);
            try
            {
                var effect = root.AddComponent<VintageCameraEffect>();
                effect.ConfigureSchoolTreatment(); effect.ConfigureForChapter(2);
                Assert.True(effect.SchoolTreatmentAllowed);
                effect.AdvanceSignal(1, false);
                effect.PulseInterference(10, 10);
                Assert.That(effect.Interference, Is.EqualTo(1));
                float clock = effect.SignalTime;
                effect.AdvanceSignal(20, true);
                Assert.That(effect.SignalTime, Is.EqualTo(clock));
                Assert.That(effect.Interference, Is.EqualTo(1));
                effect.AdvanceSignal(1, false);
                Assert.Zero(effect.Interference);
                var parameters = typeof(VintageCameraEffect).GetMethod("GetParameters", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(effect, new object[] { 1280, 720 }) as MaterialPropertyBlock;
                var tape = parameters.GetVector(Shader.PropertyToID("_VintageTape"));
                Assert.That(tape.y, Is.EqualTo(23));
                Assert.That(tape.z, Is.LessThanOrEqualTo(.5f));
            }
            finally { Object.DestroyImmediate(root); if (created) EditorSceneManager.ClosePreviewScene(school); }
        }

        [Test]
        public void ExplicitRoadGradeCannotLeakIntoMenuOrChapterZero()
        {
            var road = EditorSceneManager.NewPreviewScene(); road.name = "RoadJourney";
            var menu = EditorSceneManager.NewPreviewScene(); menu.name = "MainMenu";
            var root = new GameObject("Road grade gate fixture");
            SceneManager.MoveGameObjectToScene(root, road);
            try
            {
                var effect = root.AddComponent<VintageCameraEffect>();
                effect.ConfigureForChapter(1);
                Assert.False(effect.HorrorTreatmentAllowed);
                effect.ConfigureHorrorTreatment();
                Assert.True(effect.HorrorTreatmentAllowed);
                var parameters = ReadParameters(effect);
                Assert.That(parameters.GetVector(Shader.PropertyToID("_VintageGrade")).x,
                    Is.InRange(.5f, .9f), "Colour style should differ without removing all colour.");
                Assert.That(parameters.GetVector(Shader.PropertyToID("_VintageLens")).y, Is.EqualTo(1));
                Assert.That(parameters.GetVector(Shader.PropertyToID("_VintageTape")).y, Is.GreaterThan(12));

                effect.ConfigureForChapter(0);
                Assert.False(effect.HorrorTreatmentAllowed);
                Assert.That(ReadParameters(effect).GetVector(Shader.PropertyToID("_VintageLens")), Is.EqualTo(Vector4.zero));

                effect.ConfigureForChapter(1);
                SceneManager.MoveGameObjectToScene(root, menu);
                Assert.False(effect.HorrorTreatmentAllowed);
                parameters = ReadParameters(effect);
                Assert.That(parameters.GetVector(Shader.PropertyToID("_VintageGrade")), Is.EqualTo(new Vector4(1, 0, 0, 0)));
                Assert.That(parameters.GetVector(Shader.PropertyToID("_VintageTape")), Is.EqualTo(Vector4.zero));
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(road);
                EditorSceneManager.ClosePreviewScene(menu);
            }
        }

        [Test]
        public void HorrorGradeStillExcludesTheOfficeAndFreezesWithGameplay()
        {
            var road = EditorSceneManager.NewPreviewScene(); road.name = "RoadJourney";
            var root = new GameObject("Horror grade pause fixture");
            SceneManager.MoveGameObjectToScene(root, road);
            try
            {
                var effect = root.AddComponent<VintageCameraEffect>();
                effect.ConfigureHorrorTreatment(); effect.ConfigureForChapter(1);
                effect.AdvanceSignal(1, false);
                float clock = effect.SignalTime;
                effect.AdvanceSignal(10, true);
                Assert.That(effect.SignalTime, Is.EqualTo(clock));
                effect.prologueSceneName = road.name;
                Assert.False(effect.HorrorTreatmentAllowed);
                Assert.Zero(effect.EffectiveIntensity);
                Assert.That(ReadParameters(effect).GetVector(Shader.PropertyToID("_VintageLens")), Is.EqualTo(Vector4.zero));
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(road); }
        }

        static MaterialPropertyBlock ReadParameters(VintageCameraEffect effect)
            => typeof(VintageCameraEffect).GetMethod("GetParameters", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(effect, new object[] { 1280, 720 }) as MaterialPropertyBlock;

        [Test]
        public void ExhaustionWarpIsBoundedAndCannotReachTheOfficeOrAnotherChapter()
        {
            var school = EditorSceneManager.NewPreviewScene(); school.name = "SchoolCEEP";
            var road = EditorSceneManager.NewPreviewScene(); road.name = "RoadJourney";
            var root = new GameObject("Darkness distortion gate fixture");
            SceneManager.MoveGameObjectToScene(root, school);
            try
            {
                var effect = root.AddComponent<VintageCameraEffect>();
                effect.ConfigureSchoolTreatment(); effect.ConfigureForChapter(2);
                effect.SetDarknessDistortion(3);
                Assert.That(effect.DarknessDistortion, Is.EqualTo(1));
                Assert.That(ReadParameters(effect).GetFloat(Shader.PropertyToID("_VintageDarkness")), Is.EqualTo(1));
                effect.SetDarknessDistortion(float.NaN);
                Assert.That(effect.DarknessDistortion, Is.Zero);
                effect.SetDarknessDistortion(.6f);
                effect.prologueSceneName = "SchoolCEEP";
                Assert.That(effect.DarknessDistortion, Is.Zero);
                effect.prologueSceneName = "Chapter1Office";
                SceneManager.MoveGameObjectToScene(root, road);
                Assert.That(effect.DarknessDistortion, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(school);
                EditorSceneManager.ClosePreviewScene(road);
            }
        }

        [Test]
        public void CompositeColourIsOptInAndDoesNotScheduleRepeatedTrackingFaults()
        {
            var road = EditorSceneManager.NewPreviewScene(); road.name = "RoadJourney";
            var menu = EditorSceneManager.NewPreviewScene(); menu.name = "MainMenu";
            var root = new GameObject("Composite signal fixture");
            SceneManager.MoveGameObjectToScene(root, road);
            try
            {
                var effect = root.AddComponent<VintageCameraEffect>();
                effect.ConfigureForChapter(1);
                int composite = Shader.PropertyToID("_VintageComposite");
                Assert.That(ReadParameters(effect).GetVector(composite), Is.EqualTo(Vector4.zero));
                effect.ConfigureHorrorTreatment();
                Assert.That(ReadParameters(effect).GetVector(composite).x, Is.EqualTo(1));
                Assert.Zero(effect.scanlines, "The composite look must not require visible scanlines.");
                Assert.Zero(effect.trackingDuration, "Story pulses, not an automatic repeated glitch.");
                effect.AdvanceSignal(90, true);
                Assert.Zero(effect.SignalTime);
                SceneManager.MoveGameObjectToScene(root, menu);
                Assert.That(ReadParameters(effect).GetVector(composite), Is.EqualTo(Vector4.zero));
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(road);
                EditorSceneManager.ClosePreviewScene(menu);
            }
        }
    }
}
