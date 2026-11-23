// Ferramenta de preparação da cena e da apresentação do menu.

using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace SchoolMenu.Editor
{
    public static class MenuHauntingPass
    {
        const string Root = "Assets/HorrorMenu";
        static Material Material(string name, Color color, bool transparent = false)
        {
            string path = Root + "/Art/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.name = name;
            material.SetColor("_BaseColor", color);
            if (transparent)
            {
                material.SetFloat("_Surface", 1);
                material.SetFloat("_ZWrite", 0);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = 3000;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void Strand(Transform parent, string name, Vector3[] points, Material material, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.widthMultiplier = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
        }

        static void Web(Transform parent, string name, Vector3 origin, Vector3 across, Vector3 down, float size, Material material)
        {
            if (parent.Find(name)) return;
            var web = new GameObject(name).transform;
            web.SetParent(parent, false);
            web.localPosition = origin;
            var random = new System.Random(name.Length * 139);
            int spokes = 9;
            var directions = new Vector3[spokes];
            for (int i = 0; i < spokes; i++)
            {
                float angle = i * Mathf.PI * .5f / (spokes - 1) + (float)(random.NextDouble() - .5) * .035f;
                directions[i] = across * Mathf.Cos(angle) + down * Mathf.Sin(angle);
                float length = size * (.82f + (float)random.NextDouble() * .18f);
                Strand(web, "Anchor thread " + i, new[] { Vector3.zero, directions[i] * length }, material, .0038f);
            }
            foreach (float ring in new[] { .16f, .29f, .44f, .61f, .80f })
            {
                var points = new System.Collections.Generic.List<Vector3>();
                for (int i = 0; i < spokes - 1; i++)
                {
                    for (int step = 0; step < 3; step++)
                    {
                        float t = step / 3f;
                        var position = Vector3.Lerp(directions[i], directions[i + 1], t) * ring * size;
                        position *= 1f - .06f * Mathf.Sin(t * Mathf.PI);
                        points.Add(position);
                    }
                }
                points.Add(directions[spokes - 1] * ring * size);
                Strand(web, "Uneven silk ring " + ring, points.ToArray(), material, .0025f);
            }
            Strand(web, "Broken hanging thread", new[] { directions[2] * size * .68f, directions[2] * size * .68f + down * size * .20f, directions[2] * size * .68f + down * size * .37f + across * .03f }, material, .002f);
        }

        static Texture2D WornTexture()
        {
            string path = Root + "/Art/TitleWear.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture) return texture;
            texture = new Texture2D(512, 256, TextureFormat.RGBA32, false);
            texture.name = "Worn ink";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color[512 * 256];
            var random = new System.Random(961);
            for (int i = 0; i < pixels.Length; i++)
            {
                float shade = .88f + (float)random.NextDouble() * .12f;
                pixels[i] = random.NextDouble() < .007 ? Color.clear : new Color(shade, shade, shade, 1);
            }
            // Small diagonal ink losses, rather than a generic distressed-font preset.
            for (int cut = 0; cut < 23; cut++)
            {
                int x = random.Next(512), y = random.Next(256), length = random.Next(8, 37);
                for (int k = 0; k < length; k++)
                {
                    int px = (x + k) % 512, py = (y + k / 4) % 256;
                    pixels[py * 512 + px] = Color.clear;
                    if (cut % 3 == 0) pixels[((py + 1) % 256) * 512 + px] = Color.clear;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        public static string Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != Root + "/Scenes/MainMenu.unity") throw new Exception("Open MainMenu first.");
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            var analog = menu.GetComponent<AnalogSignal>();
            analog.menuCamera = Camera.main;
            var environment = GameObject.Find("School corridor").transform;

            var eyesTransform = environment.Find("Eyes in the corridor");
            if (!eyesTransform)
            {
                eyesTransform = new GameObject("Eyes in the corridor").transform;
                eyesTransform.SetParent(environment, false);
                eyesTransform.localPosition = new Vector3(-.35f, 1.66f, 18.4f);
                var white = Material("DistantEyes", new Color(2.2f, 2.2f, 2.15f));
                foreach (float x in new[] { -.18f, .18f })
                {
                    var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    eye.name = x < 0 ? "Left eye" : "Right eye";
                    eye.transform.SetParent(eyesTransform, false);
                    eye.transform.localPosition = new Vector3(x, 0, 0);
                    eye.transform.localScale = new Vector3(.10f, .05f, .03f);
                    eye.GetComponent<Renderer>().sharedMaterial = white;
                    UnityEngine.Object.DestroyImmediate(eye.GetComponent<Collider>());
                }
            }
            eyesTransform.gameObject.SetActive(false);
            analog.distantEyes = eyesTransform.gameObject;
            var signal = new SerializedObject(analog);
            signal.FindProperty("firstSweepDelay").vector2Value = new Vector2(7, 11);
            signal.FindProperty("disturbanceInterval").vector2Value = new Vector2(11, 18);
            signal.FindProperty("disturbanceDuration").floatValue = 1.65f;
            signal.FindProperty("trackingOpacity").floatValue = .30f;
            signal.ApplyModifiedPropertiesWithoutUndo();
            analog.bandTransform.sizeDelta = new Vector2(3000, 32);
            var band = analog.bandTransform;
            var bright = band.Find("Tracking edge");
            if (!bright)
            {
                var rect = new GameObject("Tracking edge", typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(band, false);
                rect.anchorMin = new Vector2(0, .5f); rect.anchorMax = new Vector2(1, .5f);
                rect.sizeDelta = new Vector2(0, 1.3f);
                rect.anchoredPosition = new Vector2(0, 2.5f);
                var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.color = new Color(.82f,.84f,.80f,.72f);
                image.raycastTarget = false;
            }
            if (!analog.bandStatic)
            {
                var rect = new GameObject("Tracking static", typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(band, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                analog.bandStatic = rect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
                analog.bandStatic.color = new Color(1,1,1,.25f);
                analog.bandStatic.raycastTarget = false;
            }

            band.GetComponent<UnityEngine.UI.RawImage>().color = Color.clear;
            band.Find("Tracking edge").gameObject.SetActive(false);
            var eyeMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/DistantEyes.mat");
            eyeMaterial.SetColor("_BaseColor", new Color(2.2f, 2.2f, 2.15f));
            EditorUtility.SetDirty(eyeMaterial);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .057f;
            RenderSettings.fogColor = new Color(.060f, .070f, .065f);
            var canvas = menu.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;
            analog.scanlines.transform.SetAsLastSibling();
            analog.noiseLayer.transform.SetAsLastSibling();
            band.SetAsLastSibling();
            canvas.transform.Find("Fade curtain").SetAsLastSibling();
            EditorUtility.SetDirty(canvas);

            var title = menu.title;
            string materialPath = Root + "/Art/WhispersWornTitle.mat";
            var titleMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!titleMaterial)
            {
                titleMaterial = new Material(title.fontSharedMaterial);
                titleMaterial.shader = Shader.Find("TextMeshPro/Distance Field");
                titleMaterial.name = "Whispers worn lettering";
                titleMaterial.SetTexture("_FaceTex", WornTexture());
                titleMaterial.SetFloat("_FaceDilate", .055f);
                titleMaterial.SetFloat("_OutlineWidth", 0);
                titleMaterial.SetFloat("_Diffuse", 0);
                titleMaterial.SetFloat("_Ambient", 1);
                titleMaterial.DisableKeyword("GLOW_ON");
                titleMaterial.DisableKeyword("UNDERLAY_ON");
                titleMaterial.DisableKeyword("BEVEL_ON");
                AssetDatabase.CreateAsset(titleMaterial, materialPath);
            }
            title.fontSharedMaterial = titleMaterial;
            title.text = "<size=82>WHISPERS</size>\n<size=118>OF PAST</size>";
            title.rectTransform.sizeDelta = new Vector2(690, 300);
            title.horizontalMapping = TextureMappingOptions.Line;
            title.verticalMapping = TextureMappingOptions.Line;
            title.mappingUvLineOffset = .37f;
            title.characterSpacing = -1.5f;
            title.color = new Color(.83f,.12f,.09f);
            var haunted = title.GetComponent<HauntedTitle>();
            if (!haunted) haunted = title.gameObject.AddComponent<HauntedTitle>();
            haunted.title = title;
            title.ForceMeshUpdate();

            var silk = Material("OldSpiderSilk", new Color(.64f,.67f,.60f,.40f), true);
            Web(environment, "Web at the fluorescent", new Vector3(2.67f,3.38f,1.15f), Vector3.left, Vector3.down, 1.32f, silk);
            Web(environment, "Web over the notice board", new Vector3(-2.66f,3.36f,3.1f), Vector3.right, Vector3.down, 1.02f, silk);
            Web(environment, "Web behind the lockers", new Vector3(2.085f,2.19f,4.7f), Vector3.forward, Vector3.down, .7f, silk);

            var dark = AssetDatabase.LoadAssetAtPath<Material>(Root+"/Art/DarkMetal.mat");
            var plaster = AssetDatabase.LoadAssetAtPath<Material>(Root+"/Art/Plaster.mat");
            if (!environment.Find("Loose ceiling tile"))
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "Loose ceiling tile"; tile.transform.SetParent(environment,false);
                tile.transform.localPosition = new Vector3(1.0f,3.37f,7.6f);
                tile.transform.localScale = new Vector3(.75f,.055f,.65f);
                tile.transform.localRotation = Quaternion.Euler(-9,0,5);
                tile.GetComponent<Renderer>().sharedMaterial=plaster;
                UnityEngine.Object.DestroyImmediate(tile.GetComponent<Collider>());
            }
            if (!environment.Find("Damp at the skirting"))
            {
                var stainRoot=new GameObject("Damp at the skirting").transform;stainRoot.SetParent(environment,false);
                for(int i=0;i<7;i++)
                {
                    var stain=GameObject.CreatePrimitive(PrimitiveType.Cube);stain.name="Old water stain "+i;stain.transform.SetParent(stainRoot,false);
                    stain.transform.localPosition=new Vector3(-2.718f,.17f+i*.055f,.4f+i*.16f);
                    stain.transform.localScale=new Vector3(.008f,.10f+i*.07f,.32f+i*.025f);
                    stain.GetComponent<Renderer>().sharedMaterial=dark;
                    UnityEngine.Object.DestroyImmediate(stain.GetComponent<Collider>());
                }
            }
            EditorUtility.SetDirty(analog);
            EditorUtility.SetDirty(title);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "Rolling tracking line, synchronized eyes, worn title and abandoned-school details saved.";
        }
    }
}

