// Guarda os eventos cumpridos e verifica qual ação vem em seguida.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame
{
    [Serializable]
    public sealed class NarrativeProgress
    {
        public string storyId;
        public int storyRevision;
        public string chapterId, pendingChapterId;
        public bool endingSeen;
        public List<string> milestones = new List<string>();
        public List<string> sequences = new List<string>();
        public List<string> completedChapters = new List<string>();
        public NarrativeProgress Copy() => JsonUtility.FromJson<NarrativeProgress>(JsonUtility.ToJson(this));
        public bool HasSignal(string id) => milestones != null && milestones.Contains(id);
        public bool HasSequence(string id) => sequences != null && sequences.Contains(id);
        public StoryMilestoneDefinition Next(StoryBook book)
        {
            var chapter = book ? book.Chapter(chapterId) : null;
            return NextMilestone(chapter);
        }
        StoryMilestoneDefinition NextMilestone(StoryChapterDefinition chapter)
        {
            foreach (var milestone in chapter?.milestones ?? Array.Empty<StoryMilestoneDefinition>())
                if (milestone != null && !HasSignal(milestone.id)) return milestone;
            return null;
        }
        public bool IsChapterComplete(StoryBook book)
        {
            var chapter = book ? book.Chapter(chapterId) : null;
            return chapter?.milestones?.Length > 0 && NextMilestone(chapter) == null;
        }
        public bool TryComplete(StoryBook book, string id, Func<string, bool> hasItem)
        {
            var next = Next(book);
            if (next == null || next.id != id) return false;
            foreach (string item in next.requiredItemIds ?? Array.Empty<string>())
                if (hasItem == null || !hasItem(item)) return false;
            (milestones ??= new List<string>()).Add(id);
            return true;
        }
        public bool CompleteSequence(string id)
        {
            if (string.IsNullOrEmpty(id) || HasSequence(id)) return false;
            (sequences ??= new List<string>()).Add(id);
            return true;
        }
        public bool CompleteChapter(StoryBook book)
        {
            if (!IsChapterComplete(book)) return false;
            if (completedChapters == null) completedChapters = new List<string>();
            bool added = !completedChapters.Contains(chapterId);
            if (added) completedChapters.Add(chapterId);
            int index = book.ChapterIndex(chapterId);
            if (index + 1 < book.chapters.Length) pendingChapterId = book.chapters[index + 1].id;
            else { pendingChapterId = ""; endingSeen = true; }
            return added;
        }
        public bool EnterPendingChapter(StoryBook book)
        {
            if (!book || !IsChapterComplete(book) || string.IsNullOrEmpty(pendingChapterId) || book.ChapterIndex(pendingChapterId) != book.ChapterIndex(chapterId) + 1) return false;
            chapterId = pendingChapterId; pendingChapterId = ""; return true;
        }
    }
}
