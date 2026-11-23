// Verifica ReflectionMirrorsAcrossAnOffsetPlaneAndApplyingTwiceRestoresThePoint, LookingAwayAndPauseFreezeTheSmileAndRestoreDoesNotReplayTheMemory.

using NUnit.Framework;
using UnityEngine;

namespace HorrorGame.Tests
{
    public sealed class SchoolMemoryMirrorTests
    {
        [Test]
        public void ReflectionMirrorsAcrossAnOffsetPlaneAndApplyingTwiceRestoresThePoint()
        {
            Vector3 normal = new Vector3(1, 0, 1).normalized;
            Vector3 plane = new Vector3(5, 1, 2);
            Vector3 point = plane + normal * 3 + Vector3.up * 2;
            var matrix = SchoolMemoryMirror.ReflectionMatrix(plane, normal);
            Assert.That(Vector3.Dot(matrix.MultiplyPoint(point) - plane, normal), Is.EqualTo(-3).Within(.0001f));
            Assert.That(Vector3.Distance(matrix.MultiplyPoint(matrix.MultiplyPoint(point)), point), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(matrix.MultiplyPoint(plane), plane), Is.LessThan(.0001f));
        }

        [Test]
        public void LookingAwayAndPauseFreezeTheSmileAndRestoreDoesNotReplayTheMemory()
        {
            var go = new GameObject("Mirror clock fixture");
            var smile = new GameObject("Smile");
            try
            {
                var mirror = go.AddComponent<SchoolMemoryMirror>();
                mirror.smile = smile;
                mirror.Restore(false);
                mirror.AdvanceObservation(2.3f, true);
                mirror.AdvanceObservation(9, false);
                mirror.AdvanceObservation(0, true);
                Assert.That(mirror.ObservedSeconds, Is.EqualTo(2.3f));
                Assert.That(smile.activeSelf, Is.False);
                mirror.AdvanceObservation(.2f, true);
                Assert.That(smile.activeSelf, Is.True);
                mirror.AdvanceObservation(1, true);
                Assert.That(mirror.Finished, Is.True);
                mirror.Restore(true);
                mirror.AdvanceObservation(5, true);
                Assert.That(mirror.Finished, Is.True);
                Assert.That(smile.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(smile); }
        }
    }
}
