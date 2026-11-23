// Verifica FindsTheExistingDedicatedMixersAsWellAsGameplayGroups, NamesCannotEscapeTheCatalog, RawAndExternalFilesCannotBecomeRuntimeClips, NativeImporterAppliesLatencyAndStreamingRules.

using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.Tests
{
    public sealed class SfxBatchImporterTests
    {
        static Type Importer => Type.GetType("HorrorGame.Editor.SfxBatchImporter, HorrorGame.Editor", true);

        [Test]
        public void FindsTheExistingDedicatedMixersAsWellAsGameplayGroups()
        {
            var groups = (System.Collections.IDictionary)Importer.GetMethod("MixerGroups", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            foreach (string name in new[] { "Master", "Music", "Ambience", "SFX", "UI", "Voice", "PC", "Phone" })
                Assert.That(groups.Contains(name), Is.True, name);
        }

        [TestCase("door_open", true)]
        [TestCase("Door Open", false)]
        [TestCase("../door", false)]
        public void NamesCannotEscapeTheCatalog(string value, bool expected)
            => Assert.That(Importer.GetMethod("SafeName").Invoke(null, new object[] { value }), Is.EqualTo(expected));

        [TestCase("Assets/Audio/SFX/Player/step.wav", true)]
        [TestCase("Assets/Audio/Ambience/night.ogg", true)]
        [TestCase("Assets/Audio/Raw/Editor/night.wav", false)]
        [TestCase("Assets/Audio/SFX/../old.wav", false)]
        [TestCase("C:/outside.wav", false)]
        public void RawAndExternalFilesCannotBecomeRuntimeClips(string value, bool expected)
            => Assert.That(Importer.GetMethod("ProcessedPath").Invoke(null, new object[] { value }), Is.EqualTo(expected));

        [TestCase(false, "UI", 1, AudioClipLoadType.DecompressOnLoad)]
        [TestCase(true, "Ambience", 30, AudioClipLoadType.Streaming)]
        public void NativeImporterAppliesLatencyAndStreamingRules(bool loop, string mixer, float seconds, AudioClipLoadType load)
        {
            const string source = "Assets/Audio/SFX/ElevenLabs/WhistleResponses/whistle_child_marta_elevenlabs.wav";
            string path = "Assets/Audio/SFX/sfx_import_test_" + Guid.NewGuid().ToString("N") + ".wav";
            string backup = "Assets/_Substituidos/SfxBatch20261104/" + path.Substring("Assets/".Length) + ".bak";
            try
            {
                Assert.That(AssetDatabase.CopyAsset(source, path), Is.True);
                var itemType = Importer.GetNestedType("Item"); var item = Activator.CreateInstance(itemType);
                itemType.GetField("nome").SetValue(item, "test"); itemType.GetField("loop").SetValue(item, loop);
                itemType.GetField("mixer").SetValue(item, mixer); itemType.GetField("categoria").SetValue(item, mixer);
                itemType.GetField("spatial").SetValue(item, !loop);
                var report = Activator.CreateInstance(Importer.GetNestedType("Report"));
                Importer.GetMethod("Configure", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { path, item, seconds, report });
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(load));
                Assert.That(importer.defaultSampleSettings.preloadAudioData, Is.EqualTo(!loop));
                Assert.That(importer.forceToMono, Is.EqualTo(!loop));
                Assert.That(AssetDatabase.LoadAssetAtPath<AudioClip>(path), Is.Not.Null);
                Assert.That(File.Exists(backup), Is.True);
            }
            finally
            {
                AssetDatabase.DeleteAsset(path); AssetDatabase.DeleteAsset(backup);
                if (File.Exists(backup + ".meta.bak")) File.Delete(backup + ".meta.bak");
            }
        }
    }
}
