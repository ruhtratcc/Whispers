// Ferramenta de preparação ou verificação usada no Editor.

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.Editor
{
    public static class OfficePolishBootstrap
    {
        const string Request = "Assets/ChapterOneOffice/Documentation/Office-polish.request";
        const string ReportPath = "Assets/ChapterOneOffice/Documentation/Office-polish-apply.json";
        [Serializable] sealed class Report { public string generatedUtc, error; public bool applied, testsScheduled; }
        [Serializable] sealed class ValidationFlag { public bool passed = false; }

        [InitializeOnLoadMethod]
        static void ConsumeExplicitRequest()
        {
            if (!File.Exists(Request)) return;
            EditorApplication.delayCall += WaitUntilReady;
        }
        static void WaitUntilReady()
        {
            if (!File.Exists(Request)) { EditorApplication.update -= WaitUntilReady; return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.update -= WaitUntilReady;
                EditorApplication.update += WaitUntilReady;
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.update -= WaitUntilReady;
                EditorApplication.update += WaitUntilReady;
                return;
            }
            EditorApplication.update -= WaitUntilReady;
            File.Delete(Request);
            var report = new Report { generatedUtc = DateTime.UtcNow.ToString("o") };
            try
            {
                OfficeDetailPolishPass.Apply();
                AudioPolishPass.Apply();
                OfficeDetailPolishPass.ValidateAndCapture();
                var details = JsonUtility.FromJson<ValidationFlag>(File.ReadAllText("Assets/ChapterOneOffice/Documentation/Office-detail-polish-report.json"));
                if (details == null || !details.passed) throw new InvalidOperationException("Validação visual falhou; consulte Office-detail-polish-report.json.");
                AudioPolishPass.Validate();
                report.applied = true;
                SessionState.SetString("Whispers.OfficePolish.OriginalScene", "Assets/ChapterOneOffice/Scenes/Chapter1Office.unity");
                SessionState.SetBool("Whispers.OfficePolish.RunTests", true);
                report.testsScheduled = true;
                Debug.Log("Office phone, drawer and Foley polish applied. Focused tests scheduled.");
            }
            catch (Exception error)
            {
                report.error = error.ToString();
                Debug.LogException(error);
            }
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            AssetDatabase.Refresh();
        }
    }
}
