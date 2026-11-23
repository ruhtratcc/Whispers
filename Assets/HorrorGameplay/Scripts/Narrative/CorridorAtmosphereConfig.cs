// Guarda medidas, limites e parâmetros visuais do corredor.

using UnityEngine;

namespace HorrorGame
{
    [CreateAssetMenu(menuName="Whispers/Corridor Atmosphere")]
    public sealed class CorridorAtmosphereConfig:ScriptableObject
    {
        public Color fogColor=Color.black;
        [Range(.1f,.8f)]public float fogDensity=.34f;
        [Range(10,14)]public float visibility=12;
        [Min(2)]public float moduleLength=13.5f;
        [Min(1)]public float corridorHalfWidth=1.5f;
        [Range(4,12)]public float flashlightRange=9;
        [Range(0,2)]public float lightIntensity=.5f;
        [Range(.1f,2)]public float lightFadeSeconds=.45f;
        [Range(3,12)]public float lightRange=7;
        [Range(0,2)]public int litNeighbours=1;
        public Color lightColor=new Color(.68f,.73f,.66f);
        [Min(1)]public int maxTallyGroups=12;
        public static float FogTransmission(float distance,float density)=>Mathf.Exp(-Mathf.Abs(distance)*density);
    }
}
