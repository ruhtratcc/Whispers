// Calcula quando e como a resposta das crianças é reproduzida.

using UnityEngine;

namespace HorrorGame
{
    // One fixed source per tap: it never follows the player or an apparition.
    [DisallowMultipleComponent]
    public sealed class WhistleChildAnswers : MonoBehaviour
    {
        WhistleSystem owner;
        BeforeDemolitionFourStudentsFlow students;
        BeforeDemolitionTwoVoicesFlow corridor;
        ObjectiveService objectives;
        WhistleHighlightable[] highlights;
        AudioSource source;
        AudioLowPassFilter lowPass;
        AudioReverbFilter echo;
        readonly RaycastHit[] hits = new RaycastHit[32];
        float wait = -1, remaining, barkDelay = -1, rareRemaining;
        string chapter;
        int serial, child;
        Vector3 origin, target;
        bool empty;
        public float PendingDelay => wait;
        public Vector3 AnswerPosition => target;
        public int ChildIndex => child;
        public AudioSource AnswerSource => source;

        void Awake()
        {
            owner = GetComponent<WhistleSystem>();
            students = GetComponentInChildren<BeforeDemolitionFourStudentsFlow>(true);
            corridor = GetComponentInChildren<BeforeDemolitionTwoVoicesFlow>(true);
            objectives = GetComponent<ObjectiveService>();
            var go = new GameObject("Apito infantil — fonte fixa");
            go.transform.SetParent(transform, false);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1;
            source.dopplerLevel = 0;
            source.minDistance = 2;
            source.rolloffMode = AudioRolloffMode.Linear;
            lowPass = go.AddComponent<AudioLowPassFilter>();
            lowPass.lowpassResonanceQ = 1;
            echo = go.AddComponent<AudioReverbFilter>();
            echo.reverbPreset = AudioReverbPreset.Room;
            echo.decayTime = .35f;
            echo.enabled = false;
        }

        void Start() => highlights = FindObjectsByType<WhistleHighlightable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        void OnEnable()
        {
            if (owner)
                owner.Blown += Heard;
        }

        void OnDisable()
        {
            if (owner)
                owner.Blown -= Heard;
            Cancel();
        }

        void OnDestroy()
        {
            if (source)
                Destroy(source.gameObject);
        }

        public void Cancel()
        {
            wait = barkDelay = -1;
            remaining = 0;
            if (source)
                source.Stop();
        }

        public static bool FacesQuietSide(Vector3 direction, Vector3 quiet, Vector3 voice) => quiet.sqrMagnitude > .001f && Vector3.Dot(direction.normalized, quiet.normalized) > .35f && (voice.sqrMagnitude < .001f || Vector3.Dot(direction.normalized, quiet.normalized) > Vector3.Dot(direction.normalized, voice.normalized) + .05f);
        public static float Delay(WhistleConfig config, float distance, bool blocked, float tension)
        {
            float near = Mathf.Max(.05f, Mathf.Min(config.childAnswerDelay.x, config.childAnswerDelay.y));
            float far = Mathf.Max(near, Mathf.Max(config.childAnswerDelay.x, config.childAnswerDelay.y));
            return Mathf.Lerp(near, far, Mathf.Clamp01(distance / Mathf.Max(1, config.childAnswerRange))) + (blocked ? .12f : 0) + Mathf.Clamp(tension, 0, 100) * .07f;
        }

        public static float Pitch(int index) => index == 0 ? 1.14f : index == 1 ? 1.27f : index == 2 ? 1.08f : 1.2f;
        public bool TryTarget(WhistleContext context, out Vector3 point, out int student, out bool silent)
        {
            point = default;
            student = 0;
            silent = false;
            if (!owner || !owner.SupernaturalActive || context.kind != WhistleKind.Tap)
                return false;
            if (owner.Chapter == BeforeDemolitionTwoVoicesFlow.ChapterId && corridor && corridor.IsActive)
            {
                if (corridor.Escaped || !corridor.quietAnchor)
                {
                    silent = true;
                    return false;
                }

                Vector3 quiet = corridor.quietAnchor.position - context.position;
                Vector3 voice = corridor.voiceAnchor ? corridor.voiceAnchor.position - context.position : -quiet;
                if (!FacesQuietSide(context.direction, quiet, voice))
                {
                    silent = true;
                    return false;
                }

                point = corridor.quietAnchor.position;
                student = 2;
                return InRange(context.position, point);
            }

            if (owner.Chapter == BeforeDemolitionFourStudentsFlow.ChapterId && students)
                for (int i = 0; i < BeforeDemolitionFourStudentsFlow.StudentItemIds.Length; i++)
                    if (owner.Inventory && !owner.Inventory.Contains(BeforeDemolitionFourStudentsFlow.StudentItemIds[i]))
                    {
                        student = i;
                        var item = i < students.studentVisuals.Length ? students.studentVisuals[i] : null;
                        if (!item)
                            return false;
                        point = item.transform.position + Vector3.up * .3f;
                        return InRange(context.position, point);
                    }

            float nearest = float.MaxValue;
            if (highlights != null)
                foreach (var h in highlights)
                {
                    if (!h || h.gameObject.scene != gameObject.scene || !h.Eligible(context) || h.GetComponentInParent<InteractiveDoor>())
                        continue;
                    bool figure = false;
                    if (students)
                        foreach (var visual in students.appearanceVisuals)
                            if (visual && h.transform.IsChildOf(visual.transform))
                            {
                                figure = true;
                                break;
                            }

                    if (figure)
                        continue;
                    var p = h.FocusRenderer ? h.FocusRenderer.bounds.center : h.transform.position;
                    float d = (p - context.position).sqrMagnitude;
                    // An objective may point at a figure: use it to rank eligible objects, never as a voice emitter.
                    float score = objectives && objectives.HasTarget ? (p - objectives.TargetPosition).sqrMagnitude * 4 + d * .2f : d;
                    if (score < nearest && InRange(context.position, p))
                    {
                        nearest = score;
                        point = p;
                    }
                }

            if (nearest < float.MaxValue)
                return true;
            foreach (var r in WhistleResponder.Active)
                if (r && r.gameObject.scene == gameObject.scene && r.role == WhistleResponseRole.Echo && r.CanHear(context))
                {
                    float d = (r.transform.position - context.position).sqrMagnitude;
                    if (d < nearest && InRange(context.position, r.transform.position))
                    {
                        nearest = d;
                        point = r.transform.position;
                    }
                }

            return nearest < float.MaxValue;
        }

        bool InRange(Vector3 from, Vector3 to) => (to - from).sqrMagnitude <= owner.config.childAnswerRange * owner.config.childAnswerRange;
        bool Occluded(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            int n = distance > .15f ? Physics.RaycastNonAlloc(from, delta / distance, hits, distance - .12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) : 0;
            for (int i = 0; i < n; i++)
                if (!owner.game.player || !hits[i].transform.IsChildOf(owner.game.player.transform))
                    return true;
            return n == hits.Length;
        }

        void Heard(WhistleContext context)
        {
            if (!owner.SupernaturalActive || context.kind != WhistleKind.Tap || wait >= 0 || remaining > 0)
                return;
            bool found = TryTarget(context, out target, out child, out bool silent);
            if (silent)
                return;
            empty = !found;
            origin = context.position;
            if (empty)
                target = origin - context.direction.normalized * 4;
            chapter = owner.Chapter;
            serial = context.serial;
            wait = Delay(owner.config, Vector3.Distance(origin, target), Occluded(origin, target), owner.State.tension);
        }

        void Update()
        {
            if (!owner || !owner.game)
                return;
            if (owner.game.State == GameState.Paused)
            {
                if (source.isPlaying)
                    source.Pause();
                return;
            }

            if (!owner.SupernaturalActive || !owner.game.CanControl || owner.game.IsBlackingOut || owner.Chapter != chapter || owner.State.totalBlows < serial)
            {
                Cancel();
                return;
            }

            if (remaining > 0)
            {
                source.UnPause();
                remaining -= Time.deltaTime;
                if (remaining <= 0)
                    source.Stop();
            }

            rareRemaining = Mathf.Max(0, rareRemaining - Time.deltaTime);
            if (wait >= 0)
            {
                wait -= Time.deltaTime;
                if (wait <= 0)
                {
                    wait = -1;
                    PlayAnswer();
                }
            }

            if (barkDelay >= 0)
            {
                barkDelay -= Time.deltaTime;
                if (barkDelay <= 0)
                {
                    barkDelay = -1;
                    if (rareRemaining <= 0 && owner.Speak("child.answer"))
                    {
                        rareRemaining = 70;
                        owner.MarkResponded("child.answer.heard");
                    }
                }
            }
        }

        void PlayAnswer()
        {
            var c = owner.config;
            AudioClip clip = empty ? c.emptyEchoClip : c.childAnswerClips != null && c.childAnswerClips.Length > child ? c.childAnswerClips[child] : null;
            // Missing recordings stay silent; never generate a replacement tone at runtime.
            if (!clip)
                return;
            bool blocked = Occluded(owner.Eye ? owner.Eye.transform.position : origin, target);
            source.transform.position = target;
            source.clip = clip;
            source.pitch = empty ? .85f : Pitch(child);
            source.outputAudioMixerGroup = AudioManager.Instance ? AudioManager.Instance.sfxGroup : null;
            source.maxDistance = Mathf.Max(8, c.childAnswerRange + 8);
            float distance = Vector3.Distance(origin, target);
            source.volume = (empty ? c.emptyEchoVolume : c.childAnswerVolume) * Mathf.Lerp(1, .72f, Mathf.Clamp01(distance / c.childAnswerRange)) * (blocked ? .68f : 1);
            lowPass.cutoffFrequency = blocked ? Mathf.Lerp(3800, 1800, Mathf.Clamp01(distance / c.childAnswerRange)) : Mathf.Lerp(18000, 6500, Mathf.Clamp01(distance / c.childAnswerRange));
            echo.enabled = empty;
            source.Play();
            remaining = Mathf.Min(empty ? .45f : c.responseMaxSeconds, clip.length / Mathf.Max(.1f, source.pitch));
            if (owner.game.hud && (!owner.game.hud.SubtitleVisible || owner.game.hud.subtitle && owner.game.hud.subtitle.text.StartsWith("[") && !owner.game.hud.subtitle.text.Contains("\n")))
                owner.game.hud.ShowSubtitle(empty ? "[eco vazio]" : "[apito ao longe]", 1.5f);
            if (!empty && (serial % 4 == 1 || !owner.HasResponded("child.answer.heard")))
                barkDelay = 1.7f;
        }
    }
}
