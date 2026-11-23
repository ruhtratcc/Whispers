// Verifica OldSavesHaveNoClockAndNewClockRoundTrips, NightRollsOverAndHistoricalMailMetadataStaysIntact.

using NUnit.Framework;
using UnityEngine;
namespace HorrorGame.Tests
{
 [Category("JungClock")]public sealed class GameClockDataTests
 {
  [Test]public void OldSavesHaveNoClockAndNewClockRoundTrips(){var old=JsonUtility.FromJson<SaveData>("{\"version\":2}");Assert.That(old.clock==null||old.clock.Valid&&old.clock.minute==1512,Is.True);var state=new GameClockState{minute=1627,locked=true,anomalies=3};Assert.That(JsonUtility.FromJson<GameClockState>(JsonUtility.ToJson(state)).Valid,Is.True);state.elapsed=float.NaN;Assert.That(state.Valid,Is.False);state.elapsed=0;state.minute=-1;Assert.That(state.Valid,Is.False);}
  [Test]public void NightRollsOverAndHistoricalMailMetadataStaysIntact(){Assert.That(GameClock.NightStart.AddMinutes(1512).ToString("dd/MM/yyyy HH:mm"),Is.EqualTo("27/10/2026 01:12"));var mail=new EmailMessage{id="email.01",date="14/03/2006",time=""};Assert.That(GameClock.MailDate(mail),Is.EqualTo(mail.date));mail.id="email.08";Assert.That(GameClock.MailDate(mail),Is.EqualTo("27/10/2026"));Assert.That(GameClock.MailTime(mail),Is.EqualTo("03:07"));}
 }
}
