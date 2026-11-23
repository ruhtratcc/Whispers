// Verifica .

#if UNITY_EDITOR
using System;using System.Collections;using System.Linq;using NUnit.Framework;using UnityEngine;using UnityEngine.TestTools;
namespace HorrorGame.Tests{
 public sealed partial class BeforeDemolitionFourStudentsTests{
 [UnityTest,Category("Polish20261103Intercom"),Timeout(90000)]
 public IEnumerator FourEvidenceAndMessageReadStillRequireCentralArrivalBeforeStairKey(){
 var phone=PhoneMessages.For(game);Assert.That(flow.studentVisuals[0].transform.position.x,Is.LessThan(0),"Marta now belongs to arts, outside Automotive.");
 game.player.Warp(new Vector3(-4,.05f,84),0);
 foreach(string id in BeforeDemolitionFourStudentsFlow.StudentItemIds)Add(id);Add(BeforeDemolitionFourStudentsFlow.BiaTapeHeardId);
 yield return Until(()=>phone.State.Has("route.four.hint"),"Route message is delivered",6);
 Assert.That(flow.Gathered,Is.False,"Reading the route is necessary.");Assert.That(phone.OpenDirect(),Is.True);yield return null;phone.Close();
 yield return Until(()=>flow.Gathered&&game.CanControl,"All evidence and actual conversation reading permit gathering.",6);
 yield return new WaitForSeconds(.25f);Assert.That(flow.IntercomCompleted,Is.False);Assert.That(game.narrative.SequencePlaying,Is.False);Assert.That(flow.stairKeyVisual.activeSelf,Is.False);
 var sequence=game.narrative.book.sequences.Single(s=>s.id==BeforeDemolitionFourStudentsFlow.IntercomSequenceId);var seconds=sequence.lines.Select(l=>l.seconds).ToArray();
 try{foreach(var line in sequence.lines)line.seconds=.12f;game.player.Warp(new Vector3(0,.7f,52),0);yield return Until(()=>flow.IntercomCompleted&&game.CanControl,"Only arriving centrally plays the intercom.",7);
 Assert.That(flow.stairKeyVisual.activeSelf,Is.True);Assert.That(flow.CanInteract(BeforeDemolitionFourStudentsAction.StairKey,game.interaction),Is.True);
 flow.Interact(BeforeDemolitionFourStudentsAction.StairKey,game.interaction);Assert.That(flow.HasStairKey,Is.True);
 flow.stairDoor.Interact(game.interaction);Assert.That(flow.stairDoor.locked,Is.False);Assert.That(game.save.Save(),Is.True);Assert.That(SaveSystem.TryReadCheckpoint(game.save.SavePath,out _,"SchoolCEEP",BeforeDemolitionCampaign.StoryId),Is.True);
 }finally{for(int i=0;i<seconds.Length;i++)sequence.lines[i].seconds=seconds[i];}
 }
 }
}
#endif

