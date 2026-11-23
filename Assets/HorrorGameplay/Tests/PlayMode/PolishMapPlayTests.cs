// Verifica .

#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using NUnit.Framework;using UnityEngine;using UnityEngine.TestTools;
namespace HorrorGame.Tests{
 public sealed partial class BeforeDemolitionFourStudentsTests {
 [UnityTest,Category("Polish20261103Map"),Timeout(120000)]
 public IEnumerator WiderLandingAndStairsAllowBothDirectionsWithTheNativeController(){
 var motor=game.player;bool enabled=motor.enabled;motor.enabled=false;var records=new List<string>();
 try{
 foreach(bool up in new[]{false,true}){
 Vector3 start=new Vector3(-61.54f,up?-.54f:.66f,up?60.1f:55.8f);motor.Warp(start,up?180:0);Physics.SyncTransforms();
 for(int i=0;i<92;i++){motor.Controller.Move(new Vector3(0,-.12f,up?-.05f:.05f));yield return null;Assert.That(motor.transform.position.y,Is.GreaterThan(-1.1f),"Cannot fall through stairs.");}
 Assert.That(motor.transform.position.z,up?Is.LessThan(56.0f):Is.GreaterThan(59.9f),"Traverses full staircase without a rail snag.");records.Add((up?"up":"down")+":"+motor.transform.position);
 }
 motor.Warp(new Vector3(-59.6f,.66f,56.1f),270);Physics.SyncTransforms();
 for(int i=0;i<75;i++){motor.Controller.Move(new Vector3(-.05f,-.12f,0));yield return null;Assert.That(motor.transform.position.y,Is.GreaterThan(.45f));}
 Assert.That(motor.transform.position.x,Is.LessThan(-63.1f));records.Add("landing:"+motor.transform.position);
 Assert.That(flow.stairDoor.persistentId,Is.EqualTo("bd.door.stairs"));Assert.That(flow.stairDoor.locked,Is.True);Assert.That(game.interaction.inventory.Contains(BeforeDemolitionFourStudentsFlow.StudentItemIds[0]),Is.False);
 File.WriteAllText("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/Passe20261103/west-walking.txt",string.Join("\n",records));
 }finally{motor.enabled=enabled;}
 }
 }
}
#endif
