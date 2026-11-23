// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
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
    /// <summary>Continues only the central stair; the original campus remains intact.</summary>
    public static class SchoolFinalChaptersPass
    {
        public const string GroupName = "Escola — terraço e última saída";
        const string Data = "Assets/SchoolCEEP/Data/FinalChapters";
        const string Output = "C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/FinalChapters";
        const float West = -28.9531765f, East = -26.6826935f, South = 57.7680664f, North = 62.71542f;
        public const float TerraceHeight = 7.32f;
        const string AuthorizedOpening = "Central — mureta do vão superior da escada";
        static Transform group;
        static Material plaster, concrete, steel, dark, paper, ghost;

        public static string Install()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.name != "SchoolCEEP")
                throw new InvalidOperationException("A clean saved school outside Play is required.");
            var root = scene.GetRootGameObjects().Single(g => g.GetComponentInChildren<SchoolOpeningFlow>(true)).transform;
            if (!root.Find(GroupName)) Apply(root);
            RepairOpening(root);
            RefreshPresentation(root);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("School save failed.");
            return Audit();
        }

        public static void Apply(Transform root)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || root.gameObject.scene.name != "SchoolCEEP")
                throw new InvalidOperationException("Install the finale outside Play in the school.");
            if (root.Find(GroupName)) return;
            var game = root.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameSession>(true)).Single();
            var opening = root.GetComponentInChildren<SchoolOpeningFlow>(true);
            var sanity = root.GetComponentInChildren<SchoolSanityController>(true);
            var earlier = root.GetComponentInChildren<SchoolLeftBehindFlow>(true);
            if (!root.GetComponentInChildren<SchoolMemoriesFlow>(true) || !opening || !sanity || !earlier)
                throw new InvalidOperationException("The existing five chapters are required.");
            string before = SchoolChapterThreePass.ArchitectureSignature(root, AuthorizedOpening);
            Folder(Data); Folder(Data + "/Materials"); Directory.CreateDirectory(Output);
            // Prove the exact twelve original ceiling triangles BEFORE changing any references.
            var ceilingProof = OpenCubeVisual(root, "Escada — forro superior branco");
            var book = AssetDatabase.LoadAssetAtPath<StoryBook>(Data + "/SchoolCompleteStory.asset");
            if (!book) { book = ScriptableObject.CreateInstance<StoryBook>(); AssetDatabase.CreateAsset(book, Data + "/SchoolCompleteStory.asset"); }
            EditorUtility.CopySerialized(game.narrative.book, book); Configure(book);
            game.narrative.book = book; game.narrative.continueSameSceneChapters = true; game.narrative.autoLoadNextChapter = false;
            EditorUtility.SetDirty(book); Record(game.narrative);
            group = new GameObject(GroupName).transform; group.SetParent(root, false);
            plaster = Existing("PhotoCentralWhite"); concrete = Existing("PhotoCentralStairTread"); steel = Existing("PhotoCentralRailSteel");
            dark = Material("OxidizedDoor", new Color(.16f, .18f, .17f), .65f);
            paper = Material("TimewornPaper", new Color(.50f, .43f, .29f));
            ghost = Material("PaleMemory", new Color(.48f, .52f, .49f), .05f);
            var terrace = group.gameObject.AddComponent<SchoolTerraceFlow>();
            terrace.persistentId = SchoolTerraceFlow.PersistentId; terrace.director = game.narrative;
            terrace.player = game.player.transform; terrace.playerCamera = game.player.view.playerCamera;
            terrace.observationMask = ~LayerMask.GetMask("Player");
            StairAndTerrace(terrace);
            terrace.ascentVolume = Volume("Escada — início da subida ao terraço", new Vector3(West, 3.81f, 57.12f), new Vector3(1.68f, 2f, 1.10f));
            terrace.terraceVolume = Volume("Terraço — além do arco da porta", new Vector3(East, TerraceHeight, 55.70f), new Vector3(3.7f, 2.1f, 1.25f));
            terrace.descentVolume = Volume("Escada central — retorno ao térreo", new Vector3(West, .035f, 56.95f), new Vector3(1.70f, 2f, 1.18f));
            terrace.demonVisual = CloneImmaterial(earlier.demonVisual.gameObject, "Presença imóvel — terraço", new Vector3(-36, TerraceHeight + 1.59f, 56.30f));
            terrace.demonLook = Node("Presença — olhar", terrace.demonVisual.transform.position);
            terrace.ghostGuide = CloneImmaterial(opening.ghostVisual, "Criança — indicando a descida", new Vector3(East + 1.6f, TerraceHeight, 56.30f));
            terrace.childrenVisuals = Node("Crianças — lembrança ao fundo", Vector3.zero).gameObject;
            for (int i = 0; i < 4; i++)
            {
                var child = CloneImmaterial(opening.ghostVisual, "Lembrança imóvel " + (i + 1), new Vector3(-39.4f + i * .65f, TerraceHeight, 56.15f));
                child.transform.SetParent(terrace.childrenVisuals.transform, true);
                child.SetActive(true);
            }
            terrace.childrenVisuals.SetActive(false);
            terrace.distortionVisuals = Node("Escada — sombra de lembrança", Vector3.zero).gameObject;
            var message = Text("JOÃO...", new Vector3(-29.786f, 6.75f, 62.9f), Quaternion.Euler(0, -90, 0), new Vector2(1.2f, .4f), .22f, new Color(.26f, .23f, .18f));
            message.transform.SetParent(terrace.distortionVisuals.transform, true); terrace.distortionVisuals.SetActive(false);
            terrace.eventLight = Node("Terraço — memória de luz", new Vector3(East, 9.15f, 56.4f)).gameObject.AddComponent<Light>();
            terrace.eventLight.type = LightType.Point; terrace.eventLight.intensity = 9; terrace.eventLight.range = 9;
            terrace.eventLight.color = new Color(.72f, .79f, 1); terrace.eventLight.enabled = false;
            terrace.memoryAnchor = Notebook("Âncora — promessa no caderno", "João: Eu posso lembrar sem ficar aqui.", new Vector3(-24.3f, TerraceHeight + .736f, 55.65f), sanity);
            Cube(group, "Terraço — banco antigo", new Vector3(-24.2f, TerraceHeight + .36f, 55.6f), new Vector3(1.6f, .72f, .48f), concrete, true);
            terrace.supplies = new[] { Battery(new Vector3(-24.7f, TerraceHeight + .746f, 55.6f), "school.chapter6.battery", game.narrative) };

            var final = group.gameObject.AddComponent<SchoolLastExitFlow>();
            final.persistentId = SchoolLastExitFlow.PersistentId; final.director = game.narrative;
            final.player = game.player.transform; final.playerCamera = game.player.view.playerCamera;
            final.observationMask = ~LayerMask.GetMask("Player");
            final.entranceDoors = root.GetComponentsInChildren<InteractiveDoor>(true).Where(d => d.persistentId == "school.entry.left" || d.persistentId == "school.entry.right").ToArray();
            if (final.entranceDoors.Length != 2) throw new InvalidOperationException("The real paired school entrance is required.");
            final.entranceVolume = Volume("Final — entrada por dentro do hall", new Vector3(.55f, .644f, 23), new Vector3(5.5f, 2f, 2.2f));
            final.outsideVolume = Volume("Final — exterior após a escadinha", new Vector3(.55f, .035f, 18), new Vector3(5.5f, 2f, 2.2f));
            final.phoneVolume = Volume("Final — telefone fora da escola", new Vector3(.55f, .035f, 15), new Vector3(5.5f, 2f, 2.2f));
            final.ghostGuide = CloneImmaterial(opening.ghostVisual, "Criança — último caminho pelo hall", new Vector3(.55f, .644f, 25.2f));
            final.distortionVisuals = Node("Final — marcas que parecem repetir o hall", Vector3.zero).gameObject;
            {
                var writing = Text("FIQUE", new Vector3(-4.408f, 2.35f, 27.54f), Quaternion.Euler(0, -90, 0), new Vector2(.85f, .38f), .21f, new Color(.12f, .11f, .085f));
                writing.transform.SetParent(final.distortionVisuals.transform, true);
            }
            final.distortionVisuals.SetActive(false);
            final.phoneVisual = Node("Telefone — última chamada na mão", Vector3.zero).gameObject;
            Phone(final.phoneVisual.transform); var phone = final.phoneVisual.AddComponent<SchoolFinalPhone>(); phone.flow = final;
            final.phoneVisual.SetActive(false);
            // A real window in the newly continued top stair enclosure, not a target hidden by a solid wall.
            final.windowLook = Node("Última janela — face exterior", new Vector3(West, 9.46f, South - .12f));
            final.windowSilhouette = CloneImmaterial(opening.ghostVisual, "Última janela — criança atrás do vidro", new Vector3(West, 8.80f, South + .22f));
            final.windowSilhouette.transform.localScale *= .64f;
            final.windowLight = Node("Última janela — luz breve", new Vector3(West, 9.09f, South + .10f)).gameObject.AddComponent<Light>();
            final.windowLight.type = LightType.Point; final.windowLight.color = new Color(1, .76f, .40f);
            final.windowLight.range = 5; final.windowLight.intensity = 20; final.windowLight.enabled = false;
            final.memoryAnchor = Notebook("Âncora — bilhete na entrada", "João: A saída está aqui. Isto ainda é real.", new Vector3(2.3f, 1.40f, 23.9f), sanity);
            Cube(group, "Entrada — apoio do bilhete", new Vector3(2.3f, 1.0f, 23.9f), new Vector3(.6f, .76f, .40f), concrete, true);
            final.supplies = new[] { Battery(new Vector3(2.15f, 1.396f, 23.9f), "school.final.battery", game.narrative) };
            Physics.SyncTransforms();
            RepairOpening(root);
            RefreshPresentation(root);
            string after = SchoolChapterThreePass.ArchitectureSignature(root, AuthorizedOpening);
            if (before != after) throw new InvalidOperationException("Original physical architecture changed outside the authorized addition.");
            File.WriteAllText(Output + "/installation.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { originalArchitectureBefore = before,
                originalArchitectureAfter = after, authorizedAddition = GroupName, ceilingProof,
                terraceHeight = TerraceHeight, newStairTreads = 20, riserHeight = .1755f,
                newColliders = group.GetComponentsInChildren<Collider>(true).Length,
                entityColliders = new[] { terrace.demonVisual, terrace.ghostGuide, terrace.childrenVisuals, final.ghostGuide, final.windowSilhouette }
                    .Sum(g => g.GetComponentsInChildren<Collider>(true).Length) }, Newtonsoft.Json.Formatting.Indented));
        }

        static void StairAndTerrace(SchoolTerraceFlow flow)
        {
            float depth = (North - South) / 10, rise = (TerraceHeight - 3.81f) / 20;
            for (int i = 0; i < 10; i++)
            {
                Cube(group, "Terraço — degrau oeste " + (i + 1), new Vector3(West, 3.81f + (i + 1) * rise - .10f, South + (i + .5f) * depth), new Vector3(1.6764f, .20f, depth), concrete, true);
                Cube(group, "Terraço — degrau leste " + (i + 1), new Vector3(East, 5.565f + (i + 1) * rise - .10f, North - (i + .5f) * depth), new Vector3(1.67358f, .20f, depth), concrete, true);
            }
            Cube(group, "Terraço — patamar de retorno norte", new Vector3((West + East) * .5f, 5.485f, 63.275f), new Vector3(3.945f, .16f, 1.12f), concrete, true);
            // Extend the existing walls upward only. Thin stairs retain >3m clearance to the original flights.
            Wall("Escada — continuação oeste", -29.903f, 7.10f, 9.90f, 60.841f, .20f, 6.1468f);
            Wall("Escada — continuação leste", -25.7339f, 7.10f, 9.90f, 60.841f, .20f, 6.1468f);
            Cube(group, "Escada — continuação norte", new Vector3(-27.8186f, 8.5f, 63.9149f), new Vector3(4.17f, 2.8f, .075f), plaster, true);
            Wall("Escada — divisória prolongada", -27.8172f, 7.10f, 8.50f, 60.2417f, .57149f, 4.94735f);
            Cube(group, "Escada — teto acima da nova subida", new Vector3(-27.8186f, 9.97f, 60.841f), new Vector3(4.40f, .14f, 6.1468f), plaster, true);
            float left = -30.003f, jambLeft = East - .75f, right = -25.6339f, jambRight = East + .75f;
            // South enclosure: one real window on the west and one connected door on the east.
            float wx0 = West - .62f, wx1 = West + .62f;
            Panel("Terraço — parede sul oeste", left, wx0, TerraceHeight, 9.90f);
            Panel("Terraço — parede sul central", wx1, jambLeft, TerraceHeight, 9.90f);
            Panel("Terraço — parede sul leste", jambRight, right, TerraceHeight, 9.90f);
            Panel("Última janela — peitoril", wx0, wx1, TerraceHeight, 8.56f);
            Panel("Última janela — verga", wx0, wx1, 9.62f, 9.90f);
            Panel("Porta do terraço — verga", jambLeft, jambRight, 9.68f, 9.90f);
            var glass = Material("LastWindowGlass", new Color(.20f, .27f, .30f, .27f)); Transparent(glass);
            Cube(group, "Última janela — vidro", new Vector3(West, 9.09f, South + .015f), new Vector3(1.17f, 1.06f, .016f), glass);
            foreach (float x in new[] { wx0, wx1, West }) Cube(group, "Última janela — montante", new Vector3(x, 9.09f, South), new Vector3(.04f, 1.06f, .06f), steel);
            foreach (float y in new[] { 8.56f, 9.09f, 9.62f }) Cube(group, "Última janela — travessa", new Vector3(West, y, South), new Vector3(1.24f, .035f, .06f), steel);
            flow.terraceDoor = MetalDoor(East - .70f, TerraceHeight, South);
            // Use the actual existing middle-corridor roof, rather than overlaying a second coincident floor.
            Cube(group, "Terraço — mureta sul", new Vector3(-27.7424f, TerraceHeight + .575f, 54.8932f), new Vector3(26.70f, 1.15f, .16f), plaster, true);
            Cube(group, "Terraço — mureta oeste", new Vector3(-41.0906f, TerraceHeight + .575f, 56.2906f), new Vector3(.16f, 1.15f, 2.795f), plaster, true);
            Cube(group, "Terraço — mureta leste", new Vector3(-14.3943f, TerraceHeight + .575f, 56.2906f), new Vector3(.16f, 1.15f, 2.795f), plaster, true);
            Cube(group, "Terraço — mureta norte oeste", new Vector3((-41.0906f + left) * .5f, TerraceHeight + .575f, South - .08f), new Vector3(left + 41.0906f, 1.15f, .16f), plaster, true);
            Cube(group, "Terraço — mureta norte leste", new Vector3((right - 14.3943f) * .5f, TerraceHeight + .575f, South - .08f), new Vector3(-14.3943f - right, 1.15f, .16f), plaster, true);
            foreach (float x in new[] { West - .72f, West + .72f }) Rail("Corrimão — subida oeste", new Vector3(x, 4.72f, South + .1f), new Vector3(x, 6.38f, North - .1f));
            foreach (float x in new[] { East - .72f, East + .72f }) Rail("Corrimão — subida leste", new Vector3(x, 6.46f, North - .1f), new Vector3(x, 8.22f, South + .1f));
            var sign = Text("TERRAÇO", new Vector3(East, 9.79f, South + .086f), Quaternion.Euler(0, 180, 0), new Vector2(1.1f, .20f), .12f, new Color(.13f, .14f, .12f));
        }
        static void Wall(string name, float x, float bottom, float top, float z, float width, float length)
            => Cube(group, name, new Vector3(x, (bottom + top) * .5f, z), new Vector3(width, top - bottom, length), plaster, true);
        static void Panel(string name, float x0, float x1, float bottom, float top)
            => Cube(group, name, new Vector3((x0 + x1) * .5f, (bottom + top) * .5f, South), new Vector3(x1 - x0, top - bottom, .16f), plaster, true);
        static InteractiveDoor MetalDoor(float x, float y, float z)
        {
            var root = Node("Porta metálica do terraço", new Vector3(x, y, z));
            var hinge = new GameObject("Dobradiça").transform; hinge.SetParent(root, false);
            var leaf = Cube(hinge, "Porta — folha com volume", new Vector3(.7f, 1.15f, 0), new Vector3(1.38f, 2.28f, .07f), dark, true);
            var door = root.gameObject.AddComponent<InteractiveDoor>(); door.persistentId = "school.chapter6.terrace.door";
            door.hinge = hinge; door.leafCollider = leaf.GetComponent<BoxCollider>(); door.locked = true; door.requiredKey = "";
            door.lockedMessage = "As vozes ainda vêm da escada. A porta não cede."; door.displayName = "porta do terraço";
            door.playerMask = LayerMask.GetMask("Player"); door.openAngle = 95; door.speed = 100;
            var source = UnityEngine.Object.FindObjectsByType<InteractiveDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(d => d != door && d.openSound);
            if (source) { door.openSound = source.openSound; door.closeSound = source.closeSound; door.latchSound = source.latchSound; door.lockedSound = source.lockedSound; }
            foreach (float dz in new[] { -.044f, .044f })
            {
                Cube(hinge, "Chapa rebaixada", new Vector3(.7f, 1.27f, dz), new Vector3(1.13f, 1.85f, .016f), dark);
                Cube(hinge, "Maçaneta metálica", new Vector3(1.20f, 1.04f, dz * 1.8f), new Vector3(.17f, .034f, .034f), steel);
                Cube(hinge, "Espelho da fechadura", new Vector3(1.25f, .99f, dz * 1.35f), new Vector3(.054f, .16f, .013f), steel);
            }
            foreach (float yy in new[] { .27f, 1.15f, 2.0f }) Cube(hinge, "Pino da dobradiça", new Vector3(.015f, yy, .04f), new Vector3(.04f, .13f, .04f), steel);
            foreach (float dx in new[] { -.045f, 1.445f }) Cube(root, "Batente conectado à parede", new Vector3(dx, 1.18f, 0), new Vector3(.11f, 2.36f, .21f), steel, true);
            Cube(root, "Batente superior", new Vector3(.70f, 2.34f, 0), new Vector3(1.60f, .08f, .21f), steel, true);
            return door;
        }

        static void Configure(StoryBook book)
        {
            var terrace = book.Chapter("terrace"); terrace.sceneName = "SchoolCEEP";
            terrace.initialObjective = "Voltar à escada central, atravessar o patamar do segundo andar e continuar a subida pela esquerda até a porta do terraço.";
            terrace.completionPhrase = "A escola nunca deixou João sair.";
            string[] objectives = { "Continuar subindo a escada central. As vozes vêm de cima.", "Abrir a porta metálica e atravessar para o terraço.",
                "Afastar-se da porta. Há algo no fundo do terraço.", "Olhar o terraço. Lembrar o que aconteceu naquela noite.",
                "Encontrar a criança e o caminho de volta à escada.", "Descer os dois andares da escada central até o corredor do térreo.", "Encontrar a última saída pelo hall principal." };
            for (int i = 0; i < terrace.milestones.Length; i++) terrace.milestones[i].objectiveAfter = objectives[i];
            var final = book.Chapter("last-exit"); final.sceneName = "SchoolCEEP";
            final.initialObjective = "Da escada central, seguir à direita pelo corredor até o hall. Voltar pela entrada da escola; a criança mostra o caminho.";
            final.completionPhrase = "Alguma coisa saiu com ele.";
            string[] last = { "Abrir o portão da entrada e descer a escadinha para fora da escola.", "Continuar pelo caminho coberto, afastando-se da entrada.",
                "Atender o telefone. Pressionar E.", "Seguir pelo jardim à esquerda da entrada. Olhar para a janela do último andar, acima da escada central.",
                "João saiu. Ou apenas se lembrou de sair?", "FIM" };
            for (int i = 0; i < final.milestones.Length; i++) final.milestones[i].objectiveAfter = last[i];
            var additions = new[] {
                new StorySequenceDefinition { id = "terrace.truth", lockPlayer = false, completionSignal = "terrace.cycle-revelation", lines = new[] {
                    Line("João", "Foi aqui. Eu consigo lembrar.", 3), Line("", "[Uma chamada escolar termina em silêncio.]", 3),
                    Line("João", "Eu prometi voltar para buscá-los. Mas nunca voltei.", 4),
                    Line("Criança", "Você saiu naquela noite. A gente ficou.", 4),
                    Line("João", "A ligação... esta escola queria que eu voltasse.", 4),
                    Line("", "[As crianças olham para um espaço vazio atrás de João.]", 3),
                    Line("Criança", "Ele precisa de alguém que lembre. Para o ciclo continuar.", 4),
                    Line("João", "Eu lembro de vocês. Mas não vou ficar.", 3) } },
                new StorySequenceDefinition { id = "terrace.unravel", lockPlayer = true, completionSignal = "terrace.final-chase", lines = new[] {
                    Line("", "[O prédio parece ceder. Não há ninguém se aproximando.]", 3),
                    Line("Criança", "Não siga as vozes. Volte pela escada.", 4), Line("João", "A porta está aberta.", 2) } }
            };
            var ids = additions.Select(s => s.id).ToArray(); book.sequences = book.sequences.Where(s => !ids.Contains(s.id)).Concat(additions).ToArray();
            var errors = book.ValidateContent(); if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors));
        }
        static StoryLine Line(string speaker, string text, float seconds) => new StoryLine { speaker = speaker, text = text, seconds = seconds };
        static GameObject Notebook(string name, string caption, Vector3 p, SchoolSanityController sanity)
        {
            var t = Node(name, p); Cube(t, "Caderno", Vector3.zero, new Vector3(.14f, .018f, .20f), paper);
            var c = t.gameObject.AddComponent<BoxCollider>(); c.size = new Vector3(.14f, .028f, .20f);
            var anchor = t.gameObject.AddComponent<SchoolMemoryAnchor>(); anchor.radius = 1.6f; anchor.recoveryPerSecond = .65f;
            var inspect = t.gameObject.AddComponent<SchoolAnchorInspection>(); inspect.title = name; inspect.caption = caption;
            sanity.anchors = sanity.anchors.Concat(new[] { anchor }).ToArray(); Record(sanity); t.gameObject.SetActive(false); return t.gameObject;
        }
        static CollectibleItem Battery(Vector3 p, string id, NarrativeDirector director)
        {
            var t = Node("Pilhas de reserva — " + id, p); var item = t.gameObject.AddComponent<CollectibleItem>();
            item.persistentId = id; item.kind = CollectibleKind.Battery; item.title = "Pilhas de reserva"; item.batterySeconds = 140;
            var c = t.gameObject.AddComponent<BoxCollider>(); c.size = new Vector3(.12f, .05f, .10f);
            for (int i = 0; i < 2; i++) { Cube(t, "Pilha", new Vector3((i - .5f) * .05f, 0, 0), new Vector3(.032f, .032f, .09f), dark);
                Cube(t, "Contato metálico", new Vector3((i - .5f) * .05f, 0, .046f), new Vector3(.032f, .032f, .008f), steel); }
            UnityEventTools.AddPersistentListener(item.onCollected, director.ScheduleSave); t.gameObject.SetActive(false); return item;
        }
        static void Phone(Transform t)
        {
            Cube(t, "Telefone antigo — corpo", Vector3.zero, new Vector3(.075f, .15f, .021f), dark);
            var screen = Material("PhoneScreen", new Color(.34f, .47f, .34f)); screen.EnableKeyword("_EMISSION"); screen.SetColor("_EmissionColor", new Color(.07f, .13f, .07f));
            Cube(t, "Tela — chamada recebida", new Vector3(0, .030f, -.012f), new Vector3(.06f, .05f, .004f), screen);
            for (int i = 0; i < 12; i++) Cube(t, "Tecla", new Vector3((i % 3 - 1) * .020f, -.010f - (i / 3) * .014f, -.014f), new Vector3(.011f, .007f, .004f), steel);
            Cube(t, "Antena", new Vector3(.025f, .094f, 0), new Vector3(.008f, .044f, .008f), dark);
        }
        static GameObject CloneImmaterial(GameObject source, string name, Vector3 p)
        {
            var copy = UnityEngine.Object.Instantiate(source, group); copy.name = name; copy.transform.position = p; copy.transform.rotation = Quaternion.identity;
            foreach (var c in copy.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            copy.SetActive(false); return copy;
        }
        static BoxCollider Volume(string name, Vector3 feet, Vector3 size)
        { var t = Node(name, feet + Vector3.up); var c = t.gameObject.AddComponent<BoxCollider>(); c.isTrigger = true; c.size = size; return c; }
        static Transform Node(string name, Vector3 p)
        { var g = new GameObject(name); g.transform.SetParent(group, false); g.transform.position = p; return g.transform; }
        static Transform Cube(Transform parent, string name, Vector3 p, Vector3 size, Material m, bool collision = false)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(parent, false); g.transform.localPosition = p; g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m; if (!collision) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>()); return g.transform;
        }
        static void Rail(string name, Vector3 a, Vector3 b)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = name; g.transform.SetParent(group, false);
            g.transform.position = (a + b) * .5f; g.transform.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            g.transform.localScale = new Vector3(.040f, (b - a).magnitude * .5f, .040f); g.GetComponent<Renderer>().sharedMaterial = steel;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        }
        static TextMeshPro Text(string value, Vector3 p, Quaternion rotation, Vector2 size, float fontSize, Color colour)
        {
            var t = Node("Texto — " + value, p); t.rotation = rotation; var text = t.gameObject.AddComponent<TextMeshPro>();
            text.text = value; text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/HorrorMenu/TMP/Resources/Fonts & Materials/LiberationSans SDF.asset");
            text.fontSize = fontSize; text.color = colour; text.alignment = TextAlignmentOptions.Center; text.rectTransform.sizeDelta = size;
            return text;
        }
        static Material Existing(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/SchoolCEEP/Materials/" + name + ".mat")
            ?? throw new InvalidOperationException("Missing existing material " + name);
        static Material Material(string name, Color colour, float metallic = 0)
        {
            string path = Data + "/Materials/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", colour); m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", .20f); m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
        }
        static void Transparent(Material m)
        { m.SetFloat("_Surface", 1); m.SetFloat("_SrcBlend", 5); m.SetFloat("_DstBlend", 10); m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000; }
        static void Record(UnityEngine.Object item)
        { EditorUtility.SetDirty(item); if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item); }
        static void Folder(string path)
        { if (AssetDatabase.IsValidFolder(path)) return; int split = path.LastIndexOf('/'); Folder(path.Substring(0, split)); AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1)); }

        /// <summary>Remove exactly the ceiling cube's proved world-space triangles from cloned combined meshes.</summary>
        static object OpenCubeVisual(Transform root, string sourceName)
        {
            var source = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == sourceName);
            var cube = AssetDatabase.LoadAllAssetsAtPath("Library/unity default resources").OfType<Mesh>().Single(m => m.name == "Cube" && m.vertexCount == 24);
            var direct = source.GetComponent<MeshRenderer>();
            if (direct && direct.enabled)
            {
                var directMesh = source.GetComponent<MeshFilter>().sharedMesh;
                var cv = cube.vertices; var ct = cube.triangles; var dv = directMesh.vertices; var dt = directMesh.triangles;
                var nativeFaces = Enumerable.Range(0, ct.Length / 3).Select(i => new[] { cv[ct[i * 3]], cv[ct[i * 3 + 1]], cv[ct[i * 3 + 2]] }).ToArray();
                if (dt.Length != 36 || Enumerable.Range(0, dt.Length / 3).Any(i => !nativeFaces.Any(f => Match(dv[dt[i * 3]], f) && Match(dv[dt[i * 3 + 1]], f) && Match(dv[dt[i * 3 + 2]], f))))
                    throw new InvalidOperationException("Expected exact native cube geometry for " + sourceName);
                direct.enabled = false; EditorUtility.SetDirty(direct);
                return new { exactSource = sourceName, directRenderer = true, removedTriangles = 12, originalMesh = AssetDatabase.GetAssetPath(cube) };
            }
            var vertices = cube.vertices.Select(source.TransformPoint).ToArray(); var indices = cube.triangles;
            var faces = Enumerable.Range(0, indices.Length / 3).Select(i => new[] { vertices[indices[i * 3]], vertices[indices[i * 3 + 1]], vertices[indices[i * 3 + 2]] }).ToArray();
            int[] counts = new int[faces.Length]; var edits = new List<Tuple<MeshFilter, int[]>>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.StartsWith("Superfícies agrupadas — ", StringComparison.Ordinal) && f.GetComponent<MeshRenderer>() && f.GetComponent<MeshRenderer>().enabled))
            {
                if (!mf.sharedMesh || mf.sharedMesh.subMeshCount != 1) continue;
                var mesh = mf.sharedMesh; var vv = mesh.vertices; var tt = mesh.triangles; var keep = new List<int>(tt.Length); bool found = false;
                for (int i = 0; i < tt.Length; i += 3)
                {
                    var a = mf.transform.TransformPoint(vv[tt[i]]); var b = mf.transform.TransformPoint(vv[tt[i + 1]]); var c = mf.transform.TransformPoint(vv[tt[i + 2]]);
                    int matching = -1;
                    for (int f = 0; f < faces.Length; f++)
                        if (Match(a, faces[f]) && Match(b, faces[f]) && Match(c, faces[f])) { matching = f; break; }
                    if (matching >= 0) { counts[matching]++; found = true; }
                    else { keep.Add(tt[i]); keep.Add(tt[i + 1]); keep.Add(tt[i + 2]); }
                }
                if (found) edits.Add(Tuple.Create(mf, keep.ToArray()));
            }
            if (counts.Any(c => c != 1)) throw new InvalidOperationException("Central ceiling exact-face proof failed: " + string.Join(",", counts));
            string folder = Data + "/RoofOpeningMeshes-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"); Folder(folder);
            var references = new List<object>(); int index = 0;
            foreach (var edit in edits)
            {
                var original = edit.Item1.sharedMesh; var clone = UnityEngine.Object.Instantiate(original); clone.name = original.name + " — central stair roof opening";
                clone.SetTriangles(edit.Item2, 0); clone.RecalculateBounds(); string path = folder + "/Opened-" + index++ + ".asset"; AssetDatabase.CreateAsset(clone, path);
                edit.Item1.sharedMesh = clone; EditorUtility.SetDirty(edit.Item1);
                references.Add(new { original = AssetDatabase.GetAssetPath(original), replacement = path, removedTriangles = (original.triangles.Length - edit.Item2.Length) / 3 });
            }
            return new { exactSource = source.name, removedTriangles = counts.Sum(), toleranceMetres = .0001f, references };
        }
        static bool Match(Vector3 value, Vector3[] face) => face.Any(p => (p - value).sqrMagnitude < .00000001f);
        static void RefreshPresentation(Transform root)
        {
            var terrace = root.GetComponentInChildren<SchoolTerraceFlow>(true); var final = root.GetComponentInChildren<SchoolLastExitFlow>(true);
            var truth = final.director.book.Sequence("terrace.truth"); truth.lockPlayer = false; EditorUtility.SetDirty(final.director.book);
            final.windowLook.position = new Vector3(West, 9.46f, South - .12f);
            final.windowSilhouette.transform.position = new Vector3(West, 8.80f, South + .22f);
            final.windowLight.transform.position = new Vector3(West, 9.09f, South + .10f);
            var own = terrace.transform;
            var sill = own.Find("Última janela — peitoril"); sill.position = new Vector3(West, (TerraceHeight + 8.56f) * .5f, South); sill.localScale = new Vector3(1.24f, 8.56f - TerraceHeight, .16f);
            var lintel = own.Find("Última janela — verga"); lintel.position = new Vector3(West, (9.62f + 9.90f) * .5f, South); lintel.localScale = new Vector3(1.24f, .28f, .16f);
            own.Find("Última janela — vidro").position = new Vector3(West, 9.09f, South + .015f);
            foreach (Transform item in own) if (item.name == "Última janela — montante") { item.position = new Vector3(item.position.x, 9.09f, South); item.localScale = new Vector3(.04f, 1.06f, .06f); }
            var bars = own.Cast<Transform>().Where(t => t.name == "Última janela — travessa").OrderBy(t => t.position.y).ToArray();
            for (int i = 0; i < bars.Length; i++) bars[i].position = new Vector3(West, new[] { 8.56f, 9.09f, 9.62f }[i], South);
            var memories = terrace.distortionVisuals.GetComponentsInChildren<TextMeshPro>(true);
            terrace.memoryAnchor.transform.position = new Vector3(-24.3f, TerraceHeight + .736f, 55.65f);
            var phone = final.phoneVisual.GetComponent<SchoolFinalPhone>();
            phone.ringSound = AssetDatabase.LoadAssetAtPath<SoundSet>("Assets/HorrorGameplay/Audio/Narrative/TelephoneRing.asset");
            phone.receiverSound = AssetDatabase.LoadAssetAtPath<SoundSet>("Assets/HorrorGameplay/Audio/Narrative/ReceiverPickup.asset");
            if (!phone.ringSound || !phone.receiverSound) throw new InvalidOperationException("The approved telephone sounds are required.");
            Record(phone);
            // The point light is behind the glass. A chapter-owned luminous interior makes the event readable outside.
            var silhouette = final.windowSilhouette.transform;
            var interior = silhouette.Find("Última janela — interior iluminado");
            if (!interior)
            {
                var glow = AssetDatabase.LoadAssetAtPath<Material>(Data + "/Materials/LastWindowInterior.mat");
                if (!glow) { glow = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(glow, Data + "/Materials/LastWindowInterior.mat"); }
                glow.SetColor("_BaseColor", new Color(2.4f, 1.4f, .47f)); EditorUtility.SetDirty(glow);
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = "Última janela — interior iluminado";
                cube.transform.position = new Vector3(West, 9.09f, South + .42f); cube.transform.localScale = new Vector3(1.10f, .99f, .018f);
                cube.GetComponent<Renderer>().sharedMaterial = glow; UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(silhouette, true);
                var shadow = AssetDatabase.LoadAssetAtPath<Material>(Data + "/Materials/LastWindowSilhouette.mat");
                if (!shadow) { shadow = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(shadow, Data + "/Materials/LastWindowSilhouette.mat"); }
                shadow.SetColor("_BaseColor", new Color(.018f, .022f, .019f)); EditorUtility.SetDirty(shadow);
                foreach (var renderer in silhouette.GetComponentsInChildren<Renderer>(true).Where(r => r.transform != cube.transform)) renderer.sharedMaterial = shadow;
            }
            else interior.position = new Vector3(West, 9.09f, South + .42f);
            foreach (var memory in memories) { memory.transform.position = new Vector3(-29.786f, 6.75f, 62.9f); memory.transform.rotation = Quaternion.Euler(0, -90, 0); }
            var marks = final.distortionVisuals.GetComponentsInChildren<TextMeshPro>(true);
            for (int i = 1; i < marks.Length; i++) UnityEngine.Object.DestroyImmediate(marks[i].gameObject);
            if (marks.Length > 0) { marks[0].transform.position = new Vector3(-4.408f, 2.35f, 27.54f); marks[0].transform.rotation = Quaternion.Euler(0, -90, 0); marks[0].rectTransform.sizeDelta = new Vector2(.85f, .38f); }
        }
        static void RepairOpening(Transform root)
        {
            // The old guard across the west flight has to yield to the authorized continuation.
            var barrier = root.GetComponentsInChildren<BoxCollider>(true).Single(c => c.name == AuthorizedOpening);
            if (!barrier.enabled) return;
            string before = SchoolChapterThreePass.ArchitectureSignature(root, AuthorizedOpening);
            var visualProof = OpenCubeVisual(root, AuthorizedOpening); barrier.enabled = false; EditorUtility.SetDirty(barrier);
            var terrace = root.GetComponentInChildren<SchoolTerraceFlow>(true); var final = root.GetComponentInChildren<SchoolLastExitFlow>(true);
            terrace.demonVisual.transform.position = new Vector3(-36, TerraceHeight + 1.59f, 56.3f); terrace.demonLook.position = terrace.demonVisual.transform.position;
            foreach (Transform child in terrace.childrenVisuals.transform) child.gameObject.SetActive(true);
            final.windowLook.position = new Vector3(West, 9.08f, South - .12f); final.windowSilhouette.transform.position = new Vector3(West, 8.30f, South + .22f);
            Physics.SyncTransforms(); string after = SchoolChapterThreePass.ArchitectureSignature(root, AuthorizedOpening);
            if (before != after) throw new InvalidOperationException("Geometry outside the authorized guard opening changed.");
            File.WriteAllText(Output + "/stair-guard-opening.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { authorizedCollider = AuthorizedOpening, visualProof,
                originalRestBefore = before, originalRestAfter = after, guardPosition = barrier.transform.position.ToString("F5"), guardSize = barrier.transform.lossyScale.ToString("F5") }, Newtonsoft.Json.Formatting.Indented));
        }
        public static string Audit()
        {
            var flow = UnityEngine.Object.FindAnyObjectByType<SchoolTerraceFlow>(FindObjectsInactive.Include);
            if (!flow) throw new InvalidOperationException("Final chapters missing."); var root = flow.transform.parent;
            return Newtonsoft.Json.JsonConvert.SerializeObject(new { scene = flow.gameObject.scene.path, dirty = flow.gameObject.scene.isDirty,
                originalArchitecture = SchoolChapterThreePass.ArchitectureSignature(root), protectedRestOfArchitecture = SchoolChapterThreePass.ArchitectureSignature(root, AuthorizedOpening), terraceHeight = TerraceHeight,
                sameSceneContinuation = flow.director.continueSameSceneChapters, chapters = flow.director.book.chapters.Select(c => new { c.id, c.sceneName }),
                storyErrors = flow.director.book.ValidateContent(), missingScripts = root.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),
                enemies = root.GetComponentsInChildren<EnemyBrain>(true).Length,
                duplicateIds = root.GetComponentsInChildren<PersistentObject>(true).GroupBy(p => p.persistentId).Where(g => g.Count() > 1).Select(g => g.Key).ToArray()
            }, Newtonsoft.Json.Formatting.Indented);
        }
    }
}
