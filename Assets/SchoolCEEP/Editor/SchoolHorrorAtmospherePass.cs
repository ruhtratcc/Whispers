// Ferramenta da escola para construção ou acabamento.

using System;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SchoolCEEP.Editor
{
    /// <summary>Explicit scene-instance pass. Leaves prefab, office, road, menu, global settings and geometry intact.</summary>
    public static class SchoolHorrorAtmospherePass
    {
        public const string GroupName = "Atmosfera da escola — silêncio e fita";
        public const string ProfilePath = "Assets/SchoolCEEP/Materials/SchoolHorrorVolume.asset";

        [Serializable]
        public sealed class Report
        {
            public bool passed, schoolOnly, cameraEnabled, ownVolume, noLoopingBed, breathingPreserved, noProvisionalBreathing, noAmbientColliders;
            public int ambientPoints, shortFaultLights, pointClips, shaderErrors;
            public float intensity, grain, chromaPixels, trackingSeconds, trackingDuration, postExposure;
            public string profile, shader, scene;
            public string[] licensedClips = Array.Empty<string>();
            public string[] errors = Array.Empty<string>();
        }

        public static Report Apply(Transform root)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !root || EditorUtility.IsPersistent(root)
                || root.gameObject.scene.path != SchoolIntegrationPass.ScenePath)
                throw new InvalidOperationException("Configure a loaded SchoolCEEP scene instance outside Play.");
            var scene = root.gameObject.scene;
            var all = scene.GetRootGameObjects();
            var game = all.SelectMany(g => g.GetComponentsInChildren<GameSession>(true)).Single();
            var camera = game.player.view.playerCamera;
            var effect = camera.GetComponent<VintageCameraEffect>();
            if (!effect) effect = camera.gameObject.AddComponent<VintageCameraEffect>();
            effect.ConfigureSchoolTreatment(); effect.ConfigureForChapter(2); effect.enabled = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            Record(effect, data);
            ConfigureProfile(game.atmosphere);

            // Only this scene's old placeholder bed is silenced. Recorded steps/props and audio mixer preferences remain.
            foreach (var manager in all.SelectMany(g => g.GetComponentsInChildren<AudioManager>(true)))
                foreach (var source in new[] { manager.calmMusic, manager.tenseMusic, manager.ambience })
                    if (source) { source.clip = null; source.playOnAwake = false; Record(source); }
            foreach (var random in all.SelectMany(g => g.GetComponentsInChildren<RandomScareAudio>(true)))
            { random.enabled = false; Record(random); }
            foreach (var loop in all.SelectMany(g => g.GetComponentsInChildren<SpatialAmbience>(true)))
            { loop.enabled = false; Record(loop); }
            foreach (var oldFault in root.GetComponentsInChildren<FlickerLight>(true))
            { oldFault.enabled = false; Record(oldFault); }

            Transform group = root.Find(GroupName);
            if (!group) { group = new GameObject(GroupName).transform; group.SetParent(root, false); }
            foreach (Transform child in group.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
            var runtime = group.GetComponent<SchoolHorrorAtmosphere>();
            if (!runtime) runtime = group.gameObject.AddComponent<SchoolHorrorAtmosphere>();
            runtime.session = game; runtime.listener = game.player.transform; runtime.cameraEffect = effect;
            runtime.initialSilence = 36; runtime.quietInterval = new Vector2(38, 68); runtime.automaticAmbience = true;
            var creak = RequiredSound("Room creaks"); var metal = RequiredSound("Impact Metal");
            runtime.points = new[]
            {
                Point(group, "Hall — travessa distante", SchoolMapBuilder.Point(1416, 1037, 4.1f), creak, 17, .32f),
                Point(group, "Cantina — cobertura metálica", SchoolMapBuilder.Point(1458, 848, 4.7f), metal, 15, .28f),
                Point(group, "Hospício — madeira ao fundo", SchoolMapBuilder.Point(488, 450, 4.9f), creak, 16, .35f),
                Point(group, "Escada central — andar vazio", SchoolMapBuilder.Point(1018, 759, 5.5f), creak, 16, .32f),
                Point(group, "Quadra — estrutura distante", SchoolMapBuilder.Point(2530, 1240, 7.7f), metal, 18, .24f)
            };
            runtime.flickerLights = ConfigureFixtures(root);
            runtime.fixtureBaseIntensity = runtime.flickerLights.Select(light => light.intensity).ToArray();
            Record(runtime);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            return Validate(root);
        }

        static void ConfigureProfile(HorrorAtmosphere atmosphere)
        {
            if (!atmosphere || !atmosphere.volume || !atmosphere.volume.sharedProfile)
                throw new InvalidOperationException("School gameplay must contain its existing volume.");
            var target = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (!target)
            {
                target = ScriptableObject.CreateInstance<VolumeProfile>();
                target.name = "SchoolHorrorVolume";
                foreach (var component in atmosphere.volume.sharedProfile.components)
                    if (component) target.components.Add(UnityEngine.Object.Instantiate(component));
                AssetDatabase.CreateAsset(target, ProfilePath);
                foreach (var component in target.components) AssetDatabase.AddObjectToAsset(component, target);
            }
            var color = Get<ColorAdjustments>(target);
            color.postExposure.Override(.24f); color.contrast.Override(5); color.saturation.Override(-12);
            color.colorFilter.Override(new Color(.96f, 1, .985f, 1));
            var balance = Get<WhiteBalance>(target); balance.temperature.Override(-8); balance.tint.Override(-4);
            var vignette = Get<Vignette>(target); vignette.intensity.Override(.28f); vignette.smoothness.Override(.45f);
            var bloom = Get<Bloom>(target); bloom.intensity.Override(.16f); bloom.threshold.Override(1.2f); bloom.scatter.Override(.45f);
            Get<FilmGrain>(target).intensity.Override(0); Get<ChromaticAberration>(target).intensity.Override(0);
            Get<MotionBlur>(target).intensity.Override(0);
            atmosphere.volume.sharedProfile = target;
            atmosphere.volume.isGlobal = true; atmosphere.volume.weight = 1;
            // Keep the user's selected human breath recording and its effort/scare response unchanged.
            var serialized = new SerializedObject(atmosphere);
            serialized.FindProperty("baseExposure").floatValue = .24f;
            serialized.FindProperty("adaptationGain").floatValue = .18f;
            serialized.FindProperty("defaultDarkness").floatValue = .35f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (var item in target.components) EditorUtility.SetDirty(item);
            foreach (var item in target.components)
                if (!AssetDatabase.Contains(item)) AssetDatabase.AddObjectToAsset(item, target);
            Record(target, atmosphere, atmosphere.volume);
        }

        static Light[] ConfigureFixtures(Transform root)
        {
            var hall = root.GetComponentsInChildren<Light>(true)
                .Where(light => light.name == "Hall — iluminação legível").OrderBy(light => light.transform.position.z).ToArray();
            foreach (var light in hall)
            {
                light.intensity = 4.4f; light.range = 12; light.color = new Color(.79f, .86f, .81f);
                light.enabled = true; Record(light);
            }
            // Existing geometry already supplies the light fixtures; use a few, leave the main orientation lights steady.
            var corridor = root.GetComponentsInChildren<Light>(true)
                .Where(light => light.type == LightType.Point && light.name == "Luz de ambiente")
                .OrderBy(light => Vector3.Distance(light.transform.position, SchoolMapBuilder.Point(1018, 705, 3))).FirstOrDefault();
            if (corridor)
            { corridor.intensity = Mathf.Max(1.45f, corridor.intensity); corridor.color = new Color(.81f, .85f, .76f); Record(corridor); }
            return hall.Concat(corridor ? new[] { corridor } : Array.Empty<Light>()).ToArray();
        }

        static SchoolHorrorAtmosphere.AmbientPoint Point(Transform group, string name, Vector3 position, SoundSet sound, float radius, float gain)
        {
            var anchor = new GameObject(name).transform; anchor.SetParent(group, false); anchor.position = position;
            return new SchoolHorrorAtmosphere.AmbientPoint { origin = anchor, sound = sound, hearingRadius = radius, gain = gain };
        }

        static SoundSet RequiredSound(string name)
        {
            var sound = AssetDatabase.LoadAssetAtPath<SoundSet>("Assets/HorrorGameplay/Audio/Recorded/Sets/" + name + ".asset");
            if (!sound || sound.clips == null || !sound.clips.Any(clip => clip))
                throw new InvalidOperationException("Missing licensed Foley set: " + name);
            return sound;
        }

        static T Get<T>(VolumeProfile profile) where T : VolumeComponent => profile.TryGet<T>(out var component) ? component : profile.Add<T>(true);

        static void Record(params UnityEngine.Object[] items)
        {
            foreach (var item in items)
            {
                if (!item) continue;
                EditorUtility.SetDirty(item);
                if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }
        }

        public static Report Validate(Transform root)
        {
            var scene = root.gameObject.scene;
            var game = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameSession>(true)).Single();
            var effect = game.player.view.playerCamera.GetComponent<VintageCameraEffect>();
            var runtime = root.GetComponentInChildren<SchoolHorrorAtmosphere>(true);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/HorrorGameplay/Shaders/VintageCamera.shader");
            var errors = new System.Collections.Generic.List<string>();
            int shaderErrors = shader ? ShaderUtil.GetShaderMessages(shader).Count(message => message.severity.ToString() == "Error") : 1;
            var managers = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AudioManager>(true));
            bool noLoop = managers.All(manager => new[] { manager.calmMusic, manager.tenseMusic, manager.ambience }.All(source => !source || !source.clip));
            bool realBreath = managers.All(manager => !manager.breathing || !manager.breathing.clip
                || AssetDatabase.GetAssetPath(manager.breathing.clip) == "Assets/HorrorGameplay/Audio/Narrative/Clips/HeavyBreathing.wav");
            var clips = runtime ? runtime.points.Where(point => point != null && point.sound).SelectMany(point => point.sound.clips)
                .Where(clip => clip).Select(clip => AssetDatabase.GetAssetPath(clip)).Distinct().ToArray() : Array.Empty<string>();
            bool licensed = clips.Length > 0 && clips.All(path => path.StartsWith("Assets/HorrorGameplay/Audio/Recorded/Clips/", StringComparison.Ordinal));
            bool own = AssetDatabase.GetAssetPath(game.atmosphere.volume.sharedProfile) == ProfilePath;
            // Outside Play the transient blend has not run OnEnable; validate the saved configuration.
            if (!effect || !effect.SchoolTreatmentAllowed || !effect.ChapterAllowed || !effect.enabled
                || effect.chapterIndex != 2 || effect.intensity < .8f) errors.Add("School camera preset missing.");
            if (!runtime || runtime.points.Length != 5 || runtime.flickerLights.Length == 0) errors.Add("Ambient anchors or fixtures missing.");
            if (!own || !noLoop || !realBreath || !licensed) errors.Add("Scene-owned profile/silence/recorded Foley check failed.");
            if (shaderErrors != 0 || !shader || !shader.isSupported) errors.Add("Vintage shader unavailable or has compile errors.");
            bool noColliders = runtime && runtime.GetComponentsInChildren<Collider>(true).Length == 0;
            if (!noColliders) errors.Add("Atmosphere group must not obstruct navigation.");
            return new Report
            {
                passed = errors.Count == 0, scene = scene.path, schoolOnly = scene.name == "SchoolCEEP",
                cameraEnabled = effect && effect.enabled, ownVolume = own, noLoopingBed = noLoop,
                breathingPreserved = realBreath, noProvisionalBreathing = realBreath, noAmbientColliders = noColliders,
                ambientPoints = runtime ? runtime.points.Length : 0, shortFaultLights = runtime ? runtime.flickerLights.Length : 0,
                pointClips = clips.Length, shaderErrors = shaderErrors, intensity = effect ? effect.intensity : 0,
                grain = effect ? effect.grain : 0, chromaPixels = effect ? effect.chromaticPixels : 0,
                trackingSeconds = effect ? effect.trackingInterval : 0, trackingDuration = effect ? effect.trackingDuration : 0,
                postExposure = .24f, profile = ProfilePath, shader = shader ? AssetDatabase.GetAssetPath(shader) : "",
                licensedClips = clips, errors = errors.ToArray()
            };
        }
    }
}
