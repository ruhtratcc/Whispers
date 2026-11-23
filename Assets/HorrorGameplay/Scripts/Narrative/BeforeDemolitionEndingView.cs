// Mostra a decisão final e as telas associadas ao desfecho.

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace HorrorGame
{
    [DisallowMultipleComponent]
    public sealed class BeforeDemolitionEndingView : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        public UnityEngine.UI.Button RefuseButton { get; private set; }
        public UnityEngine.UI.Button SignButton { get; private set; }
        public UnityEngine.UI.Button ContinueButton { get; private set; }
        public UnityEngine.UI.Button MessageButton { get; private set; }
        public UnityEngine.UI.RawImage Photo => photo;
        public event Action<BeforeDemolitionEndingChoice> ChoiceSelected;
        public event Action Continued;
        [Min(.2f)] public float cardFadeSeconds = .45f;
        [Min(1)] public float badCardSeconds = 4;
        [Min(1)] public float badPhotoSeconds = 5;
        [Min(1)] public float badCreditsSeconds = 8;
        [Min(10)] public float offerReminderSeconds = 40;
        [NonSerialized] public AudioClip[] helenaOfferReminders = Array.Empty<AudioClip>();
        static readonly string[] OfferReminders = { "Helena: Pode pensar, João. Eu espero.", "Helena: O senhor sempre quis consertar isso.", "Helena: É só ficar no lugar deles." };
        public string[] badCards = {
            "Amanheceu. A demolição da SEEPE começou às 8h.",
            "Ninguém encontrou as crianças.",
            "O que saiu da escola não era a Helena.",
            "João de Claus nunca mais foi visto."
        };
        [TextArea] public string badCredits = "WHISPERS OF THE PAST\nANTES DA DEMOLIÇÃO\n\nFinal: Ficar no lugar deles\n\nCréditos e licenças no menu principal.";

        GameSession game, subscribedGame;
        Canvas canvas;
        UnityEngine.UI.GraphicRaycaster raycaster;
        GameObject panel, photoArea, ownedEventSystem, previousSelection;
        TMP_FontAsset normalFont, childFont;
        TMP_Text heading, metadata, body, signature, photoCaption;
        RectTransform bodyRect;
        UnityEngine.UI.RawImage photo;
        bool choosing, ownsInspection;
        bool epilogueComplete;
        float reminderAge;
        int reminderIndex;
        AudioSource reminderSource;
        AudioClip reminderClip;
        Coroutine epilogue;
        UnityEngine.UI.Image paperImage, shadeImage;
        readonly Color ink = new Color(.1f, .15f, .14f);

        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
        static UnityEngine.UI.Image Plate(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; return image;
        }
        TMP_Text Text(string name, Transform parent, Vector2 min, Vector2 max, float size, Color color)
        {
            var text = Rect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = normalFont; text.fontSize = size; text.color = color; text.raycastTarget = false;
            text.richText = false; text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = TextAlignmentOptions.TopLeft; text.margin = new Vector4(10, 6, 10, 6);
            return text;
        }
        UnityEngine.UI.Button Button(string name, Transform parent, Vector2 min, Vector2 max, string label, Action click)
        {
            var rect = Rect(name, parent, min, max);
            var image = Plate(rect, new Color(.18f, .29f, .26f));
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(.62f, .78f, .65f);
            colors.selectedColor = new Color(.62f, .78f, .65f); colors.pressedColor = new Color(.4f, .6f, .46f);
            button.colors = colors;
            var text = Text("Label", rect, Vector2.zero, Vector2.one, 23, new Color(.97f, .97f, .91f));
            text.text = label; text.alignment = TextAlignmentOptions.Center;
            button.onClick.AddListener(() => click()); return button;
        }
        public void Initialize(GameSession session, TMP_FontAsset font, TMP_FontAsset handwriting)
        {
            if (canvas) return;
            game = session; normalFont = font ? font : TMP_Settings.defaultFontAsset;
            childFont = handwriting ? handwriting : normalFont;
            canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 560;
            var scale = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scale.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1280, 720); scale.matchWidthOrHeight = .5f;
            raycaster = gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var shade = Rect("Leitura", transform, Vector2.zero, Vector2.one);
            shadeImage = Plate(shade, new Color(.006f, .011f, .012f, .94f)); panel = shade.gameObject;
            var paper = Rect("Documento", shade, new Vector2(.1f, .08f), new Vector2(.9f, .92f));
            paperImage = Plate(paper, new Color(.91f, .91f, .85f));
            var titleBar = Rect("TitleBar", paper, new Vector2(0, .88f), Vector2.one);
            Plate(titleBar, new Color(.12f, .24f, .23f)).raycastTarget = false;
            heading = Text("Heading", titleBar, new Vector2(.025f, .05f), new Vector2(.975f, .95f), 25, new Color(.95f, .96f, .91f));
            heading.alignment = TextAlignmentOptions.MidlineLeft;
            metadata = Text("Metadata", paper, new Vector2(.045f, .74f), new Vector2(.955f, .87f), 20, ink);
            body = Text("Body", paper, new Vector2(.045f, .23f), new Vector2(.955f, .72f), 25, ink);
            bodyRect = (RectTransform)body.transform;
            photoArea = Rect("Fotografia", paper, new Vector2(.055f, .25f), new Vector2(.58f, .71f)).gameObject;
            var photoRect = Rect("Imagem", photoArea.transform, Vector2.zero, Vector2.one);
            photo = photoRect.gameObject.AddComponent<UnityEngine.UI.RawImage>(); photo.raycastTarget = false;
            var fit = photoRect.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            photoCaption = Text("PhotoCaption", paper, new Vector2(.055f, .18f), new Vector2(.58f, .25f), 16, ink);
            signature = Text("Assinatura", paper, new Vector2(.1f, .26f), new Vector2(.9f, .46f), 52, ink);
            signature.font = childFont; signature.alignment = TextAlignmentOptions.Center; signature.text = "João de Claus";
            RefuseButton = Button("Recusar", paper, new Vector2(.52f, .055f), new Vector2(.945f, .16f), "Não confio em você", () => Select(BeforeDemolitionEndingChoice.Refuse));
            SignButton = Button("Assinar", paper, new Vector2(.055f, .055f), new Vector2(.48f, .16f), "Ficar no lugar deles", () => Select(BeforeDemolitionEndingChoice.Sign));
            ContinueButton = Button("Continuar", paper, new Vector2(.66f, .055f), new Vector2(.945f, .16f), "Continuar", Continue);
            var phone = PhoneMessages.For(game);
            if (phone) MessageButton = Button("Mensagens", paper, new Vector2(.055f, .055f), new Vector2(.48f, .16f), phone.catalog.viewMessage,
                () => { if (phone.OpenOverEnding(() => { raycaster.enabled = true; Focus(MessageButton); })) raycaster.enabled = false; });
            panel.SetActive(false); canvas.enabled = raycaster.enabled = false; Subscribe();
        }
        bool Open()
        {
            if (IsOpen || !game || !canvas || !game.BeginInspection()) return false;
            previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if (!EventSystem.current)
            {
                if (!ownedEventSystem)
                {
                    ownedEventSystem = new GameObject("EndingEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                }
                else ownedEventSystem.SetActive(true);
            }
            IsOpen = ownsInspection = true; panel.SetActive(true); canvas.enabled = raycaster.enabled = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true; return true;
        }
        public bool ShowChoice()
        {
            if (!Open()) return false;
            choosing = true; epilogueComplete = false; reminderAge = 0;
            paperImage.color = new Color(.018f, .025f, .024f, .88f);
            heading.text = ""; metadata.text = "";
            body.color = new Color(.9f, .92f, .88f); body.alignment = TextAlignmentOptions.Center;
            body.text = "Alguém precisa ficar no lugar deles.";
            SetBody(new Vector2(.045f, .23f), new Vector2(.955f, .72f));
            photoArea.SetActive(false); photoCaption.gameObject.SetActive(false); signature.gameObject.SetActive(false);
            RefuseButton.gameObject.SetActive(true); SignButton.gameObject.SetActive(true); ContinueButton.gameObject.SetActive(false);
            if (MessageButton) MessageButton.gameObject.SetActive(false);
            Focus(SignButton); return true;
        }
        public bool ShowEpilogue(BeforeDemolitionEndingChoice choice, Texture2D image)
        {
            // The good branch stays in the room until the whistle is returned, then uses the office epilogue.
            if (choice != BeforeDemolitionEndingChoice.Sign || !Open()) return false;
            choosing = epilogueComplete = false;
            paperImage.color = Color.black; shadeImage.color = Color.black;
            heading.text = metadata.text = "";
            body.alignment = TextAlignmentOptions.Center;
            photoArea.SetActive(false); photoCaption.gameObject.SetActive(false); signature.gameObject.SetActive(false);
            RefuseButton.gameObject.SetActive(false); SignButton.gameObject.SetActive(false); ContinueButton.gameObject.SetActive(false);
            if (MessageButton) MessageButton.gameObject.SetActive(false);
            epilogue = StartCoroutine(BadEndingCards(image));
            return true;
        }
        IEnumerator BadEndingCards(Texture2D image)
        {
            SetBody(new Vector2(.08f, .25f), new Vector2(.92f, .78f));
            foreach (string card in badCards ?? Array.Empty<string>())
            {
                body.text = card;
                yield return FadeText(0, 1, cardFadeSeconds);
                yield return WaitVisible(badCardSeconds);
                yield return FadeText(1, 0, cardFadeSeconds);
            }
            body.text = "";
            photo.texture = image;
            photoArea.SetActive(image);
            if (image)
            {
                var photoRect = (RectTransform)photoArea.transform;
                photoRect.anchorMin = new Vector2(.08f, .18f); photoRect.anchorMax = new Vector2(.92f, .86f);
                photo.GetComponent<UnityEngine.UI.AspectRatioFitter>().aspectRatio = (float)image.width / image.height;
                photo.color = Color.white;
            }
            else
            {
                body.text = "SEEPE\nA janela do último andar continua acesa.";
                body.color = new Color(.9f, .92f, .88f);
            }
            yield return WaitVisible(badPhotoSeconds);
            photoArea.SetActive(false);
            body.text = badCredits;
            yield return FadeText(0, 1, cardFadeSeconds);
            yield return WaitVisible(badCreditsSeconds);
            epilogueComplete = true; epilogue = null;
            ContinueButton.GetComponentInChildren<TMP_Text>().text = "Encerrar";
            ContinueButton.gameObject.SetActive(true);
            Focus(ContinueButton);
        }
        IEnumerator FadeText(float from, float to, float seconds)
        {
            float age = 0;
            while (age < Mathf.Max(.01f, seconds))
            {
                if (game && game.State != GameState.Paused) age += Time.unscaledDeltaTime;
                body.color = new Color(.9f, .92f, .88f, Mathf.Lerp(from, to, age / Mathf.Max(.01f, seconds)));
                yield return null;
            }
            body.color = new Color(.9f, .92f, .88f, to);
        }
        IEnumerator WaitVisible(float seconds)
        {
            while (seconds > 0)
            { if (game && game.State != GameState.Paused) seconds -= Time.unscaledDeltaTime; yield return null; }
        }
        void Update()
        {
            if (!IsOpen || !choosing || !game || game.State != GameState.Inspecting) return;
            reminderAge += Time.unscaledDeltaTime;
            if (reminderAge < Mathf.Max(10, offerReminderSeconds)) return;
            reminderAge = 0;
            int index = reminderIndex++ % OfferReminders.Length;
            reminderClip = index < helenaOfferReminders.Length ? helenaOfferReminders[index] : null;
            reminderSource = AudioManager.Instance?.PlayClip(reminderClip, transform.position, .62f, 1, AudioChannel.Voice, false);
            game.hud?.ShowSubtitle(OfferReminders[index], reminderClip ? Mathf.Max(4, reminderClip.length + .15f) : 4);
            shadeImage.color = new Color(.006f, .011f, .012f, Mathf.Min(.985f, shadeImage.color.a + .01f));
        }
        void SetBody(Vector2 min, Vector2 max)
        { bodyRect.anchorMin = min; bodyRect.anchorMax = max; bodyRect.offsetMin = bodyRect.offsetMax = Vector2.zero; }
        void Focus(UnityEngine.UI.Button button)
        { if (EventSystem.current && button && button.gameObject.activeInHierarchy) EventSystem.current.SetSelectedGameObject(button.gameObject); }
        void Select(BeforeDemolitionEndingChoice choice)
        {
            if (!IsOpen || !choosing || !game || game.State != GameState.Inspecting) return;
            Close(); ChoiceSelected?.Invoke(choice);
        }
        void Continue()
        {
            if (!IsOpen || choosing || !epilogueComplete || !game || game.State != GameState.Inspecting) return;
            Close(); Continued?.Invoke();
        }
        public void Close()
        {
            if (!IsOpen && !ownsInspection) return;
            if (reminderSource && reminderSource.clip == reminderClip) reminderSource.Stop();
            reminderSource = null; reminderClip = null;
            IsOpen = false; if (panel) panel.SetActive(false);
            if (epilogue != null) StopCoroutine(epilogue);
            epilogue = null; epilogueComplete = false;
            if (canvas) canvas.enabled = false; if (raycaster) raycaster.enabled = false;
            bool release = ownsInspection; ownsInspection = false;
            if (ownedEventSystem) ownedEventSystem.SetActive(false);
            else if (EventSystem.current) EventSystem.current.SetSelectedGameObject(previousSelection && previousSelection.activeInHierarchy ? previousSelection : null);
            previousSelection = null;
            if (release && game) game.EndInspection();
        }
        void StateChanged(GameState next)
        {
            if (!IsOpen) return;
            if (next != GameState.Inspecting && next != GameState.Paused) { Close(); return; }
            bool visible = next == GameState.Inspecting;
            canvas.enabled = visible;
            var messages = PhoneMessages.For(game);
            raycaster.enabled = visible && (!messages || !messages.IsOpen);
            if (visible && (!messages || !messages.IsOpen)) Focus(choosing ? SignButton : epilogueComplete ? ContinueButton : null);
        }
        void Subscribe()
        {
            if (subscribedGame == game) return;
            if (subscribedGame) subscribedGame.StateChanged -= StateChanged;
            subscribedGame = game;
            if (subscribedGame) subscribedGame.StateChanged += StateChanged;
        }
        void OnEnable() => Subscribe();
        void OnDisable()
        {
            Close(); if (subscribedGame) subscribedGame.StateChanged -= StateChanged; subscribedGame = null;
        }
        void OnDestroy() { if (ownedEventSystem) Destroy(ownedEventSystem); }
    }
}
