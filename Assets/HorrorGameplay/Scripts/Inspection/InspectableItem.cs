// Permite que um objeto da cena entre no sistema de inspeção.

using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    [DisallowMultipleComponent]
    public sealed class InspectableItem : MonoBehaviour, IInteractable
    {
        public InspectionItemData item;
        public MonoBehaviour originalInteraction;
        public GameObject visual;
        public bool collectible = true;
        public UnityEvent onInspectionFinished = new UnityEvent();

        bool FloorEvidence => originalInteraction is BeforeDemolitionSchoolInteraction school && school.action == BeforeDemolitionSchoolAction.Cups;
        public string Prompt(PlayerInteraction player) => FloorEvidence ? "Analisar pegadas no chão" : "Examinar " + (item ? item.title : name);
        public bool CanInteract(PlayerInteraction player)
        {
            if (FloorEvidence) return originalInteraction is IInteractable evidence && evidence.CanInteract(player);
            if (!item || !item.model || !InspectionManager.Instance || InspectionManager.Instance.IsOpen) return false;
            if (originalInteraction is IInteractable original) return original.CanInteract(player);
            return GameSession.Instance && GameSession.Instance.CanControl && player
                && (!collectible || !player.inventory.Contains(item.itemId));
        }
        public void Interact(PlayerInteraction player)
        {
            if (!CanInteract(player)) return;
            if (FloorEvidence)
            {
                Complete(player);
                if (item && item.recordInNotes && !player.inventory.Contains(item.itemId))
                    player.inventory.Add(new InventoryItem { id=item.itemId,title=item.title,text=item.backText,note=true });
                GameSession.Instance?.narrative?.ScheduleSave();
            }
            else InspectionManager.Instance.Begin(this, player);
        }
        public void Complete(PlayerInteraction player)
        {
            if (originalInteraction is IInteractable original) original.Interact(player);
            else
            {
                if (item.recordInNotes || collectible)
                    player.inventory.Add(new InventoryItem { id = item.itemId, title = item.title,
                        text = item.backText, note = true });
                if (collectible) gameObject.SetActive(false);
            }
            onInspectionFinished.Invoke();
        }
    }
}
