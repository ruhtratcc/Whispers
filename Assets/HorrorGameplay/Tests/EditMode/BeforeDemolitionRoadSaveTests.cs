// Verifica SetUp, AllRadioPhasesRoundTripWithoutLosingOfficeContext, ArrivalAndOrderedPlayerActionsHaveValidSnapshots, InvalidRoadSnapshotsAreRejectedBeforeRestore, DeliveryAndChapterCompletionCannotDisagreeWithWorldState, FailedHauntedPowerAttemptRoundTripsWithoutReplayingTheAttempt, FailedHauntedPowerCannotExistBeforeTheStrangeSong, MissingStateFieldsFailClosed, ProductionRadioHasARealTwoSecondSilenceAndAtLeastOneMinuteOfNews, TearDown.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HorrorGame.Tests
{
    [Category("BeforeDemolitionRoadSave")]
    public sealed class BeforeDemolitionRoadSaveTests
    {
        string directory, path;
        EmailCatalog catalog;
        SaveData data;
        BeforeDemolitionRoadState state;
        [SetUp] public void SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"Whispers-RoadSave-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);path=Path.Combine(directory,"road.json");
            catalog=ScriptableObject.CreateInstance<EmailCatalog>();catalog.messages=EmailCatalog.BeforeDemolitionContent();
            var emails=EmailInboxState.Create(catalog);foreach(string id in new[]{"email.01","email.06","email.07"})emails.MarkRead(catalog,id);
            state=new BeforeDemolitionRoadState{emails=emails,phase=RoadRadioPhase.News,phaseDuration=75};
            data=new SaveData{scene="RoadJourney",position=new Vector3(-.43f,-.37f,-.28f),battery=240,stamina=100,
                narrative=new NarrativeProgress{storyId=BeforeDemolitionCampaign.StoryId,storyRevision=3,chapterId=BeforeDemolitionCampaign.RoadChapterId,pendingChapterId="",
                    milestones=new List<string>{BeforeDemolitionCampaign.ReadySignal,BeforeDemolitionCampaign.ExitSignal},completedChapters=new List<string>{BeforeDemolitionCampaign.OfficeChapterId}},
                inventory=new List<InventoryItem>{new InventoryItem{id=BeforeDemolitionCampaign.WhistleItemId}},objects=new List<SavedObject>()};
            foreach(string id in emails.readIds)data.inventory.Add(new InventoryItem{id="bd.note."+id,title=id,text=catalog.Find(id).PlainText,note=true});
        }
        void SerializeState(){data.objects.RemoveAll(o=>o.id==BeforeDemolitionRoadFlow.StateId);data.objects.Add(new SavedObject{id=BeforeDemolitionRoadFlow.StateId,json=JsonUtility.ToJson(state)});}
        bool Read(){SerializeState();File.WriteAllText(path,JsonUtility.ToJson(data));return SaveSystem.TryRead(path,out _);}
        void SetPhase(RoadRadioPhase phase,int roadMilestones=-1)
        {
            state.phase=phase;state.phaseAge=0;state.phaseDuration=phase==RoadRadioPhase.Parked?0:phase==RoadRadioPhase.News?75:12;
            bool eighth=phase>=RoadRadioPhase.Song;
            if(eighth){state.emails.Deliver(catalog,"email.08");data.inventory.Add(new InventoryItem{id=BeforeDemolitionRoadFlow.EmailDeliveryItemId});}
            int count=roadMilestones>=0?roadMilestones:phase==RoadRadioPhase.Parked?2:eighth?1:0;
            data.narrative.milestones.AddRange(BeforeDemolitionCampaign.RoadSignals.Take(count));
            if(count==5){data.narrative.completedChapters.Add(BeforeDemolitionCampaign.RoadChapterId);data.narrative.pendingChapterId="bd.secretary-library";}
        }
        [TestCase(RoadRadioPhase.News)] [TestCase(RoadRadioPhase.FirstAnnouncement)] [TestCase(RoadRadioPhase.SecondAnnouncement)]
        [TestCase(RoadRadioPhase.ThirdAnnouncement)] [TestCase(RoadRadioPhase.Silence)] [TestCase(RoadRadioPhase.Song)] [TestCase(RoadRadioPhase.Parked)]
        public void AllRadioPhasesRoundTripWithoutLosingOfficeContext(RoadRadioPhase phase)
        {SetPhase(phase);Assert.That(Read(),Is.True);Assert.That(SaveSystem.TryRead(path,out var loaded),Is.True);Assert.That(loaded.inventory.Any(i=>i.id==BeforeDemolitionCampaign.WhistleItemId),Is.True);}
        [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void ArrivalAndOrderedPlayerActionsHaveValidSnapshots(int count){SetPhase(RoadRadioPhase.Parked,count);Assert.That(Read(),Is.True);}
        [TestCase("identity")] [TestCase("revision")] [TestCase("scene")] [TestCase("whistle")] [TestCase("office-history")]
        [TestCase("office-signals")] [TestCase("required-email")] [TestCase("read-without-note")] [TestCase("future-delivery")]
        [TestCase("phase-range")] [TestCase("nan-age")] [TestCase("infinite-duration")] [TestCase("age-overflow")]
        [TestCase("answered-without-call")] [TestCase("call-before-song")] [TestCase("duplicate-milestone")]
        public void InvalidRoadSnapshotsAreRejectedBeforeRestore(string fault)
        {
            switch(fault)
            {
                case "identity":data.narrative.storyId="other-story";break;
                case "revision":data.narrative.storyRevision=4;break;
                case "scene":data.scene="SchoolCEEP";break;
                case "whistle":data.inventory.RemoveAll(i=>i.id==BeforeDemolitionCampaign.WhistleItemId);break;
                case "office-history":data.narrative.completedChapters.Clear();break;
                case "office-signals":data.narrative.milestones.Reverse();break;
                case "required-email":state.emails.readIds.Remove("email.06");break;
                case "read-without-note":data.inventory.RemoveAll(i=>i.id=="bd.note.email.07");break;
                case "future-delivery":state.emails.Deliver(catalog,"email.08");break;
                case "phase-range":state.phase=(RoadRadioPhase)99;break;
                case "nan-age":state.phaseAge=float.NaN;break;
                case "infinite-duration":state.phaseDuration=float.PositiveInfinity;break;
                case "age-overflow":state.phaseAge=76;break;
                case "answered-without-call":state.callAnswered=true;break;
                case "call-before-song":state.callOffered=true;break;
                case "duplicate-milestone":data.narrative.milestones.Add(BeforeDemolitionCampaign.ExitSignal);break;
            }
            Assert.That(Read(),Is.False,fault);
        }
        [TestCase("marker")] [TestCase("radio-signal")] [TestCase("parked-signal")] [TestCase("next-chapter")]
        public void DeliveryAndChapterCompletionCannotDisagreeWithWorldState(string fault)
        {
            SetPhase(fault=="next-chapter"?RoadRadioPhase.Parked:RoadRadioPhase.Song,fault=="next-chapter"?5:1);
            if(fault=="marker")data.inventory.RemoveAll(i=>i.id==BeforeDemolitionRoadFlow.EmailDeliveryItemId);
            if(fault=="radio-signal")data.narrative.milestones.Remove("bd.road.radio-changed");
            if(fault=="parked-signal")data.narrative.milestones.Add("bd.road.car-parked");
            if(fault=="next-chapter")data.narrative.pendingChapterId="old-school";
            Assert.That(Read(),Is.False);
        }
        [Test]public void FailedHauntedPowerAttemptRoundTripsWithoutReplayingTheAttempt()
        {
            SetPhase(RoadRadioPhase.Song);state.hauntedPowerTried=true;Assert.That(Read(),Is.True);
            Assert.That(SaveSystem.TryRead(path,out var restored),Is.True);
            Assert.That(BeforeDemolitionRoadState.Parse(restored.objects.Single(o=>o.id==BeforeDemolitionRoadFlow.StateId).json).hauntedPowerTried,Is.True);
        }
        [Test]public void FailedHauntedPowerCannotExistBeforeTheStrangeSong()
        {state.hauntedPowerTried=true;Assert.That(Read(),Is.False);}
        [Test]public void MissingStateFieldsFailClosed(){Assert.That(BeforeDemolitionRoadState.Parse("{}").ValidFields,Is.False);}
        [Test]public void ProductionRadioHasARealTwoSecondSilenceAndAtLeastOneMinuteOfNews()
        {
            var root=new GameObject("Radio timings test");try{var radio=root.AddComponent<BeforeDemolitionRoadRadio>();Assert.That(radio.Duration(RoadRadioPhase.Silence),Is.EqualTo(2));Assert.That(radio.Duration(RoadRadioPhase.News),Is.InRange(60,120));}
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [TearDown]public void TearDown(){if(catalog)UnityEngine.Object.DestroyImmediate(catalog);foreach(string f in Directory.GetFiles(directory))File.Delete(f);Directory.Delete(directory);}
    }
}
