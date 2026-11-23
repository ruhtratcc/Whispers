// Organiza a fila de cartões e guarda os que o jogador já viu.

using System;
using System.Linq;
using UnityEngine;

namespace HorrorGame
{
    [DefaultExecutionOrder(350), DisallowMultipleComponent]
    public sealed class TutorialCardSystem : MonoBehaviour
    {
        public GameSession game;
        public TutorialCardCatalog catalog;
        public TutorialCardState State { get; private set; } = new TutorialCardState();
        public TutorialCardView View { get; private set; }
        public TutorialCardData Current { get; private set; }
        public bool IsVisible => View && (Current || View.HelpVisible);
        public bool IsOpen => Current;
        public bool BlocksWhistle => TutorialPreferences.Mode != TutorialMode.Off && !State.seen.Contains("whistle") && State.pending.Exists(p => p.id == "whistle");

        public event Action Changed;
        Inventory inventory;
        WhistleSystem whistle;
        PhoneMessages phone;
        InspectionManager inspection;
        BeforeDemolitionRoadFlow road;
        bool subscribed, ownsModal, returnToHelp, initializing;
        int inputAfterFrame;
        public static TutorialCardSystem For(GameSession value) => value ? value.tutorials : null;
        void Awake()
        {
            if (!game)
                game = GetComponent<GameSession>();
            if (game)
            {
                game.tutorials = this;
                inventory = game.interaction ? game.interaction.inventory : null;
            }

            whistle = GetComponent<WhistleSystem>();
            phone = GetComponent<PhoneMessages>();
            inspection = GetComponent<InspectionManager>();
            road = GetComponent<BeforeDemolitionRoadFlow>();
        }

        void OnEnable()
        {
            if (!game || subscribed)
                return;
            subscribed = true;
            if (inventory)
                inventory.Changed += Observe;
            if (phone)
                phone.Changed += Observe;
            if (game.equipment)
                game.equipment.Changed += Observe;
            if (game.narrative)
            {
                game.narrative.Initialized += Initialize;
                game.narrative.SignalAccepted += Signal;
            }
        }

        void Start()
        {
            if (!game || !catalog)
                return;
            EnsureView();
            if (game.narrative && game.narrative.IsInitialized)
                Initialize();
        }

        void EnsureView()
        {
            if (View || !catalog || !game)
                return;
            var root = new GameObject("Dicas de item", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            View = root.AddComponent<TutorialCardView>();
            View.Build(this, catalog.font);
            View.InstallMenuEntries();
        }

        void Initialize()
        {
            if (initializing || !catalog || !game)
                return;
            initializing = true;
            if (!State.initialized)
            {
                State.initialized = true;
                State.mode = TutorialPreferences.Mode;
            }

            EnsureView();
            Observe();
            initializing = false;
        }

        void Signal(string id)
        {
            if (!catalog)
                return;
            foreach (var card in catalog.cards)
                if (card && card.trigger == TutorialTrigger.Signal && card.triggerId == id)
                    Discover(card.id);
        }

        public bool Discover(string id)
        {
            if (!catalog || !game || game.IsRestoringCheckpoint)
                return false;
            var card = catalog.Find(id);
            if (card && card.trigger == TutorialTrigger.Flashlight && game.gameObject.scene.name == "Chapter1Office")
                return false;
            if (!State.Discover(card))
                return false;
            State.initialized = true;
            Changed?.Invoke();
            Save();
            return true;
        }

        void Observe()
        {
            if (!State.initialized || !catalog || !game || game.IsRestoringCheckpoint)
                return;
            int equipped = 0;
            if (game.equipment)
                for (int i = 0; i < game.equipment.items.Length; i++)
                    if (game.equipment.Available(i))
                        equipped++;
            foreach (var card in catalog.cards)
            {
                if (!card)
                    continue;
                bool match = false;
                switch (card.trigger)
                {
                    case TutorialTrigger.Signal:
                        match = game.narrative && game.narrative.HasSignal(card.triggerId);
                        break;
                    case TutorialTrigger.Flashlight:
                        match = game.equipment && game.equipment.Owns(0) && game.gameObject.scene.name != "Chapter1Office";
                        break;
                    case TutorialTrigger.InventoryItem:
                        match = inventory && inventory.Contains(card.triggerId);
                        break;
                    case TutorialTrigger.SecondEquipment:
                        match = equipped >= 2;
                        break;
                    case TutorialTrigger.FirstMessage:
                        match = phone && phone.Acquired && phone.State.received.Count > 0;
                        break;
                }

                if (match)
                    Discover(card.id);
            }
        }

        bool ImportantSubtitleVisible
        {
            get
            {
                var hud = game ? game.hud : null;
                if (!hud || !hud.SubtitleVisible)
                    return false;
                string text = hud.subtitle ? hud.subtitle.text : "";
                return !text.TrimStart().StartsWith("[") || text.Contains("\n");
            }
        }

        public bool SafeToShow => game && game.CanControl && !game.IsBlackingOut && !SceneTransitionService.ControlsBlocked && (!game.narrative || game.narrative.IsInitialized && !game.narrative.SequencePlaying) && !ImportantSubtitleVisible && (!inspection || !inspection.IsOpen && !inspection.IsCompleting) && (!phone || !phone.IsOpen && !phone.BannerVisible) && (!game.lockpick || !game.lockpick.IsOpen) && (!whistle || !whistle.Busy) && (!road || !road.driver || !road.driver.Seated && !road.Glancing);

        void Update()
        {
            if (!game || !catalog)
                return;
            if (IsVisible)
            {
                if (Time.frameCount > inputAfterFrame)
                    View.ReadKeyboard();
                return;
            }

            Tick(Time.unscaledDeltaTime);
        }

        public void Tick(float seconds)
        {
            if (!State.initialized || !catalog || !SafeToShow || !float.IsFinite(seconds) || seconds <= 0)
                return;
            PendingTutorial candidate = null;
            int priority = int.MinValue;
            foreach (var p in State.pending)
            {
                p.remaining = Mathf.Max(0, p.remaining - Mathf.Min(seconds, .25f));
                var card = catalog.Find(p.id);
                if (card && p.remaining <= 0 && !State.seen.Contains(p.id) && card.priority > priority)
                {
                    candidate = p;
                    priority = card.priority;
                }
            }

            if (TutorialPreferences.Mode != TutorialMode.Off && candidate != null)
                Open(candidate.id, false);
        }

        public bool Open(string id, bool fromHelp)
        {
            var card = catalog ? catalog.Find(id) : null;
            if (!card || Current || !game || !fromHelp && State.seen.Contains(id))
                return false;
            if (fromHelp)
            {
                if (!State.discovered.Contains(id) || !card.readInHelp || game.State != GameState.Paused)
                    return false;
            }
            else if (!SafeToShow)
                return false;
            EnsureView();
            if (!View)
                return false;
            returnToHelp = fromHelp;
            ownsModal = !fromHelp;
            if (ownsModal && !game.BeginTutorial(card.blocksGame))
                return false;
            Current = card;
            inputAfterFrame = Time.frameCount + 1;
            State.pending.RemoveAll(p => p.id == id);
            if (!State.seen.Contains(id))
                State.seen.Add(id);
            AudioManager.Instance?.SetTutorialDuck(true);
            View.Show(card, TutorialPreferences.Mode == TutorialMode.TextOnly);
            Play(card.openSound);
            Changed?.Invoke();
            Save();
            return true;
        }

        public bool ShowHelp()
        {
            if (!game || game.State != GameState.Paused || Current)
                return false;
            EnsureView();
            inputAfterFrame = Time.frameCount + 1;
            View.ShowHelp();
            return true;
        }

        public void Close()
        {
            Close(true);
        }

        void Close(bool save)
        {
            if (!View)
                return;
            var card = Current;
            Current = null;
            View.Hide();
            AudioManager.Instance?.SetTutorialDuck(false);
            if (ownsModal && game)
                game.EndTutorial();
            ownsModal = false;
            if (card)
                Play(card.closeSound);
            if (save && card)
                foreach (var pending in State.pending)
                    pending.remaining = Mathf.Max(pending.remaining, .75f);
            if (save)
                Save();
            if (returnToHelp && save)
            {
                returnToHelp = false;
                View.ShowHelp();
            }

            Changed?.Invoke();
        }

        public void CloseHelp()
        {
            if (Current)
            {
                returnToHelp = true;
                Close();
                return;
            }

            if (View)
                View.Hide();
        }

        public void SetMode(TutorialMode mode)
        {
            TutorialPreferences.Set(mode);
            State.mode = mode;
            Changed?.Invoke();
            Save();
        }

        public TutorialCardData[] HelpCards => catalog ? catalog.cards.Where(c => c && c.readInHelp && State.discovered.Contains(c.id)).OrderBy(c => c.helpOrder).ToArray() : Array.Empty<TutorialCardData>();

        public void Play(SoundSet sound)
        {
            if (sound)
                AudioManager.Instance?.Play(sound, transform.position, .12f, AudioChannel.UI, false, true);
        }

        public TutorialCardState Capture()
        {
            var result = State.Copy();
            result.initialized = true;
            result.mode = TutorialPreferences.Mode;
            return result;
        }

        public void Restore(TutorialCardState data)
        {
            Close(false);
            if (data != null && data.initialized && data.Valid)
            {
                State = data.Copy();
                if (!TutorialPreferences.HasSetting)
                    TutorialPreferences.Set(State.mode);
                State.mode = TutorialPreferences.Mode;
            }
            else
            {
                State = new TutorialCardState
                {
                    initialized = true,
                    mode = TutorialPreferences.Mode
                };
                MarkLegacyOwned();
            }

            Changed?.Invoke();
        }

        void MarkLegacyOwned()
        {
            if (!catalog)
                return;
            int count = 0;
            if (game.equipment)
                for (int i = 0; i < game.equipment.items.Length; i++)
                    if (game.equipment.Available(i))
                        count++;
            foreach (var c in catalog.cards)
            {
                if (!c)
                    continue;
                bool owned = c.trigger == TutorialTrigger.InventoryItem && inventory && inventory.Contains(c.triggerId) || c.trigger == TutorialTrigger.Flashlight && game.equipment && game.equipment.Owns(0) || c.trigger == TutorialTrigger.WhistleLore && whistle && whistle.State.loreKnown || c.trigger == TutorialTrigger.FirstMessage && phone && (phone.Acquired || phone.State.received.Count > 0) || c.trigger == TutorialTrigger.SecondEquipment && count >= 2 || c.trigger == TutorialTrigger.Signal && game.narrative && game.narrative.HasSignal(c.triggerId);
                if (owned)
                {
                    State.discovered.Add(c.id);
                    State.seen.Add(c.id);
                }
            }
        }

        void Save()
        {
            if (game && game.narrative && !game.IsRestoringCheckpoint)
                game.narrative.ScheduleSave();
        }

        void OnDisable()
        {
            if (subscribed)
            {
                if (inventory)
                    inventory.Changed -= Observe;
                if (phone)
                    phone.Changed -= Observe;
                if (game && game.equipment)
                    game.equipment.Changed -= Observe;
                if (game && game.narrative)
                {
                    game.narrative.Initialized -= Initialize;
                    game.narrative.SignalAccepted -= Signal;
                }
            }

            subscribed = false;
            Close(false);
        }
    }
}
