// Ferramenta da escola de validação.

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SchoolCEEP.Editor
{
    // Guards the real checkpoint before the test runner can initialize any gameplay scene.
    public static class SchoolOpeningTestGuard
    {
        const string GuardPath=SchoolMapBuilder.Output+"/School-opening-testguard.json";
        [Serializable] class Guard
        {
            public string scene,path,saveScene;
            public bool hadScene,hadResume,hadCarry;
            public int resume,carry;
            public string[] bytes;
            public bool[] existed;
        }
        public static string Begin()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Guard before Play.");
            if(File.Exists(GuardPath))throw new InvalidOperationException("Previous test guard still active; restore it first.");
            var scene=SceneManager.GetActiveScene();if(scene.isDirty)throw new InvalidOperationException("Save scene before testing.");
            string path=Path.Combine(Application.persistentDataPath,"save.json");
            var guard=new Guard{scene=scene.path,path=path,hadScene=PlayerPrefs.HasKey("Game.SaveScene"),
                hadResume=PlayerPrefs.HasKey("Game.ResumeRequested"),hadCarry=PlayerPrefs.HasKey("Game.StoryCarryRequested"),
                saveScene=PlayerPrefs.GetString("Game.SaveScene",""),resume=PlayerPrefs.GetInt("Game.ResumeRequested",0),
                carry=PlayerPrefs.GetInt("Game.StoryCarryRequested",0),existed=new[]{"",".bak",".tmp"}.Select(s=>File.Exists(path+s)).ToArray(),
                bytes=new[]{"",".bak",".tmp"}.Select(s=>File.Exists(path+s)?Convert.ToBase64String(File.ReadAllBytes(path+s)):"").ToArray()};
            File.WriteAllText(GuardPath,JsonUtility.ToJson(guard));
            PlayerPrefs.SetInt("Game.ResumeRequested",0);PlayerPrefs.SetInt("Game.StoryCarryRequested",0);PlayerPrefs.Save();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            return "Checkpoint guarded; blank scene ready for tests.";
        }
        public static string End()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Wait for test cleanup before restoring.");
            var guard=JsonUtility.FromJson<Guard>(File.ReadAllText(GuardPath));
            var suffixes=new[]{"",".bak",".tmp"};
            for(int i=0;i<suffixes.Length;i++)
            {
                string path=guard.path+suffixes[i];
                if(guard.existed[i])File.WriteAllBytes(path,Convert.FromBase64String(guard.bytes[i]));
                else if(File.Exists(path))File.Delete(path);
            }
            if(guard.hadScene)PlayerPrefs.SetString("Game.SaveScene",guard.saveScene);else PlayerPrefs.DeleteKey("Game.SaveScene");
            if(guard.hadResume)PlayerPrefs.SetInt("Game.ResumeRequested",guard.resume);else PlayerPrefs.DeleteKey("Game.ResumeRequested");
            if(guard.hadCarry)PlayerPrefs.SetInt("Game.StoryCarryRequested",guard.carry);else PlayerPrefs.DeleteKey("Game.StoryCarryRequested");
            PlayerPrefs.Save();
            bool identical=suffixes.Select((s,i)=>File.Exists(guard.path+s)==guard.existed[i]
                &&(!guard.existed[i]||Convert.ToBase64String(File.ReadAllBytes(guard.path+s))==guard.bytes[i])).All(x=>x);
            if(!identical)throw new InvalidOperationException("Checkpoint restoration mismatch.");
            if(!string.IsNullOrEmpty(guard.scene))EditorSceneManager.OpenScene(guard.scene,OpenSceneMode.Single);
            File.Delete(GuardPath);return "Checkpoint and preferences restored; school reopened.";
        }
    }
}
