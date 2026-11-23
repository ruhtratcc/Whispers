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
    /// <summary>Adds only Chapter 4 content to the finished school. No architectural rebuild.</summary>
    public static class SchoolChapterFourPass
    {
        public const string GroupName = "Escola — capítulo 4 e arquivo antigo";
        const string Data = "Assets/SchoolCEEP/Data/ChapterFour";
        const string Output = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/ChapterFour";
        static Transform group;
        static Material paper, ink, metal, shadow;
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
                throw new InvalidOperationException("Install Chapter 4 outside Play in the school.");
            if (root.Find(GroupName)) return;
            string before = SchoolChapterThreePass.ArchitectureSignature(root);
            var game = root.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameSession>(true)).Single();
            var opening = root.GetComponentInChildren<SchoolOpeningFlow>(true);
            if (!opening || !game.narrative || !root.GetComponentInChildren<SchoolSanityController>(true))
                throw new InvalidOperationException("Chapter 3 integration is required.");
            Folder(Data);
            var source = game.narrative.book;
            var book = AssetDatabase.LoadAssetAtPath<StoryBook>(Data + "/SchoolStoryThroughChapterFour.asset");
            if (!book) { book = ScriptableObject.CreateInstance<StoryBook>(); AssetDatabase.CreateAsset(book, Data + "/SchoolStoryThroughChapterFour.asset"); }
            EditorUtility.CopySerialized(source, book);
            Configure(book);
            game.narrative.book = book;
            game.narrative.continueSameSceneChapters = true;
            game.narrative.autoLoadNextChapter = false;
            EditorUtility.SetDirty(book); Record(game.narrative);
            group = new GameObject(GroupName).transform; group.SetParent(root, false);
            var flow = group.gameObject.AddComponent<SchoolLeftBehindFlow>();
            flow.persistentId = "school.chapter4.investigation";
            flow.director = game.narrative; flow.player = game.player.transform; flow.playerCamera = game.player.view.playerCamera;
            flow.observationMask = ~LayerMask.GetMask("Player");
            flow.ambience = root.GetComponentInChildren<SchoolHorrorAtmosphere>(true);
            flow.movementSound = AssetDatabase.LoadAssetAtPath<SoundSet>("Assets/HorrorGameplay/Audio/Recorded/Sets/Room creaks.asset");
            paper = Material("ArchivePaper", new Color(.55f, .49f, .33f));
            ink = Material("ArchiveInk", new Color(.09f, .075f, .05f));
            metal = Material("ArchiveKey", new Color(.31f, .24f, .12f), .72f);
            shadow = Material("StationaryPresence", new Color(.009f, .011f, .012f));
            var tables = root.GetComponentsInChildren<BoxCollider>(true).Where(c => c.name == "Apoio — mesa de trabalho").ToArray();
            var reportTable = tables.Single(c => c.transform.parent.name == "Grêmio estudantil — função escolar");
            var photoTable = tables.Single(c => c.transform.parent.name == "Sala de apoio aos alunos — função escolar");
            var p = reportTable.bounds.center; p.y = reportTable.bounds.max.y + .012f;
            flow.reportClue = Clue(flow, SchoolLeftBehindClueKind.Report, "Relatório da noite dos desaparecimentos", p + new Vector3(-.43f, 0, -.20f),
                "ARQUIVO DE OCORRÊNCIAS — CÓPIA INTERNA\n\nNa noite dos desaparecimentos, a chamada foi interrompida. As páginas seguintes foram arrancadas.\n\nEntre os nomes ainda legíveis: João de Claus. Uma anotação a lápis pede que a fotografia seja devolvida à Sala de apoio aos alunos.\n\nA chave do arquivo foi guardada no Depósito técnico, no fim deste corredor. A porta do arquivo fica na antiga Informática B.\n\nAlguém escreveu na margem: não apague meu nome.");
            Document(flow.reportClue, "OCORRÊNCIAS\nJOÃO DE CLAUS", new Vector2(.30f, .39f));
            p = photoTable.bounds.center; p.y = photoTable.bounds.max.y + .012f;
            flow.photographClue = Clue(flow, SchoolLeftBehindClueKind.Photograph, "Fotografia de João — aluno", p + new Vector3(-.43f, 0, -.20f),
                "Uma criança posa diante do corredor. O rosto parece familiar.\n\nNo verso: João de Claus — aluno.\n\nJoão: Eu não... Por que meu nome está aqui?\n\nA chave antiga permanece no Depósito técnico.");
            Document(flow.photographClue, "JOÃO DE CLAUS\nALUNO", new Vector2(.28f, .22f));
            var photoVisual = flow.photographClue.visual.transform;
            Primitive(photoVisual, "Retrato desbotado — uniforme", new Vector3(0, .008f, .015f), new Vector3(.058f, .003f, .075f), ink);
            Primitive(photoVisual, "Retrato desbotado — rosto", new Vector3(0, .012f, .065f), new Vector3(.041f, .002f, .048f), paper, PrimitiveType.Sphere);
            var keyFeet = SupportedFloor(new Vector3(-68.36212f, .60f, 19.80278f));
            var boxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/models/decoracao/cardboard_box_01/cardboard_box_01.prefab");
            if (!boxPrefab) throw new InvalidOperationException("The already approved cardboard model is required.");
            var box = (GameObject)PrefabUtility.InstantiatePrefab(boxPrefab, group);
            box.name = "Caixa de arquivo — apoio da chave";
            FitModel(box.transform, keyFeet, new Vector3(.55f, .44f, .48f));
            Physics.SyncTransforms();
            var boxBounds = box.GetComponentsInChildren<Renderer>(true)[0].bounds;
            foreach (var renderer in box.GetComponentsInChildren<Renderer>(true).Skip(1)) boxBounds.Encapsulate(renderer.bounds);
            var boxSupport = Anchor("Apoio físico — caixa do arquivo", boxBounds.center).gameObject.AddComponent<BoxCollider>();
            boxSupport.size = boxBounds.size;
            var keyPosition = new Vector3(keyFeet.x, boxBounds.max.y + .012f, keyFeet.z);
            flow.keyClue = Clue(flow, SchoolLeftBehindClueKind.Key, "Chave antiga do arquivo", keyPosition,
                "Uma chave gasta, com etiqueta ARQUIVO. Abre a antiga Informática B, perto da entrada do bloco.");
            Key(flow.keyClue);
            flow.returnVolume = Anchor("Corredor — retorno do depósito", new Vector3(-65.62372f, 1.60f, 32f)).gameObject.AddComponent<BoxCollider>();
            flow.returnVolume.isTrigger = true; flow.returnVolume.size = new Vector3(2.7f, 2.3f, 4f);
            flow.distantPose = Anchor("Presença — olhar distante", new Vector3(-65.62372f, 2.20f, 46));
            flow.closerPose = Anchor("Presença — apenas fora da visão", new Vector3(-65.62372f, 2.20f, 37));
            flow.reliefAnchor = Anchor("Corredor — retomar investigação", new Vector3(-65.62372f, .60f, 40));
            flow.demonVisual = Presence(flow.distantPose.position); flow.demonVisual.gameObject.SetActive(false);
            flow.wallAnchor = Anchor("Arquivo — inscrição observável", WallFace(2.14f, 45.25f));
            flow.wallMessage = Text("ELE LEMBROU\nDE VOCÊ.", flow.wallAnchor.position, Quaternion.Euler(0, -90, 0), new Vector2(1.8f, .7f), 1.7f, new Color(.55f, .51f, .37f)).gameObject;
            flow.wallMessage.name = "Inscrição — lembrança incerta"; flow.wallMessage.SetActive(false);
            flow.destinationDoor = root.GetComponentsInChildren<InteractiveDoor>(true).Single(d => d.persistentId == "school.door.0.58");
            flow.destinationDoor.displayName = "arquivo antigo";
            flow.destinationDoor.SetLocked(true); flow.destinationDoor.requiredKey = "";
            flow.destinationDoor.lockedMessage = "Arquivo trancado. Os registros do Grêmio podem explicar como entrar.";
            UnityEventTools.AddPersistentListener(flow.destinationDoor.onOpen, flow.DestinationOpened); Record(flow.destinationDoor);
            flow.photographRoomDoor = root.GetComponentsInChildren<InteractiveDoor>(true).Single(d => d.persistentId == "school.door.0.60");
            flow.ghostGuide = UnityEngine.Object.Instantiate(opening.ghostVisual, group);
            flow.ghostGuide.name = "Criança — indicação do arquivo";
            flow.ghostGuide.transform.position = new Vector3(-65.94f, .60f, 44.5f);
            flow.ghostGuide.transform.rotation = Quaternion.Euler(0, -90, 0); flow.ghostGuide.SetActive(false);
            foreach (var collider in flow.ghostGuide.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            var chairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/models/mobiliario/SchoolChair_01/SchoolChair_01.prefab");
            var chair = (GameObject)PrefabUtility.InstantiatePrefab(chairPrefab, group); chair.name = "Cadeira do arquivo — deslocamento incerto";
            FitModel(chair.transform, SupportedFloor(new Vector3(-72.5f, .6f, 33.0f)), new Vector3(.52f, .90f, .56f));
            flow.changedChair = chair.transform; flow.chairOriginal = chair.transform.position; flow.chairChanged = flow.chairOriginal + new Vector3(.38f, 0, -.24f);
            flow.eventLights = opening.schoolLights.Where(l => l && l.type != LightType.Directional && l.name.Contains("Luz")
                && Mathf.Abs(l.transform.position.x + 65.62372f) < 4 && l.transform.position.y < 5.6f)
                .OrderBy(l => l.transform.position.z).Take(3).ToArray();
            // Existing anchor system recovers sanity without an electric fixture.
            var memory = Anchor("Âncora — caderno com o próprio nome", keyPosition + new Vector3(.19f, 0, .05f));
            flow.memoryAnchor = memory.gameObject;
            var anchor = memory.gameObject.AddComponent<SchoolMemoryAnchor>(); anchor.radius = 1.5f; anchor.recoveryPerSecond = .65f;
            var inspection = memory.gameObject.AddComponent<SchoolAnchorInspection>(); inspection.title = "Caderno de João";
            inspection.caption = "João: O meu nome. Ainda é o meu nome.";
            var memoryVolume = memory.gameObject.AddComponent<BoxCollider>(); memoryVolume.size = new Vector3(.11f, .04f, .14f);
            Primitive(memory, "Caderno envelhecido", Vector3.zero, new Vector3(.11f, .018f, .14f), paper);
            memory.gameObject.SetActive(false);
            var sanity = root.GetComponentInChildren<SchoolSanityController>(true);
            sanity.anchors = sanity.anchors.Concat(new[] { anchor }).ToArray(); Record(sanity);
            Battery(flow, reportTable.bounds.center + new Vector3(.35f, reportTable.bounds.extents.y + .035f, -.28f), 1);
            Battery(flow, keyPosition + new Vector3(-.18f, .02f, .06f), 2);
            Text("ARQUIVO ANTIGO", WallFace(3.05f, 43.66f), Quaternion.Euler(0, -90, 0), new Vector2(1.4f, .20f), .85f, new Color(.42f, .39f, .28f));
            Physics.SyncTransforms();
            string after = SchoolChapterThreePass.ArchitectureSignature(root);
            if (before != after) throw new InvalidOperationException("Chapter 4 changed protected school collision geometry.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/installation.json", Newtonsoft.Json.JsonConvert.SerializeObject(new {
                architectureBefore = before, architectureAfter = after, group = GroupName,
                clues = new[] { flow.reportClue, flow.photographClue, flow.keyClue }.Select(c => new { c.title, position = new[] { c.transform.position.x, c.transform.position.y, c.transform.position.z } }),
                destination = flow.destinationDoor.persistentId, eventLights = flow.eventLights.Length,
                newColliders = group.GetComponentsInChildren<Collider>(true).Length,
                entitiesHaveColliders = flow.demonVisual.GetComponentsInChildren<Collider>(true).Length + flow.ghostGuide.GetComponentsInChildren<Collider>(true).Length
            }, Newtonsoft.Json.Formatting.Indented));
        }
        static void Configure(StoryBook book)
        {
            var chapter = book.Chapter("left-behind"); chapter.sceneName = "SchoolCEEP";
            chapter.heading = "CAPÍTULO 4"; chapter.completionPhrase = "A chave estava esperando por ele.";
            chapter.initialObjective = "Investigar o Grêmio no bloco antigo (‘hospício’), à esquerda no fim do corredor central. Objetivo e anotações: I.";
            string[] objectives = {
                "Investigar a fotografia na Sala de apoio aos alunos, ao lado do Grêmio.",
                "Encontrar a chave antiga no Depósito técnico, no fim deste corredor.",
                "Voltar pelo corredor do bloco antigo.",
                "Continuar pelo corredor. A figura não se move.",
                "Seguir em frente e retomar a investigação.",
                "Investigar a mensagem ao lado do Arquivo antigo, perto da entrada do bloco.",
                "Usar a chave na porta do Arquivo antigo (antiga Informática B).",
                "Explorar o arquivo e recuperar as memórias." };
            for (int i = 0; i < chapter.milestones.Length; i++) chapter.milestones[i].objectiveAfter = objectives[i];
            // Deliberately stop here. Chapter 5 is not implemented by this pass.
            book.Chapter("memories").sceneName = "";
            var errors = book.ValidateContent(); if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors));
        }
        public static string Audit()
        {
            var flow = UnityEngine.Object.FindAnyObjectByType<SchoolLeftBehindFlow>();
            if (!flow) throw new InvalidOperationException("Chapter 4 is missing.");
            var root = flow.transform.parent;
            return Newtonsoft.Json.JsonConvert.SerializeObject(new { scene = flow.gameObject.scene.name,
                dirty = flow.gameObject.scene.isDirty, architecture = SchoolChapterThreePass.ArchitectureSignature(root),
                sameSceneContinuation = flow.director.continueSameSceneChapters,
                stopBeforeChapterFive = flow.director.book.Chapter("memories").sceneName == "" && !flow.director.autoLoadNextChapter,
                storyErrors = flow.director.book.ValidateContent(), missingScripts = root.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),
                enemies = root.GetComponentsInChildren<EnemyBrain>(true).Length,
                duplicateIds = root.GetComponentsInChildren<PersistentObject>(true).GroupBy(p => p.persistentId).Where(g => g.Count() > 1).Select(g => g.Key).ToArray()
            }, Newtonsoft.Json.Formatting.Indented);
        }
        static SchoolLeftBehindClue Clue(SchoolLeftBehindFlow flow, SchoolLeftBehindClueKind kind, string title, Vector3 position, string body)
        {
            var root = Anchor(title, position);
            var clue = root.gameObject.AddComponent<SchoolLeftBehindClue>(); clue.flow = flow; clue.kind = kind; clue.title = title; clue.body = body;
            clue.visual = new GameObject("Objeto de investigação"); clue.visual.transform.SetParent(root, false);
            var volume = root.gameObject.AddComponent<BoxCollider>(); volume.size = kind == SchoolLeftBehindClueKind.Key ? new Vector3(.17f, .06f, .10f) : new Vector3(.30f, .025f, .39f);
            clue.SetVisible(false); return clue;
        }
        static void Document(SchoolLeftBehindClue clue, string text, Vector2 size)
        {
            Primitive(clue.visual.transform, "Papel dobrado e envelhecido", Vector3.zero, new Vector3(size.x, .006f, size.y), paper);
            var label = Text(text, clue.transform.position + Vector3.up * .016f, Quaternion.Euler(90, 0, 0), size * .9f, .20f, new Color(.12f, .09f, .055f));
            label.transform.SetParent(clue.visual.transform, true);
            for (int i = 0; i < 5; i++) Primitive(clue.visual.transform, "Linhas do registro", new Vector3(0, .005f, -.025f - i * .017f), new Vector3(size.x * .65f, .002f, .002f), ink);
        }
        static void Key(SchoolLeftBehindClue clue)
        {
            var parent = clue.visual.transform;
            Primitive(parent, "Chave — haste", new Vector3(.026f, .018f, 0), new Vector3(.09f, .013f, .013f), metal);
            foreach (float x in new[] { .038f, .062f }) Primitive(parent, "Chave — dentes gastos", new Vector3(x, .018f, .014f), new Vector3(.015f, .013f, .028f), metal);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6;
                var part = Primitive(parent, "Chave — aro metálico", new Vector3(-.047f + Mathf.Cos(a) * .023f, .018f, Mathf.Sin(a) * .023f), new Vector3(.016f, .013f, .009f), metal);
                part.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
            }
        }
        static void Battery(SchoolLeftBehindFlow flow, Vector3 position, int index)
        {
            var root = Anchor("Pilhas de reserva — arquivo " + index, position);
            var item = root.gameObject.AddComponent<CollectibleItem>(); item.persistentId = "school.chapter4.battery." + index;
            item.kind = CollectibleKind.Battery; item.title = "Pilhas de reserva"; item.batterySeconds = 110;
            var volume = root.gameObject.AddComponent<BoxCollider>(); volume.size = new Vector3(.12f, .06f, .10f);
            var dark = Material("ArchiveBattery", new Color(.025f, .028f, .023f), .1f);
            for (int i = 0; i < 2; i++)
            {
                Primitive(root, "Pilha — corpo", new Vector3((i - .5f) * .05f, 0, 0), new Vector3(.032f, .032f, .09f), dark);
                Primitive(root, "Pilha — contato", new Vector3((i - .5f) * .05f, 0, .047f), new Vector3(.032f, .032f, .014f), metal);
            }
            UnityEventTools.AddPersistentListener(item.onCollected, flow.director.ScheduleSave);
        }
        static Transform Presence(Vector3 p)
        {
            var t = Anchor("Presença imóvel — sem AI e sem collider", p);
            Primitive(t, "Cabeça indistinta", Vector3.zero, new Vector3(.28f, .35f, .27f), shadow, PrimitiveType.Sphere);
            Primitive(t, "Ombros", new Vector3(0, -.31f, 0), new Vector3(.52f, .23f, .27f), shadow, PrimitiveType.Capsule);
            Primitive(t, "Corpo na sombra", new Vector3(0, -.70f, 0), new Vector3(.41f, .57f, .26f), shadow, PrimitiveType.Capsule);
            foreach (float side in new[] { -1f, 1f })
            {
                Primitive(t, "Braço parado", new Vector3(side * .28f, -.68f, 0), new Vector3(.09f, .47f, .08f), shadow, PrimitiveType.Capsule);
                Primitive(t, "Perna imóvel", new Vector3(side * .10f, -1.28f, 0), new Vector3(.13f, .31f, .13f), shadow, PrimitiveType.Capsule);
            }
            return t;
        }
        static void FitModel(Transform root, Vector3 floor, Vector3 size)
        {
            root.position = Vector3.zero; root.localScale = Vector3.one;
            var renderers = root.GetComponentsInChildren<Renderer>(true); var bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            float scale = Mathf.Min(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            root.localScale = Vector3.one * scale;
            root.position = floor - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
        }
        static Vector3 SupportedFloor(Vector3 guess)
        {
            if (!Physics.Raycast(guess + Vector3.up * .40f, Vector3.down, out var hit, 2, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore) || hit.normal.y < .75f)
                throw new InvalidOperationException("No existing supported floor at " + guess);
            return hit.point;
        }
        static Vector3 WallFace(float height, float z)
        {
            var origin = new Vector3(-65.62372f, height, z);
            if (!Physics.Raycast(origin, Vector3.left, out var hit, 4, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("No existing archive wall at " + origin);
            return hit.point + hit.normal * .016f;
        }
        static Transform Anchor(string name, Vector3 p) { var go = new GameObject(name); go.transform.SetParent(group, false); go.transform.position = p; return go.transform; }
        static Transform Primitive(Transform parent, string name, Vector3 p, Vector3 size, Material m, PrimitiveType type = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = m; return go.transform;
        }
        static TextMeshPro Text(string content, Vector3 p, Quaternion q, Vector2 size, float fontSize, Color colour)
        {
            var t = Anchor(content, p); t.rotation = q;
            var text = t.gameObject.AddComponent<TextMeshPro>(); text.text = content; text.font = TMP_Settings.defaultFontAsset; text.fontSize = fontSize;
            text.color = colour; text.alignment = TextAlignmentOptions.Center; text.rectTransform.sizeDelta = size; text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false; return text;
        }
        static Material Material(string name, Color colour, float metallic = 0)
        {
            string path = Data + "/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", colour); mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Smoothness", .13f); EditorUtility.SetDirty(mat); return mat;
        }
        static void Record(UnityEngine.Object obj) { EditorUtility.SetDirty(obj); if (PrefabUtility.IsPartOfPrefabInstance(obj)) PrefabUtility.RecordPrefabInstancePropertyModifications(obj); }
        static void Folder(string path) { if (AssetDatabase.IsValidFolder(path)) return; string parent = path.Substring(0, path.LastIndexOf('/')); Folder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1)); }
    }
}
