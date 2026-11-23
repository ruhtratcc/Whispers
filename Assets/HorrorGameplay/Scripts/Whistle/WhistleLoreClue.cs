// Reconhece uma pista que explica o apito.

using UnityEngine;
namespace HorrorGame
{
    public sealed class WhistleLoreClue:MonoBehaviour
    {
        public WhistleSystem owner;public bool teachesSignal=true;
        public void Read(){if(teachesSignal&&owner)owner.LearnLore();}
    }
}
