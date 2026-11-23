// Executa a ligação final do fluxo anterior.

using UnityEngine;

namespace HorrorGame
{
    /// <summary>Only an explicit interaction can start the final call.</summary>
    public sealed class SchoolFinalPhone : MonoBehaviour, IInteractable
    {
        public SchoolLastExitFlow flow;
        public InputReader input;
        public SoundSet ringSound, receiverSound;
        public bool useHeldInput = true;
        [Min(.1f)] public float answerDelay = .35f;
        float visibleAge;
        int activationFrame;
        bool notified;
        float ringRemaining;
        AudioSource currentRing, currentReceiver;
        AudioClip ringClip, receiverClip;
        GameSession subscribedGame;

        public float VisibleAge => visibleAge;
        public bool RingPlaying => currentRing && currentRing.clip == ringClip && currentRing.isPlaying;
        public bool ReadyToAnswer => isActiveAndEnabled && flow && flow.CanAnswerPhone
            && visibleAge >= answerDelay && Time.frameCount > activationFrame;

        void OnEnable()
        {
            subscribedGame = flow && flow.director ? flow.director.game : null;
            if (subscribedGame) subscribedGame.StateChanged += StateChanged;
            ResetForRestore();
        }
        public void ResetForRestore()
        {
            StopRing(); StopReceiver();
            visibleAge = 0;
            ringRemaining = 0;
            activationFrame = Time.frameCount;
            notified = false;
        }
        void Update()
        {
            if (!input && flow && flow.director && flow.director.game) input = flow.director.game.input;
            AdvanceInput(Time.deltaTime, useHeldInput && input && input.Interact);
        }
        /// <summary>The held phone uses the same action as world interaction, without requiring a collider.</summary>
        public void AdvanceInput(float deltaTime, bool interactPressed)
        {
            if (!isActiveAndEnabled || !flow || !flow.IsChapterActive || !flow.director.game
                || !flow.director.game.CanControl || deltaTime <= 0)
            { StopRing(); return; }
            visibleAge += deltaTime;
            if (!flow.CanAnswerPhone) { StopRing(); return; }
            if (!notified)
            {
                flow.director.game.hud?.Notify("[E] Atender telefone");
                notified = true;
            }
            if (ReadyToAnswer)
            {
                ringRemaining = Mathf.Max(0, ringRemaining - deltaTime);
                if (ringRemaining <= 0)
                {
                    if (!RingPlaying)
                    {
                        currentRing = AudioManager.Instance?.Play(ringSound, transform.position, .22f, AudioChannel.SFX, false);
                        ringClip = currentRing ? currentRing.clip : null;
                    }
                    ringRemaining = 3.6f;
                }
            }
            if (useHeldInput && interactPressed && ReadyToAnswer) Answer();
        }
        void Answer()
        {
            if (!flow || !flow.AnswerPhone()) return;
            StopRing();
            currentReceiver = AudioManager.Instance?.Play(receiverSound, transform.position, .22f, AudioChannel.SFX, false);
            receiverClip = currentReceiver ? currentReceiver.clip : null;
        }
        void StateChanged(GameState state)
        {
            if (state == GameState.Playing) return;
            StopRing(); ringRemaining = Mathf.Max(.35f, answerDelay);
            if (state == GameState.Paused || state == GameState.GameOver || state == GameState.StoryScreen) StopReceiver();
        }
        void StopRing()
        {
            // AudioManager lends pooled sources. Never stop another cue that has since reused the source.
            if (currentRing && currentRing.clip == ringClip) currentRing.Stop();
            currentRing = null; ringClip = null;
        }
        void StopReceiver()
        {
            if (currentReceiver && currentReceiver.clip == receiverClip) currentReceiver.Stop();
            currentReceiver = null; receiverClip = null;
        }
        void OnDisable()
        {
            if (subscribedGame) subscribedGame.StateChanged -= StateChanged;
            subscribedGame = null; StopRing(); StopReceiver();
        }
        public string Prompt(PlayerInteraction player) => "Atender o telefone";
        public bool CanInteract(PlayerInteraction player) => flow && flow.CanAnswerPhone && (!useHeldInput || ReadyToAnswer);
        public void Interact(PlayerInteraction player) { if (CanInteract(player)) Answer(); }
    }
}
