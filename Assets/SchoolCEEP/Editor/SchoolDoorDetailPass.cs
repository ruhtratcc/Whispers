// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SchoolCEEP.Editor
{
    /// <summary>Visual jambs and moving hardware measured from the native school leaves. No physics or gameplay changes.</summary>
    public static class SchoolDoorDetailPass
    {
        public const int SourceRevision = 1;
        public const string ReportFile = "School-door-detail-report.json";
        public const string ProofFile = "School-door-detail-proof.json";
        const string MovingName = "Porta — detalhes móveis";
        const string FramePrefix = "Porta — marco fixo ";
        const float Clearance = .003f, WallOverlap = .010f;

        [Serializable] public sealed class NativeLeaf
        {
            public string id, partnerId, hingePath, leafPath;
            public Vector3 hingePosition, hingeScale, leafPosition, leafScale, colliderCenter, colliderSize;
            public Quaternion hingeRotation, leafRotation;
            public float openAngle, speed;
            public bool locked, colliderEnabled, colliderTrigger;
            public int playerMask;
        }
        [Serializable] public sealed class Contact
        {
            public string id, side, material, nativeSupportName;
            public Vector3 nativePoint, renderedPoint, normal;
            public float originalClearanceMeters, closedLeafClearanceMeters;
            public bool nativeSupportFound;
        }
        [Serializable] public sealed class Opening
        {
            public string firstId, secondId, framePath;
            public bool grille, metal;
            public Vector3 min, max;
            public int frameParts;
        }
        [Serializable] public sealed class Report
        {
            public int sourceRevision = SourceRevision;
            public string scene;
            public bool passed, nativeLeavesPreserved, noAddedColliders, movingHardwareValid, fixedFramesValid;
            public bool editorOnly = true, gameplayExecuted, checkpointTouched, temporaryPosesRestored;
            public int leaves, openings, pairedOpenings, grilleLeaves, metalLeaves, leverHandles, hingeBarrels;
            public int fixedParts, movingParts, movingRenderers, removedLegacyBraces, collidersBefore, collidersAfter;
            public int nativeContactChecks, renderedContactChecks, movingPoseChecks, recessedPanelClearanceChecks, legacyBracesRemaining;
            public List<NativeLeaf> native = new List<NativeLeaf>();
            public List<Opening> frames = new List<Opening>();
            public List<Contact> contacts = new List<Contact>();
            public List<string> errors = new List<string>();
            public string[] notes;
        }
        sealed class Axes
        {
            public Vector3 origin;
            public Quaternion rotation;
            public Vector3 World(Vector3 p) => origin + rotation * p;
            public Vector3 Local(Vector3 p) => Quaternion.Inverse(rotation) * (p - origin);
        }
        sealed class Wall
        {
            public Collider collider;
            public Bounds bounds;
        }
        sealed class Part
        {
            public Transform transform;
            public Mesh mesh;
            public Material material;
        }
        static Mesh cylinder;
        static string meshFolder;
        static int meshIndex;

        /// <summary>Run after all photo/architecture passes and pairing, before masonry optimization and static batching.</summary>
        public static Report Apply(Transform architecture)
        {
            RequireEditor(architecture);
            var doors = SchoolDoors(architecture);
            if (doors.Any(d => !d.hinge || !d.leafCollider || !d.leafCollider.transform.IsChildOf(d.hinge)))
                throw new InvalidOperationException("School door detailing requires every original hinge and native leaf collider.");
            if (doors.Select(d => d.persistentId).Distinct().Count() != doors.Length)
                throw new InvalidOperationException("Duplicate school door ID before detailing.");
            Physics.SyncTransforms();
            var report = new Report { scene = architecture.gameObject.scene.path, leaves = doors.Length,
                collidersBefore = architecture.GetComponentsInChildren<Collider>(true).Length,
                native = doors.Select(d => Snapshot(architecture, d)).ToList(),
                notes = new[] {
                    "Dimensions are derived from the built native BoxColliders in hinge axes, preserving the existing nonuniform leaf/root scales, pivots, persistent IDs and interaction references.",
                    "Visual jambs bridge the carved 65mm side allowance. Their inner faces leave 3mm beside the closed leaves; their outer faces overlap the actual native masonry by 10mm. Front and rear casings cover the wall/leaf seam. Side support is measured at 61% of the native leaf height, including beside adjacent glazing; support only below the window does not satisfy this contact proof.",
                    "Paired leaves share one outer frame with no center post. A reciprocal native pairedLeaf link is authoritative; a coplanar pair of opposing outer pivots meeting at its free edges is the conservative fallback.",
                    "Metal lever handles have roses, shafts, round grips and screws on both faces. Three segmented hinge barrels and modest panel moldings move with each solid leaf. Existing metal emergency panic bars remain.",
                    "The generated legacy blue X braces are removed from the paneled leaves after confirming that they contain no native collider or interaction. Recessed panel faces start 4mm outside the original leaf surface and their moldings rise to 16mm, avoiding nearly coincident surfaces in distant grazing views.",
                    "The open refectory grille receives hardware only. Its original hidden body, grille bars and native collision remain; no panel or solid frame is added to its foliage.",
                    "All generated details are visual only. Fixed frames belong to the architecture, outside every swinging hinge. Moving detail meshes are nonstatic children of the native leaf. No gameplay, save, audio or runtime door state is exercised." } };
            Directory.CreateDirectory(SchoolMapBuilder.Folder + "/Materials");
            Directory.CreateDirectory(SchoolMapBuilder.Output);
            meshFolder = SchoolMapBuilder.Folder + "/Meshes/DoorDetails-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            Directory.CreateDirectory(meshFolder); meshIndex = 0;
            cylinder = CylinderMesh();
            AssetDatabase.CreateAsset(cylinder, meshFolder + "/Hardware-cylinder.asset");
            var hardware = Material("DoorDetailHardwareSteel", new Color(.35f, .38f, .39f), .54f, .86f);
            var dark = Material("DoorDetailScrewSlots", new Color(.045f, .052f, .055f), .26f, .55f);
            var blue = Material("DoorDetailCasingBlue", new Color(.045f, .105f, .19f), .24f, .28f);
            var timber = Material("DoorDetailCasingTimber", new Color(.18f, .137f, .083f), .18f, 0f);
            foreach (var door in doors)
            {
                var previous = door.leafCollider.transform.Find(MovingName);
                if (previous) DestroyVisual(previous);
                // Keep the prefab object/reference intact, suppressing only its obsolete cuboid visual.
                foreach (var old in door.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "Handle"))
                {
                    old.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(old);
                }
                foreach (var oldBrace in door.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Armação azul em X").ToArray())
                {
                    DestroyVisual(oldBrace);
                    report.removedLegacyBraces++;
                }
                var legacyFrame = door.transform.parent.Find("Emergência — marco metálico fixo " + door.persistentId);
                if (legacyFrame) DestroyVisual(legacyFrame);
                MovingDetails(door, hardware, dark, report);
            }
            var consumed = new HashSet<InteractiveDoor>();
            foreach (var door in doors)
            {
                if (consumed.Contains(door)) continue;
                bool grille = IsGrille(door);
                var partner = grille ? null : Partner(door, doors, consumed);
                consumed.Add(door); if (partner) consumed.Add(partner);
                var opening = new Opening { firstId = door.persistentId, secondId = partner ? partner.persistentId : null,
                    grille = grille, metal = IsMetal(door) };
                report.frames.Add(opening);
                if (grille) continue;
                if (partner) report.pairedOpenings++;
                var body = door.leafCollider.GetComponent<MeshRenderer>();
                var color = body && body.sharedMaterial && body.sharedMaterial.HasProperty("_BaseColor")
                    ? body.sharedMaterial.GetColor("_BaseColor") : Color.white;
                FixedFrame(architecture, door, partner, opening, opening.metal || color.grayscale > .42f ? blue : timber, report);
            }
            report.openings = report.frames.Count;
            report.collidersAfter = architecture.GetComponentsInChildren<Collider>(true).Length;
            report.noAddedColliders = report.collidersBefore == report.collidersAfter;
            report.nativeLeavesPreserved = NativeMatches(architecture, doors, report.native);
            if (!report.noAddedColliders) report.errors.Add("Detailing changed the architecture collider count.");
            if (!report.nativeLeavesPreserved) report.errors.Add("Detailing changed a native leaf, hinge, interaction setting or ID.");
            AssetDatabase.SaveAssets();
            VerifyInternal(architecture, report, false);
            report.passed = report.errors.Count == 0;
            File.WriteAllText(SchoolMapBuilder.Output + "/" + ReportFile, JsonUtility.ToJson(report, true));
            EditorSceneManager.MarkSceneDirty(architecture.gameObject.scene);
            return report;
        }

        /// <summary>Rechecks native contacts and rendered meshes, including meshes combined by the school builder.</summary>
        public static Report Verify(Transform architecture, Report expected = null)
        {
            RequireEditor(architecture);
            var baseline = expected ?? JsonUtility.FromJson<Report>(File.ReadAllText(SchoolMapBuilder.Output + "/" + ReportFile));
            if (baseline == null || baseline.sourceRevision != SourceRevision) throw new InvalidDataException("Missing current door-detail baseline.");
            var report = JsonUtility.FromJson<Report>(JsonUtility.ToJson(baseline));
            report.errors.Clear(); report.nativeContactChecks = report.renderedContactChecks = report.movingPoseChecks = report.recessedPanelClearanceChecks = 0;
            VerifyInternal(architecture, report, true);
            report.passed = report.errors.Count == 0;
            File.WriteAllText(SchoolMapBuilder.Output + "/" + ProofFile, JsonUtility.ToJson(report, true));
            return report;
        }

        static void FixedFrame(Transform architecture, InteractiveDoor first, InteractiveDoor second, Opening record, Material material, Report report)
        {
            var axes = HingeAxes(first);
            var opening = ColliderBounds(first.leafCollider, axes);
            if (second) opening.Encapsulate(ColliderBounds(second.leafCollider, axes));
            record.min = opening.min; record.max = opening.max;
            var block = first.transform.parent;
            string name = FramePrefix + first.persistentId;
            var previous = block.Find(name); if (previous) DestroyVisual(previous);
            var frame = new GameObject(name).transform;
            frame.SetParent(block, false); frame.position = axes.origin; frame.rotation = axes.rotation;
            frame.localScale = Divide(Vector3.one, Abs(block.lossyScale)); frame.gameObject.isStatic = true;
            record.framePath = Path(architecture, frame);
            var walls = architecture.GetComponentsInChildren<Collider>(true).Where(IsMasonry)
                .Where(c => c.transform.IsChildOf(block)).Select(c => new Wall { collider = c, bounds = ColliderBounds(c, axes) }).ToArray();
            float height = opening.size.y, sampleY = opening.min.y + height * .61f, z = opening.center.z;
            var left = FindJamb(walls, opening.min.x, sampleY, z, false);
            var right = FindJamb(walls, opening.max.x, sampleY, z, true);
            float leftEdge = left != null ? left.bounds.max.x : opening.min.x - .065f;
            float rightEdge = right != null ? right.bounds.min.x : opening.max.x + .065f;
            float x0 = Mathf.Min(leftEdge - WallOverlap, opening.min.x - .040f);
            float x1 = Mathf.Max(rightEdge + WallOverlap, opening.max.x + .040f);
            float innerLeft = opening.min.x - Clearance, innerRight = opening.max.x + Clearance;
            float z0 = Mathf.Min(opening.min.z - .006f, Mathf.Min(left != null ? left.bounds.min.z : z - .085f,
                right != null ? right.bounds.min.z : z - .085f));
            float z1 = Mathf.Max(opening.max.z + .006f, Mathf.Max(left != null ? left.bounds.max.z : z + .085f,
                right != null ? right.bounds.max.z : z + .085f));
            float baseY = opening.min.y + .010f, crownY = opening.max.y + Clearance;
            var head = walls.Where(w => w.bounds.min.y >= opening.max.y - .003f && w.bounds.min.y <= opening.max.y + .12f
                && w.bounds.min.x < opening.center.x && w.bounds.max.x > opening.center.x && w.bounds.min.z <= z && w.bounds.max.z >= z)
                .OrderBy(w => w.bounds.min.y).FirstOrDefault();
            float top = Mathf.Max(crownY + .065f, head != null ? head.bounds.min.y + WallOverlap : crownY + .065f);
            var parts = new List<Part>();
            AddBox(parts, frame, "Ombreira esquerda volumétrica", new Vector3((x0 + innerLeft) * .5f, (baseY + crownY) * .5f, (z0 + z1) * .5f),
                new Vector3(innerLeft - x0, crownY - baseY, z1 - z0), material, true);
            AddBox(parts, frame, "Ombreira direita volumétrica", new Vector3((innerRight + x1) * .5f, (baseY + crownY) * .5f, (z0 + z1) * .5f),
                new Vector3(x1 - innerRight, crownY - baseY, z1 - z0), material, true);
            AddBox(parts, frame, "Travessa superior volumétrica", new Vector3((x0 + x1) * .5f, (crownY + top) * .5f, (z0 + z1) * .5f),
                new Vector3(x1 - x0, top - crownY, z1 - z0), material, true);
            foreach (int face in new[] { -1, 1 })
            {
                float surface = face < 0 ? z0 : z1, faceZ = surface + face * .011f;
                AddBox(parts, frame, "Guarnição esquerda", new Vector3((x0 - .040f + innerLeft) * .5f, (baseY + crownY) * .5f, faceZ),
                    new Vector3(innerLeft - x0 + .040f, crownY - baseY, .022f), material, true);
                AddBox(parts, frame, "Guarnição direita", new Vector3((innerRight + x1 + .040f) * .5f, (baseY + crownY) * .5f, faceZ),
                    new Vector3(x1 - innerRight + .040f, crownY - baseY, .022f), material, true);
                AddBox(parts, frame, "Guarnição superior", new Vector3((x0 + x1) * .5f, crownY + .045f, faceZ),
                    new Vector3(x1 - x0 + .080f, .090f, .022f), material, true);
                ContactRecord(first, "left", left, axes, material, opening.min.x - leftEdge,
                    new Vector3(leftEdge - .006f, sampleY, z), new Vector3((leftEdge + innerLeft) * .5f, sampleY, surface + face * .022f), face, report);
                ContactRecord(first, "right", right, axes, material, rightEdge - opening.max.x,
                    new Vector3(rightEdge + .006f, sampleY, z), new Vector3((rightEdge + innerRight) * .5f, sampleY, surface + face * .022f), face, report);
                if (head != null) ContactRecord(first, "top", head, axes, material, head.bounds.min.y - opening.max.y,
                    new Vector3(opening.center.x, head.bounds.min.y + .006f, z), new Vector3(opening.center.x, crownY + .032f, surface + face * .022f), face, report);
            }
            record.frameParts = parts.Count; report.fixedParts += parts.Count;
            Combine(parts, frame, "Marco e guarnições", true);
        }

        static void ContactRecord(InteractiveDoor door, string side, Wall native, Axes axes, Material material, float gap,
            Vector3 nativePoint, Vector3 renderedPoint, int face, Report report)
        {
            report.contacts.Add(new Contact { id = door.persistentId, side = side, material = material.name,
                nativeSupportName = native != null ? native.collider.name : string.Empty,
                originalClearanceMeters = gap, closedLeafClearanceMeters = Clearance, nativeSupportFound = native != null,
                nativePoint = axes.World(nativePoint), renderedPoint = axes.World(renderedPoint), normal = axes.rotation * (Vector3.forward * face) });
        }

        static void MovingDetails(InteractiveDoor door, Material steel, Material dark, Report report)
        {
            var axes = HingeAxes(door); var b = ColliderBounds(door.leafCollider, axes);
            bool grille = IsGrille(door), metal = IsMetal(door);
            if (grille) report.grilleLeaves++; if (metal) report.metalLeaves++;
            var group = new GameObject(MovingName).transform;
            group.SetParent(door.leafCollider.transform, false); group.position = axes.origin; group.rotation = axes.rotation;
            group.localScale = Divide(Vector3.one, Abs(door.leafCollider.transform.lossyScale)); group.gameObject.isStatic = false;
            var parts = new List<Part>();
            float handleX = b.max.x - Mathf.Min(.115f, b.size.x * .17f);
            float handleY = b.min.y + Mathf.Min(1.025f, b.size.y * .48f);
            foreach (int face in new[] { -1, 1 })
            {
                float surface = face < 0 ? b.min.z : b.max.z;
                Vector3 center = new Vector3(handleX, handleY, surface + face * .008f);
                AddRound(parts, group, "Maçaneta — roseta", center, .027f, .014f, Vector3.forward, steel);
                AddRound(parts, group, "Maçaneta — eixo", center + Vector3.forward * face * .025f, .009f, .048f, Vector3.forward, steel);
                Vector3 grip = center + new Vector3(-.051f, -.010f, face * .055f);
                AddRound(parts, group, "Maçaneta — alavanca metálica", grip, .010f, .116f, Vector3.right, steel);
                AddRound(parts, group, "Maçaneta — retorno arredondado", grip + Vector3.right * .050f + Vector3.up * .005f,
                    .010f, .026f, Vector3.up, steel);
                foreach (int screw in new[] { -1, 1 })
                {
                    var point = center + new Vector3(0, screw * .018f, face * .009f);
                    AddRound(parts, group, "Maçaneta — parafuso", point, .0035f, .0024f, Vector3.forward, steel);
                    AddBox(parts, group, "Maçaneta — fenda do parafuso", point + Vector3.forward * face * .0014f,
                        new Vector3(.0044f, .0009f, .0006f), dark, false);
                }
                report.leverHandles++;
            }
            for (int hinge = 0; hinge < 3; hinge++)
            {
                float h = Mathf.Lerp(b.min.y + .19f, b.max.y - .19f, hinge * .5f);
                float z = b.min.z - .010f;
                AddBox(parts, group, "Dobradiça — chapa da folha", new Vector3(b.min.x + .026f, h, b.min.z - .003f),
                    new Vector3(.052f, .086f, .006f), steel, false);
                for (int knuckle = -1; knuckle <= 1; knuckle++)
                    AddRound(parts, group, "Dobradiça — segmento do barril", new Vector3(b.min.x + .004f, h + knuckle * .027f, z),
                        .010f, .025f, Vector3.up, steel);
                foreach (int end in new[] { -1, 1 })
                    AddRound(parts, group, "Dobradiça — remate do pino", new Vector3(b.min.x + .004f, h + end * .043f, z),
                        .011f, .006f, Vector3.up, steel);
                report.hingeBarrels++;
            }
            if (!grille && !metal)
            {
                var leafRenderer = door.leafCollider.GetComponent<MeshRenderer>();
                var paint = leafRenderer ? leafRenderer.sharedMaterial : null;
                if (paint)
                {
                    var color = paint.HasProperty("_BaseColor") ? paint.GetColor("_BaseColor") : Color.gray;
                    var inset = Material("DoorDetailInset_" + Safe(paint.name), color * .92f, .12f, 0f);
                    foreach (int face in new[] { -1, 1 })
                    foreach (var panel in new[] { new Vector2(.22f, .28f), new Vector2(.69f, .41f) })
                    {
                        float panelY = b.min.y + b.size.y * panel.x, panelH = b.size.y * panel.y;
                        float panelW = b.size.x * .70f, z = (face < 0 ? b.min.z : b.max.z);
                        AddBox(parts, group, "Painel — centro recuado", new Vector3(b.center.x, panelY, z + face * .006f),
                            new Vector3(panelW, panelH, .004f), inset, false);
                        foreach (int side in new[] { -1, 1 })
                        {
                            AddBox(parts, group, "Painel — moldura vertical", new Vector3(b.center.x + side * (panelW * .5f + .008f), panelY, z + face * .011f),
                                new Vector3(.020f, panelH + .040f, .010f), paint, false);
                            AddBox(parts, group, "Painel — moldura horizontal", new Vector3(b.center.x, panelY + side * (panelH * .5f + .010f), z + face * .011f),
                                new Vector3(panelW + .020f, .020f, .010f), paint, false);
                        }
                    }
                }
            }
            report.movingParts += parts.Count;
            report.movingRenderers += Combine(parts, group, "Maçanetas, dobradiças e relevos", false);
        }

        static InteractiveDoor Partner(InteractiveDoor door, InteractiveDoor[] doors, HashSet<InteractiveDoor> consumed)
        {
            if (door.pairedLeaf && door.pairedLeaf.pairedLeaf == door && doors.Contains(door.pairedLeaf) && !consumed.Contains(door.pairedLeaf))
                return door.pairedLeaf;
            var axes = HingeAxes(door); var a = ColliderBounds(door.leafCollider, axes);
            return doors.Where(d => d != door && !consumed.Contains(d) && !IsGrille(d) && d.transform.parent == door.transform.parent
                    && Vector3.Dot(door.hinge.right, d.hinge.right) < -.999f)
                .FirstOrDefault(d => { var b = ColliderBounds(d.leafCollider, axes);
                    return Mathf.Abs(a.max.x - b.min.x) < .012f && Mathf.Abs(a.center.z - b.center.z) < .012f
                        && Mathf.Abs(a.min.y - b.min.y) < .010f && Mathf.Abs(a.max.y - b.max.y) < .010f; });
        }
        static Wall FindJamb(Wall[] walls, float edge, float y, float z, bool right)
        {
            return walls.Where(w => w.bounds.min.y < y && w.bounds.max.y > y && w.bounds.min.z <= z && w.bounds.max.z >= z
                    && w.bounds.size.z < .65f && (right ? w.bounds.min.x >= edge - .012f && w.bounds.min.x < edge + .32f
                        : w.bounds.max.x <= edge + .012f && w.bounds.max.x > edge - .32f))
                .OrderBy(w => right ? Mathf.Abs(w.bounds.min.x - edge) : Mathf.Abs(w.bounds.max.x - edge)).FirstOrDefault();
        }
        static bool IsGrille(InteractiveDoor d) => d.leafCollider.transform.Find("Refeitório — grade azul interativa")
            || d.leafCollider.GetComponent<MeshRenderer>() && !d.leafCollider.GetComponent<MeshRenderer>().enabled;
        static bool IsMetal(InteractiveDoor d) => d.leafCollider.transform.Find("Emergência — ferragens móveis");
        static bool IsMasonry(Collider c) => c && c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy
            && (c.name.StartsWith("Parede", StringComparison.Ordinal) || c.name.StartsWith("Verga", StringComparison.Ordinal) || c.name == "Peitoril"
                || c.name.StartsWith("Bandeira de vidro", StringComparison.Ordinal) || c.name.Contains("alvenaria") || c.name.Contains("fecho"));
        static InteractiveDoor[] SchoolDoors(Transform a) => a.GetComponentsInChildren<InteractiveDoor>(true)
            .Where(d => d.persistentId != null && d.persistentId.StartsWith("school.door.", StringComparison.Ordinal)).OrderBy(d => d.persistentId, StringComparer.Ordinal).ToArray();
        static Axes HingeAxes(InteractiveDoor d) => new Axes { origin = d.hinge.position, rotation = d.hinge.rotation };
        static Bounds ColliderBounds(Collider c, Axes axes)
        {
            if (c is BoxCollider box)
            {
                Vector3 first = axes.Local(box.transform.TransformPoint(box.center - box.size * .5f));
                var result = new Bounds(first, Vector3.zero);
                foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                    result.Encapsulate(axes.Local(box.transform.TransformPoint(box.center + Vector3.Scale(box.size * .5f, new Vector3(x, y, z)))));
                return result;
            }
            var b = c.bounds; var bounds = new Bounds(axes.Local(b.min), Vector3.zero);
            foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                bounds.Encapsulate(axes.Local(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z))));
            return bounds;
        }
        static NativeLeaf Snapshot(Transform a, InteractiveDoor d) => new NativeLeaf {
            id = d.persistentId, partnerId = d.pairedLeaf ? d.pairedLeaf.persistentId : string.Empty,
            hingePath = Path(a, d.hinge), leafPath = Path(a, d.leafCollider.transform), hingePosition = d.hinge.position,
            hingeRotation = d.hinge.rotation, hingeScale = d.hinge.localScale, leafPosition = d.leafCollider.transform.localPosition,
            leafRotation = d.leafCollider.transform.localRotation, leafScale = d.leafCollider.transform.localScale,
            colliderCenter = d.leafCollider.center, colliderSize = d.leafCollider.size, colliderEnabled = d.leafCollider.enabled,
            colliderTrigger = d.leafCollider.isTrigger, openAngle = d.openAngle, speed = d.speed, locked = d.locked, playerMask = d.playerMask.value };
        static bool NativeMatches(Transform a, InteractiveDoor[] doors, List<NativeLeaf> expected)
        {
            return doors.Length == expected.Count && expected.All(e => {
                var d = doors.FirstOrDefault(x => x.persistentId == e.id);
                if (!d || !d.hinge || !d.leafCollider) return false;
                var n = Snapshot(a, d);
                // Unity's JSON serializer persists absent strings as empty strings.
                return (n.partnerId ?? string.Empty) == (e.partnerId ?? string.Empty) && n.hingePath == e.hingePath && n.leafPath == e.leafPath
                    && Near(n.hingePosition, e.hingePosition) && Quaternion.Angle(n.hingeRotation, e.hingeRotation) < .003f
                    && Near(n.hingeScale, e.hingeScale) && Near(n.leafPosition, e.leafPosition)
                    && Quaternion.Angle(n.leafRotation, e.leafRotation) < .003f && Near(n.leafScale, e.leafScale)
                    && Near(n.colliderCenter, e.colliderCenter) && Near(n.colliderSize, e.colliderSize)
                    && n.colliderEnabled == e.colliderEnabled && n.colliderTrigger == e.colliderTrigger
                    && n.openAngle == e.openAngle && n.speed == e.speed && n.locked == e.locked && n.playerMask == e.playerMask;
            });
        }

        static void VerifyInternal(Transform architecture, Report report, bool exercisePose)
        {
            Physics.SyncTransforms();
            var doors = SchoolDoors(architecture);
            report.nativeLeavesPreserved = NativeMatches(architecture, doors, report.native);
            report.noAddedColliders = true; report.fixedFramesValid = true; report.movingHardwareValid = true;
            report.legacyBracesRemaining = doors.Sum(d => d.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Armação azul em X"));
            if (report.legacyBracesRemaining != 0) report.errors.Add("Legacy blue X braces remain on the detailed school leaves.");
            if (!report.nativeLeavesPreserved) report.errors.Add("Native door IDs, collider dimensions or closed hinge settings differ from the detailing baseline.");
            var masonry = architecture.GetComponentsInChildren<Collider>(true).Where(IsMasonry).ToArray();
            var meshes = architecture.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh && f.GetComponent<MeshRenderer>()
                && f.GetComponent<MeshRenderer>().enabled && f.gameObject.activeInHierarchy).ToArray();
            foreach (var contact in report.contacts)
            {
                report.nativeContactChecks++;
                bool native = contact.nativeSupportFound && masonry.Any(c => ContainsNative(c, contact.nativePoint));
                if (!native) report.errors.Add(contact.id + " " + contact.side + ": no native masonry contact at " + contact.nativePoint + ".");
                report.renderedContactChecks++;
                if (!RenderedContact(meshes, contact)) report.errors.Add(contact.id + " " + contact.side + ": missing rendered casing face.");
            }
            foreach (var opening in report.frames.Where(o => !o.grille))
            {
                var frame = architecture.Find(opening.framePath);
                bool valid = frame && frame.gameObject.isStatic && !doors.Any(d => frame.IsChildOf(d.hinge));
                report.fixedFramesValid &= valid;
                if (!valid) report.errors.Add(opening.firstId + ": fixed frame missing or parented to a moving hinge.");
                if (frame && frame.GetComponentsInChildren<Collider>(true).Length != 0) report.noAddedColliders = false;
            }
            foreach (var door in doors)
            {
                var moving = door.leafCollider.transform.Find(MovingName);
                bool valid = moving && moving.IsChildOf(door.leafCollider.transform)
                    && moving.GetComponentsInChildren<Transform>(true).All(t => !t.gameObject.isStatic)
                    && moving.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.sharedMaterial && r.sharedMaterial.name == "DoorDetailHardwareSteel");
                if (!valid) report.errors.Add(door.persistentId + ": moving handle geometry or nonstatic leaf hierarchy is missing.");
                report.movingHardwareValid &= valid;
                if (moving && moving.GetComponentsInChildren<Collider>(true).Length != 0) report.noAddedColliders = false;
                if (moving)
                {
                    var axes = HingeAxes(door); var body = ColliderBounds(door.leafCollider, axes);
                    foreach (var panel in moving.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh && f.GetComponent<MeshRenderer>()
                        && f.GetComponent<MeshRenderer>().sharedMaterial && f.GetComponent<MeshRenderer>().sharedMaterial.name.StartsWith("DoorDetailInset_", StringComparison.Ordinal)))
                    {
                        report.recessedPanelClearanceChecks++;
                        bool separated = panel.sharedMesh.vertices.All(v => {
                            float z = axes.Local(panel.transform.TransformPoint(v)).z;
                            return z <= body.min.z - .0035f || z >= body.max.z + .0035f;
                        });
                        if (!separated) report.errors.Add(door.persistentId + ": recessed panel surface is too close to the native leaf plane.");
                    }
                }
                if (!exercisePose || !valid) continue;
                var mesh = moving.GetComponentsInChildren<MeshFilter>().First(f => f.GetComponent<MeshRenderer>().sharedMaterial.name == "DoorDetailHardwareSteel");
                var vertices = mesh.sharedMesh.vertices;
                int index = Enumerable.Range(0, vertices.Length).OrderByDescending(i => Vector3.Distance(mesh.transform.TransformPoint(vertices[i]), door.hinge.position)).First();
                Vector3 before = mesh.transform.TransformPoint(vertices[index]), local = door.hinge.InverseTransformPoint(before);
                var rotation = door.hinge.localRotation;
                var frames = report.frames.Where(o => !o.grille).Select(o => architecture.Find(o.framePath)).Where(t => t).ToArray();
                var positions = frames.Select(t => t.position).ToArray();
                try
                {
                    door.hinge.localRotation = rotation * Quaternion.Euler(0, 18, 0);
                    Vector3 after = mesh.transform.TransformPoint(vertices[index]);
                    bool moved = Vector3.Distance(before, after) > .015f && Near(after, door.hinge.TransformPoint(local))
                        && frames.Select((t, i) => Near(t.position, positions[i])).All(p => p);
                    report.movingPoseChecks++;
                    report.movingHardwareValid &= moved;
                    if (!moved) report.errors.Add(door.persistentId + ": native hinge rotation did not move hardware independently of the fixed frames.");
                }
                finally { door.hinge.localRotation = rotation; }
            }
            report.temporaryPosesRestored = !exercisePose || NativeMatches(architecture, doors, report.native);
            if (!report.temporaryPosesRestored) report.errors.Add("Temporary hinge poses were not restored exactly.");
            if (!report.noAddedColliders) report.errors.Add("A generated moving detail or fixed frame contains a collider.");
            Physics.SyncTransforms();
        }
        static bool ContainsNative(Collider collider, Vector3 world)
        {
            if (collider is BoxCollider b)
            {
                Vector3 p = b.transform.InverseTransformPoint(world) - b.center, half = b.size * .5f;
                return Mathf.Abs(p.x) <= half.x + .00002f && Mathf.Abs(p.y) <= half.y + .00002f && Mathf.Abs(p.z) <= half.z + .00002f;
            }
            return Vector3.Distance(collider.ClosestPoint(world), world) < .0002f;
        }
        static bool RenderedContact(MeshFilter[] meshes, Contact contact)
        {
            var ray = new Ray(contact.renderedPoint + contact.normal * .035f, -contact.normal);
            foreach (var filter in meshes)
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer.sharedMaterials.Any(m => m && m.name == contact.material) || !renderer.bounds.IntersectRay(ray, out float distance) || distance > .071f) continue;
                var mesh = filter.sharedMesh; var vertices = mesh.vertices;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    if (renderer.sharedMaterials.Length <= sub || !renderer.sharedMaterials[sub] || renderer.sharedMaterials[sub].name != contact.material) continue;
                    var indices = mesh.GetTriangles(sub);
                    for (int i = 0; i < indices.Length; i += 3)
                        if (RayTriangle(ray, filter.transform.TransformPoint(vertices[indices[i]]), filter.transform.TransformPoint(vertices[indices[i + 1]]),
                            filter.transform.TransformPoint(vertices[indices[i + 2]]), out float hit) && hit < .071f) return true;
                }
            }
            return false;
        }
        static bool RayTriangle(Ray ray, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            distance = 0; Vector3 ab = b - a, ac = c - a, p = Vector3.Cross(ray.direction, ac);
            float det = Vector3.Dot(ab, p); if (Mathf.Abs(det) < 1e-8f) return false;
            float inverse = 1f / det; Vector3 offset = ray.origin - a;
            float u = Vector3.Dot(offset, p) * inverse; if (u < -.0001f || u > 1.0001f) return false;
            Vector3 q = Vector3.Cross(offset, ab); float v = Vector3.Dot(ray.direction, q) * inverse;
            if (v < -.0001f || u + v > 1.0001f) return false;
            distance = Vector3.Dot(ac, q) * inverse; return distance >= 0;
        }

        static void AddBox(List<Part> parts, Transform parent, string name, Vector3 center, Vector3 size, Material material, bool fixedPart)
        {
            if (size.x <= 0 || size.y <= 0 || size.z <= 0) throw new InvalidOperationException("Invalid door detail dimensions: " + name);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = center; go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material; go.isStatic = fixedPart;
            parts.Add(new Part { transform = go.transform, mesh = go.GetComponent<MeshFilter>().sharedMesh, material = material });
        }
        static void AddRound(List<Part> parts, Transform parent, string name, Vector3 center, float radius, float length, Vector3 axis, Material material)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis); go.transform.localScale = new Vector3(radius, length, radius);
            go.AddComponent<MeshFilter>().sharedMesh = cylinder; go.AddComponent<MeshRenderer>().sharedMaterial = material; go.isStatic = false;
            parts.Add(new Part { transform = go.transform, mesh = cylinder, material = material });
        }
        static int Combine(List<Part> parts, Transform parent, string name, bool fixedPart)
        {
            int count = 0;
            foreach (var set in parts.GroupBy(p => p.material))
            {
                var mesh = new Mesh { name = name + " — " + set.Key.name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(set.Select(p => new CombineInstance { mesh = p.mesh, transform = parent.worldToLocalMatrix * p.transform.localToWorldMatrix }).ToArray(), true, true);
                string path = meshFolder + "/Detail-" + meshIndex++ + ".asset"; AssetDatabase.CreateAsset(mesh, path);
                var go = new GameObject(mesh.name); go.transform.SetParent(parent, false); go.isStatic = fixedPart;
                go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = set.Key; count++;
            }
            foreach (var part in parts) UnityEngine.Object.DestroyImmediate(part.transform.gameObject);
            return count;
        }
        static Mesh CylinderMesh()
        {
            const int sides = 12;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float angle = Mathf.PI * 2 * i / sides; var normal = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                vertices.Add(normal + Vector3.down * .5f); vertices.Add(normal + Vector3.up * .5f);
                normals.Add(normal); normals.Add(normal); uv.Add(new Vector2(i / (float)sides, 0)); uv.Add(new Vector2(i / (float)sides, 1));
                if (i < sides) { int a = i * 2; triangles.AddRange(new[] { a, a + 1, a + 2, a + 2, a + 1, a + 3 }); }
            }
            foreach (int end in new[] { -1, 1 })
            {
                int center = vertices.Count; vertices.Add(Vector3.up * end * .5f); normals.Add(Vector3.up * end); uv.Add(Vector2.one * .5f);
                for (int i = 0; i <= sides; i++)
                {
                    float angle = Mathf.PI * 2 * i / sides; var p = new Vector3(Mathf.Cos(angle), end * .5f, Mathf.Sin(angle));
                    vertices.Add(p); normals.Add(Vector3.up * end); uv.Add(new Vector2(p.x, p.z) * .5f + Vector2.one * .5f);
                    if (i < sides) triangles.AddRange(end > 0 ? new[] { center, center + i + 2, center + i + 1 } : new[] { center, center + i + 1, center + i + 2 });
                }
            }
            var mesh = new Mesh { name = "School door hardware cylinder — 12 sides" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); return mesh;
        }
        static Material Material(string name, Color color, float smooth, float metallic)
        {
            string path = SchoolMapBuilder.Folder + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metallic); EditorUtility.SetDirty(m); return m;
        }
        static string Safe(string value) => string.Concat(value.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .000025f;
        static string Path(Transform root, Transform target)
        {
            var names = new List<string>(); for (var t = target; t && t != root; t = t.parent) names.Add(t.name);
            names.Reverse(); return string.Join("/", names);
        }
        static void DestroyVisual(Transform target)
        {
            if (target.GetComponentsInChildren<Collider>(true).Length != 0 || target.GetComponentsInChildren<InteractiveDoor>(true).Length != 0)
                throw new InvalidOperationException("Refusing to remove a door detail containing native collision or interaction: " + target.name);
            UnityEngine.Object.DestroyImmediate(target.gameObject);
        }
        static void RequireEditor(Transform architecture)
        {
            if (!architecture) throw new ArgumentNullException(nameof(architecture));
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("School door detailing runs outside Play mode.");
        }
    }
}
