// Traduz interações com o interior do carro e o portão em ações.

using UnityEngine;

namespace HorrorGame
{
    public enum BeforeDemolitionRoadAction { Phone, RadioPower, RadioStation, Flashlight, Exit, Gate, RadioVolumeUp, RadioVolumeDown }
    public sealed class BeforeDemolitionRoadInteraction : MonoBehaviour, IInteractable
    {
        public BeforeDemolitionRoadFlow flow;
        public BeforeDemolitionRoadAction action;
        public string Prompt(PlayerInteraction player)
        {
            switch (action)
            {
                case BeforeDemolitionRoadAction.Phone: return "Pegar o celular";
                case BeforeDemolitionRoadAction.RadioPower: return "Desligar rádio";
                case BeforeDemolitionRoadAction.RadioStation: return "Próxima faixa";
                case BeforeDemolitionRoadAction.RadioVolumeUp: return "Aumentar volume";
                case BeforeDemolitionRoadAction.RadioVolumeDown: return "Diminuir volume";
                case BeforeDemolitionRoadAction.Flashlight: return "Pegar a lanterna";
                case BeforeDemolitionRoadAction.Exit: return "Sair do carro pela porta";
                case BeforeDemolitionRoadAction.Gate: return "Entrar na escola";
                default: return "Investigar";
            }
        }
        public bool CanInteract(PlayerInteraction player) => isActiveAndEnabled && player && flow && flow.CanInteract(action);
        public void Interact(PlayerInteraction player) { if (CanInteract(player)) flow.Interact(action, player); }
    }
}
