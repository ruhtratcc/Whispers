// Identifica o material do chão para os passos.

using UnityEngine;

namespace HorrorGame
{
    public enum GroundSurface
    {
        Wood,
        Concrete,
        Dirt,
        Grass,
        Metal,
        Tile,
        Carpet
    }

    public sealed class SurfaceType : MonoBehaviour
    {
        [SerializeField]
        public GroundSurface surface = GroundSurface.Concrete;
    }
}
