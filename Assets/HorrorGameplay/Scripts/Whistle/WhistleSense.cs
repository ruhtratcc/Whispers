// Seleciona as pistas e os objetos elegíveis para o pulso.

using UnityEngine;

namespace HorrorGame
{
    [DefaultExecutionOrder(260)]
    public sealed class WhistleSense : MonoBehaviour
    {
        public WhistleSystem owner;
        public WhistleTrailRoute[] trails = System.Array.Empty<WhistleTrailRoute>();
        readonly Collider[] overlaps = new Collider[512];
        readonly RaycastHit[] walls = new RaycastHit[32];
        readonly WhistleHighlightable[] active = new WhistleHighlightable[512];
        int count;
        public int LastOverlapCount { get; private set; }
        public bool BufferSaturated { get; private set; }
        public int ActiveCount => count;

        public static bool OnPlayerFloor(WhistleSystem system, Vector3 point) => !system || !system.game || !system.game.player || system.game.gameObject.scene.name != "SchoolCEEP" || system.config.SameHighlightFloor(system.game.player.transform.position.y, point.y);
        void Awake()
        {
            if (!owner)
                owner = GetComponent<WhistleSystem>();
        }

        public void Pulse(WhistleContext context)
        {
            Clear();
            if (!owner || !owner.SupernaturalActive)
                return;
            LastOverlapCount = Physics.OverlapSphereNonAlloc(context.position, owner.config.range, overlaps, owner.config.highlightMask, QueryTriggerInteraction.Collide);
            BufferSaturated = LastOverlapCount == overlaps.Length;
            int limit = Mathf.Min(active.Length, owner.config.maxHighlights);
            for (int i = 0; i < LastOverlapCount && count < limit; i++)
            {
                var h = overlaps[i] ? overlaps[i].GetComponentInParent<WhistleHighlightable>() : null;
                if (!h || !h.Eligible(context))
                    continue;
                bool duplicate = false;
                for (int j = 0; j < count; j++)
                    if (active[j] == h)
                    {
                        duplicate = true;
                        break;
                    }

                if (duplicate)
                    continue;
                Vector3 point = h.transform.position;
                if (h.FocusRenderer)
                    point = h.FocusRenderer.bounds.center;
                Vector3 delta = point - context.position;
                float distance = delta.magnitude;
                bool blocked = false;
                int hits = distance > .05f ? Physics.RaycastNonAlloc(context.position, delta / distance, walls, distance - .04f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) : 0;
                for (int j = 0; j < hits; j++)
                {
                    var t = walls[j].transform;
                    if (t.IsChildOf(owner.game.player.transform) || t.IsChildOf(h.transform) || h.transform.IsChildOf(t))
                        continue;
                    bool own = false;
                    for (int k = 0; k < h.targets.Length; k++)
                        if (h.targets[k] && (t == h.targets[k].transform || t.IsChildOf(h.targets[k].transform)))
                        {
                            own = true;
                            break;
                        }

                    if (!own)
                    {
                        blocked = true;
                        break;
                    }
                }

                if (h.Reveal(context, blocked))
                {
                    active[count++] = h;
                    h.Tick(0);
                }
            }

            WhistleTrailRoute best = null;
            float nearest = float.MaxValue;
            for (int i = 0; i < trails.Length; i++)
            {
                var route = trails[i];
                if (!route || !route.Eligible(context))
                    continue;
                float d = (route.transform.position - context.position).sqrMagnitude;
                if (d < nearest)
                {
                    nearest = d;
                    best = route;
                }
            }

            if (best)
                best.Show(owner);
        }

        void Update()
        {
            if (!owner || owner.game.State == GameState.Paused)
                return;
            if (!owner.CanUse)
            {
                Clear();
                return;
            }

            for (int i = 0; i < count; i++)
                if (active[i])
                    active[i].Tick(Time.deltaTime);
            for (int i = 0; i < trails.Length; i++)
                if (trails[i])
                    trails[i].Tick(Time.deltaTime);
        }

        public void Clear()
        {
            for (int i = 0; i < count; i++)
            {
                if (active[i])
                    active[i].Clear();
                active[i] = null;
            }

            count = 0;
            for (int i = 0; i < trails.Length; i++)
                if (trails[i])
                    trails[i].Clear();
        }

        void OnDisable() => Clear();
    }
}
