// Executa sustos pontuais configurados no cenário.

using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    public sealed class Jumpscare : MonoBehaviour
    {
        [Header("Optional camera pose during the scare")]
        [SerializeField] public Transform cameraPose;
        [SerializeField] public SoundSet sound;
        [SerializeField] public float duration = .8f, shake = .055f, tensionIncrease = .35f;
        [SerializeField] public Color flashColor = new Color(.5f, .55f, .58f, .3f);
        [SerializeField] public bool oneShot = true;
        [SerializeField] public UnityEvent onScare = new UnityEvent();
        bool fired;
        public void Fire()
        {
            var game = GameSession.Instance;
            if (!game || game.State != GameState.Playing || oneShot && fired) return;
            fired = true;
            if (game.player) AudioManager.Instance?.Play(sound, game.player.transform.position, .85f, AudioChannel.SFX, false);
            game.atmosphere?.AddTension(tensionIncrease);
            game.atmosphere?.ReactToScare(Mathf.Clamp01(tensionIncrease + .25f), Mathf.Max(.01f, duration) + 4);
            game.hud?.Flash(flashColor, duration);
            game.PlayCutscene(cameraPose, duration, shake);
            onScare.Invoke();
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<FirstPersonMotor>()) Fire();
        }
    }
}
