// Localiza efeitos sonoros pelos nomes usados nos scripts.

using System;
using UnityEngine;

namespace HorrorGame
{
    [CreateAssetMenu(menuName="Whispers/Audio/Cue library")]
    public sealed class AudioCueLibrary : ScriptableObject
    {
        [Serializable] public sealed class Cue
        {
            public string id;
            public SoundSet sound;
            public AudioChannel channel=AudioChannel.SFX;
            public bool spatial=true, essential=true, loopPrepared;
        }
        public Cue[] cues=Array.Empty<Cue>();
        public AudioClip officeBed,roadBed,schoolBed,tensionMusic;
        public Cue Find(string id)=>Array.Find(cues,c=>c!=null&&c.id==id);
    }
}
