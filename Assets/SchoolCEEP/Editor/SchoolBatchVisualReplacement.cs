// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>
    /// Removes only proven original furniture faces from saved static batches.
    /// Source transforms, colliders, materials and original mesh assets stay intact.
    /// New visual models are the caller's responsibility.
    /// </summary>
    public static class SchoolBatchVisualReplacement
    {
        const float Tolerance = .0001f, CellSize = .02f;
        const string BatchPrefix = "Superfícies agrupadas — ";
        const string JournalName = "SchoolBatchVisualReplacement.json";
        const string PhotoMeshes = "Assets/SchoolCEEP/Meshes/ClassroomPhoto-v1-";
        static Mesh frontPhotoCylinder;

        [Serializable] public sealed class BatchChange
        {
            public string filterPath, originalMeshAssetPath, replacementMeshAssetPath;
            public Mesh originalMesh, replacementMesh;
            public int originalTriangles, removedTriangles, remainingTriangles;
            public int subMeshCount;
            public int[] removedTrianglesBySubMesh;
        }
        [Serializable] public sealed class Report
        {
            public int partCount, expectedTriangles, removedTriangles, changedBatches, directRenderersDisabled;
            public bool alreadyApplied;
            public float positionTolerance = Tolerance;
            public string scene, targetDigest;
            public string[] sourcePartPaths, directRendererPaths;
            public BatchChange[] batches;
        }
        struct Face
        {
            public Vector3 a, b, c;
            public int owner;
            public Vector3 Center => (a + b + c) / 3f;
        }
        sealed class SubMeshChange
        {
            public int index;
            public int[] originalIndices;
            public readonly List<int> removedFaces = new List<int>();
        }
        sealed class Change
        {
            public MeshFilter filter;
            public Mesh original;
            public int originalTriangles;
            public readonly List<SubMeshChange> subMeshes = new List<SubMeshChange>();
            public int RemovedTriangles => subMeshes.Sum(s => s.removedFaces.Count);
        }
        sealed class Plan
        {
            public Report report;
            public readonly List<Change> changes = new List<Change>();
            public readonly List<MeshRenderer> direct = new List<MeshRenderer>();
        }

        /// <summary>Pure read-only geometry proof; creates no assets or scene objects.</summary>
        public static Report Preview(Transform architecture, Transform[] parts)
            => Prepare(architecture, ValidateParts(architecture, parts)).report;

        /// <summary>
        /// First proves every selected face, then clones affected batches into meshFolder.
        /// Repeating with the same selected parts and folder validates the journal and
        /// returns the previous report without modifying anything.
        /// </summary>
        public static Report Replace(Transform architecture, Transform[] parts, string meshFolder)
        {
            var selected = ValidateParts(architecture, parts);
            meshFolder = (meshFolder ?? "").Replace('\\', '/').TrimEnd('/');
            if (!meshFolder.StartsWith("Assets/", StringComparison.Ordinal) || meshFolder.Split('/').Any(p => p == ".." || p == "."))
                throw new ArgumentException("Use a dedicated folder below Assets.", nameof(meshFolder));
            string journalPath = meshFolder + "/" + JournalName;
            string digest = Digest(architecture, selected);
            if (File.Exists(journalPath))
            {
                var prior = JsonUtility.FromJson<Report>(File.ReadAllText(journalPath));
                if (prior == null || prior.targetDigest != digest || prior.scene != architecture.gameObject.scene.path)
                    throw new InvalidOperationException("This folder already belongs to a different selection. Use a new output folder.");
                ValidateApplied(architecture, prior);
                prior.alreadyApplied = true;
                return prior;
            }
            if (architecture.gameObject.scene.isDirty)
                throw new InvalidOperationException("Start visual replacement from a saved, clean scene.");

            // Nothing changes until Prepare has proved the complete selection.
            var plan = Prepare(architecture, selected);
            var report = plan.report;
            report.targetDigest = digest;
            for (int i = 0; i < plan.changes.Count; i++)
            {
                report.batches[i].replacementMeshAssetPath = meshFolder + "/FurnitureBatch-" + i.ToString("D3") + ".asset";
                if (AssetDatabase.LoadMainAssetAtPath(report.batches[i].replacementMeshAssetPath))
                    throw new InvalidOperationException("Replacement mesh path already exists without its journal: " + report.batches[i].replacementMeshAssetPath);
            }

            EnsureFolder(meshFolder);
            // Clone first. Mesh attributes and unused vertices are deliberately kept:
            // filtering indices cannot lose another surface's normals, UVs or colors.
            // Original assets are never edited, even if the operation later fails.
            for (int i = 0; i < plan.changes.Count; i++)
            {
                var change = plan.changes[i];
                var replacement = UnityEngine.Object.Instantiate(change.original);
                replacement.name = change.original.name + " — furniture faces removed";
                // Keep the original submesh/material slot order. Only matched
                // triangle sections receive new indices; every other section
                // (including a non-triangle topology) remains an exact clone.
                foreach (var sub in change.subMeshes)
                {
                    var mask = new bool[sub.originalIndices.Length / 3];
                    foreach (int face in sub.removedFaces) mask[face] = true;
                    var retained = new List<int>(sub.originalIndices.Length - sub.removedFaces.Count * 3);
                    for (int face = 0; face < mask.Length; face++)
                        if (!mask[face])
                        { retained.Add(sub.originalIndices[face * 3]); retained.Add(sub.originalIndices[face * 3 + 1]); retained.Add(sub.originalIndices[face * 3 + 2]); }
                    replacement.SetTriangles(retained, sub.index, false);
                }
                replacement.RecalculateBounds();
                AssetDatabase.CreateAsset(replacement, report.batches[i].replacementMeshAssetPath);
                EditorUtility.SetDirty(replacement); AssetDatabase.SaveAssetIfDirty(replacement);
                report.batches[i].replacementMesh = replacement;
            }
            // Record the exact original references before touching any component.
            // If disk writing fails, scene references are still entirely unchanged.
            File.WriteAllText(journalPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            for (int i = 0; i < plan.changes.Count; i++)
            {
                plan.changes[i].filter.sharedMesh = report.batches[i].replacementMesh;
                EditorUtility.SetDirty(plan.changes[i].filter);
            }
            foreach (var renderer in plan.direct)
            { renderer.enabled = false; EditorUtility.SetDirty(renderer); }
            EditorSceneManager.MarkSceneDirty(architecture.gameObject.scene);
            return report;
        }

        static Transform[] ValidateParts(Transform architecture, Transform[] parts)
        {
            if (!architecture || EditorUtility.IsPersistent(architecture) || !architecture.gameObject.scene.IsValid())
                throw new ArgumentException("A loaded architecture scene instance is required.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Replace visuals outside Play mode.");
            if (parts == null || parts.Length == 0 || parts.Any(p => !p || p == architecture || !p.IsChildOf(architecture)))
                throw new ArgumentException("Select actual original furniture parts below architecture.", nameof(parts));
            return parts.Distinct().OrderBy(PathOf, StringComparer.Ordinal).ToArray();
        }

        static Plan Prepare(Transform root, Transform[] parts)
        {
            var plan = new Plan { report = new Report { scene = root.gameObject.scene.path,
                partCount = parts.Length, sourcePartPaths = parts.Select(PathOf).ToArray(), targetDigest = Digest(root, parts) } };
            // Cylinder.fbx is the *old*, radius-one pCylinder1 mesh. CreatePrimitive
            // uses Cylinder from this library (88 vertices, extents .5/1/.5).
            var builtins = AssetDatabase.LoadAllAssetsAtPath("Library/unity default resources").OfType<Mesh>().ToArray();
            var cube = builtins.SingleOrDefault(m => m.name == "Cube" && m.vertexCount == 24);
            var cylinder = builtins.SingleOrDefault(m => m.name == "Cylinder" && m.vertexCount == 88);
            var sphere = builtins.SingleOrDefault(m => m.name == "Sphere" && (m.bounds.size - Vector3.one).sqrMagnitude < .000001f);
            if (!cube || !cylinder) throw new InvalidOperationException("The saved school's canonical Cube/Cylinder meshes are unavailable.");
            var faces = new List<Face>();
            var grid = new Dictionary<Vector3Int, List<int>>();
            var sourceBounds = new Bounds(parts[0].position, Vector3.zero);

            for (int owner = 0; owner < parts.Length; owner++)
            {
                var part = parts[owner];
                var source = SourceMesh(part, cube, cylinder, sphere);
                if (!source || source.subMeshCount != 1 || source.GetTopology(0) != MeshTopology.Triangles)
                    throw new InvalidOperationException("Unsupported original furniture geometry: " + PathOf(part));
                var vertices = source.vertices;
                var indices = source.GetTriangles(0);
                plan.report.expectedTriangles += indices.Length / 3;
                var renderer = part.GetComponent<MeshRenderer>();
                var directFilter = part.GetComponent<MeshFilter>();
                if (renderer && renderer.enabled && part.gameObject.activeInHierarchy)
                {
                    // A direct source mesh is proof of its own complete geometry.
                    // It is disabled only after all combined source faces also match.
                    if (!directFilter || directFilter.sharedMesh != source || renderer.sharedMaterials.Length != 1)
                        throw new InvalidOperationException("Direct renderer does not match its original recipe: " + PathOf(part));
                    plan.direct.Add(renderer);
                    plan.report.removedTriangles += indices.Length / 3;
                    continue;
                }
                var matrix = part.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++)
                { vertices[i] = matrix.MultiplyPoint3x4(vertices[i]); sourceBounds.Encapsulate(vertices[i]); }
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var face = new Face { a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]], owner = owner };
                    int index = faces.Count; faces.Add(face);
                    var cell = Cell(face.Center);
                    if (!grid.TryGetValue(cell, out var list)) { list = new List<int>(); grid.Add(cell, list); }
                    list.Add(index);
                }
            }
            sourceBounds.Expand(Tolerance * 4);
            var matched = new bool[faces.Count];
            var batches = root.GetComponentsInChildren<MeshFilter>(true)
                .Where(f => f.name.StartsWith(BatchPrefix, StringComparison.Ordinal) && f.sharedMesh
                    && f.GetComponent<MeshRenderer>() && f.GetComponent<MeshRenderer>().enabled && f.gameObject.activeInHierarchy)
                .OrderBy(f => PathOf(f.transform), StringComparer.Ordinal).ToArray();
            foreach (var filter in batches)
            {
                var mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer.bounds.Intersects(sourceBounds)) continue;
                var change = new Change { filter = filter, original = mesh };
                var vertices = mesh.vertices;
                var matrix = filter.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                for (int subIndex = 0; subIndex < mesh.subMeshCount; subIndex++)
                {
                    if (mesh.GetTopology(subIndex) != MeshTopology.Triangles) continue;
                    // GetTriangles applies this submesh's base vertex. The
                    // retained positions remain exact even after earlier
                    // material-splitting passes created additional sections.
                    var sub = new SubMeshChange { index = subIndex, originalIndices = mesh.GetTriangles(subIndex) };
                    change.originalTriangles += sub.originalIndices.Length / 3;
                    for (int i = 0; i < sub.originalIndices.Length; i += 3)
                    {
                        var face = new Face { a = vertices[sub.originalIndices[i]], b = vertices[sub.originalIndices[i + 1]], c = vertices[sub.originalIndices[i + 2]] };
                        if (!sourceBounds.Contains(face.Center)) continue;
                        int match = FindMatch(face, faces, grid, matched, out bool duplicate);
                        if (duplicate)
                            throw new InvalidOperationException("Ambiguous coincident batch face: " + PathOf(filter.transform)
                                + " submesh " + subIndex + " triangle " + i / 3);
                        if (match < 0) continue;
                        matched[match] = true; sub.removedFaces.Add(i / 3); plan.report.removedTriangles++;
                    }
                    if (sub.removedFaces.Count > 0) change.subMeshes.Add(sub);
                }
                if (change.subMeshes.Count > 0) plan.changes.Add(change);
            }
            if (matched.Any(m => !m) || plan.report.expectedTriangles != plan.report.removedTriangles)
            {
                var missing = new int[parts.Length];
                for (int i = 0; i < faces.Count; i++) if (!matched[i]) missing[faces[i].owner]++;
                string samples = string.Join("; ", Enumerable.Range(0, parts.Length).Where(i => missing[i] > 0).Take(8)
                    .Select(i => PathOf(parts[i]) + " missing=" + missing[i]));
                throw new InvalidOperationException("No changes made: furniture face proof incomplete (" + plan.report.removedTriangles
                    + "/" + plan.report.expectedTriangles + "). " + samples);
            }
            plan.report.changedBatches = plan.changes.Count;
            plan.report.directRenderersDisabled = plan.direct.Count;
            plan.report.directRendererPaths = plan.direct.Select(r => PathOf(r.transform)).ToArray();
            plan.report.batches = plan.changes.Select(change => new BatchChange {
                filterPath = PathOf(change.filter.transform), originalMesh = change.original,
                originalMeshAssetPath = AssetDatabase.GetAssetPath(change.original),
                originalTriangles = change.originalTriangles, removedTriangles = change.RemovedTriangles,
                remainingTriangles = change.originalTriangles - change.RemovedTriangles,
                subMeshCount = change.original.subMeshCount,
                removedTrianglesBySubMesh = Enumerable.Range(0, change.original.subMeshCount)
                    .Select(i => change.subMeshes.Where(s => s.index == i).Sum(s => s.removedFaces.Count)).ToArray() }).ToArray();
            return plan;
        }

        static Mesh SourceMesh(Transform part, Mesh cube, Mesh cylinder, Mesh sphere)
        {
            switch (part.name)
            {
                // Exact static fixture recipes. Light objects and fixture holder
                // roots are deliberately absent; only their original mesh parts
                // may be removed from a saved batch.
                case "Luminária fluorescente":
                case "Sala — luminária fluorescente comprida": case "Sala — refletor da luminária":
                case "Sala — tubo fluorescente": case "Sala — aleta do refletor":
                case "Central — luminária retangular": case "Central — tubo fluorescente":
                case "Passarela — luminária sob cobertura": case "Hall — luminária suspensa":
                case "Escada — luminária de parede": case "WestBlock canopy — luminária fluorescente":
                case "Cantina — fixação de luminária":
                case "Ligação refeitório/bloco — soquete":
                case "Vestíbulo — carcaça de luminária sob telhado":
                case "Vestíbulo — difusor branco de luminária": case "Vestíbulo — fixação da luminária":
                case "Carcaça da luminária": case "Difusor emissivo": return cube;
                case "Refeitório — corpo de plafon": case "Refeitório — difusor luminoso": return cylinder;
                case "Pátio oeste — braço de arandela": case "Mesas de jogos — braço de arandela": return FrontCylinder(part);
                case "Suspensão" when part.parent && part.parent.name == "Luminária de teto — foto": return FrontCylinder(part);
                case "Ligação refeitório/bloco — lâmpada sob forro":
                case "Oeste alto — base da luminária oval": case "Oeste alto — luminária oval opalina":
                    if (!sphere) throw new InvalidOperationException("The canonical CreatePrimitive Sphere mesh is unavailable.");
                    return sphere;
                // Exact original WallFan recipes from SchoolClassroomPhotoPass:
                // toroidal FanRing, cuboid support/blades, and primitive pipes.
                case "Sala — aro do ventilador": return Photo("FanRing");
                case "Sala — suporte do ventilador": case "Sala — hélice do ventilador": return cube;
                case "Sala — motor do ventilador": case "Sala — grade do ventilador": return cylinder;
                case "Sala — assento plástico azul arredondado": return Photo("Seat");
                case "Sala — encosto plástico azul": return Photo("Back");
                case "Sala — tampo com cantos arredondados": return Photo("Tablet");
                case "Sala — pé tubular da cadeira": case "Sala — pé tubular dianteiro":
                case "Sala — suporte do encosto": case "Sala — travessa inferior":
                case "Sala — coluna da carteira": case "Sala — base estabilizadora da carteira":
                case "Sala — apoio do tampo": return cylinder;
                case "Sala — ponteira azul": case "Sala — borda de madeira do tampo":
                case "Cadeira — assento": case "Cadeira — encosto": case "Cadeira — estrutura":
                case "Assento gasto": case "Encosto gasto": case "Pé oxidado":
                case "Armazenamento — caixa": case "Biblioteca — livro": return cube;
                case "Vestíbulo — banco de madeira junto da cantina": return cube;
                // Exact cuboid board and sanitary recipes; mirrors, partitions and colliders stay.
                case "Sala — quadro verde": case "Sala — moldura lateral do quadro":
                case "Sala — moldura horizontal do quadro": case "Laboratório — quadro":
                case "Sanitário — base": case "Sanitário — assento": case "Sanitário — caixa":
                case "Sanitário — bancada de lavatórios": case "Sanitário — torneira": return cube;
                // SchoolArchitectureRepairPass.Table authors these exact cuboid parts.
                case "Biblioteca — mesa de leitura": case "Biblioteca — balcão de empréstimos": case "Pé metálico": return cube;
            }
            if ((part.name.StartsWith("Armazenamento — estante — ", StringComparison.Ordinal)
                || part.name.StartsWith("Biblioteca — estante de livros — ", StringComparison.Ordinal)
                || part.name.StartsWith("Laboratório — armário de material — ", StringComparison.Ordinal)
                || part.name.StartsWith("Apoio — armário — ", StringComparison.Ordinal))
                && (part.name.EndsWith("fundo", StringComparison.Ordinal) || part.name.EndsWith("lateral", StringComparison.Ordinal)
                    || part.name.EndsWith("prateleira", StringComparison.Ordinal))) return cube;
            throw new InvalidOperationException("Select known original furniture mesh parts, not roots/volumes: " + PathOf(part));
        }
        static Mesh Photo(string name)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(PhotoMeshes + name + ".asset");
            if (!mesh) throw new FileNotFoundException("Original photographic furniture mesh missing: " + name);
            return mesh;
        }

        // SchoolFrontPhotoProps uses this exact 12-sided, unit-height mesh,
        // rather than CreatePrimitive's height-two Cylinder. Keep the recipe
        // transient: proving saved source faces creates no asset/scene object.
        static Mesh FrontCylinder(Transform part)
        {
            if (!frontPhotoCylinder)
            {
                const int sides = 12;
                var vertices = new Vector3[sides * 2 + 2]; var indices = new int[sides * 12];
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    vertices[i] = new Vector3(Mathf.Cos(a) * .5f, -.5f, Mathf.Sin(a) * .5f);
                    vertices[sides + i] = vertices[i] + Vector3.up;
                    int next = (i + 1) % sides;
                    int[] face = { i, sides + i, next, next, sides + i, sides + next,
                        sides * 2, i, next, sides * 2 + 1, sides + next, sides + i };
                    Array.Copy(face, 0, indices, i * 12, 12);
                }
                vertices[sides * 2] = Vector3.down * .5f; vertices[sides * 2 + 1] = Vector3.up * .5f;
                frontPhotoCylinder = new Mesh { name = "Front photo cylinder — proof recipe", hideFlags = HideFlags.HideAndDontSave,
                    vertices = vertices, triangles = indices };
                frontPhotoCylinder.RecalculateBounds();
            }
            var filter = part.GetComponent<MeshFilter>();
            if (filter && filter.sharedMesh)
            {
                var actual = filter.sharedMesh;
                if (actual.vertexCount != frontPhotoCylinder.vertexCount || actual.subMeshCount != 1
                    || !actual.GetTriangles(0).SequenceEqual(frontPhotoCylinder.GetTriangles(0))
                    || actual.vertices.Where((v, i) => !Near(v, frontPhotoCylinder.vertices[i])).Any())
                    throw new InvalidOperationException("Front-photo cylinder recipe changed: " + PathOf(part));
                return actual;
            }
            return frontPhotoCylinder;
        }

        static int FindMatch(Face face, List<Face> source, Dictionary<Vector3Int, List<int>> grid, bool[] matched, out bool duplicate)
        {
            var cell = Cell(face.Center); int available = -1; bool seenMatched = false;
            // Neighbor cells avoid failures at quantization boundaries. Bounds/grid
            // merely accelerate lookup; all three vertices must actually correspond.
            for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
            {
                if (!grid.TryGetValue(cell + new Vector3Int(x, y, z), out var candidates)) continue;
                foreach (int candidate in candidates)
                    if (SameFace(face, source[candidate]))
                    { if (matched[candidate]) seenMatched = true; else if (available < 0) available = candidate; }
            }
            duplicate = available < 0 && seenMatched;
            return available;
        }
        static bool SameFace(Face a, Face b) =>
            (Near(a.a, b.a) && ((Near(a.b, b.b) && Near(a.c, b.c)) || (Near(a.b, b.c) && Near(a.c, b.b))))
            || (Near(a.a, b.b) && ((Near(a.b, b.a) && Near(a.c, b.c)) || (Near(a.b, b.c) && Near(a.c, b.a))))
            || (Near(a.a, b.c) && ((Near(a.b, b.a) && Near(a.c, b.b)) || (Near(a.b, b.b) && Near(a.c, b.a))));
        static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= Tolerance * Tolerance;
        static Vector3Int Cell(Vector3 point) => new Vector3Int(Mathf.FloorToInt(point.x / CellSize), Mathf.FloorToInt(point.y / CellSize), Mathf.FloorToInt(point.z / CellSize));

        static void ValidateApplied(Transform root, Report report)
        {
            var nodes = root.GetComponentsInChildren<Transform>(true).ToDictionary(PathOf);
            foreach (var batch in report.batches ?? Array.Empty<BatchChange>())
            {
                if (!nodes.TryGetValue(batch.filterPath, out var node)) throw new InvalidOperationException("Recorded batch disappeared.");
                var filter = node.GetComponent<MeshFilter>();
                batch.originalMesh = AssetDatabase.LoadAssetAtPath<Mesh>(batch.originalMeshAssetPath);
                batch.replacementMesh = AssetDatabase.LoadAssetAtPath<Mesh>(batch.replacementMeshAssetPath);
                if (!filter || !batch.originalMesh || !batch.replacementMesh || filter.sharedMesh != batch.replacementMesh)
                    throw new InvalidOperationException("Recorded replacement does not match the loaded scene: " + batch.filterPath);
            }
            foreach (string path in report.directRendererPaths ?? Array.Empty<string>())
                if (!nodes.TryGetValue(path, out var node) || !node.GetComponent<MeshRenderer>() || node.GetComponent<MeshRenderer>().enabled)
                    throw new InvalidOperationException("Recorded direct source renderer was restored: " + path);
        }
        static string PathOf(Transform part)
        {
            var names = new Stack<string>();
            for (var p = part; p; p = p.parent) names.Push(p.name + "[" + p.GetSiblingIndex() + "]");
            return string.Join("/", names);
        }
        static string Digest(Transform root, Transform[] parts)
        {
            var values = new StringBuilder(root.gameObject.scene.path);
            foreach (var part in parts)
            {
                values.Append('\n').Append(PathOf(part)); var matrix = part.localToWorldMatrix;
                for (int i = 0; i < 16; i++) values.Append('|').Append(matrix[i].ToString("R", CultureInfo.InvariantCulture));
            }
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(values.ToString()))).Replace("-", "");
        }
        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            EnsureFolder(folder.Substring(0, slash)); AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }
    }
}
