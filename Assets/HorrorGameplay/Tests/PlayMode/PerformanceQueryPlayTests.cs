// Verifica StandingQueryPreservesCeilingAndSelfCollisionRulesAfterBufferGrowth.

using System;
using System.Reflection;
using HorrorGame;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HorrorGame.Tests
{
    [Category("PerformanceQueries")]
    public sealed class PerformanceQueryPlayTests
    {
        [Test]
        public void StandingQueryPreservesCeilingAndSelfCollisionRulesAfterBufferGrowth()
        {
            const int layer = 30;
            var root = new GameObject("Standing query test") { layer = layer };
            root.transform.position = new Vector3(10000, 10000, 10000);
            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.05f;
            controller.radius = .3f;
            controller.center = Vector3.up * controller.height * .5f;
            var motor = root.AddComponent<FirstPersonMotor>();
            motor.enabled = false;
            motor.collisionMask = 1 << layer;
            GameObject ceiling = null;
            try
            {
                Physics.SyncTransforms();
                Assert.That(motor.CanStand(), Is.EqualTo(OriginalStandingQuery(motor)));
                Assert.That(motor.CanStand(), Is.True, "The player's own controller must be ignored.");

                for (int i = 0; i < 40; i++)
                {
                    var child = new GameObject("Self collider " + i) { layer = layer };
                    child.transform.SetParent(root.transform, false);
                    child.transform.localPosition = new Vector3((i % 5 - 2) * .015f, 1.35f, 0);
                    child.AddComponent<BoxCollider>().size = Vector3.one * .08f;
                }
                Physics.SyncTransforms();
                Assert.That(motor.CanStand(), Is.EqualTo(OriginalStandingQuery(motor)));
                Assert.That(motor.CanStand(), Is.True, "All forty descendant colliders still belong to the player.");
                var buffer = (Collider[])typeof(FirstPersonMotor).GetField("standingOverlaps", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(motor);
                Assert.That(buffer.Length, Is.GreaterThan(40), "A saturated query must grow and run again.");

                ceiling = new GameObject("Foreign ceiling") { layer = layer };
                ceiling.transform.position = root.transform.position + Vector3.up * (motor.standingHeight - .03f);
                var collider = ceiling.AddComponent<BoxCollider>();
                collider.size = new Vector3(1, .12f, 1);
                Physics.SyncTransforms();
                Assert.That(motor.CanStand(), Is.EqualTo(OriginalStandingQuery(motor)));
                Assert.That(motor.CanStand(), Is.False, "The upper end of the standing capsule must find a foreign ceiling.");

                collider.isTrigger = true;
                Physics.SyncTransforms();
                Assert.That(motor.CanStand(), Is.EqualTo(OriginalStandingQuery(motor)));
                Assert.That(motor.CanStand(), Is.True, "Trigger volumes must remain excluded.");

                motor.CanStand();
                long before = GC.GetAllocatedBytesForCurrentThread();
                bool allClear = true;
                for (int i = 0; i < 1000; i++) allClear &= motor.CanStand();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allClear, Is.True);
                Assert.That(allocated, Is.Zero, "The warmed standing query must reuse its managed buffer.");
            }
            finally
            {
                if (ceiling) Object.DestroyImmediate(ceiling);
                Object.DestroyImmediate(root);
                Physics.SyncTransforms();
            }
        }

        static bool OriginalStandingQuery(FirstPersonMotor motor)
        {
            float radius = Mathf.Max(.05f, motor.Controller.radius - .025f);
            Vector3 lower = motor.transform.position + Vector3.up * Mathf.Max(motor.Controller.height - radius, radius);
            Vector3 upper = motor.transform.position + Vector3.up * (motor.standingHeight - radius);
            foreach (var collider in Physics.OverlapCapsule(lower, upper, radius, motor.collisionMask, QueryTriggerInteraction.Ignore))
                if (!collider.transform.IsChildOf(motor.transform)) return false;
            return true;
        }
    }
}
