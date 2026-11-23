// Aplica mudanças de percepção e consequências do blackout à cena.

using System;
using UnityEngine;

namespace HorrorGame
{
    /// <summary>One visual uncertainty, only while outside the player's sight. No AI or obstruction.</summary>
    [DisallowMultipleComponent]
    public sealed class SchoolPerceptionEvents : PersistentObject
    {
        public NarrativeDirector director;
        public Camera playerCamera;
        public Transform chair;
        public Vector3 originalChairPosition, changedChairPosition;
        public SoundSet movementSound;
        bool chairMoved, blackoutApplied;
        [Serializable] sealed class Data { public bool chairMoved, blackoutApplied; }
        void Update()
        {
            if (chairMoved || blackoutApplied || !chair || !director || !director.IsInitialized || !director.game || !director.game.CanControl
                || director.Progress.chapterId!="school" || director.SequencePlaying || !director.HasSignal("school.photograph")
                || director.HasSignal("school.blackout") || !playerCamera) return;
            Vector3 delta=chair.position-playerCamera.transform.position;
            if (delta.magnitude<4 || delta.magnitude>24 || Vector3.Angle(playerCamera.transform.forward,delta)<100) return;
            chairMoved=true;
            chair.position=changedChairPosition;
            if (movementSound) AudioManager.Instance?.Play(movementSound,chair.position,.18f,AudioChannel.Ambience,true);
            director.ScheduleSave();
        }
        public void ApplyBlackoutConsequence()
        {
            if (!chair) return;
            // Alternate authored placements so repeated blackouts cannot drift into a passage.
            chairMoved=!chairMoved;
            blackoutApplied=true;
            chair.position=chairMoved?changedChairPosition:originalChairPosition;
        }
        public override string CaptureState()=>JsonUtility.ToJson(new Data {chairMoved=chairMoved,blackoutApplied=blackoutApplied});
        public override void RestoreState(string json)
        {
            var data=JsonUtility.FromJson<Data>(json);
            chairMoved=data!=null&&data.chairMoved;
            blackoutApplied=data!=null&&data.blackoutApplied;
            if (chair) chair.position=chairMoved?changedChairPosition:originalChairPosition;
        }
    }
}
