// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>Close actual exterior holes around both U stairs; their two registered entrances remain open.</summary>
    public static class SchoolStairEnvelopePass
    {
        public const string GroupName = "Escadas — caixas e retornos contínuos";
        public const string ReportFile = "School-stair-envelope-report.json";
        public const string ProofFile = "School-stair-envelope-proof.json";
        const float Scale = SchoolCirculation.PdfScale;
        const float Thickness = .20f;
        const float CentralWestSkin = 990.76f - .112f / Scale;
        const float CentralEastSkin = 1046.68f + .112f / Scale;

        [Serializable] public sealed class Panel
        {
            public string name, block, material;
            public bool vertical;
            public float axis, start, end, bottom, top, thickness = Thickness;
        }
        [Serializable] public sealed class Report
        {
            public bool dimensionsEstimated = true, stairTreadsUnchanged, registeredRoutesUnchanged;
            public int structuralPanels, originalTreads, exteriorHoleProbesBefore, actualMissingBefore;
            public Panel[] panels;
            public string[] notes;
        }
        [Serializable] public sealed class Check
        { public string panel, side, detail; public Vector3 point; public bool physical, rendered; }
        [Serializable] public sealed class RouteCheck
        { public string name; public int samples, missingSupport, newWallObstructions; public bool passed; }
        [Serializable] public sealed class Proof
        {
            public bool passed, editorOnly = true, noPlay = true;
            public int structuralPanels, physicalFaceSamples, renderedFaceSamples, missingPhysical, missingRendered;
            public int intentionalPassageSamples, passageObstructions, preservedStairTreads;
            public Check[] checks; public RouteCheck[] routes; public string[] errors;
        }
        sealed class RenderMesh
        { public Bounds bounds; public Vector3[] vertices; public int[] triangles; }
        static Report report;

        // Called after CampusConnections.Prepare. These checks reject incompatible anchors;
        // the continuous seven-metre wall profiles are built below, rather than adding
        // two shorter CAD spans with a false gap at the intermediate floor slab.
        public static void Prepare(SchoolMapBuilder.Layout layout)
        {
            if (layout == null || layout.levels.Length != 2) throw new InvalidOperationException("Stair envelopes require both registered floors.");
            foreach (string block in new[] { "main_middle_circulation", "proposed_east_teaching" })
                if (!layout.regions.Any(r => r.name == block)) throw new InvalidOperationException("Missing stair-envelope region: " + block);
        }

        public static Report Apply(Transform architecture)
        {
            if (!architecture || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Build stair envelopes outside Play.");
            var old = architecture.Find(GroupName); if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var group = new GameObject(GroupName) { isStatic = true }.transform; group.SetParent(architecture, false);
            var treads = Treads(architecture);
            if (treads.Length != 40) throw new InvalidOperationException("Expected twenty unchanged treads in each U stair.");
            var transforms = treads.Select(t => t.localToWorldMatrix).ToArray();
            var routeNames = SchoolCirculation.Routes.Select(r => r.name).ToArray();
            report = new Report { originalTreads = treads.Length, panels = Panels(), notes = new[] {
                "Native rays confirmed the missing central north-corridor returns and the eastern stair's north/west exterior faces at both occupied floors. Room-perimeter checks did not cover these stair volumes.",
                "Two central horizontal returns meet the actual south faces of the northern room banks through short perpendicular returns; their endpoints meet the existing full-height stair skins.",
                "The eastern stair is enclosed on its north and west exterior perimeter. Its existing southern teaching-room boundary is retained. The ground entrance remains in the south lane, and the upper exit remains in the north lane.",
                "Two eastern end panels close the unused ground space below the upper exit and the upper space over the lower-flight void. They do not cross either walking entrance, the west return, any door, or the treads.",
                "Walls have continuous physical volumes and rendered faces. The saved-scene proof samples both sides of every new panel plus native tread support, added-wall clearance and both intentional mouths; no gameplay is executed." } };
            Physics.SyncTransforms();
            foreach (var p in report.panels)
            {
                Vector3 centre = Centre(p); Vector3 direction = p.vertical ? Vector3.right : Vector3.forward;
                foreach (float sample in new[] { .60f, 1.65f, 4.95f })
                {
                    centre.y = sample; report.exteriorHoleProbesBefore++;
                    if (!Physics.RaycastAll(centre - direction * .40f, direction, .80f, ~0, QueryTriggerInteraction.Ignore)
                        .Any(h => h.collider && IsMasonry(h.collider))) report.actualMissingBefore++;
                }
                var material = AssetDatabase.LoadAssetAtPath<Material>(SchoolMapBuilder.Folder + "/Materials/" + p.material + ".mat");
                if (!material) throw new InvalidOperationException("Missing stair envelope material: " + p.material);
                Cube(group, p.name, Centre(p), Size(p), material, true);
            }
            // Finish the transverse corridor's inside face without introducing new collision.
            var tile = AssetDatabase.LoadAssetAtPath<Material>(SchoolMapBuilder.Folder + "/Materials/PhotoCentralWallTiles.mat");
            foreach (var p in report.panels.Where(p => p.block == "main_middle_circulation" && !p.vertical))
                for (int level = 0; level < 2; level++)
                {
                    Vector3 c = Centre(p); c.y = level * SchoolCirculation.FirstFloor + .70f; c.z -= p.thickness * .5f + .006f;
                    var skin = Cube(group, "Escada — acabamento do retorno transversal", c, new Vector3(Size(p).x, 1.40f, .010f), tile, false);
                    SchoolCentralPhotoPass.WorldUV(skin.GetComponent<MeshFilter>(), .60f, .30f);
                }
            var ochre = AssetDatabase.LoadAssetAtPath<Material>(SchoolMapBuilder.Folder + "/Materials/PhotoWallOchre.mat");
            foreach (var p in report.panels.Where(p => p.block == "proposed_east_teaching" && p.top - p.bottom > 6f))
                for (int level = 0; level < 2; level++)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 c = Centre(p); c.y = SchoolMapBuilder.OccupiedFloorHeight(p.block, level) + .55f;
                        Vector3 size = Size(p); size.y = 1.10f;
                        if (p.vertical) { c.x += side * (p.thickness * .5f + .004f); size.x = .006f; }
                        else { c.z += side * (p.thickness * .5f + .004f); size.z = .006f; }
                        Cube(group, "Escada leste — faixa inferior da fachada", c, size, ochre, false);
                    }
            report.structuralPanels = report.panels.Length;
            report.stairTreadsUnchanged = treads.Select((t, i) => t.localToWorldMatrix == transforms[i]).All(v => v);
            report.registeredRoutesUnchanged = routeNames.SequenceEqual(SchoolCirculation.Routes.Select(r => r.name));
            if (!report.stairTreadsUnchanged || !report.registeredRoutesUnchanged) throw new InvalidOperationException("The stair envelope changed existing circulation.");
            Physics.SyncTransforms(); Directory.CreateDirectory(SchoolMapBuilder.Output);
            File.WriteAllText(SchoolMapBuilder.Output + "/" + ReportFile, JsonUtility.ToJson(report, true));
            return report;
        }

        static Panel[] Panels()
        {
            float eastNorth = SchoolCampusConnectionsPass.Y("proposed_east_teaching", 774.6f);
            float eastSouth = SchoolCampusConnectionsPass.Y("proposed_east_teaching", 824.84f);
            float baseHeight = SchoolMapBuilder.OccupiedFloorHeight("proposed_east_teaching", 0);
            float upperHeight = SchoolMapBuilder.OccupiedFloorHeight("proposed_east_teaching", 1);
            var list = new List<Panel>();
            void Add(string name, string block, bool vertical, float axis, float start, float end, float bottom, float top)
            { list.Add(new Panel { name = name, block = block, vertical = vertical, axis = axis, start = start, end = end,
                bottom = bottom, top = top, material = block == "main_middle_circulation" ? "PhotoCentralWhite" : "PhotoWallTerracotta" }); }
            Add("Parede — retorno oeste da escada central", "main_middle_circulation", false, 809.24f, 959.74f, CentralWestSkin, 0, 7.12f);
            Add("Parede — encontro oeste com o bloco norte", "main_middle_circulation", true, 959.74f, 805, 809.24f, 0, 7.12f);
            Add("Parede — retorno leste da escada central", "main_middle_circulation", false, 809.24f, CentralEastSkin, 1076.8f, 0, 7.12f);
            Add("Parede — encontro leste com o bloco norte", "main_middle_circulation", true, 1076.8f, 805, 809.24f, 0, 7.12f);
            Add("Parede — fachada norte da caixa da escada leste", "proposed_east_teaching", false, eastNorth, 2290.84f, 2393.08f, baseHeight, upperHeight + 3.31f);
            Add("Parede — fachada oeste da caixa da escada leste", "proposed_east_teaching", true, 2290.84f, eastNorth, eastSouth, baseHeight, upperHeight + 3.31f);
            Add("Parede — fecho sob a saída da escada leste", "proposed_east_teaching", true, 2393.08f, eastNorth,
                SchoolCampusConnectionsPass.Y("proposed_east_teaching", 800.12f), baseHeight, upperHeight - .18f);
            Add("Parede — fecho acima do vazio da escada leste", "proposed_east_teaching", true, 2393.08f,
                SchoolCampusConnectionsPass.Y("proposed_east_teaching", 800.12f), eastSouth, upperHeight, upperHeight + 3.31f);
            return list.ToArray();
        }

        public static Proof Verify(Transform architecture)
        {
            var proof = new Proof(); var errors = new List<string>(); var checks = new List<Check>();
            if (!architecture || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Verify saved stair envelopes outside Play.");
            var expected = JsonUtility.FromJson<Report>(File.ReadAllText(SchoolMapBuilder.Output + "/" + ReportFile));
            var meshes = architecture.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy)
                .Select(r => { var f = r.GetComponent<MeshFilter>(); if (!f || !f.sharedMesh || !f.sharedMesh.isReadable) return null;
                    return new RenderMesh { bounds = r.bounds, vertices = f.sharedMesh.vertices.Select(f.transform.TransformPoint).ToArray(), triangles = f.sharedMesh.triangles }; })
                .Where(m => m != null).ToArray();
            Physics.SyncTransforms();
            foreach (var p in expected.panels)
            {
                int along = Mathf.Max(3, Mathf.CeilToInt((p.end - p.start) * Scale / .35f));
                int vertical = Mathf.Max(3, Mathf.CeilToInt((p.top - p.bottom) / .60f));
                for (int h = 1; h < vertical; h++) for (int i = 1; i < along; i++) for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 point = p.vertical ? SchoolMapBuilder.Point(p.axis, Mathf.Lerp(p.start, p.end, i / (float)along), Mathf.Lerp(p.bottom, p.top, h / (float)vertical))
                        : SchoolMapBuilder.Point(Mathf.Lerp(p.start, p.end, i / (float)along), p.axis, Mathf.Lerp(p.bottom, p.top, h / (float)vertical));
                    var normal = (p.vertical ? Vector3.right : Vector3.forward) * side; point += normal * p.thickness * .5f;
                    var ray = new Ray(point + normal * .04f, -normal);
                    bool physical = Physics.RaycastAll(ray, .09f, ~0, QueryTriggerInteraction.Ignore).Any(hit => IsMasonry(hit.collider));
                    bool rendered = meshes.Any(m => Rendered(ray, .09f, m));
                    proof.physicalFaceSamples++; proof.renderedFaceSamples++;
                    if (!physical) proof.missingPhysical++; if (!rendered) proof.missingRendered++;
                    checks.Add(new Check { panel = p.name, side = side < 0 ? "negative" : "positive", point = point, physical = physical, rendered = rendered });
                }
            }
            var newWalls = architecture.Find(GroupName).GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToArray();
            var scene = architecture.gameObject.scene;
            var player = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FirstPersonMotor>(true)).Single();
            var capsule = player.GetComponent<CharacterController>(); var routes = new List<RouteCheck>();
            var data = JsonUtility.FromJson<SchoolCirculation.BuildReport>(File.ReadAllText(SchoolMapBuilder.Output + "/School-circulation-report.json"));
            foreach (var r in data.routes.Where(r => r.name.StartsWith("MainU_") || r.name.StartsWith("EastU_")))
            {
                var result = new RouteCheck { name = r.name };
                foreach (var expectedFoot in r.feet)
                {
                    result.samples++;
                    var hits = Physics.RaycastAll(expectedFoot + Vector3.up * .35f, Vector3.down, .72f, ~0, QueryTriggerInteraction.Ignore)
                        .Where(hit => hit.collider && !hit.collider.transform.IsChildOf(player.transform) && hit.normal.y > .5f).OrderBy(hit => hit.distance).ToArray();
                    if (hits.Length == 0) { result.missingSupport++; continue; }
                    Vector3 foot = expectedFoot; foot.y = hits[0].point.y + .045f;
                    float radius = Mathf.Max(.01f, capsule.radius - .015f);
                    var touches = Physics.OverlapCapsule(foot + Vector3.up * radius, foot + Vector3.up * (capsule.height - radius), radius, ~0, QueryTriggerInteraction.Ignore);
                    if (touches.Any(c => newWalls.Contains(c))) result.newWallObstructions++;
                }
                result.passed = result.missingSupport == 0 && result.newWallObstructions == 0; routes.Add(result);
                if (!result.passed) errors.Add(result.name + ": missing support=" + result.missingSupport + ", new wall obstructions=" + result.newWallObstructions);
            }
            // Keep the two central and two eastern mouths unobstructed by this pass,
            // independently of the long stair-route centreline.
            foreach (var mouth in new[] { new[] { 1002.64f, 815f, 0f }, new[] { 1034.82f, 815f, 3.81f },
                new[] { 2401f, SchoolCampusConnectionsPass.Y("proposed_east_teaching", 812.84f), SchoolMapBuilder.OccupiedFloorHeight("proposed_east_teaching", 0) },
                new[] { 2401f, SchoolCampusConnectionsPass.Y("proposed_east_teaching", 787.34f), SchoolMapBuilder.OccupiedFloorHeight("proposed_east_teaching", 1) } })
            {
                Vector3 foot = SchoolMapBuilder.Point(mouth[0], mouth[1], mouth[2] + .045f); float radius = capsule.radius - .015f;
                proof.intentionalPassageSamples++;
                if (Physics.OverlapCapsule(foot + Vector3.up * radius, foot + Vector3.up * (capsule.height - radius), radius, ~0, QueryTriggerInteraction.Ignore).Any(c => newWalls.Contains(c))) proof.passageObstructions++;
            }
            proof.structuralPanels = newWalls.Length; proof.preservedStairTreads = Treads(architecture).Length;
            if (!expected.stairTreadsUnchanged || !expected.registeredRoutesUnchanged || proof.preservedStairTreads != expected.originalTreads) errors.Add("Stair walking geometry was changed.");
            if (proof.structuralPanels != expected.structuralPanels) errors.Add("Missing structural stair panel.");
            if (proof.missingPhysical > 0 || proof.missingRendered > 0) errors.Add("A saved stair wall has a missing physical or rendered face.");
            if (proof.passageObstructions > 0) errors.Add("An intentional stair mouth is obstructed.");
            if (routes.Count != 2) errors.Add("Both saved U-stair routes are required.");
            proof.checks = checks.ToArray(); proof.routes = routes.ToArray(); proof.errors = errors.ToArray(); proof.passed = errors.Count == 0;
            File.WriteAllText(SchoolMapBuilder.Output + "/" + ProofFile, JsonUtility.ToJson(proof, true)); return proof;
        }

        static Transform[] Treads(Transform architecture) => architecture.Find("Escadas e rampas/School_Circulation")
            .GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Tread_", StringComparison.Ordinal)).ToArray();
        static bool IsMasonry(Collider c) => c && (c.name.StartsWith("Parede", StringComparison.Ordinal) || c.name.Contains("alvenaria") || c.name.Contains("parede") || c.name.Contains("fecho") || c.name.Contains("divisória"));
        static Vector3 Centre(Panel p) => p.vertical ? SchoolMapBuilder.Point(p.axis, (p.start + p.end) * .5f, (p.bottom + p.top) * .5f)
            : SchoolMapBuilder.Point((p.start + p.end) * .5f, p.axis, (p.bottom + p.top) * .5f);
        static Vector3 Size(Panel p) => p.vertical ? new Vector3(p.thickness, p.top - p.bottom, (p.end - p.start) * Scale)
            : new Vector3((p.end - p.start) * Scale, p.top - p.bottom, p.thickness);
        static GameObject Cube(Transform parent, string name, Vector3 centre, Vector3 size, Material material, bool collision)
        {
            if (!material) throw new InvalidOperationException("Missing stair masonry finish.");
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.isStatic = true; go.transform.SetParent(parent, false);
            go.transform.position = centre; go.transform.localScale = size; go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
        static bool Rendered(Ray ray, float maximum, RenderMesh mesh)
        {
            if (!mesh.bounds.IntersectRay(ray, out float near) || near > maximum) return false;
            for (int i = 0; i < mesh.triangles.Length; i += 3)
            {
                Vector3 a = mesh.vertices[mesh.triangles[i]], b = mesh.vertices[mesh.triangles[i + 1]], c = mesh.vertices[mesh.triangles[i + 2]];
                Vector3 ab = b - a, ac = c - a, p = Vector3.Cross(ray.direction, ac); float determinant = Vector3.Dot(ab, p);
                if (Mathf.Abs(determinant) < 1e-8f) continue; float inverse = 1f / determinant; Vector3 delta = ray.origin - a;
                float u = Vector3.Dot(delta, p) * inverse; if (u < -.0001f || u > 1.0001f) continue; Vector3 q = Vector3.Cross(delta, ab);
                float v = Vector3.Dot(ray.direction, q) * inverse; if (v < -.0001f || u + v > 1.0001f) continue;
                float hit = Vector3.Dot(ac, q) * inverse; if (hit >= 0 && hit <= maximum) return true;
            }
            return false;
        }
    }
}
