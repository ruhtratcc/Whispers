// Define os itens equipáveis e as preferências gravadas desse sistema.

using System;
using UnityEngine;

namespace HorrorGame
{
    public enum EquipmentKind { Flashlight, Whistle, Other }
    public enum EquipmentHudMode { Always, Automatic, Hidden }

    [CreateAssetMenu(menuName="Whispers/Equipment item")]
    public sealed class EquipmentDefinition : ScriptableObject
    {
        public string id, inventoryItemId, displayName;
        [Range(1,9)] public int slot=1;
        public EquipmentKind kind;
        public Sprite icon;
        public SoundSet drawSound, stowSound, moveSound;
        public GameObject viewmodelPrefab;
    }

    [Serializable]
    public sealed class EquipmentState
    {
        public int version=1;
        public string equippedId="", previousId="";
        // JsonUtility materializes missing nested classes; only an explicit marker is a new equipment save.
        public bool initialized,flashlightOn, keepFlashlight, reverseWheel;
        public EquipmentHudMode hudMode=EquipmentHudMode.Automatic;
        public bool Valid => version==1 && (equippedId==null||equippedId.Length<=128)
            && (previousId==null||previousId.Length<=128) && (int)hudMode>=0 && (int)hudMode<=2;
    }

    public static class EquipmentPreferences
    {
        public static event Action Changed;
        static string profile, modeKey, lightKey, wheelKey;
        static void Keys()
        {
            string next="";
#if UNITY_EDITOR
            next=SaveSystem.TestSaveDirectory??"";
#endif
            if(modeKey!=null&&profile==next)return;
            profile=next;
            string prefix="Gameplay.Equipment."+(next.Length==0?"":"Test."+next.GetHashCode()+".");
            modeKey=prefix+"Hud";lightKey=prefix+"KeepLight";wheelKey=prefix+"ReverseWheel";
        }
        public static EquipmentHudMode Mode {get{Keys();return (EquipmentHudMode)Mathf.Clamp(PlayerPrefs.GetInt(modeKey,1),0,2);}}
        public static bool KeepLight {get{Keys();return PlayerPrefs.GetInt(lightKey,0)==1;}}
        public static bool ReverseWheel {get{Keys();return PlayerPrefs.GetInt(wheelKey,0)==1;}}
        public static void Set(EquipmentHudMode mode,bool keep,bool reverse)
        {
            Keys();PlayerPrefs.SetInt(modeKey,Mathf.Clamp((int)mode,0,2));PlayerPrefs.SetInt(lightKey,keep?1:0);PlayerPrefs.SetInt(wheelKey,reverse?1:0);
            PlayerPrefs.Save();Changed?.Invoke();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){profile=modeKey=lightKey=wheelKey=null;Changed=null;}
    }
}
