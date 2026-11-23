// Verifica SetUp, RetryBeforeChoiceIsDetachedAndStillPassesStrictSaveValidation, EveryLaterCheckpointPreservesTheCompletedCampaignPrefix, BothCompletedEndingsRoundTripWithTheChosenBranch, GatheredMilestoneRequiresEveryStudentObjectAndTheFullBiaTape, EveryLaterChapterStillRequiresTheSchoolEvidenceAndEmails, AnIncompleteRecordingMayBeKeptWithoutItsHeardMarker, CompletingTheIntercomDoesNotImplyThatTheStairKeyWasPickedUp, FourStudentEvidenceCannotSkipTheIntercomOrExplicitKeyPickup, CorridorCheckpointsRequireTheirEntryContextAndConsistentRecordingState, MirrorCheckpointsKeepStageChoiceAndEndingPresentationConsistent, LaterCheckpointsRejectCrossChapterOrMalformedProgress, TearDown.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HorrorGame.Tests
{
    [Category("BeforeDemolitionLaterSave")]
    public sealed class BeforeDemolitionLaterSaveTests
    {
        static readonly string[] Chapters = { BeforeDemolitionCampaign.OfficeChapterId, BeforeDemolitionCampaign.RoadChapterId,
            BeforeDemolitionCampaign.SchoolChapterId, BeforeDemolitionCampaign.FourChapterId,
            BeforeDemolitionCampaign.TwoVoicesChapterId, BeforeDemolitionCampaign.MirrorChapterId };
        static readonly string[][] Steps = {
            new[] { BeforeDemolitionCampaign.ReadySignal, BeforeDemolitionCampaign.ExitSignal },
            BeforeDemolitionCampaign.RoadSignals, BeforeDemolitionCampaign.SchoolSignals,
            new[] { "bd.four.gathered", "bd.four.key", "bd.four.upstairs" },
            new[] { "bd.loop.escaped", "bd.loop.tape", "bd.loop.mirror" },
            new[] { "bd.mirror.seen", "bd.mirror.broken", "bd.mirror.choice", "bd.mirror.finish" }
        };
        string directory, path, previousDirectory;
        EmailCatalog catalog;
        SaveData data;

        [SetUp]
        public void SetUp()
        {
            directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Whispers-LaterSave-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            previousDirectory = SaveSystem.TestSaveDirectory;
            SaveSystem.TestSaveDirectory = directory;
            path = SaveSystem.PathForFile(BeforeDemolitionCampaign.CheckpointFile);
            catalog = ScriptableObject.CreateInstance<EmailCatalog>();
            catalog.messages = EmailCatalog.BeforeDemolitionContent();
        }

        void At(int chapter, int count, BeforeDemolitionEndingChoice choice = BeforeDemolitionEndingChoice.Refuse)
        {
            var emails = EmailInboxState.Create(catalog);
            emails.Deliver(catalog, "email.08");
            foreach (string id in new[] { "email.01", "email.06", "email.07" }) emails.MarkRead(catalog, id);
            bool complete = count == Steps[chapter].Length;
            data = new SaveData {
                scene = "SchoolCEEP", position = new Vector3(0, .7f, 21), battery = 240, stamina = 100,
                narrative = new NarrativeProgress {
                    storyId = BeforeDemolitionCampaign.StoryId, storyRevision = BeforeDemolitionCampaign.Revision,
                    chapterId = Chapters[chapter], completedChapters = Chapters.Take(chapter).ToList(),
                    pendingChapterId = complete && chapter < 5 ? Chapters[chapter + 1] : "", endingSeen = complete && chapter == 5
                }
            };
            for (int i = 0; i < chapter; i++) data.narrative.milestones.AddRange(Steps[i]);
            data.narrative.milestones.AddRange(Steps[chapter].Take(count));
            if (complete) data.narrative.completedChapters.Add(Chapters[chapter]);
            foreach (string id in new[] { BeforeDemolitionCampaign.WhistleItemId, BeforeDemolitionRoadFlow.EmailDeliveryItemId,
                BeforeDemolitionSchoolFlow.KeyId, BeforeDemolitionSchoolFlow.ReportId,
                BeforeDemolitionSchoolFlow.TapeId, BeforeDemolitionSchoolFlow.TapeHeardId }) Add(id);
            foreach (string id in emails.readIds) Add("bd.note." + id);
            if (chapter > 3 || count >= 1)
            {
                foreach (string id in BeforeDemolitionFourStudentsFlow.StudentItemIds) Add(id);
                Add(BeforeDemolitionFourStudentsFlow.BiaTapeHeardId);
            }
            if (chapter > 3 || count >= 2)
            {
                Add(BeforeDemolitionFourStudentsFlow.StairKeyId);
                data.narrative.sequences.Add("bd.four.intercom");
            }
            if (chapter >= 4) data.narrative.sequences.Add("bd.loop.voice");
            if (chapter > 4 || chapter == 4 && count >= 2)
            {
                Add(BeforeDemolitionTwoVoicesFlow.TapeId); Add(BeforeDemolitionTwoVoicesFlow.TapeHeardId);
            }
            if (chapter == 5 && count >= 3) data.narrative.sequences.Add("bd.mirror.offer");
            if (chapter == 5 && count == 4)
                data.narrative.sequences.Add(choice == BeforeDemolitionEndingChoice.Refuse ? "bd.ending.refuse" : "bd.ending.sign");
            Put(BeforeDemolitionSchoolFlow.StateId, JsonUtility.ToJson(new BeforeDemolitionSchoolState { emails = emails, phoneAnswered = true }));
            Put(BeforeDemolitionTwoVoicesFlow.StateId, JsonUtility.ToJson(new BeforeDemolitionTwoVoicesState { loopCount = chapter >= 4 ? 2 : 0 }));
            Put(BeforeDemolitionMirrorFlow.StateId, JsonUtility.ToJson(new BeforeDemolitionMirrorFlow.State {
                stage = chapter == 5 ? count : 0,
                choice = chapter == 5 && count >= 3 ? choice : BeforeDemolitionEndingChoice.None
            }));
        }

        [TestCase(BeforeDemolitionEndingChoice.Refuse)]
        [TestCase(BeforeDemolitionEndingChoice.Sign)]
        public void RetryBeforeChoiceIsDetachedAndStillPassesStrictSaveValidation(BeforeDemolitionEndingChoice choice)
        {
            At(5,4,choice);
            string before = JsonUtility.ToJson(data);
            var copy = BeforeDemolitionMirrorFlow.PrepareChoiceRetrySnapshot(data);
            Assert.That(JsonUtility.ToJson(data), Is.EqualTo(before));
            Assert.That(copy, Is.Not.SameAs(data));
            File.WriteAllText(path,JsonUtility.ToJson(copy));
            Assert.That(SaveSystem.TryRead(path,out _), Is.True);
            var state = BeforeDemolitionMirrorFlow.State.Parse(copy.objects.Find(o=>o.id==BeforeDemolitionMirrorFlow.StateId).json);
            Assert.That(state.stage, Is.EqualTo(2));
            Assert.That(state.choice, Is.EqualTo(BeforeDemolitionEndingChoice.None));
            Assert.That(copy.narrative.endingSeen, Is.False);
            Assert.That(copy.narrative.sequences, Does.Not.Contain(BeforeDemolitionMirrorFlow.RefuseSequence));
            Assert.That(copy.narrative.sequences, Does.Not.Contain(BeforeDemolitionMirrorFlow.SignSequence));
        }
        void Add(string id) { if (!data.inventory.Any(item => item.id == id)) data.inventory.Add(new InventoryItem { id = id }); }
        void Remove(string id) => data.inventory.RemoveAll(item => item.id == id);
        void Put(string id, string json)
        {
            data.objects.RemoveAll(item => item.id == id);
            data.objects.Add(new SavedObject { id = id, json = json });
        }
        bool Read()
        {
            File.WriteAllText(path, JsonUtility.ToJson(data));
            return SaveSystem.TryRead(path, out _);
        }

        [TestCase(3, 0)][TestCase(3, 1)][TestCase(3, 2)][TestCase(3, 3)]
        [TestCase(4, 0)][TestCase(4, 1)][TestCase(4, 2)][TestCase(4, 3)]
        [TestCase(5, 0)][TestCase(5, 1)][TestCase(5, 2)][TestCase(5, 3)][TestCase(5, 4)]
        public void EveryLaterCheckpointPreservesTheCompletedCampaignPrefix(int chapter, int count)
        {
            At(chapter, count);
            Assert.That(Read(), Is.True, Chapters[chapter] + ":" + count);
        }

        [TestCase(BeforeDemolitionEndingChoice.Refuse)][TestCase(BeforeDemolitionEndingChoice.Sign)]
        public void BothCompletedEndingsRoundTripWithTheChosenBranch(BeforeDemolitionEndingChoice choice)
        {
            At(5, 4, choice);
            Assert.That(Read(), Is.True);
            Assert.That(SaveSystem.TryRead(path, out var restored), Is.True);
            var mirror = BeforeDemolitionMirrorFlow.State.Parse(restored.objects.Single(item => item.id == BeforeDemolitionMirrorFlow.StateId).json);
            Assert.That(mirror.choice, Is.EqualTo(choice));
            Assert.That(restored.narrative.endingSeen, Is.True);
            Assert.That(restored.narrative.pendingChapterId, Is.Empty);
        }

        [TestCase("bd.marta.notebook")][TestCase("bd.caio.cap")][TestCase("bd.luan.lunch")]
        [TestCase("bd.bia.tape")][TestCase("bd.bia.tape.heard")]
        public void GatheredMilestoneRequiresEveryStudentObjectAndTheFullBiaTape(string missing)
        {
            At(3, 1); Remove(missing);
            Assert.That(Read(), Is.False, missing);
        }

        [TestCase(3)][TestCase(4)][TestCase(5)]
        public void EveryLaterChapterStillRequiresTheSchoolEvidenceAndEmails(int chapter)
        {
            At(chapter, 0); Remove(BeforeDemolitionSchoolFlow.ReportId);
            Assert.That(Read(), Is.False);
            At(chapter, 0); Remove("bd.note.email.06");
            Assert.That(Read(), Is.False);
        }

        [TestCase(3)][TestCase(4)]
        public void AnIncompleteRecordingMayBeKeptWithoutItsHeardMarker(int chapter)
        {
            At(chapter, chapter == 4 ? 1 : 0);
            Add(chapter == 4 ? BeforeDemolitionTwoVoicesFlow.TapeId : BeforeDemolitionFourStudentsFlow.BiaTapeId);
            Assert.That(Read(), Is.True);
        }

        [Test]
        public void CompletingTheIntercomDoesNotImplyThatTheStairKeyWasPickedUp()
        {
            At(3, 1); data.narrative.sequences.Add("bd.four.intercom");
            Assert.That(Read(), Is.True);
            Assert.That(data.inventory.Any(item => item.id == BeforeDemolitionFourStudentsFlow.StairKeyId), Is.False);
            Assert.That(data.narrative.milestones, Does.Not.Contain("bd.four.key"));
        }

        [TestCase("early-key")][TestCase("missing-key")][TestCase("missing-intercom")]
        [TestCase("early-intercom")][TestCase("heard-without-tape")][TestCase("future-voice")]
        public void FourStudentEvidenceCannotSkipTheIntercomOrExplicitKeyPickup(string fault)
        {
            At(3, fault == "missing-key" || fault == "missing-intercom" ? 2 : 0);
            switch (fault)
            {
                case "early-key": Add(BeforeDemolitionFourStudentsFlow.StairKeyId); break;
                case "missing-key": Remove(BeforeDemolitionFourStudentsFlow.StairKeyId); break;
                case "missing-intercom": data.narrative.sequences.Remove("bd.four.intercom"); break;
                case "early-intercom": data.narrative.sequences.Add("bd.four.intercom"); break;
                case "heard-without-tape": Add(BeforeDemolitionFourStudentsFlow.BiaTapeHeardId); break;
                case "future-voice": data.narrative.sequences.Add("bd.loop.voice"); break;
            }
            Assert.That(Read(), Is.False, fault);
        }

        [TestCase("missing-key")][TestCase("early-tape")][TestCase("early-heard")]
        [TestCase("missing-heard")][TestCase("missing-tape")][TestCase("loop-negative")]
        [TestCase("loop-overflow")][TestCase("missing-loop-record")][TestCase("empty-loop-state")]
        public void CorridorCheckpointsRequireTheirEntryContextAndConsistentRecordingState(string fault)
        {
            At(4, fault == "missing-heard" || fault == "missing-tape" ? 2 : 0);
            switch (fault)
            {
                case "missing-key": Remove(BeforeDemolitionFourStudentsFlow.StairKeyId); break;
                case "early-tape": Add(BeforeDemolitionTwoVoicesFlow.TapeId); break;
                case "early-heard": Add(BeforeDemolitionTwoVoicesFlow.TapeId); Add(BeforeDemolitionTwoVoicesFlow.TapeHeardId); break;
                case "missing-heard": Remove(BeforeDemolitionTwoVoicesFlow.TapeHeardId); break;
                case "missing-tape": Remove(BeforeDemolitionTwoVoicesFlow.TapeId); break;
                case "loop-negative": Put(BeforeDemolitionTwoVoicesFlow.StateId, "{\"version\":1,\"loopCount\":-1}"); break;
                case "loop-overflow": Put(BeforeDemolitionTwoVoicesFlow.StateId, "{\"version\":1,\"loopCount\":4}"); break;
                case "missing-loop-record": data.objects.RemoveAll(item => item.id == BeforeDemolitionTwoVoicesFlow.StateId); break;
                case "empty-loop-state": Put(BeforeDemolitionTwoVoicesFlow.StateId, "{}"); break;
            }
            Assert.That(Read(), Is.False, fault);
        }

        [TestCase("stage-mismatch")][TestCase("choice-before-offer")][TestCase("missing-offer")]
        [TestCase("wrong-ending-voice")][TestCase("both-endings")][TestCase("early-ending-seen")]
        [TestCase("ending-unset")][TestCase("ending-pending")][TestCase("missing-mirror-record")]
        [TestCase("empty-mirror-state")]
        public void MirrorCheckpointsKeepStageChoiceAndEndingPresentationConsistent(string fault)
        {
            int count = fault == "stage-mismatch" || fault == "choice-before-offer" ? 2
                : fault == "missing-offer" || fault == "early-ending-seen" ? 3 : 4;
            At(5, count);
            switch (fault)
            {
                case "stage-mismatch": Put(BeforeDemolitionMirrorFlow.StateId, "{\"version\":1,\"stage\":1,\"choice\":0}"); break;
                case "choice-before-offer": Put(BeforeDemolitionMirrorFlow.StateId, "{\"version\":1,\"stage\":2,\"choice\":1}"); break;
                case "missing-offer": data.narrative.sequences.Remove("bd.mirror.offer"); break;
                case "wrong-ending-voice": data.narrative.sequences.Remove("bd.ending.refuse"); data.narrative.sequences.Add("bd.ending.sign"); break;
                case "both-endings": data.narrative.sequences.Add("bd.ending.sign"); break;
                case "early-ending-seen": data.narrative.endingSeen = true; break;
                case "ending-unset": data.narrative.endingSeen = false; break;
                case "ending-pending": data.narrative.pendingChapterId = BeforeDemolitionCampaign.OfficeChapterId; break;
                case "missing-mirror-record": data.objects.RemoveAll(item => item.id == BeforeDemolitionMirrorFlow.StateId); break;
                case "empty-mirror-state": Put(BeforeDemolitionMirrorFlow.StateId, "{}"); break;
            }
            Assert.That(Read(), Is.False, fault);
        }

        [TestCase("prefix")][TestCase("milestone-order")][TestCase("pending")][TestCase("chapter-order")]
        [TestCase("duplicate-sequence")][TestCase("unknown-sequence")][TestCase("duplicate-object")]
        [TestCase("wrong-scene")][TestCase("old-story")]
        public void LaterCheckpointsRejectCrossChapterOrMalformedProgress(string fault)
        {
            At(4, 1);
            switch (fault)
            {
                case "prefix": data.narrative.milestones[5] = "old.school"; break;
                case "milestone-order": data.narrative.milestones[15] = "bd.four.key"; break;
                case "pending": data.narrative.pendingChapterId = BeforeDemolitionCampaign.MirrorChapterId; break;
                case "chapter-order": data.narrative.completedChapters.Reverse(); break;
                case "duplicate-sequence": data.narrative.sequences.Add("bd.four.intercom"); break;
                case "unknown-sequence": data.narrative.sequences.Add("old.phone"); break;
                case "duplicate-object": data.objects.Add(data.objects[0]); break;
                case "wrong-scene": data.scene = "Chapter1Office"; break;
                case "old-story": data.narrative.storyRevision = 2; break;
            }
            Assert.That(Read(), Is.False, fault);
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.TestSaveDirectory = previousDirectory;
            if (catalog) UnityEngine.Object.DestroyImmediate(catalog);
            if (!Directory.Exists(directory)) return;
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
}
