// Verifica SetUp, TearDown, OldUnlockedOpenSaveOutsideIsClosedAndHasNoFakeKeyPrompt, LeafInteractionResolvesRestrictionBeforeItsParentDoor, CheckpointInsideCanExitAndReenteringFromOutsideStaysBlocked, PairedLeavesRemainSynchronizedAfterOldRestore, SilentClosingDoesNotTeleportThroughPlayerAtClosedPose.

using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HorrorGame.Tests
{
    [Category("MapCompletion")]
    public sealed class SchoolAreaRestrictionTests
    {
        GameObject root;
        InteractiveDoor door;
        SchoolAreaRestriction restriction;
        static readonly MethodInfo Apply = typeof(SchoolAreaRestriction).GetMethod("ApplyRestriction", BindingFlags.Instance | BindingFlags.NonPublic);

        [SetUp] public void SetUp()
        {
            root = new GameObject("Secondary room restriction test");
            door = root.AddComponent<InteractiveDoor>();
            door.hinge = root.transform;
            var leaf = new GameObject("Leaf");
            leaf.transform.SetParent(root.transform, false);
            leaf.transform.localPosition = new Vector3(.5f, 1, 0);
            door.leafCollider = leaf.AddComponent<BoxCollider>();
            door.leafCollider.size = new Vector3(1, 2, .08f);
            door.playerMask = 1 << 8;
            door.SetOpen(false, true);
            restriction = leaf.AddComponent<SchoolAreaRestriction>();
            restriction.door = door;
            restriction.interior = new Bounds(new Vector3(.5f, 1, 2), new Vector3(2, 3, 4));
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(root);
        void Refresh(Vector3 position) => Apply.Invoke(restriction, new object[] { position });

        [Test] public void OldUnlockedOpenSaveOutsideIsClosedAndHasNoFakeKeyPrompt()
        {
            Refresh(new Vector3(.5f, .2f, -2));
            door.RestoreState("{\"open\":true,\"locked\":false}");
            Refresh(new Vector3(.5f, .2f, -2));
            Assert.That(door.IsOpen, Is.False);
            Assert.That(door.locked, Is.True);
            Assert.That(root.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(door.requiredKey, Is.Empty);
            Assert.That(door.lockedMessage, Is.EqualTo(restriction.blockedMessage));
            StringAssert.DoesNotContain("Destrancar", restriction.Prompt(null));
            Refresh(new Vector3(.5f, .2f, -.3f));
            Assert.That(door.locked, Is.True, "Approaching within the exit margin must not grant entry.");
        }

        [Test] public void LeafInteractionResolvesRestrictionBeforeItsParentDoor()
        {
            Refresh(new Vector3(.5f, .2f, -2));
            IInteractable first = null;
            foreach (var behaviour in door.leafCollider.GetComponentsInParent<MonoBehaviour>())
                if (behaviour is IInteractable candidate && candidate.CanInteract(null))
                { first = candidate; break; }
            Assert.That(first, Is.SameAs(restriction));
            Assert.That(first.Prompt(null), Does.StartWith("Verificar"));
        }

        [Test] public void CheckpointInsideCanExitAndReenteringFromOutsideStaysBlocked()
        {
            Refresh(new Vector3(.5f, .2f, -2));
            door.RestoreState("{\"open\":false,\"locked\":true}");
            Refresh(new Vector3(.5f, .2f, 2));
            Assert.That(door.locked, Is.False);
            door.Interact(null);
            Assert.That(door.IsOpen, Is.True);
            Refresh(new Vector3(.5f, .2f, -.3f));
            Assert.That(door.locked, Is.False, "The existing inside permission allows clearance at the threshold.");
            Refresh(new Vector3(.5f, .2f, -1));
            Assert.That(door.locked, Is.True);
            Assert.That(door.IsOpen, Is.False);
            Refresh(new Vector3(.5f, .2f, -.3f));
            Assert.That(door.locked, Is.True);
            Refresh(new Vector3(.5f, .2f, 2));
            Assert.That(door.locked, Is.False, "A later blackout restoring the inside pose must permit leaving again.");
        }

        [Test] public void PairedLeavesRemainSynchronizedAfterOldRestore()
        {
            var other = new GameObject("Partner").AddComponent<InteractiveDoor>();
            other.transform.SetParent(root.transform, false);
            other.hinge = other.transform;
            door.persistentId = "school.door.0.1";
            other.persistentId = "school.door.0.72";
            door.pairedLeaf = other;
            other.pairedLeaf = door;
            Refresh(new Vector3(.5f, .2f, -2));
            door.RestoreState("{\"open\":true,\"locked\":false}");
            Refresh(new Vector3(.5f, .2f, -2));
            Assert.That(other.locked, Is.True);
            Assert.That(other.IsOpen, Is.False);
            Refresh(new Vector3(.5f, .2f, 2));
            restriction.Interact(null);
            Assert.That(door.IsOpen && other.IsOpen, Is.True);
            Assert.That(door.locked || other.locked, Is.False);
        }

        [Test] public void SilentClosingDoesNotTeleportThroughPlayerAtClosedPose()
        {
            Refresh(new Vector3(.5f, .2f, -2));
            door.RestoreState("{\"open\":true,\"locked\":false}");
            var body = new GameObject("Player collider");
            body.transform.SetParent(root.transform.parent, false);
            body.layer = 8;
            body.transform.position = new Vector3(.5f, 1, 0);
            body.AddComponent<CapsuleCollider>().radius = .32f;
            try
            {
                Physics.SyncTransforms();
                Quaternion openPose = root.transform.localRotation;
                Refresh(new Vector3(.5f, .2f, -2));
                Assert.That(door.locked, Is.True);
                Assert.That(door.IsOpen, Is.False);
                Assert.That(Quaternion.Angle(root.transform.localRotation, openPose), Is.LessThan(.001f), "Occupied closed pose uses the existing gradual anti-crush motion.");
            }
            finally { Object.DestroyImmediate(body); }
        }
    }
}
