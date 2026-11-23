// Dispara comentários curtos e contextuais de João.

using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame
{
    /// <summary>Uses the existing recorded-dialogue catalogue and saved whistle response IDs.</summary>
    public sealed class ContextBarks : MonoBehaviour
    {
        public GameSession game;
        [Min(10)]
        public float cooldown = 24, firstHintSeconds = 100, offRouteSeconds = 45, offRouteDistance = 28;
        WhistleSystem speech;
        EmailInboxController inbox;
        ObjectiveService objectives;
        readonly Queue<string> pending = new Queue<string>();
        float sinceSpeech = 30, idle, away;
        int hintLevel;
        void Awake()
        {
            if (!game)
                game = GetComponent<GameSession>();
            speech = GetComponent<WhistleSystem>();
            inbox = GetComponentInChildren<EmailInboxController>(true);
            objectives = GetComponent<ObjectiveService>();
        }

        void OnEnable()
        {
            if (inbox)
            {
                inbox.ReadingCompleted += EmailRead;
                inbox.ReadingAbandoned += Abandoned;
            }

            if (objectives)
                objectives.ProgressChanged += Progressed;
        }

        void OnDisable()
        {
            if (inbox)
            {
                inbox.ReadingCompleted -= EmailRead;
                inbox.ReadingAbandoned -= Abandoned;
            }

            if (objectives)
                objectives.ProgressChanged -= Progressed;
            pending.Clear();
        }

        void EmailRead(string id)
        {
            if (id.StartsWith("email.0"))
                pending.Enqueue("read." + id);
        }

        void Abandoned(string id)
        {
            if (Random.value < .12f)
                pending.Enqueue("email.abandoned");
        }

        void Progressed()
        {
            idle = away = 0;
            hintLevel = 0;
        }

        public bool Enqueue(string trigger)
        {
            if (pending.Count >= 8)
                return false;
            pending.Enqueue(trigger);
            return true;
        }

        void Update()
        {
            if (!game || !speech || !game.narrative || !game.narrative.IsInitialized || game.IsRestoringCheckpoint)
                return;
            bool reading = inbox && inbox.IsOpen && game.State == GameState.Inspecting;
            if ((!game.CanControl && !reading) || game.narrative.SequencePlaying)
                return;
            sinceSpeech += reading ? Time.unscaledDeltaTime : Time.deltaTime;
            if (game.hud && game.hud.SubtitleVisible && !(game.hud.subtitle.text.StartsWith("[") && !game.hud.subtitle.text.Contains("\n")))
                return;
            if (sinceSpeech < cooldown)
                return;
            if (pending.Count > 0)
            {
                string trigger = pending.Peek();
                if (speech.HasResponded("bark." + trigger))
                {
                    pending.Dequeue();
                    return;
                }

                if (speech.Speak(trigger))
                {
                    speech.MarkResponded("bark." + trigger);
                    pending.Dequeue();
                    sinceSpeech = 0;
                }

                return;
            }

            if (!game.CanControl || !objectives || string.IsNullOrEmpty(objectives.CurrentTitle))
                return;
            idle += Time.deltaTime;
            if (objectives.HasTarget && Vector3.Distance(game.player.transform.position, objectives.TargetPosition) > offRouteDistance)
                away += Time.deltaTime;
            else
                away = 0;
            if (away >= offRouteSeconds)
            {
                if (speech.Speak("guide.offroute"))
                {
                    away = 0;
                    sinceSpeech = 0;
                }

                return;
            }

            if (idle >= firstHintSeconds + hintLevel * 65 && speech.Speak("guide." + (Mathf.Min(2, hintLevel) + 1)))
            {
                hintLevel = Mathf.Min(2, hintLevel + 1);
                idle = 0;
                sinceSpeech = 0;
            }
        }
    }
}
