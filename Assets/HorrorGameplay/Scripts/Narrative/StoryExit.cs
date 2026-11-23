// Processa pontos de saída vinculados à narrativa.

using UnityEngine;

namespace HorrorGame
{
    public sealed class StoryExit : MonoBehaviour, IInteractable
    {
        public NarrativeDirector director;
        public string signalId = "prologue.exit", requiredSignal = "prologue.equipment";
        public string prompt = "Sair do escritório";
        public string blockedMessage = "Ainda preciso concluir a investigação no escritório.";
        public string Prompt(PlayerInteraction player) => prompt;
        public bool CanInteract(PlayerInteraction player) => director && director.IsInitialized && !director.HasSignal(signalId);
        public void Interact(PlayerInteraction player)
        {
            if (!CanInteract(player)) return;
            if (!director.HasSignal(requiredSignal)) { player.ShowMessage(blockedMessage); return; }
            director.Signal(signalId);
        }
    }
}
