// Verifica SetUp, EveryCheckpointPreservesTheOfficeAndRoadContext, ImpossibleOrCrossStoryCheckpointsAreRejected, ClosingAnUnfinishedTapeCanKeepTheObjectWithoutGrantingItsCompletion, MissingStateFieldsFailClosed, TearDown.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace HorrorGame.Tests
{
    [Category("BeforeDemolitionSchoolSave")]
    public sealed class BeforeDemolitionSchoolSaveTests
    {
        string directory,path; SaveData data; BeforeDemolitionSchoolState state; EmailCatalog catalog;
        [SetUp]public void SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"Whispers-SchoolSave-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);path=Path.Combine(directory,"school.json");
            catalog=ScriptableObject.CreateInstance<EmailCatalog>();catalog.messages=EmailCatalog.BeforeDemolitionContent();var emails=EmailInboxState.Create(catalog);emails.Deliver(catalog,"email.08");foreach(string id in new[]{"email.01","email.06","email.07"})emails.MarkRead(catalog,id);
            state=new BeforeDemolitionSchoolState{emails=emails};data=new SaveData{scene="SchoolCEEP",position=new Vector3(0,.7f,21),battery=240,stamina=100,
                narrative=new NarrativeProgress{storyId=BeforeDemolitionCampaign.StoryId,storyRevision=3,chapterId=BeforeDemolitionCampaign.SchoolChapterId,
                    milestones=new List<string>{BeforeDemolitionCampaign.ReadySignal,BeforeDemolitionCampaign.ExitSignal},completedChapters=new List<string>{BeforeDemolitionCampaign.OfficeChapterId,BeforeDemolitionCampaign.RoadChapterId}},
                inventory=new List<InventoryItem>{new InventoryItem{id=BeforeDemolitionCampaign.WhistleItemId},new InventoryItem{id=BeforeDemolitionRoadFlow.EmailDeliveryItemId}}};
            data.narrative.milestones.AddRange(BeforeDemolitionCampaign.RoadSignals);foreach(string id in emails.readIds)data.inventory.Add(new InventoryItem{id="bd.note."+id,note=true});
        }
        void At(int count)
        {
            data.narrative.milestones.AddRange(BeforeDemolitionCampaign.SchoolSignals.Take(count));
            if(count>=4)data.inventory.Add(new InventoryItem{id=BeforeDemolitionSchoolFlow.KeyId});if(count>=6)data.inventory.Add(new InventoryItem{id=BeforeDemolitionSchoolFlow.ReportId});if(count>=7)data.inventory.Add(new InventoryItem{id=BeforeDemolitionSchoolFlow.TapeHeardId});
            if(count==8){data.narrative.completedChapters.Add(BeforeDemolitionCampaign.SchoolChapterId);data.narrative.pendingChapterId="bd.four-students";}
        }
        bool Read()
        {
            data.objects.RemoveAll(o=>o.id==BeforeDemolitionSchoolFlow.StateId);data.objects.Add(new SavedObject{id=BeforeDemolitionSchoolFlow.StateId,json=JsonUtility.ToJson(state)});
            File.WriteAllText(path,JsonUtility.ToJson(data));return SaveSystem.TryRead(path,out _);
        }
        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)][TestCase(6)][TestCase(7)][TestCase(8)]
        public void EveryCheckpointPreservesTheOfficeAndRoadContext(int stage){At(stage);Assert.That(Read(),Is.True);}
        [TestCase("no-key")][TestCase("early-key")][TestCase("no-report")][TestCase("no-tape-proof")][TestCase("early-call")]
        [TestCase("wrong-prefix")][TestCase("wrong-scene")][TestCase("duplicate-item")][TestCase("missing-emails")][TestCase("missing-whistle")]
        [TestCase("wrong-next")][TestCase("missing-office-read")][TestCase("duplicate-object")]
        public void ImpossibleOrCrossStoryCheckpointsAreRejected(string fault)
        {
            At(fault=="early-key"||fault=="early-call"?0:fault=="wrong-next"?8:7);
            switch(fault){
                case "no-key":data.inventory.RemoveAll(i=>i.id==BeforeDemolitionSchoolFlow.KeyId);break;
                case "early-key":data.inventory.Add(new InventoryItem{id=BeforeDemolitionSchoolFlow.KeyId});break;
                case "no-report":data.inventory.RemoveAll(i=>i.id==BeforeDemolitionSchoolFlow.ReportId);break;
                case "no-tape-proof":data.inventory.RemoveAll(i=>i.id==BeforeDemolitionSchoolFlow.TapeHeardId);break;
                case "early-call":state.phoneAnswered=true;break;
                case "wrong-prefix":data.narrative.milestones[3]="old-school";break;
                case "wrong-scene":data.scene="Chapter1Office";break;
                case "duplicate-item":data.inventory.Add(data.inventory[0]);break;
                case "missing-emails":state.emails=null;break;
                case "missing-whistle":data.inventory.RemoveAll(i=>i.id==BeforeDemolitionCampaign.WhistleItemId);break;
                case "wrong-next":data.narrative.pendingChapterId="memories";break;
                case "missing-office-read":state.emails.readIds.Remove("email.06");break;
                case "duplicate-object":data.objects.AddRange(new[]{new SavedObject{id="test",json="{}"},new SavedObject{id="test",json="{}"}});break;
            }Assert.That(Read(),Is.False,fault);
        }
        [Test]public void ClosingAnUnfinishedTapeCanKeepTheObjectWithoutGrantingItsCompletion()
        {At(6);data.inventory.Add(new InventoryItem{id=BeforeDemolitionSchoolFlow.TapeId,note=true});Assert.That(Read(),Is.True);Assert.That(data.inventory.Any(i=>i.id==BeforeDemolitionSchoolFlow.TapeHeardId),Is.False);}
        [Test]public void MissingStateFieldsFailClosed(){Assert.That(BeforeDemolitionSchoolState.Parse("{}").ValidFields,Is.False);}
        [TearDown]public void TearDown(){if(catalog)UnityEngine.Object.DestroyImmediate(catalog);foreach(string file in Directory.GetFiles(directory))File.Delete(file);Directory.Delete(directory);}
    }
}
