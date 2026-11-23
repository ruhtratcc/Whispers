// Ferramenta de preparação ou verificação usada no Editor.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HorrorGame.Desktop;

namespace HorrorGame.Editor
{
    /// <summary>Explicit image slots only. No game/save-state APIs are used by this importer.</summary>
    public static class WhispersImageTools
    {
        const string Images = "Assets/ThirdParty/Images/";
        const string Incoming = Images + "Incoming/";
        const string BackupRoot = "Assets/_Substituidos/Imagens20261104/";
        const string ReportPath = "Assets/Docs/Imagens_Importacao.json";
        const string Data = "Assets/HorrorGameplay/Data/";
        static readonly string[] Scenes = {
            "Assets/ChapterOneOffice/Scenes/Chapter1Office.unity",
            "Assets/SchoolCEEP/Scenes/SchoolCEEP.unity"
        };

        [Serializable] public sealed class Entry
        {
            public string asset, field, image, state, detail;
            public Entry(string asset, string field, string image, string state, string detail = "")
            { this.asset = asset; this.field = field; this.image = image; this.state = state; this.detail = detail; }
        }
        [Serializable] public sealed class ImportReport
        {
            public string generatedUtc, scope = "Imagens: importação explícita; nenhum save pessoal ou estado de jogo acessado.";
            public bool completed;
            public List<Entry> entries = new List<Entry>();
            public List<string> extras = new List<string>(), errors = new List<string>();
        }

        [MenuItem("Tools/Imagens/Importar e ligar imagens")]
        static void ImportMenu() { Debug.Log(ImportAll()); }

        /// <summary>Callable from an Editor script. Aborts before mutation if any loaded scene is dirty.</summary>
        public static string ImportAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Pare o Play e aguarde a compilação antes de importar imagens.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Há uma cena com alterações não salvas. Nenhuma imagem foi alterada.");
            var report = new ImportReport { generatedUtc = DateTime.UtcNow.ToString("O") };
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ImportFiles(report);
                BindData(report);
                foreach (string path in Scenes) ProcessScene(path, report);
                // The morning office is a separate visual prefab; its case surfaces need the same explicit slots.
                ProcessMorningPrefab(report);
                ReportUnboundImages(report);
                AssetDatabase.SaveAssets();
                report.completed = report.errors.Count == 0;
            }
            catch (Exception exception)
            {
                report.errors.Add(exception.ToString());
                Debug.LogException(exception);
            }
            finally
            {
                // Never discard a dirty scene after an exception. A failed operation remains reviewable in Editor.
                bool dirty = false;
                for (int i = 0; i < SceneManager.sceneCount; i++) dirty |= SceneManager.GetSceneAt(i).isDirty;
                if (!dirty && !SameSetup(setup)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else if (dirty) report.errors.Add("Cena com alterações após falha: restauração automática adiada para não descartar trabalho.");
                EnsureFolder("Assets/Docs");
                Backup(ReportPath);
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
                AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceSynchronousImport);
            }
            return ReportPath;
        }

        static bool SameSetup(SceneSetup[] expected)
        {
            var actual = EditorSceneManager.GetSceneManagerSetup();
            return actual.Length == expected.Length && actual.Zip(expected, (a, b) =>
                a.path == b.path && a.isLoaded == b.isLoaded && a.isActive == b.isActive).All(value => value);
        }

        static Dictionary<string, string> KnownFiles()
        {
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] names = {
                "inspect_luan_drawing", "tex_investigation_wall", "wall_school_lit_4x3", "wall_school_lit_4x3_2048",
                "final_school_lit_16x9", "photo_school_facade_bw", "sign_seepe_facade",
                "tex_decal_cracks_mirror", "tex_decal_handprint_glass", "tex_decal_footprints_small",
                "icon_flashlight", "icon_whistle", "icon_lockpick", "icon_check", "icon_lock",
                "foto_escritorio", "foto_porta_secretaria", "foto_corredor"
            };
            foreach (string name in names) files[name + ".png"] = Images + name + ".png";
            foreach (string name in new[] { "inspect_helena_note", "inspect_helena_scribble", "inspect_report_2006", "inspect_news_2006", "secretary_footprint_mask" })
                files[name + ".png"] = Images + "InspectionDetails/" + name + ".png";
            for (int i = 1; i <= 3; i++)
            {
                string draw = "inspect_lunchbox_draw_" + i.ToString("00");
                files[draw + ".png"] = Images + "InspectionDetails/" + draw + ".png";
                foreach (string prefix in new[] { "tex_chalkboard_", "drawing_wall_" })
                    files[prefix + i.ToString("00") + ".png"] = Images + prefix + i.ToString("00") + ".png";
            }
            for (int i = 1; i <= 4; i++)
            {
                string name = "caso_" + i.ToString("00");
                files[name + ".png"] = Images + "Cases/" + name + ".png";
                name = "tex_decal_dirt_" + i.ToString("00");
                files[name + ".png"] = Images + name + ".png";
            }
            for (int i = 1; i <= 8; i++)
            {
                string name = "poster_school_" + i.ToString("00");
                files[name + ".png"] = Images + name + ".png";
            }
            files["attach_school_day_lit.jpg"] = Images + "attach_school_day_lit.jpg";
            files["attach_school_day_lit.png"] = Images + "attach_school_day_lit.png";
            foreach (string name in new[] { "flashlight", "whistle", "equipment", "lockpick", "phone" })
                files[name + "_placeholder.png"] = Data + "TutorialCards/" + name + "_placeholder.png";
            return files;
        }

        static void ImportFiles(ImportReport report)
        {
            var files = KnownFiles();
            EnsureFolder(Incoming.TrimEnd('/'));
            foreach (string source in Directory.GetFiles(Incoming).OrderBy(value => value, StringComparer.Ordinal))
            {
                string extension = Path.GetExtension(source).ToLowerInvariant();
                if (extension == ".meta") continue;
                string name = Path.GetFileName(source);
                if (!files.TryGetValue(name, out string destination)) { report.extras.Add(source); continue; }
                EnsureFolder(Path.GetDirectoryName(destination).Replace('\\', '/'));
                if (File.Exists(destination))
                {
                    Backup(destination);
                    // Leave the existing .meta in place: all current GUID references remain valid.
                    File.Copy(source, destination, true);
                    AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    report.entries.Add(new Entry(destination, "arquivo", destination, "atualizado", "GUID do destino preservado; Incoming mantido."));
                }
                else
                {
                    string error = AssetDatabase.MoveAsset(source.Replace('\\', '/'), destination);
                    if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                    report.entries.Add(new Entry(destination, "arquivo", destination, "movido", "GUID de Incoming preservado."));
                }
            }
            foreach (string destination in files.Values.Distinct())
            {
                if (File.Exists(destination)) Configure(destination, report);
                else if (!destination.Contains("TutorialCards/"))
                    report.entries.Add(new Entry(destination, "arquivo", destination, "faltando", "Slot opcional ou imagem ainda aguardada; nenhum conteúdo inventado."));
            }
        }

        static void Configure(string path, ImportReport report)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) { report.errors.Add("TextureImporter ausente: " + path); return; }
            Backup(path);
            string name = Path.GetFileNameWithoutExtension(path);
            bool icon = name.StartsWith("icon_", StringComparison.Ordinal);
            bool alpha = icon || name.StartsWith("tex_decal_", StringComparison.Ordinal) || name == "secretary_footprint_mask";
            importer.textureType = icon ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = icon ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 0;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = alpha;
            if (icon)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            }
            importer.SaveAndReimport();
            report.entries.Add(new Entry(path, "importSettings", path, "configurado", "NPOT None; sRGB; sem mipmaps; Clamp; aniso 0; máximo 2048."));
        }

        static Texture2D Image(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Images + name + ".png");
        static Texture2D Document(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Images + "InspectionDetails/" + name + ".png");
        static void BindData(ImportReport report)
        {
            BindInspection(Data + "Whistle/Discovery/HelenaNote.asset", Document("inspect_helena_note"), report, true);
            BindInspection(Data + "Whistle/Discovery/ScribbledWarning.asset", Document("inspect_helena_scribble"), report, true);
            BindInspection(Data + "Whistle/Discovery/LuanDrawing.asset", Image("inspect_luan_drawing"), report, true);
            AdjustLuanPrefab(report);
            BindInspection(Data + "BeforeDemolition/School/Report.asset", Document("inspect_report_2006"), report, false);
            BindInspection(Data + "BeforeDemolition/ClosedCaseReport.asset", Document("inspect_report_2006"), report, false);
            string lunchPath = Data + "BeforeDemolition/School/Lunchbox.asset";
            var lunch = AssetDatabase.LoadAssetAtPath<InspectionItemData>(lunchPath);
            var pages = Enumerable.Range(1, 3).Select(i => Document("inspect_lunchbox_draw_" + i.ToString("00"))).ToArray();
            if (lunch && pages.All(value => value))
            {
                Backup(lunchPath);
                lunch.pages = pages.Select((texture, index) => new InspectionPage { image = texture,
                    label = index == 2 ? "26/10/2026" : "Registro anual " + (index + 1) + " — ano ilegível", currentDate = index == 2 }).ToArray();
                EditorUtility.SetDirty(lunch);
                report.entries.Add(new Entry(lunchPath, "pages", "inspect_lunchbox_draw_01..03", "ligado", "Três páginas; somente 03 currentDate; itemId preservado; modelo da lancheira intocado."));
                for (int i = 0; i < pages.Length; i++)
                    report.entries.Add(new Entry(lunchPath, "pages[" + i + "].image", AssetDatabase.GetAssetPath(pages[i]), "ligado"));
            }
            else report.entries.Add(new Entry(lunchPath, "pages", "inspect_lunchbox_draw_01..03", "pendente", "Galeria atual preservada até as três imagens existirem."));

            string phonePath = Data + "PhoneMessages/Messages.asset";
            var phone = AssetDatabase.LoadAssetAtPath<PhoneMessageCatalog>(phonePath);
            if (phone)
                foreach (var pair in new[] { new[] { "secretary.photo", "foto_porta_secretaria" }, new[] { "loop.photo", "foto_corredor" } })
                {
                    var definition = phone.Find(pair[0]); var texture = Image(pair[1]);
                    if (definition != null && texture)
                    { Backup(phonePath); definition.image = texture; EditorUtility.SetDirty(phone); report.entries.Add(new Entry(phonePath, pair[0] + ".image", AssetDatabase.GetAssetPath(texture), "ligado")); }
                    else report.entries.Add(new Entry(phonePath, pair[0] + ".image", pair[1], "captura pendente", "A ferramenta de captura deve produzir a foto do próprio jogo."));
                }

            // The existing office photo is canonical. Preserve the active catalog and repair only its legacy duplicate.
            var cupboard = AssetDatabase.LoadAssetAtPath<Texture2D>(Data + "BeforeDemolition/foto_escritorio.png");
            foreach (string path in new[] { Data + "BeforeDemolition/BeforeDemolitionOfficeEmails.asset", "Assets/HorrorGameplay/Email/BeforeDemolitionEmails.asset" })
            {
                var catalog = AssetDatabase.LoadAssetAtPath<EmailCatalog>(path);
                var attachment = catalog ? catalog.Find("email.07")?.attachments?.FirstOrDefault(value => value != null && value.fileName == "foto_escritorio.jpg") : null;
                if (attachment != null && cupboard && !attachment.preview)
                { Backup(path); attachment.preview = cupboard; EditorUtility.SetDirty(catalog); }
                report.entries.Add(new Entry(path, "email.07/foto_escritorio.jpg", attachment != null && attachment.preview ? AssetDatabase.GetAssetPath(attachment.preview) : "", attachment != null && attachment.preview ? "preservado" : "pendente", "Disponibilidade e relatórios bloqueados preservados."));
            }
            report.entries.Add(new Entry(Images, "attach_school_day_lit", "attach_school_day_lit.jpg", "disponível sem slot", "Email 06 não tem anexo; estrutura narrativa preservada."));

            string desktopPath = Data + "CRTDesktop/DesktopContent.asset";
            var desktop = AssetDatabase.LoadAssetAtPath<DesktopContent>(desktopPath);
            var school = Image("wall_school_lit_4x3");
            if (desktop && school)
            {
                Backup(desktopPath); desktop.schoolWallpaper = school;
                foreach (var file in desktop.files ?? Array.Empty<DesktopFile>())
                    if (file != null && file.id == "photo.school") file.texture = school;
                EditorUtility.SetDirty(desktop);
                report.entries.Add(new Entry(desktopPath, "schoolWallpaper/files[photo.school].texture", AssetDatabase.GetAssetPath(school), "ligado", "Wallpaper paisagístico normal preservado."));
            }
            string newsPath = Data + "CRTDesktop/pagina_ceep_local_arquivo_quatro-alunos.asset";
            var news = AssetDatabase.LoadAssetAtPath<DesktopPage>(newsPath);
            var newsImage = Document("inspect_news_2006");
            if (news && newsImage)
            {
                var serialized = new SerializedObject(news); var property = serialized.FindProperty("image");
                if (property != null && property.propertyType == SerializedPropertyType.ObjectReference)
                { Backup(newsPath); property.objectReferenceValue = newsImage; serialized.ApplyModifiedPropertiesWithoutUndo(); report.entries.Add(new Entry(newsPath, "image", AssetDatabase.GetAssetPath(newsImage), "ligado")); }
                else report.entries.Add(new Entry(newsPath, "image", AssetDatabase.GetAssetPath(newsImage), "sem campo", "DesktopPage.image ainda não disponível nesta compilação."));
            }
            var ending = AssetDatabase.LoadAssetAtPath<EndingPresentationConfig>(Data + "Polish20261103/Endings.asset");
            var finalSchool = Image("final_school_lit_16x9");
            if (ending && finalSchool && IsSchool(ending.schoolImage))
            { Backup(AssetDatabase.GetAssetPath(ending)); ending.schoolImage = finalSchool; EditorUtility.SetDirty(ending); report.entries.Add(new Entry(AssetDatabase.GetAssetPath(ending), "schoolImage", AssetDatabase.GetAssetPath(finalSchool), "ligado")); }
            foreach (var pair in new[] { new[] { "LANTERNA", "icon_flashlight" }, new[] { "APITO", "icon_whistle" }, new[] { "GRAMPO", "icon_lockpick" } })
            {
                string path = Data + "Equipment/" + pair[0] + ".asset";
                var equipment = AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(path);
                var icon = AssetDatabase.LoadAssetAtPath<Sprite>(Images + pair[1] + ".png");
                if (equipment && icon) { Backup(path); equipment.icon = icon; EditorUtility.SetDirty(equipment); report.entries.Add(new Entry(path, "icon", Images + pair[1] + ".png", "ligado")); }
            }
            foreach (string id in new[] { "flashlight", "whistle", "equipment", "lockpick", "phone" })
            {
                string path = Data + "TutorialCards/" + id + ".asset";
                var card = AssetDatabase.LoadAssetAtPath<TutorialCardData>(path);
                report.entries.Add(new Entry(path, "image/fallbackImage", card && card.image ? AssetDatabase.GetAssetPath(card.image) : "", card && card.image ? "existente" : "pendente", id == "whistle" ? "Demo de vídeo ainda depende de gravação; imagem fallback preservada." : "Mídia de tutorial existente preservada."));
            }
        }

        static bool IsSchool(Texture2D texture)
        {
            if (!texture) return false;
            string path = AssetDatabase.GetAssetPath(texture);
            return path.IndexOf("FinalDeskPhoto", StringComparison.OrdinalIgnoreCase) < 0 &&
                (path.Contains("SchoolArchivePhoto") || path.Contains("wallpaper-after-school") || path.Contains("school_lit"));
        }

        static void BindInspection(string path, Texture2D texture, ImportReport report, bool dedicatedQuad)
        {
            var item = AssetDatabase.LoadAssetAtPath<InspectionItemData>(path);
            if (!item || !texture) { report.entries.Add(new Entry(path, "frontTexture", "", "pendente")); return; }
            Backup(path); item.frontTexture = texture; EditorUtility.SetDirty(item);
            report.entries.Add(new Entry(path, "frontTexture", AssetDatabase.GetAssetPath(texture), "ligado"));
            // The three whistle sheets are dedicated quads with one material; current inspection override requires two.
            if (dedicatedQuad && item.model)
            {
                var renderer = item.model.GetComponent<MeshRenderer>();
                if (renderer && renderer.sharedMaterials.Length == 1)
                    TextureMaterial(renderer.sharedMaterial, texture, report, path + "/model/paper");
            }
        }

        static void ProcessScene(string path, ImportReport report)
        {
            if (!File.Exists(path)) { report.errors.Add("Cena conhecida ausente: " + path); return; }
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Cena conhecida está com alterações não salvas: " + path);
            Backup(path);
            bool changed = false;
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    changed |= Cases(root, path, report);
                    changed |= Decorations(root, path, report);
                    foreach(var panel in root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Painel de cortiça"&&r.transform.parent&&r.transform.parent.name=="Biblioteca — investigação de Helena"))
                    {
                        var texture=Image("tex_investigation_wall");if(!texture)continue;
                        panel.sharedMaterial=ImageMaterial(panel.sharedMaterial,texture,"InvestigationBoard",report);
                        panel.sharedMaterial.SetTextureScale("_BaseMap",new Vector2(1,.8095f));panel.sharedMaterial.SetTextureOffset("_BaseMap",new Vector2(0,.09525f));
                        EditorUtility.SetDirty(panel);EditorUtility.SetDirty(panel.sharedMaterial);changed=true;
                        report.entries.Add(new Entry(path,"Painel de cortiça.material",AssetDatabase.GetAssetPath(texture),"ligado","Objetos e textos do mural preservados; recorte UV mantém proporção."));
                    }
                    foreach (var inspectable in root.GetComponentsInChildren<InspectableItem>(true))
                    {
                        if (!inspectable.item || !inspectable.item.frontTexture || !IsNewPaper(inspectable.item)) continue;
                        var visual = inspectable.visual ? inspectable.visual : inspectable.gameObject;
                        if (inspectable.item.itemId == "bd.whistle.lore.0")
                            foreach (var surface in visual.GetComponentsInChildren<MeshRenderer>(true))
                                if (surface.sharedMaterials.Length == 1 && surface.sharedMaterial &&
                                    AssetDatabase.GetAssetPath(surface.sharedMaterial) == Data + "Whistle/Discovery/LuanDrawing.mat")
                                    changed |= LuanAspect(surface.transform, true, path, report);
                        var renderer = visual.GetComponent<MeshRenderer>();
                        if (renderer && renderer.sharedMaterials.Length >= 2)
                            changed |= AssignFirstPaper(renderer, inspectable.item.frontTexture, path, report);
                    }
                    foreach (var mirror in root.GetComponentsInChildren<BeforeDemolitionMirrorFlow>(true))
                    {
                        var texture = Image("final_school_lit_16x9");
                        if (texture && IsSchool(mirror.finalPhoto) && mirror.finalPhoto != texture)
                        { mirror.finalPhoto = texture; EditorUtility.SetDirty(mirror); changed = true; report.entries.Add(new Entry(path, Hierarchy(mirror.transform) + ".finalPhoto", AssetDatabase.GetAssetPath(texture), "ligado")); }
                    }
                }
                if (changed)
                { EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Falha ao salvar " + path); }
            }
            finally { if (opened && !scene.isDirty) EditorSceneManager.CloseScene(scene, true); }
        }

        static bool IsNewPaper(InspectionItemData item) =>
            item.itemId == "bd.whistle.lore.0" || item.itemId == "bd.whistle.lore.1" || item.itemId == "bd.whistle.lore.2" ||
            item.itemId == "bd.report.2006" || item.itemId == "bd.case.report";

        static void AdjustLuanPrefab(ImportReport report)
        {
            string path = Data + "Whistle/Discovery/LuanDrawing.prefab";
            if (!Image("inspect_luan_drawing") || !File.Exists(path)) return;
            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(path);
                var renderer = contents.GetComponent<MeshRenderer>();
                if (!renderer || renderer.sharedMaterials.Length != 1 || !renderer.sharedMaterial ||
                    AssetDatabase.GetAssetPath(renderer.sharedMaterial) != Data + "Whistle/Discovery/LuanDrawing.mat")
                { report.entries.Add(new Entry(path, "aspecto", "inspect_luan_drawing", "não alterado", "Superfície dedicada não corresponde ao modelo auditado.")); return; }
                Backup(path);
                if (LuanAspect(contents.transform, false, path, report)) PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { if (contents) PrefabUtility.UnloadPrefabContents(contents); }
        }

        static bool LuanAspect(Transform surface, bool compensateParent, string owner, ImportReport report)
        {
            Vector3 scale = surface.localScale;
            Vector3 parentScale = compensateParent && surface.parent ? surface.parent.lossyScale : Vector3.one;
            if (Mathf.Abs(parentScale.y) < .00001f) return false;
            float height = Mathf.Sign(scale.y) * Mathf.Abs(scale.x * parentScale.x / parentScale.y) * .75f;
            if (Mathf.Approximately(scale.y, height)) return false;
            surface.localScale = new Vector3(scale.x, height, scale.z);
            EditorUtility.SetDirty(surface);
            report.entries.Add(new Entry(owner, Hierarchy(surface) + ".localScale.y", "inspect_luan_drawing", "ligado", "Aspecto 4:3; largura e profundidade preservadas; mundo compensa escala do pai."));
            return true;
        }

        static bool AssignFirstPaper(MeshRenderer renderer, Texture2D texture, string owner, ImportReport report)
        {
            var materials = renderer.sharedMaterials;
            if (materials.Length < 2 || !materials[0]) return false;
            materials[0] = ImageMaterial(materials[0], texture, "Paper-" + texture.name, report);
            renderer.sharedMaterials = materials;
            report.entries.Add(new Entry(owner, Hierarchy(renderer.transform) + ".sharedMaterials[0]", AssetDatabase.GetAssetPath(texture), "ligado"));
            return true;
        }

        static bool Cases(GameObject root, string owner, ImportReport report)
        {
            bool changed = false;
            foreach (var board in root.GetComponentsInChildren<Transform>(true).Where(value => value.name == "Mural de casos em aberto"))
            {
                var children = Enumerable.Range(0, board.childCount).Select(board.GetChild).ToArray();
                var photos = children.Where(value => value.name == "Foto sépia").OrderBy(value => value.localPosition.x).ToArray();
                if (photos.Length != 4) { report.entries.Add(new Entry(owner, Hierarchy(board), "caso_01..04", "não alterado", "Esperadas exatamente quatro fotos sépia diretamente filhas do mural.")); continue; }
                bool allBound = true;
                for (int i = 0; i < 4; i++)
                {
                    string imagePath = Images + "Cases/caso_" + (i + 1).ToString("00") + ".png";
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath); var renderer = photos[i].GetComponent<MeshRenderer>();
                    if (!texture || !renderer || !renderer.sharedMaterial) { allBound = false; report.entries.Add(new Entry(owner, Hierarchy(photos[i]), imagePath, "aguardando imagem")); continue; }
                    renderer.sharedMaterial = ImageMaterial(renderer.sharedMaterial, texture, "Caso-" + (i + 1).ToString("00"), report);
                    EditorUtility.SetDirty(renderer); changed = true;
                    report.entries.Add(new Entry(owner, Hierarchy(photos[i]) + " [x=" + photos[i].localPosition.x + "]", imagePath, "ligado", "Somente superfície fotográfica; papéis, textos e fios preservados."));
                }
                if (allBound)
                    foreach (var child in children.Where(value => value.name == "Silhueta na foto" || value.name == "Corpo na foto"))
                        if (child.gameObject.activeSelf) { child.gameObject.SetActive(false); changed = true; }
            }
            return changed;
        }

        static bool Decorations(GameObject root, string owner, ImportReport report)
        {
            bool changed = false; int drawingIndex = 0, boardIndex = 0;
            var art = Enumerable.Range(1, 8).Select(i => Image("poster_school_" + i.ToString("00")))
                .Concat(Enumerable.Range(1, 3).Select(i => Image("drawing_wall_" + i.ToString("00")))).Where(value => value).ToArray();
            var boards = Enumerable.Range(1, 3).Select(i => Image("tex_chalkboard_" + i.ToString("00"))).Where(value => value).ToArray();
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true).OrderBy(value => Hierarchy(value.transform), StringComparer.Ordinal))
            {
                if (!renderer.gameObject.activeInHierarchy || renderer.sharedMaterials.Length != 1 || !renderer.sharedMaterial) continue;
                bool drawing = renderer.name.Contains("Desenho infantil desbotado") ||
                    renderer.sharedMaterial.name.StartsWith("Desenho infantil desbotado", StringComparison.Ordinal)||renderer.sharedMaterial.name.StartsWith("Decor-",StringComparison.Ordinal);
                bool green = renderer.name == "Sala — quadro verde";
                var texture = drawing && art.Length > 0 ? art[drawingIndex++ % art.Length] : green && boards.Length > 0 ? boards[boardIndex++ % boards.Length] : null;
                if (!texture) continue;
                renderer.sharedMaterial = ImageMaterial(renderer.sharedMaterial, texture, (drawing ? "Decor-" : "Chalk-") + texture.name, report);
                EditorUtility.SetDirty(renderer); changed = true;
                report.entries.Add(new Entry(owner, Hierarchy(renderer.transform) + ".material", AssetDatabase.GetAssetPath(texture), "ligado", "Superfície decorativa existente; sem nova geometria ou alteração do mapa."));
            }
            if (art.Length > 0 && drawingIndex == 0) report.entries.Add(new Entry(owner, "decoração", "poster_school/drawing_wall", "sem superfície compatível", "Nenhuma geometria criada."));
            if (boards.Length > 0 && boardIndex == 0) report.entries.Add(new Entry(owner, "lousa", "tex_chalkboard_01..03", "sem superfície compatível", "Atlas Sketchfab misto preservado; não aplicar textura verde ao verso/moldura."));
            return changed;
        }

        static Material ImageMaterial(Material source, Texture2D texture, string name, ImportReport report)
        {
            string path = Images + "Materials/" + name + ".mat";
            EnsureFolder(Images + "Materials");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(source) { name = name }; AssetDatabase.CreateAsset(material, path); }
            else Backup(path);
            TextureMaterial(material, texture, report, path);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void TextureMaterial(Material material, Texture2D texture, ImportReport report, string owner)
        {
            if (!material || !texture) return;
            string path = AssetDatabase.GetAssetPath(material); if (!string.IsNullOrEmpty(path)) Backup(path);
            foreach (string property in new[] { "_BaseMap", "_MainTex" })
                if (material.HasProperty(property)) { material.SetTexture(property, texture); material.SetTextureScale(property, Vector2.one); material.SetTextureOffset(property, Vector2.zero); }
            EditorUtility.SetDirty(material);
            report.entries.Add(new Entry(owner, "material.BaseMap", AssetDatabase.GetAssetPath(texture), "ligado"));
        }

        static void ProcessMorningPrefab(ImportReport report)
        {
            string path = Data + "Polish20261103/MorningOffice.prefab";
            if (!File.Exists(path)) { report.entries.Add(new Entry(path, "casos", "", "prefab opcional ausente")); return; }
            Backup(path); GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(path);
                if (Cases(contents, path, report)) PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { if (contents) PrefabUtility.UnloadPrefabContents(contents); }
        }

        static void ReportUnboundImages(ImportReport report)
        {
            var bound = new HashSet<string>(report.entries.Where(value => value.state == "ligado" || value.state == "preservado")
                .Select(value => value.image), StringComparer.OrdinalIgnoreCase);
            foreach (string path in KnownFiles().Values.Distinct())
                if (File.Exists(path) && !bound.Contains(path) && !path.Contains("TutorialCards/"))
                {
                    string detail = "Imagem disponível; nenhum slot compatível confirmado nesta ferramenta. Sem criar geometria ou fatos narrativos.";
                    if (path.Contains("inspect_lunchbox_draw_")) detail = "Imagem da galeria, vinculada coletivamente em Lunchbox.pages quando as três páginas existem.";
                    if (path.Contains("secretary_footprint_mask")) detail = "GUID preservado; materiais existentes continuam referenciando esta máscara.";
                    if (path.Contains("tex_decal_")) detail = "Decal importado com alfa; efeitos de malha existentes preservados. Aplicação em superfície específica não presumida.";
                    report.entries.Add(new Entry(path, "uso", path, "disponível/revisar vínculo", detail));
                }
        }

        static string Hierarchy(Transform transform)
        {
            var names = new List<string>();
            for (; transform; transform = transform.parent) names.Add(transform.name);
            names.Reverse(); return string.Join("/", names);
        }

        static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new IOException("Pasta fora de Assets: " + path);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void Backup(string assetPath)
        {
            assetPath = assetPath.Replace('\\', '/');
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) || assetPath.StartsWith(BackupRoot, StringComparison.Ordinal)) return;
            string destination = BackupRoot + assetPath.Substring("Assets/".Length) + ".bak";
            if (File.Exists(assetPath) && !File.Exists(destination))
            { Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(assetPath, destination); }
            string meta = assetPath + ".meta", metaBackup = destination + ".meta.bak";
            if (File.Exists(meta) && !File.Exists(metaBackup))
            { Directory.CreateDirectory(Path.GetDirectoryName(metaBackup)); File.Copy(meta, metaBackup); }
        }
    }
}
