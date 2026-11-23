// Controla abertura, fechamento, trava e estado persistente de portas.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    public sealed class InteractiveDoor : PersistentObject, IInteractable
    {
        [Header("Assign the actual hinge/pivot")]
        [SerializeField] public Transform hinge;
        [SerializeField] public float openAngle = 95, speed = 85;
        [Tooltip("Optional reciprocal link to the other leaf of the same doorway.")]
        [SerializeField] public InteractiveDoor pairedLeaf;
        [SerializeField] public bool locked;
        [SerializeField] public string requiredKey = "school_key", displayName = "porta";
        [Tooltip("Optional wording for a story-locked doorway without a key.")]
        public string lockedMessage = "Trancada. Preciso da chave.";
        public UnityEvent onLockedAttempt = new UnityEvent();
        [Header("Spatial sounds")]
        [Tooltip("Legacy movement fallback, used when a direction-specific sound is unassigned.")]
        [SerializeField] public SoundSet creak, lockedSound;
        [Tooltip("Handle and hinge movement; begins only when the leaf actually moves.")]
        [SerializeField] public SoundSet openSound, closeSound;
        [Tooltip("Small latch/clunk played when the leaf reaches its closed position.")]
        [SerializeField] public SoundSet latchSound;
        [SerializeField, Range(0, 1)] float movementGain = .45f, latchGain = .45f;
        [SerializeField, Min(.05f)] float soundCooldown = .2f;
        [SerializeField] public LayerMask playerMask;
        [SerializeField] public BoxCollider leafCollider;
        [SerializeField] public UnityEvent onOpen = new UnityEvent(), onClose = new UnityEvent(), onUnlock = new UnityEvent();

        [Serializable] class Data { public bool open, locked; }
        Quaternion closedRotation;
        float angle, nextMotionSound, nextLockedSound;
        bool isOpen, initialized, movementSoundPending, completionSoundPending;
        public bool IsOpen => isOpen;
        [HideInInspector] public LockableDoor optionalLock;
        InteractiveDoor Partner => pairedLeaf && pairedLeaf != this && pairedLeaf.pairedLeaf == this ? pairedLeaf : null;
        InteractiveDoor StateOwner
        {
            get
            {
                var partner = Partner;
                if (!partner) return this;
                int order = CompareSaveIds(persistentId, partner.persistentId);
                // SaveSystem already requires a unique persistent ID on every leaf.
                return order <= 0 ? this : partner;
            }
        }
        static int CompareSaveIds(string first, string second)
        {
            // Appended school leaves must not take ownership from an older leaf
            // (for example .72 must sort after .8), keeping old checkpoints usable.
            int a = first?.LastIndexOf('.') ?? -1, b = second?.LastIndexOf('.') ?? -1;
            if (a >= 0 && b >= 0 && first.Substring(0, a) == second.Substring(0, b)
                && int.TryParse(first.Substring(a + 1), out int left)
                && int.TryParse(second.Substring(b + 1), out int right)) return left.CompareTo(right);
            return string.CompareOrdinal(first, second);
        }

        void Awake() => Initialize();
        void Initialize()
        {
            if (initialized) return;
            if (!hinge) hinge = transform;
            closedRotation = hinge.localRotation;
            initialized = true;
        }

        public string Prompt(PlayerInteraction player) => optionalLock && optionalLock.Handles ? optionalLock.Prompt(player) : locked || (Partner && Partner.locked) ? "Destrancar " + displayName : StateOwner.isOpen ? "Fechar " + displayName : "Abrir " + displayName;
        public bool CanInteract(PlayerInteraction player) => true;
        public void Interact(PlayerInteraction player)
        {
            if(optionalLock && optionalLock.Handles){optionalLock.Interact(player);return;}
            Initialize();
            var partner = Partner;
            if (partner) partner.Initialize();
            if (!CanUnlock(player) || (partner && !partner.CanUnlock(player)))
            {
                onLockedAttempt.Invoke();
                player?.ShowMessage(lockedMessage);
                if (Time.time >= nextLockedSound)
                {
                    AudioManager.Instance?.Play(lockedSound, SoundPosition(), .35f);
                    nextLockedSound = Time.time + .4f;
                    if (partner) partner.nextLockedSound = nextLockedSound;
                }
                return;
            }
            Unlock();
            if (partner) partner.Unlock();

            bool open = !StateOwner.isOpen;
            SetOpenTarget(open);
            if (partner) partner.SetOpenTarget(open);
        }

        /// <summary>Authoritative map state; never fires the player's unlock event.</summary>
        public void SetLocked(bool value)
        {
            locked = value;
            if (Partner) Partner.locked = value;
        }
        /// <summary>Story-driven motion. Silent restore preserves the closed hinge basis and paired save owner.</summary>
        public void SetOpen(bool open, bool silent = false)
        {
            Initialize();
            var partner = Partner;
            if (partner) partner.Initialize();
            if (silent)
            {
                StateOwner.RestoreState(JsonUtility.ToJson(new Data { open = open,
                    locked = locked || (partner && partner.locked) }));
                return;
            }
            SetOpenTarget(open);
            if (partner) partner.SetOpenTarget(open);
        }

        bool CanUnlock(PlayerInteraction player) => !locked || (!string.IsNullOrEmpty(requiredKey)
            && player && player.inventory && player.inventory.Contains(requiredKey));
        void Unlock()
        {
            if (!locked) return;
            locked = false;
            AudioManager.Instance?.PlayCue("key",transform.position,.2f);
            onUnlock.Invoke();
        }
        void SetOpenTarget(bool open)
        {
            if (isOpen == open) return;
            isOpen = open;
            movementSoundPending = true;
            completionSoundPending = true;
            if (isOpen) onOpen.Invoke();
            else onClose.Invoke();
        }

        Vector3 SoundPosition() => leafCollider ? leafCollider.bounds.center : hinge.position;

        void Update()
        {
            Initialize();
            float target = isOpen ? openAngle : 0;
            float next = Mathf.MoveTowards(angle, target, Mathf.Max(.01f, speed) * Time.deltaTime);
            if (Mathf.Approximately(next, angle)) return;

            if (leafCollider && playerMask.value != 0)
            {
                Quaternion candidate = closedRotation * Quaternion.Euler(0, next, 0);
                Quaternion world = (hinge.parent ? hinge.parent.rotation : Quaternion.identity) * candidate;
                Quaternion delta = world * Quaternion.Inverse(hinge.rotation);
                Vector3 center = hinge.position + delta * (leafCollider.transform.TransformPoint(leafCollider.center) - hinge.position);
                world = delta * leafCollider.transform.rotation;
                Vector3 half = Vector3.Scale(leafCollider.size, leafCollider.transform.lossyScale) * .5f;
                if (Physics.CheckBox(center, half, world, playerMask, QueryTriggerInteraction.Ignore)) return;
            }

            if (movementSoundPending)
            {
                movementSoundPending = false;
                if (Time.time >= nextMotionSound)
                {
                    SoundSet directionSound = isOpen ? openSound : closeSound;
                    AudioManager.Instance?.Play(directionSound ? directionSound : creak, SoundPosition(), movementGain);
                    NoiseSystem.Emit(SoundPosition(), 6, gameObject, NoiseKind.Door);
                    nextMotionSound = Time.time + soundCooldown;
                }
            }

            angle = next;
            hinge.localRotation = closedRotation * Quaternion.Euler(0, angle, 0);
            if (completionSoundPending && Mathf.Approximately(angle, target))
            {
                completionSoundPending = false;
                if (!isOpen && latchSound)
                    AudioManager.Instance?.Play(latchSound, SoundPosition(), latchGain);
            }
        }

        public override string CaptureState() => JsonUtility.ToJson(new Data
            { open = StateOwner.isOpen, locked = locked || (Partner && Partner.locked) });
        public override void RestoreState(string json)
        {
            // One stable ID owns the pair. Older saves may contain different leaf
            // states; the partner record must not override the doorway by load order.
            if (StateOwner != this) return;
            Initialize();
            var data = JsonUtility.FromJson<Data>(json);
            if (data == null) return;
            RestoreLeaf(data);
            if (Partner) Partner.RestoreLeaf(data);
        }
        void RestoreLeaf(Data data)
        {
            Initialize();
            isOpen = data.open;
            locked = data.locked;
            angle = isOpen ? openAngle : 0;
            movementSoundPending = completionSoundPending = false;
            hinge.localRotation = closedRotation * Quaternion.Euler(0, angle, 0);
        }
    }
}
