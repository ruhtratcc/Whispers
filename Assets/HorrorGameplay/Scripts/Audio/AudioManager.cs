// Toca e reaproveita fontes de som, ajustando volume, posição e canais do mixer.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace HorrorGame
{
    public enum AudioChannel
    {
        Music,
        SFX,
        Ambience,
        Voice,
        UI,
        PC,
        Phone
    }

    [DefaultExecutionOrder(-180)]
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Mixer groups")]
        [SerializeField]
        public AudioMixer mixer;
        [SerializeField]
        public AudioMixerGroup musicGroup, sfxGroup, ambienceGroup, voiceGroup;
        public AudioMixerGroup uiGroup,pcGroup,phoneGroup;
        public AudioCueLibrary cues;
        [Header("Loop layers")]
        [SerializeField]
        public AudioSource calmMusic, tenseMusic, ambience, breathing;
        [SerializeField, Range(0, 1)]
        float musicGain = .2f, ambienceGain = .28f, breathGain = .26f;
        [SerializeField, Range(4, 64)]
        int spatialVoices = 24;
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly List<AudioLowPassFilter> occlusionFilters = new List<AudioLowPassFilter>();
        readonly List<bool> occlusionEligible = new List<bool>();
        readonly List<float> occlusionCutoffs = new List<float>();
        readonly List<float> baseGains=new List<float>();
        readonly List<AudioChannel> poolChannels=new List<AudioChannel>();
        readonly List<AudioSource> externalVoices=new List<AudioSource>();
        float duck=1,ambientLayer=1,musicWindow,musicCooldown;
        float captionAfter;
        public float DuckLevel=>duck;
        float tutorialDuck=1;bool tutorialDucking;
        public void SetTutorialDuck(bool value){tutorialDucking=value;}
        public float TutorialDuckLevel=>tutorialDuck;
        readonly RaycastHit[] occlusionHits = new RaycastHit[32];
        AudioListener roomListener;
        float occlusionTimer;
        const float ClearCutoff = 22000, BlockedCutoff = 1800;
        float tension, breath;
        void Awake()
        {
            using(LoadingMetrics.Measure("AudioManager.Awake")){
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            for (int i = 0; i < spatialVoices; i++)
            {
                var g = new GameObject("Spatial voice " + i);
                g.transform.SetParent(transform);
                var s = g.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.dopplerLevel = 0;
                s.minDistance = 1;
                s.maxDistance = 22;
                pool.Add(s);
                var filter = g.AddComponent<AudioLowPassFilter>();
                filter.cutoffFrequency = ClearCutoff;
                filter.enabled = false;
                occlusionFilters.Add(filter);
                occlusionEligible.Add(false);
                occlusionCutoffs.Add(ClearCutoff);
                baseGains.Add(0);poolChannels.Add(AudioChannel.SFX);
            }
        
            }
        }

        void Start()
        {
            using(LoadingMetrics.Measure("AudioManager.Start")){
            ApplySettings();
            if (calmMusic) calmMusic.volume = 0;
            if (tenseMusic) tenseMusic.volume = 0;
            if (ambience) ambience.volume = ambienceGain;
            if (breathing) breathing.volume = 0;
            StartLoop(calmMusic, musicGroup);
            StartLoop(tenseMusic, musicGroup);
            StartLoop(ambience, ambienceGroup);
            StartLoop(breathing, voiceGroup);
        
            }
        }

        void StartLoop(AudioSource source, AudioMixerGroup group)
        {
            if (!source)
                return;
            source.outputAudioMixerGroup = group;
            source.loop = true;
            if (source.clip)
                source.Play();
        }

        public void ApplySettings()
        {
            AudioListener.volume = PlayerPrefs.GetFloat("HorrorMenu.Master", .8f);
            Set("MusicVolume", PlayerPrefs.GetFloat("HorrorMenu.Music", PresentationSettings.DefaultMusic));
            float sfx = PlayerPrefs.GetFloat("HorrorMenu.SFX", .8f);
            Set("SFXVolume", sfx);
            Set("AmbienceVolume", PlayerPrefs.GetFloat("HorrorMenu.Ambience", sfx));
            Set("VoiceVolume", PlayerPrefs.GetFloat("HorrorMenu.Voice", sfx));
        }

        void Set(string parameter, float level)
        {
            if (mixer)
                mixer.SetFloat(parameter, level <= .0001f ? -80 : Mathf.Log10(Mathf.Clamp01(level)) * 20);
        }

        public void SetTension(float value) => tension = Mathf.Clamp01(value);
        float whistleBreath;
        public void SetWhistleBreathing(float value)=>whistleBreath=Mathf.Clamp01(value);
        public float BreathingLevel => breath;
        public void SetBreathing(float value, bool immediate = false)
        {
            breath = Mathf.Clamp01(value);
            if (immediate && breathing) breathing.volume = breathGain * breath;
        }
        void Update()
        {
            float targetTutorial=tutorialDucking?.18f:1;
            if(tutorialDuck!=targetTutorial){tutorialDuck=Mathf.MoveTowards(tutorialDuck,targetTutorial,Time.unscaledDeltaTime*4);Set("MusicVolume",PlayerPrefs.GetFloat("HorrorMenu.Music",PresentationSettings.DefaultMusic)*tutorialDuck);float effects=PlayerPrefs.GetFloat("HorrorMenu.SFX",.8f);Set("SFXVolume",effects*tutorialDuck);Set("AmbienceVolume",PlayerPrefs.GetFloat("HorrorMenu.Ambience",effects)*tutorialDuck);Set("VoiceVolume",PlayerPrefs.GetFloat("HorrorMenu.Voice",effects)*tutorialDuck);}
            bool important=HasImportantVoice();
            duck=Mathf.MoveTowards(duck,important?.48f:1,Time.unscaledDeltaTime*(important?3: .65f));
            Set("MusicVolume",PlayerPrefs.GetFloat("HorrorMenu.Music",PresentationSettings.DefaultMusic)*duck);
            Set("AmbienceVolume",PlayerPrefs.GetFloat("HorrorMenu.Ambience",PlayerPrefs.GetFloat("HorrorMenu.SFX",.8f))*duck);
            musicCooldown=Mathf.Max(0,musicCooldown-Time.deltaTime);musicWindow=Mathf.Max(0,musicWindow-Time.deltaTime);
            if(tension>.7f&&musicCooldown<=0&&SceneManager.GetActiveScene().name=="SchoolCEEP") {musicWindow=9;musicCooldown=95;}
            if (calmMusic)
                calmMusic.volume = 0;
            if (tenseMusic)
                tenseMusic.volume = Mathf.Lerp(tenseMusic.volume, musicWindow>0?musicGain*.55f*tension:0, Time.unscaledDeltaTime * 2);
            if (ambience)
                ambience.volume = Mathf.MoveTowards(ambience.volume,ambienceGain*ambientLayer,Time.unscaledDeltaTime*.18f);
            if (breathing)
                breathing.volume = Mathf.Lerp(breathing.volume, Mathf.Max(breathGain * breath,.09f*whistleBreath), Time.unscaledDeltaTime * 3);
            UpdateRoomOcclusion();
        }
        public void SetAmbienceLayer(float value)=>ambientLayer=Mathf.Clamp01(value);
        public void Route(AudioSource source,AudioChannel channel)
        {
            if(!source)return;source.outputAudioMixerGroup=Group(channel);
            int pooled=pool.IndexOf(source);if(pooled>=0){poolChannels[pooled]=channel;return;}
            if(channel==AudioChannel.Voice&&!externalVoices.Contains(source))externalVoices.Add(source);
        }
        bool HasImportantVoice()
        {
            if(AudioListener.pause)return false;
            if(InspectionManager.Instance&&InspectionManager.Instance.RecordingPlaying)return true;
            var session=GameSession.Instance;if(session&&session.narrative&&session.narrative.SequencePlaying)return true;
            for(int i=externalVoices.Count-1;i>=0;i--){if(!externalVoices[i])externalVoices.RemoveAt(i);else if(externalVoices[i].isPlaying)return true;}
            for(int i=0;i<pool.Count;i++)if(poolChannels[i]==AudioChannel.Voice&&pool[i].isPlaying)return true;
            return false;
        }
        public AudioSource PlayCue(string id,Vector3 position,float gain=.25f)
        {
            var cue=cues?cues.Find(id):null;
            return cue==null?null:Play(cue.sound,position,gain,cue.channel,cue.spatial);
        }

        void UpdateRoomOcclusion()
        {
            if (AudioListener.pause || Time.timeScale <= 0) return;
            occlusionTimer -= Time.deltaTime;
            if (occlusionTimer <= 0)
            {
                occlusionTimer = .15f;
                string scene = SceneManager.GetActiveScene().name;
                bool useRooms = scene == "SchoolCEEP" || scene == "RoadJourney" || scene=="Chapter1Office";
                if (!roomListener || !roomListener.isActiveAndEnabled)
                {
                    roomListener = null;
                    foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                        if (listener.isActiveAndEnabled) { roomListener = listener; break; }
                }
                for (int i = 0; i < pool.Count; i++)
                {
                    var source = pool[i];
                    bool eligible = useRooms && roomListener && occlusionEligible[i] &&
                        source.isPlaying && source.spatialBlend > .5f;
                    occlusionCutoffs[i] = eligible && IsOccluded(source, roomListener.transform.position)
                        ? BlockedCutoff : ClearCutoff;
                    if (!eligible) ResetOcclusion(i);
                }
            }
            float blend = 1 - Mathf.Exp(-Time.deltaTime * 10);
            for (int i = 0; i < pool.Count; i++)
            {
                var filter = occlusionFilters[i];
                var source=pool[i];
                if(source.isPlaying&&source.spatialBlend>.5f&&roomListener)
                {
                    float d=Vector3.Distance(source.transform.position,roomListener.transform.position);
                    float edge=Mathf.Clamp01((source.maxDistance-d)/Mathf.Max(.1f,source.maxDistance*.2f));
                    source.volume=baseGains[i]*edge;
                }
                if (occlusionCutoffs[i] < ClearCutoff) filter.enabled = true;
                if (!filter.enabled) continue;
                filter.cutoffFrequency = Mathf.Lerp(filter.cutoffFrequency, occlusionCutoffs[i], blend);
                if (occlusionCutoffs[i] == ClearCutoff && filter.cutoffFrequency > ClearCutoff - 20)
                    ResetOcclusion(i);
            }
        }

        bool IsOccluded(AudioSource source, Vector3 listenerPosition)
        {
            Vector3 direction = listenerPosition - source.transform.position;
            float distance = direction.magnitude;
            if (distance < .4f || distance > source.maxDistance) return false;
            // Starting at the emitter skips its own containing collider; triggers never muffle sound.
            int count = Physics.RaycastNonAlloc(source.transform.position, direction / distance,
                occlusionHits, distance - .2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = occlusionHits[i];
                if (hit.distance < .08f || hit.collider.GetComponentInParent<FirstPersonMotor>() ||
                    hit.collider.transform.IsChildOf(source.transform)) continue;
                return true;
            }
            return false;
        }

        void ResetOcclusion(int index)
        {
            occlusionFilters[index].enabled = false;
            occlusionFilters[index].cutoffFrequency = ClearCutoff;
            occlusionCutoffs[index] = ClearCutoff;
        }

        AudioMixerGroup Group(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Music:
                    return musicGroup;
                case AudioChannel.Ambience:
                    return ambienceGroup;
                case AudioChannel.Voice:
                    return voiceGroup;
                case AudioChannel.UI:return uiGroup?uiGroup:sfxGroup;
                case AudioChannel.PC:return pcGroup?pcGroup:sfxGroup;
                case AudioChannel.Phone:return phoneGroup?phoneGroup:sfxGroup;
                default:
                    return sfxGroup;
            }
        }

        public AudioSource Play(SoundSet sound, Vector3 position, float gain = 1, AudioChannel channel = AudioChannel.SFX, bool spatial = true, bool duringPause = false)
        {
            if (!sound)
                return null;
            if(sound.overrideChannel)channel=sound.channel;
            var clip = sound.Next();
            if (!clip)
                return null;
            var source = PlayClip(clip, position, gain * sound.Volume, sound.Pitch, channel, spatial, duringPause);
            if (source && spatial)
            {
                source.minDistance = Mathf.Max(.05f, sound.minimumDistance);
                source.maxDistance = Mathf.Max(source.minDistance + .1f, sound.maximumDistance);
            }
            var hud=GameSession.Instance?GameSession.Instance.hud:null;
            if(source&&!string.IsNullOrEmpty(sound.caption)&&hud&&hud.subtitleGroup.alpha<.05f&&Time.unscaledTime>=captionAfter
                &&(!spatial||!roomListener||Vector3.Distance(position,roomListener.transform.position)<sound.maximumDistance))
            {hud.ShowSubtitle(sound.caption,1.6f);captionAfter=Time.unscaledTime+2;}
            return source;
        }

        public AudioSource PlayClip(AudioClip clip, Vector3 position, float gain = 1, float pitch = 1, AudioChannel channel = AudioChannel.SFX, bool spatial = true, bool duringPause = false)
        {
            if (!clip)
                return null;
            AudioSource source = pool.Find(s => !s.isPlaying);
            if (!source)
                return null;
            int voiceIndex = pool.IndexOf(source);
            ResetOcclusion(voiceIndex);
            occlusionEligible[voiceIndex] = spatial && channel != AudioChannel.Music;
            source.transform.position = position;
            source.outputAudioMixerGroup = Group(channel);
            source.spatialBlend = spatial ? 1 : 0;
            source.bypassReverbZones=channel==AudioChannel.Voice||!spatial;
            source.minDistance = 1;
            source.maxDistance = 22;
            source.volume = Mathf.Clamp01(gain);
            baseGains[voiceIndex]=source.volume;poolChannels[voiceIndex]=channel;
            source.pitch = Mathf.Clamp(pitch, .1f, 3);
            source.ignoreListenerPause = duringPause;
            source.loop=false;
            source.panStereo=0;
            source.clip = clip;
            source.Play();
            return source;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
