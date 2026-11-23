// Ferramenta da escola para construção ou acabamento.

using System;
using System.Linq;
using HorrorGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SchoolCEEP.Editor
{
    /// <summary>Opt the existing school flashlight into local shadow and close inspection settings.</summary>
    public static class SchoolImmersionLightingPass
    {
        public static object Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "SchoolCEEP" || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Polish the school outside Play mode.");
            var game = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameSession>(true)).Single();
            var controller = game.save.flashlight;
            var lamp = controller.lamp;
            var data = lamp.GetComponent<UniversalAdditionalLightData>();
            var before = new { lamp.shadowBias, lamp.shadowNormalBias, lamp.shadowNearPlane,
                usePipelineSettings = !data || data.usePipelineSettings, controller.nearSurfaceDimming };
            if (!data) data = lamp.gameObject.AddComponent<UniversalAdditionalLightData>();
            // URP ignores per-light bias while pipeline settings are selected.
            data.usePipelineSettings = false;
            // Unity persists a minimum near plane of 0.1 m for this light.
            lamp.shadowBias = .015f; lamp.shadowNormalBias = .04f; lamp.shadowNearPlane = .1f;
            controller.nearSurfaceDimming = true;
            EditorUtility.SetDirty(data); EditorUtility.SetDirty(lamp); EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            return new { before, after = new { lamp.shadowBias, lamp.shadowNormalBias, lamp.shadowNearPlane,
                data.usePipelineSettings, controller.nearSurfaceDimming }, unchangedBaseIntensity = lamp.intensity,
                unchangedRange = lamp.range, schoolOnly = true };
        }
    }
}
