// Identifica uma pista do capítulo anterior de memórias.

using UnityEngine;

namespace HorrorGame
{
    public enum SchoolMemoriesClueKind { RecordingOne, RecordingTwo, Photograph }

    /// <summary>Chapter-owned clue, read through the existing interaction raycast and notebook.</summary>
    public sealed class SchoolMemoriesClue : MonoBehaviour, IInteractable
    {
        public SchoolMemoriesFlow flow;
        public SchoolMemoriesClueKind kind;
        public string title;
        [TextArea(3, 12)] public string body;
        public GameObject visual;
        bool visible;

        public string Prompt(PlayerInteraction player) =>
            (GetComponent<InspectableItem>() ? "Examinar " : kind == SchoolMemoriesClueKind.Photograph ? "Investigar " : "Reproduzir ") + title;
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
