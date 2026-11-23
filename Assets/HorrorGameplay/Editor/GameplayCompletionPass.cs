// Ferramenta de preparação ou verificação usada no Editor.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HorrorGame;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HorrorGame.Editor
{
    /// <summary>Explicit, idempotent installation. No reload watcher or automatic scene edits.</summary>
    public static class GameplayCompletionPass
    {
        const string Office = "Assets/ChapterOneOffice/Scenes/Chapter1Office.unity";
        const string Test = "Assets/HorrorGameplay/Scenes/TestScene.unity";
        const string Prefab = "Assets/HorrorGameplay/Prefabs/GameplayRoot.prefab";
        const string AudioRoot = "Assets/HorrorGameplay/Audio/Narrative/";
        const string ReportPath = "Assets/ChapterOneOffice/Documentation/Gameplay-completion-report.json";
        static StoryBook book;
        static AudioClip panting;
        static SoundSet ring;
        [Serializable] public sealed class Report
        {
            public string generatedUtc, error;
            public bool applied, passed;
            public int chapters, milestones, sequences, renderers;
            public string[] errors;
        }
        public static string Schedule()
        {
            EditorApplication.update -= Execute;
            EditorApplication.update += Execute;
            return "Aplicação nativa agendada; relatório em Gameplay-completion-report.json.";
        }
        static void Execute()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= Execute;
            var report = new Report { generatedUtc = DateTime.UtcNow.ToString("O") };
            try
            {
                report.renderers = VintageCameraSetup.EnsureRendererFeatures();
                PrepareAudio();
                const string bookPath = "Assets/HorrorGameplay/Data/WhispersStory.asset";
                book = AssetDatabase.LoadAssetAtPath<StoryBook>(bookPath);
                if (!book) { book = ScriptableObject.CreateInstance<StoryBook>(); AssetDatabase.CreateAsset(book, bookPath); }
                var scenes = book.chapters?.ToDictionary(c => c.id, c => c.sceneName);
                StoryContent.Populate(book);
                if (scenes != null) foreach (var chapter in book.chapters)
                    if (scenes.TryGetValue(chapter.id, out var scene) && !string.IsNullOrWhiteSpace(scene)) chapter.sceneName = scene;
                EditorUtility.SetDirty(book);
                var catalog = AssetDatabase.LoadAssetAtPath<SceneCatalog>("Assets/HorrorGameplay/Data/SceneCatalog.asset");
                catalog.storyBook = book; catalog.gameplayScene = "Chapter1Office"; EditorUtility.SetDirty(catalog);
                var root = PrefabUtility.LoadPrefabContents(Prefab);
                try { ConfigureRoot(root, 0, true); PrefabUtility.SaveAsPrefabAsset(root, Prefab); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                WithScene(Test, scene => { foreach (var game in All<GameSession>(scene)) ConfigureRoot(game.gameObject, 1, false); });
                WithScene(Office, scene =>
                {
                    var game = All<GameSession>(scene).Single();
                    ConfigureRoot(game.gameObject, 0, true);
                    var director = game.narrative;
                    var flow = game.GetComponent<PrologueFlow>();
                    if (!flow) flow = game.gameObject.AddComponent<PrologueFlow>();
                    flow.director = director; flow.firstCallDelay = 6; flow.secondCallDelay = 4;
                    var phoneRoot = Named(scene, "Telefone — peça central do capítulo");
                    var telephone = phoneRoot.GetComponent<StoryTelephone>();
                    if (!telephone) telephone = phoneRoot.AddComponent<StoryTelephone>();
                    telephone.flow = flow; telephone.ringSound = ring; telephone.ringInterval = 2.8f;
                    var lift = AssetDatabase.LoadAssetAtPath<SoundSet>(AudioRoot + "ReceiverPickup.asset");
                    var place = AssetDatabase.LoadAssetAtPath<SoundSet>(AudioRoot + "ReceiverReplace.asset");
                    if (lift) telephone.pickupSound = lift;
                    if (place) telephone.hangupSound = place;
                    flow.telephone = telephone;
                    phoneRoot.layer = LayerMask.NameToLayer("Interactable");
                    var phoneCollider = phoneRoot.GetComponent<BoxCollider>();
                    if (!phoneCollider) phoneCollider = phoneRoot.AddComponent<BoxCollider>();
                    phoneCollider.center = new Vector3(0, .113f, 0); phoneCollider.size = new Vector3(.48f, .23f, .35f);
                    var kit = FindNamed(scene, "Equipamentos de investigação — prólogo");
                    if (!kit)
                    {
                        kit = new GameObject("Equipamentos de investigação — prólogo");
                        SceneManager.MoveGameObjectToScene(kit, scene);
                        kit.transform.SetParent(Named(scene, "Mesa principal — João de Claus").transform, true);
                        kit.transform.position = new Vector3(-.28f, .86f, 1.32f);
                        var cover = GameObject.CreatePrimitive(PrimitiveType.Cube); cover.name = "Pasta de investigação";
                        cover.transform.SetParent(kit.transform, false); cover.transform.localScale = new Vector3(.30f, .055f, .22f);
                        UnityEngine.Object.DestroyImmediate(cover.GetComponent<Collider>());
                        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/HorrorGameplay/Materials/InvestigationKit.mat");
                        if (!material)
                        {
                            material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.name = "Investigation kit";
                            material.SetColor("_BaseColor", new Color(.055f, .048f, .035f)); material.SetFloat("_Smoothness", .19f);
                            AssetDatabase.CreateAsset(material, "Assets/HorrorGameplay/Materials/InvestigationKit.mat");
                        }
                        cover.GetComponent<Renderer>().sharedMaterial = material;
                    }
                    kit.layer = LayerMask.NameToLayer("Interactable");
                    var kitCollider = kit.GetComponent<BoxCollider>(); if (!kitCollider) kitCollider = kit.AddComponent<BoxCollider>();
                    kitCollider.size = new Vector3(.32f, .075f, .24f);
                    var equipment = kit.GetComponent<StoryEquipment>(); if (!equipment) equipment = kit.AddComponent<StoryEquipment>();
                    equipment.director = director; equipment.persistentId = "office.story.equipment";
                    equipment.pickupSound = AssetDatabase.LoadAssetAtPath<SoundSet>("Assets/HorrorGameplay/Audio/Recorded/Sets/Paper pickup.asset");
                    PlaceEquipmentInDrawer(scene, equipment);
                    // Separate target so InteractiveDoor cannot consume the narrative exit interaction.
                    var exitRoot = FindNamed(scene, "Saída da investigação — prólogo");
                    if (!exitRoot)
                    {
                        exitRoot = new GameObject("Saída da investigação — prólogo"); SceneManager.MoveGameObjectToScene(exitRoot, scene);
                        exitRoot.transform.SetParent(Named(scene, "Porta do escritório").transform, true);
                    }
                    exitRoot.transform.position = new Vector3(-2.08f, 1.32f, -5.14f);
                    exitRoot.layer = LayerMask.NameToLayer("Interactable");
                    var exitCollider = exitRoot.GetComponent<BoxCollider>(); if (!exitCollider) exitCollider = exitRoot.AddComponent<BoxCollider>();
                    exitCollider.size = new Vector3(.78f, 1.85f, .04f); exitCollider.center = new Vector3(0, -.24f, 0);
                    var exit = exitRoot.GetComponent<StoryExit>(); if (!exit) exit = exitRoot.AddComponent<StoryExit>();
                    exit.director = director; exit.blockedMessage = "Preciso atender as ligações e pegar meus equipamentos antes de sair.";
                    var control = All<InteractableLightSwitch>(scene).Single();
                    control.alwaysOn = false; control.startsOn = true; control.SetState(true); EditorUtility.SetDirty(control);
                });
                RemoveRecordingAction();
                AssetDatabase.SaveAssets();
                report.applied = true;
                report.chapters = book.chapters.Length;
                report.milestones = book.chapters.Sum(c => c.milestones.Length);
                report.sequences = book.sequences.Length;
                report.errors = Validate().ToArray(); report.passed = report.errors.Length == 0;
            }
            catch (Exception error) { report.error = error.ToString(); report.errors = new[] { error.Message }; Debug.LogException(error); }
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            AssetDatabase.ImportAsset(ReportPath);
        }
        /// <summary>Parent to the moving drawer, never to a disposable visual group.</summary>
        public static void PlaceEquipmentInDrawer(Scene scene, StoryEquipment equipment)
        {
            var drawer = All<InteractiveDrawer>(scene).Single();
            equipment.transform.SetParent(drawer.drawer, false);
            equipment.transform.localPosition = new Vector3(0, -.035f, .16f);
            equipment.transform.localRotation = Quaternion.identity;
            equipment.transform.localScale = Vector3.one;
            equipment.containingDrawer = drawer;
            equipment.minimumDrawerOpening = .65f;
            foreach (var child in equipment.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
            EditorUtility.SetDirty(equipment);
        }
        static void ConfigureRoot(GameObject root, int chapterIndex, bool story)
        {
            var game = root.GetComponent<GameSession>() ?? root.GetComponentInChildren<GameSession>(true);
            if (!game) throw new InvalidOperationException("Gameplay root not found.");
            var hud = game.hud;
            foreach (var legacy in root.GetComponentsInChildren<RecordingOverlay>(true))
            {
                if (legacy.visuals) UnityEngine.Object.DestroyImmediate(legacy.visuals);
                UnityEngine.Object.DestroyImmediate(legacy);
            }
            if (hud.options)
            {
                if (hud.options.recording) UnityEngine.Object.DestroyImmediate(hud.options.recording.gameObject);
                hud.options.recording = null; hud.options.overlay = null;
            }
            var audio = root.GetComponentInChildren<AudioManager>(true);
            if (audio && audio.breathing) { audio.breathing.clip = panting; audio.breathing.loop = true; audio.breathing.volume = 0; audio.breathing.playOnAwake = false; }
            game.atmosphere.breathingEnabled = true;
            VintageCameraSetup.ClearDuplicateImageEffects(game.atmosphere.volume);
            var effect = VintageCameraSetup.Configure(game.player.view.playerCamera, chapterIndex);
            var director = game.GetComponent<NarrativeDirector>(); if (!director) director = game.gameObject.AddComponent<NarrativeDirector>();
            director.book = book; director.game = game; director.inventory = game.interaction.inventory;
            director.vintageCamera = effect; director.startingChapterId = "prologue"; director.enabled = story;
            game.narrative = story ? director : null; game.save.narrative = story ? director : null;
            director.transitionView = TransitionView(hud, game);
            foreach (var text in hud.restartButton.GetComponentsInChildren<TMP_Text>(true)) text.text = "Tentar novamente";
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (!component) continue;
                EditorUtility.SetDirty(component);
                if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }
        static ChapterTransitionView TransitionView(GameHUD hud, GameSession game)
        {
            var old = hud.transform.Find("Story chapter transition");
            if (old)
            {
                var existing = old.GetComponent<ChapterTransitionView>();
                if (!existing) throw new InvalidOperationException("Existing chapter card is missing its controller.");
                existing.game = game;
                existing.group.alpha = 0; existing.group.blocksRaycasts = existing.group.interactable = false;
                return existing;
            }
            var rect = GameplayUI.Rect("Story chapter transition", hud.transform, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var backdrop = rect.gameObject.AddComponent<Image>(); backdrop.color = new Color(.008f, .009f, .011f, 1); backdrop.raycastTarget = true;
            var group = rect.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = group.interactable = false;
            var view = rect.gameObject.AddComponent<ChapterTransitionView>(); view.game = game; view.group = group;
            view.heading = Label("Chapter heading", rect, "", new Vector2(1380, 100), new Vector2(0, 100), 32, hud.subtitle.font);
            view.body = Label("Chapter body", rect, "", new Vector2(1380, 240), new Vector2(0, -20), 25, hud.subtitle.font);
            var buttonRect = GameplayUI.Rect("Return to main menu", rect, new Vector2(380, 54), new Vector2(0, -220));
            var image = buttonRect.gameObject.AddComponent<Image>(); image.color = new Color(.08f, .09f, .085f); image.raycastTarget = true;
            view.returnButton = buttonRect.gameObject.AddComponent<Button>(); view.returnButton.targetGraphic = image;
            Label("Label", buttonRect, "Voltar ao menu", new Vector2(360, 48), Vector2.zero, 23, hud.subtitle.font);
            return view;
        }
        static TMP_Text Label(string name, Transform parent, string value, Vector2 size, Vector2 position, float points, TMP_FontAsset font)
        {
            var rect = GameplayUI.Rect(name, parent, size, position);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.text = value;
            text.fontSize = points; text.color = new Color(.84f, .83f, .78f); text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            return text;
        }
        static void RemoveRecordingAction()
        {
            const string path = "Assets/HorrorGameplay/Input/HorrorInput.inputactions";
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            var action = input.FindAction("Gameplay/Recording");
            if (action != null) { action.RemoveAction(); File.WriteAllText(path, input.ToJson()); AssetDatabase.ImportAsset(path); }
        }
        static void PrepareAudio()
        {
            // Preserve the reviewed, denoised clip on subsequent explicit scene applications.
            panting = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + "Clips/HeavyBreathing.wav");
            if (!panting) panting = ConvertAudio("Panting-original.mp3", "HeavyBreathing.wav", .08f, 12.45f, .72f);
            var telephone = ConvertAudio("Telephone-original.mp3", "TelephoneRing.wav", -1, 2.15f, .8f);
            const string ringPath = AudioRoot + "TelephoneRing.asset";
            ring = AssetDatabase.LoadAssetAtPath<SoundSet>(ringPath);
            if (!ring) { ring = ScriptableObject.CreateInstance<SoundSet>(); AssetDatabase.CreateAsset(ring, ringPath); }
            ring.clips = new[] { telephone }; ring.pitch = Vector2.one; ring.volume = new Vector2(.75f, .82f);
            ring.minimumDistance = 1.4f; ring.maximumDistance = 14; EditorUtility.SetDirty(ring);
        }
        static AudioClip ConvertAudio(string source, string name, float offset, float seconds, float peak)
        {
            string sourcePath = AudioRoot + "Sources/" + source;
            var importer = AssetImporter.GetAtPath(sourcePath) as AudioImporter;
            if (!importer) throw new InvalidOperationException("Missing audio source: " + sourcePath);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true; importer.defaultSampleSettings = settings; importer.SaveAndReimport();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(sourcePath);
            clip.LoadAudioData(); var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0)) throw new InvalidOperationException("Audio could not be decoded: " + source);
            var mono = new float[clip.samples];
            for (int n = 0; n < mono.Length; n++) for (int c = 0; c < clip.channels; c++) mono[n] += data[n * clip.channels + c] / clip.channels;
            if (offset < 0)
            {
                float high = mono.Max(v => Mathf.Abs(v)); int first = Array.FindIndex(mono, v => Mathf.Abs(v) > high * .12f);
                offset = Mathf.Max(0, (float)first / clip.frequency - .04f);
            }
            int start = Mathf.Clamp(Mathf.RoundToInt(offset * clip.frequency), 0, mono.Length - 1);
            int count = Mathf.Min(Mathf.RoundToInt(seconds * clip.frequency), mono.Length - start);
            var segment = new float[count]; Array.Copy(mono, start, segment, 0, count);
            float dc = (float)segment.Average(v => (double)v), highSample = 0;
            for (int i = 0; i < count; i++) { segment[i] -= dc; highSample = Mathf.Max(highSample, Mathf.Abs(segment[i])); }
            float gain = highSample > .00001f ? peak / highSample : 1;
            bool breathingClip = source.StartsWith("Panting", StringComparison.Ordinal);
            if (breathingClip)
            {
                // Ignore the recording's handling transient: normalize useful breath energy.
                double rms = Math.Sqrt(segment.Average(v => (double)v * v));
                gain = rms > .00001 ? Mathf.Min(40, .055f / (float)rms) : 1;
            }
            int fade = Mathf.Min(count / 4, Mathf.RoundToInt(clip.frequency * .025f));
            for (int i = 0; i < count; i++)
            {
                float envelope = Mathf.Min(1, Mathf.Min((float)i / fade, (float)(count - 1 - i) / fade));
                float sample = segment[i] * gain;
                if (breathingClip) sample = peak * (float)Math.Tanh(sample / peak);
                segment[i] = Mathf.Clamp(sample * envelope, -1, 1);
            }
            string destination = AudioRoot + "Clips/" + name; Directory.CreateDirectory(Path.GetDirectoryName(destination));
            using (var writer = new BinaryWriter(File.Open(destination, FileMode.Create)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(clip.frequency); writer.Write(clip.frequency * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                foreach (float sample in segment) writer.Write((short)Mathf.RoundToInt(sample * 32767));
            }
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
            var destinationImporter = (AudioImporter)AssetImporter.GetAtPath(destination);
            settings = destinationImporter.defaultSampleSettings; settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM; destinationImporter.defaultSampleSettings = settings;
            destinationImporter.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(destination);
        }
        public static List<string> Validate()
        {
            var errors = new List<string>();
            var story = AssetDatabase.LoadAssetAtPath<StoryBook>("Assets/HorrorGameplay/Data/WhispersStory.asset");
            if (!story || story.chapters.Length != 7 || story.chapters.Skip(1).Any(c => c.sceneName == "TestScene")) errors.Add("Story chapter destinations invalid.");
            foreach (string path in new[] { Office, Test }) WithScene(path, scene =>
            {
                var game = All<GameSession>(scene).Single(); int chapter = path == Office ? 0 : 1;
                errors.AddRange(VintageCameraSetup.Validate(game.player.view.playerCamera, chapter));
                if (All<RecordingOverlay>(scene).Any()) errors.Add("Recording overlay remains: " + path);
                var audio = All<AudioManager>(scene).Single();
                if (!audio.breathing || !audio.breathing.clip || audio.breathing.clip.length < 10) errors.Add("Human breathing missing: " + path);
                if (path == Office)
                {
                    if (!game.narrative || !game.save.narrative || !game.narrative.transitionView) errors.Add("Narrative references missing.");
                    if (All<StoryTelephone>(scene).Count() != 1 || All<StoryEquipment>(scene).Count() != 1 || All<StoryExit>(scene).Count() != 1) errors.Add("Prologue interactables missing.");
                    var control = All<InteractableLightSwitch>(scene).Single();
                    if (control.alwaysOn || !control.startsOn || !control.CanInteract(null)) errors.Add("Office switch not interactive.");
                    control.SetState(false); bool off = !control.IsOn && control.targetLights.All(l => !l.enabled);
                    control.SetState(true); if (!off || !control.IsOn || control.targetLights.Any(l => !l.enabled)) errors.Add("Switch circuit failed.");
                }
            });
            return errors;
        }
        static IEnumerable<T> All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
        static GameObject FindNamed(Scene scene, string name) => All<Transform>(scene).FirstOrDefault(t => t.name == name)?.gameObject;
        static GameObject Named(Scene scene, string name) => FindNamed(scene, name) ?? throw new InvalidOperationException("Missing office object: " + name);
        static void WithScene(string path, Action<Scene> action)
        {
            var original = SceneManager.GetActiveScene(); var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { action(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original); }
        }
    }
}
