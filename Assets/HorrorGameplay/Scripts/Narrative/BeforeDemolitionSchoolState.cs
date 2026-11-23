// Guarda o progresso e os estados usados na primeira visita à escola.

using System;
using UnityEngine;
namespace HorrorGame
{
    [Serializable]
    public sealed class BeforeDemolitionSchoolState
    {
        public int version = 1;
        public EmailInboxState emails;
        public bool phoneAnswered;
        public bool ValidFields => (version == 1 || version == 2) && emails != null;
        public static BeforeDemolitionSchoolState Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Checkpoint escolar vazio.", nameof(json));
            var state = new BeforeDemolitionSchoolState { version = 0 };
            JsonUtility.FromJsonOverwrite(json, state); return state;
        }
    }
}
