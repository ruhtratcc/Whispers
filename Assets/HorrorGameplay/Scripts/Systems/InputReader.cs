// Lê teclado, mouse e controle e entrega os comandos aos sistemas.

using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorGame
{
    [DefaultExecutionOrder(-200)]
    public sealed class InputReader : MonoBehaviour
    {
        [SerializeField]
        public InputActionAsset asset;
        public InputActionAsset Actions { get; private set; }
        public Vector2 Move => Read<Vector2>("Move");
        public Vector2 Look => Read<Vector2>("Look");
        public bool Sprint => Held("Sprint");
        public bool Crouch => Held("Crouch");
        public bool Breath => Held("HoldBreath");
        public bool Focus => Held("Focus");
        public bool Jump => Pressed("Jump");
        public bool Interact => Pressed("Interact");
        public bool Primary => Pressed("PrimaryInteract");
        public bool Throw => Pressed("Throw");
        public bool Drop => Pressed("Drop");
        public bool Flashlight => Pressed("Flashlight");
        public bool WhistleEquip => Pressed("WhistleEquip");
        public bool WhistlePressed => Pressed("WhistleBlow");
        public bool WhistleHeld => Held("WhistleBlow");
        public bool UseEquipmentPressed => Pressed("UseEquipment");
        public bool UseEquipmentHeld => Held("UseEquipment");
        public int EquipmentSlot => Pressed("Equip1") ? 1 : Pressed("Equip2") ? 2 : Pressed("Equip3") ? 3 : 0;
        public float EquipmentScroll => Read<Vector2>("EquipmentCycle").y;
        public bool EquipmentPrevious => Pressed("EquipmentPrevious");
        public bool Inventory => archiveAction?.WasPressedThisFrame() == true;
        public bool Recording => false; // Retired camcorder action; compatibility for old scripts.
        public bool Pause => Actions != null && Actions["Always/Pause"].WasPressedThisFrame();

        InputActionMap gameplay;
        InputAction archiveAction;
        void Awake()
        {
            if (!asset)
            {
                Debug.LogError("Assign HorrorInput to InputReader.", this);
                enabled = false;
                return;
            }

            Actions = Instantiate(asset);
            Actions.Disable();
            gameplay = Actions.FindActionMap("Gameplay", true);
            // Extend only the runtime copy so existing saved scenes keep their input asset intact.
            if (gameplay.FindAction("Focus") == null)
                gameplay.AddAction("Focus", InputActionType.Button, "<Keyboard>/z");
            if (gameplay.FindAction("WhistleEquip") == null)
            {
                var equip = gameplay.AddAction("WhistleEquip", InputActionType.Button, "<Keyboard>/q");
                equip.AddBinding("<Gamepad>/dpad/left");
            }

            if (gameplay.FindAction("WhistleBlow") == null)
            {
                var blow = gameplay.AddAction("WhistleBlow", InputActionType.Button, "<Mouse>/middleButton");
                blow.AddBinding("<Gamepad>/rightShoulder");
            }

            for (int i = 1; i <= 3; i++)
                if (gameplay.FindAction("Equip" + i) == null)
                {
                    var equip = gameplay.AddAction("Equip" + i, InputActionType.Button, "<Keyboard>/" + i);
                    equip.AddBinding("<Keyboard>/numpad" + i);
                }

            if (gameplay.FindAction("EquipmentCycle") == null)
                gameplay.AddAction("EquipmentCycle", InputActionType.Value, "<Mouse>/scroll", expectedControlLayout: "Vector2");
            if (gameplay.FindAction("EquipmentPrevious") == null)
                gameplay.AddAction("EquipmentPrevious", InputActionType.Button, "<Keyboard>/x");
            if (gameplay.FindAction("UseEquipment") == null)
                gameplay.AddAction("UseEquipment", InputActionType.Button, "<Mouse>/leftButton");
            var always = Actions.FindActionMap("Always", true);
            archiveAction = always.AddAction("Archive", InputActionType.Button);
            var inventory = gameplay.FindAction("Inventory");
            if (inventory != null)
                foreach (var binding in inventory.bindings)
                    if (!binding.isComposite && !binding.isPartOfComposite)
                        archiveAction.AddBinding(binding);
            always.Enable();
            Actions.FindActionMap("UI", true).Enable();
            gameplay.Enable();
        }

        void Start()
        {
            var module = UnityEngine.Object.FindAnyObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (!module || Actions == null)
                return;
            module.actionsAsset = Actions;
            module.move = UnityEngine.InputSystem.InputActionReference.Create(Actions["UI/Navigate"]);
            module.submit = UnityEngine.InputSystem.InputActionReference.Create(Actions["UI/Submit"]);
            module.cancel = UnityEngine.InputSystem.InputActionReference.Create(Actions["UI/Cancel"]);
            module.point = UnityEngine.InputSystem.InputActionReference.Create(Actions["UI/Point"]);
            module.leftClick = UnityEngine.InputSystem.InputActionReference.Create(Actions["UI/Click"]);
            module.scrollWheel = UnityEngine.InputSystem.InputActionReference.Create(Actions["UI/ScrollWheel"]);
        }

        public void EnableGameplay(bool value)
        {
            if (gameplay == null)
                return;
            if (value)
                gameplay.Enable();
            else
                gameplay.Disable();
        }

        InputAction Action(string name) => gameplay?.FindAction(name);
        T Read<T>(string name)
            where T : struct
        {
            var a = Action(name);
            return a != null && a.enabled ? a.ReadValue<T>() : default;
        }

        bool Held(string name)
        {
            var action = Action(name);
            return action != null && action.enabled && action.IsPressed();
        }

        bool Pressed(string name) => Action(name)?.WasPressedThisFrame() == true;
        void OnDestroy()
        {
            if (Actions)
            {
                Actions.Disable();
                Destroy(Actions);
            }
        }
    }
}
