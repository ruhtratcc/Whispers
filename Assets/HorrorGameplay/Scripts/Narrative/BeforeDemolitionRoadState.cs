// Guarda a etapa do rádio e as ações já concluídas na estrada.

using System;
using UnityEngine;

namespace HorrorGame
{
    public enum RoadRadioPhase { News, FirstAnnouncement, SecondAnnouncement, ThirdAnnouncement, Silence, Song, Parked }

    [Serializable]
    public sealed class BeforeDemolitionRoadState
    {
        public int version = 1;
        public EmailInboxState emails;
        public RoadRadioPhase phase;
        public float phaseAge;
        public float phaseDuration;
        public bool callOffered, callAnswered, phoneMoved;
        // Additive field: old version1/2 checkpoints retain their phase and default to not switched off.
        public bool radioSwitchedOff;
        public bool skippedMusic;
        public float skipStartProgress;
        public bool hauntedPowerTried;
        public float radioVolume=.75f;
        public bool radioVolumeInitialized;

        public bool ValidFields => (version == 1 || version == 2) && (int)phase >= 0 && phase <= RoadRadioPhase.Parked
            && float.IsFinite(phaseAge) && phaseAge >= 0 && phaseAge <= 600
            && float.IsFinite(phaseDuration) && (phase == RoadRadioPhase.Parked ? phaseDuration == 0 && phaseAge == 0
                : phaseDuration > 0 && phaseDuration <= 600 && phaseAge <= phaseDuration + .001f)
            && (!radioVolumeInitialized || float.IsFinite(radioVolume) && radioVolume>=.18f && radioVolume<=1)
            && (!skippedMusic || phase >= RoadRadioPhase.Song && float.IsFinite(skipStartProgress)
                && skipStartProgress >= 0 && skipStartProgress <= 1)
            && (!callAnswered || callOffered) && (!hauntedPowerTried || phase>=RoadRadioPhase.Song)
            && (phase >= RoadRadioPhase.Song || !callOffered && !phoneMoved);

        public static BeforeDemolitionRoadState Parse(string json)
        {
            var state = new BeforeDemolitionRoadState { version = 0, phase = (RoadRadioPhase)(-1), phaseAge = -1, phaseDuration = -1,
                emails = new EmailInboxState { storyId = null, version = 0, deliveredIds = null, readIds = null } };
            JsonUtility.FromJsonOverwrite(json, state); return state;
        }
    }
}
