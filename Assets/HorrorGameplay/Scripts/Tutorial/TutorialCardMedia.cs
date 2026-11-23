// Exibe o modelo, vídeo ou imagem estática do tutorial.

using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

namespace HorrorGame
{
    public sealed class TutorialCardMedia : MonoBehaviour
    {
        public RawImage image;
        public bool Prepared { get; private set; }
        public bool Failed { get; private set; }
        public double PrepareMilliseconds { get; private set; }
        public bool Animated => stage || video && video.clip;
        public bool VideoAllocated => video;
        public bool TextureAllocated => texture;

        InspectionCameraController stage;
        InspectionItemData visual;
        VideoPlayer video;
        RenderTexture texture;
        TutorialCardData data;
        AudioSource videoAudio;
        double started;
        public void Show(TutorialCardData card, bool textOnly)
        {
            Release();
            data = card;
            Prepared = Failed = false;
            PrepareMilliseconds = 0;
            image.texture = card.fallbackImage ? card.fallbackImage : card.image;
            image.gameObject.SetActive(!textOnly);
            if (textOnly)
            {
                Prepared = true;
                return;
            }

            if (card.mediaKind == TutorialMediaKind.Model3D && card.model)
            {
                visual = ScriptableObject.CreateInstance<InspectionItemData>();
                visual.title = card.title;
                visual.model = card.model;
                stage = gameObject.AddComponent<InspectionCameraController>();
                try
                {
                    stage.Open(visual, LayerMask.NameToLayer("Inspect"), new Vector2(.5f, .5f));
                    stage.SetViewportSize(720, 540);
                    stage.Animate(1);
                    image.texture = stage.Texture;
                    Prepared = true;
                }
                catch (System.Exception)
                {
                    Failed = true;
                    ReleaseModel();
                }

                return;
            }

            if (card.mediaKind != TutorialMediaKind.Video || !card.video)
            {
                Prepared = true;
                return;
            }

            started = Time.realtimeSinceStartupAsDouble;
            texture = new RenderTexture(960, 540, 0, RenderTextureFormat.ARGB32)
            {
                name = "Tutorial video (temporary)",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            if (!texture.Create())
            {
                Fallback();
                return;
            }

            video = gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            video.renderMode = VideoRenderMode.RenderTexture;
            video.targetTexture = texture;
            video.clip = card.video;
            video.isLooping = true;
            video.skipOnDrop = true;
            video.waitForFirstFrame = true;
            video.audioOutputMode = VideoAudioOutputMode.None;
            if (card.videoAudio && card.video.audioTrackCount > 0)
            {
                videoAudio = gameObject.AddComponent<AudioSource>();
                videoAudio.playOnAwake = false;
                videoAudio.ignoreListenerPause = true;
                videoAudio.volume = .12f;
                AudioManager.Instance?.Route(videoAudio, AudioChannel.UI);
                video.audioOutputMode = VideoAudioOutputMode.AudioSource;
                video.controlledAudioTrackCount = 1;
                video.SetTargetAudioSource(0, videoAudio);
            }

            video.sendFrameReadyEvents = true;
            video.prepareCompleted += OnPrepared;
            video.frameReady += FirstFrame;
            video.errorReceived += OnError;
            video.Prepare();
        }

        void OnPrepared(VideoPlayer source)
        {
            if (source == video)
                source.Play();
        }

        void FirstFrame(VideoPlayer source, long frame)
        {
            if (source != video || Prepared)
                return;
            Prepared = true;
            PrepareMilliseconds = (Time.realtimeSinceStartupAsDouble - started) * 1000;
            image.texture = texture;
            source.frameReady -= FirstFrame;
        }

        void OnError(VideoPlayer source, string message)
        {
            if (source == video)
                Fallback();
        }

        public void Fallback()
        {
            Failed = true;
            Prepared = true;
            PrepareMilliseconds = started > 0 ? (Time.realtimeSinceStartupAsDouble - started) * 1000 : 0;
            ReleaseVideo();
            if (image)
                image.texture = data ? (data.fallbackImage ? data.fallbackImage : data.image) : null;
        }

        public void Replay()
        {
            if (stage)
            {
                ReleaseModel();
                Show(data, false);
            }
            else if (video && Prepared)
            {
                video.time = 0;
                video.Play();
            }
            else if (data && data.mediaKind == TutorialMediaKind.Video && data.video)
                Show(data, false);
        }

        void Update()
        {
            if (stage)
            {
                stage.Drag(new Vector2(Time.unscaledDeltaTime * (PresentationSettings.ReduceEffects ? 12 : 64), 0));
                stage.Advance(Time.unscaledDeltaTime, true);
            }

            if (video && !Prepared && Time.realtimeSinceStartupAsDouble - started > 8)
                Fallback();
        }

        void ReleaseModel()
        {
            if (stage)
            {
                stage.Close();
                Destroy(stage);
                stage = null;
            }

            if (visual)
            {
                Destroy(visual);
                visual = null;
            }
        }

        void ReleaseVideo()
        {
            var old = video;
            video = null;
            if (old)
            {
                old.prepareCompleted -= OnPrepared;
                old.frameReady -= FirstFrame;
                old.errorReceived -= OnError;
                old.Stop();
                old.targetTexture = null;
                Destroy(old);
            }

            if (texture)
            {
                texture.Release();
                Destroy(texture);
                texture = null;
            }

            if (videoAudio)
            {
                Destroy(videoAudio);
                videoAudio = null;
            }
        }

        public void Release()
        {
            ReleaseModel();
            ReleaseVideo();
            if (image)
                image.texture = null;
            Prepared = false;
        }

        void OnDisable() => Release();
        void OnDestroy() => Release();
    }
}
