// Coordena o prólogo alternativo do escritório.

using UnityEngine;

namespace HorrorGame
{
    public sealed class PrologueFlow : MonoBehaviour
    {
        public NarrativeDirector director;
        public StoryTelephone telephone;
        [Min(0)] public float firstCallDelay = 6;
        [Min(0)] public float secondCallDelay = 4;
        public bool Ringing { get; private set; }
        public string AvailableSequence { get; private set; }
        float elapsed;
        bool initialized;
        void OnEnable()
        {
            if (!director) return;
            director.Initialized += Initialize;
            director.SequenceCompleted += SequenceCompleted;
            if (director.IsInitialized) Initialize();
        }
        void Initialize()
        {
            initialized = director && director.Progress.chapterId == "prologue";
            elapsed = 0;
            Ringing = false;
            AvailableSequence = "";
            if (telephone) telephone.SetRinging(false);
        }
        void SequenceCompleted(string id) { if (id == "prologue.call-one") elapsed = 0; }
        void Update()
        {
            if (!initialized || !director || !director.game || director.game.State != GameState.Playing
                || director.SequencePlaying || Ringing || director.HasSignal("prologue.second-call")) return;
            elapsed += Time.deltaTime;
            bool second = director.HasSignal("prologue.first-call");
            if (elapsed < (second ? secondCallDelay : firstCallDelay)) return;
            AvailableSequence = second ? "prologue.call-two" : "prologue.call-one";
            Ringing = true;
            if (telephone) telephone.SetRinging(true);
            if (director.game.hud) director.game.hud.ShowObjective("Atender o telefone.");
        }
        public bool Answer()
        {
            if (!Ringing || !director || !director.game || !director.game.CanControl) return false;
            if (!director.PlaySequence(AvailableSequence)) return false;
            Ringing = false;
            if (telephone) telephone.SetRinging(false);
            return true;
        }
        void OnDisable()
        {
            if (director) { director.Initialized -= Initialize; director.SequenceCompleted -= SequenceCompleted; }
            if (telephone) telephone.SetRinging(false);
            initialized = Ringing = false;
        }
    }
}
