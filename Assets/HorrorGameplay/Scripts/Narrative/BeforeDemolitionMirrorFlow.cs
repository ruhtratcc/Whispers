// Controla a apresentação do espelho, as escolhas e o retorno do apito.

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HorrorGame
{
    public enum BeforeDemolitionEndingChoice
    {
        None,
        Refuse,
        Sign
    }

    [DisallowMultipleComponent]
    public sealed class BeforeDemolitionMirrorFlow : PersistentObject, IInteractable
    {
        public const string ChapterId = "bd.mirror-report", StateId = "bd.mirror.flow";
        public const string SeenSignal = "bd.mirror.seen", BrokenSignal = "bd.mirror.broken";
        public const string ChoiceSignal = "bd.mirror.choice", FinishSignal = "bd.mirror.finish";
        public const string OfferSequence = "bd.mirror.offer", RefuseSequence = "bd.ending.refuse", SignSequence = "bd.ending.sign";
        [Serializable]
        public sealed class State
        {
            public int version = 1, stage;
            public float whistleCrack;
            public BeforeDemolitionEndingChoice choice;
            public bool apitoReturned;
            public bool ValidFields => version == 1 && stage >= 0 && stage <= 4 && float.IsFinite(whistleCrack) && whistleCrack >= 0 && whistleCrack <= 1 && (stage <= 2 ? choice == BeforeDemolitionEndingChoice.None : choice == BeforeDemolitionEndingChoice.Refuse || choice == BeforeDemolitionEndingChoice.Sign) && (!apitoReturned || stage >= 3 && choice == BeforeDemolitionEndingChoice.Refuse);

            public static State Parse(string json)
            {
                var data = new State
                {
                    version = 0,
                    stage = -1,
                    choice = (BeforeDemolitionEndingChoice)(-1)
                };
                if (string.IsNullOrWhiteSpace(json))
                    throw new ArgumentException("Estado do espelho ausente.", nameof(json));
                JsonUtility.FromJsonOverwrite(json, data);
                if (!data.ValidFields)
                    throw new ArgumentException("Estado do espelho inválido.", nameof(json));
                return data;
            }
        }

        public GameSession game;
        public BeforeDemolitionWhistle whistle;
        public BoxCollider mirrorVolume;
        public Transform mirrorPlane, reflectionHead, reflectionApproachPose;
        public GameObject glassVisual, revealVisual, reflectionVisual, helenaVisual, smileVisual, chalkTeethVisual;
        public GameObject sealedWall, rubbleVisual, returnedWhistleVisual;
        public GameObject[] whistleCracks = Array.Empty<GameObject>();
        public AudioClip refusalCry;
        public Light daylight;
        public Texture2D finalPhoto;
        public SoundSet mirrorShock, mirrorBreak, wallCollapse;
        public TMP_FontAsset normalFont, childFont;
        public BeforeDemolitionEndingView endingView;
        public EndingPresentationConfig presentationConfig;
        [Range(0, 1.5f)]
        public float recoilDistance = 1.25f;
        [Range(.25f, 2)]
        public float recoilSeconds = 1.15f;
        public event Action GoodEndingRequested;
        public bool GoodEndingReady => state.stage == 3 && state.choice == BeforeDemolitionEndingChoice.Refuse && state.apitoReturned && returnPresentationReady;
        public State Data => state;
        public bool IsActive => isActiveAndEnabled && Director && Director.IsInitialized && BeforeDemolitionCampaign.Is(Director.book) && Director.Progress.chapterId == ChapterId;
        public float ReflectionDelay => Mathf.Lerp(.5f, 1, Mathf.Clamp01(mirrorAge / 4));
        public bool IsPresenting => IsActive && (state.stage <= 2 && tracking || ownsRecoil || ownsReturn || scareRemaining > 0);

        State state = new State();
        NarrativeDirector subscribedDirector;
        BeforeDemolitionWhistle subscribedWhistle;
        BeforeDemolitionEndingView subscribedView;
        NarrativeDirector Director => game ? game.narrative : null;

        Camera PlayerCamera => game && game.player && game.player.view ? game.player.view.playerCamera : null;

        readonly Queue<Sample> samples = new Queue<Sample>();
        struct Sample
        {
            public float time;
            public Vector3 position;
            public Quaternion rotation;
        }

        Sample delayed;
        Vector3 seatedPosition, initialPlayerPosition;
        Quaternion seatedRotation, headRotation;
        float mirrorAge, initialYaw;
        bool poseCached, tracking;
        bool returnPresentationReady, goodEndingDispatched, ownsRecoil, ownsReturn;
        float scareRemaining;
        Coroutine recoilRoutine, returnRoutine;
        AudioSource returnVoice;
        bool strikeActive;
        GameObject strikeTorch;
        public bool StrikeActive => strikeActive;

        BoxCollider interactionCollider;
        Vector3 interactionCenter, interactionSize;
        void Awake()
        {
            persistentId = StateId;
            interactionCollider = GetComponent<BoxCollider>();
            if (interactionCollider)
            {
                interactionCenter = interactionCollider.center;
                interactionSize = interactionCollider.size;
            }

            if (!game)
                game = GameSession.Instance;
            CachePose();
        }

        void OnEnable()
        {
            if (!game)
                game = GameSession.Instance;
            subscribedDirector = Director;
            subscribedWhistle = whistle;
            if (subscribedDirector)
                subscribedDirector.Initialized += Initialize;
            if (Director && Director.IsInitialized)
                Initialize();
        }

        void CachePose()
        {
            if (poseCached || !reflectionVisual)
                return;
            seatedPosition = reflectionVisual.transform.position;
            seatedRotation = reflectionVisual.transform.rotation;
            headRotation = reflectionHead ? reflectionHead.localRotation : Quaternion.identity;
            poseCached = true;
        }

        void Initialize()
        {
            CachePose();
            if (!IsActive)
            {
                endingView?.Close();
                ApplyWorld();
                return;
            }

            mirrorAge = state.stage >= 1 ? 8 : 0;
            returnPresentationReady = state.apitoReturned;
            goodEndingDispatched = false;
            tracking = false;
            samples.Clear();
            ApplyWorld();
        }

        bool InsideMirror() => mirrorVolume && game && game.player && mirrorVolume.bounds.Contains(game.player.transform.position + Vector3.up * .8f);
        bool LookingAtMirror()
        {
            var camera = PlayerCamera;
            if (!camera || !mirrorVolume)
                return false;
            Vector3 point = mirrorPlane ? mirrorPlane.position : mirrorVolume.bounds.center;
            Vector3 viewport = camera.WorldToViewportPoint(point);
            Vector3 offset = point - camera.transform.position;
            return viewport.z > 0 && viewport.x > 0 && viewport.x < 1 && viewport.y > 0 && viewport.y < 1 && Vector3.Dot(camera.transform.forward, offset.normalized) > .75f;
        }

        void Update()
        {
            if (!IsActive)
                return;
            scareRemaining = Mathf.Max(0, scareRemaining - Time.deltaTime);
            if (game.CanControl && state.stage == 0 && InsideMirror() && LookingAtMirror())
                AdvanceMirror(Time.deltaTime);
            // Keep the existing ordered checkpoint milestones without treating "broken" as broken glass.
            if (state.stage == 1 && !Director.SequencePlaying && Director.NextSignal == BrokenSignal)
            {
                state.stage = 2;
                if (!Director.Signal(BrokenSignal))
                    state.stage = 1;
            }

            ApplyWorld();
            if (!game.CanControl || Director.SequencePlaying || endingView && endingView.IsOpen)
                return;
            if (state.stage == 2)
            {
                if (Director.HasSequence(OfferSequence))
                {
                    EnsureView();
                    endingView.ShowChoice();
                }
                else
                    Director.PlaySequence(OfferSequence);
            }
            else if (state.stage == 3)
            {
                string sequence = state.choice == BeforeDemolitionEndingChoice.Refuse ? RefuseSequence : SignSequence;
                if (Director.HasSequence(sequence))
                {
                    if (state.choice == BeforeDemolitionEndingChoice.Sign)
                    {
                        EnsureView();
                        endingView.ShowEpilogue(state.choice, presentationConfig && presentationConfig.schoolImage ? presentationConfig.schoolImage : finalPhoto);
                    }
                    else if (GoodEndingReady && !goodEndingDispatched && GoodEndingRequested != null)
                    {
                        goodEndingDispatched = true;
                        GoodEndingRequested.Invoke();
                    }
                }
                else
                    Director.PlaySequence(sequence);
            }
        }

        void AdvanceMirror(float seconds)
        {
            var camera = PlayerCamera;
            if (!camera || !reflectionVisual || seconds <= 0)
                return;
            if (!tracking)
            {
                initialPlayerPosition = game.player.transform.position;
                initialYaw = camera.transform.eulerAngles.y;
                delayed = new Sample
                {
                    time = Time.time,
                    position = initialPlayerPosition,
                    rotation = camera.transform.rotation
                };
                samples.Clear();
                tracking = true;
            }

            mirrorAge = Mathf.Min(8, mirrorAge + seconds);
            samples.Enqueue(new Sample { time = Time.time, position = game.player.transform.position, rotation = camera.transform.rotation });
            while (samples.Count > 1 && samples.Peek().time < Time.time - ReflectionDelay)
                delayed = samples.Dequeue();
            PresentReflection();
            if (mirrorAge < 8 || Director.NextSignal != SeenSignal)
                return;
            state.stage = 1;
            if (!Director.Signal(SeenSignal))
            {
                state.stage = 0;
                return;
            }
        }

        void PresentReflection()
        {
            if (!poseCached || !reflectionVisual)
                return;
            Vector3 approach = seatedPosition;
            if (reflectionApproachPose)
                approach = reflectionApproachPose.position;
            else if (mirrorPlane)
            {
                float side = Mathf.Sign(Vector3.Dot(seatedPosition - mirrorPlane.position, mirrorPlane.forward));
                approach = mirrorPlane.position + mirrorPlane.forward * (side == 0 ? -1 : side) * .85f;
                approach.y = seatedPosition.y;
            }

            Vector3 movement = tracking ? delayed.position - initialPlayerPosition : Vector3.zero;
            if (mirrorPlane)
                movement = Vector3.Reflect(movement, mirrorPlane.forward);
            // The authored seated body stays in its chair until its approach; head and small movements lag João.
            reflectionVisual.transform.SetPositionAndRotation(Vector3.Lerp(seatedPosition, approach, Mathf.SmoothStep(0, 1, Mathf.Clamp01((mirrorAge - 3) / 3))) + Vector3.ClampMagnitude(movement * .1f, .15f), seatedRotation);
            if (reflectionHead)
            {
                float yaw = tracking ? Mathf.Clamp(Mathf.DeltaAngle(initialYaw, delayed.rotation.eulerAngles.y), -25, 25) : 0;
                float pitch = tracking ? Mathf.Clamp(Mathf.DeltaAngle(0, delayed.rotation.eulerAngles.x), -25, 25) : 0;
                reflectionHead.localRotation = headRotation * Quaternion.Euler(pitch, yaw, 0);
            }
        }

        public string Prompt(PlayerInteraction player) => Director && Director.inventory && Director.inventory.Contains(BeforeDemolitionCampaign.WhistleItemId) ? "Devolver o apito" : "Aproximar";
        public bool CanInteract(PlayerInteraction player) => IsActive && game.CanControl && player == game.interaction && state.stage == 3 && state.choice == BeforeDemolitionEndingChoice.Refuse && !state.apitoReturned && !Director.SequencePlaying && returnRoutine == null;
        public void Interact(PlayerInteraction player)
        {
            if (CanInteract(player))
                ReturnWhistle();
        }

        // Legacy callers remain binary/source compatible; the mirror never reacts to a whistle now.
        public void WhistleTap()
        {
        }

        public void WhistleHold(float seconds)
        {
        }

        public bool BreakMirror() => false;
        public bool ReturnWhistle()
        {
            if (!IsActive || !game.CanControl || state.stage != 3 || state.choice != BeforeDemolitionEndingChoice.Refuse || state.apitoReturned || Director.SequencePlaying || returnRoutine != null)
                return false;
            // The inventory ID is a historical checkpoint requirement, not ownership of the returned prop.
            if (Director.inventory && !Director.inventory.Contains(BeforeDemolitionCampaign.WhistleItemId))
                Director.inventory.Add(new InventoryItem { id = BeforeDemolitionCampaign.WhistleItemId, title = "Apito devolvido" });
            state.apitoReturned = true;
            game.equipment?.RefreshOwnership();
            goodEndingDispatched = returnPresentationReady = false;
            returnRoutine = StartCoroutine(ReturnPresentation());
            return true;
        }

        IEnumerator ReturnPresentation()
        {
            game.DisableControl();
            ownsReturn = true;
            game.hud?.ShowSubtitle("Crianças: obrigado.", 2.4f);
            yield return WaitInGame(2.5f);
            var clip = presentationConfig ? presentationConfig.helenaReturnVoice : null;
            float duration = clip ? Mathf.Max(2.8f, clip.length + .15f) : 2.8f;
            var audio = AudioManager.Instance;
            if (audio)
                returnVoice = VoiceAudioPresentation.PlayInWorld(clip, audio.transform, transform.position + new Vector3(-2, 1.8f, -1.8f), VoicePlaybackStyle.Intercom, true, .86f);
            game.hud?.ShowSubtitle("Interfone — Helena: Obrigada, seu João.", duration);
            yield return WaitInGame(duration + .1f);
            returnVoice = null;
            returnPresentationReady = true;
            returnRoutine = null;
            if (ownsReturn)
            {
                ownsReturn = false;
                game.EnableControl();
            }
        }

        IEnumerator WaitInGame(float seconds)
        {
            while (seconds > 0)
            {
                if (game && game.State != GameState.Paused)
                    seconds -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        IEnumerator RefusalRecoil()
        {
            game.DisableControl();
            ownsRecoil = true;
            var motor = game.player;
            if (reflectionHead)
                reflectionHead.localRotation = headRotation * Quaternion.Euler(0, 180, 0);
            strikeActive = true;
            ApplyWorld();
            yield return StrikeWithFlashlight();
            strikeActive = false;
            ApplyWorld();
            scareRemaining = .55f;
            AudioManager.Instance?.Play(mirrorShock, mirrorPlane ? mirrorPlane.position : transform.position, .55f);
            AudioManager.Instance?.Play(mirrorBreak, mirrorPlane ? mirrorPlane.position : transform.position, .9f);
            motor.view?.Shake(PresentationSettings.ReduceEffects ? .015f : .1f, .65f);
            if (!PresentationSettings.ReduceEffects && Director.vintageCamera)
                Director.vintageCamera.PulseInterference(.65f, .16f);
            Vector3 direction = PlayerCamera ? -PlayerCamera.transform.forward : -motor.transform.forward;
            direction.y = 0;
            direction.Normalize();
            float distance = Mathf.Clamp(recoilDistance, 0, 1.5f);
            var controller = motor.Controller;
            if (controller)
            {
                Vector3 center = motor.transform.TransformPoint(controller.center);
                float half = Mathf.Max(0, controller.height * .5f - controller.radius);
                if (Physics.CapsuleCast(center + Vector3.up * half, center - Vector3.up * half, controller.radius, direction, out var hit, distance + .1f, motor.collisionMask, QueryTriggerInteraction.Ignore))
                    distance = Mathf.Clamp(hit.distance - .1f, 0, distance);
                if (!Physics.Raycast(motor.transform.position + direction * distance + Vector3.up * .3f, Vector3.down, 1.4f, motor.collisionMask, QueryTriggerInteraction.Ignore))
                    distance = 0;
            }
            else
                distance = 0;
            float age = 0, previous = 0, duration = Mathf.Clamp(recoilSeconds, .25f, 2);
            while (age < duration)
            {
                if (game.State != GameState.Paused)
                {
                    age += Time.unscaledDeltaTime;
                    float progress = Mathf.SmoothStep(0, 1, age / duration);
                    if (controller && controller.enabled)
                        controller.Move(direction * ((progress - previous) * distance));
                    previous = progress;
                }

                yield return null;
            }

            recoilRoutine = null;
            if (ownsRecoil)
            {
                ownsRecoil = false;
                game.EnableControl();
            }
        }

        IEnumerator StrikeWithFlashlight()
        {
            var torch = game.GetComponentInChildren<FlashlightController>(true);
            if (torch && torch.handRig && PlayerCamera)
            {
                strikeTorch = Instantiate(torch.handRig.gameObject, PlayerCamera.transform, false);
                strikeTorch.name = "Lanterna — golpe no espelho";
                foreach (var c in strikeTorch.GetComponentsInChildren<Collider>(true))
                    c.enabled = false;
                foreach (var l in strikeTorch.GetComponentsInChildren<Light>(true))
                    l.enabled = false;
                foreach (var m in strikeTorch.GetComponentsInChildren<MonoBehaviour>(true))
                    m.enabled = false;
                foreach (var a in strikeTorch.GetComponentsInChildren<AudioSource>(true))
                {
                    a.Stop();
                    a.enabled = false;
                }

                foreach (var r in strikeTorch.GetComponentsInChildren<Renderer>(true))
                    r.enabled = true;
                strikeTorch.SetActive(true);
            }

            float reach = PlayerCamera && mirrorPlane ? Mathf.Clamp(PlayerCamera.transform.InverseTransformPoint(mirrorPlane.position).z - .1f, .55f, 1.6f) : .67f;
            float age = 0;
            while (age < .58f)
            {
                if (game.State != GameState.Paused)
                {
                    age += Time.unscaledDeltaTime;
                    float wind = Mathf.Clamp01(age / .26f), hit = Mathf.SmoothStep(0, 1, Mathf.Clamp01((age - .26f) / .32f));
                    if (strikeTorch)
                    {
                        strikeTorch.transform.localPosition = Vector3.Lerp(Vector3.Lerp(new Vector3(.18f, -.20f, .30f), new Vector3(.32f, .02f, .17f), wind), new Vector3(.015f, -.035f, reach), hit);
                        strikeTorch.transform.localRotation = Quaternion.Euler(Mathf.Lerp(-55, 12, hit), Mathf.Lerp(-18, 0, hit), Mathf.Lerp(-28, 4, hit));
                    }
                }

                yield return null;
            }

            if (strikeTorch)
                Destroy(strikeTorch);
            strikeTorch = null;
        }

        void EnsureView()
        {
            if (!endingView)
            {
                var root = new GameObject("Relatório e correio", typeof(RectTransform));
                root.transform.SetParent(transform, false);
                endingView = root.AddComponent<BeforeDemolitionEndingView>();
            }

            endingView.Initialize(game, normalFont ? normalFont : game.hud && game.hud.objective ? game.hud.objective.font : null, childFont);
            if (presentationConfig)
            {
                endingView.badCards = presentationConfig.badCards;
                endingView.cardFadeSeconds = presentationConfig.cardFadeSeconds;
                endingView.badCardSeconds = presentationConfig.badSeconds;
                endingView.badPhotoSeconds = presentationConfig.photoSeconds;
                endingView.badCreditsSeconds = presentationConfig.creditsSeconds;
            }

            endingView.helenaOfferReminders = presentationConfig ? presentationConfig.helenaOfferReminders : Array.Empty<AudioClip>();
            if (subscribedView == endingView)
                return;
            UnsubscribeView();
            subscribedView = endingView;
            subscribedView.ChoiceSelected += Choose;
            subscribedView.Continued += CompleteEnding;
        }

        public void Choose(BeforeDemolitionEndingChoice choice)
        {
            if (!IsActive || game.State != GameState.Playing && game.State != GameState.Inspecting || state.stage != 2 || !Director.HasSequence(OfferSequence) || Director.SequencePlaying || Director.NextSignal != ChoiceSignal || choice != BeforeDemolitionEndingChoice.Refuse && choice != BeforeDemolitionEndingChoice.Sign)
                return;
            endingView?.Close();
            state.choice = choice;
            state.stage = 3;
            if (!Director.Signal(ChoiceSignal))
            {
                state.choice = BeforeDemolitionEndingChoice.None;
                state.stage = 2;
            }
            else if (choice == BeforeDemolitionEndingChoice.Refuse)
            {
                recoilRoutine = StartCoroutine(RefusalRecoil());
            }

            ApplyWorld();
        }

        public void CompleteEnding()
        {
            if (!IsActive || game.State != GameState.Playing && game.State != GameState.Inspecting || Director.SequencePlaying || state.stage != 3 || Director.NextSignal != FinishSignal || state.choice == BeforeDemolitionEndingChoice.Refuse && !state.apitoReturned)
                return;
            string sequence = state.choice == BeforeDemolitionEndingChoice.Refuse ? RefuseSequence : SignSequence;
            if (!Director.HasSequence(sequence))
                return;
            endingView?.Close();
            state.stage = 4;
            if (!Director.Signal(FinishSignal))
                state.stage = 3;
        }

        public void RestoreWorldSilently() => ApplyWorld();
        void ApplyWorld()
        {
            bool active = IsActive;
            bool refused = state.stage >= 3 && state.choice == BeforeDemolitionEndingChoice.Refuse && !strikeActive;
            if (interactionCollider)
            {
                // The reveal exposes a real opening; keep the interaction on the belongings, not an invisible wall.
                interactionCollider.center = refused && returnedWhistleVisual ? transform.InverseTransformPoint(returnedWhistleVisual.transform.position) : interactionCenter;
                interactionCollider.size = refused ? new Vector3(.55f, .3f, .55f) : interactionSize;
            }

            for (int i = 0; i < whistleCracks.Length; i++)
                SetVisible(whistleCracks[i], false);
            // The intact mirror also covers the opening before its chapter becomes active.
            SetVisible(glassVisual, !refused);
            SetVisible(revealVisual, active && refused);
            SetVisible(sealedWall, false);
            SetVisible(returnedWhistleVisual, active && refused && state.apitoReturned);
            SetVisible(rubbleVisual, active && refused);
            SetVisible(helenaVisual, false);
            SetVisible(reflectionVisual, active && (state.stage < 3 && InsideMirror() && LookingAtMirror() || state.stage >= 3 && state.choice == BeforeDemolitionEndingChoice.Sign || scareRemaining > 0));
            SetVisible(smileVisual, active && state.stage >= 3);
            SetVisible(chalkTeethVisual, active && refused && scareRemaining > 0);
            if (active && state.stage <= 2)
                PresentReflection();
            if (daylight)
                daylight.enabled = active && refused && state.apitoReturned;
        }

        static void SetVisible(GameObject target, bool visible)
        {
            if (target && target.activeSelf != visible)
                target.SetActive(visible);
        }

        // Retry edits a detached checkpoint, never the live or personal save instance.
        public static SaveData PrepareChoiceRetrySnapshot(SaveData checkpoint)
        {
            if (checkpoint?.narrative?.chapterId != ChapterId)
                return checkpoint;
            var snapshot = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(checkpoint));
            var own = snapshot.objects?.Find(o => o != null && o.id == StateId);
            if (own == null)
                return null;
            State restored;
            try
            {
                restored = State.Parse(own.json);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (restored.stage < 2)
                return snapshot;
            restored.stage = 2;
            restored.choice = BeforeDemolitionEndingChoice.None;
            restored.apitoReturned = false;
            restored.whistleCrack = 0;
            own.json = JsonUtility.ToJson(restored);
            snapshot.narrative.milestones.RemoveAll(id => id == ChoiceSignal || id == FinishSignal);
            snapshot.narrative.sequences.RemoveAll(id => id == RefuseSequence || id == SignSequence);
            snapshot.narrative.completedChapters.RemoveAll(id => id == ChapterId);
            snapshot.narrative.endingSeen = false;
            snapshot.narrative.pendingChapterId = "";
            return snapshot;
        }

        public override string CaptureState()
        {
            Debug.Assert(state.ValidFields, "Invalid mirror state.");
            return JsonUtility.ToJson(state);
        }

        public override void RestoreState(string json)
        {
            state = State.Parse(json);
            mirrorAge = state.stage >= 1 ? 8 : 0;
            StopPresentations();
            returnPresentationReady = state.apitoReturned;
            goodEndingDispatched = false;
            scareRemaining = 0;
            tracking = false;
            samples.Clear();
            endingView?.Close();
            ApplyWorld();
        }

        void StopPresentations()
        {
            if (recoilRoutine != null)
                StopCoroutine(recoilRoutine);
            if (returnRoutine != null)
                StopCoroutine(returnRoutine);
            recoilRoutine = returnRoutine = null;
            if (returnVoice && presentationConfig && returnVoice.clip == presentationConfig.helenaReturnVoice)
                returnVoice.Stop();
            returnVoice = null;
            strikeActive = false;
            if (strikeTorch)
                Destroy(strikeTorch);
            strikeTorch = null;
            if ((ownsRecoil || ownsReturn) && game)
                game.EnableControl();
            ownsRecoil = ownsReturn = false;
        }

        void UnsubscribeView()
        {
            if (!subscribedView)
                return;
            subscribedView.ChoiceSelected -= Choose;
            subscribedView.Continued -= CompleteEnding;
            subscribedView = null;
        }

        void OnDisable()
        {
            if (subscribedDirector)
                subscribedDirector.Initialized -= Initialize;
            subscribedDirector = null;
            subscribedWhistle = null;
            StopPresentations();
            endingView?.Close();
            UnsubscribeView();
            samples.Clear();
            tracking = false;
        }
    }
}
