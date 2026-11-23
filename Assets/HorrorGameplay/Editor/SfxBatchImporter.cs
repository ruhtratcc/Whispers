// Ferramenta de preparação ou verificação usada no Editor.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace HorrorGame.Editor
{
    /// <summary>Imports the offline generation manifest; never calls the audio API or save system.</summary>
    public static class SfxBatchImporter
    {
        const string Docs = "Assets/Docs/Audio/";
        const string Data = "Assets/HorrorGameplay/Data/SfxBatch/";
        const string BackupRoot = "Assets/_Substituidos/SfxBatch20261104/";

        [Serializable] public sealed class Manifest { public int version; public Item[] items = Array.Empty<Item>(); }
        [Serializable] public sealed class Item
        {
            public string nome, secao, categoria, mixer;
            public bool spatial = true, loop;
            public float volume = 1, pitchMin = .96f, pitchMax = 1.04f;
            public Variant[] variantes = Array.Empty<Variant>();
        }
        [Serializable] public sealed class Variant { public string nome, estado, processed; public float duracao; }
        [Serializable] public sealed class Bindings { public Binding[] bindings = Array.Empty<Binding>(); }
        [Serializable] public sealed class Binding { public string nome, targetAsset, field, kind; public int index = -1; }
        [Serializable] public sealed class Row
        {
            public string nome, target, state, detail;
            public Row(string name, string target, string state, string detail = "")
            { nome = name; this.target = target; this.state = state; this.detail = detail; }
        }
        [Serializable] public sealed class Report
        {
            public string date, manifest, catalog;
            public bool completed;
            public List<Row> rows = new List<Row>();
            public string[] nullBefore = Array.Empty<string>(), nullAfter = Array.Empty<string>(), newNulls = Array.Empty<string>();
            public List<string> errors = new List<string>();
        }

        [MenuItem("Tools/Áudio/Importar SFX")]
        static void Menu() => Debug.Log(ImportAll());

        public static string ImportAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Pare o Play e aguarde a compilação antes de importar SFX.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Há uma cena com alterações não salvas; nenhuma importação foi iniciada.");

            EnsureFolder(Docs.TrimEnd('/')); EnsureFolder(Data + "Sets");
            var report = new Report { date = DateTime.UtcNow.ToString("O"), manifest = Docs + "sfx_manifest.json", catalog = Data + "SfxCatalog.asset" };
            report.nullBefore = NullAudioSlots();
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var manifest = File.Exists(report.manifest) ? JsonUtility.FromJson<Manifest>(File.ReadAllText(report.manifest)) : null;
                if (manifest == null) report.rows.Add(new Row("entrada", report.manifest, "pendente", "Manifesto ausente; nenhum áudio existente será substituído."));
                else if (manifest.items == null || manifest.items.Length == 0) report.rows.Add(new Row("entrada", report.manifest, "pendente", "Manifesto sem sons gerados; catálogo disponível e referências anteriores preservadas."));
                var catalog = AssetDatabase.LoadAssetAtPath<SfxCatalog>(report.catalog);
                if (!catalog) { catalog = ScriptableObject.CreateInstance<SfxCatalog>(); AssetDatabase.CreateAsset(catalog, report.catalog); }
                var entries = (catalog.entries ?? Array.Empty<SfxCatalogEntry>()).Where(e => e != null && !string.IsNullOrEmpty(e.name))
                    .GroupBy(e => e.name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
                var imported = new Dictionary<string, SfxCatalogEntry>(StringComparer.Ordinal);
                var groups = MixerGroups();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in manifest?.items ?? Array.Empty<Item>())
                {
                    if (item == null || !SafeName(item.nome) || !seen.Add(item.nome))
                    { report.rows.Add(new Row(item?.nome ?? "", report.manifest, "inválido", "Nome inválido ou repetido; item ignorado.")); continue; }
                    try
                    {
                        var variants = item.variantes ?? Array.Empty<Variant>();
                        if (variants.Length == 0 || variants.Any(v => v == null || v.estado != "gerado" || !ProcessedPath(v.processed) || !File.Exists(v.processed)))
                        { report.rows.Add(new Row(item.nome, "variantes", "pendente", "Todas as variantes precisam estar geradas e processadas. Slots e catálogo anteriores preservados.")); continue; }
                        // Validate the complete list before changing any SoundSet or slot.
                        foreach (var variant in variants) Configure(variant.processed, item, variant.duracao, report);
                        var clips = variants.Select(v => AssetDatabase.LoadAssetAtPath<AudioClip>(v.processed)).ToArray();
                        if (clips.Any(c => !c || c.length <= 0 || c.channels < 1))
                        { report.rows.Add(new Row(item.nome, "variantes", "falhou", "Um arquivo não foi decodificado como AudioClip; nenhum slot alterado.")); continue; }
                        var channel = Enum.TryParse(item.mixer, true, out AudioChannel parsed) ? parsed : AudioChannel.SFX;
                        string setPath = Data + "Sets/" + item.nome + ".asset";
                        var sound = AssetDatabase.LoadAssetAtPath<SoundSet>(setPath);
                        if (!sound) { sound = ScriptableObject.CreateInstance<SoundSet>(); AssetDatabase.CreateAsset(sound, setPath); }
                        else Backup(setPath);
                        sound.clips = clips; sound.pitch = item.loop ? Vector2.one : new Vector2(Mathf.Max(.1f, item.pitchMin), Mathf.Max(.1f, item.pitchMax));
                        sound.volume = Vector2.one; sound.overrideChannel = true; sound.channel = channel; EditorUtility.SetDirty(sound);
                        groups.TryGetValue(item.mixer ?? channel.ToString(), out var group);
                        var entry = new SfxCatalogEntry { name = item.nome, sound = sound, channel = channel, mixerGroup = group,
                            spatial = item.spatial, loop = item.loop, defaultVolume = Mathf.Clamp01(item.volume) };
                        entries[item.nome] = entry; imported[item.nome] = entry;
                        report.rows.Add(new Row(item.nome, setPath, "catalogado", clips.Length + " variantes; loop=" + item.loop + "; mixer=" + (group ? group.name : "ausente")));
                        if (!group) report.rows.Add(new Row(item.nome, item.mixer ?? channel.ToString(), "mixer pendente", "Grupo exato não encontrado; nenhum mixer foi criado ou reestruturado."));
                    }
                    catch (Exception e) { report.errors.Add(item.nome + ": " + e.Message); }
                }
                Backup(report.catalog); catalog.entries = entries.Values.OrderBy(e => e.name, StringComparer.Ordinal).ToArray(); EditorUtility.SetDirty(catalog);
                ApplyBindings(imported, report);
                AssetDatabase.SaveAssets(); report.completed = report.errors.Count == 0;
            }
            catch (Exception e) { report.errors.Add(e.ToString()); }
            finally
            {
                report.nullAfter = NullAudioSlots(); report.newNulls = report.nullAfter.Except(report.nullBefore, StringComparer.Ordinal).ToArray();
                if (report.newNulls.Length != 0) { report.completed = false; report.errors.Add("Novos slots nulos detectados; consultar newNulls."); }
                Backup(Docs + "sfx_import_report.json");
                File.WriteAllText(Docs + "sfx_import_report.json", JsonUtility.ToJson(report, true));
                WriteUnbound(report);
                AssetDatabase.ImportAsset(Docs + "sfx_import_report.json", ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(Docs + "Sons_Sem_Slot.md", ImportAssetOptions.ForceSynchronousImport);
            }
            return Docs + "sfx_import_report.json";
        }

        public static bool SafeName(string value)
        {
            if (string.IsNullOrEmpty(value) || value != value.ToLowerInvariant()) return false;
            return value.All(c => char.IsLower(c) || char.IsDigit(c) || c == '_' || c == '-');
        }

        public static bool ProcessedPath(string value)
        {
            if (!AssetPath(value) || value.Contains("/Raw/") || value.Contains("/Editor/")) return false;
            if (!value.StartsWith("Assets/Audio/SFX/", StringComparison.Ordinal) && !value.StartsWith("Assets/Audio/Ambience/", StringComparison.Ordinal)) return false;
            string extension = Path.GetExtension(value).ToLowerInvariant(); return extension == ".wav" || extension == ".ogg";
        }

        static bool AssetPath(string path) => !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal)
            && !path.Contains('\\') && !path.Split('/').Any(s => s == ".." || s == ".") && !Path.IsPathRooted(path);

        static void Configure(string path, Item item, float seconds, Report report)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (!importer) throw new InvalidOperationException("AudioImporter ausente: " + path);
            bool background = item.loop && seconds > 8 || string.Equals(item.mixer, "Ambience", StringComparison.OrdinalIgnoreCase)
                || (item.categoria ?? "").IndexOf("drone", StringComparison.OrdinalIgnoreCase) >= 0;
            bool lowLatency = string.Equals(item.mixer, "UI", StringComparison.OrdinalIgnoreCase) || string.Equals(item.mixer, "PC", StringComparison.OrdinalIgnoreCase);
            var serialized = new SerializedObject(importer); var normalize = serialized.FindProperty("m_Normalize") ?? serialized.FindProperty("normalize");
            var settings = importer.defaultSampleSettings;
            var previous = settings;
            settings.loadType = background ? AudioClipLoadType.Streaming : seconds <= 2 || lowLatency ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
            settings.preloadAudioData = !background; settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = .85f;
            bool needsNormalize = normalize != null && normalize.propertyType == SerializedPropertyType.Boolean && normalize.boolValue;
            bool changed = importer.forceToMono != item.spatial || importer.loadInBackground != background || needsNormalize
                || previous.loadType != settings.loadType || previous.preloadAudioData != settings.preloadAudioData
                || previous.sampleRateSetting != settings.sampleRateSetting || previous.compressionFormat != settings.compressionFormat
                || !Mathf.Approximately(previous.quality, settings.quality);
            if (changed)
            {
                Backup(path);
                if (needsNormalize) { normalize.boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo(); }
                importer.forceToMono = item.spatial; importer.loadInBackground = background;
                importer.defaultSampleSettings = settings; importer.SaveAndReimport();
            }
            report.rows.Add(new Row(item.nome, path, "importado", "load=" + settings.loadType + "; preload=" + settings.preloadAudioData + "; mono=" + item.spatial + "; normalização desativada; loop controlado pelo catálogo/caller."));
        }

        static Dictionary<string, AudioMixerGroup> MixerGroups()
        {
            string[] wanted = { "Master", "Music", "Ambience", "SFX", "UI", "Voice", "PC", "Phone" };
            var best = new Dictionary<string, AudioMixerGroup>(StringComparer.OrdinalIgnoreCase);
            var fallback = new Dictionary<string, AudioMixerGroup>(StringComparer.OrdinalIgnoreCase);
            int score = -1;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioMixer"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); if (path.Contains("_Substituidos") || path.Contains("/Tests/")) continue;
                var groups = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AudioMixerGroup>().ToArray();
                foreach (string name in wanted)
                    if (!fallback.ContainsKey(name))
                    {
                        var match = groups.FirstOrDefault(g => string.Equals(g.name, name, StringComparison.OrdinalIgnoreCase));
                        if (match) fallback[name] = match;
                    }
                int candidate = wanted.Count(name => groups.Any(g => string.Equals(g.name, name, StringComparison.OrdinalIgnoreCase)));
                if (candidate <= score) continue;
                score = candidate; best.Clear();
                foreach (string name in wanted) { var group = groups.FirstOrDefault(g => string.Equals(g.name, name, StringComparison.OrdinalIgnoreCase)); if (group) best[name] = group; }
            }
            // PC, Phone and UI already have dedicated mixer assets in this project.
            foreach (var pair in fallback) if (!best.ContainsKey(pair.Key)) best[pair.Key] = pair.Value;
            return best;
        }

        static void ApplyBindings(Dictionary<string, SfxCatalogEntry> items, Report report)
        {
            string path = Docs + "sfx_bindings.json";
            var map = File.Exists(path) ? JsonUtility.FromJson<Bindings>(File.ReadAllText(path)) : null;
            var bindings = (map?.bindings ?? Array.Empty<Binding>()).Where(b => b != null && items.ContainsKey(b.nome ?? "")).ToArray();
            var conflicts = new HashSet<string>(bindings.GroupBy(SlotKey, StringComparer.Ordinal)
                .Where(g => g.Select(b => b.nome).Distinct(StringComparer.Ordinal).Count() > 1).Select(g => g.Key), StringComparer.Ordinal);
            var bound = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in bindings)
            {
                var entry = items[binding.nome];
                if (conflicts.Contains(SlotKey(binding)))
                { report.rows.Add(new Row(binding.nome, SlotKey(binding), "conflito", "Mais de um som aponta para este slot. Referência anterior preservada.")); continue; }
                if (!AssetPath(binding.targetAsset) || binding.targetAsset.Contains("_Substituidos") || binding.targetAsset.Contains("/Tests/"))
                { report.rows.Add(new Row(binding.nome, binding.targetAsset, "vínculo inválido", "Destino fora dos assets autorizados.")); continue; }
                var target = AssetDatabase.LoadAssetAtPath<ScriptableObject>(binding.targetAsset);
                if (!target) { report.rows.Add(new Row(binding.nome, binding.targetAsset, "sem slot", "ScriptableObject não encontrado; clipes continuam no catálogo.")); continue; }
                try
                {
                    if (binding.kind == "SoundSet" && target is SoundSet sound && (string.IsNullOrEmpty(binding.field) || binding.field == "clips"))
                    {
                        Backup(binding.targetAsset); sound.clips = entry.Variants; EditorUtility.SetDirty(sound);
                    }
                    else
                    {
                        var serialized = new SerializedObject(target); var property = serialized.FindProperty(binding.field ?? "");
                        if (property != null && binding.index >= 0)
                            property = property.isArray && binding.index < property.arraySize ? property.GetArrayElementAtIndex(binding.index) : null;
                        string type = binding.kind == "SoundSet" ? "SoundSet" : binding.kind == "AudioClip" ? "AudioClip" : "";
                        if (property == null || type == "" || property.propertyType != SerializedPropertyType.ObjectReference || !property.type.Contains(type))
                        { report.rows.Add(new Row(binding.nome, SlotKey(binding), "sem slot", "Campo/tipo/índice não corresponde ao mapa explícito; nada foi alterado.")); continue; }
                        Backup(binding.targetAsset); property.objectReferenceValue = binding.kind == "SoundSet" ? (UnityEngine.Object)entry.sound : entry.Variants[0];
                        serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
                    }
                    bound.Add(binding.nome); report.rows.Add(new Row(binding.nome, SlotKey(binding), "ligado", binding.kind == "AudioClip" && entry.Variants.Length > 1 ? "Slot de um clipe recebe a primeira variante; todas permanecem no catálogo." : "IDs/flags e demais campos preservados."));
                }
                catch (Exception e) { report.errors.Add(binding.nome + " -> " + SlotKey(binding) + ": " + e.Message); }
            }
            foreach (var pair in items.Where(pair => !bound.Contains(pair.Key)))
                report.rows.Add(new Row(pair.Key, "SfxCatalog", "sem slot", "Disponível no catálogo. Sugestão: ligar a um evento de áudio existente após conferir nome, categoria e contexto; nenhuma lógica foi criada."));
        }

        static string SlotKey(Binding binding) => binding.targetAsset + " :: " +
            (binding.kind == "SoundSet" && string.IsNullOrEmpty(binding.field) ? "clips" : binding.field) + "[" + binding.index + "]";

        static string[] NullAudioSlots()
        {
            var result = new List<string>();
            var folders = new[] { "Assets/HorrorGameplay/Data", "Assets/Audio" }.Where(AssetDatabase.IsValidFolder).ToArray();
            if (folders.Length == 0) return result.ToArray();
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); if (path.Contains("_Substituidos") || path.Contains("/Tests/")) continue;
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path); if (!asset) continue;
                using (var serialized = new SerializedObject(asset))
                {
                    var property = serialized.GetIterator();
                    while (property.NextVisible(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null &&
                            (property.type.Contains("AudioClip") || property.type.Contains("SoundSet"))) result.Add(path + " :: " + property.propertyPath);
                }
            }
            return result.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        static void WriteUnbound(Report report)
        {
            var text = new StringBuilder("# SFX — ligações e pendências\n\nGerado em " + report.date + ". Sem alterações de gameplay, cenas ou saves.\n\n");
            foreach (var group in report.rows.Where(r => r.state != "importado" && r.state != "catalogado").GroupBy(r => r.state))
            { text.Append("## ").Append(group.Key).Append("\n\n"); foreach (var row in group) text.Append("- ").Append(row.nome).Append(" | ").Append(row.target).Append(" | ").Append(row.detail).Append('\n'); text.Append('\n'); }
            text.Append("## Slots de áudio nulos encontrados\n\nSão referências anteriores à importação ou placeholders narrativos; não são preenchidas por aproximação. Vozes/dublagem e músicas não são substituídas por SFX.\n\n");
            foreach (string slot in report.nullAfter) text.Append("- ").Append(slot).Append(report.nullBefore.Contains(slot) ? " — pré-existente" : " — NOVO: revisar").Append('\n');
            if (report.nullAfter.Length == 0) text.Append("Nenhum slot nulo no escopo auditado.\n");
            if (report.errors.Count > 0) { text.Append("\n## Erros\n\n"); foreach (string error in report.errors) text.Append("- ").Append(error.Replace('\n', ' ')).Append('\n'); }
            Backup(Docs + "Sons_Sem_Slot.md"); File.WriteAllText(Docs + "Sons_Sem_Slot.md", text.ToString());
        }

        static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/'); if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new IOException("Pasta inválida: " + path);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void Backup(string path)
        {
            if (!AssetPath(path) || path.StartsWith(BackupRoot, StringComparison.Ordinal)) return;
            string destination = BackupRoot + path.Substring("Assets/".Length) + ".bak";
            if (File.Exists(path) && !File.Exists(destination)) { Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(path, destination); }
            if (File.Exists(path + ".meta") && !File.Exists(destination + ".meta.bak")) { Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(path + ".meta", destination + ".meta.bak"); }
        }
    }
}
