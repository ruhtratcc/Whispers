// Permite coletar equipamentos ligados à história.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    public sealed class StoryEquipment : PersistentObject, IInteractable
    {
        public NarrativeDirector director;
        public string requiredSignal = "prologue.second-call";
        public string itemId = "office_equipment";
        public string title = "Equipamentos de investigação";
        public SoundSet pickupSound;
        [Header("Storage")]
        [Tooltip("Optional moving drawer containing this item. Leave empty for items placed on a surface.")]
        public InteractiveDrawer containingDrawer;
        [Range(0, 1), Tooltip("Required actual opening before the contents can be reached.")]
        public float minimumDrawerOpening = .65f;
        public UnityEvent onCollected = new UnityEvent();
        bool collected;
        [Serializable] sealed class State { public bool collected; }
        public string Prompt(PlayerInteraction player) => "Pegar equipamentos";
        public bool CanInteract(PlayerInteraction player) => !collected && director && director.HasSignal(requiredSignal)
            && (!containingDrawer || (containingDrawer.IsOpen && containingDrawer.OpenFraction >= minimumDrawerOpening));
        public void Interact(PlayerInteraction player)
        {
            if (!player || !player.inventory || !CanInteract(player)) return;
            // Set persisted flag before the inventory event schedules a checkpoint.
            collected = true;
            player.inventory.Add(new InventoryItem { id = itemId, title = title });
            if (AudioManager.Instance) AudioManager.Instance.Play(pickupSound, transform.position);
            onCollected.Invoke();
            player.ShowMessage(title);
            director.ScheduleSave();
            gameObject.SetActive(false);
        }
        public override string CaptureState() => JsonUtility.ToJson(new State { collected = collected });
        public override void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            collected = state != null && state.collected;
            gameObject.SetActive(!collected);
        }
    }
}
