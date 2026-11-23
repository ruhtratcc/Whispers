// Ferramenta de preparação da cena e da apresentação do menu.

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;

namespace SchoolMenu.Editor
{
    public static class WindDoorPass
    {
        public static string Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/HorrorMenu/Scenes/MainMenu.unity")
                throw new System.InvalidOperationException("Open MainMenu outside Play Mode.");
            var corridor = GameObject.Find("School corridor").transform;
            if (corridor.Find("Left classroom hinge")) return "Left classroom door already configured.";
            var door = corridor.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "Classroom door").OrderBy(t => Mathf.Abs(t.position.z - 10)).First();
            var hinge = new GameObject("Left classroom hinge").transform;
            hinge.SetParent(corridor, false);
            Vector3 pivot = door.position + door.rotation * new Vector3(0,0,door.localScale.z * .5f);
            pivot.y = 0;
            hinge.position = pivot;
            hinge.rotation = door.rotation;
            door.SetParent(hinge,true);
            var wind = hinge.gameObject.AddComponent<WindDoor>();
            var serialized = new SerializedObject(wind);
            serialized.FindProperty("restingAngle").floatValue = -34;
            serialized.FindProperty("swingAngle").floatValue = 6;
            serialized.FindProperty("windSpeed").floatValue = .42f;
            serialized.FindProperty("gustAngle").floatValue = 1.2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var spill = corridor.Find("Classroom light spill").GetComponent<Light>();
            spill.transform.localPosition = new Vector3(-2.08f,.65f,10.02f);
            spill.color = new Color(1f,.72f,.40f);
            spill.intensity = .65f;
            spill.range = 3.5f;
            var inside = new GameObject("Left classroom interior light");
            inside.transform.SetParent(corridor, false);
            inside.transform.localPosition = new Vector3(-2.48f,1.35f,10.08f);
            inside.transform.rotation = Quaternion.LookRotation(new Vector3(1,-.38f,-.28f));
            var light = inside.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f,.72f,.40f);
            light.intensity = 1.65f;
            light.range = 4.8f;
            light.spotAngle = 75;
            light.innerSpotAngle = 35;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = .85f;
            var recess = corridor.Find("Open classroom recess").GetComponent<Renderer>();
            string path = "Assets/HorrorMenu/Art/WeakClassroomGlow.mat";
            var glow = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!glow)
            {
                var plaster = AssetDatabase.LoadAssetAtPath<Material>("Assets/HorrorMenu/Art/FadedPaint.mat");
                glow = new Material(plaster);
                glow.name = "Weak classroom glow";
                AssetDatabase.CreateAsset(glow,path);
            }
            glow.SetColor("_BaseColor",new Color(.40f,.29f,.15f));
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor",new Color(.16f,.10f,.038f));
            recess.sharedMaterial = glow;
            EditorUtility.SetDirty(wind);
            EditorUtility.SetDirty(spill);
            EditorUtility.SetDirty(glow);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "Left classroom door moves on its hinge, with weak warm light from inside.";
        }
    }
}
