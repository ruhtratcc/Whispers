// Executa a investigação da área antiga no roteiro anterior.

using System;
using UnityEngine;

namespace HorrorGame
{
    /// <summary>Authored investigation and stationary presences. No AI, damage or physical apparition.</summary>
    [DisallowMultipleComponent]
    public sealed class SchoolLeftBehindFlow : PersistentObject
    {
        public NarrativeDirector director;
        public Transform player;
        public Camera playerCamera;
        public SchoolLeftBehindClue reportClue, photographClue, keyClue;
        public BoxCollider returnVolume;
        public Transform demonVisual, distantPose, closerPose, reliefAnchor, wallAnchor;
        public GameObject wallMessage, ghostGuide;
        public GameObject memoryAnchor;
        public InteractiveDoor destinationDoor, photographRoomDoor;
        public Transform changedChair;
        public Vector3 chairOriginal, chairChanged;
        public Light[] eventLights = Array.Empty<Light>();
        public SchoolHorrorAtmosphere ambience;
        public SoundSet movementSound;
        public LayerMask observationMask = ~0;
        [Min(.1f)] public float gazeSeconds = 2.2f, quietSeconds = 1.5f;
        public float observationAngle = 26, apparitionDistance = 23, wallDistance = 6;

        [Serializable] sealed class Data
        {
            public int version = 1;
            public bool firstEligible, firstHidden, secondHidden, firstSeen, secondSeen, chairMoved, doorClosed;
        }
        Data state = new Data();
        NarrativeDirector subscribed;
        float gazeAge, quietAge, wallAge, lightAge;
        bool firstEligible, lighting;
        public bool IsChapterActive => director && director.IsInitialized && director.Progress.chapterId == "left-behind";
        bool CanAct => IsChapterActive && director.game && director.game.CanControl && !director.SequencePlaying;
        Inventory Inventory => director ? director.inventory : null;

        void OnEnable()
        {
            subscribed = director;
            if (subscribed)
            {
                subscribed.Initialized += RestoreWorldSilently;
                subscribed.SignalAccepted += Accepted;
                if (subscribed.IsInitialized) RestoreWorldSilently();
            }
        }
        public void RestoreWorldSilently()
        {
            gazeAge = quietAge = wallAge = lightAge = 0;
            firstEligible = IsChapterActive && (state.firstEligible || director.HasSignal("left-behind.demon-appeared"));
            RestoreLights();
            bool active = IsChapterActive;
            if (memoryAnchor) memoryAnchor.SetActive(active);
            if (director && director.HasSignal("left-behind.safe-place")) state.secondHidden = true;
            reportClue?.SetVisible(active && !director.HasSignal("left-behind.documents-found"));
            photographClue?.SetVisible(active && !(Inventory && Inventory.Contains("old_student_photo")));
            keyClue?.SetVisible(active && !(Inventory && Inventory.Contains("old_key")));
            if (demonVisual)
            {
                bool second = active && director.HasSignal("left-behind.chase-started");
                demonVisual.position = second && closerPose ? closerPose.position : distantPose ? distantPose.position : demonVisual.position;
                demonVisual.gameObject.SetActive(active && (second ? !state.secondHidden : firstEligible && !state.firstHidden));
            }
            if (wallMessage) wallMessage.SetActive(active && director.HasSignal("left-behind.safe-place")
                && !director.HasSignal("left-behind.wall-message"));
            if (ghostGuide) ghostGuide.SetActive(active && director.HasSignal("left-behind.wall-message")
                && !director.HasSignal("left-behind.locked-area-opened"));
            if (changedChair) changedChair.position = state.chairMoved ? chairChanged : chairOriginal;
            if (destinationDoor)
            {
                if (director && director.HasSignal("left-behind.locked-area-opened")) destinationDoor.SetLocked(false);
                else
                {
                    destinationDoor.requiredKey = active && director.HasSignal("left-behind.wall-message") ? "old_key" : "";
                    destinationDoor.lockedMessage = active && director.HasSignal("left-behind.wall-message")
                        ? "Trancada. A chave antiga deve servir aqui." : "Arquivo trancado. Os registros do Grêmio podem explicar como entrar.";
                    destinationDoor.SetLocked(true);
                    destinationDoor.SetOpen(false, true);
                }
            }
        }
        public bool CanReadClue(SchoolLeftBehindClueKind kind)
        {
            if (!CanAct) return false;
            return director.NextSignal == (kind == SchoolLeftBehindClueKind.Report ? "left-behind.documents-found"
                : kind == SchoolLeftBehindClueKind.Photograph ? "left-behind.photograph" : "left-behind.key-found");
        }
        public bool ReadClue(SchoolLeftBehindClue clue)
        {
            if (!clue || !CanReadClue(clue.kind) || !Inventory) return false;
            if (clue.kind == SchoolLeftBehindClueKind.Report)
            {
                Inventory.Add(new InventoryItem { id = "old_disappearance_report", title = clue.title, text = clue.body, note = true });
                director.Signal("left-behind.documents-found");
                director.game.hud?.ShowNote(clue.title, clue.body);
            }
            else if (clue.kind == SchoolLeftBehindClueKind.Photograph)
            {
                if (!director.PlaySequence("left-behind.photo-inscription")) return false;
                Inventory.Add(new InventoryItem { id = "old_student_photo", title = clue.title, text = clue.body, note = true });
            }
            else
            {
                Inventory.Add(new InventoryItem { id = "old_key", title = clue.title, text = clue.body });
                director.Signal("left-behind.key-found");
                director.game.hud?.Notify("Chave antiga guardada. Volte pelo corredor.");
            }
            clue.SetVisible(false);
            director.ScheduleSave();
            return true;
        }
        void Update() => AdvanceFlow(Time.deltaTime);
        public void AdvanceFlow(float deltaTime)
        {
            if (!CanAct || deltaTime <= 0) { if (lighting) RestoreLights(); return; }
            if (director.NextSignal == "left-behind.photograph" && Inventory && Inventory.Contains("old_student_photo"))
            { director.PlaySequence("left-behind.photo-inscription"); return; }
            Poltergeist();
            string next = director.NextSignal;
            if (next == "left-behind.demon-appeared")
            {
                if (!firstEligible && InsideReturn())
                {
                    firstEligible = true;
                    state.firstEligible = true;
                    if (demonVisual && distantPose) { demonVisual.position = distantPose.position; demonVisual.gameObject.SetActive(true); }
                    director.ScheduleSave();
                }
                if (firstEligible && Observes(distantPose, apparitionDistance))
                { state.firstSeen = true; director.Signal(next); }
            }
            else if (next == "left-behind.chase-started")
            {
                // The saved legacy ID names a second STILL presence, never pursuit.
                if (!state.firstHidden)
                {
                    bool seen = Observes(distantPose, apparitionDistance);
                    gazeAge = seen ? gazeAge + deltaTime : 0;
                    if ((state.firstSeen && !seen) || gazeAge >= gazeSeconds)
                    { state.firstHidden = true; HideDemon(); quietAge = gazeAge = 0; director.ScheduleSave(); }
                }
                else
                {
                    quietAge += deltaTime;
                    if (quietAge >= quietSeconds && !InView(closerPose, 65))
                    {
                        if (demonVisual && closerPose) { demonVisual.position = closerPose.position; demonVisual.gameObject.SetActive(true); }
                        director.Signal(next);
                    }
                    else if (quietAge >= quietSeconds + 4)
                    {
                        // A player who never looks away may continue: absence itself is the event.
                        state.secondHidden = true;
                        director.Signal(next);
                    }
                }
            }
            else if (next == "left-behind.safe-place")
            {
                if (!state.secondHidden)
                {
                    bool seen = Observes(closerPose, apparitionDistance);
                    if (seen) state.secondSeen = true;
                    gazeAge = seen ? gazeAge + deltaTime : 0;
                    if ((state.secondSeen && !seen) || gazeAge >= gazeSeconds
                        || (player && closerPose && Vector3.Distance(player.position, closerPose.position) < 3))
                    { state.secondHidden = true; HideDemon(); director.ScheduleSave(); }
                }
                if (state.secondHidden && player && reliefAnchor && player.position.z >= reliefAnchor.position.z - 1)
                    director.Signal(next);
            }
            else if (next == "left-behind.wall-message")
            {
                wallAge = Observes(wallAnchor, wallDistance) ? wallAge + deltaTime : 0;
                if (wallAge >= 1.1f) director.PlaySequence("left-behind.wall-inscription");
            }
            if (lighting)
            {
                lightAge += deltaTime;
                int index = Mathf.FloorToInt(lightAge / 1.3f);
                for (int i = 0; i < eventLights.Length; i++)
                    if (eventLights[i]) eventLights[i].enabled = i == index && lightAge % 1.3f < .3f;
                if (index >= eventLights.Length) RestoreLights();
            }
        }
        void Poltergeist()
        {
            if (!director.HasSignal("left-behind.photograph") || !player) return;
            if (!state.doorClosed && photographRoomDoor && Vector3.Distance(player.position, photographRoomDoor.transform.position) > 3)
            {
                state.doorClosed = true;
                // Existing door collision safeguards stop a leaf against the player. No lock is applied.
                photographRoomDoor.SetOpen(false);
                director.ScheduleSave();
            }
            if (!state.chairMoved && changedChair && Vector3.Distance(player.position, changedChair.position) < 18
                && !InView(changedChair, 95))
            {
                state.chairMoved = true; changedChair.position = chairChanged;
                AudioManager.Instance?.Play(movementSound, changedChair.position, .17f, AudioChannel.Ambience, true);
                director.ScheduleSave();
            }
        }
        bool InsideReturn() => player && returnVolume && returnVolume.bounds.Contains(player.position + Vector3.up);
        bool InView(Transform target, float angle) => target && playerCamera
            && Vector3.Angle(playerCamera.transform.forward, target.position - playerCamera.transform.position) < angle;
        public bool Observes(Transform target, float distance)
        {
            if (!target || !playerCamera) return false;
            Vector3 delta = target.position - playerCamera.transform.position;
            if (delta.magnitude > distance || !InView(target, observationAngle)) return false;
            return !Physics.Raycast(playerCamera.transform.position, delta.normalized, out var hit,
                Mathf.Max(0, delta.magnitude - .16f), observationMask, QueryTriggerInteraction.Ignore)
                || hit.transform == target || hit.transform.IsChildOf(target);
        }
        void Accepted(string signal)
        {
            if (!IsChapterActive) return;
            if (signal == "left-behind.documents-found") reportClue?.SetVisible(false);
            if (signal == "left-behind.photograph") photographClue?.SetVisible(false);
            if (signal == "left-behind.key-found") keyClue?.SetVisible(false);
            if (signal == "left-behind.demon-appeared") { lighting = true; lightAge = 0; ambience?.ReactToEvent(.4f); }
            if (signal == "left-behind.safe-place" && wallMessage) wallMessage.SetActive(true);
            if (signal == "left-behind.wall-message")
            {
                if (wallMessage) wallMessage.SetActive(false);
                if (ghostGuide) ghostGuide.SetActive(true);
                if (destinationDoor) destinationDoor.requiredKey = "old_key";
            }
            if (signal == "left-behind.locked-area-opened") { if (ghostGuide) ghostGuide.SetActive(false); RestoreLights(); }
        }
        public void DestinationOpened()
        {
            if (CanAct && director.NextSignal == "left-behind.locked-area-opened" && destinationDoor && destinationDoor.IsOpen)
                director.Signal("left-behind.locked-area-opened");
        }
        void HideDemon() { if (demonVisual) demonVisual.gameObject.SetActive(false); }
        void RestoreLights() { foreach (var light in eventLights) if (light) light.enabled = false; lighting = false; }
        public override string CaptureState() => JsonUtility.ToJson(state);
        public override void RestoreState(string json)
        {
            var restored = JsonUtility.FromJson<Data>(json);
            if (restored != null && restored.version == 1) state = restored;
            // GameSession emits Initialized only after every persistent object was restored.
        }
        void OnDisable()
        {
            if (subscribed) { subscribed.Initialized -= RestoreWorldSilently; subscribed.SignalAccepted -= Accepted; }
            subscribed = null; RestoreLights();
        }
    }
}
