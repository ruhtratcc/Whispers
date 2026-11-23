// Verifica .

#if UNITY_EDITOR
using System;using System.Linq;using NUnit.Framework;using UnityEngine;using UnityEditor;
namespace HorrorGame.Tests {
 [Category("Immersion20261106")]
 public sealed class ImmersionPolishDataTests {
  const string Root="Assets/HorrorGameplay/Data/Immersion20261106/";
  [Test] public void FourChildSolesAndFourFadeLevelsAreKeptSeparate(){
   for(int child=1;child<=4;child++){
    var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"ChildTrack"+child+".asset");Assert.That(mesh.subMeshCount,Is.EqualTo(4));Assert.That(mesh.vertexCount,Is.GreaterThan(100));
    var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"ChildSole"+child+".png");Assert.That(tex,Is.Not.Null);
    for(int fade=0;fade<4;fade++){var mat=AssetDatabase.LoadAssetAtPath<Material>(Root+"ChildTrack"+child+"Fade"+fade+".mat");Assert.That(mat.mainTexture,Is.EqualTo(tex));Assert.That(mat.GetColor("_BaseColor").a,Is.EqualTo(new[]{.22f,.37f,.54f,.7f}[fade]).Within(.001f));}
   }
  }
  [Test] public void RequiredPhoneCluesStillArriveWithoutOpeningThePhone(){
   var catalog=AssetDatabase.LoadAssetAtPath<PhoneMessageCatalog>("Assets/HorrorGameplay/Data/PhoneMessages/Messages.asset");
   foreach(string id in new[]{"road.01","secretary.photo","secretary.who","route.library.key","route.four.hint","route.loop.hint"}){Assert.That(catalog.Find(id),Is.Not.Null,id);Assert.That(catalog.Find(id).onlyWhenOpened,Is.False,id);}
   Assert.That(catalog.Find("marta").onlyWhenOpened,Is.True);Assert.That(catalog.Find("loop.whistle").onlyWhenOpened,Is.True);
  }
  [Test] public void RatHasAnImportedLoopAndThreeRealPawRecordings(){
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Models/Environment/RatCC0/Rat.prefab");Assert.That(prefab.GetComponentInChildren<SkinnedMeshRenderer>(),Is.Not.Null);var a=prefab.GetComponent<Animation>();Assert.That(a&&a.clip&&a.clip.legacy,Is.True);Assert.That(a.clip.isLooping,Is.True);Assert.That(prefab.transform.localScale.x,Is.EqualTo(.1f));
   for(int i=1;i<=3;i++)Assert.That(AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Environment/rat_scurry_wall_0"+i+".wav"),Is.Not.Null);
  }
  [Test] public void MirrorObjectiveGivesADirectionFromTheTapeRoom(){
   var b=AssetDatabase.LoadAssetAtPath<StoryBook>("Assets/HorrorGameplay/Data/BeforeDemolition/BeforeDemolitionStory.asset");var line=b.chapters.Single(c=>c.id=="bd.two-voices").milestones.Single(m=>m.id=="bd.loop.tape");Assert.That(line.objectiveAfter,Does.Contain("última porta à direita").And.Not.Contain("sala do espelho"));
  }
 }
}
#endif

