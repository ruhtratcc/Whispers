// Adapta os pontos interativos à viagem anterior.

using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    // Explicit numbers preserve existing scene references to the phone, torch and gate.
    public enum RoadJourneyInteractionKind { AnswerPhone = 0, TakeFlashlight = 1, SchoolGate = 2, ExitCar = 3 }

    /// <summary>Physical, raycastable interactions for the short road chapter.</summary>
    [DisallowMultipleComponent]
    public sealed class RoadJourneyInteraction : MonoBehaviour, IInteractable
    {
        public RoadJourneyFlow flow;
        public RoadJourneyInteractionKind kind;
        public GameObject visual;
        public UnityEvent onInteracted = new UnityEvent();
        bool visible = true;
        Renderer[] renderers;
        bool[] authoredVisibility;
        Collider[] colliders;
        bool[] authoredColliders;

        void Awake() => CacheVisuals();
        void CacheVisuals()
        {
            if (renderers != null) return;
            renderers = GetComponentsInChildren<Renderer>(true);
            authoredVisibility = new bool[renderers.Length];
            for (int i=0;i<renderers.Length;i++) authoredVisibility[i]=renderers[i].enabled;
            colliders=GetComponentsInChildren<Collider>(true);authoredColliders=new bool[colliders.Length];
            for(int i=0;i<colliders.Length;i++)authoredColliders[i]=colliders[i].enabled;
        }

        public string Prompt(PlayerInteraction player)
        {
            switch (kind)
            {
                case RoadJourneyInteractionKind.AnswerPhone: return "Atender o telefone";
                case RoadJourneyInteractionKind.TakeFlashlight: return "Pegar a lanterna";
                case RoadJourneyInteractionKind.ExitCar: return "Sair do carro · clique na porta";
                case RoadJourneyInteractionKind.SchoolGate: return "Ir até a entrada";
                default: return "Investigar";
            }
        }

        public bool CanInteract(PlayerInteraction player) => visible && flow && flow.CanInteract(kind);
        public void Interact(PlayerInteraction player)
        { if (CanInteract(player) && flow.Interact(this)) onInteracted.Invoke(); }

        public void SetVisible(bool value)
        {
            CacheVisuals();
            visible = value;
            for(int i=0;i<colliders.Length;i++)if(colliders[i])colliders[i].enabled=value&&authoredColliders[i];
            if (visual) visual.SetActive(value);
            // Hidden replacement sources must not reappear when a checkpoint restores the pickup.
            else for (int i=0;i<renderers.Length;i++) if(renderers[i]) renderers[i].enabled=value&&authoredVisibility[i];
        }
    }
}
