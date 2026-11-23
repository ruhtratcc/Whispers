// Identifica a campanha atual, seus capítulos e as regras de compatibilidade de progresso.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame
{
    public static class BeforeDemolitionCampaign
    {
        public const string StoryId = "before-demolition";
        public const int Revision = 3;
        public const string OfficeChapterId = "bd.office";
        public const string RoadChapterId = "bd.road";
        public const string SchoolChapterId = "bd.secretary-library";
        public const string FourChapterId = "bd.four-students", TwoVoicesChapterId = "bd.two-voices", MirrorChapterId = "bd.mirror-report";
        public const string ReadySignal = "bd.office.ready";
        public const string ExitSignal = "bd.office.exit";
        public const string WhistleItemId = "bd.whistle";
        public const string FlowStateId = "bd.office.flow";
        public const string CheckpointFile = "before-demolition-save.json";
        public static readonly string[] RoadSignals = { "bd.road.radio-changed", "bd.road.car-parked", "bd.road.flashlight-taken", "bd.road.car-exited", "bd.road.school-gate" };
        public static readonly string[] SchoolSignals = { "bd.school.secretary", "bd.school.cups", "bd.school.lunchbox", "bd.school.key", "bd.school.library", "bd.school.report", "bd.school.tape", "bd.school.exit" };

        // Identity selects storage even if its content revision is invalid; it must never fall back to legacy data.
        public static bool Is(StoryBook book) => book && book.storyId == StoryId;
        public static string Key(StoryBook book, string suffix) => (Is(book) ? "BeforeDemolition." : "Game.") + suffix;
        public static string FileName(StoryBook book, string configured) => Is(book) ? CheckpointFile : configured;

        [Serializable] sealed class OfficeState { public EmailState emails; }
        [Serializable] sealed class EmailState
        {
            public string storyId;
            public int version;
            public List<string> deliveredIds, readIds;
        }

        public static bool IsSchoolChapter(string id) => id == SchoolChapterId || id == FourChapterId || id == TwoVoicesChapterId || id == MirrorChapterId;
        internal static bool ValidCheckpoint(SaveData data, HashSet<string> items) => data.narrative != null && (data.narrative.chapterId == FourChapterId || data.narrative.chapterId == TwoVoicesChapterId || data.narrative.chapterId == MirrorChapterId)
            ? ValidLaterCheckpoint(data, items) : data.narrative?.chapterId == SchoolChapterId
            ? ValidSchoolCheckpoint(data, items) : data.narrative?.chapterId == RoadChapterId
            ? ValidRoadCheckpoint(data, items) : ValidOfficeCheckpoint(data, items);

        static bool ValidLaterCheckpoint(SaveData data, HashSet<string> items)
        {
            var p = data.narrative;
            string[] chapters = { OfficeChapterId, RoadChapterId, SchoolChapterId, FourChapterId, TwoVoicesChapterId, MirrorChapterId };
            string[] four = { "bd.four.gathered", "bd.four.key", "bd.four.upstairs" };
            string[] loop = { "bd.loop.escaped", "bd.loop.tape", "bd.loop.mirror" };
            string[] mirror = { "bd.mirror.seen", "bd.mirror.broken", "bd.mirror.choice", "bd.mirror.finish" };
            int chapter = Array.IndexOf(chapters, p.chapterId);
            if (data.version != 2 || data.scene != "SchoolCEEP" || p.storyId != StoryId || p.storyRevision != Revision
                || chapter < 3 || p.milestones == null || p.completedChapters == null || p.sequences == null || data.objects == null) return false;
            var expected = new List<string> { ReadySignal, ExitSignal };
            expected.AddRange(RoadSignals); expected.AddRange(SchoolSignals);
            if (chapter > 3) expected.AddRange(four);
            if (chapter > 4) expected.AddRange(loop);
            string[] current = chapter == 3 ? four : chapter == 4 ? loop : mirror;
            int count = p.milestones.Count - expected.Count;
            if (count < 0 || count > current.Length) return false;
            for (int i = 0; i < count; i++) expected.Add(current[i]);
            for (int i = 0; i < expected.Count; i++) if (p.milestones[i] != expected[i]) return false;
            bool completed = count == current.Length, ended = completed && chapter == 5;
            if (p.endingSeen != ended || p.completedChapters.Count != chapter + (completed ? 1 : 0)) return false;
            for (int i = 0; i < p.completedChapters.Count; i++) if (p.completedChapters[i] != chapters[i]) return false;
            string pending = completed && chapter < 5 ? chapters[chapter + 1] : "";
            if ((p.pendingChapterId ?? "") != pending) return false;
            var objects = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in data.objects) if (entry == null || string.IsNullOrWhiteSpace(entry.id) || objects.ContainsKey(entry.id)) return false; else objects.Add(entry.id, entry.json);
            if (!objects.TryGetValue(BeforeDemolitionSchoolFlow.StateId, out string schoolJson)) return false;
            BeforeDemolitionSchoolState school;
            try { school = BeforeDemolitionSchoolState.Parse(schoolJson); } catch (ArgumentException) { return false; }
            if (school == null || !school.ValidFields || school.emails == null
                || !ValidEmailData(school.emails.storyId, school.emails.version, school.emails.deliveredIds, school.emails.readIds, school.version == 1)) return false;
            foreach (string id in new[] { "email.01", "email.06", "email.07" }) if (!school.emails.IsRead(id)) return false;
            foreach (string id in school.emails.readIds) if (!items.Contains("bd.note." + id)) return false;
            foreach (string id in new[] { WhistleItemId, BeforeDemolitionRoadFlow.EmailDeliveryItemId, BeforeDemolitionSchoolFlow.KeyId, BeforeDemolitionSchoolFlow.ReportId, BeforeDemolitionSchoolFlow.TapeId, BeforeDemolitionSchoolFlow.TapeHeardId }) if (!items.Contains(id)) return false;
            bool gathered = chapter > 3 || count >= 1, key = chapter > 3 || count >= 2;
            bool escaped = chapter > 4 || chapter == 4 && count >= 1, heard = chapter > 4 || chapter == 4 && count >= 2;
            if (gathered)
            {
                foreach (string id in BeforeDemolitionFourStudentsFlow.StudentItemIds) if (!items.Contains(id)) return false;
                if (!items.Contains(BeforeDemolitionFourStudentsFlow.BiaTapeHeardId)) return false;
            }
            if (items.Contains(BeforeDemolitionFourStudentsFlow.BiaTapeHeardId) && !items.Contains(BeforeDemolitionFourStudentsFlow.BiaTapeId)
                || items.Contains(BeforeDemolitionFourStudentsFlow.StairKeyId) != key
                || items.Contains(BeforeDemolitionTwoVoicesFlow.TapeHeardId) != heard
                || items.Contains(BeforeDemolitionTwoVoicesFlow.TapeId) && !escaped
                || heard && !items.Contains(BeforeDemolitionTwoVoicesFlow.TapeId)) return false;
            var sequences = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in p.sequences)
                if (id != "bd.four.intercom" && id != "bd.loop.voice" && id != "bd.loop.phone" && id != "bd.mirror.offer" && id != "bd.ending.refuse" && id != "bd.ending.sign" || !sequences.Add(id)) return false;
            if (sequences.Contains("bd.four.intercom") && !gathered || key && !sequences.Contains("bd.four.intercom")
                || chapter < 4 && (sequences.Contains("bd.loop.voice") || sequences.Contains("bd.loop.phone"))) return false;
            if (!objects.TryGetValue(BeforeDemolitionTwoVoicesFlow.StateId, out string loopJson) || !BeforeDemolitionTwoVoicesFlow.ValidState(loopJson)
                || !objects.TryGetValue(BeforeDemolitionMirrorFlow.StateId, out string mirrorJson)) return false;
            if (chapter < 4 && (BeforeDemolitionTwoVoicesState.Parse(loopJson).loopCount != 0 || items.Contains(BeforeDemolitionTwoVoicesFlow.TapeId))) return false;
            BeforeDemolitionMirrorFlow.State state;
            try { state = BeforeDemolitionMirrorFlow.State.Parse(mirrorJson); } catch (ArgumentException) { return false; }
            if (state.stage != (chapter == 5 ? count : 0)) return false;
            bool offer = sequences.Contains("bd.mirror.offer"), refuse = sequences.Contains("bd.ending.refuse"), sign = sequences.Contains("bd.ending.sign");
            if (offer && (chapter < 5 || count < 2) || chapter == 5 && count >= 3 && !offer
                || refuse && state.choice != BeforeDemolitionEndingChoice.Refuse || sign && state.choice != BeforeDemolitionEndingChoice.Sign
                || ended && !(refuse || sign)) return false;
            return true;
        }

        internal static bool ValidSchoolCheckpoint(SaveData data, HashSet<string> items)
        {
            var p = data.narrative;
            if (data.version != 2 || data.scene != "SchoolCEEP" || p == null || p.chapterId != SchoolChapterId || p.storyId != StoryId || p.storyRevision != Revision
                || p.endingSeen || p.milestones == null || p.completedChapters == null || p.sequences == null || p.sequences.Count != 0
                || data.objects == null || !items.Contains(WhistleItemId) || !items.Contains(BeforeDemolitionRoadFlow.EmailDeliveryItemId)
                || p.milestones.Count < 7 || p.milestones.Count > 15 || p.milestones[0] != ReadySignal || p.milestones[1] != ExitSignal) return false;
            for (int i = 0; i < RoadSignals.Length; i++) if (p.milestones[i + 2] != RoadSignals[i]) return false;
            int count = p.milestones.Count - 7;
            for (int i = 0; i < count; i++) if (p.milestones[i + 7] != SchoolSignals[i]) return false;
            bool complete = count == SchoolSignals.Length;
            if (p.completedChapters.Count != (complete ? 3 : 2) || p.completedChapters[0] != OfficeChapterId || p.completedChapters[1] != RoadChapterId
                || complete && (p.completedChapters[2] != SchoolChapterId || p.pendingChapterId != "bd.four-students")
                || !complete && !string.IsNullOrEmpty(p.pendingChapterId)) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal); BeforeDemolitionSchoolState state = null;
            foreach (var saved in data.objects)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.id) || !ids.Add(saved.id)) return false;
                if (saved.id == BeforeDemolitionSchoolFlow.StateId)
                    try { state = BeforeDemolitionSchoolState.Parse(saved.json); } catch (ArgumentException) { return false; }
            }
            if (state == null || !state.ValidFields || state.emails == null
                || !ValidEmailData(state.emails.storyId, state.emails.version, state.emails.deliveredIds, state.emails.readIds, state.version == 1)
                || state.phoneAnswered && count < 6) return false;
            foreach (string id in new[] { "email.01", "email.06", "email.07" }) if (!state.emails.IsRead(id)) return false;
            foreach (string id in state.emails.readIds) if (!items.Contains("bd.note." + id)) return false;
            return items.Contains(BeforeDemolitionSchoolFlow.KeyId) == (count >= 4)
                && items.Contains(BeforeDemolitionSchoolFlow.ReportId) == (count >= 6)
                && items.Contains(BeforeDemolitionSchoolFlow.TapeHeardId) == (count >= 7);
        }

        static bool ValidRoadCheckpoint(SaveData data, HashSet<string> items)
        {
            var p = data.narrative;
            if (data.version != 2 || data.scene != "RoadJourney" || p == null || p.storyId != StoryId || p.storyRevision != Revision
                || p.chapterId != RoadChapterId || p.endingSeen || p.milestones == null || p.completedChapters == null
                || p.sequences == null || p.sequences.Count != 0 || data.objects == null || !items.Contains(WhistleItemId)
                || p.milestones.Count < 2 || p.milestones.Count > 7 || p.milestones[0] != ReadySignal || p.milestones[1] != ExitSignal) return false;
            int count = p.milestones.Count - 2;
            for (int i = 0; i < count; i++) if (p.milestones[i + 2] != RoadSignals[i]) return false;
            bool completed = count == 5;
            if (p.completedChapters.Count != (completed ? 2 : 1) || p.completedChapters[0] != OfficeChapterId
                || completed && (p.completedChapters[1] != RoadChapterId || p.pendingChapterId != "bd.secretary-library")
                || !completed && !string.IsNullOrEmpty(p.pendingChapterId)) return false;
            SavedObject flow = null; var objectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var saved in data.objects)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.id) || !objectIds.Add(saved.id)) return false;
                if (saved.id == BeforeDemolitionRoadFlow.StateId) flow = saved;
            }
            if (flow == null || string.IsNullOrWhiteSpace(flow.json)) return false;
            BeforeDemolitionRoadState state;
            try { state = BeforeDemolitionRoadState.Parse(flow.json); } catch (ArgumentException) { return false; }
            if (!state.ValidFields || state.emails == null) return false;
            bool deliveredEight = state.phase >= RoadRadioPhase.Song;
            if (!ValidEmailData(state.emails.storyId, state.emails.version, state.emails.deliveredIds, state.emails.readIds, state.version == 1 && deliveredEight)
                || deliveredEight != items.Contains(BeforeDemolitionRoadFlow.EmailDeliveryItemId)
                || deliveredEight != (count >= 1) || (state.phase == RoadRadioPhase.Parked) != (count >= 2)) return false;
            foreach (string id in new[] { "email.01", "email.06", "email.07" }) if (!state.emails.IsRead(id)) return false;
            foreach (string id in state.emails.readIds) if (!items.Contains("bd.note." + id)) return false;
            return true;
        }

        internal static bool ValidOfficeCheckpoint(SaveData data, HashSet<string> items)
        {
            var progress = data.narrative;
            if (data.version != 2 || data.scene != "Chapter1Office" || progress == null
                || progress.storyId != StoryId || progress.storyRevision != Revision
                || progress.chapterId != OfficeChapterId || progress.endingSeen
                || progress.milestones == null || progress.completedChapters == null
                || progress.sequences == null || progress.sequences.Count != 0 || data.objects == null) return false;
            var milestones = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in progress.milestones)
                if ((id != ReadySignal && id != ExitSignal) || !milestones.Add(id)) return false;
            if (progress.milestones.Count > 0 && progress.milestones[0] != ReadySignal) return false;
            if (progress.completedChapters.Count > 1
                || (progress.completedChapters.Count == 1 && progress.completedChapters[0] != OfficeChapterId)) return false;
            bool exited = milestones.Contains(ExitSignal);
            if (exited ? !milestones.Contains(ReadySignal) || progress.completedChapters.Count != 1
                || progress.pendingChapterId != RoadChapterId
                : progress.completedChapters.Count != 0 || !string.IsNullOrEmpty(progress.pendingChapterId)) return false;

            SavedObject flow = null;
            var objectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var saved in data.objects)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.id) || !objectIds.Add(saved.id)) return false;
                if (saved.id == FlowStateId) flow = saved;
            }
            if (flow == null || string.IsNullOrWhiteSpace(flow.json)) return false;
            OfficeState state;
            try
            {
                state = new OfficeState { emails = new EmailState() };
                JsonUtility.FromJsonOverwrite(flow.json, state);
            }
            catch (ArgumentException) { return false; }
            var emails = state.emails;
            if (emails == null || !ValidEmailData(emails.storyId, emails.version, emails.deliveredIds, emails.readIds, false)) return false;
            var read = new HashSet<string>(emails.readIds, StringComparer.Ordinal);
            return !milestones.Contains(ReadySignal) || items.Contains(WhistleItemId)
                && read.Contains("email.01") && read.Contains("email.06") && read.Contains("email.07");
        }

        static bool ValidEmailData(string storyId, int version, List<string> deliveredIds, List<string> readIds, bool eighth)
        {
            if (storyId != StoryId || version != 1 || deliveredIds == null || readIds == null || deliveredIds.Count != (eighth ? 8 : 7)) return false;
            var delivered = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in deliveredIds)
                if ((!InitialEmailId(id) && !(eighth && id == "email.08")) || !delivered.Add(id)) return false;
            var read = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in readIds) if (id == null || !delivered.Contains(id) || !read.Add(id)) return false;
            return true;
        }

        static bool InitialEmailId(string id) => id != null && id.Length == 8
            && id.StartsWith("email.0", StringComparison.Ordinal) && id[7] >= '1' && id[7] <= '7';
    }
}
