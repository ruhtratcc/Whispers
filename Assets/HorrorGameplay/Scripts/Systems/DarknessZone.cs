// Identifica regiões escuras para o sistema de atmosfera.

using UnityEngine;

namespace HorrorGame
{
    [RequireComponent(typeof(Collider))]
    public sealed class DarknessZone : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            var p = other.GetComponentInParent<FirstPersonMotor>();
            if (p)
                GameSession.Instance?.atmosphere?.EnterDarkness();
        }

        void OnTriggerExit(Collider other)
        {
            var p = other.GetComponentInParent<FirstPersonMotor>();
            if (p)
                GameSession.Instance?.atmosphere?.LeaveDarkness();
        }

        void OnValidate()
        {
            GetComponent<Collider>().isTrigger = true;
        }
    }
}
