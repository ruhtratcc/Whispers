// Verifica IrregularPatchUsesWetSoundOnlyInsideItsShapeOnTheSameFloor, MissingOrDegenerateMeshesKeepTheExistingSurface.

using NUnit.Framework;
using UnityEngine;

namespace HorrorGame.Tests
{
    public sealed class FloorPatchFootstepTests
    {
        [Test]
        public void IrregularPatchUsesWetSoundOnlyInsideItsShapeOnTheSameFloor()
        {
            var go = new GameObject("floor patch", typeof(MeshFilter));
            var player = new GameObject("footstep test");
            var floor = new GameObject("original floor", typeof(BoxCollider));
            var library = ScriptableObject.CreateInstance<FootstepLibrary>();
            var shape = new Mesh();
            try
            {
                shape.vertices = new[]{new Vector3(0,0,0),new Vector3(2,0,0),new Vector3(0,0,2)};
                shape.triangles = new[]{0,2,1};shape.RecalculateBounds();
                go.GetComponent<MeshFilter>().sharedMesh=shape;
                go.transform.position=new Vector3(8,3.813f,9);go.transform.rotation=Quaternion.Euler(0,30,0);
                var wet=new FootstepLibrary.Entry{surface=GroundSurface.Dirt};
                var fallback=new FootstepLibrary.Entry{surface=GroundSurface.Concrete};library.entries=new[]{fallback};
                var audio=player.AddComponent<FootstepAudio>();audio.library=library;
                audio.groundPatches=new[]{new FootstepAudio.SurfacePatch{mesh=go.GetComponent<MeshFilter>(),footsteps=wet}};
                var inside=go.transform.TransformPoint(new Vector3(.25f,-.003f,.25f));
                var outsideTriangle=go.transform.TransformPoint(new Vector3(1.5f,-.003f,1.5f));
                Assert.That(audio.ResolveGround(floor.GetComponent<Collider>(),inside),Is.SameAs(wet));
                Assert.That(audio.ResolveGround(floor.GetComponent<Collider>(),outsideTriangle),Is.SameAs(fallback));
                Assert.That(audio.ResolveGround(floor.GetComponent<Collider>(),inside+Vector3.up*3.81f),Is.SameAs(fallback));
                Assert.That(audio.ResolveGround(null,inside),Is.SameAs(fallback));
                go.SetActive(false);
                Assert.That(audio.ResolveGround(floor.GetComponent<Collider>(),inside),Is.SameAs(fallback));
                Assert.That(go.GetComponent<Collider>(),Is.Null);
            }
            finally{Object.DestroyImmediate(player);Object.DestroyImmediate(go);Object.DestroyImmediate(floor);Object.DestroyImmediate(shape);Object.DestroyImmediate(library);}
        }

        [Test]
        public void MissingOrDegenerateMeshesKeepTheExistingSurface()
        {
            var patch=new FootstepAudio.SurfacePatch();
            Assert.That(patch.Contains(Vector3.zero),Is.False);
            var go=new GameObject("degenerate patch",typeof(MeshFilter));var shape=new Mesh();
            try{shape.vertices=new[]{Vector3.zero,Vector3.right,Vector3.right*2};shape.triangles=new[]{0,1,2};shape.RecalculateBounds();go.GetComponent<MeshFilter>().sharedMesh=shape;patch.mesh=go.GetComponent<MeshFilter>();Assert.That(patch.Contains(Vector3.right),Is.False);}
            finally{Object.DestroyImmediate(go);Object.DestroyImmediate(shape);}
        }
    }
}
