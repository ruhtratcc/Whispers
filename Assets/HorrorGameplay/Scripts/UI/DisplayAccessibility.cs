// Mantém ajustes de visibilidade e apresentação.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HorrorGame
{
    public sealed class DisplayAccessibility : MonoBehaviour
    {
        VolumeProfile ownedProfile;
        LiftGammaGain grading;
        public Vector4 GammaVector => grading ? grading.gamma.value : new Vector4(1, 1, 1, 0);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() { SceneManager.sceneLoaded -= Install; SceneManager.sceneLoaded += Install; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initial() => Install(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        static void Install(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "MainMenu" && scene.name != "Chapter1Office" && scene.name != "RoadJourney" && scene.name != "SchoolCEEP") return;
            if (FindAnyObjectByType<DisplayAccessibility>()) return;
            var root = new GameObject("Display accessibility (runtime)");
            SceneManager.MoveGameObjectToScene(root, scene); root.AddComponent<DisplayAccessibility>();
        }
        void Awake()
        {
            ownedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            ownedProfile.name = "Player gamma — runtime only";
            grading = ownedProfile.Add<LiftGammaGain>(false);
            var volume = gameObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10000;
            volume.sharedProfile = ownedProfile;
            Apply();
        }
        void Start()
        {
            var camera = Camera.main;
            if (camera && camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) data.volumeLayerMask |= 1;
        }
        void OnEnable() => PresentationSettings.Changed += Apply;
        void OnDisable() => PresentationSettings.Changed -= Apply;
        void Apply()
        {
            if (grading) grading.gamma.Override(new Vector4(1, 1, 1, PresentationSettings.Gamma - 1));
        }
        void OnDestroy() { if (ownedProfile) Destroy(ownedProfile); }
    }
}
