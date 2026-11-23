// Liga os objetos dos quatro alunos ao fluxo do capítulo.

using UnityEngine;

namespace HorrorGame
{
    public enum BeforeDemolitionFourStudentsAction { Marta, Caio, Luan, Bia, StairKey }

    public sealed class BeforeDemolitionFourStudentsInteraction : MonoBehaviour, IInteractable
    {
        public BeforeDemolitionFourStudentsFlow flow;
        public BeforeDemolitionFourStudentsAction action;
        public string label;
        public string Prompt(PlayerInteraction player) => label;
        public bool CanInteract(PlayerInteraction player) => enabled && flow && flow.CanInteract(action, player);
        public void Interact(PlayerInteraction player)
        {
            if (CanInteract(player)) flow.Interact(action, player);
        }
    }
}
