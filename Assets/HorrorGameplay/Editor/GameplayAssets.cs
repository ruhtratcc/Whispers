// Ferramenta de preparação ou verificação usada no Editor.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HorrorGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorGame.Editor
{
    public static class GameplayAssets
    {
        public const string Root = "Assets/HorrorGameplay/";
        public static void Folders()
        {
            foreach (var folder in new[]
            {
                "Scenes",
                "Prefabs",
                "Audio/Placeholder",
                "Data",
                "Materials",
                "Input",
                "Editor",
                "Tests/EditMode",
                "Tests/PlayMode",
                "Documentation"
            }

            )
                Directory.CreateDirectory(Root + folder);
            AssetDatabase.Refresh();
        }

        public static SceneCatalog Scenes()
        {
            var path = Root + "Data/SceneCatalog.asset";
            var asset = AssetDatabase.LoadAssetAtPath<SceneCatalog>(path);
            if (!asset)
            {
                asset = ScriptableObject.CreateInstance<SceneCatalog>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        static InputAction Button(InputActionMap map, string name, params string[] bindings)
        {
            var action = map.AddAction(name, InputActionType.Button);
            foreach (var binding in bindings)
                action.AddBinding(binding);
            return action;
        }

        public static InputActionAsset Input()
        {
            var path = Root + "Input/HorrorInput.inputactions";
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (asset)
                return asset;
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "HorrorInput";
            var game = asset.AddActionMap("Gameplay");
            var move = game.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");
            var look = game.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            look.AddBinding("<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick").WithProcessor("stickDeadzone");
            Button(game, "Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            Button(game, "Crouch", "<Keyboard>/leftCtrl", "<Keyboard>/c", "<Gamepad>/rightStickPress");
            Button(game, "Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Button(game, "Interact", "<Keyboard>/e", "<Gamepad>/buttonWest");
            Button(game, "PrimaryInteract", "<Mouse>/leftButton");
            Button(game, "Throw", "<Mouse>/rightButton", "<Gamepad>/rightTrigger");
            Button(game, "Drop", "<Keyboard>/g", "<Gamepad>/buttonEast");
            Button(game, "Flashlight", "<Keyboard>/f", "<Gamepad>/leftShoulder");
            Button(game, "Inventory", "<Keyboard>/tab", "<Keyboard>/i", "<Gamepad>/buttonNorth");
            Button(game, "HoldBreath", "<Keyboard>/leftAlt", "<Gamepad>/leftTrigger");
            var always = asset.AddActionMap("Always");
            Button(always, "Pause", "<Keyboard>/escape", "<Gamepad>/start");
            var ui = asset.AddActionMap("UI");
            var navigate = ui.AddAction("Navigate", InputActionType.Value, expectedControlLayout: "Vector2");
            navigate.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            navigate.AddBinding("<Gamepad>/leftStick");
            navigate.AddBinding("<Gamepad>/dpad");
            Button(ui, "Submit", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Gamepad>/buttonSouth");
            Button(ui, "Cancel", "<Keyboard>/escape", "<Gamepad>/buttonEast");
            ui.AddAction("Point", InputActionType.PassThrough, expectedControlLayout: "Vector2").AddBinding("<Mouse>/position");
            ui.AddAction("Click", InputActionType.PassThrough, expectedControlLayout: "Button").AddBinding("<Mouse>/leftButton");
            ui.AddAction("ScrollWheel", InputActionType.PassThrough, expectedControlLayout: "Vector2").AddBinding("<Mouse>/scroll");
            asset.AddControlScheme("KeyboardMouse").WithRequiredDevice("<Keyboard>").WithRequiredDevice("<Mouse>");
            asset.AddControlScheme("Gamepad").WithRequiredDevice("<Gamepad>");
            File.WriteAllText(path, asset.ToJson());
            UnityEngine.Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }

        public static int Layer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0)
                return existing;
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = settings.FindProperty("layers");
            for (int i = 8; i < 32; i++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {
                    layers.GetArrayElementAtIndex(i).stringValue = name;
                    settings.ApplyModifiedProperties();
                    return i;
                }

            throw new InvalidOperationException("No free user layer for " + name);
        }

        public static Material Material(string name, Color color, float metallic = 0, float smoothness = .2f)
        {
            var path = Root + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Type TypeNamed(string name) => Type.GetType(name + ", UnityEditor.AudioModule", true);
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        public static AudioMixer Mixer()
        {
            string path = Root + "Audio/Gameplay.mixer";
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
            if (existing)
                return existing;
            var type = TypeNamed("UnityEditor.Audio.AudioMixerController");
            var mixer = type.GetMethod("CreateMixerControllerAtPath", Flags).Invoke(null, new object[] { path });
            var groupType = TypeNamed("UnityEditor.Audio.AudioMixerGroupController");
            var exposedType = TypeNamed("UnityEditor.Audio.ExposedAudioParameter");
            var groups = Array.CreateInstance(groupType, 4);
            var parameters = Array.CreateInstance(exposedType, 4);
            var names = new[]
            {
                "Music",
                "SFX",
                "Ambience",
                "Voice"
            };
            for (int i = 0; i < names.Length; i++)
            {
                var group = type.GetMethod("CreateNewGroup", Flags).Invoke(mixer, new object[] { names[i], true });
                groups.SetValue(group, i);
                var parameter = Activator.CreateInstance(exposedType);
                exposedType.GetField("guid", Flags).SetValue(parameter, groupType.GetMethod("GetGUIDForVolume", Flags).Invoke(group, null));
                exposedType.GetField("name", Flags).SetValue(parameter, names[i] + "Volume");
                parameters.SetValue(parameter, i);
                EditorUtility.SetDirty((UnityEngine.Object)group);
            }

            var master = type.GetProperty("masterGroup", Flags).GetValue(mixer);
            groupType.GetProperty("children", Flags).SetValue(master, groups);
            type.GetProperty("exposedParameters", Flags).SetValue(mixer, parameters);
            type.GetMethod("OnChangedExposedParameter", Flags)?.Invoke(mixer, null);
            EditorUtility.SetDirty((UnityEngine.Object)mixer);
            EditorUtility.SetDirty((UnityEngine.Object)master);
            AssetDatabase.SaveAssets();
            return (AudioMixer)mixer;
        }

        public static SoundSet Sound(string name, float duration, float frequency, int mode, int variants = 2)
        {
            var path = Root + "Audio/" + name + ".asset";
            var set = AssetDatabase.LoadAssetAtPath<SoundSet>(path);
            if (set)
                return set;
            set = ScriptableObject.CreateInstance<SoundSet>();
            set.clips = new AudioClip[variants];
            for (int variant = 0; variant < variants; variant++)
            {
                string clipPath = Root + "Audio/Placeholder/" + name + "-" + variant + ".wav";
                int sampleRate = 22050, length = Mathf.RoundToInt(duration * sampleRate);
                var random = new System.Random(name.GetHashCode() + variant * 173);
                using (var stream = File.Create(clipPath))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                    writer.Write(36 + length * 2);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16);
                    writer.Write((short)1);
                    writer.Write((short)1);
                    writer.Write(sampleRate);
                    writer.Write(sampleRate * 2);
                    writer.Write((short)2);
                    writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                    writer.Write(length * 2);
                    float filtered = 0;
                    for (int n = 0; n < length; n++)
                    {
                        float t = n / (float)sampleRate, noise = (float)(random.NextDouble() * 2 - 1);
                        filtered = Mathf.Lerp(filtered, noise, mode == 2 ? .07f : .3f);
                        float envelope = mode >= 3 ? Mathf.Sin(Mathf.PI * n / length) * .35f : Mathf.Exp(-t * (mode == 1 ? 5 : 17));
                        float tone = Mathf.Sin(2 * Mathf.PI * (frequency + variant * 17) * t + Mathf.Sin(t * 31) * .4f);
                        float signal = mode == 0 ? (filtered * .65f + tone * .25f) : mode == 1 ? (tone * .5f + filtered * .2f) : mode == 2 ? filtered * .75f : mode == 3 ? filtered * .45f : mode == 4 ? (tone * .3f + Mathf.Sin(t * 2 * Mathf.PI * frequency * .503f) * .13f) : filtered * Mathf.Pow(Mathf.Sin(t * 2.7f), 2);
                        writer.Write((short)(Mathf.Clamp(signal * envelope, -1, 1) * 20000));
                    }
                }

                AssetDatabase.ImportAsset(clipPath);
                var importer = (AudioImporter)AssetImporter.GetAtPath(clipPath);
                var settings = importer.defaultSampleSettings;
                settings.loadType = duration > 2 ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = .8f;
                importer.forceToMono = true;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                set.clips[variant] = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            }

            AssetDatabase.CreateAsset(set, path);
            return set;
        }

        public static FootstepLibrary Steps()
        {
            string path = Root + "Audio/FootstepLibrary.asset";
            var library = AssetDatabase.LoadAssetAtPath<FootstepLibrary>(path);
            if (library)
                return library;
            library = ScriptableObject.CreateInstance<FootstepLibrary>();
            library.entries = new FootstepLibrary.Entry[6];
            float[] frequencies =
            {
                130,
                70,
                50,
                40,
                850,
                230
            };
            for (int i = 0; i < 6; i++)
            {
                var surface = (GroundSurface)i;
                library.entries[i] = new FootstepLibrary.Entry
                {
                    surface = surface,
                    steps = Sound("Step-" + surface, .22f, frequencies[i], i == 4 ? 1 : i >= 2 && i <= 3 ? 2 : 0),
                    landing = Sound("Land-" + surface, .35f, frequencies[i] * .7f, 0)
                };
            }

            AssetDatabase.CreateAsset(library, path);
            return library;
        }

        public static VolumeProfile Profile()
        {
            string path = Root + "Data/GameplayVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile)
                return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = .28f;
            vignette.smoothness.value = .48f;
            var grain = profile.Add<FilmGrain>(true);
            grain.type.value = FilmGrainLookup.Thin1;
            grain.intensity.value = 0;
            grain.response.value = .8f;
            var chromatic = profile.Add<ChromaticAberration>(true);
            chromatic.intensity.value = .025f;
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.value = .15f;
            color.saturation.value = -25;
            color.contrast.value = 12;
            color.colorFilter.value = new Color(.88f, .94f, 1);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.value = .12f;
            bloom.threshold.value = 1.1f;
            bloom.scatter.value = .45f;
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.value = TonemappingMode.ACES;
            foreach (var component in profile.components)
                AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        [MenuItem("Tools/Whispers/Assign Unique Save IDs")]
        public static void AssignIds()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var obj in UnityEngine.Object.FindObjectsByType<PersistentObject>(FindObjectsInactive.Include))
            {
                if (string.IsNullOrWhiteSpace(obj.persistentId) || !seen.Add(obj.persistentId))
                {
                    Undo.RecordObject(obj, "Assign save ID");
                    obj.persistentId = Guid.NewGuid().ToString("N");
                    seen.Add(obj.persistentId);
                    EditorUtility.SetDirty(obj);
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }
}

