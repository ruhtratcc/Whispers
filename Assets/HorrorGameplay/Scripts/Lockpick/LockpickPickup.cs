// Registra a coleta do grampo.

using System;
using UnityEngine;

namespace HorrorGame
{
    public sealed class LockpickPickup : PersistentObject, IInteractable
    {
        public int count = 1;
        public bool firstPickup;
        public Transform visual;
        public InteractiveDrawer concealingDrawer;
        bool collected;
        [Serializable]
        sealed class Data
        {
            public bool collected;
        }

        public bool CanInteract(PlayerInteraction player) => !collected && (firstPickup || GameSession.Instance && GameSession.Instance.lockpick && GameSession.Instance.lockpick.Acquired) && (!concealingDrawer || concealingDrawer.OpenFraction > .65f) && GameSession.Instance && GameSession.Instance.lockpick && GameSession.Instance.lockpick.CanAcquire;
        public string Prompt(PlayerInteraction player) => firstPickup ? "Pegar o grampo da Helena" : "Pegar grampos de reserva";
        public void Interact(PlayerInteraction player)
        {
            var s = GameSession.Instance ? GameSession.Instance.lockpick : null;
            if (!CanInteract(player) || !s.Acquire(firstPickup ? s.config.initialPins : count, firstPickup))
                return;
            collected = true;
            if (visual)
                visual.gameObject.SetActive(false);
            foreach (var c in GetComponents<Collider>())
                c.enabled = false;
            GameSession.Instance.narrative?.ScheduleSave();
        }

        public override string CaptureState() => JsonUtility.ToJson(new Data { collected = collected });
        public override void RestoreState(string json)
        {
            var d = JsonUtility.FromJson<Data>(json);
            collected = d != null && d.collected;
            if (visual)
                visual.gameObject.SetActive(!collected);
            foreach (var c in GetComponents<Collider>())
                c.enabled = !collected;
        }
    }
}
