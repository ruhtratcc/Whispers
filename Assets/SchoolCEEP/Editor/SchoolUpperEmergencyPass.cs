// Ferramenta da escola para construção ou acabamento.

using System;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>User-confirmed metal doors at the two upper corridor ends; the corresponding ground passages stay open.</summary>
    public static class SchoolUpperEmergencyPass
    {
        public const string SourceMetal = "User-confirmed upper metal emergency exit";
        public const string SourceOpenPassage = "User-confirmed open ground corridor end";
        public const string SourceJamb = "User-confirmed upper emergency door jamb closure";
        public const float CasingClearanceMeters = .065f;
        const string MaterialFolder = "Assets/SchoolCEEP/Materials/";
        const float NorthPdfY = 337.22f;
        static readonly string[] UpperIds = { "school.door.1.12", "school.door.1.33" };
        static readonly string[] GroundIds = { "school.door.0.41", "school.door.0.42" };

        [Serializable] public sealed class DoorDimensions
        {
            public string id, block;
            public float widthMeters, heightMeters, thicknessMeters, openAngle;
            public Vector3 hingePosition;
        }
        [Serializable] public sealed class Report
        {
            public bool passed, dimensionsNative = true, groundPassagesOpen;
            public bool originalUpperHingeAndColliderPreserved = true, fourteenPairsPreserved, pairedOpeningsValid;
            public int pairedOpenings;
            public int upperMetalDoors, panicBars, movingDecorativeParts, activeDoorLeaves;
            public int nativeJambClosures = 2, fixedCasingParts;
            public float casingInnerEdgeClearanceMeters = CasingClearanceMeters, casingWidthMeters = .05f, casingThicknessMeters = .05f;
            public float northPdfY = NorthPdfY, supportedNorthApronMeters = (NorthPdfY - 303.32f) * SchoolCirculation.PdfScale;
            public string[] upperMetalDoorIds, removedGroundDoorIds, notes;
            public DoorDimensions[] doors;
        }

        /// <summary>Call after CentralEnclosure.Prepare and before CorridorX adaptation. Keep every existing array index.</summary>
        public static void Prepare(SchoolMapBuilder.Layout layout)
        {
            if (layout == null || layout.levels == null) throw new ArgumentNullException(nameof(layout));
            var ground = layout.levels.Single(l => l.index == 0);
            var upper = layout.levels.Single(l => l.index == 1);
            RequireEndSpan(ground, 41, "main_laboratories_north").source = SourceOpenPassage;
            RequireEndSpan(ground, 42, "main_classrooms_north").source = SourceOpenPassage;
            RequireEndSpan(upper, 12, "main_laboratories_north").source = SourceMetal;

            // The existing central upper ramp door occupies index 32. Append the
            // missing right corridor door without renumbering its predecessors.
            if (upper.doors.Length == 33)
            {
                if (upper.doors[32].source != SchoolCentralEnclosurePass.Source)
                    throw new InvalidOperationException("Prepare emergency doors after the central ramp doorway is appended.");
                upper.doors = upper.doors.Concat(new[] { new SchoolMapBuilder.Span {
                    block = "main_classrooms_north", source = SourceMetal, vertical = false,
                    p = NorthPdfY, a = 1184.20f, b = 1200.75f, width = 16.55f
                } }).ToArray();
            }
            else if (upper.doors.Length != 34 || RequireEndSpan(upper, 33, "main_classrooms_north").source != SourceMetal)
                throw new InvalidOperationException("Unexpected upper door registry; emergency door must retain school.door.1.33.");
            // Close the two previously open strips beside the new right door,
            // using the registered corridor axes before CorridorX adapts them.
            if (!upper.walls.Any(w => w.source == SourceJamb))
                upper.walls = upper.walls.Concat(new[] {
                    new SchoolMapBuilder.Span { block = "main_classrooms_north", source = SourceJamb, vertical = false,
                        p = NorthPdfY, a = 1179.34f, b = 1184.20f, thickness = 2.13f },
                    new SchoolMapBuilder.Span { block = "main_classrooms_north", source = SourceJamb, vertical = false,
                        p = NorthPdfY, a = 1200.75f, b = 1206.64f, thickness = 2.13f }
                }).ToArray();
        }

        static SchoolMapBuilder.Span RequireEndSpan(SchoolMapBuilder.Level level, int index, string block)
        {
            if (level.doors == null || level.doors.Length <= index)
                throw new InvalidOperationException("Missing registered corridor-end door slot " + level.index + "-" + index);
            var span = level.doors[index];
            if (span.block != block || span.vertical || Mathf.Abs(span.p - NorthPdfY) > .1f)
                throw new InvalidOperationException("Registered corridor-end door changed at " + level.index + "-" + index);
            return span;
        }

        /// <summary>Call after ClassroomPhoto.Apply so its cream paint cannot overwrite the emergency metal finish.</summary>
        public static Report Apply(Transform root)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Apply upper emergency doors outside Play mode.");
            var all = root.GetComponentsInChildren<InteractiveDoor>(true);
            var doors = UpperIds.Select(id => all.Single(d => d.persistentId == id)).ToArray();
            if (all.Any(d => GroundIds.Contains(d.persistentId)))
                throw new InvalidOperationException("BuildDoor must omit SourceOpenPassage leaves at the ground corridor ends.");
            if (doors.Any(d => !d.hinge || !d.leafCollider || d.pairedLeaf || !d.gameObject.activeInHierarchy
                || Mathf.Abs(d.hinge.position.y - 3.81f) > .02f))
                throw new InvalidOperationException("Upper emergency doors require active single leaves at the existing upper slab.");

            var report = new Report {
                groundPassagesOpen = true,
                upperMetalDoorIds = UpperIds.ToArray(), removedGroundDoorIds = GroundIds.ToArray(),
                activeDoorLeaves = all.Count(d => d.gameObject.activeInHierarchy),
                notes = new[] {
                    "User confirms a metal emergency door at the far end of both upper central corridors, and no door at the matching ground passage.",
                    "Ground door array slots 41 and 42 are retained as open-passage records; their scene leaves are intentionally omitted, preserving all subsequent numeric IDs.",
                    "The left upper door retains ID 1.12 and its native hinge/collider. The right upper door is appended as ID 1.33 after the central ramp door.",
                    "The metal finish is opaque. Panic bars and small leaf reinforcements move with each leaf, are nonstatic and add no colliders.",
                    "Two upper-only native masonry spans close the gaps beside the new right emergency door. Both emergency leaves receive fixed blue metal casing outside their 65mm end clearances; this decoration adds no collider and is batched under the existing architecture blocks.",
                    "Both single leaves open north by 95 degrees onto the existing upper regional slab, which extends to PDF y303.32. No exterior stair or additional exit route is invented.",
                    "Reported leaf dimensions come from the built native BoxColliders; they are not measurements of the photographed real building. The architectural redesign retains the original paired openings and adds a paired pedagogy entrance." }
            };
            var steel = MetalMaterial();
            var hardware = HardwareMaterial();
            var casingMaterial = Material("PhotoUpperEmergencyCasing", new Color(.045f, .115f, .20f), .34f, .55f);
            report.doors = new DoorDimensions[doors.Length];
            for (int i = 0; i < doors.Length; i++)
            {
                var door = doors[i];
                var leaf = door.leafCollider;
                var hingePosition = door.hinge.position;
                var leafPosition = leaf.transform.position;
                var colliderSize = leaf.size;
                var colliderCenter = leaf.center;
                var renderer = leaf.GetComponent<MeshRenderer>();
                if (!renderer) throw new InvalidOperationException("Emergency leaf has no native mesh renderer: " + door.persistentId);
                renderer.sharedMaterial = steel;
                foreach (var handle in door.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.Contains("Handle")))
                    handle.sharedMaterial = hardware;
                door.displayName = "porta de emergência";
                door.openAngle = -95f;
                var scale = Abs(leaf.transform.lossyScale);
                var dimensions = Vector3.Scale(leaf.size, scale);
                if (dimensions.x < .9f || dimensions.y < 2.1f || dimensions.z < .01f)
                    throw new InvalidOperationException("Emergency leaf native dimensions are invalid: " + door.persistentId);

                var previous = leaf.transform.Find("Emergência — ferragens móveis");
                if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
                var details = new GameObject("Emergência — ferragens móveis").transform;
                details.SetParent(leaf.transform, false);
                float width = dimensions.x, height = dimensions.y, face = -dimensions.z * .5f;
                FixedCasing(door, width, casingMaterial, report);
                // Local -Z faces the internal corridor for these two horizontal
                // north-end doors. Native leaf rotation, pivot and collider stay intact.
                float barY = -height * .5f + 1.05f;
                Part(details, leaf, "Barra antipânico", new Vector3(0, barY, face - .066f),
                    new Vector3(width * .70f, .055f, .045f), hardware, report);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(details, leaf, "Suporte da barra antipânico", new Vector3(side * width * .31f, barY, face - .028f),
                        new Vector3(.072f, .13f, .046f), hardware, report);
                    Part(details, leaf, "Reforço lateral da folha metálica", new Vector3(side * (width * .5f - .026f), 0, face - .011f),
                        new Vector3(.035f, height - .065f, .018f), steel, report);
                    Part(details, leaf, "Reforço horizontal da folha metálica", new Vector3(0, side * (height * .5f - .026f), face - .011f),
                        new Vector3(width - .065f, .035f, .018f), steel, report);
                }
                report.panicBars++;
                report.upperMetalDoors++;
                report.doors[i] = new DoorDimensions {
                    id = door.persistentId, block = door.transform.parent.name,
                    widthMeters = width, heightMeters = height, thicknessMeters = dimensions.z,
                    openAngle = door.openAngle, hingePosition = hingePosition
                };
                report.originalUpperHingeAndColliderPreserved &= Vector3.Distance(door.hinge.position, hingePosition) < .0001f
                    && Vector3.Distance(leaf.transform.position, leafPosition) < .0001f
                    && leaf.size == colliderSize && leaf.center == colliderCenter;
                PrefabUtility.RecordPrefabInstancePropertyModifications(door);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                EditorUtility.SetDirty(door);
            }
            report.pairedOpenings = all.Count(d => d.pairedLeaf && string.CompareOrdinal(d.persistentId, d.pairedLeaf.persistentId) < 0);
            report.fourteenPairsPreserved = report.pairedOpenings >= 14;
            report.pairedOpeningsValid = all.Where(d => d.pairedLeaf).All(d => d.pairedLeaf.pairedLeaf == d);
            report.passed = report.upperMetalDoors == 2 && report.panicBars == 2 && report.fixedCasingParts == 6 && report.groundPassagesOpen
                && report.originalUpperHingeAndColliderPreserved && report.fourteenPairsPreserved && report.pairedOpeningsValid;
            return report;
        }

        static void FixedCasing(InteractiveDoor door, float width, Material material, Report report)
        {
            // Keep the casing under its registered static architecture block,
            // so BatchStaticGeometry combines it and the moving hinge stays free.
            var block = door.transform.parent;
            string groupName = "Emergência — marco metálico fixo " + door.persistentId;
            var previous = block.Find(groupName);
            if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var group = new GameObject(groupName).transform; group.SetParent(block, false); group.gameObject.isStatic = true;
            var center = door.leafCollider.transform.TransformPoint(door.leafCollider.center);
            float baseY = door.hinge.position.y;
            const float casingWidth = .05f, casingHeight = 2.30f, casingDepth = .05f;
            float leftX = center.x - width * .5f - CasingClearanceMeters - casingWidth * .5f;
            float rightX = center.x + width * .5f + CasingClearanceMeters + casingWidth * .5f;
            float leftFace = NativeJambFace(block, leftX, center.z, baseY);
            float rightFace = NativeJambFace(block, rightX, center.z, baseY);
            // The south face looks into the corridor. The rear of each casing
            // sits 1mm inside its native wall face, avoiding a floating overlay.
            StaticPart(group, "Marco metálico — ombreira esquerda", new Vector3(leftX, baseY + casingHeight * .5f, leftFace - casingDepth * .5f + .001f),
                new Vector3(casingWidth, casingHeight, casingDepth), material, report);
            StaticPart(group, "Marco metálico — ombreira direita", new Vector3(rightX, baseY + casingHeight * .5f, rightFace - casingDepth * .5f + .001f),
                new Vector3(casingWidth, casingHeight, casingDepth), material, report);
            float crownWidth = rightX - leftX + casingWidth;
            float crownFace = NativeJambFace(block, center.x, center.z, baseY, casingHeight + casingWidth * .5f);
            StaticPart(group, "Marco metálico — travessa superior", new Vector3(center.x, baseY + casingHeight + casingWidth * .5f,
                crownFace - casingDepth * .5f + .001f), new Vector3(crownWidth, casingWidth, casingDepth), material, report);
        }
        static float NativeJambFace(Transform block, float x, float z, float baseY, float heightAboveFloor = .80f)
        {
            var faces = block.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy
                && (c.name == "Parede — pintura inferior" || c.name == "Parede — reboco" || c.name == "Verga da porta")
                && c.bounds.min.x <= x + .002f && c.bounds.max.x >= x - .002f
                && c.bounds.min.y <= baseY + heightAboveFloor && c.bounds.max.y >= baseY + heightAboveFloor
                && Mathf.Abs(c.bounds.center.z - z) < .25f && c.bounds.size.z < .70f).ToArray();
            if (faces.Length == 0) throw new InvalidOperationException("Emergency casing has no native masonry jamb at x=" + x + ", z=" + z);
            return faces.Min(c => c.bounds.min.z);
        }
        static void StaticPart(Transform parent, string name, Vector3 position, Vector3 size, Material material, Report report)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.isStatic = true;
            go.transform.SetParent(parent, false); go.transform.position = position; go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            report.fixedCasingParts++;
        }

        static void Part(Transform parent, BoxCollider leaf, string name, Vector3 meterOffset, Vector3 meterSize,
            Material material, Report report)
        {
            var scale = Abs(leaf.transform.lossyScale);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.isStatic = false;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = leaf.center + new Vector3(meterOffset.x / scale.x, meterOffset.y / scale.y, meterOffset.z / scale.z);
            go.transform.localScale = new Vector3(meterSize.x / scale.x, meterSize.y / scale.y, meterSize.z / scale.z);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            report.movingDecorativeParts++;
        }
        static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

        static Material MetalMaterial()
        {
            var material = Material("PhotoUpperEmergencyMetal", new Color(.36f, .38f, .39f), .35f, .72f);
            const string texturePath = MaterialFolder + "PhotoUpperEmergencyBrushedGrain.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (!texture)
            {
                texture = new Texture2D(128, 64, TextureFormat.RGBA32, true, true) {
                    name = "PhotoUpperEmergencyBrushedGrain", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear
                };
                var pixels = new Color[128 * 64];
                var random = new System.Random(5133);
                for (int y = 0; y < 64; y++)
                {
                    float stripe = .90f + (float)random.NextDouble() * .07f;
                    for (int x = 0; x < 128; x++)
                    {
                        float grain = stripe + ((float)random.NextDouble() - .5f) * .023f;
                        pixels[y * 128 + x] = new Color(grain, grain, grain, 1);
                    }
                }
                texture.SetPixels(pixels); texture.Apply();
                AssetDatabase.CreateAsset(texture, texturePath);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", new Vector2(1, 3));
            EditorUtility.SetDirty(material);
            return material;
        }
        static Material HardwareMaterial() => Material("PhotoUpperEmergencyHardware", new Color(.24f, .26f, .27f), .48f, .78f);
        static Material Material(string name, Color color, float smoothness, float metallic)
        {
            var path = MaterialFolder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness); material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Surface", 0); material.SetFloat("_AlphaClip", 0);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = -1;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
