// Verifica .

#if UNITY_EDITOR
using System.Collections;using NUnit.Framework;using UnityEngine;using UnityEngine.TestTools;
namespace HorrorGame.Tests
{
 public sealed partial class BeforeDemolitionSchoolSceneTests
 {
  [UnityTest,Category("ChildAnswers"),Timeout(120000)]
  public IEnumerator ChildAnswerRequiresLoreDelaysAudioAndCancelsOnRestore()
  {
   // Isolate child audio from the independently tested auto-opening phone modal.
   var phone=game.GetComponent<PhoneMessages>();if(phone){phone.Close();phone.enabled=false;}game.tutorials?.SetMode(TutorialMode.Off);
   var whistle=WhistleSystem.For(game);var answer=whistle.GetComponent<WhistleChildAnswers>();Assert.That(answer,Is.Not.Null);
   game.player.Warp(new Vector3(-16.4f,.65f,28.2f),0);Assert.That(whistle.BlowTap(),Is.True);Assert.That(answer.PendingDelay,Is.LessThan(0));Assert.That(answer.AnswerSource.isPlaying,Is.False);
   whistle.State.loreKnown=whistle.State.discovered=true;whistle.State.cooldownRemaining=0;
   Assert.That(whistle.BlowTap(),Is.True);Assert.That(answer.PendingDelay,Is.GreaterThanOrEqualTo(.6f));Assert.That(answer.AnswerSource.isPlaying,Is.False);Assert.That(whistle.BlowTap(),Is.False,"One answer per accepted click, using the existing cooldown.");
   yield return Until(()=>answer.AnswerSource.isPlaying,"Recorded 3D child answer",4);
   Assert.That(answer.AnswerSource.spatialBlend,Is.EqualTo(1));Assert.That(answer.AnswerSource.clip,Is.Not.Null);Assert.That(answer.AnswerSource.transform.position,Is.EqualTo(answer.AnswerPosition));Assert.That(answer.AnswerSource.GetComponent<AudioLowPassFilter>().cutoffFrequency,Is.LessThanOrEqualTo(18000));
   Assert.That(game.save.Save(),Is.True);Assert.That(game.save.TryReadCurrent(out var copied),Is.True);whistle.Restore(copied.whistle);Assert.That(answer.AnswerSource.isPlaying,Is.False);Assert.That(answer.PendingDelay,Is.LessThan(0));Assert.That(whistle.SupernaturalActive,Is.True);
   whistle.State.loreKnown=whistle.State.discovered=false;whistle.State.cooldownRemaining=0;Assert.That(whistle.BlowTap(),Is.True);yield return new WaitForSeconds(1.5f);Assert.That(answer.AnswerSource.isPlaying,Is.False);
  }
  [UnityTest,Category("ChildAnswers"),Timeout(120000)]
  public IEnumerator NativeChildrenFollowUncollectedItemsAndWrongCorridorSideIsSilent()
  {
   var whistle=WhistleSystem.For(game);var answer=whistle.GetComponent<WhistleChildAnswers>();whistle.State.loreKnown=whistle.State.discovered=true;
   string original=game.narrative.Progress.chapterId;game.narrative.Progress.chapterId=BeforeDemolitionFourStudentsFlow.ChapterId;
   var students=game.GetComponent<BeforeDemolitionFourStudentsFlow>();Assert.That(students,Is.Not.Null);
   for(int i=0;i<4;i++){Vector3 p=students.studentVisuals[i].transform.position;var context=new WhistleContext(whistle,p+Vector3.forward*2,Vector3.back,WhistleKind.Tap,0,i,"SchoolCEEP","");Assert.That(answer.TryTarget(context,out var target,out var child,out var silent),Is.True);Assert.That(child,Is.EqualTo(i));Assert.That(silent,Is.False);Assert.That(Vector3.Distance(target,p),Is.LessThan(1));game.interaction.inventory.Add(new InventoryItem{id=BeforeDemolitionFourStudentsFlow.StudentItemIds[i],title="fixture"});}
   game.narrative.Progress.chapterId=BeforeDemolitionTwoVoicesFlow.ChapterId;var corridor=game.GetComponent<BeforeDemolitionTwoVoicesFlow>();Vector3 origin=corridor.returnPose.position+Vector3.up*1.5f,quiet=corridor.quietAnchor.position-origin,voice=corridor.voiceAnchor.position-origin;
   var good=new WhistleContext(whistle,origin,quiet.normalized,WhistleKind.Tap,0,8,"SchoolCEEP","");Assert.That(answer.TryTarget(good,out var point,out _,out var blocked),Is.True);Assert.That(point,Is.EqualTo(corridor.quietAnchor.position));Assert.That(blocked,Is.False);
   var wrong=new WhistleContext(whistle,origin,voice.normalized,WhistleKind.Tap,0,9,"SchoolCEEP","");Assert.That(answer.TryTarget(wrong,out _,out _,out bool silentWrong),Is.False);Assert.That(silentWrong,Is.True,"No empty echo toward Helena either.");game.narrative.Progress.chapterId=original;
   yield return null;
  }
 }
}
#endif
