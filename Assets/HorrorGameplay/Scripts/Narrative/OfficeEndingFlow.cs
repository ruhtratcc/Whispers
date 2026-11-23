// Coordena o retorno ao escritório no fluxo narrativo ao qual está ligado.

using TMPro;
using UnityEngine;

namespace HorrorGame
{
    /// <summary>The last chapter returns to the existing office, without replaying its prologue.</summary>
    [DisallowMultipleComponent]
    public sealed class OfficeEndingFlow : MonoBehaviour
    {
        public NarrativeDirector director;
        public FirstPersonMotor player;
        public StoryTelephone telephone;
        [Tooltip("Eye-height pose at the existing desk, facing the telephone.")]
        public Transform seatedPose;
        public TMP_Text callerDisplay;
        public string schoolNumber = "0000-0113";
        public GameObject initialCaseNote, endingCaseNote, penChalk;
        public Transform personalPen, initialPenPose, endingPenPose;

        NarrativeDirector subscribed;
        Camera endingCamera;
        int originalMask;
        CameraClearFlags originalClearFlags;
        Color originalBackground;
        bool active, cameraDarkened;

        public bool IsEndingActive => active && director && director.IsInitialized
            && director.Progress.chapterId == "last-exit" && director.HasSignal("last-exit.outside");

        void OnEnable()
        {
            subscribed = director;
            if (!subscribed) return;
            subscribed.Initialized += RestoreWorld;
            subscribed.SequenceStarted += SequenceStarted;
            if (subscribed.IsInitialized) RestoreWorld();
        }

        void RestoreWorld()
        {
            RestoreCamera();
            if (active && telephone) telephone.SetRinging(false);
            active = director && director.IsInitialized && director.Progress.chapterId == "last-exit"
                && director.HasSignal("last-exit.outside");
            if (initialCaseNote) initialCaseNote.SetActive(!active);
            if (endingCaseNote) endingCaseNote.SetActive(active);
            if (penChalk) penChalk.SetActive(active);
            var penPose = active ? endingPenPose : initialPenPose;
            if (personalPen && penPose) personalPen.SetPositionAndRotation(penPose.position, penPose.rotation);
            if (!active) return;
            if (!player && director.game) player = director.game.player;
            if (player && seatedPose)
            {
                player.Warp(seatedPose.position - Vector3.up * player.EyeHeight, seatedPose.eulerAngles.y);
                if (player.view)
                {
                    player.view.Snap();
                    player.view.OverridePose(seatedPose, true);
                }
            }
            director.game?.DisableControl();
            if (callerDisplay) callerDisplay.text = "ESCOLA\n" + schoolNumber;
            // A checkpoint taken during the ring resumes that beat; the first call never replays.
            if (telephone) telephone.SetRinging(!director.HasSignal("last-exit.ending")
                && director.HasSignal("last-exit.awake"));
            if (director.HasSignal("last-exit.ending")) DarkenCamera();
        }

        void Update()
        {
            if (IsEndingActive && seatedPose && player && player.view) player.view.OverridePose(seatedPose, true);
            if (!IsEndingActive || !director.game || director.SequencePlaying
                || (director.game.State != GameState.Playing && director.game.State != GameState.Cutscene)) return;
            switch (director.NextSignal)
            {
                case "last-exit.awake": director.PlaySequence("simplified.office-awake"); break;
                case "last-exit.phone-ringing":
                    if (telephone) telephone.SetRinging(true);
                    director.PlaySequence("simplified.office-ring");
                    break;
                case "last-exit.ending": director.PlaySequence("ending"); break;
            }
        }

        void SequenceStarted(string id)
        {
            if (!IsEndingActive || id != "ending") return;
            if (telephone) telephone.SetRinging(false);
            DarkenCamera();
        }

        void DarkenCamera()
        {
            if (cameraDarkened || !player || !player.view || !player.view.playerCamera) return;
            endingCamera = player.view.playerCamera;
            originalMask = endingCamera.cullingMask;
            originalClearFlags = endingCamera.clearFlags;
            originalBackground = endingCamera.backgroundColor;
            // Keep the existing overlay captions above a black world; no second UI or camera is needed.
            endingCamera.cullingMask = 0;
            endingCamera.clearFlags = CameraClearFlags.SolidColor;
            endingCamera.backgroundColor = Color.black;
            cameraDarkened = true;
        }

        void OnDisable()
        {
            if (subscribed)
            {
                subscribed.Initialized -= RestoreWorld;
                subscribed.SequenceStarted -= SequenceStarted;
            }
            subscribed = null;
            if (active && telephone) telephone.SetRinging(false);
            if (active && player && player.view) player.view.ClearOverride();
            RestoreCamera();
            cameraDarkened = active = false;
        }

        void RestoreCamera()
        {
            if (cameraDarkened && endingCamera)
            {
                endingCamera.cullingMask = originalMask;
                endingCamera.clearFlags = originalClearFlags;
                endingCamera.backgroundColor = originalBackground;
            }
            cameraDarkened = false;
        }
    }
}
