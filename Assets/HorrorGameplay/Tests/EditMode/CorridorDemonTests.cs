// Verifica .

#if UNITY_EDITOR
using System.Linq;using NUnit.Framework;using UnityEditor;using UnityEngine;
namespace HorrorGame.Tests
{
 [Category("CorridorDemon20261105")]
 public sealed class CorridorDemonTests
 {
  DemonVoiceConfig config;AudioClip clip;
  [SetUp]public void SetUp(){config=Object.Instantiate(AssetDatabase.LoadAssetAtPath<DemonVoiceConfig>("Assets/HorrorGameplay/Data/CorridorDemon20261105/DemonVoice.asset"));clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/HorrorGameplay/Audio/VoiceDubbing20261023/JOA-69.wav");Assert.That(config&&clip,Is.True);}
  [TearDown]public void TearDown(){Object.DestroyImmediate(config);}
  [TestCase(0,1)][TestCase(.5f,2)][TestCase(1,3)]public void StagesUseTheCorrectTakes(float slip,int stage)
  {var p=DemonVoice.Plan(new DemonLine{mask=clip,low=clip},slip,config);Assert.That(p.mask!=null,Is.EqualTo(stage!=3));Assert.That(p.low!=null,Is.EqualTo(stage!=1));Assert.That(p.maskGain+p.lowGain,Is.LessThanOrEqualTo(config.voiceCeiling+.0001f));if(stage==2)Assert.That(p.delay,Is.InRange(.08f,.15f));if(stage==3)Assert.That(p.pitch,Is.LessThan(1));}
  [Test]public void MissingLowReusesTheActualMaskClipAndExtendsTheSubtitle(){var p=DemonVoice.Plan(new DemonLine{mask=clip},1,config);Assert.That(p.fallback,Is.True);Assert.That(p.low,Is.SameAs(clip));Assert.That(p.seconds,Is.GreaterThan(clip.length));Assert.That(p.mask,Is.Null);}
  [Test]public void MissingBothTakesUsesCaptionOnly(){var p=DemonVoice.Plan(new DemonLine{captionSeconds=5},.5f,config);Assert.That(p.mask,Is.Null);Assert.That(p.low,Is.Null);Assert.That(p.seconds,Is.EqualTo(5));}
  [Test]public void LegacyStoryLineDurationAndOldStateRemainCompatible(){Assert.That(new StoryLine{seconds=2}.Duration,Is.EqualTo(2));Assert.That(new StoryLine{voice=clip,seconds=100}.Duration,Is.EqualTo(clip.length+.2f));var old=BeforeDemolitionTwoVoicesState.Parse("{\"version\":1,\"loopCount\":3}");Assert.That(old.ValidFields,Is.True);Assert.That(old.wrapCount,Is.Zero);Assert.That(BeforeDemolitionTwoVoicesFlow.ValidState("{\"version\":1,\"loopCount\":3,\"wrapCount\":100001}"),Is.False);}
  [TestCase(13.65f,13.5f)][TestCase(-.15f,-13.5f)][TestCase(27.1f,27)][TestCase(0,0)][TestCase(-.001f,0)]public void WrapIsAlwaysAnExactModuleMultiple(float z,float expected){Assert.That(CorridorAtmosphere.WrapDelta(z,13.5f),Is.EqualTo(expected).Within(.0001f));}
  [Test]public void FogIsBlackAndSymmetric(){var c=AssetDatabase.LoadAssetAtPath<CorridorAtmosphereConfig>("Assets/HorrorGameplay/Data/CorridorDemon20261105/CorridorAtmosphere.asset");Assert.That(c.fogColor,Is.EqualTo(Color.black));Assert.That(c.visibility,Is.InRange(10,14));Assert.That(CorridorAtmosphereConfig.FogTransmission(12,c.fogDensity),Is.EqualTo(CorridorAtmosphereConfig.FogTransmission(-12,c.fogDensity)));Assert.That(CorridorAtmosphereConfig.FogTransmission(12,c.fogDensity),Is.LessThan(.02f));}
  [Test]public void BankContainsAllExactBaseLinesVariantsAndOriginalMirrorQuote(){Assert.That(config.lines.Count(l=>!l.mirror),Is.EqualTo(30));foreach(int stage in new[]{1,2,3})Assert.That(config.lines.Count(l=>!l.mirror&&l.stage==stage),Is.EqualTo(stage==3?12:9));Assert.That(config.Find("demon_mirror_refuse_01").text,Is.EqualTo("O senhor não deveria ter feito isso."));Assert.That(config.Find("demon_mirror_refuse_01").lowOnly,Is.True);Assert.That(config.Find("demon_mirror_offer_01").mirrorTransition,Is.True);Assert.That(config.maskGroup.name,Is.EqualTo("Voice_Demon_Mask"));Assert.That(config.lowGroup.name,Is.EqualTo("Voice_Demon_Low"));}
 }
}
#endif
