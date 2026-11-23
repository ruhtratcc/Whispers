// Ferramenta da escola para construção ou acabamento.

using System;
using System.IO;
using System.Linq;
using HorrorGame;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SchoolCEEP.Editor
{
    /// <summary>Authored memories in existing rooms. No school rebuild or Chapter 6 gameplay.</summary>
    public static class SchoolChapterFivePass
    {
        public const string GroupName = "Escola — capítulo 5 e memórias";
        const string Data = "Assets/SchoolCEEP/Data/ChapterFive";
        const string Output = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/ChapterFive";
        static Transform group;
        static Material body, metal, paper, ink, memoryMaterial;

        public static string Install()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.name != "SchoolCEEP")
                throw new InvalidOperationException("A clean saved school outside Play is required.");
            var root = scene.GetRootGameObjects().Single(g => g.GetComponentInChildren<SchoolOpeningFlow>(true)).transform;
            if (root.Find(GroupName)) return Audit();
            Apply(root);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("School save failed.");
            return Audit();
        }

        public static void Apply(Transform root)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || root.gameObject.scene.name != "SchoolCEEP")
                throw new InvalidOperationException("Install Chapter 5 outside Play in the school.");
            if (root.Find(GroupName)) return;
            string before = SchoolChapterThreePass.ArchitectureSignature(root);
            var game = root.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameSession>(true)).Single();
            var opening = root.GetComponentInChildren<SchoolOpeningFlow>(true);
            var previous = root.GetComponentInChildren<SchoolLeftBehindFlow>(true);
            var sanity = root.GetComponentInChildren<SchoolSanityController>(true);
            if (!previous || !opening || !sanity || !game.narrative)
                throw new InvalidOperationException("Chapter 4 integration is required.");
            Folder(Data);
            var book = AssetDatabase.LoadAssetAtPath<StoryBook>(Data + "/SchoolStoryThroughChapterFive.asset");
            if (!book) { book = ScriptableObject.CreateInstance<StoryBook>(); AssetDatabase.CreateAsset(book, Data + "/SchoolStoryThroughChapterFive.asset"); }
            EditorUtility.CopySerialized(game.narrative.book, book);
            Configure(book);
            game.narrative.book = book; game.narrative.continueSameSceneChapters = true; game.narrative.autoLoadNextChapter = false;
            EditorUtility.SetDirty(book); Record(game.narrative);

            group = new GameObject(GroupName).transform; group.SetParent(root, false);
            var flow = group.gameObject.AddComponent<SchoolMemoriesFlow>();
            flow.persistentId = "school.chapter5.memories";
            flow.director = game.narrative; flow.player = game.player.transform; flow.playerCamera = game.player.view.playerCamera;
            flow.observationMask = ~LayerMask.GetMask("Player"); flow.observationAngle = 26; flow.observationDistance = 12;
            flow.gazeSeconds = 1.1f; flow.memoryDuration = 2.4f;
            body = Material("RecorderCase", new Color(.042f, .046f, .042f), .05f);
            metal = Material("RecorderMetal", new Color(.39f, .40f, .37f), .75f);
            paper = Material("MemoryPaper", new Color(.48f, .43f, .30f));
            ink = Material("MemoryInk", new Color(.075f, .059f, .041f));
            memoryMaterial = Material("MemorySilhouettes", new Color(.54f, .45f, .30f, .45f));
            Transparent(memoryMaterial);

            var benches = root.GetComponentsInChildren<BoxCollider>(true).Where(c => c.name == "Laboratório — bancada técnica").ToArray();
            var archiveBenches = benches.Where(c => c.transform.parent.name == "Informática — laboratório B — função escolar").OrderByDescending(c => c.bounds.center.z).ToArray();
            var projectBenches = benches.Where(c => c.transform.parent.name == "Sala de projetos — função escolar").OrderByDescending(c => c.bounds.center.z).ToArray();
            if (archiveBenches.Length < 2 || projectBenches.Length < 1) throw new InvalidOperationException("Existing archive/project benches are required.");
            flow.firstRecording = Clue(flow, SchoolMemoriesClueKind.RecordingOne, "Gravador antigo — fita 1", OnBench(archiveBenches[0], .48f),
                "FITA 1 — TRANSCRIÇÃO\n\nSe alguém encontrar isso, não suba.\n\n[Silêncio]\n\nEle fica esperando lá em cima.\n\nJoão: Quem?\n\nA etiqueta da caixa indica outra gravação na Sala de projetos, ao fundo deste corredor, do lado oposto ao Depósito técnico.");
            Recorder(flow.firstRecording, "FITA 1");
            flow.secondRecording = Clue(flow, SchoolMemoriesClueKind.RecordingTwo, "Gravador antigo — fita 2", OnBench(projectBenches[0], .48f),
                "FITA 2 — TRANSCRIÇÃO\n\nJoão prometeu que voltaria.\n\n[O mecanismo para.]\n\nUma etiqueta remete à fotografia da turma no Arquivo antigo, sobre a segunda bancada.");
            Recorder(flow.secondRecording, "FITA 2");
            flow.photograph = Clue(flow, SchoolMemoriesClueKind.Photograph, "Fotografia da turma — a noite esquecida", OnBench(archiveBenches[1], .48f),
                "João reconhece o próprio rosto entre as crianças.\n\nEle estudou nesta escola. Estava aqui na noite dos desaparecimentos. As crianças foram atraídas para uma parte proibida do prédio. João conseguiu sair; as outras nunca voltaram.\n\nPor todos esses anos, ele esqueceu. Algo na escola continuou esperando.\n\nNo canto da fotografia: PROMETE QUE VOLTA?");
            Photograph(flow.photograph);

            var archiveFeet = Floor(new Vector3(-71.0835f, .60f, 39.3684f));
            flow.archiveVolume = Anchor("Arquivo — início das memórias", archiveFeet + Vector3.up * .9f).gameObject.AddComponent<BoxCollider>();
            flow.archiveVolume.isTrigger = true; flow.archiveVolume.size = new Vector3(3.6f, 2f, 3.4f);
            flow.memoryVisuals = Anchor("Lembrança intrusiva — crianças sem colisão", archiveFeet).gameObject;
            for (int i = 0; i < 3; i++)
            {
                var child = UnityEngine.Object.Instantiate(opening.ghostVisual, flow.memoryVisuals.transform);
                child.name = "Lembrança incompleta " + (i + 1); child.SetActive(true);
                child.transform.localPosition = new Vector3((i - 1) * .7f, 0, .5f + (i % 2) * .4f);
                child.transform.localRotation = Quaternion.Euler(0, 90, 0); child.transform.localScale *= .8f;
                foreach (var collider in child.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (var renderer in child.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = memoryMaterial;
            }
            flow.memoryVisuals.SetActive(false);
            flow.memoryLight = Anchor("Memória — luz quente por instantes", new Vector3(-70.8f, 3.25f, 41.2f)).gameObject.AddComponent<Light>();
            flow.memoryLight.type = LightType.Point; flow.memoryLight.color = new Color(1, .75f, .42f);
            flow.memoryLight.intensity = 14; flow.memoryLight.range = 6; flow.memoryLight.shadows = LightShadows.None; flow.memoryLight.enabled = false;

            var ghostFeet = Floor(new Vector3(-28.9532f, .05f, 56.9087f));
            flow.ghostGuide = UnityEngine.Object.Instantiate(opening.ghostVisual, group);
            flow.ghostGuide.name = "Criança — indicação da escada central";
            flow.ghostGuide.transform.position = ghostFeet; flow.ghostGuide.transform.rotation = Quaternion.identity;
            foreach (var collider in flow.ghostGuide.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            Primitive(flow.ghostGuide.transform, "Braço — apontando a subida", new Vector3(.22f, .82f, .20f), new Vector3(.09f, .09f, .38f), memoryMaterial);
            flow.ghostGuide.SetActive(false);
            flow.ghostLook = Anchor("Escada — olhar para a criança", ghostFeet + Vector3.up * 1.05f);

            var notebook = Anchor("Âncora — caderno da turma", OnBench(archiveBenches[1], -.35f));
            flow.memoryAnchor = notebook.gameObject;
            var anchor = notebook.gameObject.AddComponent<SchoolMemoryAnchor>(); anchor.radius = 1.6f; anchor.recoveryPerSecond = .65f;
            var inspection = notebook.gameObject.AddComponent<SchoolAnchorInspection>(); inspection.title = "Caderno da turma";
            inspection.caption = "João: Eu estou aqui. Agora. Preciso continuar.";
            var notebookCollider = notebook.gameObject.AddComponent<BoxCollider>(); notebookCollider.size = new Vector3(.14f, .025f, .18f);
            Primitive(notebook, "Caderno fechado", Vector3.zero, new Vector3(.14f, .02f, .18f), paper);
            notebook.gameObject.SetActive(false);
            sanity.anchors = sanity.anchors.Concat(new[] { anchor }).ToArray(); Record(sanity);
            flow.supplies = new[] { Battery(OnBench(archiveBenches[0], -.40f), 1, game.narrative),
                Battery(OnBench(projectBenches[0], -.40f), 2, game.narrative) };
            foreach (var supply in flow.supplies) supply.gameObject.SetActive(false);
            Physics.SyncTransforms();
            string after = SchoolChapterThreePass.ArchitectureSignature(root);
            if (before != after) throw new InvalidOperationException("Protected school architecture changed.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/installation.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { architectureBefore = before, architectureAfter = after,
                group = GroupName, clues = new[] { flow.firstRecording, flow.secondRecording, flow.photograph }.Select(c => new { c.title, position = new[] { c.transform.position.x, c.transform.position.y, c.transform.position.z } }),
                ghost = new[] { ghostFeet.x, ghostFeet.y, ghostFeet.z }, newColliders = group.GetComponentsInChildren<Collider>(true).Length,
                entitiesHaveColliders = flow.ghostGuide.GetComponentsInChildren<Collider>(true).Length + flow.memoryVisuals.GetComponentsInChildren<Collider>(true).Length
            }, Newtonsoft.Json.Formatting.Indented));
        }

        static void Configure(StoryBook book)
        {
            var chapter = book.Chapter("memories"); chapter.sceneName = "SchoolCEEP";
            chapter.initialObjective = "Entrar no Arquivo antigo, na porta que a chave abriu, e investigar as memórias.";
            chapter.completionPhrase = "As lembranças nunca saíram daquela escola.";
            string[] objectives = {
                "Investigar a fita 1 no gravador da primeira bancada do Arquivo antigo.",
                "Ouvir a primeira gravação.",
                "Encontrar a fita 2 na Sala de projetos, ao fundo do corredor do hospício, do lado oposto ao Depósito técnico.",
                "Ouvir a segunda gravação.",
                "Voltar ao Arquivo antigo e investigar a fotografia da turma, sobre a segunda bancada.",
                "Voltar pela saída do hospício e pelo corredor central até a escada principal. Encontrar a criança junto à subida.",
                "Observar a escada indicada pela criança.",
                "Subir para o terraço." };
            for (int i = 0; i < chapter.milestones.Length; i++) chapter.milestones[i].objectiveAfter = objectives[i];
            var additions = new[] {
                new StorySequenceDefinition { id = "memories.past-echo", lockPlayer = true, completionSignal = "memories.past-echoes", lines = new[] {
                    Line("Professora — lembrança", "Abram os cadernos. A chamada ainda não terminou.", 3),
                    Line("", "", 1.5f), Line("João", "Eu conheço esta sala...", 3) } },
                new StorySequenceDefinition { id = "memories.revelation", lockPlayer = true, completionSignal = "memories.childhood-recognized", lines = new[] {
                    Line("João", "Esse menino... sou eu.", 3), Line("João", "Eu estudava aqui. Eu estava aqui naquela noite.", 4),
                    Line("João", "Chamaram a gente para uma parte proibida do prédio.", 4), Line("", "", 1.5f),
                    Line("João", "Eu consegui sair. Eles não.", 4), Line("João", "Eu esqueci. Por que eu esqueci?", 3),
                    Line("", "Algo aqui nunca deixou de esperar.", 3) } }
            };
            var additionsIds = additions.Select(s => s.id).ToArray();
            book.sequences = book.sequences.Where(s => !additionsIds.Contains(s.id)).Concat(additions).ToArray();
            book.Chapter("terrace").sceneName = "";
            var errors = book.ValidateContent(); if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors));
        }
        static StoryLine Line(string speaker, string text, float seconds) => new StoryLine { speaker = speaker, text = text, seconds = seconds };
        public static string Audit()
        {
            var flow = UnityEngine.Object.FindAnyObjectByType<SchoolMemoriesFlow>(FindObjectsInactive.Include);
            if (!flow) throw new InvalidOperationException("Chapter 5 is missing.");
            return Newtonsoft.Json.JsonConvert.SerializeObject(new { scene = flow.gameObject.scene.path, dirty = flow.gameObject.scene.isDirty,
                architecture = SchoolChapterThreePass.ArchitectureSignature(flow.transform.parent),
                sameSceneContinuation = flow.director.continueSameSceneChapters,
                stopBeforeChapterSix = flow.director.book.Chapter("terrace").sceneName == "" && !flow.director.autoLoadNextChapter,
                storyErrors = flow.director.book.ValidateContent(),
                missingScripts = flow.transform.parent.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),
                enemies = flow.transform.parent.GetComponentsInChildren<EnemyBrain>(true).Length,
                duplicateIds = flow.transform.parent.GetComponentsInChildren<PersistentObject>(true).GroupBy(p => p.persistentId).Where(g => g.Count() > 1).Select(g => g.Key).ToArray()
            }, Newtonsoft.Json.Formatting.Indented);
        }
        static SchoolMemoriesClue Clue(SchoolMemoriesFlow flow, SchoolMemoriesClueKind kind, string title, Vector3 position, string content)
        {
            var root = Anchor(title, position); var clue = root.gameObject.AddComponent<SchoolMemoriesClue>();
            clue.flow = flow; clue.kind = kind; clue.title = title; clue.body = content;
            clue.visual = new GameObject("Objeto de memória"); clue.visual.transform.SetParent(root, false);
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = Vector3.up * .04f; collider.size = kind == SchoolMemoriesClueKind.Photograph ? new Vector3(.30f, .08f, .23f) : new Vector3(.26f, .10f, .18f);
            clue.SetVisible(false); return clue;
        }
        static void Recorder(SchoolMemoriesClue clue, string label)
        {
            var t = clue.visual.transform;
            Primitive(t, "Gravador — carcaça", new Vector3(0, .037f, 0), new Vector3(.25f, .074f, .17f), body);
            Primitive(t, "Tampa da fita", new Vector3(-.06f, .078f, .015f), new Vector3(.10f, .006f, .09f), metal);
            foreach (float x in new[] { -.084f, -.036f })
                Primitive(t, "Carretel visível", new Vector3(x, .083f, .015f), new Vector3(.021f, .004f, .021f), ink, PrimitiveType.Cylinder);
            for (int i = 0; i < 9; i++) Primitive(t, "Grade do alto-falante", new Vector3(.015f + i * .009f, .078f, .018f), new Vector3(.003f, .004f, .076f), metal);
            for (int i = 0; i < 4; i++) Primitive(t, "Botão mecânico", new Vector3(-.084f + i * .047f, .065f, -.071f), new Vector3(.03f, .014f, .02f), metal);
            Primitive(t, "Alça recolhida", new Vector3(0, .038f, .09f), new Vector3(.17f, .025f, .014f), metal);
            var text = Text(label, clue.transform.position + new Vector3(-.06f, .089f, .016f), Quaternion.Euler(90, 0, 0), new Vector2(.085f, .035f), .06f, Color.black);
            text.transform.SetParent(t, true);
        }
        static void Photograph(SchoolMemoriesClue clue)
        {
            var t = clue.visual.transform;
            Primitive(t, "Fotografia antiga — papel", Vector3.up * .006f, new Vector3(.30f, .008f, .22f), paper);
            Primitive(t, "Fotografia — fundo sepia", new Vector3(0, .013f, .024f), new Vector3(.26f, .004f, .14f), body);
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2) * .045f;
                Primitive(t, "Retrato — uniforme", new Vector3(x, .017f, .010f), new Vector3(.026f, .003f, .05f), metal);
                Primitive(t, "Retrato — criança", new Vector3(x, .020f, .046f), new Vector3(.023f, .003f, .025f), paper, PrimitiveType.Sphere);
            }
            var text = Text("PROMETE QUE VOLTA?", clue.transform.position + new Vector3(0, .020f, -.072f), Quaternion.Euler(90, 0, 0), new Vector2(.27f, .04f), .055f, new Color(.14f, .09f, .05f));
            text.transform.SetParent(t, true);
        }
        static CollectibleItem Battery(Vector3 position, int index, NarrativeDirector director)
        {
            var t = Anchor("Pilhas de reserva — memória " + index, position + Vector3.up * .02f);
            var item = t.gameObject.AddComponent<CollectibleItem>(); item.persistentId = "school.chapter5.battery." + index;
            item.kind = CollectibleKind.Battery; item.title = "Pilhas de reserva"; item.batterySeconds = 110;
            var c = t.gameObject.AddComponent<BoxCollider>(); c.size = new Vector3(.12f, .04f, .10f);
            for (int i = 0; i < 2; i++)
            {
                Primitive(t, "Pilha — corpo", new Vector3((i - .5f) * .05f, 0, 0), new Vector3(.032f, .032f, .09f), body);
                Primitive(t, "Pilha — contato", new Vector3((i - .5f) * .05f, 0, .047f), new Vector3(.032f, .032f, .014f), metal);
            }
            UnityEventTools.AddPersistentListener(item.onCollected, director.ScheduleSave);
            return item;
        }
        static Vector3 OnBench(BoxCollider bench, float offset)
        { var p = bench.bounds.center; p.x += offset; p.y = bench.bounds.max.y + .016f; return p; }
        static Vector3 Floor(Vector3 guess)
        {
            if (!Physics.Raycast(guess + Vector3.up * .5f, Vector3.down, out var hit, 2, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore) || hit.normal.y < .75f)
                throw new InvalidOperationException("No existing floor at " + guess);
            return hit.point;
        }
        static Transform Anchor(string name, Vector3 p) { var g = new GameObject(name); g.transform.SetParent(group, false); g.transform.position = p; return g.transform; }
        static Transform Primitive(Transform parent, string name, Vector3 p, Vector3 size, Material m, PrimitiveType type = PrimitiveType.Cube)
        {
            var g = GameObject.CreatePrimitive(type); g.name = name; g.transform.SetParent(parent, false); g.transform.localPosition = p; g.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>()); g.GetComponent<Renderer>().sharedMaterial = m; return g.transform;
        }
        static TextMeshPro Text(string value, Vector3 p, Quaternion q, Vector2 size, float fontSize, Color color)
        {
            var t = Anchor(value, p); t.rotation = q; var text = t.gameObject.AddComponent<TextMeshPro>(); text.text = value;
            text.font = TMP_Settings.defaultFontAsset; text.fontSize = fontSize; text.color = color; text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = size; text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false; return text;
        }
        static Material Material(string name, Color color, float metallic = 0)
        {
            string path = Data + "/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", .20f); EditorUtility.SetDirty(m); return m;
        }
        static void Transparent(Material m)
        {
            m.SetFloat("_Surface", 1); m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000; m.SetShaderPassEnabled("ShadowCaster", false); EditorUtility.SetDirty(m);
        }
        static void Record(UnityEngine.Object obj) { EditorUtility.SetDirty(obj); if (PrefabUtility.IsPartOfPrefabInstance(obj)) PrefabUtility.RecordPrefabInstancePropertyModifications(obj); }
        static void Folder(string path) { if (AssetDatabase.IsValidFolder(path)) return; string parent = path.Substring(0, path.LastIndexOf('/')); Folder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1)); }
    }
}
