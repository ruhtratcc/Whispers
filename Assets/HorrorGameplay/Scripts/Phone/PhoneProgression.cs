// Verifica leituras obrigatórias, lembretes e travas ligadas às mensagens.

using System;
using UnityEngine;

namespace HorrorGame
{
    [DefaultExecutionOrder(145), DisallowMultipleComponent]
    public sealed class PhoneProgression : MonoBehaviour
    {
        public GameSession game;
        public PhoneMessages phone;
        public PhoneProgressionConfig config;
        public string PendingObjective { get; private set; } = "";
        public bool IsWaitingForRead { get; private set; }
        public event Action ProgressChanged;

        NarrativeDirector subscribedDirector;
        PhoneMessages subscribedPhone;
        PhoneProgressionConfig.Gate currentGate;
        string currentKey = "";
        float pollAge, unreadAge, missingAge, nextReminder;
        bool missingDeliveryBypass, strongShown;

        NarrativeDirector Director => game ? game.narrative : null;
        bool Ready => game && phone && phone.State != null && config && Director && Director.IsInitialized
            && BeforeDemolitionCampaign.Is(Director.book) && !game.IsRestoringCheckpoint;

        public static PhoneProgression For(GameSession session) => session ? session.GetComponent<PhoneProgression>() : null;

        public static bool CanAdvance(GameSession session, string signal)
        {
            var service = For(session);
            if (!service || !service.isActiveAndEnabled || !service.Ready) return true;
            if (service.Director.HasSignal(signal)) return true;
            var gate = service.FindGate(signal);
            if (gate == null) return true;
            service.RefreshProgress();
            return service.Read(gate.messageId) || service.currentGate == gate && service.missingDeliveryBypass;
        }

        void Awake() => Bind();
        void OnEnable() => Bind();
        void Start() { Bind(); ChapterChanged(Director ? Director.Progress.chapterId : ""); }

        void Bind()
        {
            if (!game) game = GetComponent<GameSession>();
            if (!phone) phone = PhoneMessages.For(game);
            var director = Director;
            if (subscribedDirector != director)
            {
                if (subscribedDirector)
                {
                    subscribedDirector.Initialized -= Initialized;
                    subscribedDirector.ChapterInitialized -= ChapterChanged;
                    subscribedDirector.SignalAccepted -= SignalChanged;
                }
                subscribedDirector = director;
                if (subscribedDirector)
                {
                    subscribedDirector.Initialized += Initialized;
                    subscribedDirector.ChapterInitialized += ChapterChanged;
                    subscribedDirector.SignalAccepted += SignalChanged;
                }
            }
            if (subscribedPhone != phone)
            {
                if (subscribedPhone) { subscribedPhone.Changed -= RefreshProgress; subscribedPhone.ConversationRead -= RefreshProgress; }
                subscribedPhone = phone;
                if (subscribedPhone) { subscribedPhone.Changed += RefreshProgress; subscribedPhone.ConversationRead += RefreshProgress; }
            }
        }

        void Initialized() => ChapterChanged(Director ? Director.Progress.chapterId : "");
        void SignalChanged(string signal) => RefreshProgress();
        void ChapterChanged(string chapter)
        {
            if (!Ready) return;
            if (chapter == BeforeDemolitionCampaign.FourChapterId) phone.Trigger(PhoneTrigger.RouteFour);
            else if (chapter == BeforeDemolitionCampaign.TwoVoicesChapterId) phone.Trigger(PhoneTrigger.RouteLoop);
            RefreshProgress();
        }

        PhoneProgressionConfig.Gate FindGate(string signal)
        {
            if (!Ready || string.IsNullOrEmpty(signal)) return null;
            string chapter = Director.Progress.chapterId;
            foreach (var gate in config.gates ?? Array.Empty<PhoneProgressionConfig.Gate>())
                if (gate != null && gate.chapterId == chapter && gate.signal == signal && !string.IsNullOrWhiteSpace(gate.messageId))
                    return gate;
            return null;
        }

        bool Read(string id)
        {
            if (!phone || phone.State == null || phone.State.received == null) return false;
            foreach (var receipt in phone.State.received)
                if (receipt != null && receipt.id == id && receipt.read) return true;
            return false;
        }

        bool DeliveryMissing(PhoneProgressionConfig.Gate gate) => gate != null
            && (!phone.catalog || phone.catalog.messages == null || phone.catalog.Find(gate.messageId) == null || !phone.State.Has(gate.messageId));

        void RefreshProgress()
        {
            if (!Ready) return;
            var gate = FindGate(Director.NextSignal);
            if (gate != null && Director.HasSignal(gate.signal)) gate = null;
            string key = gate == null ? "" : gate.chapterId + ":" + gate.signal + ":" + gate.messageId;
            bool changed = currentKey != key;
            if (changed)
            {
                currentKey = key;
                unreadAge = missingAge = 0;
                missingDeliveryBypass = strongShown = false;
                nextReminder = Mathf.Max(10, config.reminderSeconds);
            }
            currentGate = gate;
            bool waiting = gate != null && !Read(gate.messageId) && !missingDeliveryBypass;
            string objective = waiting ? "Leia a mensagem no celular." : missingDeliveryBypass && gate != null ? gate.fallbackObjective ?? "" : "";
            changed |= waiting != IsWaitingForRead || objective != PendingObjective;
            IsWaitingForRead = waiting; PendingObjective = objective;
            if (changed) ProgressChanged?.Invoke();
        }

        void Update()
        {
            if (!Ready || !game.CanControl) return;
            pollAge += Time.deltaTime;
            if (pollAge < Mathf.Max(.1f, config.pollSeconds)) return;
            float elapsed = pollAge; pollAge = 0;
            EvaluateZones();
            RefreshProgress();
            if (!IsWaitingForRead || currentGate == null) return;

            var gate = currentGate;
            var definition = phone.catalog && phone.catalog.messages != null ? phone.catalog.Find(gate.messageId) : null;
            if (definition != null && !phone.State.Has(definition.id)) phone.Trigger(definition.trigger);
            if (currentGate != gate || !IsWaitingForRead) return;
            if (DeliveryMissing(currentGate)) missingAge += elapsed; else missingAge = 0;
            // This is a delivery fault escape, never a timeout that reads a normal message for the player.
            if (missingAge >= Mathf.Max(15, config.missingDeliveryFallbackSeconds))
            {
                missingDeliveryBypass = true;
                RefreshProgress();
                return;
            }
            if (Read(currentGate.messageId)) { RefreshProgress(); return; }
            unreadAge += elapsed;
            bool strong = !strongShown && unreadAge >= Mathf.Max(config.reminderSeconds, config.strongReminderSeconds);
            if (unreadAge < nextReminder && !strong) return;
            if (!SafeReminder()) return;
            nextReminder = unreadAge + Mathf.Max(10, config.reminderSeconds);
            strongShown |= strong;
            phone.RemindUnread();
            var whistle = WhistleSystem.For(game);
            if (whistle) whistle.Speak("phone.reminder");
            else game.hud?.ShowSubtitle("João: Preciso ver essa mensagem.", 3);
            if (strong) game.hud?.Notify("Há uma mensagem importante no celular.");
        }

        void EvaluateZones()
        {
            if (!game.player || !BeforeDemolitionCampaign.IsSchoolChapter(Director.Progress.chapterId)) return;
            Vector3 point = game.player.transform.position + Vector3.up * .8f;
            foreach (var zone in config.zones ?? Array.Empty<PhoneProgressionConfig.Zone>())
                if (zone != null && (string.IsNullOrEmpty(zone.chapterId) || zone.chapterId == Director.Progress.chapterId)
                    && zone.bounds.Contains(point)) phone.Trigger(zone.trigger);
        }

        bool SafeReminder() => game.CanControl && !phone.IsOpen && !phone.BannerVisible && !Director.SequencePlaying
            && !game.IsBlackingOut && (!game.hud || !game.hud.SubtitleVisible);

        void OnDisable()
        {
            if (subscribedDirector)
            {
                subscribedDirector.Initialized -= Initialized;
                subscribedDirector.ChapterInitialized -= ChapterChanged;
                subscribedDirector.SignalAccepted -= SignalChanged;
            }
            if (subscribedPhone) { subscribedPhone.Changed -= RefreshProgress; subscribedPhone.ConversationRead -= RefreshProgress; }
            subscribedDirector = null; subscribedPhone = null;
        }
    }
}
