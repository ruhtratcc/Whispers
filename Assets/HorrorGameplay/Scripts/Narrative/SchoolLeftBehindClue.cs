// Identifica as pistas do capítulo antigo sobre o que ficou para trás.

using UnityEngine;

namespace HorrorGame
{
    public enum SchoolLeftBehindClueKind { Report, Photograph, Key }

    /// <summary>Chapter-owned interaction using the player's existing raycast and notebook.</summary>
    public sealed class SchoolLeftBehindClue : MonoBehaviour, IInteractable
    {
        public SchoolLeftBehindFlow flow;
        public SchoolLeftBehindClueKind kind;
        public string title;
        [TextArea(3, 12)] public string body;
        public GameObject visual;
        bool visible;
        public string Prompt(PlayerInteraction player) => (GetComponent<InspectableItem>() ? "Examinar " : kind == SchoolLeftBehindClueKind.Key ? "Pegar " : "Investigar ") + title;
        public bool CanInteract(PlayerInteraction player) => visible && flow && flow.CanReadClue(kind);
        public void Interact(PlayerInteraction player)
        {
            if (InspectionManager.TryInspect(this, player)) return;
            if (CanInteract(player)) flow.ReadClue(this);
        }
        public void SetVisible(bool value)
        {
            visible = value;
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = value;
            if (visual) visual.SetActive(value);
        }
    }
}
