// Guarda textos e etapas consultáveis dos objetivos.

using System;
using UnityEngine;

namespace HorrorGame
{
    [Serializable] public sealed class ObjectiveSubitem
    {
        public string label, requiredItemId, requiredFlag;
        public bool IsComplete(Inventory inventory, NarrativeDirector director)
        {
            bool item = !string.IsNullOrEmpty(requiredItemId), flag = !string.IsNullOrEmpty(requiredFlag);
            return (item || flag) && (!item || inventory && inventory.Contains(requiredItemId)) && (!flag || ObjectiveEntry.HasFlag(director,requiredFlag));
        }
    }
    [Serializable] public sealed class ObjectiveEntry
    {
        public string id, title, requiredFlag, completionFlag, nextObjectiveId;
        public string[] requiredNextSignals = Array.Empty<string>();
        public ObjectiveSubitem[] subitems = Array.Empty<ObjectiveSubitem>();
        public bool completeWhenAllSubitems, hasTarget;
        public Vector3 targetPosition;
        public static bool HasFlag(NarrativeDirector director, string flag) => director && !string.IsNullOrEmpty(flag)
            && (director.HasSignal(flag) || director.HasSequence(flag));
        public bool Matches(NarrativeDirector director) => director && (string.IsNullOrEmpty(requiredFlag) || HasFlag(director,requiredFlag))
            && (requiredNextSignals == null || requiredNextSignals.Length == 0 || Array.IndexOf(requiredNextSignals,director.NextSignal) >= 0);
        public bool IsComplete(Inventory inventory, NarrativeDirector director)
        {
            if (!string.IsNullOrEmpty(completionFlag) && HasFlag(director,completionFlag)) return true;
            if (!completeWhenAllSubitems || subitems == null || subitems.Length == 0) return false;
            foreach (var item in subitems) if (item == null || !item.IsComplete(inventory,director)) return false;
            return true;
        }
    }
    [CreateAssetMenu(menuName="Whispers/Objectives/Chapter")]
    public sealed class ChapterObjectives : ScriptableObject
    {
        public string chapterId;
        public ObjectiveEntry[] entries = Array.Empty<ObjectiveEntry>();
    }
    public static class ObjectivePreferences
    {
        static string profile, suffix;
        static string Suffix
        {
            get
            {
                string next = "";
#if UNITY_EDITOR
                next = SaveSystem.TestSaveDirectory ?? "";
#endif
                if (suffix == null || profile != next) { profile=next; suffix=next.Length==0?"":".Test."+next.GetHashCode(); }
                return suffix;
            }
        }
        public static bool ShowObjectives
        {
            get => PlayerPrefs.GetInt("Gameplay.Objectives.Show"+Suffix,1)==1;
            set { PlayerPrefs.SetInt("Gameplay.Objectives.Show"+Suffix,value?1:0); PlayerPrefs.Save(); }
        }
        public static float TextScale
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat("Gameplay.Objectives.TextScale"+Suffix,1),.8f,1.5f);
            set { PlayerPrefs.SetFloat("Gameplay.Objectives.TextScale"+Suffix,Mathf.Clamp(value,.8f,1.5f)); PlayerPrefs.Save(); }
        }
    }
}
