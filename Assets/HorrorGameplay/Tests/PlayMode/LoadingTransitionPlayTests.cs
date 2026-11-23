// Verifica .

#if UNITY_EDITOR
using System;using System.Collections;using System.Linq;using System.Reflection;using NUnit.Framework;using UnityEngine;using UnityEngine.TestTools;
namespace HorrorGame.Tests{
 static class LoadingSeedProof{
  public static void Validate(GameSession game,bool road){var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("HorrorGame.LoadingBenchmark")).First(t=>t!=null);var host=new GameObject("Disposable benchmark seed validation");try{var runner=host.AddComponent(type);var data=(SaveData)type.GetMethod("ExitSeed",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(runner,new object[]{game,road});game.ShowStoryScreen();Assert.That(game.save.RestoreSnapshot(data),Is.True,"Seed must pass the production validator.");Assert.That(game.save.StageChapterCarry(road?"SchoolCEEP":"RoadJourney"),Is.True,"Actual handoff capture must remain valid.");SaveSystem.ClearChapterCarry();}finally{UnityEngine.Object.Destroy(host);}}
 }
 public sealed partial class BeforeDemolitionOfficeSceneTests{
  [UnityTest,Category("LoadingSeeds")]public IEnumerator LoadingBenchmarkOfficeExitSeedPassesProductionValidation(){LoadingSeedProof.Validate(game,false);yield return null;}
 }
 public sealed partial class BeforeDemolitionRoadSceneTests{
  [UnityTest,Category("LoadingSeeds")]public IEnumerator LoadingBenchmarkRoadExitSeedPassesProductionValidation(){LoadingSeedProof.Validate(game,true);yield return null;}
 }
}
#endif
