// Integra o apito às regras de investigação da campanha.

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorGame
{
    [DisallowMultipleComponent]
    public sealed class BeforeDemolitionWhistle : MonoBehaviour
    {
        public const string MirrorChapterId = "bd.mirror-report";
        public GameSession game;
        public WhistleSystem system;
        public Transform echoAnchor;
        public bool enableEcho;
        public event Action Blown;
        public bool CanUse => system ? system.CanUse : game && game.player && game.CanControl && game.narrative && game.narrative.IsInitialized
            && BeforeDemolitionCampaign.Is(game.narrative.book) && RelevantChapter
            && game.interaction && game.interaction.inventory
            && game.interaction.inventory.Contains(BeforeDemolitionCampaign.WhistleItemId);
        bool RelevantChapter => game && game.narrative && (game.narrative.Progress.chapterId == BeforeDemolitionTwoVoicesFlow.ChapterId
            || game.narrative.Progress.chapterId == MirrorChapterId);
        public AudioClip clip;
        AudioSource primary, echo;
        float cooldown, echoDelay = -1;
        Vector3 pendingEchoPosition;

        void Awake() { if (!game) game = GetComponentInParent<GameSession>(); }

        void Update()
        {
            if(system)return;
            if (!RelevantChapter || !game.narrative.IsInitialized || game.IsBlackingOut
                || game.State == GameState.StoryScreen || game.State == GameState.GameOver) { CancelPending(); return; }
            if (game.State == GameState.Paused) return;
            cooldown = Mathf.Max(0, cooldown - Time.deltaTime);
            if (echoDelay >= 0)
            {
                echoDelay -= Time.deltaTime;
                if (echoDelay <= 0)
                {
                    echoDelay = -1;
                    echo = AudioManager.Instance?.PlayClip(clip, pendingEchoPosition, .10f, .97f, AudioChannel.SFX, true);
                }
            }
            if (CanUse && Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) Blow();
        }

        public bool Blow()
        {
            if(system)return system.BlowTap();
            if (!CanUse || cooldown > 0) return false;
            if(!clip){var cue=AudioManager.Instance&&AudioManager.Instance.cues?AudioManager.Instance.cues.Find("whistle"):null;clip=cue?.sound?cue.sound.Next():null;}
            cooldown = 1.2f;
            primary = AudioManager.Instance?.PlayClip(clip, game.player.transform.position, .18f, 1, AudioChannel.SFX, false);
            var camera = game.player && game.player.view ? game.player.view.playerCamera : null;
            if (enableEcho && echoAnchor && camera)
            {
                Vector3 direction = echoAnchor.position - camera.transform.position;
                direction.y = 0;
                Vector3 forward = camera.transform.forward; forward.y = 0;
                if (direction.sqrMagnitude > .01f && forward.sqrMagnitude > .01f
                    && Vector3.Dot(direction.normalized, forward.normalized) >= .64f)
                { pendingEchoPosition = echoAnchor.position; echoDelay = .55f; }
            }
            Blown?.Invoke(); return true;
        }

        public void CancelPending()
        {
            if(system)system.Cancel();
            echoDelay = -1; cooldown = 0;
            if (primary && primary.clip == clip) primary.Stop();
            if (echo && echo.clip == clip) echo.Stop();
            primary = echo = null;
        }
        void OnDisable() { CancelPending(); }
        void OnDestroy() { CancelPending(); }
    }
}
