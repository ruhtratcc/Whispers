// Verifica QuietSideRejectsHelenaAndSideways, DistanceOcclusionAndExcessUseIncreaseDelayWithoutBlocking, EachChildHasADistinctRecordedPlaybackTimbre.

using NUnit.Framework;
using UnityEngine;
namespace HorrorGame.Tests
{
 public sealed class WhistleChildAnswerTests
 {
  [Test]public void QuietSideRejectsHelenaAndSideways(){Assert.That(WhistleChildAnswers.FacesQuietSide(Vector3.back,Vector3.back*8,Vector3.forward*10),Is.True);Assert.That(WhistleChildAnswers.FacesQuietSide(Vector3.forward,Vector3.back*8,Vector3.forward*10),Is.False);Assert.That(WhistleChildAnswers.FacesQuietSide(Vector3.right,Vector3.back*8,Vector3.forward*10),Is.False);}
  [Test]public void DistanceOcclusionAndExcessUseIncreaseDelayWithoutBlocking(){var c=ScriptableObject.CreateInstance<WhistleConfig>();try{Assert.That(WhistleChildAnswers.Delay(c,0,false,0),Is.EqualTo(.6f).Within(.001f));Assert.That(WhistleChildAnswers.Delay(c,c.childAnswerRange,false,0),Is.EqualTo(1.2f).Within(.001f));Assert.That(WhistleChildAnswers.Delay(c,c.childAnswerRange,true,6),Is.GreaterThan(1.2f).And.LessThan(2));}finally{Object.DestroyImmediate(c);}}
  [Test]public void EachChildHasADistinctRecordedPlaybackTimbre(){var values=new System.Collections.Generic.HashSet<float>();for(int i=0;i<4;i++)Assert.That(values.Add(WhistleChildAnswers.Pitch(i)),Is.True);}
 }
}
