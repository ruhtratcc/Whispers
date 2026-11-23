// Controla o movimento, a interação e o estado de uma gaveta.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    public sealed class InteractiveDrawer : PersistentObject, IInteractable
    {
        [SerializeField] public Transform drawer;
        [SerializeField] public Vector3 localTravel = new Vector3(0, 0, -.45f);
        [Tooltip("Travel speed in metres per second. Existing serialized settings are preserved.")]
        [SerializeField, Min(.05f)] public float travelSpeed = 2;
        [Tooltip("Gentle acceleration and braking of the drawer instead of an abrupt constant speed.")]
        [SerializeField, Min(.01f)] public float travelSmoothTime = .09f;
        [SerializeField] public string displayName = "gaveta";
        [Header("Spatial sounds")]
        [Tooltip("Legacy movement fallback.")]
        [SerializeField] public SoundSet sound;
        [SerializeField] public SoundSet openSound, closeSound;
        [Tooltip("Soft stop/latch, played only once after reaching the closed position.")]
        [SerializeField] public SoundSet endSound;
        [SerializeField, Range(0, 1)] float movementGain = .32f, endGain = .32f;
        [SerializeField, Min(.05f)] float soundCooldown = .2f;
        [SerializeField] public UnityEvent onOpen = new UnityEvent(), onClose = new UnityEvent();
        [Serializable] class Data { public bool open; }

        Vector3 closed, velocity;
        float nextMotionSound;
        bool isOpen, initialized, movementSoundPending, completionSoundPending;

        public bool IsOpen => isOpen;
        [HideInInspector] public LockableDoor optionalLock;
        /// <summary>Actual travel, so contents cannot be picked through a closed or barely open drawer.</summary>
        public float OpenFraction
        {
            get
            {
                Initialize();
                float distanceSquared = localTravel.sqrMagnitude;
                return distanceSquared > .000001f
                    ? Mathf.Clamp01(Vector3.Dot(drawer.localPosition - closed, localTravel) / distanceSquared)
                    : 0;
            }
        }

        void Initialize()
        {
            if (initialized) return;
            if (!drawer) drawer = transform;
            closed = drawer.localPosition;
            initialized = true;
        }

        void Awake() => Initialize();
        public string Prompt(PlayerInteraction player) => optionalLock && optionalLock.Handles ? optionalLock.Prompt(player) : isOpen ? "Fechar " + displayName : "Abrir " + displayName;
        public bool CanInteract(PlayerInteraction player) => true;
        public void Interact(PlayerInteraction player)
        {
            if(optionalLock && optionalLock.Handles){optionalLock.Interact(player);return;}
            Initialize();
            isOpen = !isOpen;
            movementSoundPending = true;
            completionSoundPending = true;
            if (isOpen) onOpen.Invoke();
            else onClose.Invoke();
        }

        void Update()
        {
            Initialize();
            if (Time.deltaTime <= 0) return;
            Vector3 target = closed + (isOpen ? localTravel : Vector3.zero);
            Vector3 next = Vector3.SmoothDamp(drawer.localPosition, target, ref velocity, Mathf.Max(.01f, travelSmoothTime), Mathf.Max(.05f, travelSpeed), Time.deltaTime);
            if ((next - target).sqrMagnitude < .000001f)
            {
                next = target;
                velocity = Vector3.zero;
            }

            if (movementSoundPending && (next - drawer.localPosition).sqrMagnitude > .00000001f)
            {
                movementSoundPending = false;
                if (Time.time >= nextMotionSound)
                {
                    SoundSet directionSound = isOpen ? openSound : closeSound;
                    AudioManager.Instance?.Play(directionSound ? directionSound : sound, drawer.position, movementGain);
                    NoiseSystem.Emit(drawer.position, 3, gameObject, NoiseKind.Door);
                    nextMotionSound = Time.time + soundCooldown;
                }
            }
            drawer.localPosition = next;

            if (completionSoundPending && next == target)
            {
                completionSoundPending = false;
                if (!isOpen && endSound)
                    AudioManager.Instance?.Play(endSound, drawer.position, endGain);
            }
        }

        public override string CaptureState() => JsonUtility.ToJson(new Data { open = isOpen });
        public override void RestoreState(string json)
        {
            Initialize();
            var data = JsonUtility.FromJson<Data>(json);
            if (data == null) return;
            isOpen = data.open;
            velocity = Vector3.zero;
            movementSoundPending = completionSoundPending = false;
            drawer.localPosition = closed + (isOpen ? localTravel : Vector3.zero);
        }
    }
}
