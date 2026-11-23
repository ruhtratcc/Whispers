// Localiza os dados de inspeção pelo ID do item.

using UnityEngine;

namespace HorrorGame
{
    [CreateAssetMenu(menuName = "Whispers/School inspection catalog")]
    public sealed class SchoolInspectionCatalog : ScriptableObject
    {
        public InspectionItemData[] items;
        public InspectionItemData Find(string id)
        {
            if (items != null) foreach (var item in items) if (item && item.itemId == id) return item;
            return null;
        }
    }
}
