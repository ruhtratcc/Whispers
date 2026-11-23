// Reúne e localiza os efeitos que o jogo usa.

using System;
using UnityEngine;
using UnityEngine.Audio;

namespace HorrorGame
{
    [Serializable]
    public sealed class SfxCatalogEntry
    {
        public string name;
        public SoundSet sound;
        public AudioMixerGroup mixerGroup;
        public AudioChannel channel = AudioChannel.SFX;
        public bool spatial = true, loop;
        [Range(0, 1)] public float defaultVolume = 1;

        public AudioClip[] Variants => sound && sound.clips != null ? sound.clips : Array.Empty<AudioClip>();
        public Vector2 Pitch => sound ? sound.pitch : Vector2.one;
        public AudioClip Next() => sound ? sound.Next() : null;
    }

    [CreateAssetMenu(menuName = "Whispers/SFX Catalog")]
    public sealed class SfxCatalog : ScriptableObject
    {
        public SfxCatalogEntry[] entries = Array.Empty<SfxCatalogEntry>();

        public SfxCatalogEntry Find(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || entries == null) return null;
            foreach (var entry in entries)
                if (entry != null && string.Equals(entry.name, name, StringComparison.Ordinal)) return entry;
            return null;
        }

        public AudioSource Play(string name, Vector3 position, bool duringPause = false)
        {
            var entry = Find(name);
            var manager = AudioManager.Instance;
            if (entry == null || !entry.sound || !manager) return null;
            var source = manager.Play(entry.sound, position, entry.defaultVolume, entry.channel, entry.spatial, duringPause);
            if (!source) return null;
            if (entry.mixerGroup) source.outputAudioMixerGroup = entry.mixerGroup;
            source.spatialBlend = entry.spatial ? 1 : 0;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.loop = entry.loop;
            return source;
        }
    }
}
