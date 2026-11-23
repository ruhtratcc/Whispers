// Liga o telefone do cenário a uma ação narrativa.

using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    public sealed class StoryTelephone : MonoBehaviour, IInteractable
    {
        public PrologueFlow flow;
        public SoundSet ringSound;
        [Min(.2f)] public float ringInterval = 2.8f;
        [Range(0, 1)] public float ringVolume = .8f;
        public UnityEvent onRingStarted = new UnityEvent(), onRingStopped = new UnityEvent(), onAnswered = new UnityEvent();
        [Header("Receiver handling")]
        [Tooltip("Short handset/plastic contact when answering. Played once per accepted call.")]
        public SoundSet pickupSound;
        [Tooltip("Handset settling in its cradle after the complete conversation.")]
        public SoundSet hangupSound;
        [Range(0, 1)] public float receiverVolume = .75f;
        public UnityEvent onReceiverLifted = new UnityEvent(), onReceiverPlaced = new UnityEvent();
        public bool IsRinging { get; private set; }
        public bool ReceiverLifted { get; private set; }
        AudioSource currentRing;
        NarrativeDirector subscribedDirector;
        string heldSequence = "";
        bool OwnsCurrentRing => currentRing && ringSound && ringSound.clips != null
            && System.Array.Exists(ringSound.clips, clip => clip == currentRing.clip);
        float nextRing;
        public string Prompt(PlayerInteraction player) => "Atender";
        public bool CanInteract(PlayerInteraction player) => IsRinging && flow && flow.Ringing;
        public void Interact(PlayerInteraction player)
        {
            BindDirector();
            if (!flow || !flow.Answer()) return;
            LiftReceiver(flow.AvailableSequence);
            onAnswered.Invoke();
        }
        void OnEnable() => BindDirector();
        void BindDirector()
        {
            var director = flow ? flow.director : null;
            if (subscribedDirector == director) return;
            Unsubscribe();
            subscribedDirector = director;
            if (!subscribedDirector) return;
            subscribedDirector.Initialized += ClearReceiver;
            subscribedDirector.SequenceStarted += SequenceStarted;
            subscribedDirector.SequenceCompleted += SequenceCompleted;
        }
        void Unsubscribe()
        {
            if (subscribedDirector)
            {
                subscribedDirector.Initialized -= ClearReceiver;
                subscribedDirector.SequenceStarted -= SequenceStarted;
                subscribedDirector.SequenceCompleted -= SequenceCompleted;
            }
            subscribedDirector = null;
            ClearReceiver();
        }
        void SequenceStarted(string id)
        {
            // Also cover callers that answer through PrologueFlow directly. This callback
            // follows Interact by one frame; the sequence ID prevents duplicate Foley.
            if (flow && !flow.Ringing && id == flow.AvailableSequence) LiftReceiver(id);
        }
        void LiftReceiver(string id)
        {
            if (string.IsNullOrEmpty(id) || (ReceiverLifted && heldSequence == id)) return;
            ReceiverLifted = true;
            heldSequence = id;
            PlayReceiver(pickupSound);
            onReceiverLifted.Invoke();
        }
        void SequenceCompleted(string id)
        {
            if (!ReceiverLifted || heldSequence != id) return;
            var game = subscribedDirector ? subscribedDirector.game : null;
            ClearReceiver();
            // Reload, death and deactivation cancel silently rather than faking a hangup.
            if (!game || (game.State != GameState.Playing && game.State != GameState.Cutscene)) return;
            PlayReceiver(hangupSound);
            onReceiverPlaced.Invoke();
        }
        void PlayReceiver(SoundSet sound)
        {
            if (sound && AudioManager.Instance)
                AudioManager.Instance.Play(sound, transform.position, receiverVolume, AudioChannel.SFX, true);
        }
        void ClearReceiver() { ReceiverLifted = false; heldSequence = ""; }
        public void SetRinging(bool enabled)
        {
            if (IsRinging == enabled) return;
            IsRinging = enabled; nextRing = 0;
            if (enabled) onRingStarted.Invoke();
            else { if (OwnsCurrentRing) currentRing.Stop(); currentRing = null; onRingStopped.Invoke(); }
        }
        void Update()
        {
            BindDirector();
            if (ReceiverLifted && subscribedDirector && subscribedDirector.game
                && subscribedDirector.game.State == GameState.GameOver) ClearReceiver();
            if (!IsRinging || AudioListener.pause || !ringSound || !AudioManager.Instance) return;
            nextRing -= Time.deltaTime;
            if (nextRing > 0) return;
            if (!OwnsCurrentRing || !currentRing.isPlaying)
                currentRing = AudioManager.Instance.Play(ringSound, transform.position, ringVolume, AudioChannel.SFX, true);
            nextRing = ringInterval;
        }
        void OnDisable() { SetRinging(false); Unsubscribe(); }
    }
}
