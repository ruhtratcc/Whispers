// Apresenta a interface da ligação recebida na estrada.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HorrorGame
{
    public sealed class RoadPhoneCallView : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        public Button CloseButton { get; private set; }
        public event Action Closed;
        GameSession game;
        Canvas canvas;
        GameObject panel;
        int openedFrame;
        public void Initialize(GameSession session, TMP_FontAsset font)
        {
            if (canvas) return;
            game = session;
            canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 550;
            var scale = gameObject.AddComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1920, 1080); scale.matchWidthOrHeight = 1;
            gameObject.AddComponent<GraphicRaycaster>();
            panel = InspectionUI.Fill("Chamada recebida", transform).gameObject;
            panel.AddComponent<Image>().color = new Color(.004f, .007f, .009f, .94f);
            InspectionUI.Text("Sem identificação", panel.transform, "Número desconhecido", new Vector2(920, 85), new Vector2(0, 230), font, 36);
            InspectionUI.Text("Estado da chamada", panel.transform, "CHAMADA EM ANDAMENTO", new Vector2(920, 44), new Vector2(0, 145), font, 19);
            InspectionUI.Text("Cantiga", panel.transform, "Só a cantiga de roda.\nUm pouco fora de sincronia com o rádio.", new Vector2(1050, 150), Vector2.zero, font, 26);
            CloseButton = InspectionUI.Button("Encerrar chamada", panel.transform, "Encerrar  [E / Esc]", new Vector2(0, -250), new Vector2(400, 68), font, Close);
            panel.SetActive(false); canvas.enabled = false;
        }
        public bool Open()
        {
            if (IsOpen || !game || !game.BeginInspection()) return false;
            IsOpen = true; openedFrame = Time.frameCount; panel.SetActive(true); canvas.enabled = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(CloseButton.gameObject); return true;
        }
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false; panel.SetActive(false); canvas.enabled = false;
            if (game) game.EndInspection(); Closed?.Invoke();
        }
        void Update()
        {
            if (!IsOpen) return;
            canvas.enabled = game && game.State != GameState.Paused;
            if (!canvas.enabled || Time.frameCount <= openedFrame) return;
            if (game.State != GameState.Inspecting) { Close(); return; }
            if (Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)) Close();
        }
        void OnDisable() => Close();
    }
}
