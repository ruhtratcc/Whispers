// Guarda clipes, volumes e atrasos das camadas da voz.

using System;
using UnityEngine;
using UnityEngine.Audio;

namespace HorrorGame
{
    [Serializable]
    public sealed class DemonLine
    {
        public string id;
        [Range(1,3)] public int stage=1;
        [TextArea] public string text;
        public AudioClip mask,low;
        public bool mirror,lowOnly,mirrorTransition;
        [Range(0,1)] public float mirrorSlip;
        [Min(.5f)] public float captionSeconds=3;
    }

    [CreateAssetMenu(menuName="Whispers/Demon Voice")]
    public sealed class DemonVoiceConfig:ScriptableObject
    {
        public DemonLine[] lines=Array.Empty<DemonLine>();
        public AudioMixerGroup maskGroup,lowGroup;
        public string pitchParameter="DemonLowPitch";
        [Range(.5f,.6f)] public float fallbackPitch=.56f;
        [Range(.65f,1)] public float slowPitch=.9f;
        public Vector2 layerDelay=new Vector2(.08f,.15f);
        [Range(0,1)] public float maskGain=.64f,lowGain=.44f,voiceCeiling=.8f,choirGain=.045f;
        [Min(1)] public float voiceDistance=9,maxDistance=24;
        public AudioClip choirHum,choirHumReversed;
        public DemonLine Find(string id)=>Array.Find(lines,line=>line!=null&&line.id==id);
        public static int Stage(float slip)=>slip<.25f?1:slip<.75f?2:3;
    }
}
