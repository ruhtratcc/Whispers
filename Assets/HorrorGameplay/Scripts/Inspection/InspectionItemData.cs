// Guarda modelo, textos, imagens, páginas e gravação de um objeto.

using UnityEngine;

namespace HorrorGame
{
    [System.Serializable]
    public sealed class InspectionPage
    {
        public Texture2D image;
        public string label;
        public bool currentDate;
    }

    [System.Serializable]
    public struct RecordingCaptionCue
    {
        [Min(0)] public float start, end;
        [TextArea] public string text;
    }

    [CreateAssetMenu(menuName = "Whispers/Inspection item")]
    public sealed class InspectionItemData : ScriptableObject
    {
        public string itemId, title;
        [TextArea] public string description, frontText, backText, detailText, alternateBackText, recordingText;
        public GameObject model;
        public Texture2D frontTexture, backTexture;
        public InspectionPage[] pages = System.Array.Empty<InspectionPage>();
        public bool recordInNotes = true, canPlay;
        public bool captionOnlyRecording;
        [Min(1)] public float captionRecordingSeconds = 20;
        [Min(0)] public float stressPerSecond = .12f;
        [Min(0)] public float maximumStress = 5;
        public SoundSet pickupSound, whisper, recording;
        public RecordingCaptionCue[] recordingCues = System.Array.Empty<RecordingCaptionCue>();
    }

}
