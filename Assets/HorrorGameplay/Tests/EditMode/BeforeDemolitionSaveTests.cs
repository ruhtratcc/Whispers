// Verifica SetUp, TearDown, NewProfileResolvesDedicatedFileAndRequestKeysBeforeAnyIO, LegacyProfileRetainsItsConfiguredFileAndKeys, TestStorageRejectsRelativeDirectories, InitialOfficeCheckpointAndOutOfOrderReadStateRoundTrip, ReadyAndCompletedExitRoundTripWithoutLegacyKeysOrEnding, RevisionThreeCannotMasqueradeAsLegacyOrAnotherStory, NewIdentityNeverEntersOldRevisionMigration, PhaseCRejectsUnimplementedScenesAndOldChapterIds, ReadyRequiresEveryApprovedMinimumRead, ReadyRequiresWhistleAndExitRequiresCompletedPendingBoundary, UnfinishedOfficeCannotClaimCompletionOrGameEnding, UnorderedReadFlagsAndOldSignalsCannotPolluteOrderedMilestones, DuplicateMilestonesObjectsAndInventoryAreRejected, ExitCannotPrecedeItsReadyMilestone, OfficeFlowAndEmailIdentityFieldsAreRequiredInsteadOfConstructorDefaults, FutureDeliveryUnknownMessagesAndDuplicateReadIdsAreRejected, ProfileFilteredBackupSkipsLegacyPrincipalWithoutRewritingEither, CorruptNewPrimaryUsesOnlyCompatibleBackupAndNeverCreatesMissingFiles.

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace HorrorGame.Tests
{
    [Category("BeforeDemolitionSave")]
    public sealed class BeforeDemolitionSaveTests
    {
        string directory, path, previousDirectory;
        StoryBook book;
        [Serializable] sealed class OfficeState { public EmailInboxState emails; }

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "Whispers-before-demolition-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            previousDirectory = SaveSystem.TestSaveDirectory;
            SaveSystem.TestSaveDirectory = directory;
            path = SaveSystem.PathForFile(BeforeDemolitionCampaign.CheckpointFile);
            book = ScriptableObject.CreateInstance<StoryBook>();
            book.storyId = BeforeDemolitionCampaign.StoryId;
            book.revision = BeforeDemolitionCampaign.Revision;
            book.chapters = new[]
            {
                new StoryChapterDefinition { id = BeforeDemolitionCampaign.OfficeChapterId, sceneName = "Chapter1Office" },
                new StoryChapterDefinition { id = BeforeDemolitionCampaign.RoadChapterId, sceneName = "" }
            };
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.TestSaveDirectory = previousDirectory;
            if (book) UnityEngine.Object.DestroyImmediate(book);
            if (Directory.Exists(directory))
            {
                foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        }

        [Test]
        public void NewProfileResolvesDedicatedFileAndRequestKeysBeforeAnyIO()
        {
            Assert.That(BeforeDemolitionCampaign.FileName(book, "save.json"), Is.EqualTo(BeforeDemolitionCampaign.CheckpointFile));
            Assert.That(BeforeDemolitionCampaign.Key(book, "ResumeRequested"), Is.EqualTo("BeforeDemolition.ResumeRequested"));
            Assert.That(BeforeDemolitionCampaign.Key(book, "StoryCarryRequested"), Is.EqualTo("BeforeDemolition.StoryCarryRequested"));
            Assert.That(BeforeDemolitionCampaign.Key(book, "SaveScene"), Is.EqualTo("BeforeDemolition.SaveScene"));
            Assert.That(path, Is.EqualTo(Path.Combine(directory, BeforeDemolitionCampaign.CheckpointFile)));
            book.revision = 2;
            Assert.That(BeforeDemolitionCampaign.FileName(book, "save.json"), Is.EqualTo(BeforeDemolitionCampaign.CheckpointFile),
                "A broken new-story revision must never redirect storage to the legacy file.");
        }

        [Test]
        public void LegacyProfileRetainsItsConfiguredFileAndKeys()
        {
            book.storyId = null;
            Assert.That(BeforeDemolitionCampaign.FileName(book, "legacy-fixture.json"), Is.EqualTo("legacy-fixture.json"));
            Assert.That(BeforeDemolitionCampaign.Key(book, "ResumeRequested"), Is.EqualTo("Game.ResumeRequested"));
            Assert.That(BeforeDemolitionCampaign.Key(null, "SaveScene"), Is.EqualTo("Game.SaveScene"));
        }

        [Test]
        public void TestStorageRejectsRelativeDirectories()
        {
            Assert.Throws<ArgumentException>(() => SaveSystem.TestSaveDirectory = "relative-test-directory");
            Assert.That(SaveSystem.TestSaveDirectory, Is.EqualTo(directory));
        }

        [Test]
        public void InitialOfficeCheckpointAndOutOfOrderReadStateRoundTrip()
        {
            var data = Initial();
            SetEmails(data, Emails("email.07", "email.06", "email.01"));
            Assert.That(Read(data, out var loaded), Is.True);
            Assert.That(loaded.narrative.storyId, Is.EqualTo(BeforeDemolitionCampaign.StoryId));
            Assert.That(loaded.narrative.storyRevision, Is.EqualTo(3));
            Assert.That(loaded.inventory, Is.Empty);
            Assert.That(loaded.narrative.milestones, Is.Empty);
        }

        [Test]
        public void ReadyAndCompletedExitRoundTripWithoutLegacyKeysOrEnding()
        {
            var data = Ready();
            Assert.That(Read(data, out _), Is.True);
            data.narrative.milestones.Add(BeforeDemolitionCampaign.ExitSignal);
            data.narrative.completedChapters.Add(BeforeDemolitionCampaign.OfficeChapterId);
            data.narrative.pendingChapterId = BeforeDemolitionCampaign.RoadChapterId;
            Assert.That(Read(data, out var loaded), Is.True);
            Assert.That(loaded.narrative.endingSeen, Is.False);
            Assert.That(loaded.inventory.Count, Is.EqualTo(1));
            Assert.That(loaded.inventory[0].id, Is.EqualTo(BeforeDemolitionCampaign.WhistleItemId));
        }

        [TestCase(null)]
        [TestCase("old-story")]
        public void RevisionThreeCannotMasqueradeAsLegacyOrAnotherStory(string identity)
        {
            var data = Initial();
            data.narrative.storyId = identity;
            Assert.That(Read(data, out _), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void NewIdentityNeverEntersOldRevisionMigration(int revision)
        {
            var data = Initial();
            data.narrative.storyRevision = revision;
            Assert.That(Read(data, out _), Is.False);
        }

        [TestCase("RoadJourney", "bd.road")]
        [TestCase("SchoolCEEP", "bd.office")]
        [TestCase("Chapter1Office", "prologue")]
        public void PhaseCRejectsUnimplementedScenesAndOldChapterIds(string scene, string chapter)
        {
            var data = Initial(); data.scene = scene; data.narrative.chapterId = chapter;
            Assert.That(Read(data, out _), Is.False);
        }

        [TestCase("email.01")]
        [TestCase("email.06")]
        [TestCase("email.07")]
        public void ReadyRequiresEveryApprovedMinimumRead(string missing)
        {
            var data = Ready();
            var emails = Emails("email.01", "email.06", "email.07");
            emails.readIds.Remove(missing); SetEmails(data, emails);
            Assert.That(Read(data, out _), Is.False);
        }

        [Test]
        public void ReadyRequiresWhistleAndExitRequiresCompletedPendingBoundary()
        {
            var data = Ready(); data.inventory.Clear();
            Assert.That(Read(data, out _), Is.False);
            data = Ready(); data.narrative.milestones.Add(BeforeDemolitionCampaign.ExitSignal);
            Assert.That(Read(data, out _), Is.False);
            data.narrative.completedChapters.Add(BeforeDemolitionCampaign.OfficeChapterId);
            Assert.That(Read(data, out _), Is.False);
            data.narrative.pendingChapterId = BeforeDemolitionCampaign.RoadChapterId;
            Assert.That(Read(data, out _), Is.True);
        }

        [Test]
        public void UnfinishedOfficeCannotClaimCompletionOrGameEnding()
        {
            var data = Initial(); data.narrative.completedChapters.Add(BeforeDemolitionCampaign.OfficeChapterId);
            Assert.That(Read(data, out _), Is.False);
            data = Initial(); data.narrative.pendingChapterId = BeforeDemolitionCampaign.RoadChapterId;
            Assert.That(Read(data, out _), Is.False);
            data = Initial(); data.narrative.endingSeen = true;
            Assert.That(Read(data, out _), Is.False);
        }

        [TestCase("prologue.first-call")]
        [TestCase("email.07.read")]
        public void UnorderedReadFlagsAndOldSignalsCannotPolluteOrderedMilestones(string signal)
        {
            var data = Initial(); data.narrative.milestones.Add(signal);
            Assert.That(Read(data, out _), Is.False);
        }

        [Test]
        public void DuplicateMilestonesObjectsAndInventoryAreRejected()
        {
            var data = Ready(); data.narrative.milestones.Add(BeforeDemolitionCampaign.ReadySignal);
            Assert.That(Read(data, out _), Is.False);
            data = Initial(); data.objects.Add(data.objects[0]);
            Assert.That(Read(data, out _), Is.False);
            data = Ready(); data.inventory.Add(data.inventory[0]);
            Assert.That(Read(data, out _), Is.False);
        }

        [Test]
        public void ExitCannotPrecedeItsReadyMilestone()
        {
            var data = Ready();
            data.narrative.milestones.Insert(0, BeforeDemolitionCampaign.ExitSignal);
            data.narrative.completedChapters.Add(BeforeDemolitionCampaign.OfficeChapterId);
            data.narrative.pendingChapterId = BeforeDemolitionCampaign.RoadChapterId;
            Assert.That(Read(data, out _), Is.False);
        }

        [Test]
        public void OfficeFlowAndEmailIdentityFieldsAreRequiredInsteadOfConstructorDefaults()
        {
            var data = Initial(); data.objects.Clear();
            Assert.That(Read(data, out _), Is.False);
            data = Initial(); data.objects[0].json = "{}";
            Assert.That(Read(data, out _), Is.False);
            data.objects[0].json = "{\"emails\":{\"version\":1,\"deliveredIds\":[\"email.01\",\"email.02\",\"email.03\",\"email.04\",\"email.05\",\"email.06\",\"email.07\"],\"readIds\":[]}}";
            Assert.That(Read(data, out _), Is.False);
            data.objects[0].json = "{\"emails\":{\"storyId\":\"before-demolition\",\"deliveredIds\":[\"email.01\",\"email.02\",\"email.03\",\"email.04\",\"email.05\",\"email.06\",\"email.07\"],\"readIds\":[]}}";
            Assert.That(Read(data, out _), Is.False);
        }

        [Test]
        public void FutureDeliveryUnknownMessagesAndDuplicateReadIdsAreRejected()
        {
            var data = Initial(); var emails = Emails(); emails.deliveredIds.Add("email.08"); SetEmails(data, emails);
            Assert.That(Read(data, out _), Is.False);
            emails = Emails(); emails.deliveredIds[0] = "email.08"; SetEmails(data, emails);
            Assert.That(Read(data, out _), Is.False);
            emails = Emails("email.08"); SetEmails(data, emails);
            Assert.That(Read(data, out _), Is.False);
            emails = Emails("email.01", "email.01"); SetEmails(data, emails);
            Assert.That(Read(data, out _), Is.False);
            emails = Emails(); emails.deliveredIds[1] = "email.01"; SetEmails(data, emails);
            Assert.That(Read(data, out _), Is.False);
        }

        [Test]
        public void ProfileFilteredBackupSkipsLegacyPrincipalWithoutRewritingEither()
        {
            var legacy = new SaveData { scene = "Chapter1Office", narrative = new NarrativeProgress { storyRevision = 2, chapterId = "prologue" } };
            File.WriteAllText(path, JsonUtility.ToJson(legacy));
            File.WriteAllText(path + ".bak", JsonUtility.ToJson(Initial()));
            byte[] primary = File.ReadAllBytes(path), backup = File.ReadAllBytes(path + ".bak");
            Assert.That(SaveSystem.TryReadCheckpoint(path, out var loaded, "Chapter1Office", BeforeDemolitionCampaign.StoryId), Is.True);
            Assert.That(loaded.narrative.storyId, Is.EqualTo(BeforeDemolitionCampaign.StoryId));
            CollectionAssert.AreEqual(primary, File.ReadAllBytes(path));
            CollectionAssert.AreEqual(backup, File.ReadAllBytes(path + ".bak"));
            Assert.That(SaveSystem.TryReadCheckpoint(path + ".bak", out _, "Chapter1Office", ""), Is.False,
                "A legacy-profile request must not accept the new story either.");
        }

        [Test]
        public void CorruptNewPrimaryUsesOnlyCompatibleBackupAndNeverCreatesMissingFiles()
        {
            File.WriteAllText(path, "{broken");
            File.WriteAllText(path + ".bak", JsonUtility.ToJson(Ready()));
            Assert.That(SaveSystem.TryReadCheckpoint(path, out _, "Chapter1Office", BeforeDemolitionCampaign.StoryId), Is.True);
            File.Delete(path + ".bak");
            byte[] invalid = File.ReadAllBytes(path);
            Assert.That(SaveSystem.TryReadCheckpoint(path, out _, "Chapter1Office", BeforeDemolitionCampaign.StoryId), Is.False);
            CollectionAssert.AreEqual(invalid, File.ReadAllBytes(path));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        bool Read(SaveData data, out SaveData loaded)
        {
            File.WriteAllText(path, JsonUtility.ToJson(data));
            return SaveSystem.TryRead(path, out loaded);
        }

        static SaveData Initial()
        {
            var data = new SaveData { scene = "Chapter1Office", narrative = new NarrativeProgress
                { storyId = BeforeDemolitionCampaign.StoryId, storyRevision = 3, chapterId = BeforeDemolitionCampaign.OfficeChapterId } };
            SetEmails(data, Emails());
            return data;
        }

        static SaveData Ready()
        {
            var data = Initial();
            SetEmails(data, Emails("email.01", "email.06", "email.07"));
            data.inventory.Add(new InventoryItem { id = BeforeDemolitionCampaign.WhistleItemId, title = "Apito do Luan" });
            data.narrative.milestones.Add(BeforeDemolitionCampaign.ReadySignal);
            return data;
        }

        static EmailInboxState Emails(params string[] reads)
        {
            var state = new EmailInboxState { readIds = new List<string>(reads) };
            for (int i = 1; i <= 7; i++) state.deliveredIds.Add("email.0" + i);
            return state;
        }

        static void SetEmails(SaveData data, EmailInboxState state)
        {
            data.objects = new List<SavedObject> { new SavedObject { id = BeforeDemolitionCampaign.FlowStateId,
                json = JsonUtility.ToJson(new OfficeState { emails = state }) } };
        }
    }
}
