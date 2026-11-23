// Monta as caixas de erro com aparência do sistema.

using UnityEngine;

namespace HorrorGame.Desktop
{
    [CreateAssetMenu(menuName = "Whispers/PC/Error dialog")]
    public sealed class DesktopError : ScriptableObject
    {
        public string id, title;
        public Sprite icon;
        public string[] buttons =
        {
            "OK"
        };
        [TextArea(2, 5)]
        public string message;
    }
}
