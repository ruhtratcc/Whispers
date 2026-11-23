// Verifica InputAssetContainsAllRequiredActions, SoundSelectionNeverRepeatsWhenAlternativesExist, SaveReaderRejectsInvalidAndNonfiniteData, GameplayPrefabHasReferencesAndRenderingOverrides, SceneObjectsHaveUniqueSaveIds.

using System.IO;
using System.Linq;
using HorrorGame;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorGame.Tests
{
    public sealed class FoundationEditTests
    {
        [Test]
        public void InputAssetContainsAllRequiredActions()
        {
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/HorrorGameplay/Input/HorrorInput.inputactions");
            Assert.NotNull(input);
            foreach (var name in new[]
            {
                "Move",
                "Look",
                "Sprint",
                "Crouch",
                "Jump",
                "Interact",
                "PrimaryInteract",
                "Drop",
                "Throw",
                "Flashlight",
                "Inventory",
                "HoldBreath"
            }

            )
                Assert.NotNull(input.FindAction("Gameplay/" + name), name);
            foreach (var name in new[]
            {
                "Navigate",
                "Submit",
                "Cancel",
                "Point",
                "Click",
                "ScrollWheel"
            }

            )
                Assert.NotNull(input.FindAction("UI/" + name), name);
            Assert.NotNull(input.FindAction("Always/Pause"));
            Assert.IsNull(input.FindAction("Gameplay/Recording"), "The retired camcorder action must stay absent.");
        }

        [Test]
        public void SoundSelectionNeverRepeatsWhenAlternativesExist()
        {
            var sound = ScriptableObject.CreateInstance<SoundSet>();
            var a = AudioClip.Create("a", 64, 1, 22050, false);
            var b = AudioClip.Create("b", 64, 1, 22050, false);
            try
            {
                sound.clips = new[]
                {
                    a,
                    null,
                    a,
                    b
                };
                AudioClip last = null;
                for (int i = 0; i < 100; i++)
                {
                    var clip = sound.Next();
                    Assert.NotNull(clip);
                    Assert.AreNotEqual(last, clip);
                    last = clip;
                }
            }
            finally
            {
                Object.DestroyImmediate(sound);
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void SaveReaderRejectsInvalidAndNonfiniteData()
        {
            string path = Path.Combine(Path.GetTempPath(), "WhispersSaveTest-" + System.Guid.NewGuid() + ".json");
            try
            {
                File.WriteAllText(path, "broken");
                Assert.IsFalse(SaveSystem.TryRead(path, out _));
                File.WriteAllText(path, JsonUtility.ToJson(new SaveData { scene = "TestScene", position = new Vector3(float.NaN, 0, 0) }));
                Assert.IsFalse(SaveSystem.TryRead(path, out _));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void GameplayPrefabHasReferencesAndRenderingOverrides()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/HorrorGameplay/Prefabs/GameplayRoot.prefab");
            try
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    Assert.Zero(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject), t.name);
                var game = root.GetComponent<GameSession>();
                Assert.NotNull(game.player);
                Assert.NotNull(game.hud);
                Assert.NotNull(game.save);
                Assert.NotNull(game.scenes);
                Assert.NotNull(game.input.asset);
                Assert.IsTrue(game.player.view.playerCamera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing);
                var profile = game.atmosphere.volume.sharedProfile;
                Assert.IsTrue(profile.Has<Vignette>());
                Assert.IsTrue(profile.Has<FilmGrain>());
                Assert.IsTrue(profile.Has<ChromaticAberration>());
                Assert.IsTrue(profile.Has<ColorAdjustments>());
                Assert.IsTrue(profile.Has<Bloom>());
                var audio = root.GetComponentInChildren<AudioManager>();
                foreach (string parameter in new[]
                {
                    "MusicVolume",
                    "SFXVolume",
                    "AmbienceVolume",
                    "VoiceVolume"
                }

                )
                    Assert.IsTrue(audio.mixer.GetFloat(parameter, out _), parameter);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void SceneObjectsHaveUniqueSaveIds()
        {
            var scene = EditorSceneManager.OpenScene("Assets/HorrorGameplay/Scenes/TestScene.unity", OpenSceneMode.Additive);
            try
            {
                var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PersistentObject>(true)).ToArray();
                Assert.Greater(objects.Length, 3);
                Assert.IsFalse(objects.Any(p => string.IsNullOrWhiteSpace(p.persistentId)));
                Assert.AreEqual(objects.Length, objects.Select(p => p.persistentId).Distinct().Count());
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
