// Escolhe quando e onde reproduzir sons inesperados.

using UnityEngine;

namespace HorrorGame
{
    public sealed class RandomScareAudio : MonoBehaviour
    {
        [SerializeField]
        public SoundSet sounds;
        [SerializeField]
        public AudioChannel channel = AudioChannel.Ambience;
        [SerializeField]
        Vector2 interval = new Vector2(22, 55);
        [SerializeField]
        float gain = .23f, tensionIncrease = .04f;
        [SerializeField]
        public bool generateNoise = false;
        [SerializeField]
        float noiseRadius = 8;
        float next;
        void OnEnable() => Schedule();
        void Schedule() => next = Time.time + Random.Range(Mathf.Max(1, interval.x), Mathf.Max(interval.x, interval.y));
        void Update()
        {
            if (Time.time < next)
                return;
            if (GameSession.Instance && GameSession.Instance.State != GameState.Playing)
                return;
            Schedule();
            AudioManager.Instance?.Play(sounds, transform.position, gain, channel);
            GameSession.Instance?.atmosphere?.AddTension(tensionIncrease);
            if (generateNoise)
                NoiseSystem.Emit(transform.position, noiseRadius, gameObject, NoiseKind.Scare);
        }
    }
}
