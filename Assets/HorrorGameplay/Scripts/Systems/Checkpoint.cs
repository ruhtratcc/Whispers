// Marca um lugar ou evento que serve como ponto de retorno.

using UnityEngine;

namespace HorrorGame
{
    [RequireComponent(typeof(Collider))]
    public sealed class Checkpoint : MonoBehaviour
    {
        [SerializeField]
        public Transform respawnPose;
        [SerializeField]
        bool oneShot = true;
        bool saved;
        public void ResetAfterRestore() => saved = false;
        public void Activate()
        {
            if (oneShot && saved)
                return;
            var game = GameSession.Instance;
            if (!game || game.State != GameState.Playing || game.player.IsHidden)
                return;
            if (game.save.Save(respawnPose))
            {
                saved = true;
                game.hud?.Notify("Progresso salvo.");
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<FirstPersonMotor>())
                Activate();
        }

        void OnValidate()
        {
            GetComponent<Collider>().isTrigger = true;
        }
    }
}
