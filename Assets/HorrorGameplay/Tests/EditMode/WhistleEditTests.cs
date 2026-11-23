// Verifica .

#if UNITY_EDITOR
using System;using NUnit.Framework;using UnityEngine;using UnityEditor;using System.Linq;
namespace HorrorGame.Tests{
 [Category("Whistle")]
 public sealed class WhistleEditTests{
  [TestCase(-1)] [TestCase(1000001)]public void InvalidBlowCountRejected(int count){Assert.That(new WhistleState{owned=true,totalBlows=count}.Valid,Is.False);}
  [Test]public void StateCopyOldSaveAndCooling(){var config=ScriptableObject.CreateInstance<WhistleConfig>();try{var state=new WhistleState{owned=true,totalBlows=2,tension=3,cooldownRemaining=4,rareCommentRemaining=75};state.responders.Add("closet");var copy=state.Copy();copy.responders.Add("other");Assert.That(state.responders.Count,Is.EqualTo(1));state.Cool(10,config);Assert.That(state.tension,Is.EqualTo(2.55f).Within(.001f));Assert.That(state.cooldownRemaining,Is.Zero);Assert.That(state.rareCommentRemaining,Is.EqualTo(65));Assert.That(state.Valid,Is.True);Assert.That(JsonUtility.FromJson<SaveData>("{\"version\":2}").whistle.Valid,Is.True);Assert.That(JsonUtility.FromJson<WhistleState>(JsonUtility.ToJson(copy)).Valid,Is.True);}finally{UnityEngine.Object.DestroyImmediate(config);}}
  [TestCase("nan")] [TestCase("duplicate")] [TestCase("ownership")]public void CorruptNewStateRejected(string fault){var state=new WhistleState{owned=true};if(fault=="nan")state.tension=float.NaN;if(fault=="duplicate"){state.responders.Add("x");state.responders.Add("x");}if(fault=="ownership"){state.owned=false;state.learnedHold=true;}Assert.That(state.Valid,Is.False);}
  [TestCase(0,0,6,true)] [TestCase(0,0,11,false)] [TestCase(6,0,0,false)] [TestCase(0,0,-6,false)]
  public void RadiusAndCone(float x,float y,float z,bool expected){var go=new GameObject("Responder");try{go.transform.position=new Vector3(x,y,z);var r=go.AddComponent<WhistleResponder>();r.range=10;r.hearingAngle=60;Assert.That(r.CanHear(new WhistleContext(null,Vector3.zero,Vector3.forward,WhistleKind.Tap,0,1,"test","room")),Is.EqualTo(expected));}finally{UnityEngine.Object.DestroyImmediate(go);}}
  [Test]public void InstalledConfigIsRecordedAndDialogueComplete(){var c=AssetDatabase.LoadAssetAtPath<WhistleConfig>("Assets/HorrorGameplay/Data/Whistle/WhistleConfig.asset");Assert.That(c&&c.tapClip&&c.holdClip&&c.dialogue,Is.True);Assert.That(c.dialogue.lines.Length,Is.EqualTo(183));Assert.That(c.dialogue.lines.Select(l=>l.id).Distinct().Count(),Is.EqualTo(183));Assert.That(c.dialogue.lines.All(l=>!l.text.Contains("Q:")&&!l.text.Contains("Botão")),Is.True);Assert.That(c.dialogue.lines.All(l=>l.voice==null),Is.True,"New human recordings remain explicitly pending, never TTS.");}
 }
}
#endif
