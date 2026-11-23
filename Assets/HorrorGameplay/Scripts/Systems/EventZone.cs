// Detecta entrada em áreas que disparam uma ação.

using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    [RequireComponent(typeof(Collider))]
    public sealed class EventZone : MonoBehaviour
    {
        [SerializeField]
        public bool oneShot = true;
        [SerializeField]
        public UnityEvent onEnter = new UnityEvent(), onExit = new UnityEvent();
        bool fired;
        int occupants;
        void OnTriggerEnter(Collider other)
        {
            if (!other.GetComponentInParent<FirstPersonMotor>())
                return;
            occupants++;
            if (occupants > 1 || oneShot && fired)
                return;
            fired = true;
            onEnter.Invoke();
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.GetComponentInParent<FirstPersonMotor>())
                return;
            occupants = Mathf.Max(0, occupants - 1);
            if (occupants == 0)
                onExit.Invoke();
        }

        void OnValidate()
        {
            GetComponent<Collider>().isTrigger = true;
        }
    }
}
