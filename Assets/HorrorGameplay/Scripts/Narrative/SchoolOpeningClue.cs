// Identifica um objeto da abertura antiga da escola.

using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    public enum SchoolOpeningClueKind { Documents, Photograph, Drawing }

    /// <summary>An actual raycast interaction, gated by saved opening progress.</summary>
    [DisallowMultipleComponent]
    public sealed class SchoolOpeningClue : MonoBehaviour, IInteractable
    {
        public SchoolOpeningFlow flow;
        public SchoolOpeningClueKind kind;
        public string title = "Documento antigo";
        [TextArea(3, 12)] public string body;
        [Tooltip("Optional child visuals. Leave empty to hide the whole clue object.")]
        public GameObject visual;
        public UnityEvent onRead = new UnityEvent();
        bool visible = true;

        public string Prompt(PlayerInteraction player) => (GetComponent<InspectableItem>() ? "Examinar " : "Investigar ") + title;
        public bool CanInteract(PlayerInteraction player) => visible && flow && flow.CanReadClue(kind);
        public void Interact(PlayerInteraction player)
        {
            if (InspectionManager.TryInspect(this, player)) return;
            if (CanInteract(player) && flow.ReadClue(this)) onRead.Invoke();
        }
        public void SetVisible(bool value)
        {
            visible = value;
            // Disabled colliders cannot mask another interactable behind a collected clue.
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = value;
            if (visual) visual.SetActive(value);
            else gameObject.SetActive(value);
        }
    }
}
