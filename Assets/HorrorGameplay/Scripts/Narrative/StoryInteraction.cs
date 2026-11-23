// Liga uma interação genérica a um evento narrativo.

using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    /// <summary>Map-ready inscription, recording, telephone or trigger hook. Captions only.</summary>
    public sealed class StoryInteraction : MonoBehaviour, IInteractable
    {
        public NarrativeDirector director;
        [Tooltip("Use a sequence ID for dialogue, otherwise a milestone ID.")]
        public string sequenceId, signalId;
        public string requiredSignal;
        public string prompt = "Investigar";
        public UnityEvent onActivated = new UnityEvent();
        public string Prompt(PlayerInteraction player) => prompt;
        public bool CanInteract(PlayerInteraction player)
        {
            if (!director || !director.IsInitialized || (!string.IsNullOrEmpty(requiredSignal) && !director.HasSignal(requiredSignal))) return false;
            if (!string.IsNullOrEmpty(sequenceId))
            {
                var sequence = director.book.Sequence(sequenceId);
                return sequence != null && !director.HasSequence(sequenceId) && !director.SequencePlaying
                    && (string.IsNullOrEmpty(sequence.completionSignal) || director.NextSignal == sequence.completionSignal);
            }
            return director.NextSignal == signalId;
        }
        public void Interact(PlayerInteraction player) { Activate(); }
        public void Activate()
        {
            if (!CanInteract(null)) return;
            bool accepted = string.IsNullOrEmpty(sequenceId) ? director.Signal(signalId) : director.PlaySequence(sequenceId);
            if (accepted) onActivated.Invoke();
        }
    }
}
