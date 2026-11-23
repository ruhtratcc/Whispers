// Verifica SetUp, TearDown, ExistingVersionOneCheckpointsRemainReadable, VersionTwoRoundTripPreservesNarrativeAndPendingMap, UnknownSchemaAndDamagedJsonAreRejected, CurrentChaptersAcceptTheirAuthoredScenesWithRequiredKeys, CurrentChaptersRejectAnIncompatibleScene, CurrentStoryRejectsChaptersOutsideTheSixChapterCampaign, UnknownStoryRevisionsAreNotTreatedAsLegacy, CurrentAdvancedChaptersRejectMissingKeys, FirstHospicioChapterNeedsOnlyTheLibraryKey, OfficeReturnAndCompletedEndingKeepTheLastExitChapter, LastExitCannotAppearInTheOfficeBeforeLeavingSchool, NullInventoryEntriesAreRejectedAfterJsonRoundTrip, InventoryEntriesRequireAnId, DuplicateInventoryIdsAreRejectedWithoutChoosingOneEntry, LegacySnapshotsWithoutInventoryOrNarrativeRemainReadable, WellFormedUnknownItemIdsAndInspectionMetadataArePreserved, OlderAdvancedStoriesCanReachTheirExistingMigrationWithoutCurrentKeys, MigratedAdvancedStoriesRemainReadableAfterTheirNextSave, CurrentCheckpointPreservesAnOlderPendingChapterValue, ImpossiblePrincipalSelectsHealthyBackupWithoutRewritingEitherFile, SnapshotWriterRejectsInvalidInventoryWithoutCreatingItsDisposableFile.

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorGame.Tests
{
    [Category("GameplayCompletion")]
    public sealed class NarrativeSaveTests
    {
        string path;
        [SetUp] public void SetUp() => path = Path.Combine(Path.GetTempPath(), "Whispers-narrative-" + System.Guid.NewGuid().ToString("N") + ".json");
        [TearDown] public void TearDown()
        {
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }
        [Test] public void ExistingVersionOneCheckpointsRemainReadable()
        {
            var data = new SaveData { version = 1, scene = "Chapter1Office", position = new Vector3(1, 0, 2) };
            File.WriteAllText(path, JsonUtility.ToJson(data));
            Assert.That(SaveSystem.TryRead(path, out var loaded), Is.True);
            Assert.That(loaded.version, Is.EqualTo(1));
            Assert.That(loaded.position, Is.EqualTo(data.position));
        }
        [Test] public void VersionTwoRoundTripPreservesNarrativeAndPendingMap()
        {
            var data = new SaveData { scene = "Chapter1Office", narrative = new NarrativeProgress { chapterId = "prologue", pendingChapterId = "road" } };
            data.narrative.milestones.Add("prologue.first-call"); data.narrative.sequences.Add("prologue.call-one");
            File.WriteAllText(path, JsonUtility.ToJson(data));
            Assert.That(SaveSystem.TryRead(path, out var loaded), Is.True);
            Assert.That(loaded.version, Is.EqualTo(2));
            Assert.That(loaded.narrative.pendingChapterId, Is.EqualTo("road"));
            Assert.That(loaded.narrative.HasSignal("prologue.first-call"), Is.True);
            Assert.That(loaded.narrative.HasSequence("prologue.call-one"), Is.True);
        }
        [Test] public void UnknownSchemaAndDamagedJsonAreRejected()
        {
            File.WriteAllText(path, JsonUtility.ToJson(new SaveData { version = 99, scene = "Chapter1Office" }));
            Assert.That(SaveSystem.TryRead(path, out _), Is.False);
            File.WriteAllText(path, "{broken");
            Assert.That(SaveSystem.TryRead(path, out _), Is.False);
        }

        [TestCase("prologue", "Chapter1Office")]
        [TestCase("road", "RoadJourney")]
        [TestCase("school", "SchoolCEEP")]
        [TestCase("left-behind", "SchoolCEEP")]
        [TestCase("memories", "SchoolCEEP")]
        [TestCase("last-exit", "SchoolCEEP")]
        [Category("SaveValidation")]
        public void CurrentChaptersAcceptTheirAuthoredScenesWithRequiredKeys(string chapter, string scene)
        {
            var data = Current(chapter, scene, "iron_key", "room_113_key");
            Assert.That(Read(data, out var loaded), Is.True);
            Assert.That(loaded.scene, Is.EqualTo(scene));
            Assert.That(loaded.narrative.chapterId, Is.EqualTo(chapter));
        }

        [TestCase("prologue", "SchoolCEEP")]
        [TestCase("road", "Chapter1Office")]
        [TestCase("school", "RoadJourney")]
        [TestCase("left-behind", "Chapter1Office")]
        [TestCase("memories", "Chapter1Office")]
        [TestCase("last-exit", "RoadJourney")]
        [Category("SaveValidation")]
        public void CurrentChaptersRejectAnIncompatibleScene(string chapter, string scene)
        {
            Assert.That(Read(Current(chapter, scene, "iron_key", "room_113_key"), out _), Is.False);
        }

        [TestCase("terrace")]
        [TestCase("unknown-chapter")]
        [Category("SaveValidation")]
        public void CurrentStoryRejectsChaptersOutsideTheSixChapterCampaign(string chapter)
        {
            Assert.That(Read(Current(chapter, "SchoolCEEP", "iron_key", "room_113_key"), out _), Is.False);
        }

        [TestCase(-1)]
        [TestCase(3)]
        [Category("SaveValidation")]
        public void UnknownStoryRevisionsAreNotTreatedAsLegacy(int revision)
        {
            var data = Current("prologue", "Chapter1Office");
            data.narrative.storyRevision = revision;
            Assert.That(Read(data, out _), Is.False);
        }

        [TestCase("left-behind", "")]
        [TestCase("memories", "")]
        [TestCase("memories", "iron_key")]
        [TestCase("memories", "room_113_key")]
        [TestCase("last-exit", "iron_key")]
        [TestCase("last-exit", "room_113_key")]
        [Category("SaveValidation")]
        public void CurrentAdvancedChaptersRejectMissingKeys(string chapter, string existingKey)
        {
            var data = Current(chapter, "SchoolCEEP");
            if (!string.IsNullOrEmpty(existingKey)) data.inventory.Add(new InventoryItem { id = existingKey });
            Assert.That(Read(data, out _), Is.False);
        }

        [Test, Category("SaveValidation")]
        public void FirstHospicioChapterNeedsOnlyTheLibraryKey()
        {
            Assert.That(Read(Current("left-behind", "SchoolCEEP", "iron_key"), out var loaded), Is.True);
            Assert.That(loaded.inventory.Count, Is.EqualTo(1));
            Assert.That(loaded.inventory[0].id, Is.EqualTo("iron_key"));
        }

        [TestCase(false)]
        [TestCase(true)]
        [Category("SaveValidation")]
        public void OfficeReturnAndCompletedEndingKeepTheLastExitChapter(bool endingSeen)
        {
            var data = Current("last-exit", "Chapter1Office", "iron_key", "room_113_key");
            data.narrative.milestones.Add("last-exit.outside");
            data.narrative.endingSeen = endingSeen;
            Assert.That(Read(data, out var loaded), Is.True);
            Assert.That(loaded.narrative.endingSeen, Is.EqualTo(endingSeen));
            Assert.That(loaded.narrative.chapterId, Is.EqualTo("last-exit"));
        }

        [Test, Category("SaveValidation")]
        public void LastExitCannotAppearInTheOfficeBeforeLeavingSchool()
        {
            Assert.That(Read(Current("last-exit", "Chapter1Office", "iron_key", "room_113_key"), out _), Is.False);
        }

        [Test, Category("SaveValidation")]
        public void NullInventoryEntriesAreRejectedAfterJsonRoundTrip()
        {
            var data = Current("school", "SchoolCEEP");
            data.inventory.Add(null);
            Assert.That(Read(data, out _), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        [Category("SaveValidation")]
        public void InventoryEntriesRequireAnId(string id)
        {
            Assert.That(Read(Current("prologue", "Chapter1Office", id), out _), Is.False);
        }

        [Test, Category("SaveValidation")]
        public void DuplicateInventoryIdsAreRejectedWithoutChoosingOneEntry()
        {
            Assert.That(Read(Current("prologue", "Chapter1Office", "same-note", "same-note"), out _), Is.False);
        }

        [TestCase(1)]
        [TestCase(2)]
        [Category("SaveValidation")]
        public void LegacySnapshotsWithoutInventoryOrNarrativeRemainReadable(int version)
        {
            var data = new SaveData { version = version, scene = "Chapter1Office", inventory = null };
            Assert.That(Read(data, out var loaded), Is.True);
            Assert.That(loaded.version, Is.EqualTo(version));
        }

        [Test, Category("SaveValidation")]
        public void WellFormedUnknownItemIdsAndInspectionMetadataArePreserved()
        {
            var data = Current("prologue", "Chapter1Office", "legacy.not-in-catalog");
            data.inventory[0].text = "Evidência anterior.";
            data.inventory[0].note = true;
            data.inventory[0].inspectionId = "legacy.inspection";
            data.inventory[0].inspectionSeen = true;
            data.inventory[0].inspectionCount = 3;
            Assert.That(Read(data, out var loaded), Is.True);
            Assert.That(JsonUtility.ToJson(loaded.inventory[0]), Is.EqualTo(JsonUtility.ToJson(data.inventory[0])));
        }

        [TestCase(1, 0)]
        [TestCase(2, 1)]
        [Category("SaveValidation")]
        public void OlderAdvancedStoriesCanReachTheirExistingMigrationWithoutCurrentKeys(int version, int revision)
        {
            var data = Current("memories", "School-legacy-fixture", "old_note");
            data.version = version;
            data.narrative.storyRevision = revision;
            Assert.That(Read(data, out var loaded), Is.True);
            Assert.That(loaded.narrative.storyRevision, Is.EqualTo(revision));
            Assert.That(loaded.inventory.Count, Is.EqualTo(1));
            Assert.That(loaded.inventory[0].id, Is.EqualTo("old_note"));
        }

        [TestCase("left-behind")]
        [TestCase("memories")]
        [TestCase("terrace")]
        [TestCase("last-exit")]
        [Category("SaveValidation")]
        public void MigratedAdvancedStoriesRemainReadableAfterTheirNextSave(string oldChapter)
        {
            var data = Current(oldChapter, "SchoolCEEP", "old_key");
            data.narrative.storyRevision = 0;
            data.narrative.milestones.Add("legacy.visited-room");
            data.narrative.completedChapters.Add("terrace");
            Assert.That(Read(data, out var legacy), Is.True);
            var root = new GameObject("Save migration validation fixture");
            try
            {
                var inventory = root.AddComponent<Inventory>();
                inventory.Restore(legacy.inventory);
                Assert.That(SimplifiedStoryContent.Migrate(legacy.narrative, inventory), Is.True);
                legacy.inventory = inventory.items;
                Assert.That(Read(legacy, out var loaded), Is.True);
                Assert.That(loaded.narrative.storyRevision, Is.EqualTo(2));
                Assert.That(loaded.narrative.HasSignal("legacy.visited-room"), Is.True);
                Assert.That(loaded.narrative.completedChapters, Does.Contain("terrace"));
                Assert.That(loaded.inventory.Exists(item => item.id == "old_key"), Is.True);
                Assert.That(loaded.inventory.Exists(item => item.id == "iron_key"), Is.True);
                Assert.That(loaded.inventory.Exists(item => item.id == "student_113_record"), Is.False,
                    "The historical migration never supplied this document; validation cannot require it retroactively.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test, Category("SaveValidation")]
        public void CurrentCheckpointPreservesAnOlderPendingChapterValue()
        {
            var data = Current("prologue", "Chapter1Office");
            data.narrative.pendingChapterId = "legacy.pending";
            Assert.That(Read(data, out var loaded), Is.True);
            Assert.That(loaded.narrative.pendingChapterId, Is.EqualTo("legacy.pending"));
        }

        [Test, Category("SaveValidation")]
        public void ImpossiblePrincipalSelectsHealthyBackupWithoutRewritingEitherFile()
        {
            var impossible = Current("memories", "SchoolCEEP");
            var healthy = Current("school", "SchoolCEEP", "backup-proof");
            healthy.position = new Vector3(2, 0, 3);
            File.WriteAllText(path, JsonUtility.ToJson(impossible));
            File.WriteAllText(path + ".bak", JsonUtility.ToJson(healthy));
            byte[] principalBytes = File.ReadAllBytes(path), backupBytes = File.ReadAllBytes(path + ".bak");
            Assert.That(SaveSystem.TryRead(path, out _), Is.False);
            Assert.That(SaveSystem.TryReadCheckpoint(path, out var loaded, "SchoolCEEP"), Is.True);
            Assert.That(loaded.narrative.chapterId, Is.EqualTo("school"));
            Assert.That(loaded.position, Is.EqualTo(healthy.position));
            Assert.That(loaded.inventory[0].id, Is.EqualTo("backup-proof"));
            CollectionAssert.AreEqual(principalBytes, File.ReadAllBytes(path));
            CollectionAssert.AreEqual(backupBytes, File.ReadAllBytes(path + ".bak"));
        }

        [Test, Category("SaveValidation")]
        public void SnapshotWriterRejectsInvalidInventoryWithoutCreatingItsDisposableFile()
        {
            bool hadScene = PlayerPrefs.HasKey("Game.SaveScene");
            string originalScene = PlayerPrefs.GetString("Game.SaveScene", "");
            var root = new GameObject("Snapshot writer validation fixture");
            var save = root.AddComponent<SaveSystem>();
            save.fileName = "save-validation-" + System.Guid.NewGuid().ToString("N") + ".json";
            try
            {
                Assert.That(save.SavesSuspended, Is.False, "This assertion must exercise validation, not the startup lock.");
                var data = new SaveData { scene = SceneManager.GetActiveScene().name,
                    inventory = new List<InventoryItem> { new InventoryItem { id = " " } } };
                Assert.That(save.SaveSnapshot(data), Is.False);
                foreach (string suffix in new[] { "", ".bak", ".tmp" })
                    Assert.That(File.Exists(save.SavePath + suffix), Is.False);
                Assert.That(PlayerPrefs.HasKey("Game.SaveScene"), Is.EqualTo(hadScene));
                Assert.That(PlayerPrefs.GetString("Game.SaveScene", ""), Is.EqualTo(originalScene));
            }
            finally
            {
                foreach (string suffix in new[] { "", ".bak", ".tmp" })
                    if (File.Exists(save.SavePath + suffix)) File.Delete(save.SavePath + suffix);
                if (hadScene) PlayerPrefs.SetString("Game.SaveScene", originalScene); else PlayerPrefs.DeleteKey("Game.SaveScene");
                PlayerPrefs.Save();
                Object.DestroyImmediate(root);
            }
        }

        bool Read(SaveData data, out SaveData loaded)
        {
            File.WriteAllText(path, JsonUtility.ToJson(data));
            return SaveSystem.TryRead(path, out loaded);
        }

        static SaveData Current(string chapter, string scene, params string[] ids)
        {
            var data = new SaveData { scene = scene, narrative = new NarrativeProgress { chapterId = chapter, storyRevision = 2 } };
            foreach (string id in ids) data.inventory.Add(new InventoryItem { id = id });
            return data;
        }
    }
}
