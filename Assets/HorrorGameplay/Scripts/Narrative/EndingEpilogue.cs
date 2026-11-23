// Orquestra a sequência final, incluindo transições, notícias e apresentação das cenas.

using System;
using System.Collections;
using System.Collections.Generic;
using HorrorGame.Desktop;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorGame
{
    [DisallowMultipleComponent]
    public sealed class EndingEpilogue : MonoBehaviour
    {
        public GameSession game;
        public BeforeDemolitionMirrorFlow mirror;
        public GameObject morningOfficePrefab;
        public EndingPresentationConfig config;
        public static bool Active { get; private set; }
        public bool IsRunning { get; private set; }
        public string Stage { get; private set; } = "Idle";
        public int ReplySelected { get; private set; } = -1;
        public RetroComputer Computer => computer;
        public GameObject MorningOffice => morningOffice;

        const string ReplyMessageId = "ending.helena.reply";
        BeforeDemolitionMirrorFlow subscribedMirror;
        GameObject morningOffice, lightRoot, overlay, replyPanel;
        RetroComputer computer;
        EmailCatalog finalMail;
        Canvas canvas;
        UnityEngine.UI.Image curtain;
        TMP_Text caption, credits, sentText, answerText;
        GameObject mailNotice;
        readonly List<UnityEngine.UI.Button> replyButtons = new List<UnityEngine.UI.Button>();
        readonly List<AudioSource> sources = new List<AudioSource>();
        readonly List<AudioClip> sourceClips = new List<AudioClip>();
        Transform seatPose, windowPose, tvPose, pcPose;
        Camera camera;
        FirstPersonCamera cameraMotor;
        bool cameraWasEnabled, restoreCaptured, ownsControl;
        Vector3 cameraLocalPosition;
        Quaternion cameraLocalRotation;
        Vector3 schoolPlayerPosition;
        float schoolPlayerHeading, schoolPlayerPitch;
        float originalFov, sirenAge;
        CameraClearFlags originalClear;
        Color originalBackground, originalAmbient;
        AmbientMode originalAmbientMode;
        bool originalFog;
        Light red, blue, redSpill, blueSpill;
        AudioSource siren;
        Coroutine routine, newsRoutine;
        string[] replyTexts;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => Active = false;

        void Awake() { if (!game) game = GetComponent<GameSession>(); }
        void OnEnable() => Bind();
        void Start() => Bind();
        void Bind()
        {
            if (!game) game = GetComponent<GameSession>();
            if (subscribedMirror == mirror) return;
            if (subscribedMirror) subscribedMirror.GoodEndingRequested -= Requested;
            subscribedMirror = mirror;
            if (subscribedMirror) subscribedMirror.GoodEndingRequested += Requested;
        }
        void Requested() => Begin();
        public bool Begin()
        {
            if (IsRunning || Active || !game || !game.player || !game.player.view || !game.player.view.playerCamera
                || !mirror || !mirror.GoodEndingReady || !config || !morningOfficePrefab) return false;
            cameraMotor = game.player.view; camera = cameraMotor.playerCamera;
            cameraWasEnabled = cameraMotor.enabled; cameraLocalPosition = camera.transform.localPosition;
            cameraLocalRotation = camera.transform.localRotation; originalFov = camera.fieldOfView;
            schoolPlayerPosition = game.player.transform.position; schoolPlayerHeading = game.player.transform.eulerAngles.y;
            schoolPlayerPitch = game.player.Pitch;
            originalClear = camera.clearFlags; originalBackground = camera.backgroundColor;
            originalAmbient = RenderSettings.ambientLight; originalAmbientMode = RenderSettings.ambientMode;
            originalFog = RenderSettings.fog; restoreCaptured = true;
            replyTexts = config.replies != null && config.replies.Length >= 2 ? config.replies
                : new[] { "Desculpa.", "Obrigado.", "Eu devia ter respondido." };
            ReplySelected = -1; IsRunning = Active = true; Stage = "Phone";
            routine = StartCoroutine(Run());
            return true;
        }

        IEnumerator Run()
        {
            try
            {
                game.DisableControl(); ownsControl = true;
                cameraMotor.enabled = false;
                CreateOverlay();
                yield return Fade(0, 1, config.cardFadeSeconds);
                foreach (var line in config.goodPhoneLines ?? Array.Empty<StoryLine>()) yield return Speak(line, Vector3.zero, false);
                CreateMorningOffice();
                camera.fieldOfView = 49;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.58f, .63f, .66f);
                camera.transform.SetPositionAndRotation(seatPose.position, seatPose.rotation);
                Stage = "Waking";
                newsRoutine = StartCoroutine(Speak(config.morningNews, TelevisionPosition(), true));
                yield return Fade(1, 0, config.eyeSeconds);
                Stage = "Morning";
                float duration = Mathf.Clamp(config.morningSeconds, 12, 18);
                yield return MoveCamera(windowPose, duration * .33f);
                yield return Wait(duration * .07f);
                yield return MoveCamera(tvPose, duration * .27f);
                yield return Wait(duration * .08f);
                Stage = "EmailNotice";
                mailNotice.SetActive(true);
                Play(config.emailSound, pcPose.position, .48f, AudioChannel.UI, false);
                yield return Wait(1.8f);
                mailNotice.SetActive(false);
                yield return MoveCamera(pcPose, duration * .25f);
                yield return newsRoutine;
                newsRoutine = null;
                caption.text = "";
                game.EnableControl(); ownsControl = false;
                Stage = "Email";
                while (ReplySelected < 0)
                {
                    if (game.State == GameState.Paused) { yield return null; continue; }
                    if (!computer.IsSeated)
                    {
                        if (game.CanControl) computer.Open();
                        yield return null; continue;
                    }
                    if (computer.Ready)
                    {
                        DesktopWindow mail;
                        if (!computer.Windows.TryGetValue("mail", out mail) || !mail)
                            mail = computer.OpenApp("mail", ReplyMessageId);
                        else if (mail.Minimized) mail.Restore();
                        if (mail && computer.inbox.CurrentMessageId != ReplyMessageId) mail.App?.OnOpen(ReplyMessageId);
                        if (mail && computer.inbox.State != null && computer.inbox.State.IsRead(ReplyMessageId)) EnsureReplyPanel(mail);
                    }
                    yield return null;
                }
                Stage = "Reply";
                if (sentText) sentText.text = "João: " + replyTexts[ReplySelected];
                yield return Wait(1);
                if (answerText) answerText.text = "Helena: " + HelenaReply(ReplySelected);
                Play(config.emailSound, computer.screenSurface ? computer.screenSurface.position : computer.transform.position, .12f, AudioChannel.UI, false);
                yield return Wait(4);
                computer.Close();
                while (computer && computer.IsSeated) yield return null;
                game.DisableControl(); ownsControl = true;
                Stage = "Credits";
                yield return Fade(0, 1, config.cardFadeSeconds);
                credits.text = config.creditsText;
                credits.gameObject.SetActive(true);
                yield return Wait(config.creditsSeconds);
                DisposeMorning();
                // Destroy is deferred; no cloned PersistentObject may enter the school completion save.
                yield return null;
                Active = false; IsRunning = false; Stage = "Complete";
                if (ownsControl) { ownsControl = false; game.EnableControl(); }
                mirror.CompleteEnding();
            }
            finally
            {
                Cleanup();
                routine = null;
            }
        }

        void CreateMorningOffice()
        {
            morningOffice = Instantiate(morningOfficePrefab, config.morningOrigin, Quaternion.identity);
            morningOffice.name = "Escritório — amanhecer (apresentação)";
            morningOffice.SetActive(false);
            computer = morningOffice.GetComponentInChildren<RetroComputer>(true);
            if (!computer || !computer.inbox) throw new InvalidOperationException("O escritório de apresentação precisa do PC e da caixa de entrada existentes.");
            computer.game = game; computer.office = null; computer.endingAccess = true;
            computer.inbox.game = game; computer.inbox.openOnStart = false;
            computer.inbox.enforceChronologicalReading = false;
            computer.State.visits = Mathf.Max(1, computer.State.visits);
            if (!computer.State.eventIds.Contains("desktop.cursor")) computer.State.eventIds.Add("desktop.cursor");
            finalMail = ScriptableObject.CreateInstance<EmailCatalog>();
            finalMail.name = "Email final — temporário";
            finalMail.messages = new[] { new EmailMessage {
                id = ReplyMessageId, sender = "Helena Souza", date = "27/10/2026", time = "08:12",
                subject = "Pode me responder agora.", initiallyDelivered = true,
                paragraphs = new[] { new EmailParagraph { text = "Pode me responder agora." } }
            } };
            computer.inbox.Configure(finalMail);
            seatPose = Pose("EpilogueSeat", config.seatPosition, config.seatEuler);
            windowPose = Pose("EpilogueWindow", config.windowPosition, config.windowEuler);
            tvPose = Pose("EpilogueTV", config.tvPosition, config.tvEuler);
            pcPose = Find("EpiloguePC");
            if (!pcPose) pcPose = computer.seatCameraPose ? computer.seatCameraPose : Pose("EpiloguePC", config.pcPosition, config.pcEuler);
            morningOffice.SetActive(true);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.35f, .36f, .33f);
            RenderSettings.fog = false;
            CreateLights();
            var ambience = Play(config.morningAmbience, windowPose.position, config.morningVolume, AudioChannel.Ambience, true);
            if (ambience) ambience.loop = true;
            siren = Play(config.sirenSound, windowPose.position + windowPose.forward * 3 - windowPose.right * 8,
                config.sirenVolume, AudioChannel.Ambience, true);
            if (siren)
            {
                siren.minDistance = 5; siren.maxDistance = 40;
                siren.rolloffMode = AudioRolloffMode.Logarithmic; siren.dopplerLevel = .4f;
                siren.loop = true;
            }
        }

        Transform Find(string name)
        {
            foreach (var candidate in morningOffice.GetComponentsInChildren<Transform>(true))
                if (candidate.name == name) return candidate;
            return null;
        }
        Transform Pose(string name, Vector3 position, Vector3 euler)
        {
            var found = Find(name); if (found) return found;
            var pose = new GameObject(name).transform; pose.SetParent(morningOffice.transform, false);
            pose.localPosition = position; pose.localRotation = Quaternion.Euler(euler);
            return pose;
        }
        Vector3 TelevisionPosition()
        {
            var screen = morningOffice ? Find("EpilogueTVScreen") : null;
            return screen ? screen.position : tvPose ? tvPose.position + tvPose.forward * 1.8f : config.morningOrigin;
        }
        void CreateLights()
        {
            lightRoot = new GameObject("Luzes de apresentação — amanhecer");
            var sunlight = Light("Manhã", LightType.Directional, new Color(1, .88f, .72f), .85f);
            sunlight.transform.rotation = Quaternion.Euler(35, -25, 0); sunlight.shadows = LightShadows.Soft;
            var fill = Light("Luz da janela", LightType.Point, new Color(.7f, .8f, 1), .7f);
            fill.transform.position = windowPose.position + Vector3.up * .6f; fill.range = 8;
            red = Light("Giroflex vermelho da rua", LightType.Spot, new Color(1, .025f, .018f), 0);
            blue = Light("Giroflex azul da rua", LightType.Spot, new Color(.025f, .16f, 1), 0);
            red.range = blue.range = 11;
            red.spotAngle = blue.spotAngle = 108;
            var street = windowPose.position + windowPose.forward * 2.5f + Vector3.up * .4f;
            red.transform.position = street + windowPose.right * 1.05f;
            blue.transform.position = street - windowPose.right * 1.05f;
            red.transform.rotation = blue.transform.rotation = Quaternion.LookRotation(-windowPose.forward - Vector3.up * .13f);
            // The window frame and the nearby wall catch the light spilling in from the street.
            redSpill = Light("Giroflex vermelho refletido na janela", LightType.Point, red.color, 0);
            blueSpill = Light("Giroflex azul refletido na janela", LightType.Point, blue.color, 0);
            redSpill.range = blueSpill.range = 4.1f;
            var insideWindow = windowPose.position + windowPose.forward * 1.05f + Vector3.up * .15f;
            redSpill.transform.position = insideWindow + windowPose.right * .65f;
            blueSpill.transform.position = insideWindow - windowPose.right * .65f;
            sirenAge = 0;
        }
        Light Light(string name, LightType type, Color color, float intensity)
        {
            var go = new GameObject(name); go.transform.SetParent(lightRoot.transform, false);
            var lamp = go.AddComponent<Light>(); lamp.type = type; lamp.color = color; lamp.intensity = intensity; lamp.shadows = LightShadows.None;
            return lamp;
        }
        void Update()
        {
            if (!IsRunning || !game || game.State == GameState.Paused || !red || !blue) return;
            float dt = Time.unscaledDeltaTime;
            sirenAge += dt;
            float duration = Mathf.Max(14, config.morningSeconds + 1);
            float envelope = Mathf.Clamp01(sirenAge / .6f) * Mathf.Clamp01((duration - sirenAge) / 2.5f);
            float wave = PresentationSettings.ReduceEffects ? .5f : .5f + .5f * Mathf.Sin(sirenAge * Mathf.PI * 2 * Mathf.Clamp(config.policeSweepHertz, .05f, .5f));
            float power = config.policeLightIntensity * envelope;
            red.intensity = power * (.02f + .98f * Mathf.Pow(wave, 3));
            blue.intensity = power * (.02f + .98f * Mathf.Pow(1 - wave, 3));
            redSpill.intensity = .52f * red.intensity;
            blueSpill.intensity = .52f * blue.intensity;
            if (siren && siren.clip == config.sirenSound)
            {
                siren.transform.position = windowPose.position + windowPose.forward * 3
                    + windowPose.right * Mathf.Lerp(-8, 8, Mathf.Clamp01(sirenAge / duration));
                siren.volume = config.sirenVolume * envelope;
                if (sirenAge > duration) { siren.loop = false; siren.Stop(); siren = null; }
            }
        }

        IEnumerator MoveCamera(Transform target, float seconds)
        {
            Vector3 start = camera.transform.position; Quaternion rotation = camera.transform.rotation;
            float age = 0;
            while (age < seconds)
            {
                if (game.State != GameState.Paused) age += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, age / Mathf.Max(.01f, seconds));
                camera.transform.SetPositionAndRotation(Vector3.Lerp(start, target.position, t), Quaternion.Slerp(rotation, target.rotation, t));
                yield return null;
            }
        }
        IEnumerator Speak(StoryLine line, Vector3 position, bool spatial)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.text)) yield break;
            caption.text = string.IsNullOrEmpty(line.speaker) ? line.text : line.speaker + ": " + line.text;
            var voice = Play(line.voice, position, .6f, AudioChannel.Voice, spatial);
            if (voice && spatial) { voice.minDistance = 1.5f; voice.maxDistance = 12; }
            yield return Wait(line.Duration);
            caption.text = "";
        }
        AudioSource Play(AudioClip clip, Vector3 position, float gain, AudioChannel channel, bool spatial)
        {
            if (!clip) return null;
            var source = AudioManager.Instance?.PlayClip(clip, position, gain, 1, channel, spatial);
            if (source) { sources.Add(source); sourceClips.Add(clip); }
            return source;
        }
        IEnumerator Wait(float seconds)
        {
            while (seconds > 0)
            { if (game && game.State != GameState.Paused) seconds -= Time.unscaledDeltaTime; yield return null; }
        }

        void EnsureReplyPanel(DesktopWindow mail)
        {
            if (replyPanel) return;
            var panel = DesktopUI.Panel(mail.body, "Resposta final", 0, Mathf.Max(0, mail.body.rect.height - 132), mail.body.rect.width, 132);
            panel.anchorMin = panel.anchorMax = new Vector2(0, 0); panel.pivot = Vector2.zero; panel.anchoredPosition = Vector2.zero;
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.97f, .97f, .93f);
            replyPanel = panel.gameObject; replyButtons.Clear();
            DesktopUI.Label(panel, "Responder a Helena", 12, 5, panel.rect.width - 24, 24, 14);
            int count = Mathf.Min(3, replyTexts.Length);
            float width = Mathf.Max(90, (panel.rect.width - 30) / Mathf.Max(1, count));
            for (int i = 0; i < count; i++)
            {
                int index = i;
                replyButtons.Add(DesktopUI.Button(panel, replyTexts[i], 10 + i * width, 34, width - 7, 32, () => SelectReply(index)));
            }
            sentText = DesktopUI.Label(panel, "", 12, 73, panel.rect.width - 24, 23, 14);
            answerText = DesktopUI.Label(panel, "", 12, 100, panel.rect.width - 24, 24, 14);
        }
        public bool SelectReply(int index)
        {
            if (!IsRunning || Stage != "Email" || ReplySelected >= 0 || !computer || !computer.Ready
                || !computer.IsSeated || game.State != GameState.Inspecting || replyTexts == null
                || computer.inbox.State == null || !computer.inbox.State.IsRead(ReplyMessageId)
                || index < 0 || index >= Mathf.Min(3, replyTexts.Length)) return false;
            ReplySelected = index;
            foreach (var button in replyButtons) if (button) button.interactable = false;
            if (sentText) sentText.text = "João: " + replyTexts[index];
            computer.PlaySound("click");
            return true;
        }
        string HelenaReply(int index) => config.helenaReplies != null && index < config.helenaReplies.Length
            && !string.IsNullOrWhiteSpace(config.helenaReplies[index]) ? config.helenaReplies[index] : "Obrigada por responder.";

        static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        TMP_Text Label(Transform parent, string text, float size)
        {
            var label = Rect(parent, "Texto", Vector2.zero, Vector2.one).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = game.hud && game.hud.objective ? game.hud.objective.font : TMP_Settings.defaultFontAsset;
            label.text = text; label.fontSize = size; label.enableAutoSizing = false; label.extraPadding = false;
            label.color = Color.white; label.richText = false; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal; label.alignment = TextAlignmentOptions.Center;
            label.margin = new Vector4(20, 15, 20, 15);
            return label;
        }
        void CreateOverlay()
        {
            overlay = new GameObject("Epílogo — apresentação", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            canvas = overlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 580;
            var scaler = overlay.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            curtain = Rect(overlay.transform, "Preto", Vector2.zero, Vector2.one).gameObject.AddComponent<UnityEngine.UI.Image>();
            curtain.color = Color.clear; curtain.raycastTarget = false;
            var captionRoot = Rect(overlay.transform, "Legendas", new Vector2(.12f, .04f), new Vector2(.88f, .22f));
            caption = Label(captionRoot, "", 24);
            var creditRoot = Rect(overlay.transform, "Créditos", new Vector2(.15f, .1f), new Vector2(.85f, .9f));
            credits = Label(creditRoot, "", 25); credits.gameObject.SetActive(false);

            var noticeRoot = Rect(overlay.transform, "Aviso de novo e-mail", Vector2.one, Vector2.one);
            noticeRoot.pivot = Vector2.one;
            noticeRoot.anchoredPosition = new Vector2(-36, -32);
            noticeRoot.sizeDelta = new Vector2(280, 74);
            var panel = noticeRoot.gameObject.AddComponent<UnityEngine.UI.Image>();
            panel.color = new Color(.065f, .09f, .13f, .98f);
            panel.raycastTarget = false;
            var notice = Label(noticeRoot, "Novo e-mail\nHelena", 22);
            notice.alignment = TextAlignmentOptions.MidlineLeft;
            notice.margin = new Vector4(20, 0, 12, 0);
            mailNotice = noticeRoot.gameObject;
            mailNotice.SetActive(false);
        }
        IEnumerator Fade(float from, float to, float seconds)
        {
            float age = 0;
            while (age < Mathf.Max(.01f, seconds))
            {
                if (game.State != GameState.Paused) age += Time.unscaledDeltaTime;
                curtain.color = new Color(0, 0, 0, Mathf.Lerp(from, to, age / Mathf.Max(.01f, seconds)));
                yield return null;
            }
            curtain.color = new Color(0, 0, 0, to);
        }

        void DisposeMorning()
        {
            for (int i = 0; i < sources.Count; i++)
                if (sources[i] && sources[i].clip == sourceClips[i]) { sources[i].loop = false; sources[i].Stop(); }
            sources.Clear(); sourceClips.Clear(); siren = null;
            if (morningOffice) { morningOffice.SetActive(false); Destroy(morningOffice); }
            morningOffice = null; computer = null; replyPanel = null;
            if (lightRoot) Destroy(lightRoot); lightRoot = null;
            if (finalMail) Destroy(finalMail); finalMail = null;
            if (!restoreCaptured) return;
            RenderSettings.ambientLight = originalAmbient; RenderSettings.ambientMode = originalAmbientMode; RenderSettings.fog = originalFog;
            if (game && game.player) game.player.Warp(schoolPlayerPosition, schoolPlayerHeading, schoolPlayerPitch, false);
            if (camera)
            {
                camera.transform.localPosition = cameraLocalPosition; camera.transform.localRotation = cameraLocalRotation;
                camera.fieldOfView = originalFov; camera.clearFlags = originalClear; camera.backgroundColor = originalBackground;
            }
            if (cameraMotor) cameraMotor.enabled = cameraWasEnabled;
        }
        void Cleanup()
        {
            bool wasRunning = IsRunning;
            if (newsRoutine != null) StopCoroutine(newsRoutine);
            newsRoutine = null;
            if (computer && computer.IsSeated) computer.Close();
            DisposeMorning();
            if (overlay) Destroy(overlay); overlay = null;
            if (ownsControl && game) { ownsControl = false; game.EnableControl(); }
            restoreCaptured = false; IsRunning = false;
            if (wasRunning) Active = false;
        }
        void OnDisable()
        {
            if (subscribedMirror) subscribedMirror.GoodEndingRequested -= Requested;
            subscribedMirror = null;
            if (routine != null) StopCoroutine(routine);
            routine = null; Cleanup();
        }
    }
}
