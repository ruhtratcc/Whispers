// Controla a viagem da versão anterior da história.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    /// <summary>
    /// A short seated journey, followed by the arrival call and the walk to the gate.
    /// Milestones are the authority on reload; no vehicle simulation or hostile AI is used.
    /// </summary>
    [DefaultExecutionOrder(75), DisallowMultipleComponent]
    public sealed class RoadJourneyFlow : MonoBehaviour
    {
        [Header("Existing narrative and player")]
        public NarrativeDirector director;
        public FirstPersonMotor player;
        public Camera playerCamera;
        public InputReader input;
        public FlashlightController flashlight;
        [Header("New campaign transport — legacy narrative stays independent")]
        public bool externalControl;
        public string externalChapterId = "bd.road";
        [Header("Journey — seated pose is at eye height, exit pose at foot height")]
        public Transform carRoot, driveStart, driveEnd, seatedPose, exitPose;
        [Tooltip("Optional ordered world-space waypoints, including the start and end.")]
        public Transform[] driveWaypoints = Array.Empty<Transform>();
        [Min(1)] public float driveDuration = 24;
        [Range(20, 100)] public float seatedYawLimit = 75;
        [Range(10, 60)] public float seatedPitchLimit = 38;
        [Min(.001f)] public float lookSensitivity = .07f;
        [Header("Arrival")]
        public Transform windowAnchor;
        public GameObject windowVisual;
        public Light windowLight;
        [Min(.1f)] public float windowObservationSeconds = .45f;
        [Range(5, 60)] public float windowObservationAngle = 30;
        [Min(.1f)] public float windowLightSeconds = 4;
        public LayerMask observationMask = ~0;
        public RoadJourneyInteraction telephone, flashlightPickup, driverDoor, gate;
        [Header("Recorded Foley only — spoken content stays in captions")]
        public AudioSource driveHum;
        [Header("Subtle driving presentation")]
        public Transform steeringWheel;
        public Vector3 steeringAxis = new Vector3(0, -.25f, -.968f);
        [Range(0, 8)] public float steeringDegrees = 3;
        public SoundSet ringSound, receiverSound, parkedSound;
        [Min(.5f)] public float ringInterval = 3.6f;
        [Range(0, 1)] public float ringGain = .28f;
        [Header("Presentation hooks, not invoked during checkpoint reconstruction")]
        public UnityEvent onParked = new UnityEvent();
        public UnityEvent onCallAnswered = new UnityEvent();
        public UnityEvent onWindowSeen = new UnityEvent();
        public UnityEvent onFlashlightTaken = new UnityEvent();
        public UnityEvent onExitedCar = new UnityEvent();

        public bool IsRoadActive => initialized && director && director.IsInitialized
            && director.Progress.chapterId == (externalControl ? externalChapterId : "road");
        public bool Seated { get; private set; }
        public float Drive01 => Mathf.Clamp01(driveAge / Mathf.Max(1, driveDuration));
        public bool IsRinging => CanAct && director.NextSignal == "road.arrival-call";
        bool CanStep => IsRoadActive && director.game && director.game.CanControl
            && director.game.State == GameState.Playing;
        public bool CanAct => CanStep && !director.SequencePlaying;

        bool initialized, capturedPlayerState, originalMotorEnabled, originalControllerEnabled;
        bool originalFlashlightEnabled, originalHandVisible, driveWasPaused;
        float driveAge, windowLookAge, windowLightAge, ringAge;
        Vector2 seatedLookAngles;
        Transform lookPose;
        NarrativeDirector subscribedDirector;
        AudioSource currentRing;
        Quaternion steeringRest;
        bool steeringCaptured;
        float steeringAge;

        void OnEnable()
        {
            if (externalControl) return;
            subscribedDirector = director;
            if (subscribedDirector)
            {
                subscribedDirector.Initialized += RestoreWorldSilently;
                subscribedDirector.SignalAccepted += SignalAccepted;
            }
            if (director && director.IsInitialized) RestoreWorldSilently();
        }

        /// <summary>Reconstruct only unconsumed beats; completed calls and scares remain silent.</summary>
        public void RestoreWorldSilently()
        {
            if (externalControl) return;
            initialized = director && director.IsInitialized && director.Progress.chapterId == "road";
            StopRing();
            driveAge = windowLookAge = windowLightAge = ringAge = 0;
            if (!initialized) { ReleaseSeat(); return; }
            CapturePlayerState();
            bool parked = director.HasSignal("road.car-parked");
            driveAge = parked ? Mathf.Max(1, driveDuration) : 0;
            PlaceCar(parked ? 1 : 0);
            MigrateLegacyWalkingCheckpoint();
            bool exited = director.HasSignal("road.car-exited") || director.HasSignal("road.school-gate");
            bool hasFlashlight = director.HasSignal("road.flashlight-taken");
            if (exited)
            {
                ReleaseSeat();
                SetFlashlightAvailable(true, false);
            }
            else
            {
                SitInCar();
                SetFlashlightAvailable(hasFlashlight, false);
            }
            // A window event already acknowledged by the player does not replay on load.
            SetWindow(director.NextSignal == "road.window-lit");
            RefreshPickup();
            UpdateDriveAudio(false);
        }

        void CapturePlayerState()
        {
            if (capturedPlayerState || !player || !player.Controller) return;
            capturedPlayerState = true;
            originalMotorEnabled = player.enabled;
            originalControllerEnabled = player.Controller.enabled;
            originalFlashlightEnabled = flashlight && flashlight.enabled;
            originalHandVisible = flashlight && flashlight.handRig && flashlight.handRig.gameObject.activeSelf;
        }

        void SitInCar()
        {
            if (!player || !seatedPose || !player.view || !capturedPlayerState) return;
            if (!lookPose)
            {
                var poseObject = new GameObject("Road — seated look pose");
                lookPose = poseObject.transform;
            }
            lookPose.SetParent(seatedPose, false);
            lookPose.localPosition = Vector3.zero;
            seatedLookAngles = Vector2.zero;
            lookPose.localRotation = Quaternion.identity;
            // IsHidden is deliberately not used: SaveSystem rejects hidden-player saves.
            player.enabled = false;
            player.Controller.enabled = false;
            player.HoldingBreath = false;
            Seated = true;
            FollowSeat();
            player.view.OverridePose(lookPose, true);
            player.view.Snap();
        }

        void ReleaseSeat()
        {
            if (Seated && player && player.view) player.view.ClearOverride();
            Seated = false;
            if (!capturedPlayerState || !player) return;
            if (player.Controller) player.Controller.enabled = originalControllerEnabled;
            player.enabled = originalMotorEnabled;
        }

        void FollowSeat()
        {
            if (!Seated || !player || !lookPose) return;
            player.transform.SetPositionAndRotation(lookPose.position - Vector3.up * player.EyeHeight,
                Quaternion.Euler(0, lookPose.eulerAngles.y, 0));
            // NarrativeDirector releases its control lock by clearing the camera override.
            // Our seated ownership remains until the explicit flashlight/exit interaction.
            if (player.view && !player.view.HasOverride) player.view.OverridePose(lookPose, true);
        }

        void Update()
        {
            if (!IsRoadActive) return;
            if (externalControl)
            {
                if (CanStep) { UpdateSeatedLook(); UpdateSteering(); }
                return;
            }
            bool driving = !director.HasSignal("road.car-parked")
                && (director.HasSignal("road.context-shown") || director.ActiveSequenceId == "road.context");
            UpdateDriveAudio(CanStep && driving);
            if (!CanStep) { StopRing(); return; }
            UpdateSeatedLook();
            UpdateSteering();
            if (director.NextSignal == "road.context-shown" && CanAct)
            {
                director.PlaySequence("road.context");
            }
            if (driving)
            {
                driveAge = Mathf.Min(Mathf.Max(1, driveDuration), driveAge + Time.deltaTime);
                PlaceCar(Drive01);
                FollowSeat();
                // Radio captions may still be running when a shortened test drive ends.
                // Respect the milestone order instead of skipping the context completion.
                if (Drive01 >= 1 && CanAct && director.NextSignal == "road.car-parked")
                    director.Signal("road.car-parked");
            }
            if (!CanAct) { StopRing(); return; }
            if (director.NextSignal == "road.arrival-call") UpdateRing();
            else if (director.NextSignal == "road.window-lit") ObserveWindow();
            else if (director.HasSignal("road.window-lit"))
            {
                windowLightAge += Time.deltaTime;
                if (windowLightAge >= windowLightSeconds) SetWindow(false);
            }
        }

        void LateUpdate() => FollowSeat();

        void UpdateSteering()
        {
            if (!steeringWheel) return;
            if (!steeringCaptured) { steeringRest=steeringWheel.localRotation; steeringCaptured=true; }
            bool moving=Seated&&driveAge<Mathf.Max(1,driveDuration)-.001f;
            if(moving)steeringAge+=Time.deltaTime;
            float angle=moving?steeringDegrees*(.75f*Mathf.Sin(steeringAge*.72f)+.25f*Mathf.Sin(steeringAge*2.11f)):0;
            steeringWheel.localRotation=Quaternion.Slerp(steeringWheel.localRotation,steeringRest*Quaternion.AngleAxis(angle,steeringAxis.normalized),1-Mathf.Exp(-Time.deltaTime*5));
        }

        void UpdateSeatedLook()
        {
            if (!Seated || !lookPose || !input || input.Actions == null) return;
            Vector2 delta = input.Look;
            float sensitivity = Mathf.Max(.001f, lookSensitivity)
                * PlayerPrefs.GetFloat("HorrorMenu.Sensitivity", 1);
            var action = input.Actions.FindAction("Gameplay/Look");
            if (action != null && action.activeControl?.device is UnityEngine.InputSystem.Gamepad)
                delta *= 180 * Time.deltaTime / Mathf.Max(.001f, lookSensitivity);
            float invert = PlayerPrefs.GetInt("HorrorMenu.InvertY", 0) == 1 ? -1 : 1;
            seatedLookAngles.x = Mathf.Clamp(seatedLookAngles.x + delta.x * sensitivity, -seatedYawLimit, seatedYawLimit);
            seatedLookAngles.y = Mathf.Clamp(seatedLookAngles.y - delta.y * sensitivity * invert, -seatedPitchLimit, seatedPitchLimit);
            lookPose.localRotation = Quaternion.Euler(seatedLookAngles.y, seatedLookAngles.x, 0);
        }

        void PlaceCar(float progress)
        {
            if (!carRoot || !driveStart || !driveEnd) return;
            // Easing only controls speed. Geometry and checkpoints never depend on vehicle physics.
            float p = Mathf.Clamp01(progress);
            // The new trip starts at cruising speed; ease only the final approach to the gate.
            float t = externalControl ? (p<=.9f?p/.95f:1-(1-p)*(1-p)/.19f) : Mathf.SmoothStep(0,1,p);
            if (driveWaypoints != null && driveWaypoints.Length >= 2
                && !Array.Exists(driveWaypoints, point => !point))
            {
                float total = 0;
                for (int i = 1; i < driveWaypoints.Length; i++)
                    total += Vector3.Distance(driveWaypoints[i - 1].position, driveWaypoints[i].position);
                float remaining = t * total;
                for (int i = 1; i < driveWaypoints.Length; i++)
                {
                    float length = Vector3.Distance(driveWaypoints[i - 1].position, driveWaypoints[i].position);
                    if (remaining <= length || i == driveWaypoints.Length - 1)
                    {
                        float segment = length > .001f ? Mathf.Clamp01(remaining / length) : 1;
                        carRoot.SetPositionAndRotation(Vector3.Lerp(driveWaypoints[i - 1].position, driveWaypoints[i].position, segment),
                            Quaternion.Slerp(driveWaypoints[i - 1].rotation, driveWaypoints[i].rotation, segment));
                        return;
                    }
                    remaining -= length;
                }
            }
            carRoot.SetPositionAndRotation(Vector3.Lerp(driveStart.position, driveEnd.position, t),
                Quaternion.Slerp(driveStart.rotation, driveEnd.rotation, t));
        }

        void ObserveWindow()
        {
            bool observing = ObservesWindow();
            windowLookAge = observing ? windowLookAge + Time.deltaTime : 0;
            if (windowLookAge >= windowObservationSeconds) director.Signal("road.window-lit");
        }

        public bool ObservesWindow()
        {
            if (!playerCamera || !windowAnchor) return false;
            Vector3 delta = windowAnchor.position - playerCamera.transform.position;
            float distance = delta.magnitude;
            if (distance < .01f || Vector3.Angle(playerCamera.transform.forward, delta) > windowObservationAngle) return false;
            if (!Physics.Raycast(playerCamera.transform.position, delta / distance, out var hit, distance,
                observationMask, QueryTriggerInteraction.Ignore)) return true;
            return hit.transform == windowAnchor || hit.transform.IsChildOf(windowAnchor)
                || windowAnchor.IsChildOf(hit.transform) || hit.distance >= distance - .2f;
        }

        public bool CanInteract(RoadJourneyInteractionKind kind)
        {
            if (externalControl) return false;
            if (!CanAct) return false;
            switch (kind)
            {
                case RoadJourneyInteractionKind.AnswerPhone:
                    return Seated && director.NextSignal == "road.arrival-call";
                case RoadJourneyInteractionKind.TakeFlashlight:
                    return Seated && director.NextSignal == "road.flashlight-taken";
                case RoadJourneyInteractionKind.ExitCar:
                    return Seated && exitPose && director.NextSignal == "road.car-exited";
                case RoadJourneyInteractionKind.SchoolGate:
                    return !Seated && director.NextSignal == "road.school-gate";
                default: return false;
            }
        }

        public bool Interact(RoadJourneyInteraction interaction)
        {
            if (!interaction || interaction.flow != this || !CanInteract(interaction.kind)) return false;
            switch (interaction.kind)
            {
                case RoadJourneyInteractionKind.AnswerPhone:
                    if (!director.PlaySequence("road.arrival-call")) return false;
                    StopRing();
                    Play(receiverSound, interaction.transform.position, .3f);
                    onCallAnswered.Invoke();
                    return true;
                case RoadJourneyInteractionKind.TakeFlashlight:
                    if (!director.Signal("road.flashlight-taken")) return false;
                    SetFlashlightAvailable(true, true);
                    RefreshPickup();
                    onFlashlightTaken.Invoke();
                    director.ScheduleSave();
                    return true;
                case RoadJourneyInteractionKind.ExitCar:
                    if (!player || !exitPose || !director.Signal("road.car-exited")) return false;
                    // The door is a static interaction target. Exit immediately without animation.
                    ReleaseSeat();
                    player.Warp(exitPose.position, exitPose.eulerAngles.y);
                    player.RestoreStance(false);
                    RefreshPickup();
                    onExitedCar.Invoke();
                    director.ScheduleSave();
                    return true;
                case RoadJourneyInteractionKind.SchoolGate:
                    if (!player || Vector3.Distance(player.transform.position, interaction.transform.position) > 3.2f) return false;
                    return director.Signal("road.school-gate");
                default: return false;
            }
        }

        void SignalAccepted(string id)
        {
            if (!IsRoadActive) return;
            switch (id)
            {
                case "road.car-parked":
                    PlaceCar(1);
                    UpdateDriveAudio(false);
                    Play(parkedSound, carRoot ? carRoot.position : transform.position, .3f);
                    onParked.Invoke();
                    break;
                case "road.arrival-call":
                    SetWindow(true);
                    break;
                case "road.window-lit":
                    windowLightAge = 0;
                    director.game.atmosphere?.AddTension(.12f);
                    director.game.atmosphere?.ReactToScare(.18f, 2);
                    onWindowSeen.Invoke();
                    RefreshPickup();
                    break;
            }
        }

        void RefreshPickup()
        {
            // The torch remains on the seat before it can be collected; only the interaction is gated.
            if (flashlightPickup) flashlightPickup.SetVisible(!director.HasSignal("road.flashlight-taken"));
        }

        void MigrateLegacyWalkingCheckpoint()
        {
            if (!director.HasSignal("road.flashlight-taken") || director.HasSignal("road.car-exited")
                || director.NextSignal != "road.car-exited") return;
            // Earlier builds coupled collecting the torch with exiting the car. A new seated
            // save has the same old milestone but its body still lies inside the cabin.
            bool walking = director.HasSignal("road.school-gate") || IsSupportedOutsideCabin();
            if (!walking || !director.Progress.TryComplete(director.book, "road.car-exited",
                item => director.inventory && director.inventory.Contains(item))) return;
            var exitMilestone = Array.Find(director.CurrentChapter.milestones, milestone => milestone.id == "road.car-exited");
            if (!director.HasSignal("road.school-gate") && exitMilestone != null && director.game.hud)
                director.game.hud.ShowObjective(exitMilestone.objectiveAfter);
            director.ScheduleSave(); // No accepted-signal hooks, sounds or door interaction replay.
        }

        bool IsSupportedOutsideCabin()
        {
            if (!player || !carRoot || !seatedPose) return false;
            Vector3 body = player.transform.position;
            var shell = carRoot.GetComponent<BoxCollider>();
            if (shell)
            {
                Vector3 local = carRoot.InverseTransformPoint(body + Vector3.up * .5f) - shell.center;
                Vector3 half = shell.size * .5f + new Vector3(.2f, 0, .2f);
                if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z) return false;
            }
            else if (Vector3.Distance(body, seatedPose.position - Vector3.up * player.EyeHeight) < 1.2f) return false;
            Physics.SyncTransforms();
            return Physics.Raycast(body + Vector3.up * .8f, Vector3.down, out var hit, 1.1f,
                ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(carRoot) && hit.normal.y >= .75f;
        }

        void SetFlashlightAvailable(bool available, bool switchOn)
        {
            if (!flashlight) return;
            flashlight.enabled = available && originalFlashlightEnabled;
            if (flashlight.handRig) flashlight.handRig.gameObject.SetActive(available && originalHandVisible);
            if (!available) flashlight.Restore(flashlight.BatterySeconds, false);
            else if (switchOn) flashlight.Restore(flashlight.BatterySeconds, true);
            if (!available && flashlight.lamp) flashlight.lamp.enabled = false;
        }

        void SetWindow(bool value)
        {
            if (windowVisual) windowVisual.SetActive(value);
            if (windowLight) windowLight.enabled = value;
        }

        void UpdateRing()
        {
            if (!ringSound || !AudioManager.Instance) return;
            ringAge -= Time.deltaTime;
            if (ringAge > 0) return;
            if (!currentRing || !currentRing.isPlaying)
                currentRing = AudioManager.Instance.Play(ringSound, telephone ? telephone.transform.position : transform.position,
                    ringGain, AudioChannel.SFX, true);
            ringAge = Mathf.Max(.5f, ringInterval);
        }

        void StopRing()
        {
            // AudioManager uses a source pool: never stop a source that has since been reused.
            if (currentRing && ringSound && ringSound.clips != null
                && Array.Exists(ringSound.clips, clip => clip == currentRing.clip)) currentRing.Stop();
            currentRing = null;
        }

        void UpdateDriveAudio(bool running)
        {
            if (!driveHum) return;
            bool paused = director && director.game && director.game.State == GameState.Paused;
            if (paused)
            {
                if (driveHum.isPlaying) { driveHum.Pause(); driveWasPaused = true; }
                return;
            }
            if (!running)
            {
                if (driveHum.isPlaying || driveWasPaused) driveHum.Stop();
                driveWasPaused = false;
                return;
            }
            if (driveWasPaused) { driveHum.UnPause(); driveWasPaused = false; }
            else if (!driveHum.isPlaying) driveHum.Play();
            driveHum.pitch=.92f+.11f*Mathf.Sin(Mathf.PI*Drive01);
        }

        static void Play(SoundSet sound, Vector3 position, float gain)
        { if (sound) AudioManager.Instance?.Play(sound, position, gain); }

        /// <summary>Reuse the seat, route and safe exit without replaying the old radio or arrival call.</summary>
        public void ReconstructExternalJourney(float progress, bool exited, bool hasFlashlight)
        {
            if (!externalControl) return;
            initialized = director && director.IsInitialized && director.Progress.chapterId == externalChapterId;
            if (!initialized) return;
            CapturePlayerState();
            SetExternalProgress(progress);
            if (exited) ReleaseSeat();
            else if (!Seated) SitInCar();
            SetExternalFlashlight(hasFlashlight, false);
        }

        public void SetExternalProgress(float progress)
        {
            if (!externalControl) return;
            driveAge = Mathf.Clamp01(progress) * Mathf.Max(1, driveDuration);
            PlaceCar(progress); FollowSeat();
            // The moving phone must match the visible car before the next interaction raycast.
            Physics.SyncTransforms();
        }

        public void SetExternalFlashlight(bool available, bool switchOn)
        {
            if (!externalControl) return;
            SetFlashlightAvailable(available, switchOn);
            if (flashlightPickup) flashlightPickup.SetVisible(!available);
        }

        public void SetExternalDrivingAudio(bool running) { if (externalControl) UpdateDriveAudio(running); }
        public void SetExternalWindow(bool lit) { if (externalControl) SetWindow(lit); }

        public void GlanceAtExternalPhone(Transform target, float blend)
        {
            if (!externalControl || !Seated || !lookPose || !target || !seatedPose) return;
            Vector3 local = seatedPose.InverseTransformDirection(target.position - lookPose.position);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            seatedLookAngles = Vector2.Lerp(seatedLookAngles,
                new Vector2(Mathf.Clamp(yaw, -seatedYawLimit, seatedYawLimit), Mathf.Clamp(pitch, -seatedPitchLimit, seatedPitchLimit)), blend);
            lookPose.localRotation = Quaternion.Euler(seatedLookAngles.y, seatedLookAngles.x, 0);
        }

        public bool ExitExternalCar()
        {
            if (!externalControl || !Seated || !player || !exitPose) return false;
            ReleaseSeat(); player.Warp(exitPose.position, exitPose.eulerAngles.y); player.RestoreStance(false); return true;
        }

        void OnDisable()
        {
            if (subscribedDirector)
            {
                subscribedDirector.Initialized -= RestoreWorldSilently;
                subscribedDirector.SignalAccepted -= SignalAccepted;
            }
            subscribedDirector = null;
            initialized = false;
            StopRing();
            if (driveHum) driveHum.Stop();
            ReleaseSeat();
            if (capturedPlayerState && flashlight)
            {
                flashlight.enabled = originalFlashlightEnabled;
                if (flashlight.handRig) flashlight.handRig.gameObject.SetActive(originalHandVisible);
            }
            if (lookPose) Destroy(lookPose.gameObject);
            lookPose = null;
            capturedPlayerState = false;
        }
    }
}
