// Controla feixe, bateria e aparência da lanterna na mão.

using UnityEngine;

namespace HorrorGame
{
    [DefaultExecutionOrder(120)]
    public sealed class FlashlightController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        public InputReader input;
        [HideInInspector] public EquipmentController equipment;
        [SerializeField]
        public Camera view;
        [SerializeField]
        public Light lamp;
        [SerializeField]
        public Transform handRig;
        [SerializeField]
        public SoundSet click;
        [Tooltip("Optional direction-specific switch sounds. Unassigned fields fall back to Click.")]
        [SerializeField]
        public SoundSet clickOn, clickOff;
        [Header("Battery")]
        [SerializeField]
        public float capacitySeconds = 240;
        [SerializeField, Range(0, 1)]
        float lowBattery = .18f;
        [SerializeField]
        public bool startsOn = true;
        [Header("Hand lag")]
        [SerializeField]
        Vector3 handOffset = new Vector3(.16f, -.16f, .25f);
        [SerializeField]
        float positionFollow = 22, rotationFollow = 13, breathingSway = .007f;
        [Header("Near surface exposure")]
        [Tooltip("School-only close inspection: reduce glare on nearby surfaces without shortening the distant beam.")]
        public bool nearSurfaceDimming;
        [SerializeField, Min(1)] float exposureDistance = 5.8f;
        [SerializeField, Range(.01f, 1)] float minimumExposure = .045f;
        float surfaceExposure = 1;
        float battery, baseIntensity;
        bool on;
        bool detachedLight;
        bool PresentationLight => equipment && equipment.PresentationLight;
        FirstPersonMotor motor;
        public bool IsAvailable => isActiveAndEnabled && gameObject.scene.name != "Chapter1Office";
        public bool RequestedOn => on;
        // A luz depende da cena, do equipamento e da bateria. Uma cena guiada também pode acendê-la.
        public bool IsOn => IsAvailable && (PresentationLight || on && battery > 0 && (!equipment || equipment.LightAllowed));
        public float BatterySeconds => battery;
        public float Battery01 => battery / Mathf.Max(1, capacitySeconds);

        void Awake()
        {
            battery = capacitySeconds;
            on = startsOn;
            motor = GetComponent<FirstPersonMotor>();
            if (lamp)
            {
                baseIntensity = lamp.intensity;
            }
            if (gameObject.scene.name == "Chapter1Office")
            {
                on = false;
                if (handRig) handRig.gameObject.SetActive(false);
                if (lamp) lamp.enabled = false;
            }
        }

        void Start()
        {
            if (view && handRig)
            {
                handRig.SetPositionAndRotation(view.transform.TransformPoint(handOffset), view.transform.rotation);
            }

            Apply();
        }

        void Update()
        {
            if (GameSession.Instance && (GameSession.Instance.State == GameState.Paused || GameSession.Instance.State == GameState.GameOver))
            {
                Apply();
                return;
            }

            if (!equipment && input && input.Flashlight && (!GameSession.Instance || GameSession.Instance.CanControl))
                Toggle();
            var story = GameSession.Instance ? GameSession.Instance.narrative : null;
            bool finalChapters = gameObject.scene.name == "SchoolCEEP" && story && story.IsInitialized
                && BeforeDemolitionCampaign.Is(story.book)
                && (story.Progress.chapterId == BeforeDemolitionTwoVoicesFlow.ChapterId
                    || story.Progress.chapterId == BeforeDemolitionMirrorFlow.ChapterId);
            // Nos capítulos finais da escola, garante pelo menos um quarto da bateria.
            if (finalChapters)
                battery = Mathf.Max(battery, capacitySeconds * .25f);
            // Nos outros capítulos, a bateria só gasta tempo enquanto o feixe está aceso.
            else if (IsOn && !PresentationLight)
                battery = Mathf.Max(0, battery - Time.deltaTime);
            Apply();
        }

        public void Toggle()
        {
            if (!IsAvailable) return;
            if (battery <= 0)
            {
                on = false;
                return;
            }

            on = !on;
            var switchSound = on ? clickOn : clickOff;
            AudioManager.Instance?.Play(switchSound ? switchSound : click, view ? view.transform.position : transform.position, .35f, AudioChannel.SFX, false);
            Apply();
        }

        void Apply()
        {
            if (!lamp)
                return;
            lamp.enabled = IsOn && !PresentationLight;
            float level = 1;
            if (!PresentationLight && Battery01 < lowBattery)
            {
                // Com pouca carga, calcula o brilho a partir da bateria restante.
                float charge = Battery01 / Mathf.Max(.01f, lowBattery);
                float flicker = Mathf.PerlinNoise(Time.time * 19, 4.2f);
                level = Mathf.Lerp(.15f, 1, charge) * (flicker > .3f ? 1 : .3f);
            }

            lamp.intensity = baseIntensity * level * surfaceExposure;
            if(presentationBeam){presentationBeam.enabled=PresentationLight&&IsAvailable;presentationBeam.intensity=lamp.intensity;presentationBeam.range=lamp.range;}
        }

        void LateUpdate()
        {
            KeepPresentationBeam(PresentationLight);
            if (!IsAvailable || !view || !handRig || Time.timeScale <= 0 || Time.deltaTime <= 0)
                return;
            // Ao correr, abaixa a lanterna para ela ficar fora do enquadramento.
            Vector3 tuck = motor && motor.IsSprinting ? new Vector3(.015f, -.065f, -.12f) : Vector3.zero;
            Vector3 desired = view.transform.TransformPoint(handOffset + tuck + (equipment ? equipment.FlashlightOffset : Vector3.zero) + Vector3.up * Mathf.Sin(Time.time * 1.1f) * breathingSway);
            handRig.position = Vector3.Lerp(handRig.position, desired, 1 - Mathf.Exp(-positionFollow * Time.deltaTime));
            handRig.rotation = Quaternion.Slerp(handRig.rotation, view.transform.rotation, 1 - Mathf.Exp(-rotationFollow * Time.deltaTime));
            if (nearSurfaceDimming && lamp)
            {
                // O raio ignora o corpo de João e as áreas que só disparam eventos.
                // A carga da bateria e o efeito de sanidade ainda influenciam o brilho.
                int mask = ~LayerMask.GetMask("Player", "Ignore Raycast");
                float target = 1;
                if (Physics.Raycast(lamp.transform.position, lamp.transform.forward, out var hit,
                    exposureDistance, mask, QueryTriggerInteraction.Ignore))
                    target = Mathf.Clamp(hit.distance * hit.distance / (exposureDistance * exposureDistance), minimumExposure, 1);
                surfaceExposure = Mathf.Lerp(surfaceExposure, target,
                    1 - Mathf.Exp(-(target < surfaceExposure ? 30 : 8) * Time.deltaTime));
                Apply();
            }
        }

        void KeepPresentationBeam(bool keep)
        {
            if (!lamp || !view || keep==detachedLight) return;
            // Em cenas guiadas, usa uma cópia só da luz, pois a mão da lanterna fica escondida.
            if (keep)
            {
                var source=lamp;
                var beam=new GameObject("Lanterna — luz da apresentação").AddComponent<Light>();
                beam.enabled=false;beam.type=source.type;beam.spotAngle=source.spotAngle;beam.innerSpotAngle=source.innerSpotAngle;
                beam.range=source.range;beam.color=source.color;beam.intensity=source.intensity;beam.shadows=source.shadows;
                beam.shadowBias=source.shadowBias;beam.shadowNormalBias=source.shadowNormalBias;beam.shadowNearPlane=source.shadowNearPlane;
                beam.cullingMask=source.cullingMask;beam.renderingLayerMask=source.renderingLayerMask;beam.cookie=source.cookie;
                beam.transform.SetParent(view.transform,false);beam.transform.localPosition=new Vector3(.04f,-.035f,.10f);
                presentationBeam=beam;detachedLight=true;
            }
            else {if(presentationBeam)Destroy(presentationBeam.gameObject);presentationBeam=null;detachedLight=false;}
            Apply();
        }
        Light presentationBeam;
        void OnDisable(){if(presentationBeam)Destroy(presentationBeam.gameObject);presentationBeam=null;detachedLight=false;if(lamp)lamp.enabled=false;}

        public void Recharge(float seconds)
        {
            battery = Mathf.Clamp(battery + Mathf.Max(0, seconds), 0, capacitySeconds);
            Apply();
        }

        public void Restore(float seconds, bool wasOn)
        {
            battery = Mathf.Clamp(seconds, 0, capacitySeconds);
            on = wasOn;
            Apply();
        }
    }
}
