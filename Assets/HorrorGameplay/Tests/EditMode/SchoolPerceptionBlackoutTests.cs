// Verifica SetUp, TearDown, LegacyChairPlacementIsPreservedAndBlackoutStillChangesIt, ConsecutiveBlackoutsRemainBoundedAndRestoreWithoutApplyingAgain, MissingChairDoesNotThrowOrScheduleAChange.

using NUnit.Framework;
using UnityEngine;

namespace HorrorGame.Tests
{
    [Category("BlackoutCheckpoint")]
    public sealed class SchoolPerceptionBlackoutTests
    {
        GameObject root;
        SchoolPerceptionEvents perception;

        [SetUp] public void SetUp()
        {
            root=new GameObject("Blackout chair test");
            perception=root.AddComponent<SchoolPerceptionEvents>();
            perception.chair=new GameObject("Decorative chair").transform;
            perception.chair.SetParent(root.transform,false);
            perception.originalChairPosition=new Vector3(1,.45f,2);
            perception.changedChairPosition=perception.originalChairPosition+new Vector3(.6f,0,-.35f);
            perception.chair.rotation=Quaternion.Euler(0,17,0);
        }
        [TearDown] public void TearDown()=>Object.DestroyImmediate(root);

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyChairPlacementIsPreservedAndBlackoutStillChangesIt(bool moved)
        {
            perception.RestoreState("{\"chairMoved\":"+(moved?"true":"false")+"}");
            Assert.That(perception.chair.position,Is.EqualTo(moved?perception.changedChairPosition:perception.originalChairPosition));
            perception.ApplyBlackoutConsequence();
            Assert.That(perception.chair.position,Is.EqualTo(moved?perception.originalChairPosition:perception.changedChairPosition));
            StringAssert.Contains("\"blackoutApplied\":true",perception.CaptureState());
        }

        [Test] public void ConsecutiveBlackoutsRemainBoundedAndRestoreWithoutApplyingAgain()
        {
            perception.RestoreState("{\"chairMoved\":true}");
            Quaternion rotation=perception.chair.rotation;
            for (int i=0;i<12;i++)
            {
                Vector3 before=perception.chair.position;
                perception.ApplyBlackoutConsequence();
                Vector3 after=perception.chair.position;
                Assert.That(after,Is.Not.EqualTo(before));
                Assert.That(after==perception.originalChairPosition||after==perception.changedChairPosition,Is.True);
                Assert.That(perception.chair.rotation,Is.EqualTo(rotation));
                string saved=perception.CaptureState();
                perception.chair.position=Vector3.one*100;
                perception.RestoreState(saved);
                perception.RestoreState(saved);
                Assert.That(perception.chair.position,Is.EqualTo(after));
                Assert.That(perception.CaptureState(),Is.EqualTo(saved));
            }
            Assert.That(root.GetComponentsInChildren<Collider>(),Is.Empty);
        }

        [Test] public void MissingChairDoesNotThrowOrScheduleAChange()
        {
            perception.chair=null;
            string before=perception.CaptureState();
            Assert.DoesNotThrow(()=>perception.ApplyBlackoutConsequence());
            Assert.That(perception.CaptureState(),Is.EqualTo(before));
        }
    }
}
