// Liga eventos no ambiente a marcos da história.

using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    public sealed class StoryWorldEvent : MonoBehaviour
    {
        public NarrativeDirector director;
        public string signalId;
        [Tooltip("One-time action when this milestone is reached (sound, scare, chase).")]
        public UnityEvent onTriggered = new UnityEvent();
        [Tooltip("Restore persistent map state silently after a checkpoint is loaded.")]
        public UnityEvent onRestoreCompleted = new UnityEvent(), onRestorePending = new UnityEvent();
        void OnEnable()
        {
            if (!director) return;
            director.SignalAccepted += Signal;
            director.Initialized += Restore;
            if (director.IsInitialized) Restore();
        }
        void Signal(string id) { if (id == signalId) onTriggered.Invoke(); }
        void Restore()
        {
            if (director.HasSignal(signalId)) onRestoreCompleted.Invoke();
            else onRestorePending.Invoke();
        }
        void OnDisable()
        {
            if (!director) return;
            director.SignalAccepted -= Signal;
            director.Initialized -= Restore;
        }
    }
}
