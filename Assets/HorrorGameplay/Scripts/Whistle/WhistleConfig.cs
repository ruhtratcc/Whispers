// Guarda alcance, tempo de espera, sons e limites do apito.

using System;
using UnityEngine;

namespace HorrorGame
{
    public enum WhistleKind { Tap, Hold }
    public readonly struct WhistleContext
    {
        public readonly WhistleSystem system;
        public readonly Vector3 position, direction;
        public readonly WhistleKind kind;
        public readonly float duration;
        public readonly int serial;
        public readonly string scene, room;
        public WhistleContext(WhistleSystem owner,Vector3 origin,Vector3 forward,WhistleKind type,float seconds,int sequence,string sceneName,string roomName)
        {system=owner;position=origin;direction=forward;kind=type;duration=seconds;serial=sequence;scene=sceneName;room=roomName;}
    }
    public interface IWhistleResponsive
    {
        void OnWhistleTap(WhistleContext context);
        void OnWhistleHold(WhistleContext context,float duration);
        void OnWhistleHeard(WhistleContext context);
    }
    [Serializable] public sealed class WhistleRoom
    {
        public string name;
        public Bounds bounds;
        [Range(.05f,1)] public float echoDelay=.35f;
        [Range(.2f,2)] public float responseMaxSeconds=1.1f;
    }
    [CreateAssetMenu(menuName="Whispers/Whistle configuration")]
    public sealed class WhistleConfig : ScriptableObject
    {
        [Min(1)] public float range=24;
        [Min(.5f)] public float highlightHeightTolerance=2.5f;
        public bool SameHighlightFloor(float observerFloorY,float targetY)=>Mathf.Abs(observerFloorY-targetY)<=highlightHeightTolerance;
        [Min(.1f)] public float cooldown=7;
        [Min(.1f)] public float holdMinimum=.55f,holdMaximum=3,mirrorHoldSeconds=2.1f;
        [Range(0,1)] public float tapVolume=.3f,holdVolume=.16f,responseVolume=.22f;
        [Min(0)] public float echoDelay=.35f;
        [Range(.2f,2)] public float responseMaxSeconds=1.1f;
        [Min(.1f)] public float stressPerBlow=1,stressThreshold=3,stressMaximum=6;
        [Min(0)] public float stressCoolingPerSecond=.045f;
        [Min(1)] public float rareCommentCooldown=75;
        [Header("School discovery and visual response")]
        [Min(.6f)] public float pulseExpansionSeconds=1.6f;
        [Min(1)] public float effectDuration=10;
        [Range(-40,0)] public float desaturation=-12;
        [Range(.01f,.08f)] public float outlineWidth=.02f;
        [Range(0,1)] public float highlightStrength=.9f,throughWallStrength=.8f;
        [Range(1,512)] public int maxHighlights=512;
        public LayerMask highlightMask;
        public Color usefulColor=new Color(.46f,.79f,1),storyColor=new Color(1,.67f,.27f),pathColor=new Color(.78f,.94f,.84f);
        public Material highlightMaterial,trailMaterial;
        public AudioClip tapClip,holdClip;
        [Header("Child answers — Marta, Caio, Luan, Bia")]
        public AudioClip[] childAnswerClips=Array.Empty<AudioClip>();
        public AudioClip emptyEchoClip;
        public Vector2 childAnswerDelay=new Vector2(.6f,1.2f);
        [Min(1)] public float childAnswerRange=38;
        [Range(0,1)] public float childAnswerVolume=.38f,emptyEchoVolume=.08f;
        public WhistleDialogueCatalog dialogue;
        public string tapCaption="[apito]",responseCaption="[algo respondeu]";
    }
}
