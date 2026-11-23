// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HorrorGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace SchoolCEEP.Editor
{
    /// <summary>Pairs adjoining CAD swing spans only. Never groups doors by proximity.</summary>
    public static class SchoolDoorPairPass
    {
        [Serializable] sealed class Layout { public float metersPerPoint; public Level[] levels; }
        [Serializable] sealed class Level { public int index; public Span[] doors, walls; }
        [Serializable] sealed class Span
        {
            public string block, source;
            public bool vertical, provisional;
            public float p, a, b;
            public int openDirection;
        }
        [Serializable] public sealed class Check { public string name, detail; public bool passed; }
        [Serializable] public sealed class Pair
        {
            public string block, firstId, secondId;
            public int floor;
            public float axis, start, seam, end, gapMeters, openingWidthMeters;
            public bool vertical;
            public Vector3 firstHinge, secondHinge;
        }
        [Serializable] public sealed class Report
        {
            public bool passed, editorOnly = true, gameplayExecuted, checkpointTouched;
            public bool doorTargetsExercised, temporaryDoorStateRestored;
            public int doors, pairs, leaves, singleDoors;
            public string scene, detection = "Same floor/block/axis; non-provisional quarter-swing CAD spans share an endpoint within 0.05 PDF points; no intervening wall pier. Native curves place the hinges at the two outer jambs.";
            public List<Pair> openings = new List<Pair>();
            public List<Check> checks = new List<Check>();
            public List<string> errors = new List<string>();
        }
        sealed class Candidate
        {
            public int floor, firstIndex, secondIndex;
            public Span first, second;
        }
        const BindingFlags PrivateFields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        static readonly FieldInfo[] RuntimeFields = typeof(InteractiveDoor).GetFields(PrivateFields).Where(f => !f.IsStatic).ToArray();
        static FieldInfo Field(string name) => RuntimeFields.Single(f => f.Name == name);
        static InteractiveDoor Owner(InteractiveDoor leaf) => (InteractiveDoor)typeof(InteractiveDoor).GetProperty("StateOwner", PrivateFields).GetValue(leaf);
        static string Id(int floor, int index) => "school.door." + floor + "." + index;

        static List<Candidate> Detect(Layout layout)
        {
            var candidates = new List<Candidate>();
            foreach (var level in layout.levels)
            {
                for (int i = 0; i < level.doors.Length; i++)
                for (int j = i + 1; j < level.doors.Length; j++)
                {
                    var a = level.doors[i]; var b = level.doors[j];
                    if (a.provisional || b.provisional || a.source == null || b.source == null
                        || !a.source.StartsWith("quarter-swing CAD curve", StringComparison.Ordinal)
                        || !b.source.StartsWith("quarter-swing CAD curve", StringComparison.Ordinal)
                        || a.block != b.block || a.vertical != b.vertical || Mathf.Abs(a.p - b.p) > .05f) continue;
                    bool aFirst = a.a < b.a;
                    var first = aFirst ? a : b; var second = aFirst ? b : a;
                    if (Mathf.Abs(second.a - first.b) > .05f) continue;
                    // A visible pier separates two independent doors, even on the same wall.
                    if (level.walls.Any(w => w.block == a.block && w.vertical == a.vertical
                        && Mathf.Abs(w.p - a.p) < 3f && w.b - w.a > .12f
                        && w.a < second.a + .05f && w.b > first.b - .05f)) continue;
                    candidates.Add(new Candidate { floor = level.index, firstIndex = aFirst ? i : j,
                        secondIndex = aFirst ? j : i, first = first, second = second });
                }
            }
            var ids = candidates.SelectMany(c => new[] { Id(c.floor, c.firstIndex), Id(c.floor, c.secondIndex) }).ToArray();
            if (ids.Distinct().Count() != ids.Length) throw new InvalidDataException("Ambiguous CAD door pairing; a leaf belongs to more than one opening.");
            return candidates;
        }

        public static Report Apply(Transform architectureRoot, SchoolMapBuilder.Layout authoredLayout = null)
        {
            RequireSchool(architectureRoot);
            var scene = architectureRoot.gameObject.scene;
            var layout = authoredLayout != null ? JsonUtility.FromJson<Layout>(JsonUtility.ToJson(authoredLayout))
                : JsonUtility.FromJson<Layout>(File.ReadAllText(SchoolMapBuilder.Folder + "/Data/school-layout.json"));
            if (layout == null || layout.levels == null || layout.metersPerPoint <= 0) throw new InvalidDataException("Missing school door-span data.");
            var candidates = Detect(layout);
            var doors = architectureRoot.GetComponentsInChildren<InteractiveDoor>(true);
            var byId = doors.ToDictionary(d => d.persistentId);
            // Check all references before changing any leaf.
            foreach (var candidate in candidates)
            foreach (var id in new[] { Id(candidate.floor, candidate.firstIndex), Id(candidate.floor, candidate.secondIndex) })
            {
                if (!byId.TryGetValue(id, out var door) || !door.hinge || door.hinge != door.transform || !door.leafCollider)
                    throw new InvalidOperationException("Double-door leaf requires its original hinge and collider: " + id);
            }
            var report = new Report { scene = scene.path, doors = doors.Length };
            foreach (var candidate in candidates)
            {
                var first = byId[Id(candidate.floor, candidate.firstIndex)];
                var second = byId[Id(candidate.floor, candidate.secondIndex)];
                Vector3 previousCenter = second.leafCollider.transform.TransformPoint(second.leafCollider.center);
                float end = authoredLayout != null || candidate.second.vertical ? candidate.second.b
                    : SchoolMapBuilder.CorridorX(candidate.second.block, candidate.floor, candidate.second.b);
                float axis = authoredLayout == null && candidate.second.vertical ? SchoolMapBuilder.CorridorX(candidate.second.block, candidate.floor, candidate.second.p)
                    : candidate.second.p;
                float finalEnd = authoredLayout != null ? end : SchoolCampusConnectionsPass.Y(candidate.second.block,end);
                float finalAxis = authoredLayout != null ? candidate.second.p : SchoolCampusConnectionsPass.Y(candidate.second.block,candidate.second.p);
                var position = candidate.second.vertical ? SchoolMapBuilder.Point(axis, finalEnd)
                    : SchoolMapBuilder.Point(end, finalAxis);
                position.y = second.transform.position.y;
                second.transform.position = position;
                second.transform.rotation = Quaternion.Euler(0, candidate.second.vertical ? 270 : 180, 0);
                if (candidate.first.openDirection != 0) first.openAngle = candidate.first.openDirection * 95f;
                // This upper entrance opens onto a supported exterior gallery.
                // Swinging into the narrow internal west aisle blocks its south
                // door. Preserve both closed jambs and open onto the gallery.
                if(candidate.floor==1&&candidate.first.block=="main_auditorium_south"
                    &&candidate.first.vertical&&Mathf.Abs(candidate.first.a-851.78f)<.1f)
                    first.openAngle=-first.openAngle;
                second.openAngle = -first.openAngle;
                second.speed = first.speed;
                first.pairedLeaf = second; second.pairedLeaf = first;
                var opening = new Pair { block = candidate.first.block, floor = candidate.floor,
                    firstId = first.persistentId, secondId = second.persistentId, vertical = candidate.first.vertical,
                    axis = candidate.first.p, start = candidate.first.a, seam = (candidate.first.b + candidate.second.a) * .5f,
                    end = candidate.second.b, gapMeters = (candidate.second.a - candidate.first.b) * layout.metersPerPoint,
                    openingWidthMeters = (candidate.second.b - candidate.first.a) * layout.metersPerPoint,
                    firstHinge = first.hinge.position, secondHinge = second.hinge.position };
                report.openings.Add(opening);
                Add(report, first.persistentId + " + " + second.persistentId + " closed leaf center preserved",
                    Vector3.Distance(previousCenter, second.leafCollider.transform.TransformPoint(second.leafCollider.center)) < .005f,
                    "Second hinge moves to the outer jamb; the closed physical leaf remains in its original opening.");
                Add(report, opening.block + " reciprocal pair and opposite hinges",
                    first.pairedLeaf == second && second.pairedLeaf == first
                    && Vector3.Dot(first.transform.right, second.transform.right) < -.999f
                    && Mathf.Abs(first.openAngle + second.openAngle) < .01f
                    && Mathf.Abs(first.hinge.position.y - second.hinge.position.y) < .005f,
                    "Both leaves receive one shared target and rotate around opposite outer jambs.");
                foreach (var leaf in new[] { first, second })
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(leaf);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(leaf.transform);
                    EditorUtility.SetDirty(leaf);
                }
            }
            var pairedIds = new HashSet<string>(report.openings.SelectMany(o => new[] { o.firstId, o.secondId }));
            Add(report, "Independent school doors remain unpaired", doors.Where(d => !pairedIds.Contains(d.persistentId)).All(d => !d.pairedLeaf),
                "Nearby doors separated by wall piers are not synchronized.");
            report.pairs = report.openings.Count; report.leaves = report.pairs * 2; report.singleDoors = report.doors - report.leaves;
            report.passed = report.errors.Count == 0;
            EditorSceneManager.MarkSceneDirty(scene);
            return report;
        }

        /// <summary>Exercises real door interaction targets and JSON restore without Play, movement, audio or existing event listeners.</summary>
        public static Report ValidateInteraction(Transform architectureRoot, Report report = null)
        {
            RequireSchool(architectureRoot);
            var doors = architectureRoot.GetComponentsInChildren<InteractiveDoor>(true);
            if (report == null)
            {
                report = new Report { scene = architectureRoot.gameObject.scene.path, doors = doors.Length };
                foreach (var door in doors.Where(d => d.pairedLeaf && string.CompareOrdinal(d.persistentId, d.pairedLeaf.persistentId) < 0))
                    report.openings.Add(new Pair { firstId = door.persistentId, secondId = door.pairedLeaf.persistentId, block = door.transform.parent.name });
                report.pairs = report.openings.Count; report.leaves = report.pairs * 2; report.singleDoors = doors.Length - report.leaves;
            }
            var snapshots = doors.Select(d => new Snapshot(d)).ToArray();
            GameObject temporary = null;
            try
            {
                temporary = new GameObject("School double-door regression") { hideFlags = HideFlags.HideAndDontSave };
                temporary.SetActive(false);
                SceneManager.MoveGameObjectToScene(temporary, architectureRoot.gameObject.scene);
                var player = temporary.AddComponent<PlayerInteraction>();
                player.inventory = temporary.AddComponent<Inventory>();
                foreach (var opening in report.openings)
                {
                    var a = doors.Single(d => d.persistentId == opening.firstId);
                    var b = doors.Single(d => d.persistentId == opening.secondId);
                    var owner = Owner(a);
                    var partner = owner == a ? b : a;
                    Silence(a); Silence(b);
                    owner.RestoreState("{\"open\":false,\"locked\":false}");
                    a.Interact(null);
                    Add(report, opening.block + " interaction on first leaf opens both targets", Targets(a, b, true), "Both leaves queue movement/completion towards their own openAngle.");
                    b.Interact(null);
                    Add(report, opening.block + " interaction on second leaf closes both targets", Targets(a, b, false), "One interaction closes the entire doorway.");
                    b.Interact(null);
                    Add(report, opening.block + " interaction on second leaf opens both targets", Targets(a, b, true), "The reciprocal link has the same behavior from either leaf.");
                    a.Interact(null);
                    Add(report, opening.block + " interaction on first leaf closes both targets", Targets(a, b, false), "No recursive toggle cancels either leaf's target.");
                    a.locked = b.locked = true; a.requiredKey = "regression.left"; b.requiredKey = "regression.right";
                    player.inventory.items.Clear(); player.inventory.items.Add(new InventoryItem { id = a.requiredKey });
                    a.Interact(player);
                    Add(report, opening.block + " shared lock is atomic", a.locked && b.locked && !a.IsOpen && !b.IsOpen
                        && a.Prompt(player).StartsWith("Destrancar", StringComparison.Ordinal), "A key for only one leaf does not unlock or open either leaf.");
                    player.inventory.items.Add(new InventoryItem { id = b.requiredKey });
                    b.Interact(player);
                    Add(report, opening.block + " keys unlock both from either leaf", !a.locked && !b.locked && Targets(a, b, true), "Both required keys are checked before either lock changes.");
                    string saved = owner.CaptureState();
                    Add(report, opening.block + " paired capture shares state", saved == partner.CaptureState(), "Both existing persistent IDs capture the same open/locked JSON.");
                    owner.RestoreState("{\"open\":false,\"locked\":true}");
                    partner.RestoreState(saved);
                    Add(report, opening.block + " legacy restore owner then partner", !a.IsOpen && !b.IsOpen && a.locked && b.locked,
                        "A divergent partner record cannot override the stable owner ID.");
                    partner.RestoreState("{\"open\":false,\"locked\":true}");
                    owner.RestoreState(saved);
                    Add(report, opening.block + " legacy restore partner then owner", a.IsOpen && b.IsOpen && !a.locked && !b.locked,
                        "Save enumeration order does not split a doorway.");
                    Add(report, opening.block + " JSON restore snaps both leaf rotations", RestoredPose(a) && RestoredPose(b),
                        "Each leaf is at its own openAngle; no movement or completion sounds are queued.");
                    b.locked = true;
                    Add(report, opening.block + " either leaf lock appears in both captures", a.CaptureState() == b.CaptureState()
                        && a.CaptureState().Contains("\"locked\":true"), "A lock on either leaf locks the doorway in its saved state.");
                }
                var single = doors.FirstOrDefault(d => !d.pairedLeaf);
                if (single)
                {
                    Silence(single); single.RestoreState("{\"open\":false,\"locked\":false}");
                    single.Interact(null); bool opened = single.IsOpen;
                    single.Interact(null);
                    Add(report, "Single door still toggles independently", opened && !single.IsOpen, "Optional pairing defaults to null on existing single doors.");
                }
                report.doorTargetsExercised = report.openings.Count > 0;
            }
            finally
            {
                foreach (var snapshot in snapshots) snapshot.Restore();
                if (temporary) UnityEngine.Object.DestroyImmediate(temporary);
                Physics.SyncTransforms();
                report.temporaryDoorStateRestored = snapshots.All(snapshot => snapshot.Matches());
                Add(report, "Exact Editor door state restored", report.temporaryDoorStateRestored,
                    "Hinge rotations, private animation flags/timers, locks, keys and original UnityEvent references were restored. No disk save or PlayerPrefs calls.");
            }
            report.passed = report.errors.Count == 0;
            return report;
        }

        static void RequireSchool(Transform root)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before pairing or testing school doors.");
            if (!root || root.parent || root.name != "CEEP — arquitetura das plantas" || !root.gameObject.scene.IsValid()
                || (!string.IsNullOrEmpty(root.gameObject.scene.path) && root.gameObject.scene.path != SchoolIntegrationPass.ScenePath))
                throw new InvalidOperationException("Door pairing is limited to the SchoolCEEP architecture root.");
        }
        static void Silence(InteractiveDoor door)
        {
            door.onOpen = new UnityEvent(); door.onClose = new UnityEvent(); door.onUnlock = new UnityEvent();
            Field("nextLockedSound").SetValue(door, float.MaxValue);
        }
        static bool Targets(InteractiveDoor a, InteractiveDoor b, bool open) => a.IsOpen == open && b.IsOpen == open
            && (bool)Field("movementSoundPending").GetValue(a) && (bool)Field("movementSoundPending").GetValue(b)
            && (bool)Field("completionSoundPending").GetValue(a) && (bool)Field("completionSoundPending").GetValue(b);
        static bool RestoredPose(InteractiveDoor door) => Mathf.Abs((float)Field("angle").GetValue(door) - door.openAngle) < .001f
            && Quaternion.Angle(door.hinge.localRotation, (Quaternion)Field("closedRotation").GetValue(door) * Quaternion.Euler(0, door.openAngle, 0)) < .01f
            && !(bool)Field("movementSoundPending").GetValue(door) && !(bool)Field("completionSoundPending").GetValue(door);
        static void Add(Report report, string name, bool passed, string detail)
        {
            report.checks.Add(new Check { name = name, passed = passed, detail = detail });
            if (!passed) report.errors.Add(name + ": " + detail);
        }
        sealed class Snapshot
        {
            readonly InteractiveDoor door;
            readonly Quaternion rotation;
            readonly bool locked;
            readonly string key;
            readonly UnityEvent onOpen, onClose, onUnlock;
            readonly object[] privateValues;
            public Snapshot(InteractiveDoor door)
            {
                this.door = door; rotation = door.hinge ? door.hinge.localRotation : door.transform.localRotation;
                locked = door.locked; key = door.requiredKey; onOpen = door.onOpen; onClose = door.onClose; onUnlock = door.onUnlock;
                privateValues = RuntimeFields.Select(field => field.GetValue(door)).ToArray();
            }
            public void Restore()
            {
                for (int i = 0; i < RuntimeFields.Length; i++) RuntimeFields[i].SetValue(door, privateValues[i]);
                door.locked = locked; door.requiredKey = key; door.onOpen = onOpen; door.onClose = onClose; door.onUnlock = onUnlock;
                (door.hinge ? door.hinge : door.transform).localRotation = rotation;
            }
            public bool Matches() => RuntimeFields.Select((field, i) => Equals(field.GetValue(door), privateValues[i])).All(value => value)
                && door.locked == locked && door.requiredKey == key && ReferenceEquals(door.onOpen, onOpen)
                && ReferenceEquals(door.onClose, onClose) && ReferenceEquals(door.onUnlock, onUnlock)
                && Quaternion.Angle((door.hinge ? door.hinge : door.transform).localRotation, rotation) < .001f;
        }
    }
}
