// Guarda os itens adquiridos e registra quais foram examinados.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame
{
    [Serializable]
    public sealed class InventoryItem
    {
        public string id, title, text;
        public bool note;
        public string inspectionId, chapterId;
        public bool inspectionSeen;
        public int inspectionCount;
    }

    public sealed class Inventory : MonoBehaviour
    {
        [SerializeField]
        public List<InventoryItem> items = new List<InventoryItem>();
        public event Action Changed, Restored;
        public event Action<InventoryItem> ItemAdded;
        public InventoryItem Find(string id) => items.Find(item => item != null && item.id == id);
        public bool Contains(string id) => Find(id) != null;
        public bool Add(InventoryItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id) || Contains(item.id))
                return false;
            var game = GameSession.Instance;
            if (string.IsNullOrEmpty(item.chapterId) && game && game.interaction && game.interaction.inventory == this)
                item.chapterId = game.narrative?.Progress.chapterId;
            items.Add(item);
            ItemAdded?.Invoke(item);
            Changed?.Invoke();
            return true;
        }

        public void Restore(List<InventoryItem> saved)
        {
            items = CopyItems(saved);
            SecretaryEvidenceText.Refresh(items);
            Restored?.Invoke();
            Changed?.Invoke();
        }
        public static List<InventoryItem> CopyItems(List<InventoryItem> source)
        {
            var copy = new List<InventoryItem>(source?.Count ?? 0);
            if (source != null)
                foreach (var item in source)
                    if (item != null) copy.Add(new InventoryItem
                    {
                        id = item.id, title = item.title, text = item.text, note = item.note,
                        inspectionId = item.inspectionId, inspectionSeen = item.inspectionSeen,
                        inspectionCount = item.inspectionCount, chapterId = item.chapterId
                    });
            return copy;
        }

        public bool MarkExamined(string id)
        {
            var item = Find(id);
            if (item == null || item.inspectionSeen) return false;
            item.inspectionSeen = true;
            Changed?.Invoke();
            return true;
        }

        public bool Remove(string id)
        {
            int index=items.FindIndex(item=>item.id==id);if(index<0)return false;
            items.RemoveAt(index);Changed?.Invoke();return true;
        }
    }
}
