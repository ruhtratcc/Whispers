// Reúne capítulos, marcos, falas e sequências, com buscas por ID.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame
{
    [Serializable] public sealed class StoryLine
    {
        public string speaker;
        [TextArea(2, 5)] public string text;
        [Min(.1f)] public float seconds = 3;
        public AudioClip voice;
        public string demonVoiceId;
        [NonSerialized] public float playbackSeconds;
        public float Duration => playbackSeconds>0 ? playbackSeconds : voice ? Mathf.Max(.1f, voice.length + .2f) : Mathf.Max(.1f, seconds);
    }
    [Serializable] public sealed class StorySequenceDefinition
    {
        public string id;
        public bool lockPlayer = true;
        public string completionSignal;
        public StoryLine[] lines = Array.Empty<StoryLine>();
    }
    [Serializable] public sealed class StoryMilestoneDefinition
    {
        public string id;
        [TextArea] public string objectiveAfter;
        public string automaticSequenceId;
        public string[] requiredItemIds = Array.Empty<string>();
    }
    [Serializable] public sealed class StoryChapterDefinition
    {
        public string id, title, heading;
        [Tooltip("Existing enabled scene name; leave empty until its map exists.")] public string sceneName;
        [TextArea] public string initialObjective, completionPhrase;
        public StoryMilestoneDefinition[] milestones = Array.Empty<StoryMilestoneDefinition>();
    }
    [CreateAssetMenu(menuName = "Whispers/Story Book")]
    public sealed class StoryBook : ScriptableObject
    {
        public string storyId;
        public int revision = 1;
        public StoryChapterDefinition[] chapters = Array.Empty<StoryChapterDefinition>();
        public StorySequenceDefinition[] sequences = Array.Empty<StorySequenceDefinition>();
        public int ChapterIndex(string id)
        {
            if (chapters == null) return -1;
            for (int i = 0; i < chapters.Length; i++)
                if (chapters[i] != null && chapters[i].id == id) return i;
            return -1;
        }
        public StoryChapterDefinition Chapter(string id)
        {
            int index = ChapterIndex(id);
            return index < 0 ? null : chapters[index];
        }
        public StorySequenceDefinition Sequence(string id)
        {
            foreach (var sequence in sequences ?? Array.Empty<StorySequenceDefinition>())
                if (sequence != null && sequence.id == id) return sequence;
            return null;
        }
        public string[] ValidateContent()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>();
            var milestones = new HashSet<string>();
            foreach (var chapter in chapters ?? Array.Empty<StoryChapterDefinition>())
            {
                if (chapter == null || string.IsNullOrWhiteSpace(chapter.id) || !ids.Add(chapter.id)) { errors.Add("Capítulo sem ID único."); continue; }
                foreach (var milestone in chapter.milestones ?? Array.Empty<StoryMilestoneDefinition>())
                    if (milestone == null || string.IsNullOrWhiteSpace(milestone.id) || !milestones.Add(milestone.id)) errors.Add("Marco sem ID único em " + chapter.id);
            }
            ids.Clear();
            foreach (var sequence in sequences ?? Array.Empty<StorySequenceDefinition>())
            {
                if (sequence == null || string.IsNullOrWhiteSpace(sequence.id) || !ids.Add(sequence.id)) { errors.Add("Sequência sem ID único."); continue; }
                if (!string.IsNullOrEmpty(sequence.completionSignal) && !milestones.Contains(sequence.completionSignal)) errors.Add("Sequência com destino desconhecido: " + sequence.id);
            }
            foreach (var chapter in chapters ?? Array.Empty<StoryChapterDefinition>())
            foreach (var milestone in chapter?.milestones ?? Array.Empty<StoryMilestoneDefinition>())
                if (!string.IsNullOrEmpty(milestone.automaticSequenceId) && Sequence(milestone.automaticSequenceId) == null) errors.Add("Sequência automática inexistente: " + milestone.automaticSequenceId);
            return errors.ToArray();
        }
    }
}
