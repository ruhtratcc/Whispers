// Ferramenta de preparação ou verificação usada no Editor.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorGame.PolishVerification
{
    /// <summary>Runs only after an explicit polish request. Normal reloads never run tests.</summary>
    [InitializeOnLoad]
    public static class OfficePolishTestCoordinator
    {
        public const string RunKey = "Whispers.OfficePolish.RunTests";
        public const string SceneKey = "Whispers.OfficePolish.OriginalScene";
        const string PhaseKey = "Whispers.OfficePolish.TestsPhase";
        const string ReportKey = "Whispers.OfficePolish.TestsReport";
        const string DirectoryPath = "Assets/ChapterOneOffice/Documentation";
        const string JsonPath = DirectoryPath + "/Office-polish-tests.json";
        const string XmlPath = DirectoryPath + "/Office-polish-tests.xml";
        const string EditXmlPath = DirectoryPath + "/Office-polish-tests-edit.xml";
        const string PlayXmlPath = DirectoryPath + "/Office-polish-tests-play.xml";
        const string OfficeScene = "Assets/ChapterOneOffice/Scenes/Chapter1Office.unity";
        static readonly MethodInfo isRunActive = typeof(TestRunnerApi).GetMethod("IsRunActive", BindingFlags.NonPublic | BindingFlags.Static);
        static readonly Callbacks callbacks = new Callbacks();

        [Serializable] public sealed class Failure
        {
            public string test, state, message, stackTrace;
        }
        [Serializable] public sealed class Stage
        {
            public string mode, result;
            public int expectedMinimum, passed, failed, skipped, inconclusive;
            public double duration;
            public bool success;
            public List<Failure> failures = new List<Failure>();
        }
        [Serializable] public sealed class Report
        {
            public string startedUtc, generatedUtc, scene, status;
            public bool completed, passed;
            public Stage editMode, playMode;
            public List<string> errors = new List<string>();
        }

        static OfficePolishTestCoordinator()
        {
            // The callback holder is recreated at a domain reload; register in every domain.
            TestRunnerApi.RegisterTestCallback(callbacks, -100);
            EditorApplication.update += Tick;
        }

        /// <summary>Optional reflection entry point; the two public SessionState keys also suffice.</summary>
        public static void Request(string originalScene)
        {
            if (SessionState.GetBool(RunKey, false)) return;
            SessionState.SetString(SceneKey, string.IsNullOrEmpty(originalScene) ? OfficeScene : originalScene);
            SessionState.SetString(PhaseKey, "");
            SessionState.SetBool(RunKey, true);
        }

        static bool RunnerBusy()
        {
            // The installed Test Framework has no public cleanup-complete query. Its internal
            // query is used only to wait; no internals or project settings are modified.
            return isRunActive != null && (bool)isRunActive.Invoke(null, null);
        }

        static void Tick()
        {
            if (!SessionState.GetBool(RunKey, false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string phase = SessionState.GetString(PhaseKey, "");
            if (phase == "edit-running" || phase == "play-running") return;
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || RunnerBusy()) return;
                if (phase == "")
                {
                    string scene = SessionState.GetString(SceneKey, "");
                    if (string.IsNullOrEmpty(scene)) scene = SceneManager.GetActiveScene().path;
                    if (string.IsNullOrEmpty(scene)) scene = OfficeScene;
                    SessionState.SetString(SceneKey, scene);
                    Save(new Report { startedUtc = DateTime.UtcNow.ToString("O"), scene = scene, status = "EditMode queued" });
                    SessionState.SetString(PhaseKey, "edit-pending");
                    phase = "edit-pending";
                }
                if (phase == "edit-pending") Launch(TestMode.EditMode, "edit-running", "HorrorGame.EditTests");
                else if (phase == "play-pending") Launch(TestMode.PlayMode, "play-running", "HorrorGame.PlayTests");
                else if (phase == "finish-pending") Finish();
            }
            catch (Exception error) { Fail(error.ToString()); }
        }

        static void Launch(TestMode mode, string phase, string assembly)
        {
            SessionState.SetString(PhaseKey, phase);
            var report = Read();
            report.status = mode + " running";
            Save(report);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            try
            {
                // targetPlatform remains null: PlayMode runs in the Editor and never builds a player.
                api.Execute(new ExecutionSettings(new Filter {
                    testMode = mode, assemblyNames = new[] { assembly },
                    groupNames = new[] { "^HorrorGame\\.Tests\\." }
                }));
            }
            finally { UnityEngine.Object.DestroyImmediate(api); }
        }

        static Report Read()
        {
            return JsonUtility.FromJson<Report>(SessionState.GetString(ReportKey, "{}")) ?? new Report();
        }

        static void Save(Report report)
        {
            report.generatedUtc = DateTime.UtcNow.ToString("O");
            string json = JsonUtility.ToJson(report, true);
            SessionState.SetString(ReportKey, json);
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(JsonPath, json, new UTF8Encoding(false));
        }

        static void Fail(string message)
        {
            var report = Read();
            report.errors.Add(message);
            report.status = "Verification failed";
            report.passed = false;
            Save(report);
            SessionState.SetString(PhaseKey, "finish-pending");
        }

        static Stage Summarize(ITestResultAdaptor result, string mode, int expected)
        {
            var stage = new Stage {
                mode = mode, result = result.ResultState, expectedMinimum = expected,
                passed = result.PassCount, failed = result.FailCount, skipped = result.SkipCount,
                inconclusive = result.InconclusiveCount, duration = result.Duration
            };
            stage.success = result.ResultState == "Passed" && stage.passed >= expected && stage.failed == 0 && stage.skipped == 0 && stage.inconclusive == 0;
            CollectFailures(result, stage.failures);
            return stage;
        }

        static void CollectFailures(ITestResultAdaptor result, List<Failure> failures)
        {
            if (result.HasChildren)
            {
                foreach (var child in result.Children) CollectFailures(child, failures);
            }
            else if (result.ResultState != "Passed")
                failures.Add(new Failure { test = result.FullName, state = result.ResultState, message = result.Message, stackTrace = result.StackTrace });
        }

        static void Finish()
        {
            // Called only after the Test Framework has exited PlayMode and completed scene cleanup.
            var report = Read();
            report.passed = report.errors.Count == 0 && report.editMode != null && report.editMode.success && report.playMode != null && report.playMode.success;
            report.completed = true;
            report.status = report.passed ? "Passed" : "Failed";
            if (File.Exists(report.scene) && SceneManager.GetActiveScene().path != report.scene)
                EditorSceneManager.OpenScene(report.scene, OpenSceneMode.Single);
            WriteCombinedXml(report);
            Save(report);
            SessionState.SetBool(RunKey, false);
            SessionState.SetString(PhaseKey, "");
            AssetDatabase.ImportAsset(JsonPath);
            AssetDatabase.ImportAsset(XmlPath);
            Debug.Log("Office polish verification " + report.status + ": " + (report.editMode?.passed ?? 0) + " EditMode, " + (report.playMode?.passed ?? 0) + " PlayMode. Report: " + JsonPath);
        }

        static void WriteCombinedXml(Report report)
        {
            int passed = (report.editMode?.passed ?? 0) + (report.playMode?.passed ?? 0);
            int failed = (report.editMode?.failed ?? 0) + (report.playMode?.failed ?? 0);
            int skipped = (report.editMode?.skipped ?? 0) + (report.playMode?.skipped ?? 0);
            int inconclusive = (report.editMode?.inconclusive ?? 0) + (report.playMode?.inconclusive ?? 0);
            var root = new XElement("test-run", new XAttribute("id", "office-polish"), new XAttribute("result", report.status),
                new XAttribute("testcasecount", passed + failed + skipped + inconclusive), new XAttribute("total", passed + failed + skipped + inconclusive),
                new XAttribute("passed", passed), new XAttribute("failed", failed), new XAttribute("skipped", skipped), new XAttribute("inconclusive", inconclusive),
                new XAttribute("duration", ((report.editMode?.duration ?? 0) + (report.playMode?.duration ?? 0)).ToString(CultureInfo.InvariantCulture)),
                new XAttribute("start-time", report.startedUtc ?? ""), new XAttribute("end-time", DateTime.UtcNow.ToString("O")));
            AppendResultXml(root, report.editMode, EditXmlPath, "edit-");
            AppendResultXml(root, report.playMode, PlayXmlPath, "play-");
            if (report.errors.Count > 0) root.Add(new XElement("failure", new XElement("message", string.Join("\n", report.errors))));
            new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(XmlPath);
        }

        static void AppendResultXml(XElement root, Stage stage, string path, string idPrefix)
        {
            if (stage == null || !File.Exists(path)) return;
            var saved = XDocument.Load(path).Root;
            if (saved == null) return;
            foreach (var element in saved.Elements())
            {
                var copy = new XElement(element);
                foreach (var node in copy.DescendantsAndSelf())
                {
                    var id = node.Attribute("id");
                    if (id != null) id.Value = idPrefix + id.Value;
                }
                root.Add(copy);
            }
        }

        sealed class Callbacks : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void OnError(string message)
            {
                if (SessionState.GetBool(RunKey, false)) Fail(message);
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(RunKey, false)) return;
                string phase = SessionState.GetString(PhaseKey, "");
                if (phase != "edit-running" && phase != "play-running") return;
                try
                {
                    var report = Read();
                    bool edit = phase == "edit-running";
                    var stage = Summarize(result, edit ? "EditMode" : "PlayMode", edit ? 5 : 14);
                    TestRunnerApi.SaveResultToFile(result, edit ? EditXmlPath : PlayXmlPath);
                    if (edit) report.editMode = stage;
                    else report.playMode = stage;
                    if (!stage.success) report.errors.Add(stage.mode + " did not pass all required tests (minimum " + stage.expectedMinimum + ").");
                    report.status = edit && stage.success ? "PlayMode queued" : "Finalizing";
                    Save(report);
                    SessionState.SetString(PhaseKey, edit && stage.success ? "play-pending" : "finish-pending");
                }
                catch (Exception error) { Fail(error.ToString()); }
            }
        }
    }
}
