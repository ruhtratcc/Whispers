// Coordena a campanha simplificada usada como roteiro anterior.

using System;
using UnityEngine;

namespace HorrorGame
{
    /// <summary>One saved, almost-linear investigation across the existing school rooms.</summary>
    [DefaultExecutionOrder(120), DisallowMultipleComponent]
    public sealed class SchoolSimplifiedFlow : MonoBehaviour
    {
        public NarrativeDirector director;
        public SchoolSanityController sanity;
        public Camera playerCamera;
        public Transform player;
        public BoxCollider libraryVolume, hospicioVolume, upstairsVolume, corridorVolume, downstairsVolume, returnVolume, entranceVolume, outsideVolume;
        public InteractiveDoor libraryDoor, entryDoor, passageGate, stairGate, room113Door;
        public SimplifiedSchoolClue[] clues = Array.Empty<SimplifiedSchoolClue>();
        public Transform fallingBook, bookRestPose, figureLook, loopReturnPose, exitLoopPose;
        public GameObject figureVisual;
        public Light guideLight;
        public SchoolMemoryMirror mirror;
        public SoundSet bookFallSound, distantFootsteps, whisperSound;
        public string officeScene = "Chapter1Office";
        Vector3 bookStart;
        Quaternion bookRotation;
        float bookAge, figureSeenAge, figureAwayAge, nextAmbient;
        bool bookCaptured, loadingOffice;
        InspectionManager listening;
        NarrativeDirector subscribed;

        public bool CanAct => director && director.IsInitialized && director.game && director.game.CanControl
            && !director.SequencePlaying && !director.game.IsBlackingOut;
        void OnEnable()
        {
            subscribed = director;
            if (subscribed) { subscribed.Initialized += RestoreWorldSilently; subscribed.SignalAccepted += Accepted; }
            if (director && director.IsInitialized) RestoreWorldSilently();
        }
        void OnDisable()
        {
            if (subscribed) { subscribed.Initialized -= RestoreWorldSilently; subscribed.SignalAccepted -= Accepted; }
            if (listening) listening.RecordingCompleted -= RecordListening;
            listening = null;
        }
        public void RestoreWorldSilently()
        {
            if (!director || !director.IsInitialized) return;
            if (fallingBook && !bookCaptured) { bookStart = fallingBook.position; bookRotation = fallingBook.rotation; bookCaptured = true; }
            bookAge = figureSeenAge = figureAwayAge = 0;
            if (director.HasSignal("hospicio.figure-seen")) figureSeenAge = .25f;
            loadingOffice = false;
            if (figureVisual) figureVisual.SetActive(false);
            if (fallingBook && bookRestPose)
            {
                fallingBook.SetPositionAndRotation(director.HasSignal("library.book-fell") ? bookRestPose.position : bookStart,
                    director.HasSignal("library.book-fell") ? bookRestPose.rotation : bookRotation);
            }
            bool entered = director.HasSignal("school.entered");
            if (libraryDoor)
            {
                libraryDoor.requiredKey = entered ? "iron_key" : "";
                if (libraryDoor.pairedLeaf) libraryDoor.pairedLeaf.requiredKey = libraryDoor.requiredKey;
                libraryDoor.lockedMessage = "A fechadura antiga travou. A Chave de Ferro deve estar na biblioteca.";
                libraryDoor.SetLocked(entered && !director.HasSignal("library.passage-opened"));
                libraryDoor.SetOpen(!entered || director.HasSignal("library.passage-opened"), true);
            }
            // An older checkpoint may start beyond an already-open 113 door; retain its way out.
            bool preserveRoomEgress = room113Door && room113Door.IsOpen && player
                && player.position.y >= room113Door.transform.position.y - .25f;
            ApplyDoors(preserveRoomEgress);
            ApplyClues();
            if (mirror) mirror.Restore(director.HasSignal("memories.mirror"));
            if (director.MigratedStory && player && director.game.player)
            {
                Vector3 spawn = director.Progress.chapterId == "school" ? new Vector3(41.7f,.03f,55.8f)
                    : director.Progress.chapterId == "left-behind" ? new Vector3(-15.4f,.03f,60.9f)
                    : director.Progress.chapterId == "memories" ? new Vector3(-15.4f,3.84f,61.5f)
                    : new Vector3(-15.4f,3.84f,72);
                director.game.player.Warp(spawn, 0, 0);
                director.game.hud?.Notify("Roteiro atualizado. Seus documentos e progresso foram preservados.");
            }
            nextAmbient = Time.time + 28;
        }
        void ApplyDoors(bool preserveRoomEgress = false)
        {
            if (passageGate) { passageGate.requiredKey = "iron_key"; passageGate.SetLocked(!director.inventory.Contains("iron_key")); }
            bool upstairsReady = director.HasSignal("hospicio.director-tape")
                || director.Progress.chapterId == "memories" || director.Progress.chapterId == "last-exit";
            ApplyUpstairsDoor(stairGate, upstairsReady);
            ApplyUpstairsDoor(room113Door, upstairsReady, preserveRoomEgress);
            bool exiting = director.Progress.chapterId == "last-exit";
            if (entryDoor) { entryDoor.requiredKey = ""; entryDoor.SetLocked(!exiting); if (!exiting) entryDoor.SetOpen(false, true); }
            if (guideLight) guideLight.enabled = exiting;
        }
        void ApplyUpstairsDoor(InteractiveDoor door, bool ready, bool preserveEgress = false)
        {
            if (!door) return;
            door.requiredKey = ready ? "room_113_key" : "";
            door.lockedMessage = ready ? "Preciso da chave da Sala 113."
                : "Antes de subir, preciso conferir os registros e ouvir as fitas deste andar.";
            if (door.pairedLeaf)
            {
                door.pairedLeaf.requiredKey = door.requiredKey;
                door.pairedLeaf.lockedMessage = door.lockedMessage;
            }
            door.SetLocked(!ready || !director.inventory.Contains("room_113_key"));
            // Old checkpoints may contain an open leaf from collecting the key early.
            if (!ready && !preserveEgress) door.SetOpen(false, true);
        }
        void ApplyClues()
        {
            foreach (var clue in clues)
                if (clue) clue.SetVisible(!director.inventory.Contains(clue.itemId)
                    && (!clue.needsFallenBook || director.HasSignal("library.book-fell")));
        }
        public bool CanInvestigate(SimplifiedSchoolClue clue)
        {
            return CanAct && clue && clue.flow == this && director.Progress.chapterId == clue.chapterId
                && !director.inventory.Contains(clue.itemId)
                && (!clue.needsFallenBook || director.HasSignal("library.book-fell"))
                && (clue.itemId != "student_113_record" || director.HasSignal("memories.mirror"))
                && (!clue.needsRecorder || director.inventory.Contains("hospicio_recorder"));
        }
        public void RecordListening(string itemId)
        {
            if (!director || !director.IsInitialized || director.Progress.chapterId != "left-behind"
                || !director.inventory.Contains("hospicio_recorder")) return;
            if (itemId != "hospicio_child_tape" && itemId != "hospicio_director_tape") return;
            director.Progress.CompleteSequence(itemId + ".listened");
            director.ScheduleSave();
        }
        void Update()
        {
            if (listening != InspectionManager.Instance)
            {
                if (listening) listening.RecordingCompleted -= RecordListening;
                listening = InspectionManager.Instance;
                if (listening) listening.RecordingCompleted += RecordListening;
            }
            // Inspection commits the collected item one frame after restoring player control.
            if (!CanAct || (listening && listening.IsCompleting)) return;
            switch (director.NextSignal)
            {
                case "school.entered": if (Inside(libraryVolume)) director.Signal("school.entered"); break;
                case "library.book-fell": AdvanceBook(Time.deltaTime); break;
                case "library.passage-opened":
                    if (libraryDoor && libraryDoor.IsOpen && director.inventory.Contains("iron_key")) director.Signal("library.passage-opened");
                    break;
                case "hospicio.arrived": if (Inside(hospicioVolume)) director.Signal("hospicio.arrived"); break;
                case "hospicio.child-tape": if (director.HasSequence("hospicio_child_tape.listened")) director.Signal("hospicio.child-tape"); break;
                case "hospicio.figure": AdvanceFigure(Time.deltaTime); break;
                case "hospicio.director-tape": if (director.HasSequence("hospicio_director_tape.listened")) director.Signal("hospicio.director-tape"); break;
                case "memories.upstairs": if (Inside(upstairsVolume)) director.Signal("memories.upstairs"); break;
                case "memories.corridor":
                    if (Inside(corridorVolume))
                    {
                        if (loopReturnPose) director.game.player.Warp(loopReturnPose.position, loopReturnPose.eulerAngles.y, 0);
                        director.Signal("memories.corridor");
                    }
                    break;
                case "memories.mirror": if (mirror && mirror.Finished) director.Signal("memories.mirror"); break;
                case "last-exit.stairs": if (Inside(downstairsVolume)) director.Signal("last-exit.stairs"); break;
                case "last-exit.loop":
                    if (Inside(returnVolume))
                    {
                        if (exitLoopPose) director.game.player.Warp(exitLoopPose.position, exitLoopPose.eulerAngles.y, 0);
                        director.Signal("last-exit.loop");
                    }
                    break;
                case "last-exit.entrance-reached":
                    if (Inside(entranceVolume)) { entryDoor?.SetLocked(false); entryDoor?.SetOpen(true); director.Signal("last-exit.entrance-reached"); }
                    break;
                case "last-exit.outside": if (Inside(outsideVolume)) director.Signal("last-exit.outside"); break;
                case "last-exit.awake":
                    if (!loadingOffice && director.HasSignal("last-exit.outside"))
                    {
                        // One attempt for this exit; the chapter handoff also carries the live state in memory.
                        loadingOffice = true;
                        director.SaveCheckpointNow();
                        if (!director.game.LoadStoryChapter(officeScene))
                            director.game.hud?.Notify("Não foi possível abrir o escritório. Seu checkpoint foi preservado.");
                    }
                    break;
            }
            if (CanAct && Time.time >= nextAmbient && director.HasSignal("school.entered"))
            {
                Play(distantFootsteps, player.position + player.right * 7 + Vector3.up * 3, .16f);
                nextAmbient = Time.time + UnityEngine.Random.Range(32, 49);
            }
        }
        void AdvanceBook(float dt)
        {
            if (!Inside(libraryVolume)) return;
            bookAge += dt;
            if (fallingBook && bookRestPose)
            {
                float t = Mathf.Clamp01((bookAge - 1.5f) / .7f);
                fallingBook.SetPositionAndRotation(Vector3.Lerp(bookStart, bookRestPose.position, t * t), Quaternion.Slerp(bookRotation, bookRestPose.rotation, t));
            }
            if (bookAge >= 2.2f) director.Signal("library.book-fell");
        }
        void AdvanceFigure(float dt)
        {
            if (!figureVisual || !figureLook || !player || Vector3.Distance(player.position, figureLook.position) > 22) return;
            figureVisual.SetActive(true);
            if (Observes(figureLook, 22, 28))
            {
                figureSeenAge += dt; figureAwayAge = 0;
                if (figureSeenAge >= .25f && !director.HasSignal("hospicio.figure-seen"))
                {
                    if (director.PlaySequence("hospicio.figure-seen-caption") || director.HasSequence("hospicio.figure-seen-caption"))
                    {
                        // Save at presentation start so a reload during the question cannot repeat it.
                        director.Progress.milestones.Add("hospicio.figure-seen");
                        director.ScheduleSave();
                    }
                }
            }
            else if (figureSeenAge >= .25f) figureAwayAge += dt;
            if (figureAwayAge >= .2f || figureSeenAge >= 2.8f) director.Signal("hospicio.figure");
        }
        void Accepted(string id)
        {
            ApplyDoors(); ApplyClues();
            switch (id)
            {
                case "school.entered":
                    if (libraryDoor)
                    {
                        libraryDoor.SetOpen(false); libraryDoor.requiredKey = "iron_key";
                        if (libraryDoor.pairedLeaf) libraryDoor.pairedLeaf.requiredKey = "iron_key";
                        libraryDoor.SetLocked(true);
                    }
                    break;
                case "library.book-fell":
                    Play(bookFallSound, bookRestPose ? bookRestPose.position : transform.position, .35f);
                    director.PlaySequence("library.book"); break;
                case "hospicio.arrived": director.PlaySequence("hospicio.arrival"); break;
                case "hospicio.figure":
                    if (figureVisual) figureVisual.SetActive(false);
                    sanity?.Disturb(6);
                    director.vintageCamera?.PulseInterference(.12f, .12f);
                    Play(whisperSound, figureLook ? figureLook.position : transform.position, .13f);
                    director.PlaySequence("hospicio.figure-caption"); break;
                case "memories.corridor": director.PlaySequence("memories.corridor-caption"); break;
                case "memories.mirror": sanity?.Disturb(7); director.PlaySequence("memories.mirror-caption"); break;
                case "memories.truth": director.PlaySequence("memories.truth-caption"); break;
                case "last-exit.loop": director.PlaySequence("last-exit.loop-caption"); break;
            }
        }
        public bool Inside(BoxCollider volume)
        {
            if (!volume || !player) return false;
            Vector3 local = volume.transform.InverseTransformPoint(player.position) - volume.center;
            Vector3 half = volume.size * .5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }
        public bool Observes(Transform target, float distance, float angle)
        {
            if (!target || !playerCamera) return false;
            Vector3 delta = target.position - playerCamera.transform.position;
            if (delta.magnitude > distance || Vector3.Angle(playerCamera.transform.forward, delta) > angle) return false;
            if (!Physics.Raycast(playerCamera.transform.position, delta.normalized, out var hit, delta.magnitude, ~0, QueryTriggerInteraction.Ignore)) return true;
            return hit.transform == target || hit.transform.IsChildOf(target) || hit.distance >= delta.magnitude - .15f;
        }
        void Play(SoundSet set, Vector3 position, float gain) => AudioManager.Instance?.Play(set, position, gain, AudioChannel.SFX, true);
    }
}
