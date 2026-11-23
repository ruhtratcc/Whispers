// Registra pistas da campanha simplificada.

using UnityEngine;

namespace HorrorGame
{
    public sealed class SimplifiedSchoolClue : MonoBehaviour, IInteractable
    {
        public SchoolSimplifiedFlow flow;
        public string chapterId, itemId, title;
        [TextArea] public string body;
        public GameObject visual;
        public bool needsFallenBook, needsRecorder;
        public string Prompt(PlayerInteraction player) => "Examinar " + title;
        public bool CanInteract(PlayerInteraction player) => flow && flow.CanInvestigate(this);
        public void Interact(PlayerInteraction player)
        {
            if (InspectionManager.TryInspect(this, player)) return;
            if (!CanInteract(player)) return;
            flow.director.inventory.Add(new InventoryItem { id = itemId, title = title, text = body, note = true });
            SetVisible(false);
            flow.director.ScheduleSave();
        }
        public void SetVisible(bool value)
        {
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = value;
            if (visual) visual.SetActive(value);
        }
    }
}
