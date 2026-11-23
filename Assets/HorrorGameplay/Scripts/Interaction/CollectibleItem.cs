// Processa a coleta de itens e registra a ação no inventário.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    public enum CollectibleKind
    {
        Key,
        Note,
        Battery
    }

    public sealed class CollectibleItem : PersistentObject, IInteractable
    {
        [SerializeField]
        public CollectibleKind kind;
        [SerializeField]
        public string itemId = "school_key", title = "Chave da escola";
        [SerializeField, TextArea(3, 10)]
        public string noteText;
        [SerializeField]
        public float batterySeconds = 90;
        [SerializeField]
        public SoundSet pickupSound;
        [SerializeField]
        public UnityEvent onCollected = new UnityEvent();
        [Serializable]
        class Data
        {
            public bool collected;
        }

        bool collected;
        public string Prompt(PlayerInteraction player)
        {
            if (kind != CollectibleKind.Battery) return "Pegar " + title;
            var light = player && player.motor ? player.motor.GetComponent<FlashlightController>() : null;
            if (!light || !light.IsAvailable) return "Pilhas — pegue a lanterna primeiro";
            return light.Battery01 >= .999f ? "Pilhas — lanterna já carregada" : "Trocar pilhas e recarregar lanterna";
        }
        public bool CanInteract(PlayerInteraction player) => !collected;
        public void Interact(PlayerInteraction player)
        {
            if (collected)
                return;
            if (kind == CollectibleKind.Battery)
            {
                var light = player && player.motor ? player.motor.GetComponent<FlashlightController>() : null;
                if (!light || !light.IsAvailable)
                {
                    player?.ShowMessage("Pegue a lanterna antes de trocar as pilhas.");
                    return;
                }
                if (light.Battery01 >= .999f)
                {
                    player.ShowMessage("A lanterna já está carregada. Melhor deixar estas pilhas para depois.");
                    return;
                }
                light.Recharge(batterySeconds);
                // Picking up a replacement also switches an exhausted lamp back on.
                light.Restore(light.BatterySeconds, true);
                player.ShowMessage("Pilhas trocadas · Lanterna " + Mathf.CeilToInt(light.Battery01 * 100) + "%");
            }
            else
            {
                player.inventory.Add(new InventoryItem { id = itemId, title = title, text = noteText, note = kind == CollectibleKind.Note });
            }

            collected = true;
            AudioManager.Instance?.Play(pickupSound, transform.position, .4f);
            onCollected.Invoke();
            if (kind == CollectibleKind.Note)
            {
                var game = GameSession.Instance;
                game?.hud?.ShowNote(title, noteText);
                if (game && game.State == GameState.Paused) player.inventory.MarkExamined(itemId);
            }
            gameObject.SetActive(false);
            var session = GameSession.Instance;
            if (kind != CollectibleKind.Battery && session)
            {
                if (session.narrative) session.narrative.ScheduleSave();
                else session.save?.Save();
            }
        }

        public override string CaptureState() => JsonUtility.ToJson(new Data { collected = collected });
        public override void RestoreState(string json)
        {
            var data = JsonUtility.FromJson<Data>(json);
            if (data == null)
                return;
            collected = data.collected;
            gameObject.SetActive(!collected);
        }
    }
}
