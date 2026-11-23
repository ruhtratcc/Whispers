// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SchoolCEEP.Editor
{
    /// <summary>
    /// Installs only the school scene's integration overrides on an existing GameplayRoot instance.
    /// Save the new scene at ScenePath before Apply; save it again after Apply to persist the overrides.
    /// No prefab asset, menu starting scene, other chapter or narrative event is changed.
    /// </summary>
    public static class SchoolIntegrationPass
    {
        public const string SceneName = "SchoolCEEP";
        public const string ScenePath = "Assets/SchoolCEEP/Scenes/SchoolCEEP.unity";

        [Serializable]
        public sealed class IntegrationReport
        {
            public bool passed;
            public string scene, startingChapterId, schoolScene, newGameScene, roadScene;
            public int gameSessions, players, narrativeDirectors, saveSystems, prologueFlows;
            public bool referencesValid, buildSceneEnabled, flashlightStartsOn;
            public float flashlightIntensity;
            public Vector3 spawn;
            public string[] duplicatePersistentIds = Array.Empty<string>();
        }

        public static IntegrationReport Apply(GameObject gameplayRoot, Vector3 spawn)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Integrate SchoolCEEP outside Play mode.");
            if (!gameplayRoot || EditorUtility.IsPersistent(gameplayRoot))
                throw new ArgumentException("Provide the scene instance of GameplayRoot, not its prefab asset.");
            if (!Finite(spawn.x) || !Finite(spawn.y) || !Finite(spawn.z))
                throw new ArgumentException("School spawn must be finite.", nameof(spawn));

            var scene = gameplayRoot.scene;
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
                throw new InvalidOperationException("Save the new school scene at " + ScenePath + " before integration.");

            var game = Single<GameSession>(gameplayRoot);
            var player = Single<FirstPersonMotor>(gameplayRoot);
            var director = Single<NarrativeDirector>(gameplayRoot);
            var save = Single<SaveSystem>(gameplayRoot);
            var flashlight = Single<FlashlightController>(gameplayRoot);
            var controller = player.GetComponent<CharacterController>();
            if (!controller || !game.input || !game.interaction || !game.interaction.inventory
                || !game.hud || !game.scenes || !game.atmosphere || !player.view
                || !player.view.playerCamera || !flashlight.lamp || !director.transitionView)
                throw new InvalidOperationException("GameplayRoot is missing required current-game references.");
            if (game.player != player || game.save != save || game.interaction.transform != player.transform
                || save.player != player || save.inventory != game.interaction.inventory
                || save.flashlight != flashlight || save.atmosphere != game.atmosphere)
                throw new InvalidOperationException("GameplayRoot contains inconsistent player, inventory or save references.");

            var report = Inspect(scene);
            if (report.gameSessions != 1 || report.players != 1 || report.narrativeDirectors != 1
                || report.saveSystems != 1 || report.prologueFlows != 0)
                throw new InvalidOperationException("School scene requires exactly one gameplay root and no PrologueFlow.");
            if (report.duplicatePersistentIds.Length != 0)
                throw new InvalidOperationException("School contains missing or duplicate persistent IDs: "
                    + string.Join(", ", report.duplicatePersistentIds));

            var book = director.book ? director.book : game.scenes.storyBook;
            var school = book ? book.Chapter("school") : null;
            if (school == null || book.ChapterIndex("school") != 2)
                throw new InvalidOperationException("Existing StoryBook must contain school as chapter index 2.");
            if (game.scenes.storyBook != book)
                throw new InvalidOperationException("GameplayRoot and SceneCatalog must use the same existing StoryBook.");

            director.book = book;
            director.game = game;
            director.inventory = game.interaction.inventory;
            director.startingChapterId = "school";
            game.narrative = director;
            save.narrative = director;
            // Do not repopulate StoryContent: that would overwrite existing per-chapter map bindings.
            if (school.sceneName != SceneName)
            {
                school.sceneName = SceneName;
                EditorUtility.SetDirty(book);
                AssetDatabase.SaveAssetIfDirty(book);
            }

            bool wasEnabled = controller.enabled;
            try
            {
                controller.enabled = false;
                player.transform.position = spawn;
            }
            finally
            {
                controller.enabled = wasEnabled;
            }
            // Keep the inherited hand rig, battery, inputs and sounds. Runtime Awake reads startsOn.
            flashlight.startsOn = false;
            flashlight.lamp.enabled = false;
            flashlight.lamp.intensity = 90f;

            Record(game, save, director, player.transform, controller, flashlight, flashlight.lamp);
            RegisterBuildScene();
            EditorSceneManager.MarkSceneDirty(scene);

            report.scene = scene.path;
            report.startingChapterId = director.startingChapterId;
            report.schoolScene = school.sceneName;
            report.newGameScene = game.scenes.gameplayScene;
            report.roadScene = book.Chapter("road")?.sceneName ?? "";
            report.referencesValid = game.narrative == director && save.narrative == director
                && director.game == game && director.inventory == save.inventory;
            report.buildSceneEnabled = EditorBuildSettings.scenes.Any(entry => entry.path == ScenePath && entry.enabled);
            report.flashlightStartsOn = flashlight.startsOn;
            report.flashlightIntensity = flashlight.lamp.intensity;
            report.spawn = player.transform.position;
            report.passed = report.referencesValid && report.buildSceneEnabled
                && report.startingChapterId == "school" && report.schoolScene == SceneName
                && !report.flashlightStartsOn && Mathf.Approximately(report.flashlightIntensity, 90f);
            return report;
        }

        static T Single<T>(GameObject root) where T : Component
        {
            var components = root.GetComponentsInChildren<T>(true);
            if (components.Length != 1)
                throw new InvalidOperationException("GameplayRoot requires exactly one " + typeof(T).Name + ".");
            return components[0];
        }

        static IntegrationReport Inspect(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var persistent = roots.SelectMany(root => root.GetComponentsInChildren<PersistentObject>(true));
            var duplicates = persistent.GroupBy(item => item.persistentId ?? "")
                .Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)
                .Select(group => string.IsNullOrWhiteSpace(group.Key) ? "<missing>" : group.Key).ToArray();
            return new IntegrationReport
            {
                gameSessions = Count<GameSession>(roots), players = Count<FirstPersonMotor>(roots),
                narrativeDirectors = Count<NarrativeDirector>(roots), saveSystems = Count<SaveSystem>(roots),
                prologueFlows = Count<PrologueFlow>(roots), duplicatePersistentIds = duplicates
            };
        }

        static int Count<T>(GameObject[] roots) where T : Component =>
            roots.Sum(root => root.GetComponentsInChildren<T>(true).Length);

        static void Record(params UnityEngine.Object[] objects)
        {
            foreach (var item in objects)
            {
                EditorUtility.SetDirty(item);
                if (PrefabUtility.IsPartOfPrefabInstance(item))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }
        }

        static void RegisterBuildScene()
        {
            var entries = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var existing = entries.Where(entry => entry.path == ScenePath).ToArray();
            if (existing.Length == 0) entries.Add(new EditorBuildSettingsScene(ScenePath, true));
            else foreach (var entry in existing) entry.enabled = true;
            EditorBuildSettings.scenes = entries.ToArray();
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
