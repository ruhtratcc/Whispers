// Ferramenta de preparação da cena e da apresentação do menu.

using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

namespace SchoolMenu.Editor
{
    public static class MenuArtPass
    {
        const string Root = "Assets/HorrorMenu";
        static readonly Color Bone = new Color(.84f, .82f, .75f);
        static readonly Color Red = new Color(.62f, .12f, .095f);

        static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material material)
        {
            var existing = parent.Find(name);
            if (existing) return existing.gameObject;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static UnityEngine.UI.RawImage Overlay(string name, Transform parent)
        {
            var child = parent.Find(name);
            if (child) return child.GetComponent<UnityEngine.UI.RawImage>();
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.raycastTarget = false;
            return image;
        }

        static T Effect<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var effect)) effect = profile.Add<T>(true);
            if (!AssetDatabase.Contains(effect)) AssetDatabase.AddObjectToAsset(effect, profile);
            EditorUtility.SetDirty(effect);
            return effect;
        }

        public static string Apply()
        {
            if (SceneManagerPath() != Root + "/Scenes/MainMenu.unity")
                throw new Exception("Open MainMenu before applying this art pass.");
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            var canvas = menu.GetComponent<Canvas>();
            canvas.GetComponent<UnityEngine.UI.CanvasScaler>().matchWidthOrHeight = 1f;
            var font = menu.title.font;
            foreach (var text in menu.GetComponentsInChildren<TMP_Text>(true))
            {
                text.characterSpacing = 0;
                text.fontStyle = FontStyles.Normal;
                text.color = Bone;
            }
            var title = menu.title;
            title.richText = true;
            title.text = "<size=78>WHISPERS</size>\n<size=116>OF PAST</size>";
            title.fontSize = 92;
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = -2.5f;
            title.lineSpacing = -6;
            title.color = Red;
            Place(title.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(103, 5), new Vector2(690, 280));
            menu.transform.Find("Location").gameObject.SetActive(false);
            Place((RectTransform)menu.mainPanel.transform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-103, -30), new Vector2(330, 240));
            menu.continueButton.GetComponentInChildren<TMP_Text>(true).text = "Continuar";
            menu.newGameButton.GetComponentInChildren<TMP_Text>(true).text = "Novo jogo";
            menu.settingsButton.GetComponentInChildren<TMP_Text>(true).text = "Opções";
            menu.quitButton.GetComponentInChildren<TMP_Text>(true).text = "Sair";
            foreach (var button in new[] { menu.continueButton, menu.newGameButton, menu.settingsButton, menu.quitButton })
            {
                var text = button.GetComponentInChildren<TMP_Text>(true);
                text.fontSize = 34;
                text.fontStyle = FontStyles.Bold;
                button.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 54;
            }
            foreach (var item in menu.GetComponentsInChildren<MenuItemFX>(true))
            {
                var serializedItem = new SerializedObject(item);
                serializedItem.FindProperty("normal").colorValue = new Color(.87f,.85f,.80f);
                serializedItem.FindProperty("dim").floatValue = .92f;
                serializedItem.ApplyModifiedPropertiesWithoutUndo();
            }
            menu.mainPanel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing = 13;
            Place((RectTransform)menu.settingsPanel.transform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-95, -35), new Vector2(610, 449));
            if (!menu.settingsHeading)
            {
                var rect = new GameObject("Settings heading", typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(menu.transform, false);
                menu.settingsHeading = rect.gameObject.AddComponent<TextMeshProUGUI>();
            }
            var heading = menu.settingsHeading;
            heading.font = font; heading.fontSize = 44; heading.fontStyle = FontStyles.Bold; heading.text = "Opções"; heading.color = Bone; heading.raycastTarget = false;
            Place(heading.rectTransform, new Vector2(1,.5f), new Vector2(1,.5f), new Vector2(-95,245), new Vector2(610,65));
            heading.gameObject.SetActive(false);
            string[] oldNames = { "MASTER", "MUSIC", "SFX", "MOUSE SENSITIVITY", "RESOLUTION", "FULLSCREEN", "QUALITY", "INVERT Y" };
            string[] captions = { "Volume geral", "Música", "Efeitos", "Sensibilidade", "Resolução", "Tela cheia", "Qualidade", "Inverter Y" };
            for (int i=0; i<oldNames.Length; i++)
            {
                var row = menu.settingsPanel.transform.Find(oldNames[i]);
                if (!row) continue;
                var label = row.Find("Label").GetComponent<TMP_Text>();
                label.text = captions[i]; label.fontSize = 20;
                row.Find("Value").GetComponent<TMP_Text>().fontSize = 19;
            }
            menu.backButton.GetComponentInChildren<TMP_Text>(true).text = "Voltar";
            menu.backButton.GetComponentInChildren<TMP_Text>(true).fontSize = 23;
            var footer = menu.transform.Find("Navigation hint").GetComponent<TMP_Text>();
            footer.text = "SETAS  NAVEGAR     ENTER  CONFIRMAR     ESC  VOLTAR";
            footer.fontSize = 12; footer.color = new Color(.44f,.43f,.38f);
            Place(menu.status.rectTransform, new Vector2(0,0), new Vector2(0,0), new Vector2(103,80), new Vector2(900,40));
            menu.status.fontSize = 18;

            var volume = GameObject.Find("Menu film").GetComponent<Volume>();
            var profile = volume.sharedProfile;
            var vignette = Effect<Vignette>(profile); vignette.intensity.Override(.48f); vignette.smoothness.Override(.62f);
            var grain = Effect<FilmGrain>(profile); grain.type.Override(FilmGrainLookup.Thin2); grain.intensity.Override(.16f); grain.response.Override(.85f);
            var chromatic = Effect<ChromaticAberration>(profile); chromatic.intensity.Override(.09f);
            var color = Effect<ColorAdjustments>(profile); color.postExposure.Override(-.15f); color.contrast.Override(23); color.saturation.Override(-42);
            var depth = Effect<DepthOfField>(profile);
            depth.mode.Override(DepthOfFieldMode.Gaussian); depth.gaussianStart.Override(13); depth.gaussianEnd.Override(28); depth.gaussianMaxRadius.Override(.65f);
            var lens = Effect<LensDistortion>(profile);
            lens.intensity.Override(0);
            EditorUtility.SetDirty(profile);
            var cameraData = Camera.main.GetComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            RenderSettings.ambientLight = new Color(.04f,.045f,.04f);
            RenderSettings.fogDensity = .042f;
            RenderSettings.fogColor = new Color(.009f,.012f,.011f);
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light.name == "Failing fluorescent") { light.color = new Color(.76f,.79f,.72f); light.intensity = 3.7f; }
                if (light.name == "Distant spill") { light.intensity = .75f; light.range = 6f; light.color = new Color(.57f,.63f,.61f); }
                if (light.name == "Near bounce") light.intensity = .08f;
            }
            var atmosphere = Camera.main.GetComponent<MenuAtmosphere>();
            var serialized = new SerializedObject(atmosphere);
            serialized.FindProperty("baseIntensity").floatValue = 3.7f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var corridor = GameObject.Find("School corridor").transform;
            var dark = AssetDatabase.LoadAssetAtPath<Material>(Root+"/Art/DarkMetal.mat");
            var wood = AssetDatabase.LoadAssetAtPath<Material>(Root+"/Art/OldWood.mat");
            var paper = AssetDatabase.LoadAssetAtPath<Material>(Root+"/Art/OldPaper.mat");
            var door = corridor.Cast<Transform>().Where(t => t.name=="Classroom door").OrderBy(t=>Mathf.Abs(t.localPosition.z-10)).First();
            Box("Open classroom recess",corridor,new Vector3(-2.535f,1.17f,10),new Vector3(.012f,2.34f,1.12f),dark);
            door.localPosition = new Vector3(-2.245f,1.17f,9.88f);
            door.localRotation = Quaternion.Euler(0,-34,0);
            var glass = door.Find("Door glass") ?? corridor.Cast<Transform>().Where(t=>t.name=="Door glass").OrderBy(t=>Mathf.Abs(t.localPosition.z-10)).First();
            var handle = door.Find("Door handle") ?? corridor.Cast<Transform>().Where(t=>t.name=="Door handle").OrderBy(t=>Mathf.Abs(t.localPosition.z-9.6f)).First();
            glass.SetParent(door,true);handle.SetParent(door,true);
            // Place the fittings in the open door's local coordinates.
            glass.localPosition = new Vector3(.56f,.205f,0);glass.localRotation=Quaternion.identity;glass.localScale=new Vector3(.12f,.277f,.557f);
            handle.localPosition = new Vector3(1.2f,-.073f,-.35f);handle.localRotation=Quaternion.identity;handle.localScale=new Vector3(1.1f,.015f,.15f);
            if (!corridor.Find("Classroom light spill"))
            {
                var go=new GameObject("Classroom light spill");go.transform.SetParent(corridor);go.transform.position=new Vector3(-2.08f,.6f,10.05f);
                var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(.67f,.53f,.34f);light.intensity=.32f;light.range=2.8f;
            }
            for(int i=0;i<6;i++)
            {
                var cable=Box("Loose cable "+i,corridor,new Vector3(.62f+i*.035f,3.05f-i*.16f,8.2f+Mathf.Sin(i*.7f)*.08f),new Vector3(.015f,.2f,.018f),dark);
                cable.transform.localRotation=Quaternion.Euler(0,0,-8+i*4);
            }
            Box("Abandoned chair seat",corridor,new Vector3(.7f,.46f,18),new Vector3(.44f,.07f,.42f),wood);
            var back=Box("Abandoned chair back",corridor,new Vector3(.7f,.76f,18.2f),new Vector3(.44f,.55f,.04f),wood);
            back.transform.localRotation=Quaternion.Euler(-6,0,0);
            foreach(float x in new[]{.53f,.87f})foreach(float z in new[]{17.84f,18.17f})
                Box("Chair leg "+x+" "+z,corridor,new Vector3(x,.21f,z),new Vector3(.025f,.42f,.025f),dark);
            // Remove repetition from the nearby locker bank with one ajar door.
            var locker = corridor.Find("Locker 4");
            if(locker) {locker.localRotation=Quaternion.Euler(0,0,-1.4f);locker.localPosition=new Vector3(2.345f,1.13f,3.64f);}
            var scanlines=menu.transform.Find("Faint scanlines").GetComponent<UnityEngine.UI.RawImage>();
            var scanTexture = scanlines.texture as Texture2D;
            if(scanTexture) {var pixels=new Color[8];for(int i=0;i<8;i++)pixels[i]=i<2?new Color(0,0,0,1):Color.clear;scanTexture.SetPixels(pixels);scanTexture.Apply();EditorUtility.SetDirty(scanTexture);}
            var noise=Overlay("Analog noise",menu.transform);noise.color=new Color(1,1,1,.008f);scanlines.color=new Color(1,1,1,.05f);
            noise.transform.SetSiblingIndex(scanlines.transform.GetSiblingIndex()+1);
            var band=Overlay("Tracking band",menu.transform);
            var bandRect=(RectTransform)band.transform;
            Place(bandRect,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(3000,14));
            band.texture=Texture2D.whiteTexture;band.color=Color.black;
            var bandGroup=band.GetComponent<CanvasGroup>();if(!bandGroup)bandGroup=band.gameObject.AddComponent<CanvasGroup>();bandGroup.alpha=0;bandGroup.blocksRaycasts=false;
            var analog=menu.GetComponent<AnalogSignal>();if(!analog)analog=menu.gameObject.AddComponent<AnalogSignal>();
            analog.noiseLayer=noise;analog.scanlines=scanlines;analog.trackingBand=bandGroup;analog.bandTransform=bandRect;analog.filmVolume=volume;
            menu.fader.curtain.transform.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            EditorSceneManager.SaveScene(menu.gameObject.scene);
            AssetDatabase.SaveAssets();
            return "Whispers of Past art pass applied.";
        }
        static string SceneManagerPath() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
    }
}
