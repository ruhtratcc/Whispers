// Marca um objeto que pode receber o destaque do apito.

using UnityEngine;

namespace HorrorGame
{
    public enum WhistleCategory
    {
        Useful,
        Story,
        Path
    }

    [DisallowMultipleComponent]
    public sealed class WhistleHighlightable : MonoBehaviour
    {
        public string highlightId, requiredFlag, hiddenWhenItemOwned, disabledAfterFlag;
        public string[] chapters = System.Array.Empty<string>();
        public WhistleCategory category;
        public float range;
        public bool onceOnly, allowThroughWall = true;
        public MonoBehaviour[] interactions = System.Array.Empty<MonoBehaviour>();
        bool InteractionAvailable(WhistleSystem system)
        {
            if (interactions.Length == 0)
                return true;
            for (int i = 0; i < interactions.Length; i++)
                if (interactions[i] && interactions[i].isActiveAndEnabled && interactions[i] is IInteractable item && item.CanInteract(system.game.interaction))
                    return true;
            return false;
        }

        public Material outlineMaterial, moteMaterial;
        public Mesh moteMesh;
        MeshRenderer[] motes = System.Array.Empty<MeshRenderer>();
        MaterialPropertyBlock moteProperties;
        public InteractiveDrawer concealingDrawer;
        public Renderer[] concealedTargets = System.Array.Empty<Renderer>();
        Renderer[] sources;
        // Interactive contents now remain visible to x-ray even inside a closed drawer.
        public bool ContentsConcealed => interactions.Length == 0 && concealingDrawer && (concealingDrawer.OpenFraction < .6f || targets.Length == 0 || !targets[0] || !targets[0].gameObject.activeInHierarchy);
        public Renderer FocusRenderer => ContentsConcealed && concealedTargets.Length > 0 ? concealedTargets[0] : targets.Length > 0 ? targets[0] : null;

        public Renderer[] targets = System.Array.Empty<Renderer>();
        public WhistleResponder directionGate;
        MeshRenderer[] outlines;
        MaterialPropertyBlock properties;
        float age, duration;
        bool used;
        WhistleSystem owner;
        public bool Visible => age > 0 && age <= duration;
        public bool ThroughWall { get; private set; }

        public bool Eligible(WhistleContext context)
        {
            var s = context.system;
            if (!s || !s.SupernaturalActive || !isActiveAndEnabled || onceOnly && (used || s.HasResponded("highlight." + highlightId)))
                return false;
            if (!WhistleSense.OnPlayerFloor(s, FocusRenderer ? FocusRenderer.bounds.center : transform.position))
                return false;
            if (chapters.Length > 0 && System.Array.IndexOf(chapters, s.Chapter) < 0)
                return false;
            if (!string.IsNullOrEmpty(requiredFlag) && !s.HasFlag(requiredFlag))
                return false;
            if (!string.IsNullOrEmpty(disabledAfterFlag) && s.HasFlag(disabledAfterFlag))
                return false;
            if (!string.IsNullOrEmpty(hiddenWhenItemOwned) && s.Inventory.Contains(hiddenWhenItemOwned))
                return false;
            if (directionGate && !directionGate.CanHear(context) || !InteractionAvailable(s))
                return false;
            float reach = range > 0 ? range : s.config.range;
            return (transform.position - context.position).sqrMagnitude <= reach * reach;
        }

        void Awake()
        {
            using (LoadingMetrics.Measure("WhistleHighlight.Awake"))
            {
                if (GetComponent<InteractiveDoor>() || GetComponent<LockableDoor>() || GetComponent<SchoolAreaRestriction>())
                {
                    enabled = false;
                    return;
                }

                properties = new MaterialPropertyBlock();
                sources = new Renderer[targets.Length + concealedTargets.Length];
                System.Array.Copy(targets, sources, targets.Length);
                System.Array.Copy(concealedTargets, 0, sources, targets.Length, concealedTargets.Length);
                outlines = new MeshRenderer[sources.Length];
                for (int i = 0; i < sources.Length; i++)
                {
                    var r = sources[i];
                    if (!r)
                        continue;
                    var filter = r.GetComponent<MeshFilter>();
                    if (!filter || !filter.sharedMesh)
                        continue;
                    var go = new GameObject("Apito — contorno em cache");
                    go.layer = r.gameObject.layer;
                    go.transform.SetParent(r.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    var shell = go.AddComponent<MeshRenderer>();
                    shell.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    shell.receiveShadows = false;
                    shell.allowOcclusionWhenDynamic = false;
                    var mats = new Material[filter.sharedMesh.subMeshCount];
                    for (int m = 0; m < mats.Length; m++)
                        mats[m] = outlineMaterial;
                    shell.sharedMaterials = mats;
                    shell.enabled = false;
                    outlines[i] = shell;
                }

                if (category == WhistleCategory.Story && moteMaterial && moteMesh)
                {
                    motes = new MeshRenderer[3];
                    moteProperties = new MaterialPropertyBlock();
                    for (int i = 0; i < 3; i++)
                    {
                        var dust = new GameObject("Apito — partícula mínima em cache");
                        dust.layer = gameObject.layer;
                        dust.transform.SetParent(transform, false);
                        dust.AddComponent<MeshFilter>().sharedMesh = moteMesh;
                        var r = dust.AddComponent<MeshRenderer>();
                        r.sharedMaterial = moteMaterial;
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.enabled = false;
                        motes[i] = r;
                    }
                }
            }
        }

        public bool Reveal(WhistleContext context, bool occluded)
        {
            if (!Eligible(context))
                return false;
            var s = context.system;
            ThroughWall = occluded && allowThroughWall;
            if (occluded && !ThroughWall)
                return false;
            owner = s;
            duration = s.config.effectDuration;
            age = duration + Mathf.Clamp01(Vector3.Distance(context.position, transform.position) / Mathf.Max(1, s.config.range)) * s.config.pulseExpansionSeconds;
            used = true;
            if (onceOnly)
                s.MarkResponded("highlight." + highlightId);
            return true;
        }

        public void Tick(float dt)
        {
            if (age <= 0)
                return;
            if (!owner || !owner.SupernaturalActive || !WhistleSense.OnPlayerFloor(owner, FocusRenderer ? FocusRenderer.bounds.center : transform.position) || !InteractionAvailable(owner) || !string.IsNullOrEmpty(hiddenWhenItemOwned) && owner.Inventory.Contains(hiddenWhenItemOwned) || !string.IsNullOrEmpty(disabledAfterFlag) && owner.HasFlag(disabledAfterFlag))
            {
                Clear();
                return;
            }

            age = Mathf.Max(0, age - dt);
            float fade = Mathf.Clamp01((duration - age) / .6f) * Mathf.Clamp01(age / 2f);
            var c = owner.config;
            Color colour = category == WhistleCategory.Useful ? c.usefulColor : category == WhistleCategory.Story ? c.storyColor : c.pathColor;
            properties.SetColor("_SenseColor", colour);
            properties.SetFloat("_SenseAlpha", fade * (PresentationSettings.ReduceEffects ? .32f : c.highlightStrength));
            properties.SetFloat("_OutlineWidth", c.outlineWidth);
            properties.SetFloat("_Pattern", (float)category);
            properties.SetFloat("_ThroughWall", allowThroughWall ? c.throughWallStrength : 0);
            for (int i = 0; i < motes.Length; i++)
                if (motes[i])
                {
                    bool show = Visible && !ThroughWall && !ContentsConcealed && !PresentationSettings.ReduceEffects;
                    motes[i].enabled = show;
                    if (show && owner.Eye)
                    {
                        var focus = FocusRenderer ? FocusRenderer.bounds.center : transform.position;
                        motes[i].transform.position = focus + Vector3.up * (.03f + i * .025f) + owner.Eye.transform.right * (Mathf.Sin(age + i * 2) * .035f);
                        motes[i].transform.rotation = owner.Eye.transform.rotation;
                        var scale = transform.lossyScale;
                        motes[i].transform.localScale = new Vector3(.008f / Mathf.Max(.001f, Mathf.Abs(scale.x)), .008f / Mathf.Max(.001f, Mathf.Abs(scale.y)), .008f / Mathf.Max(.001f, Mathf.Abs(scale.z)));
                        moteProperties.SetColor("_SenseColor", c.storyColor);
                        moteProperties.SetFloat("_SenseAlpha", fade * .2f);
                        motes[i].SetPropertyBlock(moteProperties);
                    }
                }

            if (outlines != null)
                for (int i = 0; i < outlines.Length; i++)
                    if (outlines[i])
                    {
                        if (!outlines[i].sharedMaterial)
                            outlines[i].sharedMaterial = c.highlightMaterial;
                        outlines[i].SetPropertyBlock(properties);
                        bool selected = concealingDrawer ? (ContentsConcealed ? i >= targets.Length : i < targets.Length) : true;
                        outlines[i].enabled = selected && Visible && sources[i] && sources[i].enabled && sources[i].gameObject.activeInHierarchy;
                    }
        }

        public void Clear()
        {
            age = 0;
            for (int i = 0; i < motes.Length; i++)
                if (motes[i])
                    motes[i].enabled = false;
            if (outlines != null)
                for (int i = 0; i < outlines.Length; i++)
                    if (outlines[i])
                        outlines[i].enabled = false;
        }

        void OnDisable() => Clear();
    }
}
