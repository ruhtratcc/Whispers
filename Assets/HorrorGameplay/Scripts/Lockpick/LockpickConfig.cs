// Guarda a dificuldade e os parâmetros de arrombamento.

using System;
using UnityEngine;
namespace HorrorGame{
 public enum LockKind{Key,OptionalPickable,Unavailable}
 public enum LockDifficulty{Easy=1,Medium=2,Hard=3}
 [CreateAssetMenu(menuName="Whispers/Lockpick configuration")]
 public sealed class LockpickConfig:ScriptableObject{
  [Range(1,6)]public int initialPins=2,maximumPins=6;
  public Vector3 tolerances=new Vector3(18,12,9),autoSeconds=new Vector3(9,18,27);
  [Min(.1f)]public float turnSeconds=5.5f,stressPerSecond=.22f,stressRecovery=.16f;
  [Min(1)]public int loosenAfter=3;
  [Range(0,1)]public float loosenBonus=.15f,assistedBonus=.25f;
  public float hintSeconds=1.8f,whisperSeconds=16,commentAfter=35,helenaCooldown=100;
  [Range(0,2)]public float vibrationPixels=.7f;
  [Range(0,1)]public float scrapeGain=.18f,clickGain=.2f,breakGain=.2f,voiceGain=.16f;
  public SoundSet scrape,pinClick,blocked,snap,unlock;
  public Sprite cylinderSprite;
  public LockpickDialogue dialogue;
  public float Tolerance(LockDifficulty difficulty)=>difficulty==LockDifficulty.Easy?tolerances.x:difficulty==LockDifficulty.Medium?tolerances.y:tolerances.z;
  public float AutoSeconds(LockDifficulty difficulty)=>difficulty==LockDifficulty.Easy?autoSeconds.x:difficulty==LockDifficulty.Medium?autoSeconds.y:autoSeconds.z;
 }
 [Serializable]public sealed class LockpickState{
  public int version=1,count;public bool initialized,acquired,tutorialShown,trainingWhispered,careerSpoken;
  public bool Valid=>version==1&&count>=0&&count<=100&&(!initialized||acquired||count==0);
 }
 [Serializable]public sealed class LockpickLine{
  public string id,trigger,character,actor,text,priority,direction;public AudioClip voice;
 }
 public enum LockpickResult{None,PinSet,Broken,Complete}
 public sealed class LockpickAttempt{
  public readonly int PinCount;public int CompletedPins{get;private set;}
  public float Progress{get;private set;}public float Stress{get;private set;}public float Elapsed{get;private set;}
  public float Proximity{get;private set;}public float Angle{get;private set;}public float TargetAngle=>angles[Mathf.Min(CompletedPins,PinCount-1)];
  readonly float[] angles;readonly LockpickConfig config;readonly LockDifficulty difficulty;readonly float tolerance;
  float automaticAge;bool finished;
  public LockpickAttempt(LockpickConfig config,LockDifficulty difficulty,string id,int completed=0,float toleranceOverride=0){
   this.config=config;this.difficulty=difficulty;PinCount=(int)difficulty;CompletedPins=Mathf.Clamp(completed,0,PinCount-1);tolerance=toleranceOverride>0?toleranceOverride:config.Tolerance(difficulty);
   uint seed=2166136261;foreach(char c in id??"")seed=(seed^c)*16777619;angles=new float[PinCount];
   for(int i=0;i<PinCount;i++){seed=1664525*seed+1013904223;angles[i]=25+(seed%13000)/100f;if(i>0&&Mathf.Abs(angles[i]-angles[i-1])<24)angles[i]=25+(angles[i]+53)%130;}
  }
  public float EffectiveTolerance(bool assisted,int breaks)=>tolerance*(assisted?1+config.assistedBonus:1)*(breaks>=config.loosenAfter?1+config.loosenBonus:1);
  public LockpickResult Step(float seconds,float angle,bool tension,bool assisted,bool automatic,int breaks){
   if(finished||!float.IsFinite(seconds)||seconds<=0)return LockpickResult.None;
   seconds=Mathf.Min(seconds,.1f);Elapsed+=seconds;Angle=Mathf.Clamp(float.IsFinite(angle)?angle:90,0,180);
   // Mede a distância entre o ângulo do mouse e o ângulo correto do pino.
   float difference=Mathf.Abs(Angle-TargetAngle);Proximity=1-Mathf.Clamp01(difference/70);
   // Soltar o botão reduz a pressão e faz o pino atual perder avanço.
   if(!tension){Stress=Mathf.Max(0,Stress-config.stressRecovery*seconds);Progress=Mathf.Max(0,Progress-seconds*.7f);automaticAge=0;return LockpickResult.None;}
   // No modo automático, basta manter a pressão pelo tempo pedido.
   if(automatic){automaticAge+=seconds;Progress=Mathf.Clamp01(automaticAge/config.AutoSeconds(difficulty));if(Progress<1)return LockpickResult.None;finished=true;CompletedPins=PinCount;return LockpickResult.Complete;}
   // Dentro da faixa certa, o pino avança até encaixar.
   if(difference<=EffectiveTolerance(assisted,breaks)){
    Progress=Mathf.Min(1,Progress+seconds/config.turnSeconds);Stress=Mathf.Max(0,Stress-seconds*.08f);
    if(Progress<1)return LockpickResult.None;CompletedPins++;Progress=Stress=0;
    if(CompletedPins<PinCount)return LockpickResult.PinSet;finished=true;return LockpickResult.Complete;
   }
   // Fora da faixa certa, aumenta a pressão. Se chegar ao limite, o grampo quebra.
   Progress=Mathf.MoveTowards(Progress,Proximity*.18f,seconds*.9f);Stress+=seconds*config.stressPerSecond*Mathf.Lerp(1.2f,.6f,Proximity)*(assisted?.45f:1);
   if(Stress<1)return LockpickResult.None;Progress=Stress=automaticAge=0;return LockpickResult.Broken;
  }
 }
 public static class LockpickPreferences{
  static string profile,assistKey,autoKey;
  static string Key(string suffix){string next="";
#if UNITY_EDITOR
   next=SaveSystem.TestSaveDirectory??"";
#endif
   if(assistKey==null||profile!=next){profile=next;string prefix="Gameplay.Lockpick."+(next.Length==0?"":"Test."+next.GetHashCode());assistKey=prefix+"Assist";autoKey=prefix+"Auto";}
   return suffix=="Assist"?assistKey:autoKey;
  }
  public static bool Assisted=>PlayerPrefs.GetInt(Key("Assist"),0)==1;
  public static bool Automatic=>PlayerPrefs.GetInt(Key("Auto"),0)==1;
  public static void Set(bool assisted,bool automatic){PlayerPrefs.SetInt(Key("Assist"),assisted?1:0);PlayerPrefs.SetInt(Key("Auto"),automatic?1:0);PlayerPrefs.Save();}
 }
}
