// Define uma página que o navegador pode abrir.

using UnityEngine;

namespace HorrorGame.Desktop
{
    [CreateAssetMenu(menuName = "Whispers/PC/Local page")]
    public sealed class DesktopPage : ScriptableObject
    {
        public string address, title;
        public EmailParagraph[] paragraphs;
        public Texture2D image;
    }
}
