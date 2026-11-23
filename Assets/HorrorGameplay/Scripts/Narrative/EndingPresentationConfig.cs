// Reúne textos, imagens, falas e durações do final.

using UnityEngine;

namespace HorrorGame
{
    [CreateAssetMenu(menuName = "Whispers/Apresentação dos finais", fileName = "EndingPresentationConfig")]
    public sealed class EndingPresentationConfig : ScriptableObject
    {
        public string[] badCards = {
            "Amanheceu. A demolição da SEEPE começou às 8h.",
            "Ninguém encontrou as crianças.",
            "O que saiu da escola não era a Helena.",
            "João de Claus nunca mais foi visto."
        };
        [Min(.1f)] public float cardFadeSeconds = .45f;
        [Min(1)] public float badSeconds = 4, photoSeconds = 5, creditsSeconds = 8;
        public StoryLine[] goodPhoneLines = {
            new StoryLine { speaker = "João — telefone, 07:50", text = "Aqui é João de Claus. Parem a demolição. Chamem a polícia. Há quatro crianças dentro da parede.", seconds = 4 },
            new StoryLine { speaker = "Atendente", text = "Vou avisar a equipe. Fique na linha, senhor.", seconds = 2 }
        };
        public StoryLine morningNews = new StoryLine { speaker = "TV — noticiário", text = "...restos de quatro crianças desaparecidas há vinte anos foram encontrados na antiga escola SEEPE. A polícia está no local.", seconds = 8 };
        [Range(12, 18)] public float morningSeconds = 16;
        [Range(1, 4)] public float eyeSeconds = 2;
        public string[] replies = { "Desculpa.", "Obrigado.", "Eu devia ter respondido." };
        public string[] helenaReplies = { "Eu ouvi, seu João.", "Obrigada por responder.", "Pode descansar agora, seu João." };
        [TextArea(4, 12)] public string creditsText = "CRÉDITOS\n\nArthur de Araujo de Oliveira\nEduardo Borges Alfredo\nKahua Rodrigo Viana Cardoso\nAllan Victor Jungklaus da Silva\n\nFinal: Não confio em você";
        public Texture2D schoolImage;
        public AudioClip helenaReturnVoice;
        public AudioClip[] helenaOfferReminders = System.Array.Empty<AudioClip>();
        public AudioClip sirenSound, morningAmbience, emailSound;
        [Range(0, 1)] public float sirenVolume = .22f, morningVolume = .12f;
        [Range(0, 16)] public float policeLightIntensity = 10;
        [Range(.05f, .5f)] public float policeSweepHertz = .25f;
        public Vector3 morningOrigin = new Vector3(1000, 0, 0);
        [Header("Poses locais de reserva — os marcadores EpilogueSeat/Window/TV/PC têm prioridade")]
        public Vector3 seatPosition = new Vector3(0, 1.6f, -1.2f), seatEuler;
        public Vector3 windowPosition = new Vector3(1.2f, 1.6f, -.2f), windowEuler = new Vector3(0, 65, 0);
        public Vector3 tvPosition = new Vector3(.3f, 1.6f, .8f), tvEuler = new Vector3(0, -35, 0);
        public Vector3 pcPosition = new Vector3(-.8f, 1.6f, .8f), pcEuler;
    }
}
