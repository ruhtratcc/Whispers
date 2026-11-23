// Encena mudanças pontuais no cenário a partir de eventos configurados.

using UnityEngine;
using UnityEngine.Events;

namespace HorrorGame
{
    public sealed class PoltergeistEvent : MonoBehaviour
    {
        [Tooltip("Optional loose props already placed by the map author.")]
        public Rigidbody[] props = new Rigidbody[0];
        public Vector3 impulse = new Vector3(.4f, .2f, 0);
        public bool localImpulse = true;
        public UnityEvent onFired = new UnityEvent();
        public void Fire()
        {
            foreach (var prop in props)
                if (prop && !prop.isKinematic)
                    prop.AddForce(localImpulse ? transform.TransformDirection(impulse) : impulse, ForceMode.Impulse);
            // Connect doors, lights, sound and scares through Inspector events.
            onFired.Invoke();
        }
    }
}
