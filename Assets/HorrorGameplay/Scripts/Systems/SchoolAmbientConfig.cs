// Guarda os sons e intervalos dos eventos ambientes da escola.

using System;
using UnityEngine;
namespace HorrorGame
{
    public enum SchoolLightProfile { Mild, BriefFailure, Dying, Hum }
    [CreateAssetMenu(menuName="Whispers/School atmosphere")]
    public sealed class SchoolAmbientConfig:ScriptableObject
    {
        [Serializable] public sealed class Spot {public string name;public Vector3 position;[Range(.1f,3)]public float weight=1;}
        public Spot[] spots=Array.Empty<Spot>();
        public SoundSet[] rats=Array.Empty<SoundSet>(), distantObjects=Array.Empty<SoundSet>();
        [Min(15)]public float ratMinimum=35,ratMaximum=75;
        [Min(40)]public float objectMinimum=95,objectMaximum=160;
        [Range(0,1)]public float ratVolume=.09f,objectVolume=.12f;
        [Range(.1f,2)]public float lightFrequency=1.25f;
        [Min(1)]public float lightSeconds=3;
        public SchoolLightProfile lightProfile=SchoolLightProfile.Mild;
        public SoundSet lightNoise;
    }
}
