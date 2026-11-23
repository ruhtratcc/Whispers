// Coordena a descoberta da mecânica do apito na escola.

using UnityEngine;

namespace HorrorGame
{
    public sealed class WhistleSchoolDiscovery : MonoBehaviour
    {
        public WhistleSystem owner;
        public BoxCollider secretaryVolume, libraryExitVolume;
        public Transform whisperAnchor;
        public Transform[] answerAnchors = System.Array.Empty<Transform>();
        int step = -1;
        float wait;
        void OnEnable()
        {
            if (owner)
                owner.Discovery += Begin;
        }

        void Begin()
        {
            step = 0;
            wait = .45f;
            Vector3 forward = owner.Eye ? Vector3.ProjectOnPlane(owner.Eye.transform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            for (int i = 0; i < answerAnchors.Length; i++)
                if (answerAnchors[i])
                    answerAnchors[i].position = owner.PulseOrigin + right * (i % 2 == 0 ? -3.2f : 3.2f) + forward * (i < 2 ? -2.2f : 2.2f) - Vector3.up * .4f;
        }

        void Update()
        {
            if (!owner || !owner.CanUse)
                return;
            if (!owner.State.secretaryWhispered && owner.Chapter == BeforeDemolitionCampaign.SchoolChapterId && secretaryVolume && secretaryVolume.bounds.Contains(owner.game.player.transform.position + Vector3.up * .8f))
            {
                owner.State.secretaryWhispered = true;
                owner.SpeakAt("child.call", whisperAnchor ? whisperAnchor.position : secretaryVolume.bounds.center);
                owner.game.narrative.ScheduleSave();
            }

            if (step >= 0 && !owner.SupernaturalActive)
                step = -1;
            if (step < 0)
                return;
            wait -= Time.deltaTime;
            if (wait > 0)
                return;
            if (step < 4)
            {
                string trigger = "child.discovery." + step;
                Vector3 position = step < answerAnchors.Length && answerAnchors[step] ? answerAnchors[step].position : transform.position;
                var line = owner.config ? owner.config.dialogue?.Find(trigger, owner.State.totalBlows) : null;
                bool spoken = owner.SpeakAt(trigger, position);
                wait = spoken && line?.voice ? Mathf.Max(.85f, line.voice.length + .08f) : .85f;
                step++;
            }
            else
            {
                owner.Speak("discovered", true);
                step = -1;
            }
        }

        void OnDisable()
        {
            if (owner)
                owner.Discovery -= Begin;
            step = -1;
        }
    }
}
