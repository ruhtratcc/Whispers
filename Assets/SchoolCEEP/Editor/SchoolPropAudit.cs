// Ferramenta da escola de validação.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HorrorGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SchoolCEEP.Editor
{
    /// <summary>Read-only, bounded prop audit. Bounds are candidates, never proof that meshes penetrate.</summary>
    public static class SchoolPropAudit
    {
        [Serializable] public sealed class Issue
        {
            public string id, path, kind, reason, suggestion, otherId, confidence = "candidato";
            public Vector3 position;
        }
        [Serializable] public sealed class Report
        {
            public string scene, phase, utc, scope;
            public int renderersScanned, propsScanned, rigidbodiesScanned, candidatePairs, largePropsSkipped;
            public Bounds inferredFloorEnvelope;
            public bool complete, sceneChanged, personalSaveAccessed;
            public Issue[] issues;
            public string[] notes;
        }
        sealed class Prop
        {
            public Transform root;
            public Bounds bounds;
            public Renderer[] renderers;
            public string id, path, signature;
            public bool narrative;
        }
        const string DefaultOutput = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/Passe20261103";
        const float CellSize = 3;
        static readonly string[] PropWords = { "armário", "armario", "cabinet", "closet", "estante", "shelf", "mesa", "table", "desk", "carteira", "cadeira", "chair", "banco", "bench", "caixa", "box", "livro", "book", "papel", "paper", "caderno", "notepad", "lancheira", "lunch", "mochila", "backpack", "vaso", "pot", "balde", "bucket", "lixeira", "bin", "pilha", "battery", "grampo", "hairpin", "gaveta", "drawer", "escrivaninha", "relógio", "clock", "telefone", "phone", "ventilador", "fan", "computador", "computer" };
        static readonly string[] ArchitectureWords = { "parede", "wall", "piso", "floor", "teto", "roof", "cobertura", "telha", "forro", "viga", "beam", "coluna", "column", "pilar", "rampa", "ramp", "degrau", "step", "escada", "stair", "corrimão", "corrimao", "railing", "janela", "window", "porta", "door", "portão", "portao", "gate", "muro", "fence", "grade", "terreno", "ground", "grass", "mato", "árvore", "arvore", "tree", "tronco", "trunk", "shrubs", "fern" };
        static bool Has(string name, IEnumerable<string> words) => words.Any(w => name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);
        static bool Architecture(string name) => Has(name, ArchitectureWords);
        static string PathOf(Transform t)
        {
            var names = new List<string>();
            for (; t; t = t.parent) names.Add(t.name);
            names.Reverse(); return string.Join("/", names);
        }
        static string Id(Transform t)
        {
            var persistent = t.GetComponentInParent<PersistentObject>();
            if (persistent && !string.IsNullOrWhiteSpace(persistent.persistentId)) return persistent.persistentId;
            return GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).ToString();
        }
        static Transform RootOf(Renderer renderer)
        {
            Transform named = null;
            for (Transform t = renderer.transform; t; t = t.parent)
            {
                if (Architecture(t.name)) break;
                if (Has(t.name, PropWords)) named = t;
                if (t.GetComponent<PersistentObject>() || t.GetComponent<InspectableItem>()) return t;
            }
            if (named) return named;
            var prefab = PrefabUtility.GetNearestPrefabInstanceRoot(renderer.gameObject);
            if (prefab && !Architecture(prefab.name) && Has(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(prefab)), PropWords)) return prefab.transform;
            return null;
        }
        static bool IsOwn(Transform root, Transform candidate) => candidate == root || candidate.IsChildOf(root);
        static bool IsWall(Transform t)
        {
            for (int n = 0; t && n < 3; n++, t = t.parent)
                if (Has(t.name, new[] { "parede", "wall", "masonry", "alvenaria", "muro" })) return true;
            return false;
        }
        static float Volume(Vector3 size) => Mathf.Max(.00001f, size.x * size.y * size.z);
        static string TransformSignature(Transform t, Renderer[] renderers)
        {
            var meshes = renderers.Select(r =>
            {
                var filter = r.GetComponent<MeshFilter>();
                var skinned = r as SkinnedMeshRenderer;
                var mesh = filter ? filter.sharedMesh : skinned ? skinned.sharedMesh : null;
                return (mesh ? mesh.GetEntityId().ToString() : "none") + "@" + r.localToWorldMatrix.ToString("F4");
            }).OrderBy(v => v,StringComparer.Ordinal);
            Vector3 p = t.position, s = t.lossyScale; Quaternion q = t.rotation;
            return string.Join(",", meshes) + "|" + string.Join(",", new[] { p.x,p.y,p.z,q.x,q.y,q.z,q.w,s.x,s.y,s.z }.Select(v => v.ToString("F4", CultureInfo.InvariantCulture)));
        }
        static Issue Finding(Prop p, string kind, string reason, string suggestion, string other = null) => new Issue
        { id = p.id, path = p.path, kind = kind, position = p.root.position, reason = reason, suggestion = suggestion, otherId = other };

        [MenuItem("Tools/Whispers/Auditar objetos do mapa")]
        public static void AuditMenu()
        {
            var report = Audit();
            Debug.Log("Auditoria de objetos concluída: " + report.propsScanned + " objetos, " + report.issues.Length + " candidatos. Assets/Docs/Mapa_Problemas.md");
        }

        public static Report Audit(string phase = "manual", string outputDirectory = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Execute a auditoria fora do Play.");
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Abra a cena antes de auditar.");
            bool dirty = scene.isDirty;
            Physics.SyncTransforms();
            var renderers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(false))
                .Where(r => r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)).ToArray();
            var groups = new Dictionary<Transform, List<Renderer>>();
            foreach (var renderer in renderers)
            {
                var root = RootOf(renderer);
                if (!root || Architecture(root.name)) continue;
                if (!groups.TryGetValue(root, out var list)) groups[root] = list = new List<Renderer>();
                list.Add(renderer);
            }
            var props = new List<Prop>();
            foreach (var pair in groups)
            {
                var bounds = pair.Value[0].bounds;
                foreach (var renderer in pair.Value.Skip(1)) bounds.Encapsulate(renderer.bounds);
                if (bounds.size.sqrMagnitude < .00001f) continue;
                var array = pair.Value.ToArray();
                props.Add(new Prop { root = pair.Key, bounds = bounds, renderers = array, id = Id(pair.Key), path = PathOf(pair.Key),
                    narrative = pair.Key.GetComponentInParent<PersistentObject>() || pair.Key.GetComponentInParent<InspectableItem>(),
                    signature = TransformSignature(pair.Key, array) });
            }
            var ground = renderers.Where(r => Has(r.name, new[] { "piso", "floor", "terreno", "ground", "calçada", "calcada", "solo" })).ToArray();
            var envelopeSources = ground.Length > 0 ? ground : renderers;
            var envelope = envelopeSources.Length > 0 ? envelopeSources[0].bounds : new Bounds(Vector3.zero, Vector3.one);
            foreach (var renderer in envelopeSources.Skip(1)) envelope.Encapsulate(renderer.bounds);
            var report = new Report { scene = scene.path, phase = phase, utc = DateTime.UtcNow.ToString("o"), renderersScanned = renderers.Length,
                propsScanned = props.Count, inferredFloorEnvelope = envelope,
                scope = "Objetos ativos reconhecidos por prefab/nome/função. Interseções AABB são candidatos, não prova de malhas atravessadas; arquitetura e peças do mesmo objeto excluídas. Nenhuma correção automática ou acesso a saves." };
            var issues = new List<Issue>();
            var exact = new Dictionary<string, Prop>();
            var grid = new Dictionary<Vector2Int, List<int>>();
            for (int i = 0; i < props.Count; i++)
            {
                var p = props[i];
                if (exact.TryGetValue(p.signature, out var prior) && !IsOwn(p.root, prior.root) && !IsOwn(prior.root, p.root))
                    issues.Add(Finding(p, "duplicata exata", "Mesmas malhas e transform mundial de " + prior.path, "Conferir função; arquivar a duplicata em Assets/_Removidos antes de desativar.", prior.id));
                else exact[p.signature] = p;
                var min = p.bounds.min; var max = p.bounds.max;
                if (p.bounds.center.x < envelope.min.x-.25f || p.bounds.center.x > envelope.max.x+.25f || p.bounds.center.z < envelope.min.z-.25f || p.bounds.center.z > envelope.max.z+.25f || p.bounds.min.y < envelope.min.y-1)
                    issues.Add(Finding(p,"fora dos limites","Centro fora do envelope inferido dos pisos, ou abaixo dele.","Conferir limite físico e apoio antes de reposicionar."));
                CheckSupport(p,scene,issues);
                CheckCabinet(p,scene,issues);
                int x0 = Mathf.FloorToInt(min.x / CellSize), x1 = Mathf.FloorToInt(max.x / CellSize);
                int z0 = Mathf.FloorToInt(min.z / CellSize), z1 = Mathf.FloorToInt(max.z / CellSize);
                if ((long)(x1-x0+1)*(z1-z0+1) > 256) { report.largePropsSkipped++; continue; }
                for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
                {
                    var key = new Vector2Int(x,z);
                    if (!grid.TryGetValue(key,out var cell)) grid[key] = cell = new List<int>();
                    cell.Add(i);
                }
            }
            var seen = new HashSet<ulong>();
            foreach (var cell in grid.Values) for (int a = 0; a < cell.Count; a++) for (int b = a+1; b < cell.Count; b++)
            {
                int i = Math.Min(cell[a],cell[b]), j = Math.Max(cell[a],cell[b]);
                ulong key = ((ulong)(uint)i << 32) | (uint)j;
                if (!seen.Add(key)) continue;
                report.candidatePairs++;
                var p = props[i]; var q = props[j];
                if (IsOwn(p.root,q.root) || IsOwn(q.root,p.root) || !p.bounds.Intersects(q.bounds)) continue;
                Vector3 overlap = Vector3.Min(p.bounds.max,q.bounds.max)-Vector3.Max(p.bounds.min,q.bounds.min);
                if (overlap.x < .04f || overlap.y < .04f || overlap.z < .04f) continue;
                float fraction = Volume(overlap)/Mathf.Min(Volume(p.bounds.size),Volume(q.bounds.size));
                if (fraction < .35f) continue;
                issues.Add(Finding(p,"interseção grande de volumes",(fraction*100).ToString("F0",CultureInfo.InvariantCulture)+"% do menor AABB coincide com "+q.path+". Pode ser apoio legítimo; verificar visualmente.","Conferir interpenetração real; separar objetos independentes sem mover peças do mesmo móvel.",q.id));
            }
            var bodies = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Rigidbody>(false)).ToArray();
            report.rigidbodiesScanned = bodies.Length;
            foreach (var body in bodies) if (!body.isKinematic)
                issues.Add(new Issue { id=Id(body.transform),path=PathOf(body.transform),position=body.position,kind="Rigidbody dinâmico",reason="Corpo ativo não-kinematic; pode cair ou mover no início.",suggestion="Confirmar se a física é intencional; prender/kinematic apenas props estáticos, preservando interações." });
            report.issues = issues.OrderBy(i => i.path,StringComparer.Ordinal).ThenBy(i => i.kind,StringComparer.Ordinal).ToArray();
            report.notes = new[] { "Candidatos precisam de revisão: assentos sob mesas, livros em prateleiras e props suspensos podem ser intencionais.",
                "Apoio: cinco raios de até 0,65 m, ignorando colisores do próprio objeto. Não há teste de triângulos renderizados.",
                "Limites: envelope inferido de renderers de piso/solo, não substitui o percurso físico das bordas.",
                "Não altera transforms, corpos, IDs, flags, NavMesh, lightmaps ou saves. Objetos inativos são omitidos.",
                report.largePropsSkipped == 0 ? "Broadphase espacial completo para todos os props selecionados." : "Objetos acima de 256 células não entraram nas comparações de pares; revisar a classificação destes grupos." };
            report.complete = true; report.sceneChanged = scene.isDirty != dirty;
            Write(report,outputDirectory);
            return report;
        }

        static void CheckSupport(Prop p, Scene scene, List<Issue> issues)
        {
            if (Has(p.root.name,new[] { "relógio","clock","ventilador","fan","quadro","wall","pendur","suspens" })) return;
            var b = p.bounds;
            float x = b.extents.x*.65f, z = b.extents.z*.65f;
            foreach (var offset in new[] { Vector3.zero,new Vector3(x,0,z),new Vector3(-x,0,z),new Vector3(x,0,-z),new Vector3(-x,0,-z) })
            {
                var origin = new Vector3(b.center.x,b.min.y+.04f,b.center.z)+offset;
                foreach (var hit in Physics.RaycastAll(origin,Vector3.down,.65f,~0,QueryTriggerInteraction.Ignore))
                    if (hit.collider && hit.collider.gameObject.scene == scene && !IsOwn(p.root,hit.collider.transform) && hit.normal.y > .35f) return;
            }
            issues.Add(Finding(p,"sem apoio próximo","Nenhum apoio externo encontrado abaixo do volume visual nos cinco raios.","Conferir apoio visual e ajustar altura; não baixar objetos que devem estar pendurados."));
        }
        static void CheckCabinet(Prop p, Scene scene, List<Issue> issues)
        {
            if (p.narrative || !Has(p.root.name,new[] { "armário","armario","cabinet","estante","shelf" })) return;
            for (int i = 0; i < 8; i++)
            {
                var direction = Quaternion.Euler(0,i*45,0)*Vector3.forward;
                foreach (var hit in Physics.RaycastAll(p.bounds.center,direction,1.5f+Mathf.Max(p.bounds.extents.x,p.bounds.extents.z),~0,QueryTriggerInteraction.Ignore))
                    if (hit.collider && hit.collider.gameObject.scene == scene && !IsOwn(p.root,hit.collider.transform) && IsWall(hit.collider.transform)) return;
            }
            issues.Add(Finding(p,"armário/estante afastado","Sem parede reconhecida a até aproximadamente 1,5 m das bordas; nenhuma função narrativa identificada.","Conferir uso como divisória; se não for intencional, reposicionar junto a uma parede."));
        }
        static string Escape(string text) => (text ?? "").Replace("|","/").Replace("\r"," ").Replace("\n"," ");
        static void Write(Report report, string outputDirectory)
        {
            string docs = "Assets/Docs/Mapa_Problemas.md";
            Directory.CreateDirectory(Path.GetDirectoryName(docs));
            var builder = new StringBuilder();
            if (!File.Exists(docs)) builder.AppendLine("# Auditoria de objetos do mapa\n\nCandidatos de Editor: revisar antes de corrigir. Nenhuma correção automática.\n");
            builder.AppendLine("\n## "+report.phase+" — "+report.scene+" — "+report.utc);
            builder.AppendLine("\nProps: "+report.propsScanned+"; renderers: "+report.renderersScanned+"; pares únicos: "+report.candidatePairs+"; candidatos: "+report.issues.Length+".");
            builder.AppendLine("\n| ID persistente / GlobalObjectId | Caminho | Posição mundial | Tipo | Motivo | Sugestão | Outro ID |\n|---|---|---|---|---|---|---|");
            foreach (var i in report.issues) builder.AppendLine("| "+Escape(i.id)+" | "+Escape(i.path)+" | "+i.position.ToString("F3")+" | "+Escape(i.kind)+" | "+Escape(i.reason)+" | "+Escape(i.suggestion)+" | "+Escape(i.otherId)+" |");
            builder.AppendLine("\n"+string.Join("\n",report.notes.Select(n=>"- "+n)));
            File.AppendAllText(docs,builder.ToString(),new UTF8Encoding(false));
            string output = string.IsNullOrWhiteSpace(outputDirectory) ? DefaultOutput : outputDirectory;
            Directory.CreateDirectory(output);
            string phase = string.Concat((report.phase ?? "manual").Select(c => char.IsLetterOrDigit(c) || c=='-' || c=='_' ? c : '_'));
            string sceneName = Path.GetFileNameWithoutExtension(report.scene);
            File.WriteAllText(Path.Combine(output,"props-"+sceneName+"-"+phase+".json"),JsonUtility.ToJson(report,true),new UTF8Encoding(false));
        }
    }
}
