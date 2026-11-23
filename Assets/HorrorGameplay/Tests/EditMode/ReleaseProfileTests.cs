// Verifica WindowsDistributionContainsExactlyTheFourProductionScenes.

using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Profile;

namespace HorrorGame.Tests
{
    public sealed class ReleaseProfileTests
    {
        [Test]
        public void WindowsDistributionContainsExactlyTheFourProductionScenes()
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>("Assets/Settings/Build Profiles/Whispers Windows Prototype.asset");
            Assert.That(profile, Is.Not.Null); Assert.That(profile.overrideGlobalScenes, Is.True);
            CollectionAssert.AreEqual(new[] { "Assets/HorrorMenu/Scenes/MainMenu.unity", "Assets/ChapterOneOffice/Scenes/Chapter1Office.unity",
                "Assets/RoadChapter/Scenes/RoadJourney.unity", "Assets/SchoolCEEP/Scenes/SchoolCEEP.unity" }, profile.scenes.Select(scene => scene.path));
            Assert.That(profile.scenes.All(scene => scene.enabled && AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path)), Is.True);
        }
    }
}
