// Define as mensagens e áreas usadas para avançar a história.

using System;
using UnityEngine;

namespace HorrorGame
{
    [CreateAssetMenu(menuName = "Whispers/Progresso por mensagens", fileName = "PhoneProgressionConfig")]
    public sealed class PhoneProgressionConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Gate
        {
            public string chapterId;
            public string signal;
            public string messageId;
            [TextArea] public string fallbackObjective;
        }

        [Serializable]
        public sealed class Zone
        {
            public Bounds bounds;
            public PhoneTrigger trigger;
            [Tooltip("Vazio: qualquer capítulo da escola.")]
            public string chapterId;
        }

        public Gate[] gates = Array.Empty<Gate>();
        public Zone[] zones = Array.Empty<Zone>();
        [Min(.1f)] public float pollSeconds = .25f;
        [Min(10)] public float reminderSeconds = 45;
        [Min(20)] public float strongReminderSeconds = 90;
        [Min(15), Tooltip("Só libera o objetivo básico se a definição ou a entrega da mensagem estiver ausente. Mensagens recebidas e não lidas continuam obrigatórias.")]
        public float missingDeliveryFallbackSeconds = 90;
    }
}
