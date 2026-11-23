// Liga iluminação, ambiente e tensões específicas da escola.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    /// <summary>Scene-owned school ambience: isolated real Foley, long silence and one short light fault.</summary>
    [DisallowMultipleComponent]
    public sealed class SchoolHorrorAtmosphere : MonoBehaviour
    {
        [Serializable]
        public sealed class AmbientPoint
        {
            public Transform origin;
            public SoundSet sound;
            [Min(1)] public float hearingRadius = 17;
            [Range(0, 1)] public float gain = .32f;
            [Range(3,6)] public int minimumChapter=3;
        }

        [SerializeField] public GameSession session;
        [SerializeField] public Transform listener;
        [SerializeField] public VintageCameraEffect cameraEffect;
        [SerializeField] public AmbientPoint[] points = Array.Empty<AmbientPoint>();
        [SerializeField] public Light[] flickerLights = Array.Empty<Light>();
        [SerializeField] public float[] fixtureBaseIntensity = Array.Empty<float>();
        [SerializeField, Min(15)] public float initialSilence = 36;
        [SerializeField] public Vector2 quietInterval = new Vector2(38, 68);
        [SerializeField] public bool automaticAmbience = true;
        [Header("Chapter 3 — unpowered fixtures and event pacing")]
        public bool abandonedPower;
        public SchoolSanityController sanity;
        public UnityEvent<float> onUnsettlingCue = new UnityEvent<float>();

        float elapsed, nextCue, faultTime;
        int cueCount, selectedLight = -1, lastPoint = -1;
        AudioSource lastVoice;
        AudioClip lastPlayedClip;
        float voiceExpires;
        bool[] fixtureWasEnabled;
        readonly System.Random variation = new System.Random(63071);

        public bool SchoolScene => gameObject.scene.name == "SchoolCEEP";
        public float AmbientClock => elapsed;
        public int CueCount => cueCount;
        public bool LightFaultActive => selectedLight >= 0;

        void OnEnable()
        {
            elapsed = 0;
            nextCue = Mathf.Max(15, initialSilence);
            selectedLight = -1;
            fixtureWasEnabled = new bool[flickerLights.Length];
            for (int i=0;i<flickerLights.Length;i++) fixtureWasEnabled[i]=flickerLights[i] && flickerLights[i].enabled;
        }

        void Update()
        {
            AdvanceAtmosphere(Time.deltaTime, Time.timeScale <= 0 || (session && !session.CanControl));
        }

        public void AdvanceAtmosphere(float deltaTime, bool paused)
        {
            if (!SchoolScene || !isActiveAndEnabled) return;
            if (paused || (session && !session.CanControl))
            {
                if(abandonedPower){RestoreLight();StopOwnedVoice();}
                return;
            }
            if (deltaTime <= 0) return;
            if(session&&session.narrative&&session.narrative.SequencePlaying)return;
            elapsed += deltaTime;
            UpdateLightFault(deltaTime);
            if (!automaticAmbience || !listener || elapsed < nextCue) return;
            float pressure = sanity ? 1-sanity.Sanity01 : 0;
            nextCue = elapsed + Mathf.Lerp(Mathf.Max(20, quietInterval.x), Mathf.Max(20, quietInterval.y), (float)variation.NextDouble()) * Mathf.Lerp(1,.75f,pressure);
            // A cue is eligible only in its real acoustic area. No global whispers or looping hiss.
            int chosen = -1;
            float distance = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];
                if (point == null || !point.origin || !point.sound || i == lastPoint) continue;
                string chapter=session&&session.narrative?session.narrative.Progress.chapterId:"";
                int number=chapter==BeforeDemolitionMirrorFlow.ChapterId?6:chapter==BeforeDemolitionTwoVoicesFlow.ChapterId?5:chapter==BeforeDemolitionFourStudentsFlow.ChapterId?4:3;
                if(number<point.minimumChapter)continue;
                float d = Vector3.Distance(listener.position, point.origin.position);
                if (d < point.hearingRadius && d < distance) { distance = d; chosen = i; }
            }
            if (chosen < 0) { lastPoint = -1; return; }
            var cue = points[chosen];
            lastVoice = AudioManager.Instance?.Play(cue.sound, cue.origin.position, cue.gain, AudioChannel.Ambience, true);
            if (!lastVoice) return;
            lastVoice.minDistance = .7f;
            lastVoice.maxDistance = cue.hearingRadius;
            lastPlayedClip = lastVoice.clip;
            voiceExpires = elapsed + (lastPlayedClip ? lastPlayedClip.length / Mathf.Max(.1f, lastVoice.pitch) : 0);
            lastPoint = chosen;
            cueCount++;
            onUnsettlingCue.Invoke(.8f);
            if (cueCount % 3 == 0) BeginLightFault(.4f);
        }

        /// <summary>Opening flow can punctuate an authored event without changing a route or player controls.</summary>
        public void ReactToEvent(float strength = .65f)
        {
            if (!SchoolScene || !isActiveAndEnabled || Time.timeScale <= 0) return;
            cameraEffect?.PulseInterference(strength, .18f);
            // Authored shocks receive a quiet recovery window before another ambient event.
            nextCue = Mathf.Max(nextCue, elapsed + 24);
            if (!session || !session.narrative || !session.narrative.HasSignal("school.blackout")) BeginLightFault(strength);
            else RestoreLight();
        }

        void BeginLightFault(float strength)
        {
            RestoreLight();
            if (flickerLights.Length == 0) return;
            // Keep the nearest navigation light stable; choose a visible background fixture.
            float distance = float.MaxValue;
            for (int i = 0; i < flickerLights.Length && i < fixtureBaseIntensity.Length; i++)
            {
                var light = flickerLights[i];
                if (!light || (!abandonedPower && !light.enabled)) continue;
                float d = listener ? Vector3.Distance(light.transform.position, listener.position) : 6;
                if (d < 4 || d > 25 || d >= distance) continue;
                distance = d; selectedLight = i;
            }
            if (selectedLight >= 0)
            {
                faultTime = .30f + Mathf.Clamp01(strength) * .20f;
                if (abandonedPower) flickerLights[selectedLight].enabled=true;
            }
        }

        void UpdateLightFault(float deltaTime)
        {
            if (selectedLight < 0) return;
            faultTime -= deltaTime;
            if (faultTime <= 0 || selectedLight >= flickerLights.Length || !flickerLights[selectedLight])
            { RestoreLight(); return; }
            // A single soft dip/recovery, not a rapid flash or blackout.
            float dip = Mathf.Sin(Mathf.Clamp01(faultTime / .5f) * Mathf.PI);
            flickerLights[selectedLight].intensity = fixtureBaseIntensity[selectedLight] * (abandonedPower ? dip*.65f : 1-dip*.28f);
        }

        void RestoreLight()
        {
            if (selectedLight >= 0 && selectedLight < flickerLights.Length
                && selectedLight < fixtureBaseIntensity.Length && flickerLights[selectedLight])
            {
                flickerLights[selectedLight].intensity = fixtureBaseIntensity[selectedLight];
                if (abandonedPower) flickerLights[selectedLight].enabled = fixtureWasEnabled != null && selectedLight < fixtureWasEnabled.Length && fixtureWasEnabled[selectedLight];
            }
            selectedLight = -1;
        }

        void OnDisable()
        {
            RestoreLight();
            StopOwnedVoice();
        }
        void StopOwnedVoice()
        {
            if (lastVoice && lastVoice.isPlaying && lastVoice.clip == lastPlayedClip && elapsed < voiceExpires
                && lastVoice.outputAudioMixerGroup == AudioManager.Instance?.ambienceGroup)
                lastVoice.Stop();
            lastVoice = null;
            lastPlayedClip = null;
        }
    }
}
