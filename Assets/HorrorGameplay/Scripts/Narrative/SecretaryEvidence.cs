// Controla a análise das pegadas na secretaria.

using System;
using System.Collections.Generic;
using UnityEngine;
namespace HorrorGame
{
    /// <summary>Secretary evidence only. The global whistle lore/discovery and save schema stay unchanged.</summary>
    public sealed class SecretaryEvidence : MonoBehaviour
    {
        public const string FirstTapMarker = "secretary.traces.first-tap";
        public BeforeDemolitionSchoolFlow flow;
        public Renderer[] footprints = Array.Empty<Renderer>();
        public Renderer keyEcho;
        public Transform clock, footprintLookPoint;
        public Vector3 clockFaceCenter;
        public string category = "rastros das crianças";
        public float pulseSeconds = 12;
        public bool PulseVisible => remaining > 0;
        public bool KeyHighlighted => keyEcho && keyEcho.enabled;
        WhistleSystem whistle;
        ContextBarks barks;
        InspectionManager inspection;
        MaterialPropertyBlock key;
        Material[][] originalTracks, pulseTracks;
        readonly HashSet<string> queued = new HashSet<string>();
        float remaining;
        string drawingReaction;
        bool sawTracks, sawClock;
        void Start()
        {
            if (!flow) return;
            whistle = flow.GetComponent<WhistleSystem>(); barks = flow.GetComponent<ContextBarks>();
            inspection = InspectionManager.Instance;
            key = new MaterialPropertyBlock();
            originalTracks = new Material[footprints.Length][]; pulseTracks = new Material[footprints.Length][];
            for (int i = 0; i < footprints.Length; i++)
            {
                var r = footprints[i]; if (!r || !r.sharedMaterial) continue;
                originalTracks[i] = r.sharedMaterials;
                pulseTracks[i] = new Material[originalTracks[i].Length];
                for (int j=0;j<pulseTracks[i].Length;j++)
                { pulseTracks[i][j] = new Material(originalTracks[i][j]) { name="Rastro — pulso temporário" }; pulseTracks[i][j].EnableKeyword("_EMISSION"); }
                r.SetPropertyBlock(null); r.sharedMaterials = pulseTracks[i];
            }
            if (whistle) whistle.Blown += OnWhistle;
            if (inspection) inspection.PageViewed += OnPage;
            Clear();
        }
        void OnWhistle(WhistleContext context)
        {
            if (!flow || !flow.IsActive || context.kind != WhistleKind.Tap || !whistle || whistle.HasResponded(FirstTapMarker)) return;
            whistle.MarkResponded(FirstTapMarker);
            remaining = pulseSeconds;
            RefreshPulse();
        }
        void OnPage(string id, int index)
        {
            if (id != "bd.lunchbox" || !inspection || !inspection.CurrentItem || !inspection.CurrentItem.pages[index].currentDate) return;
            QueueVariant("secretary.today", true);
        }
        void QueueVariant(string trigger, bool afterReading = false)
        {
            if (!barks || !whistle || afterReading && drawingReaction != null) return;
            for (int i = 0; i < 2; i++)
            {
                string variant = trigger + "." + i;
                if (!whistle.HasResponded("bark." + variant) && !queued.Contains(variant))
                {
                    if (afterReading) { drawingReaction = variant; queued.Add(variant); }
                    else if (barks.Enqueue(variant)) queued.Add(variant);
                    return;
                }
            }
        }
        bool LookingAt(Vector3 point, float reach)
        {
            var eye = whistle ? whistle.Eye : null;
            if (!eye || Vector3.Distance(eye.transform.position, point) > reach) return false;
            Vector3 viewport = eye.WorldToViewportPoint(point);
            if (viewport.z <= 0 || Mathf.Abs(viewport.x - .5f) > .16f || Mathf.Abs(viewport.y - .5f) > .2f) return false;
            Vector3 direction = point - eye.transform.position;
            // End just before the evidence surface: its own collider must not count as a wall.
            return !Physics.Raycast(eye.transform.position, direction.normalized, direction.magnitude - .12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
        void Update()
        {
            if (!flow || !flow.IsActive) { if (remaining > 0) Clear(); return; }
            if (remaining > 0) { remaining = Mathf.Max(0, remaining - Time.deltaTime); RefreshPulse(); }
            if (!flow.game.CanControl || !whistle) return;
            // The dated drawing gets a prompt reaction after closing its reader, not a late ambient hint.
            if (drawingReaction != null && whistle.Speak(drawingReaction))
            { whistle.MarkResponded("bark." + drawingReaction); drawingReaction = null; }
            bool tracks = footprints.Length > 0 && footprints[0] && LookingAt(footprintLookPoint ? footprintLookPoint.position : footprints[0].bounds.center, 5);
            bool time = clock && LookingAt(clock.TransformPoint(clockFaceCenter), 6);
            if (tracks && !sawTracks) QueueVariant("secretary.footprints");
            if (time && !sawClock) QueueVariant("secretary.clock");
            sawTracks = tracks; sawClock = time;
        }
        void RefreshPulse()
        {
            float strength = Mathf.Clamp01(remaining / 2f);
            if (pulseTracks != null) for (int i = 0; i < pulseTracks.Length; i++)
            {
                if (pulseTracks[i] == null) continue;
                for (int j=0;j<pulseTracks[i].Length;j++)
                {
                    var m=pulseTracks[i][j]; Color faded=originalTracks[i][j].GetColor("_BaseColor");
                    m.SetColor("_EmissionColor",new Color(.09f,.09f,.085f)*strength);
                    m.SetColor("_BaseColor",Color.Lerp(faded,new Color(.7f,.69f,.64f,faded.a),strength));
                }
            }
            if (keyEcho)
            {
                keyEcho.enabled = strength > 0 && !whistle.Inventory.Contains(BeforeDemolitionSchoolFlow.KeyId);
                key.SetColor("_SenseColor", new Color(1,.68f,.25f)); key.SetFloat("_SenseAlpha", strength * (PresentationSettings.ReduceEffects ? .25f : .4f));
                key.SetFloat("_OutlineWidth", .008f); key.SetFloat("_Pattern", 0); key.SetFloat("_ThroughWall", .3f);
                keyEcho.SetPropertyBlock(key);
            }
        }
        void Clear()
        {
            remaining = 0;
            if (pulseTracks != null) for (int i=0;i<pulseTracks.Length;i++) if(pulseTracks[i]!=null)
                for(int j=0;j<pulseTracks[i].Length;j++) if(pulseTracks[i][j]) { pulseTracks[i][j].SetColor("_BaseColor",originalTracks[i][j].GetColor("_BaseColor"));pulseTracks[i][j].SetColor("_EmissionColor",Color.black); }
            if (keyEcho) keyEcho.enabled = false;
        }
        void OnDestroy()
        {
            if (pulseTracks != null) for(int i=0;i<pulseTracks.Length;i++) if(pulseTracks[i]!=null)
            { if(footprints[i])footprints[i].sharedMaterials=originalTracks[i];foreach(var m in pulseTracks[i])if(m)Destroy(m); }

        }
        void OnDisable()
        {
            if (whistle) whistle.Blown -= OnWhistle;
            if (inspection) inspection.PageViewed -= OnPage;
            Clear();
        }
    }
}
