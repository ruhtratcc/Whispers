// Posiciona e regula ambientes sonoros no espaço.

using UnityEngine;

namespace HorrorGame
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class SpatialAmbience : MonoBehaviour
    {
        [SerializeField]
        public AudioClip loop;
        [SerializeField]
        public AudioChannel channel = AudioChannel.Ambience;
        [SerializeField, Range(0, 1)]
        float gain = .18f;
        void Start()
        {
            var source = GetComponent<AudioSource>();
            source.clip = loop;
            source.loop = true;
            source.spatialBlend = 1;
            source.volume = gain;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            if (AudioManager.Instance)
                source.outputAudioMixerGroup = channel == AudioChannel.Voice ? AudioManager.Instance.voiceGroup : AudioManager.Instance.ambienceGroup;
            if (loop)
                source.Play();
        }
    }
}
