// Agrupa variações de um som e evita repetir o último clipe quando há alternativas.

using UnityEngine;

namespace HorrorGame
{
    [CreateAssetMenu(menuName = "Whispers/Sound Set")]
    public sealed class SoundSet : ScriptableObject
    {
        public bool overrideChannel;
        public AudioChannel channel=AudioChannel.SFX;
        public string caption;
        [SerializeField]
        public AudioClip[] clips;
        [SerializeField]
        public Vector2 pitch = new Vector2(.94f, 1.06f);
        [SerializeField]
        public Vector2 volume = new Vector2(.85f, 1);
        [Header("3D audibility")]
        [Tooltip("Distance in metres before attenuation starts. Ignored for UI/2D sounds.")]
        [SerializeField, Min(.05f)] public float minimumDistance = .7f;
        [Tooltip("Distance in metres where the sound fades completely. Ignored for UI/2D sounds.")]
        [SerializeField, Min(.1f)] public float maximumDistance = 12;
        AudioClip lastClip;
        void OnEnable() => lastClip = null;
        public AudioClip Next()
        {
            if (clips == null || clips.Length == 0)
                return null;
            int eligible = 0;
            foreach (var clip in clips)
                if (clip && clip != lastClip)
                    eligible++;
            if (eligible == 0)
            {
                foreach (var clip in clips)
                    if (clip)
                    {
                        lastClip = clip;
                        return clip;
                    }

                return null;
            }

            int selected = Random.Range(0, eligible);
            foreach (var clip in clips)
                if (clip && clip != lastClip && selected-- == 0)
                {
                    lastClip = clip;
                    return clip;
                }

            return null;
        }

        public float Pitch => Random.Range(Mathf.Min(pitch.x, pitch.y), Mathf.Max(pitch.x, pitch.y));
        public float Volume => Random.Range(Mathf.Min(volume.x, volume.y), Mathf.Max(volume.x, volume.y));
    }
}
