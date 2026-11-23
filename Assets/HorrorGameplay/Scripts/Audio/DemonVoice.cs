// Escolhe e mistura as camadas da voz de Helena no corredor e no espelho.

using System;
using System.Linq;
using UnityEngine;

namespace HorrorGame
{
    [DefaultExecutionOrder(140), DisallowMultipleComponent]
    public sealed class DemonVoice : MonoBehaviour
    {
        public GameSession game;
        public DemonVoiceConfig config;
        public BeforeDemolitionTwoVoicesFlow corridor;
        public BeforeDemolitionMirrorFlow mirror;
        public float MaskSlip { get; private set; }
        public bool IsSpeaking => pendingLow || maskSource && maskSource.isPlaying || lowSource && lowSource.isPlaying;
        public SpeechPlan LastPlan { get; private set; }
        public string CurrentLineId { get; private set; } = "";
        public float LowStartedAt { get; private set; } = -1;
        public float MaskStartedAt { get; private set; } = -1;
        public AudioSource MaskSource => maskSource;
        public AudioSource LowSource => lowSource;

        public struct SpeechPlan
        {
            public AudioClip mask, low;
            public float maskGain, lowGain, delay, pitch, seconds;
            public bool fallback;
        }

        AudioSource maskSource, lowSource, choir, reverseChoir;
        bool queued, pendingLow, inspectionPaused, ownsCaption;
        float lowDelay, captionRemaining;
        float mirrorAge, mirrorDuration;
        bool mirrorTransition;
        string ownText;
        Transform follow;
        NarrativeDirector Director => game ? game.narrative : null;

        bool Active => config && Director && Director.IsInitialized && BeforeDemolitionCampaign.Is(Director.book) && (Director.Progress.chapterId == BeforeDemolitionTwoVoicesFlow.ChapterId || Director.Progress.chapterId == BeforeDemolitionMirrorFlow.ChapterId);

        void Awake()
        {
            if (!game)
                game = GetComponent<GameSession>();
            if (!corridor)
                corridor = GetComponent<BeforeDemolitionTwoVoicesFlow>();
            if (!mirror)
                mirror = GetComponentInChildren<BeforeDemolitionMirrorFlow>(true);
        }

        public void SetMaskSlip(float value) => MaskSlip = Mathf.Clamp01(value);
        public void QueueCorridor()
        {
            queued = true;
        }

        public void RestoreSilently()
        {
            queued = false;
            StopSpeech();
            SetMaskSlip(corridor ? Mathf.Clamp01(corridor.TraversalCount * .5f) : 0);
        }

        public bool HandlesSequence(string sequence) => config && Active && (sequence == "bd.loop.voice" || sequence == BeforeDemolitionMirrorFlow.OfferSequence || sequence == BeforeDemolitionMirrorFlow.RefuseSequence || sequence == BeforeDemolitionMirrorFlow.SignSequence);
        DemonLine CorridorLine()
        {
            int count = corridor ? corridor.TraversalCount : 0;
            SetMaskSlip(Mathf.Clamp01(count * .5f));
            var options = config.lines.Where(l => l != null && !l.mirror && l.stage == DemonVoiceConfig.Stage(MaskSlip)).ToArray();
            return options.Length == 0 ? null : options[(Mathf.Max(0, count - 2) + count / 3) % options.Length];
        }

        public StoryLine Prepare(StoryLine authored, string sequence, int index)
        {
            if (authored == null || !HandlesSequence(sequence))
                return authored;
            var entry = sequence == "bd.loop.voice" && index == 0 ? CorridorLine() : config.Find(authored.demonVoiceId);
            if (entry == null)
                return authored;
            if (entry.mirror)
                SetMaskSlip(entry.lowOnly ? 1 : entry.mirrorSlip);
            var plan = Plan(entry, entry.mirrorTransition ? .5f : MaskSlip, config);
            if (entry.mirrorTransition)
                plan.seconds = Mathf.Max(plan.seconds, Plan(entry, 1, config).seconds);
            plan.seconds = Mathf.Max(plan.seconds, authored.seconds);
            foreach (var clip in new[]
            {
                entry.mask,
                entry.low
            }

            )
                if (clip && clip.loadState == AudioDataLoadState.Unloaded)
                    clip.LoadAudioData();
            return new StoryLine
            {
                speaker = authored.speaker,
                text = entry.text,
                seconds = plan.seconds,
                voice = plan.mask ? plan.mask : plan.low,
                demonVoiceId = entry.id,
                playbackSeconds = plan.seconds
            };
        }

        public static SpeechPlan Plan(DemonLine line, float slip, DemonVoiceConfig settings)
        {
            if (line == null || !settings)
                return default;
            int stage = line.lowOnly ? 3 : DemonVoiceConfig.Stage(slip);
            float mask = stage == 3 ? 0 : settings.maskGain;
            float low = stage == 1 ? 0 : settings.lowGain * (stage == 2 ? Mathf.Clamp01(slip) : 1);
            float sum = mask + low, scale = sum > settings.voiceCeiling ? settings.voiceCeiling / sum : 1;
            var result = new SpeechPlan
            {
                mask = stage == 3 ? null : line.mask,
                low = stage == 1 ? null : (line.low ? line.low : line.mask),
                maskGain = mask * scale,
                lowGain = low * scale,
                delay = stage == 2 ? Mathf.Clamp((settings.layerDelay.x + settings.layerDelay.y) * .5f, .08f, .15f) : 0,
                pitch = stage == 3 ? settings.slowPitch : 1,
                fallback = stage != 1 && !line.low && line.mask,
                seconds = Mathf.Max(.5f, line.captionSeconds)
            };
            if (result.mask)
                result.seconds = Mathf.Max(result.seconds, result.mask.length + .2f);
            if (result.low)
                result.seconds = Mathf.Max(result.seconds, result.low.length / result.pitch + result.delay + .2f);
            return result;
        }

        public bool AudioReady(StoryLine line)
        {
            var entry = config ? config.Find(line?.demonVoiceId) : null;
            return entry == null || (!entry.mask || entry.mask.loadState != AudioDataLoadState.Loading) && (!entry.low || entry.low.loadState != AudioDataLoadState.Loading);
        }

        public bool TryPlay(StoryLine line, string sequence)
        {
            if (!HandlesSequence(sequence) || string.IsNullOrEmpty(line?.demonVoiceId))
                return false;
            var entry = config.Find(line.demonVoiceId);
            if (entry == null)
                return false;
            Play(entry, sequence == "bd.loop.voice" && corridor ? corridor.voiceAnchor : mirror ? mirror.mirrorPlane : null);
            return true;
        }

        AudioSource Source(string name, bool speech = true)
        {
            var g = new GameObject(name);
            g.transform.SetParent(transform, false);
            var s = g.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.ignoreListenerPause = false;
            s.spatialBlend = 1;
            s.dopplerLevel = 0;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 2;
            s.maxDistance = config.maxDistance;
            s.volume = 0;
            AudioManager.Instance?.Route(s, speech ? AudioChannel.Voice : AudioChannel.Ambience);
            return s;
        }

        public void Play(DemonLine entry, Transform anchor)
        {
            StopSpeech();
            if (!config || entry == null)
                return;
            if (entry.mirror)
                SetMaskSlip(entry.lowOnly ? 1 : entry.mirrorSlip);
            LastPlan = Plan(entry, entry.mirrorTransition ? .5f : MaskSlip, config);
            CurrentLineId = entry.id;
            LowStartedAt = MaskStartedAt = -1;
            follow = anchor;
            mirrorTransition = entry.mirrorTransition;
            mirrorAge = 0;
            mirrorDuration = Mathf.Max(LastPlan.seconds, Plan(entry, 1, config).seconds);
            if (!maskSource)
            {
                maskSource = Source("Demon — mask");
                lowSource = Source("Demon — low");
            }

            maskSource.outputAudioMixerGroup = config.maskGroup;
            lowSource.outputAudioMixerGroup = config.lowGroup;
            if (config.lowGroup)
                config.lowGroup.audioMixer.SetFloat(config.pitchParameter, LastPlan.fallback ? config.fallbackPitch : 1);
            maskSource.clip = LastPlan.mask;
            maskSource.volume = LastPlan.maskGain;
            maskSource.pitch = 1;
            lowSource.clip = LastPlan.low;
            lowSource.volume = LastPlan.lowGain;
            lowSource.pitch = LastPlan.pitch;
            if (mirrorTransition)
            {
                maskSource.volume = config.maskGain;
                lowSource.volume = 0;
            }

            Follow();
            if (LastPlan.mask)
            {
                maskSource.Play();
                MaskStartedAt = Time.unscaledTime;
            }

            if (LastPlan.low)
            {
                lowDelay = LastPlan.delay;
                pendingLow = true;
                if (lowDelay <= 0)
                    StartLow();
            }
        }

        void StartLow()
        {
            if (!lowSource || !lowSource.clip)
                return;
            lowSource.Play();
            pendingLow = false;
            LowStartedAt = Time.unscaledTime;
        }

        void Follow()
        {
            if (!follow)
                return;
            if (maskSource)
                maskSource.transform.position = follow.position;
            if (lowSource)
                lowSource.transform.position = follow.position;
        }

        public void PauseSpeech()
        {
            if (inspectionPaused)
                return;
            inspectionPaused = true;
            if (maskSource)
                maskSource.Pause();
            if (lowSource)
                lowSource.Pause();
        }

        public void ResumeSpeech()
        {
            if (!inspectionPaused)
                return;
            inspectionPaused = false;
            if (maskSource)
                maskSource.UnPause();
            if (lowSource)
                lowSource.UnPause();
            if (ownsCaption && captionRemaining > 0)
                game.hud?.ShowSubtitle(ownText, captionRemaining);
        }

        public void StopSpeech()
        {
            if (maskSource)
                maskSource.Stop();
            if (lowSource)
                lowSource.Stop();
            pendingLow = false;
            inspectionPaused = false;
            ownsCaption = false;
            captionRemaining = 0;
            follow = null;
            mirrorTransition = false;
        }

        void Update()
        {
            if (!Active)
            {
                StopSpeech();
                queued = false;
                StopChoir();
                return;
            }

            bool inCorridor = corridor && corridor.IsActive && !corridor.Escaped;
            if (inCorridor)
            {
                SetMaskSlip(Mathf.Clamp01(corridor.TraversalCount * .5f));
                if (corridor.voiceAnchor && game.player && game.player.view)
                    corridor.voiceAnchor.position = game.player.view.playerCamera.transform.position + corridor.returnPose.forward * config.voiceDistance;
            }

            if (game.State == GameState.Paused || game.State == GameState.Inspecting)
            {
                PauseSpeech();
                return;
            }

            ResumeSpeech();
            if (pendingLow)
            {
                lowDelay -= Time.unscaledDeltaTime;
                if (lowDelay <= 0 && lowSource.clip.loadState != AudioDataLoadState.Loading)
                    StartLow();
            }

            if (mirrorTransition)
            {
                mirrorAge += Time.unscaledDeltaTime;
                SetMaskSlip(Mathf.Clamp01(mirrorAge / Mathf.Max(.1f, mirrorDuration * .85f)));
                var entry = config.Find(CurrentLineId);
                var plan = Plan(entry, MaskSlip, config);
                maskSource.volume = plan.maskGain;
                lowSource.volume = plan.lowGain;
                lowSource.pitch = plan.pitch;
                if (MaskSlip >= .75f && maskSource.isPlaying)
                    maskSource.Stop();
            }

            Follow();
            UpdateChoir(inCorridor || mirror && mirror.IsActive);
            if (queued && inCorridor && game.CanControl && !Director.SequencePlaying && !IsSpeaking && !ownsCaption)
            {
                queued = false;
                var entry = CorridorLine();
                if (entry != null)
                {
                    Play(entry, corridor.voiceAnchor);
                    ownText = "Helena: " + entry.text;
                    captionRemaining = LastPlan.seconds;
                    ownsCaption = true;
                    game.hud?.ShowSubtitle(ownText, captionRemaining);
                }
            }

            if (ownsCaption)
            {
                captionRemaining -= Time.unscaledDeltaTime;
                if (captionRemaining <= 0)
                {
                    ownsCaption = false;
                    StopSpeech();
                }
            }
        }

        void UpdateChoir(bool active)
        {
            if (!config)
                return;
            if (!choir && config.choirHum)
            {
                choir = Source("Demon — distant choir", false);
                choir.loop = true;
                choir.clip = config.choirHum;
                choir.outputAudioMixerGroup = config.maskGroup;
            }

            if (!reverseChoir && config.choirHumReversed)
            {
                reverseChoir = Source("Demon — reversed choir", false);
                reverseChoir.loop = true;
                reverseChoir.clip = config.choirHumReversed;
                reverseChoir.outputAudioMixerGroup = config.maskGroup;
            }

            TickChoir(choir, active);
            TickChoir(reverseChoir, active);
        }

        void TickChoir(AudioSource s, bool active)
        {
            if (!s)
                return;
            if (active && !s.isPlaying)
                s.Play();
            s.volume = Mathf.MoveTowards(s.volume, active ? config.choirGain * MaskSlip * .5f : 0, Time.unscaledDeltaTime * .1f);
            if (corridor && corridor.IsActive)
                s.transform.position = corridor.quietAnchor.position;
            else if (mirror && mirror.mirrorPlane)
                s.transform.position = mirror.mirrorPlane.position;
        }

        void StopChoir()
        {
            if (choir)
                choir.Stop();
            if (reverseChoir)
                reverseChoir.Stop();
        }

        void OnDisable()
        {
            StopSpeech();
            StopChoir();
            queued = false;
            if (config && config.lowGroup)
                config.lowGroup.audioMixer.ClearFloat(config.pitchParameter);
        }
    }
}
