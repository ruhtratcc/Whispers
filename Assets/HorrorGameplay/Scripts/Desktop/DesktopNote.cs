// Organiza as anotações do computador.

using UnityEngine;

namespace HorrorGame.Desktop
{
    [CreateAssetMenu(menuName = "Whispers/PC/Note visit")]
    public sealed class DesktopNote : ScriptableObject
    {
        public int visit = 1;
        public EmailParagraph[] paragraphs;
    }
}
