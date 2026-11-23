// Liga objetos ou eventos do cenário à resposta de um apito.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    public enum WhistleResponseRole
    {
        Echo,
        OfficeCloset,
        Glass,
        Door,
        CorridorCorrect,
        CorridorLost,
        Mirror
    }

    [DisallowMultipleComponent]
    public sealed class WhistleResponder : MonoBehaviour, IWhistleResponsive
    {
        internal static readonly List<WhistleResponder> Active = new List<WhistleResponder>(64);
        public string responderId, requiredFlag, disabledAfterFlag, hiddenWhenItemOwned, signalOnResponse, dialogueTrigger, childTrigger;
        public string[] chapters = Array.Empty<string>();
        public WhistleResponseRole role;
        public bool respondToTap = true, respondToHold, onceOnly;
        [Min(0)]
        public float range, cooldown = 6, delay = .3f;
        [Range(0, 360)]
        public float hearingAngle = 360;
        public SoundSet responseSound, occupiedDoorSound;
        public InteractiveDoor door;
        public bool occupiedRoom;
        [Range(0, 1)]
        public float volumeScale = 1;
        public BeforeDemolitionMirrorFlow mirror;
        public BeforeDemolitionTwoVoicesFlow corridor;
        public Transform vibratingObject;
        public Renderer[] pulseRenderers = Array.Empty<Renderer>();
        public UnityEvent onResponse = new UnityEvent();
        float wait = -1, readyAt, pulseAge, lastHold, responseRemaining;
        int heardSerial = -1;
        WhistleContext pending;
        AudioSource source;
        AudioClip sourceClip;
        Vector3 restingPosition;
        MaterialPropertyBlock[] previous, pulses;
        void Awake()
        {
            if (vibratingObject)
                restingPosition = vibratingObject.localPosition;
            previous = new MaterialPropertyBlock[pulseRenderers.Length];
            pulses = new MaterialPropertyBlock[pulseRenderers.Length];
            for (int i = 0; i < pulseRenderers.Length; i++)
            {
                previous[i] = new MaterialPropertyBlock();
                pulses[i] = new MaterialPropertyBlock();
                if (pulseRenderers[i])
                {
                    pulseRenderers[i].GetPropertyBlock(previous[i]);
                    pulseRenderers[i].GetPropertyBlock(pulses[i]);
                }
            }
        }

        void OnEnable()
        {
            if (!Active.Contains(this))
                Active.Add(this);
        }

        void OnDisable()
        {
            Active.Remove(this);
            Cancel();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => Active.Clear();
        public bool CanHear(WhistleContext context)
        {
            if (role == WhistleResponseRole.Mirror)
                return false;
            if (!isActiveAndEnabled || context.kind == WhistleKind.Tap && !respondToTap || context.kind == WhistleKind.Hold && !respondToHold)
                return false;
            var system = context.system;
            if (system)
            {
                if (!system.SupernaturalActive)
                    return false;
                if (chapters.Length > 0 && Array.IndexOf(chapters, system.Chapter) < 0)
                    return false;
                if (!string.IsNullOrEmpty(hiddenWhenItemOwned) && system.Inventory && system.Inventory.Contains(hiddenWhenItemOwned))
                    return false;
                if (!string.IsNullOrEmpty(requiredFlag) && !system.HasFlag(requiredFlag))
                    return false;
                if (!string.IsNullOrEmpty(disabledAfterFlag) && system.game.narrative && system.game.narrative.HasSignal(disabledAfterFlag))
                    return false;
                if (onceOnly && system.HasResponded(responderId))
                    return false;
            }
            else if (!string.IsNullOrEmpty(requiredFlag))
                return false;
            if (corridor && (corridor.Escaped || !corridor.IsActive))
                return false;
            if (mirror && !mirror.IsActive)
                return false;
            Vector3 offset = transform.position - context.position;
            float reach = range > 0 ? range : system && system.config ? system.config.range : 24;
            if (offset.sqrMagnitude > reach * reach)
                return false;
            if (hearingAngle < 359 && offset.sqrMagnitude > .001f && Vector3.Dot(context.direction.normalized, offset.normalized) < Mathf.Cos(hearingAngle * .5f * Mathf.Deg2Rad))
                return false;
            return true;
        }

        public void OnWhistleHeard(WhistleContext context)
        {
            if (heardSerial == context.serial)
                return;
            heardSerial = context.serial;
            lastHold = context.duration;
        }

        public void OnWhistleTap(WhistleContext context)
        {
            if (!CanHear(context))
                return;
            OnWhistleHeard(context);
            Queue(context);
            if (role == WhistleResponseRole.Mirror && mirror)
                mirror.WhistleTap();
        }

        public void OnWhistleHold(WhistleContext context, float duration)
        {
            if (!CanHear(context))
                return;
            OnWhistleHeard(context);
            float delta = Mathf.Clamp(duration - lastHold, 0, .2f);
            lastHold = duration;
            if (role == WhistleResponseRole.Mirror && mirror)
                mirror.WhistleHold(delta);
            if (role == WhistleResponseRole.Glass)
                pulseAge = 1;
            Queue(context);
        }

        void Queue(WhistleContext context)
        {
            if (wait >= 0 || Time.time < readyAt)
                return;
            pending = context;
            wait = delay + (context.system ? context.system.EchoDelay : 0);
            if (corridor && role == WhistleResponseRole.CorridorCorrect)
                wait += corridor.State.loopCount * .04f;
        }

        internal void Tick(float seconds, bool running)
        {
            if (!running)
                return;
            if (responseRemaining > 0)
            {
                responseRemaining = Mathf.Max(0, responseRemaining - seconds);
                if (!source || source.clip != sourceClip || !source.isPlaying)
                {
                    source = null;
                    responseRemaining = 0;
                }
                else if (responseRemaining <= 0)
                {
                    source.Stop();
                    source = null;
                }
                else if (responseRemaining < .15f)
                    source.volume *= responseRemaining / .15f;
            }

            if (wait >= 0)
            {
                wait -= seconds;
                if (wait <= 0)
                {
                    wait = -1;
                    Respond(pending);
                }
            }

            if (pulseAge > 0)
            {
                pulseAge = Mathf.Max(0, pulseAge - seconds);
                bool show = !PresentationSettings.ReduceEffects;
                if (vibratingObject)
                    vibratingObject.localPosition = restingPosition + (show ? new Vector3(Mathf.Sin(pulseAge * 84) * .0015f, 0, 0) : Vector3.zero);
                if (previous != null)
                    for (int i = 0; i < pulseRenderers.Length; i++)
                        if (pulseRenderers[i])
                        {
                            if (show && pulseAge > 0)
                            {
                                var mat = pulseRenderers[i].sharedMaterial;
                                if (mat && mat.HasProperty("_BaseColor"))
                                    pulses[i].SetColor("_BaseColor", mat.GetColor("_BaseColor") * (1 + .055f * Mathf.Sin(pulseAge * Mathf.PI)));
                                pulseRenderers[i].SetPropertyBlock(pulses[i]);
                            }
                            else
                                pulseRenderers[i].SetPropertyBlock(previous[i]);
                        }
            }
        }

        void Respond(WhistleContext context)
        {
            if (!CanHear(context))
                return;
            var system = context.system;
            readyAt = Time.time + Mathf.Max(.1f, cooldown);
            if (role != WhistleResponseRole.CorridorLost)
            {
                var sound = role == WhistleResponseRole.Door && occupiedRoom && occupiedDoorSound ? occupiedDoorSound : responseSound;
                source = AudioManager.Instance?.Play(sound, transform.position, (system && system.config ? system.config.responseVolume : .22f) * volumeScale, AudioChannel.SFX, true);
                sourceClip = source ? source.clip : null;
                if (source)
                {
                    responseRemaining = system && system.config ? system.config.responseMaxSeconds : 1.1f;
                    source.maxDistance = Mathf.Max(4, (range > 0 ? range : system && system.config ? system.config.range : 24) + 2);
                    source.minDistance = 1.5f;
                    source.rolloffMode = AudioRolloffMode.Linear;
                }
            }

            if (system)
            {
                if (!string.IsNullOrEmpty(childTrigger))
                    system.SpeakAt(childTrigger, transform.position);
                system.Response(this, context);
                system.MarkResponded(responderId);
            }

            if (!string.IsNullOrEmpty(signalOnResponse) && system && system.game.narrative.NextSignal == signalOnResponse)
                system.game.narrative.Signal(signalOnResponse);
            pulseAge = 1;
            onResponse.Invoke();
        }

        public void Cancel()
        {
            wait = -1;
            responseRemaining = 0;
            if (source && source.clip == sourceClip)
            {
                source.loop = false;
                source.Stop();
            }

            source = null;
            sourceClip = null;
            pulseAge = 0;
            if (vibratingObject)
                vibratingObject.localPosition = restingPosition;
            if (previous != null)
                for (int i = 0; i < pulseRenderers.Length; i++)
                    if (pulseRenderers[i])
                        pulseRenderers[i].SetPropertyBlock(previous[i]);
        }

        internal void PauseSound(bool paused)
        {
            if (!source || source.clip != sourceClip)
                return;
            if (paused)
                source.Pause();
            else
                source.UnPause();
        }
    }
}
