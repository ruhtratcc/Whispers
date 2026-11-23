// Ferramenta de preparação ou verificação usada no Editor.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorGame.Editor
{
    // This pass assigns real Foley without replacing IDs, mixer settings or save data.
    public static class AudioPolishPass
    {
        const string AudioRoot = "Assets/HorrorGameplay/Audio/";
        const string Recorded = AudioRoot + "Recorded/";
        const string OfficeScene = "Assets/ChapterOneOffice/Scenes/Chapter1Office.unity";
        const string Documentation = "Assets/ChapterOneOffice/Documentation/";
        static readonly Dictionary<string, SoundSet> sets = new Dictionary<string, SoundSet>();

        [Serializable] public sealed class Manifest { public SetEntry[] sets; public ClipEntry[] clips; }
        [Serializable] public sealed class SetEntry
        {
            public string name; public string[] clips;
            public float pitchMin, pitchMax, volumeMin, volumeMax, minDistance, maxDistance;
        }
        [Serializable] public sealed class ClipEntry { public string filename; public float duration; public bool forceToMono; }
        [Serializable] sealed class Report
        {
            public string generatedUtc; public bool passed; public int soundSets, clips, surfaceTypes;
            public string[] errors, editedScenes, editedPrefabs;
            public bool breathingDisabled, hissDisabled;
        }
        static readonly List<string> editedScenes = new List<string>(), editedPrefabs = new List<string>();

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Pare o Play antes de aplicar a revisão de áudio.");
            if (SceneManager.GetActiveScene().path != OfficeScene)
                throw new InvalidOperationException("Abra Chapter1Office antes de aplicar a revisão.");
            var manifest = ReadManifest();
            EnsureFolder(Recorded.TrimEnd('/'));
            EnsureFolder(Recorded + "Sets");
            sets.Clear(); editedScenes.Clear(); editedPrefabs.Clear();
            foreach (var clip in manifest.clips) ImportClip(clip);
            foreach (var entry in manifest.sets)
            {
                string path = Recorded + "Sets/" + entry.name + ".asset";
                SoundSet sound = AssetDatabase.LoadAssetAtPath<SoundSet>(path);
                if (!sound)
                {
                    sound = ScriptableObject.CreateInstance<SoundSet>();
                    sound.name = entry.name;
                    AssetDatabase.CreateAsset(sound, path);
                }
                Undo.RecordObject(sound, "Assign recorded Foley");
                sound.clips = entry.clips.Select(Clip).ToArray();
                sound.pitch = new Vector2(entry.pitchMin, entry.pitchMax);
                sound.volume = new Vector2(entry.volumeMin, entry.volumeMax);
                sound.minimumDistance = Mathf.Max(.05f, entry.minDistance);
                sound.maximumDistance = Mathf.Max(sound.minimumDistance + .1f, entry.maxDistance);
                if (entry.name.StartsWith("Door ") || entry.name == "Locked door") sound.maximumDistance = 8;
                if (entry.name.StartsWith("Drawer ")) sound.maximumDistance = 4.5f;
                if (entry.name == "Paper pickup") { sound.minimumDistance = .6f; sound.maximumDistance = 3; }
                EditorUtility.SetDirty(sound);
                sets.Add(entry.name, sound);
            }
            // Updating existing sets preserves every scene or prefab reference to them.
            foreach (GroundSurface surface in Enum.GetValues(typeof(GroundSurface)))
            {
                CopyLegacy("Step-" + surface, Required("Footsteps " + surface));
                var landing = CopyLegacy("Land-" + surface, Required("Footsteps " + surface));
                landing.pitch = new Vector2(.92f, .98f);
                landing.volume = new Vector2(.82f, .95f);
                EditorUtility.SetDirty(landing);
            }
            CopyLegacy("Door creak", Required("Door opening"));
            CopyLegacy("Flashlight click", Required("Flashlight on"));
            CopyLegacy("Pickup", Required("Paper pickup"));
            CopyLegacy("Physics impact", Required("Impact Wood"));
            CopyLegacy("Scare sting", Required("Scare sting"));
            CopyLegacy("Calm drone", Required("Calm drone"));
            CopyLegacy("Tension drone", Required("Tension drone"));
            // These were deliberately disabled by the user. Do not substitute fake voices.
            foreach (string name in new[] { "Heavy breathing", "Breathing gasp", "Air ambience", "Distant whisper placeholder" })
            {
                var old = AssetDatabase.LoadAssetAtPath<SoundSet>(AudioRoot + name + ".asset");
                if (old) { Undo.RecordObject(old, "Remove provisional hiss"); old.clips = Array.Empty<AudioClip>(); EditorUtility.SetDirty(old); }
            }
            UpdateFootstepLibrary();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/HorrorGameplay/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try { Bind(root); PrefabUtility.SaveAsPrefabAsset(root, path); editedPrefabs.Add(path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (string path in new[] { OfficeScene, "Assets/HorrorGameplay/Scenes/TestScene.unity", "Assets/HorrorMenu/Scenes/MainMenu.unity" })
                UpdateScene(path);
            AssetDatabase.SaveAssets();
            Debug.Log("Recorded Foley assigned; menu volumes, flashlight strength and save IDs preserved.");
        }

        static Manifest ReadManifest()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Recorded + "manifest.json"));
            if (manifest == null || manifest.sets == null || manifest.clips == null)
                throw new InvalidDataException("Manifesto de Foley ausente ou inválido.");
            return manifest;
        }
        static void EnsureFolder(string path)
        {
            string[] segments = path.TrimEnd('/').Split('/'); string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            { string next = current + "/" + segments[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]); current = next; }
        }
        static AudioClip Clip(string filename)
        {
            if (Path.GetFileName(filename) != filename) throw new InvalidDataException("Clip deve ser um nome de arquivo simples.");
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Recorded + "Clips/" + filename);
            if (!clip) throw new FileNotFoundException("Gravação não importada: " + filename);
            return clip;
        }
        static void ImportClip(ClipEntry entry)
        {
            string path = Recorded + "Clips/" + entry.filename;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (!importer) throw new InvalidDataException("AudioImporter ausente: " + path);
            bool music = entry.filename.StartsWith("Music-", StringComparison.Ordinal);
            importer.forceToMono = !music;
            var importerData = new SerializedObject(importer);
            var normalize = importerData.FindProperty("m_Normalize");
            if (normalize != null) { normalize.boolValue = false; importerData.ApplyModifiedPropertiesWithoutUndo(); }
            importer.loadInBackground = music;
            AudioImporterSampleSettings options = importer.defaultSampleSettings;
            options.preloadAudioData = !music;
            options.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            options.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            options.quality = music ? .8f : 1;
            options.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = options;
            importer.SaveAndReimport();
        }
        static SoundSet Required(string name)
        {
            if (sets.TryGetValue(name, out SoundSet sound)) return sound;
            sound = AssetDatabase.LoadAssetAtPath<SoundSet>(Recorded + "Sets/" + name + ".asset");
            if (!sound) throw new InvalidDataException("SoundSet ausente: " + name);
            sets[name] = sound; return sound;
        }
        static SoundSet CopyLegacy(string name, SoundSet source)
        {
            string path = AudioRoot + name + ".asset";
            var target = AssetDatabase.LoadAssetAtPath<SoundSet>(path);
            if (!target) { target = ScriptableObject.CreateInstance<SoundSet>(); target.name = name; AssetDatabase.CreateAsset(target, path); }
            Undo.RecordObject(target, "Replace provisional audio");
            target.clips = source.clips; target.pitch = source.pitch; target.volume = source.volume;
            target.minimumDistance = source.minimumDistance; target.maximumDistance = source.maximumDistance;
            EditorUtility.SetDirty(target); return target;
        }
        static void UpdateFootstepLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<FootstepLibrary>(AudioRoot + "FootstepLibrary.asset");
            if (!library) throw new InvalidDataException("FootstepLibrary ausente.");
            Undo.RecordObject(library, "Assign seven floor surfaces");
            var entries = library.entries?.Where(e => e != null).ToList() ?? new List<FootstepLibrary.Entry>();
            foreach (GroundSurface surface in Enum.GetValues(typeof(GroundSurface)))
            {
                var entry = entries.FirstOrDefault(e => e.surface == surface);
                if (entry == null) { entry = new FootstepLibrary.Entry { surface = surface }; entries.Add(entry); }
                entry.steps = AssetDatabase.LoadAssetAtPath<SoundSet>(AudioRoot + "Step-" + surface + ".asset");
                entry.landing = AssetDatabase.LoadAssetAtPath<SoundSet>(AudioRoot + "Land-" + surface + ".asset");
            }
            library.entries = entries.ToArray(); EditorUtility.SetDirty(library);
        }
        static void Bind(GameObject root)
        {
            foreach (var door in root.GetComponentsInChildren<InteractiveDoor>(true))
            {
                door.openSound = Required("Door opening"); door.closeSound = Required("Door closing");
                door.latchSound = Required("Door latch"); door.lockedSound = Required("Locked door");
                EditorUtility.SetDirty(door);
            }
            foreach (var drawer in root.GetComponentsInChildren<InteractiveDrawer>(true))
            {
                drawer.openSound = Required("Drawer opening"); drawer.closeSound = Required("Drawer closing");
                drawer.endSound = Required("Drawer latch"); // Movement and stop use disjoint recorded sections.
                EditorUtility.SetDirty(drawer);
            }
            foreach (var light in root.GetComponentsInChildren<FlashlightController>(true))
            { light.clickOn = Required("Flashlight on"); light.clickOff = Required("Flashlight off"); EditorUtility.SetDirty(light); }
            foreach (var item in root.GetComponentsInChildren<CollectibleItem>(true))
            {
                item.pickupSound = Required(item.kind == CollectibleKind.Note ? "Paper pickup" : item.kind == CollectibleKind.Key ? "Key pickup" : "Flashlight on");
                EditorUtility.SetDirty(item);
            }
            foreach (var impact in root.GetComponentsInChildren<ImpactAudio>(true))
            {
                string name = impact.gameObject.name.ToLowerInvariant();
                impact.sound = Required(name.Contains("barrel") || name.Contains("metal") || name.Contains("barril") ? "Impact Metal" : name.Contains("glass") || name.Contains("vidro") ? "Impact Glass" : "Impact Wood");
                EditorUtility.SetDirty(impact);
            }
            foreach (var manager in root.GetComponentsInChildren<AudioManager>(true))
            {
                SetMusic(manager.calmMusic, Required("Calm drone").clips[0]);
                SetMusic(manager.tenseMusic, Required("Tension drone").clips[0]);
                if (manager.ambience) { manager.ambience.clip = null; EditorUtility.SetDirty(manager.ambience); }
                if (manager.breathing) { manager.breathing.clip = null; EditorUtility.SetDirty(manager.breathing); }
                var serialized = new SerializedObject(manager); serialized.FindProperty("musicGain").floatValue = .095f; serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
            }
            foreach (var scare in root.GetComponentsInChildren<RandomScareAudio>(true))
            {
                if (scare.sounds && scare.sounds.name == "Distant whisper placeholder")
                { scare.sounds = Required("Room creaks"); scare.channel = AudioChannel.Ambience; EditorUtility.SetDirty(scare); }
            }
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour && behaviour.GetType().FullName == "SchoolMenu.MenuAudio") BindMenu(behaviour);
        }
        static void SetMusic(AudioSource source, AudioClip clip)
        {
            if (!source) return;
            source.clip = clip; source.loop = true; source.playOnAwake = false;
            source.spatialBlend = 0; source.dopplerLevel = 0;
            EditorUtility.SetDirty(source);
        }
        static void BindMenu(MonoBehaviour menu)
        {
            var serialized = new SerializedObject(menu);
            serialized.FindProperty("hoverTick").objectReferenceValue = Required("UI hover").clips[0];
            serialized.FindProperty("confirmThud").objectReferenceValue = Required("UI confirm").clips[0];
            serialized.FindProperty("ambientLoop").objectReferenceValue = null;
            serialized.FindProperty("music").objectReferenceValue = Required("Calm drone").clips[0];
            serialized.FindProperty("musicGain").floatValue = .12f;
            var distant = serialized.FindProperty("distantSounds");
            var clips = Required("Room creaks").clips; distant.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) distant.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(menu);
        }
        static void UpdateScene(string path)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool loaded = scene.IsValid() && scene.isLoaded;
            if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects()) Bind(root);
                if (path == OfficeScene) ConfigureOfficeCarpet(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Não foi possível salvar " + path);
                editedScenes.Add(path);
            }
            finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
        }
        static void ConfigureOfficeCarpet(Scene scene)
        {
            var rug = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Tapete central");
            if (!rug) throw new InvalidDataException("Tapete central não encontrado no escritório.");
            var surface = rug.GetComponent<SurfaceType>() ?? rug.gameObject.AddComponent<SurfaceType>();
            surface.surface = GroundSurface.Carpet;
            // Keep the existing thin floor collider; footsteps read its own surface.
            if (!rug.GetComponent<Collider>())
            {
                var collider = rug.gameObject.AddComponent<BoxCollider>();
                var mesh = rug.GetComponent<MeshFilter>();
                collider.size = mesh && mesh.sharedMesh ? mesh.sharedMesh.bounds.size : Vector3.one;
                collider.center = mesh && mesh.sharedMesh ? mesh.sharedMesh.bounds.center : Vector3.zero;
            }
            EditorUtility.SetDirty(surface);
        }
        public static void Validate()
        {
            var errors = new List<string>(); var manifest = ReadManifest();
            foreach (var entry in manifest.sets)
            {
                var sound = Required(entry.name);
                if (sound.clips == null || sound.clips.Length == 0 || sound.clips.Any(c => !c)) errors.Add("Clipes ausentes em " + entry.name);
                else if (sound.clips.Any(c => !AssetDatabase.GetAssetPath(c).StartsWith(Recorded))) errors.Add("Som provisório ainda conectado: " + entry.name);
            }
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var managers = roots.SelectMany(r => r.GetComponentsInChildren<AudioManager>(true)).ToArray();
            bool breathOff = managers.All(m => !m.breathing || !m.breathing.clip);
            bool hissOff = managers.All(m => !m.ambience || !m.ambience.clip);
            if (!breathOff || !hissOff) errors.Add("Respiração provisória ou chiado reativado.");
            foreach (var door in roots.SelectMany(r => r.GetComponentsInChildren<InteractiveDoor>(true)))
                if (!door.openSound || !door.closeSound || !door.latchSound) errors.Add("Porta sem sons de movimento/fecho.");
            foreach (var drawer in roots.SelectMany(r => r.GetComponentsInChildren<InteractiveDrawer>(true)))
                if (!drawer.openSound || !drawer.closeSound || !drawer.endSound) errors.Add("Gaveta sem sons separados de movimento e batida final.");
            foreach (var light in roots.SelectMany(r => r.GetComponentsInChildren<FlashlightController>(true)))
                if (!light.clickOn || !light.clickOff || light.lamp && Mathf.Abs(light.lamp.intensity - 90) > .01f) errors.Add("Sons ou intensidade da lanterna incorretos.");
            var rug = roots.SelectMany(r => r.GetComponentsInChildren<SurfaceType>(true)).FirstOrDefault(s => s.gameObject.name == "Tapete central");
            if (!rug || rug.surface != GroundSurface.Carpet || !rug.GetComponent<Collider>()) errors.Add("Tapete sem superfície/collider para passos abafados.");
            var library = AssetDatabase.LoadAssetAtPath<FootstepLibrary>(AudioRoot + "FootstepLibrary.asset");
            foreach (GroundSurface surface in Enum.GetValues(typeof(GroundSurface)))
                if (library == null || !library.entries.Any(e => e != null && e.surface == surface && e.steps && e.landing)) errors.Add("Superfície sem passos: " + surface);
            var report = new Report { generatedUtc = DateTime.UtcNow.ToString("o"), passed = errors.Count == 0,
                soundSets = manifest.sets.Length, clips = manifest.clips.Length, surfaceTypes = Enum.GetValues(typeof(GroundSurface)).Length,
                errors = errors.ToArray(), editedScenes = editedScenes.ToArray(), editedPrefabs = editedPrefabs.ToArray(), breathingDisabled = breathOff, hissDisabled = hissOff };
            File.WriteAllText(Documentation + "Audio-polish-report.json", JsonUtility.ToJson(report, true));
            Debug.Log("Audio polish validation " + (report.passed ? "PASS" : "FAIL") + ": " + errors.Count + " errors.");
            if (!report.passed) throw new InvalidOperationException(string.Join("; ", errors));
        }
    }
}
