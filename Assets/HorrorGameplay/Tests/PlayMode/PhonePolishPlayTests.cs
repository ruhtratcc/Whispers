// Verifica .

#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    public sealed partial class BeforeDemolitionRoadSceneTests
    {
        const string PolishVisual="C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/AudioPass20261029/Visual";
        [UnityTest,Category("PhonePolish"),Timeout(120000)]
        public IEnumerator WindowDragsClampsPersistsAndControlsWorkWithAdaptiveBubbles()
        {
            string key=PhoneMessagesView.PositionKey+".Test";
            bool hadX=PlayerPrefs.HasKey(key+"X"),hadY=PlayerPrefs.HasKey(key+"Y");float oldX=PlayerPrefs.GetFloat(key+"X"),oldY=PlayerPrefs.GetFloat(key+"Y");
            try
            {
                Fast(20);flow.ignoredBuzzSeconds=flow.incomingCallSeconds=100;
                yield return Until(()=>flow.State.phase==RoadRadioPhase.Song,"Road messages arrive before opening");
                var phone=PhoneMessages.For(game);yield return OpenMessages();var view=phone.View;
                view.ResetWindowPosition();
                yield return null;yield return null;yield return PolishShot("initial-window");
                Directory.CreateDirectory(PolishVisual);File.WriteAllText(Path.Combine(PolishVisual,"geometry.txt"),"Window="+view.Window.anchoredPosition+" size="+view.Window.rect+" canvas="+((RectTransform)view.transform).rect+" contact="+view.ContactButton.transform.position+" active="+view.ContactButton.IsActive());
                var labels=view.Window.GetComponentsInChildren<TMP_Text>();
                Assert.That(labels.Any(t=>t.text==phone.catalog.number||t.text.Contains("76%")||t.text.Contains("Sinal:")||t.text.Contains("Fechar [")),Is.False);
                Click(view.ContactButton);yield return null;Assert.That(view.Window.GetComponentsInChildren<TMP_Text>().Any(t=>t.text==phone.catalog.number),Is.True);
                Click(view.ContactButton);yield return null;
                var content=view.ConversationScroll.content;
                var small=(RectTransform)content.Find("road.03/Recebida");var large=(RectTransform)content.Find("road.04/Recebida");
                Assert.That(small.rect.width,Is.LessThan(large.rect.width));Assert.That(large.rect.width,Is.LessThanOrEqualTo((view.ConversationScroll.GetComponent<RectTransform>().rect.width-16)*.75f));
                foreach(var t in content.GetComponentsInChildren<TMP_Text>()){t.ForceMeshUpdate();Assert.That(t.textInfo.characterInfo.Take(t.textInfo.characterCount).Any(c=>c.isVisible),Is.True,t.text);}
                var initial=view.Window.anchoredPosition;var point=RectTransformUtility.WorldToScreenPoint(null,view.Header.position);
                var e=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left,pointerPressRaycast=new RaycastResult{gameObject=view.Header.gameObject}};
                ExecuteEvents.Execute(view.Header.gameObject,e,ExecuteEvents.beginDragHandler);Assert.That(view.ConversationScroll.enabled,Is.False);
                e.position+=new Vector2(-123.4f,28.7f);ExecuteEvents.Execute(view.Header.gameObject,e,ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(view.Header.gameObject,e,ExecuteEvents.endDragHandler);Assert.That(view.ConversationScroll.enabled,Is.True);
                var moved=view.Window.anchoredPosition;Assert.That(moved,Is.Not.EqualTo(initial));Assert.That(moved.x,Is.EqualTo(Mathf.Round(moved.x)));Assert.That(phone.State.composerUsed,Is.False);
                Click(view.MinimizeButton);yield return null;Assert.That(phone.IsOpen,Is.False);Assert.That(phone.OpenDirect(),Is.True);yield return null;Assert.That(view.Window.anchoredPosition,Is.EqualTo(moved));
                phone.Close();UnityEngine.Object.Destroy(view.gameObject);yield return null;Assert.That(phone.OpenDirect(),Is.True);yield return null;view=phone.View;Assert.That(view.Window.anchoredPosition,Is.EqualTo(moved),"New view restores the persisted preference.");
                e.position=point;e.pointerPressRaycast=new RaycastResult{gameObject=view.Header.gameObject};ExecuteEvents.Execute(view.Header.gameObject,e,ExecuteEvents.beginDragHandler);
                e.position=new Vector2(100000,-100000);ExecuteEvents.Execute(view.Header.gameObject,e,ExecuteEvents.dragHandler);ExecuteEvents.Execute(view.Header.gameObject,e,ExecuteEvents.endDragHandler);
                var corners=new Vector3[4];view.Header.GetWorldCorners(corners);Assert.That(corners.Max(c=>c.y),Is.GreaterThan(0));Assert.That(corners.Min(c=>c.x),Is.LessThan(Screen.width));
                Click(view.ResetButton);yield return null;Assert.That(Vector2.Distance(view.Window.anchoredPosition,initial),Is.LessThan(.01f),"Reset restores the position within subpixel Canvas rounding.");
                yield return PolishShot("adaptive-window");Click(view.CloseButton);yield return null;Assert.That(phone.IsOpen,Is.False);Assert.That(game.CanControl,Is.True);
            }
            finally
            {
                if(hadX)PlayerPrefs.SetFloat(key+"X",oldX);else PlayerPrefs.DeleteKey(key+"X");if(hadY)PlayerPrefs.SetFloat(key+"Y",oldY);else PlayerPrefs.DeleteKey(key+"Y");PlayerPrefs.Save();
            }
        }
        [UnityTest,Category("PhonePolish"),Timeout(120000)]
        public IEnumerator ChoicesBranchAndAutomaticBlueMessagesSurviveCheckpointWithoutRepeating()
        {
            Fast(20);flow.ignoredBuzzSeconds=flow.incomingCallSeconds=100;var phone=PhoneMessages.For(game);
            yield return Until(()=>flow.State.phase==RoadRadioPhase.Song,"Road messages arrive before opening");yield return OpenMessages();
            yield return null;yield return null;
            Assert.That(phone.AvailableReply.id,Is.EqualTo("road"));var original=phone.Capture();
            for(int option=0;option<2;option++)
            {
                if(option>0){phone.Restore(original);yield return null;Assert.That(phone.OpenDirect(),Is.True,"Reopen for the alternative choice: "+game.State);yield return null;}
                var rect=phone.View.Window.Find("Tela/Respostas de João/Resposta "+option);Assert.That(rect,Is.Not.Null);
                Click(rect.GetComponent<UnityEngine.UI.Button>());yield return null;
                string sent="reply.road."+option+".sent",answer="reply.road."+option+".answer";
                Assert.That(phone.State.received.Any(m=>m.id==sent&&m.sendingSeconds>0),Is.True);Assert.That(phone.ChooseReply("road",option),Is.False);
                Assert.That(phone.View.Window.GetComponentsInChildren<TMP_Text>().Any(t=>t.text==phone.catalog.sending),Is.True);
                yield return new WaitForSeconds(.7f);Assert.That(phone.View.Window.GetComponentsInChildren<TMP_Text>().Any(t=>t.text==phone.catalog.delivered),Is.True);
                yield return new WaitForSeconds(.7f);Assert.That(phone.State.received.Any(m=>m.id==answer),Is.True);
                var blue=(RectTransform)phone.View.ConversationScroll.content.Find(sent+"/Enviada");Assert.That(blue.anchorMax.x,Is.EqualTo(1));Assert.That(blue.GetComponentInChildren<TMP_Text>().color,Is.EqualTo(Color.white));
            }
            phone.Trigger(PhoneTrigger.Secretary);yield return new WaitForSeconds(8.1f);
            Assert.That(phone.State.received.Any(m=>m.id=="reply.secretary.0.sent"),Is.True);yield return new WaitForSeconds(1.3f);
            Assert.That(phone.State.received.Any(m=>m.id=="reply.secretary.0.answer"),Is.True);
            phone.Trigger(PhoneTrigger.LoopFirst);yield return null;Assert.That(phone.State.received.Any(m=>m.id=="reply.loop.0.sent"),Is.True);
            phone.Trigger(PhoneTrigger.Caio);phone.Trigger(PhoneTrigger.Mirror);yield return null;
            Assert.That(phone.AvailableReply.id,Is.EqualTo("objects"));Assert.That(phone.ChooseReply("objects",1),Is.True);Assert.That(phone.ChooseReply("mirror",0),Is.True);
            Assert.That(game.save.Save(),Is.True);Assert.That(game.save.TryReadCurrent(out var saved),Is.True);int count=phone.State.received.Count;
            phone.Restore(null);Assert.That(game.save.RestoreSnapshot(saved),Is.True);game.narrative.InitializeAfterSession(true);yield return null;
            Assert.That(phone.State.choices.Count,Is.EqualTo(5));Assert.That(phone.State.received.Count,Is.EqualTo(count));Assert.That(phone.OpenDirect(),Is.True);
            yield return new WaitForSeconds(1.4f);Assert.That(phone.State.received.Count(m=>m.id=="reply.loop.0.sent"),Is.EqualTo(1));
            Assert.That(phone.State.received.Any(m=>m.id=="reply.objects.1.answer")&&phone.State.received.Any(m=>m.id=="reply.mirror.0.answer"),Is.True);
            yield return PolishShot("blue-conversation");
        }
        IEnumerator PolishShot(string name)
        {
            Directory.CreateDirectory(PolishVisual);string path=Path.Combine(PolishVisual,name+".png");if(File.Exists(path))File.Delete(path);yield return null;yield return null;
            ScreenCapture.CaptureScreenshot(path);yield return Until(()=>File.Exists(path)&&new FileInfo(path).Length>0,"Phone polish screenshot",5);
        }
    }
}
#endif

