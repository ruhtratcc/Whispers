// Verifica SetUp, TearDown, EntireOpeningUsesPhysicalInteractionsAndEndsAtUnimplementedChapterBoundary, EntryAndPhotographWaitDuringPauseThenContinueOnce, ObservationRequiresClearSightAndLookingBackNearTheBoard, RestoreIsSilentAndDoesNotOverwriteUnrelatedDoorState, CollectedDrawingCheckpointResumesOnlyCaptionAndPausedBlackoutTimer, PairedDoorSilentStoryPoseKeepsClosedBasisAndNoPlayerEvents.

using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("SchoolOpening")]
    public sealed class SchoolOpeningFlowTests
    {
        Scene scene;
        GameObject root;
        StoryBook book;
        GameSession game;
        NarrativeDirector director;
        Inventory inventory;
        SchoolOpeningFlow flow;
        PlayerInteraction interaction;
        InteractiveDoor door, partner, unrelatedDoor;
        int entered, slams, ghosts, blackouts;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            scene = SceneManager.CreateScene("School-opening-isolated-" + Guid.NewGuid());
            SceneManager.SetActiveScene(scene);
            foreach (var old in UnityEngine.Object.FindObjectsByType<GameSession>()) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            Time.timeScale = 1;
            AudioListener.pause = false;
            root = new GameObject("School opening isolated fixture");
            root.SetActive(false);
            game = root.AddComponent<GameSession>();
            game.enabled = false; // No Start, checkpoint write, prefs or live scene load.
            inventory = root.AddComponent<Inventory>();
            director = root.AddComponent<NarrativeDirector>();
            book = ScriptableObject.CreateInstance<StoryBook>();
            StoryContent.Populate(book);
            foreach (var sequence in book.sequences)
                foreach (var line in sequence.lines) line.seconds = .12f;
            director.book = book;
            director.startingChapterId = "school";
            director.game = game;
            director.inventory = inventory;
            game.narrative = director;
            var player = Child("Player", Vector3.back * 8);
            var cameraObject = new GameObject("Player camera");
            cameraObject.transform.SetParent(player.transform);
            cameraObject.transform.localPosition = Vector3.up * 1.5f;
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            interaction = player.AddComponent<PlayerInteraction>();
            interaction.view = camera;
            interaction.inventory = inventory;
            game.interaction = interaction;
            door = Door("Entry left", "fixture.entry.left", new Vector3(-1, 0, 0));
            partner = Door("Entry right", "fixture.entry.right", new Vector3(1, 0, 0));
            door.pairedLeaf = partner;
            partner.pairedLeaf = door;
            unrelatedDoor = Door("Unrelated classroom", "fixture.classroom", new Vector3(-6, 0, 7));
            flow = root.AddComponent<SchoolOpeningFlow>();
            flow.director = director;
            flow.player = player.transform;
            flow.playerCamera = camera;
            flow.entryDoor = door;
            flow.entryVolume = Child("Entry trigger", new Vector3(0, 1, 2)).AddComponent<BoxCollider>();
            flow.entryVolume.isTrigger = true;
            flow.entryVolume.size = new Vector3(5, 4, 2);
            flow.boardAnchor = Child("Blackboard target", new Vector3(4, 1.5f, 7)).transform;
            flow.corridorLookAnchor = Child("Corridor target", new Vector3(4, 1.5f, 3)).transform;
            flow.ghostAnchor = Child("Child target", new Vector3(4, 1.35f, 12)).transform;
            flow.boardMessage = Child("Blackboard lettering", flow.boardAnchor.position);
            flow.ghostVisual = Child("Child visual", flow.ghostAnchor.position);
            flow.documentsClue = Clue("Documents", SchoolOpeningClueKind.Documents, new Vector3(-3, 1, 4));
            flow.photographClue = Clue("Photograph", SchoolOpeningClueKind.Photograph, new Vector3(-3, 1, 7));
            flow.drawingClue = Clue("Drawing", SchoolOpeningClueKind.Drawing, new Vector3(4, .15f, 12));
            var light = Child("School light", new Vector3(0, 3, 3)).AddComponent<Light>();
            flow.schoolLights = new[] { light };
            flow.blackoutDelay = .15f;
            entered = slams = ghosts = blackouts = 0;
            flow.onEntered.AddListener(() => entered++);
            flow.onCorridorSlam.AddListener(() => slams++);
            flow.onGhostDisappeared.AddListener(() => ghosts++);
            flow.onBlackout.AddListener(() => blackouts++);
            root.SetActive(true);
            director.InitializeAfterSession(false);
            Physics.SyncTransforms();
            yield return null;
        }

        GameObject Child(string name, Vector3 position)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform);
            child.transform.position = position;
            return child;
        }
        InteractiveDoor Door(string name, string id, Vector3 position)
        {
            var gameObject = Child(name, position);
            var hinge = new GameObject("Hinge");
            hinge.transform.SetParent(gameObject.transform, false);
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.transform.SetParent(hinge.transform, false);
            leaf.transform.localPosition = new Vector3(.5f, 1, 0);
            leaf.transform.localScale = new Vector3(1, 2, .06f);
            var result = gameObject.AddComponent<InteractiveDoor>();
            result.persistentId = id;
            result.hinge = hinge.transform;
            result.leafCollider = leaf.GetComponent<BoxCollider>();
            result.requiredKey = "";
            result.speed = 1000;
            result.lockedMessage = "A porta não abre.";
            return result;
        }
        SchoolOpeningClue Clue(string title, SchoolOpeningClueKind kind, Vector3 position)
        {
            var gameObject = Child(title, position);
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(.3f, .2f, .3f);
            var result = gameObject.AddComponent<SchoolOpeningClue>();
            result.flow = flow;
            result.kind = kind;
            result.title = title;
            result.body = kind == SchoolOpeningClueKind.Drawing ? "NÃO SUBA." : "Documento de investigação.";
            return result;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            if (root) UnityEngine.Object.Destroy(root);
            if (book) UnityEngine.Object.Destroy(book);
            yield return null;
            var blank = SceneManager.GetSceneByName("School-opening-test-cleanup");
            if (!blank.IsValid() || !blank.isLoaded) blank = SceneManager.CreateScene("School-opening-test-cleanup");
            SceneManager.SetActiveScene(blank);
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        IEnumerator Until(Func<bool> predicate, float timeout = 3)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "School opening condition timed out.");
        }
        void Aim(Transform target, Vector3 direction, float distance = 1.8f)
        {
            var camera = flow.playerCamera.transform;
            Vector3 origin = target.position - direction.normalized * distance;
            flow.player.position = origin - Vector3.up * 1.5f;
            camera.localPosition = Vector3.up * 1.5f;
            camera.LookAt(target);
            Physics.SyncTransforms();
        }
        IEnumerator ReadUsingPlayerRaycast(SchoolOpeningClue clue)
        {
            Aim(clue.transform, Vector3.forward);
            yield return null;
            Assert.That(interaction.Target, Is.SameAs(clue), "The real interaction ray must hit the gated clue.");
            interaction.Target.Interact(interaction);
        }
        IEnumerator EnterAndVerify()
        {
            Assert.That(door.IsOpen && partner.IsOpen, Is.True);
            flow.player.position = new Vector3(0, 0, 2);
            Physics.SyncTransforms();
            yield return Until(() => director.HasSignal("school.entered"));
            Assert.That(entered, Is.EqualTo(1));
            Assert.That(door.locked && partner.locked, Is.True);
            Assert.That(door.IsOpen || partner.IsOpen, Is.False);
            partner.Interact(interaction); // Either real leaf must accept the lock-check milestone.
            Assert.That(director.HasSignal("school.entry-locked"), Is.True);
        }
        IEnumerator ReachGhost()
        {
            yield return EnterAndVerify();
            yield return ReadUsingPlayerRaycast(flow.documentsClue);
            yield return ReadUsingPlayerRaycast(flow.photographClue);
            yield return Until(() => director.HasSequence("school.photo-inscription") && game.State == GameState.Playing);
            Assert.That(flow.boardMessage.activeSelf, Is.True);
            Aim(flow.boardAnchor, Vector3.forward, 3);
            yield return Until(() => director.HasSequence("school.blackboard-inscription") && game.State == GameState.Playing);
            Aim(flow.corridorLookAnchor, Vector3.back);
            yield return Until(() => director.HasSignal("school.blackboard-vanished"));
            Assert.That(slams, Is.EqualTo(1));
            Assert.That(flow.boardMessage.activeSelf, Is.False);
            Assert.That(flow.ghostVisual.activeSelf, Is.True);
            Assert.That(flow.drawingClue.gameObject.activeSelf, Is.False);
            Aim(flow.ghostAnchor, Vector3.forward, 1);
            yield return Until(() => director.HasSignal("school.ghost-approached"));
        }

        [UnityTest]
        public IEnumerator EntireOpeningUsesPhysicalInteractionsAndEndsAtUnimplementedChapterBoundary()
        {
            Assert.That(flow.CanReadClue(SchoolOpeningClueKind.Photograph), Is.False);
            Assert.That(flow.drawingClue.CanInteract(interaction), Is.False);
            Assert.That(director.Signal("school.ghost-approached"), Is.False);
            yield return ReachGhost();
            Assert.That(ghosts, Is.EqualTo(1));
            Assert.That(flow.ghostVisual.activeSelf, Is.False);
            Assert.That(flow.drawingClue.CanInteract(interaction), Is.True);
            yield return ReadUsingPlayerRaycast(flow.drawingClue);
            Assert.That(inventory.Contains("school_drawing"), Is.True);
            Assert.That(flow.drawingClue.gameObject.activeSelf, Is.False);
            yield return Until(() => game.State == GameState.StoryScreen);
            Assert.That(director.Progress.milestones.Count, Is.EqualTo(9));
            Assert.That(blackouts, Is.EqualTo(1));
            Assert.That(flow.schoolLights[0].enabled, Is.False);
            Assert.That(director.Progress.chapterId, Is.EqualTo("school"));
            Assert.That(director.Progress.pendingChapterId, Is.EqualTo("left-behind"));
            Assert.That(book.Chapter("left-behind").sceneName, Is.Empty);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
            Assert.That(game.CanControl, Is.False);
            Assert.That(door.locked && partner.locked, Is.True);
        }

        [UnityTest]
        public IEnumerator EntryAndPhotographWaitDuringPauseThenContinueOnce()
        {
            flow.player.position = new Vector3(0, 0, 2);
            game.Pause();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(director.HasSignal("school.entered"), Is.False);
            Assert.That(flow.EnterSchool(), Is.False);
            game.Resume();
            yield return Until(() => director.HasSignal("school.entered"));
            door.Interact(interaction);
            yield return ReadUsingPlayerRaycast(flow.documentsClue);
            book.Sequence("school.photo-inscription").lines[0].seconds = .4f;
            yield return ReadUsingPlayerRaycast(flow.photographClue);
            yield return Until(() => game.State == GameState.Cutscene);
            game.Pause();
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(director.HasSignal("school.photograph"), Is.False);
            Assert.That(flow.ReadBlackboard(), Is.False);
            game.Resume();
            yield return Until(() => director.HasSequence("school.photo-inscription") && game.State == GameState.Playing);
            Assert.That(entered, Is.EqualTo(1));
            Assert.That(slams, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ObservationRequiresClearSightAndLookingBackNearTheBoard()
        {
            RestoreThrough("school.photograph");
            Aim(flow.boardAnchor, Vector3.forward, 3);
            var blocker = Child("Opaque intervening wall", flow.playerCamera.transform.position + Vector3.forward);
            var collider = blocker.AddComponent<BoxCollider>();
            collider.size = new Vector3(3, 3, .2f);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.2f);
            Assert.That(flow.ReadBlackboard(), Is.False);
            Assert.That(director.HasSignal("school.blackboard-seen"), Is.False);
            UnityEngine.Object.Destroy(blocker);
            yield return Until(() => director.HasSequence("school.blackboard-inscription") && game.State == GameState.Playing);
            Aim(flow.corridorLookAnchor, Vector3.back, 12);
            // The target is within the cone, but the player has left the board room.
            flow.boardReturnDistance = 3;
            Assert.That(flow.LookBackToCorridor(), Is.False);
            flow.boardReturnDistance = 9;
            Aim(flow.corridorLookAnchor, Vector3.back);
            yield return Until(() => director.HasSignal("school.blackboard-vanished"));
            Assert.That(slams, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RestoreIsSilentAndDoesNotOverwriteUnrelatedDoorState()
        {
            unrelatedDoor.SetLocked(true);
            unrelatedDoor.SetOpen(true, true);
            string unrelatedState = unrelatedDoor.CaptureState();
            RestoreThrough("school.blackboard-vanished");
            yield return null;
            Assert.That(entered + slams + ghosts + blackouts, Is.Zero);
            Assert.That(door.IsOpen || partner.IsOpen, Is.False);
            Assert.That(door.locked && partner.locked, Is.True);
            Assert.That(unrelatedDoor.CaptureState(), Is.EqualTo(unrelatedState));
            Assert.That(flow.boardMessage.activeSelf, Is.False);
            Assert.That(flow.ghostVisual.activeSelf, Is.True);
            Assert.That(flow.drawingClue.gameObject.activeSelf, Is.False);
            var snapshot = director.CaptureProgress();
            flow.enabled = false;
            flow.enabled = true;
            director.RestoreProgress(snapshot);
            director.InitializeAfterSession(true);
            Aim(flow.ghostAnchor, Vector3.forward, 1);
            yield return Until(() => director.HasSignal("school.ghost-approached"));
            Assert.That(ghosts, Is.EqualTo(1));
            Assert.That(entered + slams + blackouts, Is.Zero);
            Assert.That(flow.ApproachGhost(), Is.False);
        }

        [UnityTest]
        public IEnumerator CollectedDrawingCheckpointResumesOnlyCaptionAndPausedBlackoutTimer()
        {
            flow.blackoutDelay = .5f;
            inventory.Add(new InventoryItem { id = "school_drawing", title = "Desenho", text = "NÃO SUBA.", note = true });
            RestoreThrough("school.ghost-approached");
            Assert.That(flow.drawingClue.CanInteract(interaction), Is.False);
            yield return Until(() => director.HasSequence("school.drawing-inscription") && game.State == GameState.Playing);
            game.Pause();
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(director.HasSignal("school.blackout"), Is.False);
            Assert.That(entered + slams + ghosts + blackouts, Is.Zero);
            game.Resume();
            yield return Until(() => game.State == GameState.StoryScreen);
            Assert.That(blackouts, Is.EqualTo(1));
            Assert.That(entered + slams + ghosts, Is.Zero);
            var snapshot = director.CaptureProgress();
            director.RestoreProgress(snapshot);
            director.InitializeAfterSession(true);
            yield return null;
            Assert.That(blackouts, Is.EqualTo(1));
            Assert.That(flow.schoolLights[0].enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator PairedDoorSilentStoryPoseKeepsClosedBasisAndNoPlayerEvents()
        {
            int openEvents = 0, closeEvents = 0, unlockEvents = 0;
            door.onOpen.AddListener(() => openEvents++);
            door.onClose.AddListener(() => closeEvents++);
            door.onUnlock.AddListener(() => unlockEvents++);
            partner.SetLocked(true);
            partner.SetOpen(false, true);
            Assert.That(Quaternion.Angle(door.hinge.localRotation, Quaternion.identity), Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(partner.hinge.localRotation, Quaternion.identity), Is.LessThan(.01f));
            partner.SetOpen(true, true);
            Assert.That(Quaternion.Angle(door.hinge.localRotation, Quaternion.Euler(0, door.openAngle, 0)), Is.LessThan(.01f));
            Assert.That(door.locked && partner.locked, Is.True);
            Assert.That(openEvents + closeEvents + unlockEvents, Is.Zero);
            partner.SetOpen(false);
            yield return new WaitForSeconds(.2f);
            Assert.That(door.IsOpen || partner.IsOpen, Is.False);
            Assert.That(Quaternion.Angle(door.hinge.localRotation, Quaternion.identity), Is.LessThan(.01f));
            Assert.That(closeEvents, Is.EqualTo(1));
            Assert.That(unlockEvents, Is.Zero);
        }

        void RestoreThrough(string finalSignal)
        {
            var progress = new NarrativeProgress { chapterId = "school" };
            foreach (var milestone in book.Chapter("school").milestones)
            {
                Assert.That(progress.TryComplete(book, milestone.id, inventory.Contains), Is.True);
                foreach (var sequence in book.sequences)
                    if (sequence.completionSignal == milestone.id) progress.CompleteSequence(sequence.id);
                if (milestone.id == finalSignal) break;
            }
            director.RestoreProgress(progress);
            director.InitializeAfterSession(true);
        }
    }
}
