// Ferramenta da escola de validação.

using System;
using System.Collections.Generic;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SchoolCEEP.Editor
{
    /// <summary>Editor-only geometry proof. No game session, save, preference or narrative event is executed.</summary>
    public static class SchoolValidationPass
    {
        [Serializable]
        public sealed class WalkProbe
        {
            public string name;
            public Vector3 footPosition;
        }

        [Serializable]
        public sealed class TraversalPath
        {
            public string name;
            public Vector3[] footPositions = Array.Empty<Vector3>();
            public bool isStairRoute;
        }

        [Serializable]
        public sealed class Check
        {
            public string name, detail;
            public bool passed;
            public Vector3 position;
        }

        [Serializable]
        public sealed class ValidationReport
        {
            public bool passed, editorPhysicsOnly = true, gameplayExecuted, checkpointTouched;
            public string scene, chapterId, schoolScene, prologueScene, roadScene, newGameScene;
            public int gameplayRoots, players, narrativeDirectors, saveSystems, prologueFlows;
            public int persistentObjects, doors, probes, paths, stairRoutes, missingScripts;
            public bool temporaryDoorPosesRestored, temporaryGateStateRestored;
            public Vector3 spawn;
            public List<Check> checks = new List<Check>();
            public List<string> errors = new List<string>();
            public List<string> notes = new List<string>();
        }

        public static ValidationReport Validate(Scene scene, Vector3 expectedSpawn,
            WalkProbe[] probes = null, TraversalPath[] traversals = null)
        {
            var report = new ValidationReport { scene = scene.path, spawn = expectedSpawn };
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return Fail(report, "Run school geometry validation outside Play mode.");
            if (!scene.IsValid() || !scene.isLoaded || scene.path != SchoolIntegrationPass.ScenePath)
                return Fail(report, "Load the saved SchoolCEEP scene before validation.");

            var roots = scene.GetRootGameObjects();
            var sessions = Components<GameSession>(roots);
            var players = Components<FirstPersonMotor>(roots);
            var directors = Components<NarrativeDirector>(roots);
            var saves = Components<SaveSystem>(roots);
            report.gameplayRoots = roots.Count(root => root.GetComponent<GameSession>());
            report.players = players.Length;
            report.narrativeDirectors = directors.Length;
            report.saveSystems = saves.Length;
            report.prologueFlows = Components<PrologueFlow>(roots).Length;
            report.missingScripts = Components<Transform>(roots)
                .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
            Add(report, "Single gameplay root", sessions.Length == 1 && report.gameplayRoots == 1
                && players.Length == 1 && directors.Length == 1 && saves.Length == 1 && report.prologueFlows == 0,
                "Exactly one GameSession/player/director/save and no PrologueFlow.");
            Add(report, "No missing scripts", report.missingScripts == 0, report.missingScripts + " missing scripts.");

            var persistent = Components<PersistentObject>(roots);
            report.persistentObjects = persistent.Length;
            var duplicateIds = persistent.GroupBy(item => item.persistentId ?? "")
                .Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)
                .Select(group => string.IsNullOrWhiteSpace(group.Key) ? "<missing>" : group.Key).ToArray();
            Add(report, "Persistent IDs", duplicateIds.Length == 0,
                duplicateIds.Length == 0 ? "All existing IDs are unique." : string.Join(", ", duplicateIds));
            Add(report, "Build scene", EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == scene.path), scene.path);
            if (sessions.Length != 1 || players.Length != 1 || directors.Length != 1 || saves.Length != 1)
                return Finish(report);

            var game = sessions[0];
            var player = players[0];
            var director = directors[0];
            var save = saves[0];
            var controller = player.GetComponent<CharacterController>();
            var flashlight = player.GetComponent<FlashlightController>();
            var book = director.book;
            report.chapterId = director.startingChapterId;
            report.schoolScene = book ? book.Chapter("school")?.sceneName : "";
            report.prologueScene = book ? book.Chapter("prologue")?.sceneName : "";
            report.roadScene = book ? book.Chapter("road")?.sceneName : "";
            report.newGameScene = game.scenes ? game.scenes.gameplayScene : "";
            Add(report, "School binding", book && book.ChapterIndex("school") == 2
                && director.startingChapterId == "school" && report.schoolScene == SchoolIntegrationPass.SceneName,
                "School starts at chapter school, index 2, and binds to SchoolCEEP.");
            Add(report, "Prologue and road preserved", report.prologueScene == "Chapter1Office"
                && report.newGameScene == "Chapter1Office" && string.IsNullOrEmpty(report.roadScene),
                "New game/prologue remain Chapter1Office; road map remains unavailable.");
            Add(report, "Gameplay references", game.player == player && game.save == save
                && game.narrative == director && save.narrative == director && director.game == game
                && game.input && game.interaction && game.interaction.inventory && game.hud && game.atmosphere
                && save.player == player && save.inventory == game.interaction.inventory
                && director.inventory == save.inventory && save.flashlight == flashlight
                && player.view && player.view.playerCamera && director.transitionView
                && game.scenes && game.scenes.storyBook == book,
                "Current player, inventory, camera, HUD, save and narrative reference the same root.");
            Add(report, "Flashlight starts off", flashlight && flashlight.lamp && !flashlight.startsOn
                && !flashlight.lamp.enabled && Mathf.Approximately(flashlight.lamp.intensity, 90),
                "Initial state off; inherited light intensity 90.");
            Add(report, "Spawn transform", Vector3.Distance(player.transform.position, expectedSpawn) < .02f,
                "Player matches the builder's spawn.", player.transform.position);
            if (!controller) return Fail(report, "Player has no CharacterController.");

            var doors = Components<InteractiveDoor>(roots);
            report.doors = doors.Length;
            var hinges = new List<Transform>();
            var rotations = new List<Quaternion>();
            var gate = Components<InteractiveDrawer>(roots).SingleOrDefault(item => item.persistentId == SchoolWorkshopPhotoPass.GateId);
            var gateFields = typeof(InteractiveDrawer).GetFields(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);
            var gateValues = gate ? gateFields.Select(field => field.GetValue(gate)).ToArray() : null;
            Vector3 gatePose = gate && gate.drawer ? gate.drawer.localPosition : Vector3.zero;
            GameObject temporary = null;
            try
            {
                Physics.SyncTransforms();
                CheckStanding(report, "Spawn support and standing clearance", scene,
                    expectedSpawn, controller, player.transform, null, true);

                // Check the usable map with its interaction doors fully open, restoring their exact Editor poses.
                foreach (var door in doors)
                {
                    bool references = door.hinge && door.leafCollider
                        && door.hinge.gameObject.scene == scene && door.leafCollider.gameObject.scene == scene;
                    Add(report, "Door references: " + door.name, references, "Hinge and physical leaf belong to SchoolCEEP.");
                    Add(report, "Door usable: " + door.name, !door.locked, "Geometric blockout doors must not require an unavailable key.");
                    if (!references) continue;
                    hinges.Add(door.hinge); rotations.Add(door.hinge.localRotation);
                    door.hinge.localRotation = rotations[rotations.Count - 1] * Quaternion.Euler(0, door.openAngle, 0);
                }
                if (gate && gate.drawer)
                {
                    if (!gate.IsOpen) gate.Interact(null);
                    gate.RestoreState(gate.CaptureState());
                }
                Physics.SyncTransforms();

                temporary = new GameObject("School validation capsule") { hideFlags = HideFlags.HideAndDontSave };
                temporary.layer = player.gameObject.layer;
                SceneManager.MoveGameObjectToScene(temporary, scene);
                var walker = temporary.AddComponent<CharacterController>();
                walker.height = controller.height; walker.radius = controller.radius;
                walker.center = controller.center; walker.skinWidth = controller.skinWidth;
                walker.stepOffset = controller.stepOffset; walker.slopeLimit = controller.slopeLimit;
                walker.minMoveDistance = 0;

                // Closed-door aperture positions are reconstructed from each hinge's saved rotation.
                for (int i = 0; i < doors.Length; i++)
                {
                    var door = doors[i];
                    if (!door.hinge || !door.leafCollider) continue;
                    var partner=door.pairedLeaf&&door.pairedLeaf.pairedLeaf==door?door.pairedLeaf:null;
                    if(partner&&string.CompareOrdinal(door.persistentId,partner.persistentId)>0)continue;
                    int savedIndex = hinges.IndexOf(door.hinge);
                    if (savedIndex < 0) continue;
                    Quaternion openPose = door.hinge.localRotation;
                    door.hinge.localRotation = rotations[savedIndex];
                    Physics.SyncTransforms();
                    var leaf = door.leafCollider;
                    Vector3 scale = leaf.transform.lossyScale;
                    float width = Mathf.Abs(leaf.size.x * scale.x);
                    float height = Mathf.Abs(leaf.size.y * scale.y);
                    Vector3 center = leaf.transform.TransformPoint(leaf.center);
                    Vector3 normal = leaf.transform.forward;
                    normal.y = 0; normal.Normalize();
                    if(partner&&partner.leafCollider&&partner.hinge) {
                        int otherIndex=hinges.IndexOf(partner.hinge);
                        if(otherIndex<0)throw new InvalidOperationException("Missing paired door pose.");
                        var partnerOpen=partner.hinge.localRotation;
                        partner.hinge.localRotation=rotations[otherIndex];Physics.SyncTransforms();
                        var other=partner.leafCollider;
                        width+=Mathf.Abs(other.size.x*other.transform.lossyScale.x);
                        height=Mathf.Min(height,Mathf.Abs(other.size.y*other.transform.lossyScale.y));
                        center=(center+other.transform.TransformPoint(other.center))*.5f;
                        partner.hinge.localRotation=partnerOpen;
                    }
                    Vector3 threshold = center - Vector3.up * (height * .5f - .05f);
                    door.hinge.localRotation = openPose;
                    Physics.SyncTransforms();
                    Add(report, "Door size: " + door.name, width >= controller.radius * 2 + .12f
                        && height >= controller.height + .08f,
                        (partner?"Double opening":"Leaf")+" width/height " + width.ToString("F2") + "/" + height.ToString("F2") + " m; opening is checked physically next.", threshold);
                    CheckStanding(report, "Open door aperture: " + door.name, scene, threshold,
                        controller, player.transform, temporary.transform, false);
                    var route = new[] { threshold - normal * .6f, threshold, threshold + normal * .6f };
                    CheckTraversal(report, "Door crossing: " + door.name, route, false,
                        scene, controller, walker, player.transform);
                }

                foreach (var probe in probes ?? Array.Empty<WalkProbe>())
                {
                    if (probe == null) { Add(report, "Walk probe", false, "Null walk probe."); continue; }
                    report.probes++;
                    CheckStanding(report, probe.name, scene, probe.footPosition, controller,
                        player.transform, temporary.transform, false);
                }
                foreach (var path in traversals ?? Array.Empty<TraversalPath>())
                {
                    if (path == null) { Add(report, "Traversal path", false, "Null traversal path."); continue; }
                    report.paths++;
                    if (path.isStairRoute) report.stairRoutes++;
                    CheckTraversal(report, path.name, path.footPositions, path.isStairRoute,
                        scene, controller, walker, player.transform);
                }
                Add(report, "School walk probes supplied", report.probes > 0, report.probes + " explicit floor/circulation probes.");
                Add(report, "Stair route supplied", report.stairRoutes > 0, report.stairRoutes + " explicit stairs checked in both directions.");
            }
            catch (Exception error)
            {
                report.errors.Add("Geometry verification exception: " + error);
            }
            finally
            {
                if (temporary) UnityEngine.Object.DestroyImmediate(temporary);
                for (int i = 0; i < hinges.Count; i++)
                    if (hinges[i]) hinges[i].localRotation = rotations[i];
                if (gate && gate.drawer)
                {
                    for (int i = 0; i < gateFields.Length; i++) gateFields[i].SetValue(gate, gateValues[i]);
                    gate.drawer.localPosition = gatePose;
                }
                Physics.SyncTransforms();
                report.temporaryDoorPosesRestored = hinges.Select((hinge, index) =>
                    hinge && Quaternion.Angle(hinge.localRotation, rotations[index]) < .001f).All(value => value);
                if (!report.temporaryDoorPosesRestored) report.errors.Add("A temporary door pose was not restored.");
                report.temporaryGateStateRestored = !gate || (gate.drawer && Vector3.Distance(gate.drawer.localPosition, gatePose) < .001f
                    && gateFields.Select((field, index) => Equals(field.GetValue(gate), gateValues[index])).All(value => value));
                if (!report.temporaryGateStateRestored) report.errors.Add("The temporary workshop gate state was not restored.");
            }
            report.notes.Add("CharacterController.Move ran on a temporary bare capsule in EditMode; no gameplay script or checkpoint ran.");
            report.notes.Add("Open-door and open-gate geometry was checked; exact original hinge poses, gate pose and gate state were restored. This does not verify interaction timing or audio.");
            return Finish(report);
        }

        static void CheckTraversal(ValidationReport report, string name, Vector3[] points, bool stairs,
            Scene scene, CharacterController dimensions, CharacterController walker, Transform player)
        {
            if (points == null || points.Length < 2)
            { Add(report, name, false, "Traversal requires at least two foot-position waypoints."); return; }
            if (stairs) Add(report, name + " elevation", Mathf.Abs(points[points.Length - 1].y - points[0].y) > .10f,
                "Stair route connects distinct elevations, including the photographed local grade changes.");

            bool samplesPass = true;
            for (int segment = 0; segment < points.Length - 1; segment++)
            {
                int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(points[segment], points[segment + 1]) / .2f));
                for (int sample = 0; sample <= samples; sample++)
                {
                    Vector3 foot = Vector3.Lerp(points[segment], points[segment + 1], sample / (float)samples);
                    if (!Standing(scene, foot, dimensions, player, walker.transform, out _, out string detail, true))
                    {
                        Add(report, name + " sampled clearance", false, detail, foot);
                        samplesPass = false;
                        break;
                    }
                }
                if (!samplesPass) break;
            }
            if (samplesPass) Add(report, name + " sampled clearance", true,
                "Support and standing capsule checked every 0.2 m; low risers within stepOffset are allowed and tested by actual traversal.");
            bool forward = Walk(scene, points, dimensions, walker, player, out string forwardDetail, out Vector3 forwardEnd);
            Add(report, name + " forward traversal", forward, forwardDetail, forwardEnd);
            bool reverse = Walk(scene, points.Reverse().ToArray(), dimensions, walker, player, out string reverseDetail, out Vector3 reverseEnd);
            Add(report, name + " reverse traversal", reverse, reverseDetail, reverseEnd);
        }

        static bool Walk(Scene scene, Vector3[] points, CharacterController dimensions,
            CharacterController walker, Transform player, out string detail, out Vector3 endpoint)
        {
            endpoint = points[0];
            if (!Standing(scene, points[0], dimensions, player, walker.transform, out Vector3 start, out detail, true)) return false;
            walker.enabled = false; walker.transform.position = start; walker.enabled = true;
            Physics.SyncTransforms();
            for (int index = 1; index < points.Length; index++)
            {
                Vector3 target = points[index];
                int budget = Mathf.CeilToInt(Vector3.Distance(walker.transform.position, target) / .06f) + 90;
                int stalled = 0;
                bool reached = false;
                for (int attempt = 0; attempt < budget; attempt++)
                {
                    Vector3 horizontal = target - walker.transform.position; horizontal.y = 0;
                    if (horizontal.magnitude < .10f && Mathf.Abs(walker.transform.position.y - target.y) < .24f)
                    { reached = true; break; }
                    Vector3 before = walker.transform.position;
                    walker.Move(Vector3.ClampMagnitude(horizontal, .07f) + Vector3.down * .055f);
                    float change = Vector3.Distance(before, walker.transform.position);
                    stalled = change < .003f ? stalled + 1 : 0;
                    if (stalled >= 12) break;
                }
                endpoint = walker.transform.position;
                if (!reached)
                { detail = "Bare CharacterController could not reach waypoint " + index + " (" + target + "); stopped at " + endpoint + "."; return false; }
            }
            detail = "Actual CharacterController.Move reached all waypoints using existing capsule/step/slope settings.";
            return true;
        }

        static void CheckStanding(ValidationReport report, string name, Scene scene, Vector3 foot,
            CharacterController dimensions, Transform player, Transform walker, bool exactSpawn)
        {
            bool okay = Standing(scene, foot, dimensions, player, walker, out Vector3 grounded, out string detail);
            if (okay && exactSpawn && Mathf.Abs(grounded.y - foot.y) > .12f)
            { okay = false; detail = "Spawn foot is more than 0.12 m above/below its supported capsule position."; }
            Add(report, name, okay, detail, foot);
        }

        static bool Standing(Scene scene, Vector3 foot, CharacterController dimensions, Transform player,
            Transform walker, out Vector3 grounded, out string detail, bool allowStepGeometry = false)
        {
            grounded = foot;
            if (!Finite(foot.x) || !Finite(foot.y) || !Finite(foot.z))
            { detail = "Nonfinite foot position."; return false; }
            var hits = Physics.RaycastAll(foot + Vector3.up * .45f, Vector3.down, 1.1f, ~0, QueryTriggerInteraction.Ignore)
                .Where(hit => Relevant(hit.collider, scene, player, walker) && hit.normal.y > .25f
                    && Mathf.Abs(hit.point.y - foot.y) <= .28f).OrderBy(hit => hit.distance).ToArray();
            if (hits.Length == 0) { detail = "No physical floor within 0.28 m of the expected foot elevation."; return false; }
            var floor = hits[0];
            float slope = Vector3.Angle(floor.normal, Vector3.up);
            if (slope > dimensions.slopeLimit + 1f)
            { detail = "Floor slope " + slope.ToString("F1") + " exceeds player slope limit."; return false; }
            float radius = Mathf.Max(.05f, dimensions.radius - .015f);
            float riseForSlope = radius / Mathf.Max(.25f, floor.normal.y) - radius;
            grounded.y = floor.point.y + .045f + riseForSlope;
            Vector3 lower = grounded + Vector3.up * radius;
            Vector3 upper = grounded + Vector3.up * (dimensions.height - radius);
            float allowedStepTop = grounded.y + dimensions.stepOffset + .02f;
            var overlaps = Physics.OverlapCapsule(lower, upper, radius, ~0, QueryTriggerInteraction.Ignore)
                .Where(collider => Relevant(collider, scene, player, walker)
                    && (!allowStepGeometry || collider.bounds.max.y > allowedStepTop)).ToArray();
            if (overlaps.Length > 0)
            { detail = "Standing capsule obstructed by " + string.Join(", ", overlaps.Take(4).Select(collider => collider.name)) + "."; return false; }
            detail = "Supported by " + floor.collider.name + "; standing capsule clear; slope " + slope.ToString("F1") + " degrees.";
            return true;
        }

        static bool Relevant(Collider collider, Scene scene, Transform player, Transform walker) =>
            collider && collider.enabled && collider.gameObject.activeInHierarchy && collider.gameObject.scene == scene
            && (!player || !collider.transform.IsChildOf(player)) && (!walker || !collider.transform.IsChildOf(walker));

        static T[] Components<T>(GameObject[] roots) where T : Component =>
            roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        static void Add(ValidationReport report, string name, bool passed, string detail, Vector3 position = default)
        {
            report.checks.Add(new Check { name = name, passed = passed, detail = detail, position = position });
            if (!passed) report.errors.Add(name + ": " + detail);
        }
        static ValidationReport Fail(ValidationReport report, string detail)
        { report.errors.Add(detail); return Finish(report); }
        static ValidationReport Finish(ValidationReport report)
        { report.passed = report.errors.Count == 0; return report; }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
