// Transforma a interação do jogador em cursor na tela do monitor.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HorrorGame.Desktop
{
    public sealed class DesktopPointer : MonoBehaviour
    {
        public Vector2 Position { get; private set; } = new Vector2(512, 384);
        public GameObject Hovered { get; private set; }

        RetroComputer host;
        UnityEngine.UI.GraphicRaycaster raycaster;
        Camera camera;
        PointerEventData pointer;
        readonly List<RaycastResult> hits = new List<RaycastResult>();
        GameObject pressed, drag, lastClicked;
        float lastClickTime;
        bool dragging;
        EventSystem ownerEvents;
        public void Initialize(RetroComputer owner, UnityEngine.UI.GraphicRaycaster ui, Camera view)
        {
            host = owner;
            raycaster = ui;
            camera = view;
        }

        void EnsureEvent()
        {
            if (pointer == null || ownerEvents != EventSystem.current)
            {
                ownerEvents = EventSystem.current;
                pointer = new PointerEventData(ownerEvents)
                {
                    pointerId = -991
                };
            }
        }

        public void SetPosition(Vector2 value)
        {
            Position = new Vector2(Mathf.Clamp(value.x, 0, 1023), Mathf.Clamp(value.y, 0, 767));
            RefreshHover();
        }

        public void RefreshHover()
        {
            if (!EventSystem.current || !raycaster)
                return;
            EnsureEvent();
            var before = pointer.position;
            pointer.position = Position;
            pointer.delta = Position - before;
            hits.Clear();
            raycaster.Raycast(pointer, hits);
            var over = hits.Count > 0 ? hits[0].gameObject : null;
            pointer.pointerCurrentRaycast = hits.Count > 0 ? hits[0] : new RaycastResult();
            if (over != Hovered)
            {
                if (Hovered)
                    ExecuteEvents.ExecuteHierarchy(Hovered, pointer, ExecuteEvents.pointerExitHandler);
                Hovered = over;
                if (Hovered)
                    ExecuteEvents.ExecuteHierarchy(Hovered, pointer, ExecuteEvents.pointerEnterHandler);
            }

            pointer.pointerEnter = over;
            host.SetCursorVisual(Position, over, !host.Ready);
            if (pressed && drag && Vector2.Distance(pointer.pressPosition, Position) > 4)
            {
                if (!dragging)
                {
                    dragging = true;
                    pointer.dragging = true;
                    ExecuteEvents.Execute(drag, pointer, ExecuteEvents.beginDragHandler);
                }

                ExecuteEvents.Execute(drag, pointer, ExecuteEvents.dragHandler);
            }
        }

        public void ReadMouse(float sensitivity)
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;
            SetPosition(Position + mouse.delta.ReadValue() * sensitivity);
            if (mouse.leftButton.wasPressedThisFrame)
                Down(false);
            if (mouse.leftButton.wasReleasedThisFrame)
                Up(false);
            if (mouse.rightButton.wasPressedThisFrame)
                Down(true);
            if (mouse.rightButton.wasReleasedThisFrame)
                Up(true);
            var scroll = mouse.scroll.ReadValue();
            if (scroll.sqrMagnitude > 0 && Hovered)
            {
                pointer.scrollDelta = scroll / 120;
                ExecuteEvents.ExecuteHierarchy(Hovered, pointer, ExecuteEvents.scrollHandler);
            }
        }

        public void Down(bool right)
        {
            RefreshHover();
            if (pointer == null)
                return;
            host.ClearEntry();
            pointer.button = right ? PointerEventData.InputButton.Right : PointerEventData.InputButton.Left;
            pointer.pressPosition = Position;
            pointer.pointerPressRaycast = pointer.pointerCurrentRaycast;
            pointer.eligibleForClick = true;
            pointer.dragging = false;
            dragging = false;
            Hovered?.GetComponentInParent<DesktopWindow>()?.Focus();
            pressed = Hovered ? ExecuteEvents.ExecuteHierarchy(Hovered, pointer, ExecuteEvents.pointerDownHandler) : null;
            if (!pressed && Hovered)
                pressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hovered);
            pointer.pointerPress = pressed;
            drag = Hovered && !right ? ExecuteEvents.GetEventHandler<IDragHandler>(Hovered) : null;
            pointer.pointerDrag = drag;
            if (drag)
                ExecuteEvents.Execute(drag, pointer, ExecuteEvents.initializePotentialDrag);
        }

        public void Up(bool right)
        {
            if (pointer == null)
                return;
            pointer.button = right ? PointerEventData.InputButton.Right : PointerEventData.InputButton.Left;
            if (pressed)
                ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
            var clicked = Hovered ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hovered) : null;
            if (!dragging && clicked && clicked == pressed)
            {
                pointer.clickCount = lastClicked == clicked && Time.unscaledTime - lastClickTime < .35f ? 2 : 1;
                pointer.clickTime = Time.unscaledTime;
                ExecuteEvents.Execute(clicked, pointer, ExecuteEvents.pointerClickHandler);
                lastClicked = clicked;
                lastClickTime = Time.unscaledTime;
            }

            if (right && (!Hovered || !Hovered.GetComponentInParent<DesktopWindow>() || Hovered.GetComponentInParent<DesktopWindow>().AppId != "mines"))
                host.OpenContextMenu(Hovered, Position);
            if (dragging && drag)
                ExecuteEvents.Execute(drag, pointer, ExecuteEvents.endDragHandler);
            pressed = drag = null;
            dragging = false;
            pointer.pointerPress = null;
            pointer.pointerDrag = null;
            pointer.dragging = false;
        }

        public void ClickAt(Vector2 position, bool right = false)
        {
            SetPosition(position);
            Down(right);
            Up(right);
        }
    }
}
