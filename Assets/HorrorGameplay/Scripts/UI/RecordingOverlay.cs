// Mostra os controles e o progresso da reprodução de uma fita.

using TMPro;
using UnityEngine;

namespace HorrorGame
{
    public sealed class RecordingOverlay : MonoBehaviour
    {
        [SerializeField]
        public InputReader input;
        [SerializeField]
        public GameObject visuals;
        [SerializeField]
        public TMP_Text timer;
        [SerializeField]
        public bool defaultEnabled = false;
        public bool IsEnabled { get; private set; }

        // Compatibility component for old prefabs. Recording UI has been retired.
        void Awake() => SetEnabled(false);
        public void SetEnabled(bool value)
        {
            IsEnabled = false;
            if (visuals)
                visuals.SetActive(false);
        }
    }
}
