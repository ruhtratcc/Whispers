// Encaminha o comando de usar o computador.

using UnityEngine;

namespace HorrorGame.Desktop
{
    public sealed class RetroComputerInteraction : MonoBehaviour, IInteractable
    {
        public RetroComputer computer;
        public string Prompt(PlayerInteraction player) => "Sentar e usar o computador";
        public bool CanInteract(PlayerInteraction player) => isActiveAndEnabled && computer && !computer.IsSeated && computer.game && computer.game.CanControl;
        public void Interact(PlayerInteraction player)
        {
            if (CanInteract(player))
                computer.Open();
        }
    }
}
