// Controla o item da mão, a troca de slot e o uso do apito ou da lanterna.

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HorrorGame
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class EquipmentController : MonoBehaviour
    {
        public GameSession game;
        public FlashlightController flashlight;
        public WhistleSystem whistle;
        public Inventory inventory;
        public EquipmentDefinition[] items = Array.Empty<EquipmentDefinition>();
        public EquipmentHud hud;
        public SoundSet selectionSound, clothSound;
        [Range(.25f, .4f)]
        public float switchSeconds = .32f;
        public Vector3 loweredOffset = new Vector3(0, -.24f, -.04f);
        public string officeBlockedLine = "A lanterna ficou no carro.";
        public AudioClip officeBlockedVoice;
        public event Action Changed;
        BeforeDemolitionRoadFlow road;
        PhoneMessages phone;
        BeforeDemolitionMirrorFlow mirror;
        int active = -1, previous = -1, target = -1, knownMask;
        bool switching, committed, restored;
        float switchAge, blockedUntil;
        Transform[] extraVisuals;
        public bool Switching => switching;
        public int ActiveIndex => active;
        public EquipmentDefinition ActiveItem => Item(active);
        public bool HasFlashlight => Owns(FindKind(EquipmentKind.Flashlight));
        public bool PresentationLight => game && HasFlashlight && !game.IsBlackingOut && gameObject.scene.name == "SchoolCEEP" && (game.State == GameState.Cutscene || game.narrative && game.narrative.SequencePlaying || mirror && mirror.IsPresenting);
        public bool ReservesPrimaryClick => HandsVisible && (ActiveItem || switching);
        public bool HandsVisible => isActiveAndEnabled && game && game.CanControl && !game.IsBlackingOut && (!game.narrative || !game.narrative.SequencePlaying) && (!road || !road.driver || !road.driver.Seated) && (!game.player || !game.player.IsHidden) && (!phone || !phone.IsOpen);
        public bool CanSwitch => HandsVisible && !switching && (!whistle || !whistle.Busy) && (!game.interaction || !game.interaction.EquipmentInteractionBusy) && (!game.interaction || !game.interaction.grabber || !game.interaction.grabber.Held) && (!InspectionManager.Instance || !InspectionManager.Instance.IsCompleting);
        public bool LightAllowed => PresentationLight || (HandsVisible || game && game.lockpick && game.lockpick.KeepLightWhilePicking) && !switching && (IsActive(EquipmentKind.Flashlight) || active >= 0 && EquipmentPreferences.KeepLight && Owns(FindKind(EquipmentKind.Flashlight)));

        public bool IsActive(EquipmentKind kind) => ActiveItem && ActiveItem.kind == kind;
        public float LowerAmount => !switching ? 0 : committed ? 1 - Mathf.Clamp01((switchAge / switchSeconds - .5f) * 2) : Mathf.Clamp01(switchAge / switchSeconds * 2);
        public Vector3 FlashlightOffset => loweredOffset * LowerAmount + (IsActive(EquipmentKind.Flashlight) ? Vector3.zero : new Vector3(-.34f, -.015f, 0));

        void Awake()
        {
            if (!game)
                game = GetComponent<GameSession>();
            if (!inventory && game && game.interaction)
                inventory = game.interaction.inventory;
            if (!flashlight && game && game.save)
                flashlight = game.save.flashlight;
            if (!whistle)
                whistle = GetComponent<WhistleSystem>();
            road = GetComponent<BeforeDemolitionRoadFlow>();
            phone = GetComponent<PhoneMessages>();
            mirror = FindAnyObjectByType<BeforeDemolitionMirrorFlow>();
            if (game)
                game.equipment = this;
            if (flashlight)
                flashlight.equipment = this;
            if (whistle)
                whistle.equipment = this;
            extraVisuals = new Transform[items.Length];
            Reconcile(false);
            for (int i = 0; i < items.Length; i++)
                if (items[i] && items[i].kind == EquipmentKind.Other && items[i].viewmodelPrefab && flashlight && flashlight.view)
                {
                    extraVisuals[i] = Instantiate(items[i].viewmodelPrefab, flashlight.view.transform).transform;
                    extraVisuals[i].gameObject.SetActive(false);
                }
        }

        void OnEnable()
        {
            if (inventory)
                inventory.Changed += InventoryChanged;
            if (game && game.narrative)
            {
                game.narrative.SignalAccepted += Signal;
                game.narrative.Initialized += Initialized;
            }

            EquipmentPreferences.Changed += PreferencesChanged;
        }

        void Start()
        {
            Reconcile(false);
            if (!restored && flashlight && flashlight.RequestedOn && gameObject.scene.name == "SchoolCEEP")
                SetImmediate(FindKind(EquipmentKind.Flashlight));
        }

        void InventoryChanged() => Reconcile(true);
        public void RefreshOwnership() => Reconcile(false);
        void Signal(string id)
        {
            if (id == "bd.road.flashlight-taken")
                Reconcile(true);
        }

        void Initialized() => Reconcile(false);
        void PreferencesChanged()
        {
            Changed?.Invoke();
            hud?.ShowFor(2.5f);
            Save();
        }

        EquipmentDefinition Item(int i) => i >= 0 && i < items.Length ? items[i] : null;
        public int FindKind(EquipmentKind kind)
        {
            for (int i = 0; i < items.Length; i++)
                if (items[i] && items[i].kind == kind)
                    return i;
            return -1;
        }

        int FindId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return -1;
            for (int i = 0; i < items.Length; i++)
                if (items[i] && items[i].id == id)
                    return i;
            return -1;
        }

        public bool Owns(int i) => i >= 0 && i < 31 && (knownMask & (1 << i)) != 0;
        bool ReadOwnership(int i)
        {
            var item = Item(i);
            if (!item)
                return false;
            if (item.kind == EquipmentKind.Whistle && whistle && whistle.Returned)
                return false;
            if (item.kind == EquipmentKind.Flashlight)
                return gameObject.scene.name != "Chapter1Office" && (!road || road.HasTorch);
            return inventory && !string.IsNullOrEmpty(item.inventoryItemId) && inventory.Contains(item.inventoryItemId);
        }

        public bool Available(int i) => Owns(i) && !(Item(i).kind == EquipmentKind.Flashlight && gameObject.scene.name == "Chapter1Office");
        void Reconcile(bool announce)
        {
            int mask = 0;
            for (int i = 0; i < items.Length && i < 31; i++)
                if (ReadOwnership(i))
                    mask |= 1 << i;
            int added = mask & ~knownMask;
            bool changed = mask != knownMask;
            knownMask = mask;
            if (active >= 0 && !Available(active))
            {
                switching = false;
                SetImmediate(-1);
            }

            if (previous >= 0 && !Available(previous))
                previous = -1;
            if (changed)
            {
                Changed?.Invoke();
                if (announce && added != 0)
                    hud?.ShowFor(3.5f);
            }
        }

        void Update()
        {
            if (!game)
                return;
            if (game.State == GameState.Paused)
                return;
            if (switching && !HandsVisible)
            {
                switching = false;
                target = active;
                ApplyWhistle();
                Changed?.Invoke();
            }

            if (game.input && CanSwitch)
            {
                int slot = game.input.EquipmentSlot;
                if (slot > 0)
                    RequestSlot(slot);
                else if (game.input.EquipmentPrevious)
                    Previous();
                else if (game.input.WhistleEquip)
                {
                    if (game.lockpick && game.lockpick.Acquired)
                        Cycle(1);
                    else
                        RequestSlot(2);
                }
                else if (Mathf.Abs(game.input.EquipmentScroll) > .01f && WheelAllowed())
                    Cycle(game.input.EquipmentScroll > 0 ? 1 : -1);
                if ((game.input.Flashlight || game.input.UseEquipmentPressed) && CanSwitch && IsActive(EquipmentKind.Flashlight))
                    flashlight?.Toggle();
            }

            Advance(Time.deltaTime);
        }

        bool WheelAllowed()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return false;
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            return !selected || !selected.activeInHierarchy || (!selected.GetComponentInParent<UnityEngine.UI.ScrollRect>() && !selected.GetComponent<TMPro.TMP_InputField>());
        }

        public bool RequestSlot(int slot)
        {
            for (int i = 0; i < items.Length; i++)
                if (items[i] && items[i].slot == slot)
                    return Request(i);
            return false;
        }

        bool Request(int index)
        {
            if (!CanSwitch || !Owns(index))
                return false;
            hud?.ShowFor(2.5f);
            if (!Available(index))
            {
                if (Time.unscaledTime >= blockedUntil)
                {
                    blockedUntil = Time.unscaledTime + 6;
                    game.hud?.ShowSubtitle("João: " + officeBlockedLine, 3);
                    if (officeBlockedVoice)
                        AudioManager.Instance?.PlayClip(officeBlockedVoice, transform.position, .5f, 1, AudioChannel.Voice, false);
                }

                return false;
            }

            target = active == index ? -1 : index;
            switchAge = 0;
            switching = true;
            committed = false;
            if (ActiveItem)
                Play(ActiveItem.stowSound, .13f, AudioChannel.SFX);
            Play(clothSound ? clothSound : Item(index).moveSound, .10f, AudioChannel.SFX);
            Play(selectionSound, .035f, AudioChannel.UI);
            Changed?.Invoke();
            return true;
        }

        public bool Previous()
        {
            if (!CanSwitch)
                return false;
            if (previous >= 0 && Available(previous) && previous != active)
                return Request(previous);
            for (int i = 0; i < items.Length; i++)
                if (i != active && Available(i))
                    return Request(i);
            return active >= 0 && Request(active);
        }

        public bool Cycle(int direction)
        {
            if (!CanSwitch || direction == 0)
                return false;
            hud?.ShowFor(2.5f);
            if (EquipmentPreferences.ReverseWheel)
                direction = -direction;
            int step = direction > 0 ? 1 : -1;
            for (int n = 1; n <= items.Length; n++)
            {
                int i = (active < 0 ? (step > 0 ? n - 1 : items.Length - n) : (active + step * n + items.Length * 2) % items.Length);
                if (Available(i))
                {
                    if (i == active)
                        return false;
                    return Request(i);
                }
            }

            return false;
        }

        public void Advance(float seconds)
        {
            if (!switching || !float.IsFinite(seconds) || seconds <= 0 || !HandsVisible || game.State == GameState.Paused)
                return;
            switchAge += seconds;
            float duration = PresentationSettings.ReduceEffects ? Mathf.Min(.25f, switchSeconds) : switchSeconds;
            if (!committed && switchAge >= duration * .5f)
            {
                previous = active >= 0 ? active : previous;
                active = target;
                committed = true;
                ApplyWhistle();
                if (ActiveItem)
                    Play(ActiveItem.drawSound, .13f, AudioChannel.SFX);
                Changed?.Invoke();
            }

            if (switchAge >= duration)
            {
                switching = false;
                switchAge = 0;
                Changed?.Invoke();
                hud?.ShowFor(2.5f);
                Save();
            }
        }

        void Play(SoundSet set, float gain, AudioChannel channel)
        {
            if (set)
                AudioManager.Instance?.Play(set, flashlight && flashlight.view ? flashlight.view.transform.position : transform.position, gain, channel, false);
        }

        void SetImmediate(int index)
        {
            active = Available(index) ? index : -1;
            target = active;
            switching = false;
            ApplyWhistle();
            Changed?.Invoke();
        }

        void ApplyWhistle()
        {
            if (whistle)
                whistle.SetEquipped(IsActive(EquipmentKind.Whistle) && HandsVisible);
        }

        public void CollectedFlashlight()
        {
            Reconcile(true);
            SetImmediate(FindKind(EquipmentKind.Flashlight));
            hud?.ShowFor(3.5f);
            game?.tutorials?.Discover("flashlight");
        }

        void LateUpdate()
        {
            bool visible = HandsVisible;
            if (flashlight && flashlight.handRig)
                flashlight.handRig.gameObject.SetActive(visible && (IsActive(EquipmentKind.Flashlight) || EquipmentPreferences.KeepLight && active >= 0 && Owns(FindKind(EquipmentKind.Flashlight))) || game && game.lockpick && game.lockpick.KeepLightWhilePicking);
            if (whistle)
            {
                bool show = visible && IsActive(EquipmentKind.Whistle);
                if (show && !whistle.Equipped && !whistle.Busy)
                    whistle.SetEquipped(true);
                if (whistle.handVisual)
                {
                    whistle.handVisual.gameObject.SetActive(show);
                    if (!whistle.Busy)
                        whistle.handVisual.localPosition = new Vector3(.14f, -.28f, .30f) + loweredOffset * LowerAmount;
                }
            }

            for (int i = 0; i < extraVisuals.Length; i++)
                if (extraVisuals[i])
                {
                    extraVisuals[i].gameObject.SetActive(visible && active == i);
                    extraVisuals[i].localPosition = new Vector3(.14f, -.28f, .30f) + loweredOffset * LowerAmount;
                }
        }

        public EquipmentState Capture() => new EquipmentState
        {
            initialized = true,
            equippedId = Item(switching ? target : active) ? Item(switching ? target : active).id : "",
            previousId = Item(previous) ? Item(previous).id : "",
            flashlightOn = flashlight && flashlight.RequestedOn,
            hudMode = EquipmentPreferences.Mode,
            keepFlashlight = EquipmentPreferences.KeepLight,
            reverseWheel = EquipmentPreferences.ReverseWheel
        };
        public void Restore(EquipmentState saved, bool legacyFlashlightOn)
        {
            restored = true;
            if (saved != null && (!saved.Valid || !saved.initialized))
                saved = null;
            previous = saved == null ? -1 : FindId(saved.previousId);
            if (saved != null)
                EquipmentPreferences.Set(saved.hudMode, saved.keepFlashlight, saved.reverseWheel);
            flashlight?.Restore(flashlight.BatterySeconds, saved?.flashlightOn ?? legacyFlashlightOn);
            SetImmediate(saved == null ? (legacyFlashlightOn ? FindKind(EquipmentKind.Flashlight) : -1) : FindId(saved.equippedId));
            Reconcile(false);
        }

        void Save()
        {
            if (game && game.narrative && !game.IsRestoringCheckpoint)
                game.narrative.ScheduleSave();
        }

        void OnDisable()
        {
            if (inventory)
                inventory.Changed -= InventoryChanged;
            if (game && game.narrative)
            {
                game.narrative.SignalAccepted -= Signal;
                game.narrative.Initialized -= Initialized;
            }

            EquipmentPreferences.Changed -= PreferencesChanged;
            if (whistle)
                whistle.SetEquipped(false);
        }
    }
}
