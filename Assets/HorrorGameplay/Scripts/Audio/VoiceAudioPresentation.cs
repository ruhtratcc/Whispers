// Aplica o tratamento de voz de rádio, interfone ou ambiente.

using UnityEngine;

namespace HorrorGame
{
    public enum VoicePlaybackStyle { Natural, Radio, Telephone, Tape, Presence, Intercom }

    /// <summary>Non-destructive playback colour; original recordings stay unchanged.</summary>
    [DisallowMultipleComponent]
    public sealed class VoiceAudioPresentation : MonoBehaviour
    {
        AudioSource source;
        AudioHighPassFilter highPass;
        AudioLowPassFilter lowPass;
        AudioDistortionFilter distortion;
        AudioReverbFilter room;
        AudioEchoFilter speakerEcho;
        AudioSource mediaNoise;
        SoundSet staticNoise;
        bool noiseWanted, noiseStarted;
        Transform anchor;
        Vector3 offset;

        internal static AudioSource PlayInWorld(AudioClip clip, Transform parent, Vector3 position,
            VoicePlaybackStyle style, bool inSchool, float volume, float pitch = 1)
        {
            if (!clip) return null;
            // Media treatment owns its filters. The SFX pool already has an occlusion filter.
            var emitter = new GameObject("Voz filtrada no cenário", typeof(AudioSource), typeof(VoiceAudioPresentation));
            emitter.transform.SetParent(parent, false);
            emitter.transform.position = position;
            var output = emitter.GetComponent<AudioSource>();
            output.playOnAwake = false;
            emitter.GetComponent<VoiceAudioPresentation>().Configure(output, style, inSchool);
            output.spatialBlend = 1;
            output.volume = Mathf.Clamp01(volume);
            output.pitch = Mathf.Clamp(pitch, .1f, 3);
            output.clip = clip;
            output.Play();
            Destroy(emitter, clip.length / output.pitch + .25f);
            return output;
        }

        public void Configure(AudioSource output, VoicePlaybackStyle style, bool inSchool, Transform follow = null, Vector3 localOffset = default)
        {
            source = output;
            if (!source) return;
            AudioManager.Instance?.Route(source,AudioChannel.Voice);
            source.bypassReverbZones=true; // Keep the existing voice treatment when room zones are added.
            if (!highPass) highPass = gameObject.AddComponent<AudioHighPassFilter>();
            if (!lowPass) lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            if (!distortion) distortion = gameObject.AddComponent<AudioDistortionFilter>();
            if (!room) room = gameObject.AddComponent<AudioReverbFilter>();
            anchor = follow; offset = localOffset;
            bool intercom = style == VoicePlaybackStyle.Intercom;
            bool medium = intercom || style == VoicePlaybackStyle.Radio || style == VoicePlaybackStyle.Telephone || style == VoicePlaybackStyle.Tape;
            if (intercom && !speakerEcho)
            {
                speakerEcho = gameObject.AddComponent<AudioEchoFilter>();
                speakerEcho.delay = 95;
                speakerEcho.decayRatio = .23f;
                speakerEcho.dryMix = 1;
                speakerEcho.wetMix = .17f;
            }
            if (speakerEcho) speakerEcho.enabled = intercom;
            highPass.enabled = lowPass.enabled = distortion.enabled = medium;
            highPass.highpassResonanceQ = lowPass.lowpassResonanceQ = 1;
            highPass.cutoffFrequency = intercom ? 420 : style == VoicePlaybackStyle.Telephone ? 320 : style == VoicePlaybackStyle.Radio ? 220 : 120;
            lowPass.cutoffFrequency = intercom ? 2850 : style == VoicePlaybackStyle.Telephone ? 3300 : style == VoicePlaybackStyle.Radio ? 3800 : 5200;
            distortion.distortionLevel = intercom ? .17f : style == VoicePlaybackStyle.Radio ? .12f : style == VoicePlaybackStyle.Telephone ? .04f : .035f;
            room.enabled = inSchool && (intercom || style == VoicePlaybackStyle.Natural || style == VoicePlaybackStyle.Tape || style == VoicePlaybackStyle.Presence);
            room.reverbPreset = AudioReverbPreset.User;
            room.dryLevel = 0;
            room.room = style == VoicePlaybackStyle.Presence ? -1300 : -2200;
            room.roomHF = -2100;
            room.decayTime = style == VoicePlaybackStyle.Presence ? 1.35f : .7f;
            room.decayHFRatio = .55f;
            room.reflectionsLevel = -2600;
            room.reflectionsDelay = .018f;
            room.reverbLevel = style == VoicePlaybackStyle.Presence ? -1500 : -2300;
            room.reverbDelay = .026f;
            room.diffusion = 85; room.density = 75;
            source.volume = style == VoicePlaybackStyle.Radio ? .85f : style == VoicePlaybackStyle.Presence ? .78f : .95f;
            source.pitch = 1;
            source.spatialBlend = intercom || style == VoicePlaybackStyle.Presence ? 1 : style == VoicePlaybackStyle.Radio ? .7f : .2f;
            source.dopplerLevel = 0;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2;
            source.maxDistance = 25;
            noiseWanted = style == VoicePlaybackStyle.Radio || style == VoicePlaybackStyle.Tape;
            if (noiseWanted)
            {
                if (!staticNoise) staticNoise = Resources.Load<SoundSet>("VoiceRadioStatic");
                var noiseClip = staticNoise && staticNoise.clips != null ? System.Array.Find(staticNoise.clips, clip => clip) : null;
                noiseWanted = noiseClip;
                if (noiseClip)
                {
                    if (!mediaNoise)
                    {
                        var hiss = new GameObject("Media hiss"); hiss.transform.SetParent(transform, false);
                        mediaNoise = hiss.AddComponent<AudioSource>(); mediaNoise.playOnAwake = false; mediaNoise.loop = true;
                        var high = hiss.AddComponent<AudioHighPassFilter>(); high.cutoffFrequency = 300;
                        var low = hiss.AddComponent<AudioLowPassFilter>(); low.cutoffFrequency = 3100;
                    }
                    mediaNoise.Stop(); mediaNoise.clip = noiseClip; mediaNoise.volume = 0;
                    mediaNoise.enabled=true;
                    mediaNoise.spatialBlend = source.spatialBlend; mediaNoise.dopplerLevel = 0;
                    mediaNoise.rolloffMode = source.rolloffMode; mediaNoise.minDistance = source.minDistance; mediaNoise.maxDistance = source.maxDistance;
                    noiseStarted = false;
                }
            }
            if (!noiseWanted && mediaNoise) { mediaNoise.Stop(); mediaNoise.clip = null;mediaNoise.enabled=false; noiseStarted = false; }
            LateUpdate();
        }

        void LateUpdate()
        {
            if (source && anchor) source.transform.position = anchor.TransformPoint(offset);
            if (!source || !mediaNoise || !noiseWanted) return;
            if (!source.clip) { mediaNoise.Stop(); noiseStarted = false; return; }
            if (!source.isPlaying || AudioListener.pause) { mediaNoise.Pause(); return; }
            mediaNoise.outputAudioMixerGroup = source.outputAudioMixerGroup;
            mediaNoise.bypassListenerEffects = source.bypassListenerEffects;
            if (!noiseStarted) { mediaNoise.Play(); noiseStarted = true; }
            else if (!mediaNoise.isPlaying) mediaNoise.UnPause();
            float remaining = (source.clip.samples - source.timeSamples) / (float)source.clip.frequency;
            mediaNoise.volume = Mathf.MoveTowards(mediaNoise.volume, .022f * Mathf.Clamp01(remaining / .12f), Time.unscaledDeltaTime * .16f);
        }

        void OnDisable() { if (mediaNoise) mediaNoise.Stop(); noiseStarted = false; }
    }
}
