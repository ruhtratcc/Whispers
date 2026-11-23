// Traduz interações com os objetos da secretaria e da biblioteca em ações.

using UnityEngine;
namespace HorrorGame
{
    public enum BeforeDemolitionSchoolAction { Cups, Lunchbox, Key, Report, Tape, Phone, Evidence }
    public sealed class BeforeDemolitionSchoolInteraction : MonoBehaviour, IInteractable
    {
        public BeforeDemolitionSchoolFlow flow;
        public BeforeDemolitionSchoolAction action;
        public string label;
        public string Prompt(PlayerInteraction player) => label;
        public bool CanInteract(PlayerInteraction player) => enabled && flow && flow.CanInteract(action, player);
        public void Interact(PlayerInteraction player) { if (CanInteract(player)) flow.Interact(action, player); }
    }
}
