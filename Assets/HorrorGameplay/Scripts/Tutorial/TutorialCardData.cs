// Define texto, controles, imagem ou vídeo de cada cartão.

using System;using UnityEngine;using UnityEngine.Video;
namespace HorrorGame {
 public enum TutorialMediaKind { Model3D, Video, Image }
 public enum TutorialTrigger { InventoryItem, Signal, Flashlight, WhistleLore, SecondEquipment, FirstMessage }
 public enum TutorialMode { Full, TextOnly, Off }
 [Serializable] public sealed class TutorialControl { public string actionId,label;public bool hold; }
 [CreateAssetMenu(menuName="Whispers/Tutorial card")]
 public sealed class TutorialCardData:ScriptableObject {
  public string id,title;[TextArea]public string[] lines;
  public TutorialMediaKind mediaKind;public GameObject model;public VideoClip video;public Texture2D image,fallbackImage;public bool videoAudio;
  public TutorialControl[] controls=Array.Empty<TutorialControl>();public TutorialTrigger trigger;public string triggerId;
  [Range(0,10)]public float delay=.75f;public int priority,helpOrder;public bool blocksGame=true,readInHelp=true;public SoundSet openSound,closeSound,buttonSound;
 }
 [Serializable]public sealed class PendingTutorial { public string id;public float remaining; }
 [Serializable]public sealed class TutorialCardState {
  public int version=1;public bool initialized;public TutorialMode mode;
  public System.Collections.Generic.List<string> seen=new System.Collections.Generic.List<string>(),discovered=new System.Collections.Generic.List<string>();
  public System.Collections.Generic.List<PendingTutorial> pending=new System.Collections.Generic.List<PendingTutorial>();
  public bool Valid { get {if(version!=1||(int)mode<0||(int)mode>2||seen==null||discovered==null||pending==null||seen.Count>128||discovered.Count>128||pending.Count>64)return false;var ids=new System.Collections.Generic.HashSet<string>();foreach(var id in seen)if(!Id(id)||!ids.Add(id))return false;ids.Clear();foreach(var id in discovered)if(!Id(id)||!ids.Add(id))return false;ids.Clear();foreach(var p in pending)if(p==null||!Id(p.id)||!float.IsFinite(p.remaining)||p.remaining<0||p.remaining>60||!ids.Add(p.id))return false;return true;} }
  static bool Id(string value)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=96;
  public bool Discover(TutorialCardData card){if(!card||discovered.Contains(card.id))return false;discovered.Add(card.id);if(!seen.Contains(card.id))pending.Add(new PendingTutorial{id=card.id,remaining=Mathf.Clamp(card.delay,0,10)});return true;}
  public TutorialCardState Copy()=>JsonUtility.FromJson<TutorialCardState>(JsonUtility.ToJson(this));
 }
 public static class TutorialPreferences {
  static string profile,key;static string Key {get{string next="";
#if UNITY_EDITOR
  next=SaveSystem.TestSaveDirectory??"";
#endif
  if(key==null||next!=profile){profile=next;key="Gameplay.TutorialCards.Mode"+(next.Length==0?"":".Test."+next.GetHashCode());}return key;}}
  public static bool HasSetting=>PlayerPrefs.HasKey(Key);
  public static TutorialMode Mode=>(TutorialMode)Mathf.Clamp(PlayerPrefs.GetInt(Key,0),0,2);
  public static void Set(TutorialMode mode){PlayerPrefs.SetInt(Key,Mathf.Clamp((int)mode,0,2));PlayerPrefs.Save();}
 }
}
