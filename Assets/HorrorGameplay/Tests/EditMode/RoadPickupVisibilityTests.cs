// Verifica RestoringPickupDoesNotResurrectRetiredMeshesOrColliders.

using NUnit.Framework;
using UnityEngine;
namespace HorrorGame.Tests
{
    public sealed class RoadPickupVisibilityTests
    {
        [Test] public void RestoringPickupDoesNotResurrectRetiredMeshesOrColliders()
        {
            var root=new GameObject("Temporary pickup visibility regression");
            try
            {
                var current=GameObject.CreatePrimitive(PrimitiveType.Cube);current.transform.SetParent(root.transform);
                var retired=GameObject.CreatePrimitive(PrimitiveType.Sphere);retired.transform.SetParent(root.transform);
                retired.GetComponent<Renderer>().enabled=false;retired.GetComponent<Collider>().enabled=false;
                var pickup=root.AddComponent<RoadJourneyInteraction>();
                pickup.SetVisible(false);Assert.That(current.GetComponent<Renderer>().enabled,Is.False);
                pickup.SetVisible(true);pickup.SetVisible(true);
                Assert.That(current.GetComponent<Renderer>().enabled&&current.GetComponent<Collider>().enabled,Is.True);
                Assert.That(retired.GetComponent<Renderer>().enabled||retired.GetComponent<Collider>().enabled,Is.False);
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
