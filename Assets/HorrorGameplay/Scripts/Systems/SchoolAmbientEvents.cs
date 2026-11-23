// Executa eventos ambientes conforme os pontos e regras da escola.

using UnityEngine;

namespace HorrorGame
{
    public sealed class SchoolAmbientEvents : MonoBehaviour
    {
        public GameSession game;
        public SchoolAmbientConfig config;
        public Light[] eventLights = System.Array.Empty<Light>();
        public InteractiveDoor[] subtleDoors = System.Array.Empty<InteractiveDoor>();
        public RatScurry visibleRats;
        public int RatEvents { get; private set; }
        public int DistantEvents { get; private set; }
        public float LightFrequency => config ? Mathf.Clamp(config.lightFrequency, .1f, 2) : 0;

        float ratAge, objectAge, lightAge, lightOriginal;
        int previousSpot = -1, previousRat = -1, lightIndex = -1;
        bool lightEnabled;
        SchoolLightProfile lightProfile;
        WhistleSystem speech;
        NarrativeDirector director;
        AudioSource ratSource, objectSource;
        void Awake()
        {
            if (!game)
                game = GetComponent<GameSession>();
            speech = GetComponent<WhistleSystem>();
            director = game ? game.narrative : null;
            ratSource = Source("Ratos nas paredes");
            objectSource = Source("Ruído distante");
            ResetTimers();
        }

        AudioSource Source(string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1;
            go.AddComponent<AudioLowPassFilter>();
            AudioManager.Instance?.Route(s, AudioChannel.Ambience);
            return s;
        }

        void OnEnable()
        {
            if (director)
                director.SignalAccepted += Signal;
        }

        void OnDisable()
        {
            if (director)
                director.SignalAccepted -= Signal;
            RestoreLight();
            if (ratSource)
                ratSource.Stop();
            if (objectSource)
                objectSource.Stop();
        }

        void ResetTimers()
        {
            ratAge = config ? Random.Range(config.ratMinimum, Mathf.Max(config.ratMinimum, config.ratMaximum)) : 60;
            objectAge = config ? Random.Range(config.objectMinimum, Mathf.Max(config.objectMinimum, config.objectMaximum)) : 120;
        }

        void Signal(string id)
        {
            if (id == "bd.school.library" || id == "bd.four.gathered" || id == "bd.loop.tape")
                BeginLightEvent();
        }

        public void BeginLightEvent()
        {
            if (!config || eventLights.Length == 0 || PresentationSettings.ReduceEffects)
                return;
            RestoreLight();
            lightIndex = Random.Range(0, eventLights.Length);
            var lamp = eventLights[lightIndex];
            if (!lamp)
            {
                lightIndex = -1;
                return;
            }

            lightOriginal = lamp.intensity;
            lightEnabled = lamp.enabled;
            lightAge = config.lightSeconds;
            lightProfile = config.lightProfile;
            if (config.lightNoise)
                AudioManager.Instance?.Play(config.lightNoise, lamp.transform.position, .06f, AudioChannel.SFX, true);
        }

        bool Ready => game && game.CanControl && director && director.IsInitialized && !director.SequencePlaying && !game.IsRestoringCheckpoint;

        void Update()
        {
            if (!config || !Ready || config.spots.Length == 0)
                return;
            ratAge -= Time.deltaTime;
            objectAge -= Time.deltaTime;
            if (ratAge <= 0)
            {
                ratAge = Random.Range(config.ratMinimum, Mathf.Max(config.ratMinimum, config.ratMaximum));
                Play(false);
            }

            if (objectAge <= 0)
            {
                objectAge = Random.Range(config.objectMinimum, Mathf.Max(config.objectMinimum, config.objectMaximum));
                Play(true);
            }
        }

        void Play(bool distant)
        {
            if (!distant && visibleRats && visibleRats.TryRun())
            {
                RatEvents++;
                return;
            }

            var clips = distant ? config.distantObjects : config.rats;
            if (clips.Length == 0)
                return;
            int spot = PickSpot(distant);
            if (spot < 0)
                return;
            int clip = Random.Range(0, clips.Length);
            if (!distant && clips.Length > 1 && clip == previousRat)
                clip = (clip + 1) % clips.Length;
            previousSpot = spot;
            if (!distant)
                previousRat = clip;
            Vector3 p = config.spots[spot].position;
            var source = distant ? objectSource : ratSource;
            if (!source || source.isPlaying || !clips[clip])
                return;
            var recording = clips[clip].Next();
            if (!recording)
                return;
            source.clip = recording;
            source.transform.position = p;
            source.volume = distant ? config.objectVolume : config.ratVolume;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = .6f;
            source.maxDistance = distant ? 40 : 15;
            source.dopplerLevel = 0;
            var low = source.GetComponent<AudioLowPassFilter>();
            Vector3 delta = game.player.view.playerCamera.transform.position - p;
            float distance = delta.magnitude;
            bool obstructed = Physics.Raycast(p, delta.normalized, Mathf.Max(0, distance - .4f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            low.cutoffFrequency = obstructed ? 1100 : 4200;
            source.Play();
            if (distant)
            {
                DistantEvents++;
                speech?.Speak("ambient.fall");
            }
            else
            {
                RatEvents++;
                float audibleLevel = source.volume * source.minDistance / Mathf.Max(source.minDistance, distance);
                if (source.isPlaying && !obstructed && audibleLevel >= .055f)
                    speech?.Speak("rat.squeak");
            }
        }

        int PickSpot(bool distant)
        {
            // Weighted nearby rooms keep activity in the canteen/library without sounds on top of João.
            float total = 0;
            var origin = game.player.transform.position;
            for (int i = 0; i < config.spots.Length; i++)
            {
                float d = Vector3.Distance(origin, config.spots[i].position);
                if (i != previousSpot && d > (distant ? 9 : 2) && d < (distant ? 40 : 19))
                    total += config.spots[i].weight;
            }

            if (total <= 0)
                return -1;
            float roll = Random.value * total;
            for (int i = 0; i < config.spots.Length; i++)
            {
                float d = Vector3.Distance(origin, config.spots[i].position);
                if (i == previousSpot || d <= (distant ? 9 : 2) || d >= (distant ? 40 : 19))
                    continue;
                roll -= config.spots[i].weight;
                if (roll <= 0)
                    return i;
            }

            return -1;
        }

        void LateUpdate()
        {
            if (lightIndex < 0 || !config)
                return;
            if (PresentationSettings.ReduceEffects)
            {
                RestoreLight();
                return;
            }

            if (!Ready)
                return;
            lightAge -= Time.deltaTime;
            var lamp = eventLights[lightIndex];
            if (lamp)
            {
                lamp.enabled = true;
                float wave = .5f + .5f * Mathf.Sin(lightAge * Mathf.PI * 2 * LightFrequency);
                float envelope = Mathf.Clamp01(lightAge / Mathf.Max(.1f, config.lightSeconds));
                float multiplier = lightProfile == SchoolLightProfile.BriefFailure ? (envelope > .35f && envelope < .6f ? .05f : Mathf.Lerp(.3f, .8f, wave)) : lightProfile == SchoolLightProfile.Dying ? envelope * envelope : lightProfile == SchoolLightProfile.Hum ? Mathf.Lerp(.65f, .8f, wave) : Mathf.Lerp(.32f, .8f, wave);
                lamp.intensity = Mathf.Max(.25f, lightOriginal) * multiplier;
            }

            if (lightAge <= 0)
                RestoreLight();
        }

        void RestoreLight()
        {
            if (lightIndex >= 0 && lightIndex < eventLights.Length && eventLights[lightIndex])
            {
                eventLights[lightIndex].intensity = lightOriginal;
                eventLights[lightIndex].enabled = lightEnabled;
            }

            lightIndex = -1;
            lightAge = 0;
        }
    }
}
