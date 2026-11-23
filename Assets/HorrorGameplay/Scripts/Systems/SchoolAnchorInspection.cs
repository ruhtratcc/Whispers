// Integra itens âncora ao fluxo de inspeção.

using UnityEngine;

namespace HorrorGame
{
    /// <summary>A familiar object can be inspected repeatedly without advancing the plot.</summary>
    public sealed class SchoolAnchorInspection : MonoBehaviour, IInteractable
    {
        public string title, caption;
        public string Prompt(PlayerInteraction player) => "Observar " + title;
        public bool CanInteract(PlayerInteraction player) => GameSession.Instance && GameSession.Instance.CanControl;
        public void Interact(PlayerInteraction player)
        {
            if (CanInteract(player)) GameSession.Instance.hud?.ShowSubtitle(caption, 4);
        }
    }
}
