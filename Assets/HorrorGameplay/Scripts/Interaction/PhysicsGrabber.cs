// Pega, segura e solta objetos físicos.

using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame
{
    public sealed class PhysicsGrabber : MonoBehaviour
    {
        [SerializeField]
        public Camera view;
        [SerializeField]
        public FirstPersonMotor motor;
        [Header("Holding")]
        [SerializeField]
        float holdDistance = 1.65f, maxMass = 20, spring = 65, damping = 13, maxAcceleration = 45;
        [SerializeField]
        float maximumDistance = 3, stuckError = .85f, stuckTime = .65f, throwImpulse = 7;
        [SerializeField]
        public LayerMask obstructionMask = ~0;
        public Rigidbody Held { get; private set; }

        float originalDrag, originalAngularDrag, stuckFor;
        RigidbodyInterpolation originalInterpolation;
        CollisionDetectionMode originalCollisionMode;
        readonly List<(Collider player, Collider objectCollider, bool ignored)> pairs = new List<(Collider, Collider, bool)>();
        Quaternion rotationOffset;
        public bool CanHold(Rigidbody body) => body && !body.isKinematic && body.mass <= maxMass;
        public bool PickUp(Rigidbody body)
        {
            if (Held || !CanHold(body))
                return false;
            Held = body;
            stuckFor = 0;
            originalDrag = body.linearDamping;
            originalAngularDrag = body.angularDamping;
            originalInterpolation = body.interpolation;
            originalCollisionMode = body.collisionDetectionMode;
            body.linearDamping = 0;
            body.angularDamping = 3;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rotationOffset = Quaternion.Inverse(view.transform.rotation) * body.rotation;
            foreach (var p in motor.GetComponentsInChildren<Collider>())
                foreach (var c in body.GetComponentsInChildren<Collider>())
                {
                    bool ignored = Physics.GetIgnoreCollision(p, c);
                    pairs.Add((p, c, ignored));
                    Physics.IgnoreCollision(p, c, true);
                }

            return true;
        }

        void FixedUpdate()
        {
            if (!Held)
                return;
            if (GameSession.Instance && !GameSession.Instance.CanControl)
            {
                Drop();
                return;
            }

            Vector3 desired = view.transform.position + view.transform.forward * holdDistance;
            if (Physics.SphereCast(view.transform.position, .18f, view.transform.forward, out var hit, holdDistance, obstructionMask, QueryTriggerInteraction.Ignore) && hit.rigidbody != Held)
                desired = view.transform.position + view.transform.forward * Mathf.Max(.5f, hit.distance - .2f);
            Vector3 error = desired - Held.worldCenterOfMass;
            if (Vector3.Distance(view.transform.position, Held.worldCenterOfMass) > maximumDistance)
            {
                Drop();
                return;
            }

            if (error.magnitude > stuckError)
                stuckFor += Time.fixedDeltaTime;
            else
                stuckFor = 0;
            if (stuckFor >= stuckTime)
            {
                Drop();
                return;
            }

            Held.AddForce(Vector3.ClampMagnitude(error * spring - Held.linearVelocity * damping, maxAcceleration), ForceMode.Acceleration);
            Quaternion change = (view.transform.rotation * rotationOffset) * Quaternion.Inverse(Held.rotation);
            change.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180)
                angle -= 360;
            if (!float.IsNaN(axis.x))
                Held.AddTorque(Vector3.ClampMagnitude(axis * angle * Mathf.Deg2Rad * 18 - Held.angularVelocity * 5, 25), ForceMode.Acceleration);
        }

        public void Drop()
        {
            if (Held)
            {
                Held.linearDamping = originalDrag;
                Held.angularDamping = originalAngularDrag;
                Held.interpolation = originalInterpolation;
                Held.collisionDetectionMode = originalCollisionMode;
            }

            foreach (var pair in pairs)
                if (pair.player && pair.objectCollider)
                    Physics.IgnoreCollision(pair.player, pair.objectCollider, pair.ignored);
            pairs.Clear();
            Held = null;
            stuckFor = 0;
        }

        public void Throw()
        {
            var body = Held;
            Drop();
            if (body)
                body.AddForce(view.transform.forward * throwImpulse, ForceMode.Impulse);
        }

        void OnDisable() => Drop();
    }
}
