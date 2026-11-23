// Verifica .

#if UNITY_EDITOR
using NUnit.Framework;using UnityEditor;using UnityEngine;
namespace HorrorGame.Tests
{
 [Category("Refinements20261105")]
 public sealed class WhistleFloorTests
 {
  [TestCase(0,.15f,true)][TestCase(0,1.7f,true)][TestCase(0,3.84f,false)][TestCase(3.81f,3.84f,true)][TestCase(3.81f,.15f,false)]
  public void HeightBandSeparatesTheSchoolFloors(float observer,float target,bool expected){var c=ScriptableObject.CreateInstance<WhistleConfig>();try{Assert.That(c.SameHighlightFloor(observer,target),Is.EqualTo(expected));}finally{Object.DestroyImmediate(c);}}
  [Test]public void ExistingConfigPreservesSoundAndUsesTheNewHeightLimit(){var c=AssetDatabase.LoadAssetAtPath<WhistleConfig>("Assets/HorrorGameplay/Data/Whistle/WhistleConfig.asset");Assert.That(c.highlightHeightTolerance,Is.EqualTo(2.5f));Assert.That(c.range,Is.EqualTo(24));Assert.That(c.tapClip&&c.holdClip,Is.True);}
 }
}
#endif
