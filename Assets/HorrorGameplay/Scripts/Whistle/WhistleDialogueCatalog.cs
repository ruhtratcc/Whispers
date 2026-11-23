// Escolhe as falas associadas a cada gatilho do apito.

using System;
using UnityEngine;

namespace HorrorGame
{
    [Serializable] public sealed class WhistleLine
    {
        public string id,trigger,speaker="João",priority="P2",actor="Homem 1";
        [TextArea] public string text,direction;
        public AudioClip voice;
        [Min(.5f)] public float seconds=3;
    }
    [CreateAssetMenu(menuName="Whispers/Whistle dialogue")]
    public sealed class WhistleDialogueCatalog : ScriptableObject
    {
        public WhistleLine[] lines=Array.Empty<WhistleLine>();
        public WhistleLine Find(string trigger,int variation)
        {
            int count=0;for(int i=0;i<lines.Length;i++)if(lines[i]!=null&&lines[i].trigger==trigger)count++;
            if(count==0)return null;int selected=Mathf.Abs(variation%count);
            for(int i=0;i<lines.Length;i++)if(lines[i]!=null&&lines[i].trigger==trigger&&selected--==0)return lines[i];
            return null;
        }
    }
}
