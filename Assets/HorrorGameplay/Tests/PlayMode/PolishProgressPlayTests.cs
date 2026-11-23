// Verifica .

#if UNITY_EDITOR
using System;using System.Collections;using System.Linq;using System.Reflection;using NUnit.Framework;using UnityEditor;using UnityEngine;using UnityEngine.TestTools;
namespace HorrorGame.Tests {
[Category("Polish20261103")]
public sealed class PolishProgressPlayTests {
 BeforeDemolitionChapterFixture f;UnityEngine.Object[] transient=Array.Empty<UnityEngine.Object>();
 [UnityTearDown]public IEnumerator End(){if(f!=null)yield return f.Dispose();foreach(var x in transient)if(x)UnityEngine.Object.Destroy(x);f=null;}
 [UnityTest]public IEnumerator EmailOrderNeedsVisibleDwellAndRaisesOneCompletionPerEmail(){
 f=new BeforeDemolitionChapterFixture(BeforeDemolitionCampaign.OfficeChapterId,Array.Empty<StoryMilestoneDefinition>());
 var c=ScriptableObject.CreateInstance<EmailCatalog>();c.messages=EmailCatalog.BeforeDemolitionContent();transient=new UnityEngine.Object[]{c};
 var inbox=f.Child("Inbox").AddComponent<EmailInboxController>();inbox.minimumReadingSeconds=2.5f;inbox.Configure(c);inbox.ExternalKeyboardInput=true;
 yield return f.Activate();Assert.That(inbox.Open(EmailDevice.Desktop),Is.True);int completed=0;inbox.ReadingCompleted+=id=>completed++;
 Assert.That(inbox.SelectMessage("email.07"),Is.False);
 for(int i=1;i<=7;i++){string id="email.0"+i;Assert.That(inbox.SelectMessage(id),Is.True);Assert.That(inbox.AdvanceReading(20,false),Is.False);Assert.That(inbox.State.IsRead(id),Is.False);Assert.That(inbox.AdvanceReading(2),Is.False);Assert.That(inbox.AdvanceReading(.5f),Is.True);Assert.That(inbox.AdvanceReading(5),Is.False);}
 Assert.That(completed,Is.EqualTo(7));Assert.That(inbox.CaptureState().readIds.Count,Is.EqualTo(7));inbox.Close();
 }
 [UnityTest]public IEnumerator PhoneUnreadBlocksNormallyButMissingDeliveryGetsFallback(){
 const string signal="fixture.gate";f=new BeforeDemolitionChapterFixture(BeforeDemolitionCampaign.FourChapterId,new[]{new StoryMilestoneDefinition{id=signal}});
 var cat=ScriptableObject.CreateInstance<PhoneMessageCatalog>();cat.messages=new[]{new PhoneMessageDefinition{id="route.fixture",trigger=PhoneTrigger.RouteFour,text="Vá à sala de artes."}};
 var config=ScriptableObject.CreateInstance<PhoneProgressionConfig>();config.gates=new[]{new PhoneProgressionConfig.Gate{chapterId=BeforeDemolitionCampaign.FourChapterId,signal=signal,messageId="route.fixture",fallbackObjective="Vá à sala de artes."}};
 transient=new UnityEngine.Object[]{cat,config};var phone=f.root.AddComponent<PhoneMessages>();phone.game=f.game;phone.catalog=cat;phone.enabled=false;
 var service=f.root.AddComponent<PhoneProgression>();service.game=f.game;service.phone=phone;service.config=config;
 yield return f.Activate();phone.State.received.Add(new PhoneMessageReceipt{id="route.fixture",read=false});
 Assert.That(PhoneProgression.CanAdvance(f.game,signal),Is.False);Assert.That(service.PendingObjective,Is.EqualTo("Leia a mensagem no celular."));
 typeof(PhoneProgression).GetField("missingAge",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(service,999f);
 typeof(PhoneProgression).GetField("pollAge",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(service,1f);
 service.SendMessage("Update");Assert.That(PhoneProgression.CanAdvance(f.game,signal),Is.False,"Unread delivered messages never time out.");
 phone.State.received[0].read=true;Assert.That(PhoneProgression.CanAdvance(f.game,signal),Is.True);
 phone.State.received.Clear();config.gates[0].messageId="missing-definition";PhoneProgression.CanAdvance(f.game,signal);
 typeof(PhoneProgression).GetField("missingAge",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(service,999f);typeof(PhoneProgression).GetField("pollAge",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(service,1f);
 service.SendMessage("Update");Assert.That(PhoneProgression.CanAdvance(f.game,signal),Is.True);Assert.That(service.PendingObjective,Is.EqualTo("Vá à sala de artes."));
 }
 [UnityTest]public IEnumerator ObjectivesCountRealItemsAndHideDuringModalAndPause(){
 const string signal="fixture.collect";f=new BeforeDemolitionChapterFixture(BeforeDemolitionCampaign.FourChapterId,new[]{new StoryMilestoneDefinition{id=signal}});
 var h=f.Child("Hud");h.AddComponent<Canvas>();var hud=h.AddComponent<GameHUD>();hud.enabled=false;hud.game=f.game;hud.player=f.player;hud.objective=f.Label("Objective",h.transform);hud.subtitle=f.Label("Subtitle",h.transform);hud.notification=f.Label("Notification",h.transform);hud.objectiveGroup=h.AddComponent<CanvasGroup>();hud.pausePanel=f.Child("Pause",h.transform);hud.optionsPanel=f.Child("Options",h.transform);hud.notePanel=f.Child("Note",h.transform);hud.inventoryPanel=f.Child("Inventory",h.transform);hud.gameOverPanel=f.Child("GameOver",h.transform);hud.deathCurtain=f.Child("Death",h.transform).AddComponent<CanvasGroup>();f.game.hud=hud;
 var config=ScriptableObject.CreateInstance<ChapterObjectives>();config.chapterId=BeforeDemolitionCampaign.FourChapterId;config.entries=new[]{new ObjectiveEntry{id="collect",title="Pegar os objetos",requiredNextSignals=new[]{signal},subitems=new[]{new ObjectiveSubitem{label="Boné",requiredItemId="fixture.cap"},new ObjectiveSubitem{label="Caderno",requiredItemId="fixture.book"}}}};transient=new UnityEngine.Object[]{config};
 var service=f.root.AddComponent<ObjectiveService>();service.game=f.game;service.chapters=new[]{config};yield return f.Activate();service.RefreshNow();Assert.That(service.SubitemCount,Is.EqualTo(2));Assert.That(service.CompletedSubitemCount,Is.Zero);Assert.That(service.Visible,Is.True);
 f.inventory.Add(new InventoryItem{id="fixture.cap"});yield return null;service.RefreshNow();Assert.That(service.CompletedSubitemCount,Is.EqualTo(1));Assert.That(service.IsSubitemComplete(0),Is.True);
 f.game.BeginInspection();yield return null;Assert.That(service.Visible,Is.False);f.game.EndInspection();f.game.Pause();yield return null;Assert.That(service.Visible,Is.False);f.game.Resume();yield return null;Assert.That(service.Visible,Is.True);
 }
}
public sealed partial class BeforeDemolitionRoadSceneTests {
 [UnityTest,Category("Polish20261103Native")]
 public IEnumerator TwentyReconstructedDrivesKeepPhoneAnchoredAndRadioPersistsUntilEntry(){
 var initial=flow.phone.localPosition;var state=flow.State;state.phase=RoadRadioPhase.Song;state.phaseAge=2;state.hauntedPowerTried=true;
 for(int iteration=0;iteration<20;iteration++){
  for(int step=0;step<=10;step++){flow.driver.SetExternalProgress(step/10f);flow.radio.Vibrate();yield return null;Assert.That(Vector3.Distance(flow.phone.localPosition,initial),Is.LessThan(.001f),"Phone root moved on run "+iteration);}
 }
 Assert.That(flow.driver.carRoot.InverseTransformPoint(flow.driver.seatedPose.position).x,Is.LessThan(0));
 foreach(var rb in flow.phone.GetComponentsInChildren<Rigidbody>(true)){Assert.That(rb.isKinematic,Is.True);Assert.That(rb.useGravity,Is.False);}
 flow.radio.SetVolume(-10);Assert.That(flow.radio.Volume,Is.EqualTo(.18f));flow.radio.SetVolume(10);Assert.That(flow.radio.Volume,Is.EqualTo(1));
 flow.radio.ApplyPhase(RoadRadioPhase.Parked,0,true);yield return null;
 Assert.That(flow.radio.RadioSource.clip,Is.SameAs(flow.radio.nurserySong));Assert.That(flow.radio.RadioSource.isPlaying,Is.True);Assert.That(flow.radio.RadioSource.spatialBlend,Is.EqualTo(1));
 flow.radio.BeginEntryFade();flow.radio.Tick(true,1);Assert.That(flow.radio.EntryFaded,Is.True);Assert.That(flow.radio.RadioSource.isPlaying,Is.False);
 }
}
}
#endif
