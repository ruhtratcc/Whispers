// Ferramenta de preparação da cena e da apresentação do menu.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HorrorGame;
using HorrorGame.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SchoolMenu.Editor
{
    /// <summary>Each native editing pass runs outside Pipeline and only after an explicit request.</summary>
    public static class PresentationPolishBootstrap
    {
        const string Office = "Assets/ChapterOneOffice/Scenes/Chapter1Office.unity";
        const string Documentation = "Assets/ChapterOneOffice/Documentation";
        const string Request = Documentation + "/Presentation-polish.request";
        const string PointerRequest = Documentation + "/Menu-pointer-fix.request";
        const string ReportPath = Documentation + "/Presentation-polish-apply.json";
        const string Package = @"C:\Users\ruhtra\Documents\Codex\2026-09-29\oi\work\PresentationPolish";
        static bool working;
        static double idleSince = -1, retryAfter, nextRequestProbe;
        static MethodInfo runActiveQuery;
        [Serializable] sealed class Report { public string generatedUtc, status, error; public bool applied, testsScheduled; public int completedStages; }
        [Serializable] sealed class Validation { public bool passed = false; }

        [InitializeOnLoadMethod]
        static void ResumeExplicitRequest()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("Whispers presentation observer ready: menu-lamp-hud-office-on.");
        }

        static void Tick()
        {
            // Observe only explicit requests, even when they arrive after an earlier operation ended.
            // No request means no scene editing; one idle file check per second avoids extra work.
            if (EditorApplication.timeSinceStartup < nextRequestProbe) return;
            if (!File.Exists(Request))
            {
                nextRequestProbe = EditorApplication.timeSinceStartup + 1;
                idleSince = -1;
                return;
            }
            if (working) return;
            // An explicit correction edits saved scenes, so end a manual preview before applying it.
            // Never interrupt a Test Runner operation or a normal reload without a request.
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && !TestsBusy()
                && (Application.isPlaying || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                && int.TryParse(File.ReadAllText(Request).Trim(), out int requestedStage) && requestedStage >= 6 && requestedStage <= 7)
            {
                EditorApplication.isPlaying = false;
                idleSince = -1;
                return;
            }
            if (EditorBusy()) { idleSince = -1; return; }
            // A reload/Play exit can clear managed flags before native scene editing is ready.
            if (idleSince < 0) { idleSince = EditorApplication.timeSinceStartup; return; }
            if (EditorApplication.timeSinceStartup - idleSince < .25 || EditorApplication.timeSinceStartup < retryAfter) return;
            working = true;
            var report = File.Exists(ReportPath) ? JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath)) : new Report();
            int stage = -1;
            try
            {
                if (!int.TryParse(File.ReadAllText(Request).Trim(), out stage)) throw new InvalidOperationException("Pedido da revisão inválido.");
                report.generatedUtc = DateTime.UtcNow.ToString("o");
                report.status = "Applying";
                report.error = "";
                // These correction stages do not schedule the previous gameplay/door test suite.
                if (stage == 6 || stage == 7) report.testsScheduled = false;
                if (stage == 0)
                {
                    report.applied = report.testsScheduled = false;
                    if (SceneManager.GetActiveScene().path != Office) EditorSceneManager.OpenScene(Office);
                    OfficeAtmospherePolishPass.Apply();
                }
                else if (stage == 1) HudPresentationPass.Apply();
                else if (stage == 2) MenuPresentationPass.Apply();
                else if (stage == 3)
                {
                    ValidateAndCapture();
                }
                else if (stage == 4)
                {
                    if (SceneManager.GetActiveScene().path != Office) EditorSceneManager.OpenScene(Office);
                    report.applied = true;
                    report.testsScheduled = true;
                    report.status = "Applied; testing";
                    SessionState.SetString("Whispers.OfficePolish.OriginalScene", Office);
                    SessionState.SetString("Whispers.OfficePolish.TestsPhase", "");
                    SessionState.SetBool("Whispers.OfficePolish.RunTests", true);
                    DeleteRequest();
                    Debug.Log("Menu, HUD, luzes, rua visual e chapéu aplicados. Verificação agendada.");
                }
                else if (stage == 6)
                {
                    ImportPresentationCorrection();
                    report.status = "Importing presentation correction";
                }
                else if (stage == 7)
                {
                    if (File.Exists(PointerRequest))
                    {
                        SaveOpenScenes();
                        MenuPresentationPass.RepairPointerTargets();
                        report.applied = true;
                        report.status = "Applied; verified";
                        report.completedStages = 8;
                        if (!AssetDatabase.DeleteAsset(PointerRequest) && File.Exists(PointerRequest)) File.Delete(PointerRequest);
                        DeleteRequest();
                        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
                        AssetDatabase.SaveAssets();
                        return;
                    }
                    // A previous loaded observer can import its older source list before this reload.
                    // Import the HUD correction before applying any scenes in that case.
                    string hudSource = Path.Combine(Package, "HUD/HudPresentationPass.cs");
                    const string HudTarget = "Assets/HorrorGameplay/Editor/HudPresentationPass.cs";
                    if (!File.Exists(HudTarget) || File.ReadAllText(HudTarget) != File.ReadAllText(hudSource))
                    {
                        ImportPresentationCorrection();
                        report.status = "Importing presentation correction";
                        report.completedStages = 7;
                        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
                        return;
                    }
                    SaveOpenScenes();
                    HudPresentationPass.Apply();
                    MenuPresentationPass.Apply();
                    if (SceneManager.GetActiveScene().path != Office) EditorSceneManager.OpenScene(Office);
                    OfficeAtmospherePolishPass.Apply();
                    ValidateAndCapture();
                    report.applied = true;
                    report.status = "Applied; verified";
                    DeleteRequest();
                    Debug.Log("Lâmpada do menu, HUD e escritório sempre aceso aplicados; prévias verificadas. Testes da porta não repetidos.");
                }
                else throw new InvalidOperationException("Etapa desconhecida.");
                report.completedStages = stage + 1;
                if (File.Exists(Request)) File.WriteAllText(Request, (stage + 1).ToString());
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
                AssetDatabase.SaveAssets();
            }
            catch (Exception error)
            {
                report.generatedUtc = DateTime.UtcNow.ToString("o");
                if (stage >= 0 && TransientPlayBusy(error))
                {
                    report.status = "Waiting for Editor; stage " + stage;
                    report.error = "";
                    report.applied = false;
                    if (stage == 6 || stage == 7) report.testsScheduled = false;
                    // Preserve the exact stage so the already imported correction resumes safely.
                    File.WriteAllText(Request, stage.ToString());
                    File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
                    retryAfter = EditorApplication.timeSinceStartup + .75;
                    idleSince = -1;
                    return;
                }
                report.status = "Failed";
                report.error = error.ToString();
                report.applied = false;
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
                DeleteRequest();
                Debug.LogException(error);
            }
            finally { working = false; }
        }

        static bool EditorBusy()
        {
            if (Application.isPlaying || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || EditorApplication.isUpdating) return true;
            return TestsBusy();
        }

        static bool TestsBusy()
        {
            if (SessionState.GetBool("Whispers.OfficePolish.RunTests", false))
            {
                string phase = SessionState.GetString("Whispers.OfficePolish.TestsPhase", "");
                if (phase == "edit-running" || phase == "play-running") return true;
            }
            if (runActiveQuery == null)
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("UnityEditor.TestTools.TestRunner.Api.TestRunnerApi", false))
                    .FirstOrDefault(candidate => candidate != null);
                if (type != null) runActiveQuery = type.GetMethod("IsRunActive", BindingFlags.NonPublic | BindingFlags.Static);
            }
            // Read-only query also waits for Test Framework scene cleanup after Play mode exits.
            return runActiveQuery != null && runActiveQuery.Invoke(null, null) is bool active && active;
        }

        static bool TransientPlayBusy(Exception error)
        {
            for (var current = error; current != null; current = current.InnerException)
                if (current is InvalidOperationException && current.Message.Trim().TrimEnd('.') == "This cannot be used during play mode") return true;
            return false;
        }

        static void SaveOpenScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isDirty) continue;
                if (string.IsNullOrEmpty(scene.path) || !EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Salve a cena sem nome antes de aplicar a revisão.");
            }
        }

        static void ImportPresentationCorrection()
        {
            var backup = Path.Combine(Package, "Backups", "native-correction-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            var sources = new[]
            {
                ("PresentationCapture.cs", "Assets/HorrorGameplay/Editor/PresentationCapture.cs"),
                ("HUD/HudPresentationPass.cs", "Assets/HorrorGameplay/Editor/HudPresentationPass.cs"),
                ("Menu/Editor/MenuPresentationPass.cs", "Assets/HorrorMenu/Editor/MenuPresentationPass.cs"),
                ("Office/OfficeAtmospherePolishPass.cs", "Assets/HorrorGameplay/Editor/OfficeAtmospherePolishPass.cs"),
                ("Office/Scripts/InteractableLightSwitch.cs", "Assets/HorrorGameplay/Scripts/Interaction/InteractableLightSwitch.cs")
            };
            foreach (var file in sources)
                if (!File.Exists(Path.Combine(Package, file.Item1))) throw new FileNotFoundException("Fonte da revisão ausente", file.Item1);
            SaveOpenScenes();
            foreach (string asset in new[] { Office, MenuPresentationPass.ScenePath,
                "Assets/HorrorGameplay/Prefabs/GameplayRoot.prefab", "Assets/HorrorGameplay/Scenes/TestScene.unity"
            }.Concat(sources.Select(file => file.Item2)))
            {
                if (!File.Exists(asset)) continue;
                string destination = Path.Combine(backup, asset);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(asset, destination, true);
                if (File.Exists(asset + ".meta")) File.Copy(asset + ".meta", destination + ".meta", true);
            }
            foreach (var file in sources)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file.Item2));
                File.Copy(Path.Combine(Package, file.Item1), file.Item2, true);
            }
            // The new runtime component and editor passes compile together; resume after reload.
            File.WriteAllText(Request, "7");
            AssetDatabase.Refresh();
        }

        static void ValidateAndCapture()
        {
            if (SceneManager.GetActiveScene().path != Office) EditorSceneManager.OpenScene(Office);
            HudPresentationPass.Validate();
            RequirePassed(Documentation + "/HUD-presentation-report.json");
            MenuPresentationPass.ValidateAndCapture();
            RequirePassed("Assets/HorrorMenu/Documentation/Menu-presentation-report.json");
            OfficeAtmospherePolishPass.ValidateAndCapture();
            RequirePassed(Documentation + "/Office-atmosphere-polish-report.json");
            CaptureHud();
        }

        static void DeleteRequest()
        {
            // Remove the transient asset together with its meta through Unity's asset database.
            if (!AssetDatabase.DeleteAsset(Request) && File.Exists(Request)) File.Delete(Request);
        }

        static void RequirePassed(string path)
        {
            var validation = File.Exists(path) ? JsonUtility.FromJson<Validation>(File.ReadAllText(path)) : null;
            if (validation == null || !validation.passed) throw new InvalidOperationException("Validação falhou: " + path);
        }

        static void CaptureHud()
        {
            var scene = SceneManager.GetActiveScene();
            var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameHUD>(true)).FirstOrDefault();
            if (!hud) throw new InvalidOperationException("HUD do escritório ausente.");
            var camera = hud.player ? hud.player.GetComponentInChildren<Camera>(true) : null;
            if (!camera) throw new InvalidOperationException("Câmera do jogador ausente.");
            var panels = new[] { hud.pausePanel, hud.optionsPanel, hud.gameOverPanel, hud.notePanel, hud.inventoryPanel };
            var active = panels.Select(panel => panel && panel.activeSelf).ToArray();
            var textValues = hud.GetComponentsInChildren<TMPro.TMP_Text>(true).ToDictionary(text => text, text => text.text);
            var sliderValues = hud.GetComponentsInChildren<UnityEngine.UI.Slider>(true).ToDictionary(slider => slider, slider => slider.value);
            var toggleValues = hud.GetComponentsInChildren<UnityEngine.UI.Toggle>(true).ToDictionary(toggle => toggle, toggle => toggle.isOn);
            int languageValue = hud.options.languagePreview ? hud.options.languagePreview.value : 0;
            string prompt = hud.prompt.text, battery = hud.batteryLabel.text, objective = hud.objective.text;
            string key = hud.interactionKey ? hud.interactionKey.text : "";
            string batteryState = hud.batteryStateText ? hud.batteryStateText.text : "";
            float interactionAlpha = hud.interactionGroup ? hud.interactionGroup.alpha : 0;
            float batteryAlpha = hud.batteryGroup ? hud.batteryGroup.alpha : 0;
            float objectiveAlpha = hud.objectiveGroup.alpha, staminaAlpha = hud.staminaGroup.alpha;
            float batteryFill = hud.batteryFill.fillAmount;
            var flashlightLight = hud.flashlight ? hud.flashlight.lamp : null;
            bool flashlightEnabled = flashlightLight && flashlightLight.enabled;
            try
            {
                // Match the chapter's actual startup state; FlashlightController.Start does not run in Editor captures.
                if (flashlightLight) flashlightLight.enabled = false;
                foreach (var panel in panels) if (panel) panel.SetActive(false);
                hud.prompt.text = hud.interactionKey ? "Abrir gaveta" : "[E]  Abrir gaveta";
                if (hud.interactionKey) hud.interactionKey.text = "E";
                if (hud.interactionGroup) hud.interactionGroup.alpha = 1;
                if (hud.batteryGroup) hud.batteryGroup.alpha = .6f;
                if (hud.batteryStateText) hud.batteryStateText.text = "LANTERNA DESLIGADA";
                hud.objective.text = "Explore o escritório de João.";
                hud.objectiveGroup.alpha = 1;
                hud.staminaGroup.alpha = 0;
                hud.batteryLabel.text = "100%";
                hud.batteryFill.fillAmount = 1;
                PresentationCapture.Capture(scene, camera, Documentation + "/HUD-presentation-game.png");
                hud.prompt.text = "";
                if (hud.interactionGroup) hud.interactionGroup.alpha = 0;
                if (hud.batteryGroup) hud.batteryGroup.alpha = 0;
                hud.objectiveGroup.alpha = 0;
                hud.pausePanel.SetActive(true);
                PresentationCapture.Capture(scene, camera, Documentation + "/HUD-presentation-pause.png");
                hud.pausePanel.SetActive(false);
                hud.optionsPanel.SetActive(true);
                hud.options.Refresh();
                PresentationCapture.Capture(scene, camera, Documentation + "/HUD-presentation-options.png");
            }
            finally
            {
                if (flashlightLight) flashlightLight.enabled = flashlightEnabled;
                for (int i = 0; i < panels.Length; i++) if (panels[i]) panels[i].SetActive(active[i]);
                hud.prompt.text = prompt;
                if (hud.interactionKey) hud.interactionKey.text = key;
                if (hud.batteryStateText) hud.batteryStateText.text = batteryState;
                if (hud.interactionGroup) hud.interactionGroup.alpha = interactionAlpha;
                if (hud.batteryGroup) hud.batteryGroup.alpha = batteryAlpha;
                hud.batteryLabel.text = battery;
                hud.objective.text = objective;
                hud.objectiveGroup.alpha = objectiveAlpha;
                hud.staminaGroup.alpha = staminaAlpha;
                hud.batteryFill.fillAmount = batteryFill;
                foreach (var pair in textValues) if (pair.Key) pair.Key.text = pair.Value;
                foreach (var pair in sliderValues) if (pair.Key) pair.Key.SetValueWithoutNotify(pair.Value);
                foreach (var pair in toggleValues) if (pair.Key) pair.Key.SetIsOnWithoutNotify(pair.Value);
                if (hud.options.languagePreview) hud.options.languagePreview.SetValueWithoutNotify(languageValue);
            }
        }
    }
}
