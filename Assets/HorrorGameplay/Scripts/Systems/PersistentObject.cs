// É a base de objetos que têm ID e estado salvo.

using UnityEngine;

namespace HorrorGame
{
    public abstract class PersistentObject : MonoBehaviour
    {
        [Tooltip("Unique per scene object, including prefab instances. Use Tools/Whispers/Assign Unique Save IDs after duplicating objects.")]
        [SerializeField]
        public string persistentId;
        public abstract string CaptureState();
        public abstract void RestoreState(string json);
#if UNITY_EDITOR
 protected virtual void OnValidate(){if(string.IsNullOrWhiteSpace(persistentId))persistentId=System.Guid.NewGuid().ToString("N");}
#endif
    }
}
