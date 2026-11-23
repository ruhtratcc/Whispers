// Verifica MessageReceiptsAndPendingTypingRoundTripWithoutAliasing, InvalidAndDuplicateReceiptsAreRejected, EditableCatalogContainsEveryRequestedLineAndOriginalIdentity, ReplyCatalogReferencesAreCompleteAndChoicesAreAdditiveAndValidated, PickupStateIsAdditiveAndPreservedWithoutBreakingLegacyVersions.

using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.Tests
{
    [Category("PhoneMessagesData")]
    public sealed class PhoneMessageDataTests
    {
        [Test] public void MessageReceiptsAndPendingTypingRoundTripWithoutAliasing()
        {
            var original=new PhoneMessageState();original.triggers.Add(PhoneTrigger.Secretary);
            original.received.Add(new PhoneMessageReceipt{id="secretary.hello",minute=1627,read=true,announced=true});
            original.pending.Add(new PendingPhoneMessage{id="secretary.who",seconds=5.5f});original.composerUsed=true;
            var copy=original.Copy();Assert.That(copy.Valid,Is.True);Assert.That(copy.pending[0].seconds,Is.EqualTo(5.5f));
            Assert.That(copy.received[0].read&&copy.composerUsed,Is.True);copy.received[0].read=false;
            Assert.That(original.received[0].read,Is.True);
            var old=JsonUtility.FromJson<SaveData>("{\"version\":2}");Assert.That(old.phone==null||old.phone.Valid,Is.True);
        }
        [Test] public void InvalidAndDuplicateReceiptsAreRejected()
        {
            var state=new PhoneMessageState();Assert.That(state.Valid,Is.True);
            state.received.Add(new PhoneMessageReceipt{id="road.01",minute=1627});
            state.pending.Add(new PendingPhoneMessage{id="road.01",seconds=1});Assert.That(state.Valid,Is.False);
            state.pending.Clear();state.received[0].minute=-1;Assert.That(state.Valid,Is.False);
            state.received.Clear();state.pending.Add(new PendingPhoneMessage{id="road.04",seconds=float.NaN});Assert.That(state.Valid,Is.False);
            state.pending.Clear();state.triggers.Add((PhoneTrigger)99);Assert.That(state.Valid,Is.False);
        }
        [Test] public void EditableCatalogContainsEveryRequestedLineAndOriginalIdentity()
        {
            var data=AssetDatabase.LoadAssetAtPath<PhoneMessageCatalog>("Assets/HorrorGameplay/Data/PhoneMessages/Messages.asset");
            Assert.That(data,Is.Not.Null);Assert.That(data.messages.Count(m=>!m.replyOnly),Is.EqualTo(30));
            Assert.That(data.messages.Select(m=>m.id).Distinct().Count(),Is.EqualTo(data.messages.Length));
            Assert.That(data.Find("road.01").text,Is.EqualTo("A porta da secretaria está aberta."));
            Assert.That(data.Find("road.03").text,Is.EqualTo("não vem"));
            // Humanização: Helena mantém o tratamento formal; ID e gatilho continuam os mesmos.
            Assert.That(data.Find("road.ignored").text,Is.EqualTo("O senhor sempre deixa para depois."));
            Assert.That(data.Find("library.warning").onlyWhenOpened,Is.True);
            Assert.That(data.Find("library.warning").text,Is.EqualTo("não responde ela"));
            Assert.That(data.Find("ending.refuse").text,Is.EqualTo("O senhor ainda pode ficar."));
            Assert.That(data.Find("ending.sign").text,Is.EqualTo("Obrigada por ficar, seu João."));
            Assert.That(data.messages.Count(m=>m.child),Is.EqualTo(6));Assert.That(data.messages.Count(m=>m.photo),Is.EqualTo(2));
            Assert.That(data.sender,Is.EqualTo("Número desconhecido"));Assert.That(data.number,Is.EqualTo("(00) 00307-0307"));
            Assert.That(data.font&&data.childFont&&data.icon&&data.roundedPanel,Is.True);
        }
        [Test] public void ReplyCatalogReferencesAreCompleteAndChoicesAreAdditiveAndValidated()
        {
            var data=AssetDatabase.LoadAssetAtPath<PhoneMessageCatalog>("Assets/HorrorGameplay/Data/PhoneMessages/Messages.asset");
            Assert.That(data.exchanges.Length,Is.EqualTo(5));Assert.That(data.messages.Count(m=>m.replyOnly),Is.EqualTo(16));
            foreach(var e in data.exchanges)
            {
                Assert.That(data.Find(e.afterMessageId),Is.Not.Null);Assert.That(e.sentIds.Length,Is.EqualTo(e.responseIds.Length));
                foreach(var id in e.sentIds)Assert.That(data.Find(id).replyOnly&&data.Find(id).outgoing,Is.True);
                foreach(var id in e.responseIds)Assert.That(data.Find(id).replyOnly&&!data.Find(id).outgoing,Is.True);
            }
            var state=JsonUtility.FromJson<PhoneMessageState>("{\"version\":1,\"received\":[],\"pending\":[],\"triggers\":[]}");Assert.That(state.Valid,Is.True);
            state.choices.Add(new PhoneReplyChoice{exchangeId="road",option=1});Assert.That(state.Copy().choices[0].option,Is.EqualTo(1));
            state.choices.Add(new PhoneReplyChoice{exchangeId="road",option=0});Assert.That(state.Valid,Is.False);
            state.choices.RemoveAt(1);state.choices[0].option=-1;Assert.That(state.Valid,Is.False);
            state.choices.Clear();state.received.Add(new PhoneMessageReceipt{id="reply.road.0.sent",minute=1627,sendingSeconds=float.NaN});Assert.That(state.Valid,Is.False);
        }
        [Test] public void PickupStateIsAdditiveAndPreservedWithoutBreakingLegacyVersions()
        {
            var fresh=new PhoneMessageState();Assert.That(fresh.version,Is.EqualTo(2));Assert.That(fresh.acquired,Is.False);Assert.That(fresh.Valid,Is.True);
            fresh.acquired=true;Assert.That(fresh.Copy().acquired,Is.True);
            var legacy=JsonUtility.FromJson<PhoneMessageState>("{\"version\":1,\"received\":[],\"pending\":[],\"triggers\":[]}");Assert.That(legacy.Valid,Is.True);
            legacy.version=3;Assert.That(legacy.Valid,Is.False);
        }
    }
}
