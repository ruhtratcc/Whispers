// Define os objetos que podem ser levantados.

using UnityEngine;

namespace HorrorGame
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PhysicsPickup : MonoBehaviour, IInteractable
    {
        [SerializeField]
        public string displayName = "objeto";
        Rigidbody body;
        void Awake() => body = GetComponent<Rigidbody>();
        public string Prompt(PlayerInteraction player) => "Segurar " + displayName;
        public bool CanInteract(PlayerInteraction player)
        {
            if (!body)
                body = GetComponent<Rigidbody>();
            return player.grabber && player.grabber.CanHold(body);
        }

        public void Interact(PlayerInteraction player) => player.grabber.PickUp(body);
    }
}
