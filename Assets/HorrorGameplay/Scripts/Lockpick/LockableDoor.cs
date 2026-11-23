// Conecta fechaduras e seus estados a portas ou gavetas.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame
{
    public sealed class LockableDoor : PersistentObject, IInteractable
    {
        internal static readonly List<LockableDoor> Active = new List<LockableDoor>();
        public LockKind kind = LockKind.OptionalPickable;
        public LockDifficulty difficulty = LockDifficulty.Easy;
        public InteractiveDoor door;
        public InteractiveDrawer drawer;
        public string displayName = "armário", requiredFlag;
        public int firstChapter = 3, lastChapter = 4;
        public float toleranceOverride;
        public bool training;
        public GameObject rewards;
        public int pinsCompleted, breaks;
        public bool unlocked, hintUsed, hintPending;
        public bool IsOptional => kind == LockKind.OptionalPickable && (!door || string.IsNullOrEmpty(door.requiredKey) && !door.pairedLeaf) && !GetComponentInChildren<SchoolAreaRestriction>(true);
        public bool Handles => isActiveAndEnabled && (kind == LockKind.Unavailable || IsOptional && !unlocked);

        LockpickSystem System => GameSession.Instance ? GameSession.Instance.lockpick : null;

        [Serializable]
        sealed class Data
        {
            public int version = 1, pins, breaks;
            public bool unlocked, hintUsed, hintPending;
        }

        void Awake()
        {
            if (door)
                door.optionalLock = this;
            if (drawer)
                drawer.optionalLock = this;
            Reconcile();
        }

        void OnEnable()
        {
            if (!Active.Contains(this))
                Active.Add(this);
        }

        void OnDisable()
        {
            Active.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => Active.Clear();
        public bool ChapterAllowed
        {
            get
            {
                var g = GameSession.Instance;
                if (!g || !g.narrative || !g.narrative.book || !g.narrative.IsInitialized)
                    return false;
                int chapter = g.narrative.book.ChapterIndex(g.narrative.Progress.chapterId) + 1;
                return chapter >= firstChapter && chapter <= lastChapter && (string.IsNullOrEmpty(requiredFlag) || g.narrative.HasSignal(requiredFlag));
            }
        }

        public bool CanInteract(PlayerInteraction player) => isActiveAndEnabled && (kind == LockKind.Unavailable || IsOptional);
        public string Prompt(PlayerInteraction player)
        {
            if (unlocked)
                return (door && door.IsOpen || drawer && drawer.IsOpen ? "Fechar " : "Abrir ") + displayName;
            var s = System;
            if (!IsOptional || !ChapterAllowed)
                return "Trancada";
            return s && s.Equipped ? s.Count > 0 ? "Arrombar" : "Sem grampo" : "Trancada";
        }

        public void Interact(PlayerInteraction player)
        {
            if (unlocked)
            {
                if (door)
                    door.Interact(player);
                else if (drawer)
                    drawer.Interact(player);
                return;
            }

            if (!IsOptional || !ChapterAllowed)
            {
                System?.Speak("unavailable", transform.position);
                return;
            }

            var s = System;
            if (!s)
                return;
            if (!s.Equipped || s.Count == 0)
            {
                s.Speak(s.Acquired ? s.Count == 0 ? "empty" : "avaliar" : "sem_grampo", transform.position);
                return;
            }

            s.Open(this);
        }

        public void Reconcile()
        {
            if (IsOptional && door)
                door.SetLocked(!unlocked);
            if (rewards)
                rewards.SetActive(unlocked);
        }

        public void Complete()
        {
            if (unlocked || !IsOptional)
                return;
            unlocked = true;
            pinsCompleted = (int)difficulty;
            Reconcile();
            if (door)
                door.SetOpen(true);
            else if (drawer)
                drawer.Interact(GameSession.Instance ? GameSession.Instance.interaction : null);
        }

        public bool OfferHint(WhistleContext context)
        {
            if (!IsOptional || unlocked || hintUsed || !ChapterAllowed || context.kind != WhistleKind.Tap || !context.system || !context.system.SupernaturalActive || Vector3.Distance(context.position, transform.position) > 5)
                return false;
            hintUsed = hintPending = true;
            System?.HintSound(transform.position);
            GameSession.Instance?.narrative?.ScheduleSave();
            return true;
        }

        public override string CaptureState() => JsonUtility.ToJson(new Data { pins = pinsCompleted, breaks = breaks, unlocked = unlocked, hintUsed = hintUsed, hintPending = hintPending });
        public override void RestoreState(string json)
        {
            var d = JsonUtility.FromJson<Data>(json);
            if (d == null || d.version != 1)
                return;
            unlocked = d.unlocked;
            pinsCompleted = Mathf.Clamp(d.pins, 0, (int)difficulty);
            breaks = Mathf.Clamp(d.breaks, 0, 10000);
            hintUsed = d.hintUsed;
            hintPending = d.hintPending && hintUsed;
            Reconcile();
        }
    }
}
