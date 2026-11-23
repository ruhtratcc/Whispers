// Verifica .

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HorrorGame.Desktop;
namespace HorrorGame.Tests
{
 public sealed partial class DesktopOfficePlayTests
 {
  [UnityTest,Category("JungDesktop"),Timeout(120000)]
  public IEnumerator JungIdentityMenuBrowserClockAndRestartUseRealUI()
  {
   yield return Enter();Assert.That(pc.Windows,Is.Empty);var clock=GameClock.Instance;Assert.That(clock,Is.Not.Null);Assert.That(clock.TimeText,Is.EqualTo("01:12"));Assert.That(clock.DateText,Is.EqualTo("27/10/2026"));
   var old=clock.Capture();clock.State.elapsed=24.95f;yield return new WaitForSeconds(.12f);Assert.That(clock.State.minute,Is.EqualTo(1513));clock.Restore(old);
   clock.TriggerOfficeAnomaly(false);Assert.That(clock.TimeText,Is.EqualTo("03:07"));yield return new WaitForSeconds(1.1f);Assert.That(clock.TimeText,Is.EqualTo("01:12"));clock.TriggerOfficeAnomaly(false);Assert.That(clock.TimeText,Is.EqualTo("01:12"));
   clock.TriggerOfficeAnomaly(true);Assert.That(clock.TimeText,Is.EqualTo("01:11"));yield return new WaitForSeconds(1.1f);Assert.That(clock.TimeText,Is.EqualTo("01:12"));clock.TriggerOfficeAnomaly(true);Assert.That(clock.TimeText,Is.EqualTo("01:12"));clock.Restore(old);
   var browser=pc.OpenApp("browser");Assert.That(Text(browser),Does.Contain(DesktopSystemText.SearchName));var app=(DesktopBrowserApp)browser.App;
   Assert.That(app.Navigate(DesktopSystemText.SearchHome+"/busca?q=demoli"),Is.True);yield return null;Assert.That(Text(browser),Does.Contain("Demolição"));yield return Capture("jung-search",false);
   Assert.That(app.Navigate(DesktopSystemText.MailHome),Is.True);yield return null;Assert.That(Text(browser),Does.Contain(pc.inbox.catalog.Find("email.01").subject));yield return Capture("jungmail-inbox",false);
   yield return Click(Button(browser,pc.inbox.catalog.Find("email.01").subject));Assert.That(pc.inbox.State.IsRead("email.01"),Is.True);
   Assert.That(app.Navigate("ceep.local/demolicao"),Is.True);Assert.That(app.Step(-1),Is.True);Assert.That(app.CurrentAddress,Is.EqualTo(DesktopSystemText.MailHome));Assert.That(app.Navigate("https://example.invalid"),Is.False);pc.CloseApp("error.disk");pc.CloseApp("browser");
   yield return Click(DesktopButton("Iniciar"));yield return Click(DesktopButton(DesktopSystemText.AllPrograms+"  >"));yield return Capture("jung-start-menu",false);
   yield return Click(DesktopButton(DesktopSystemText.Shutdown));Assert.That(pc.Windows.ContainsKey("shutdown"),Is.True);yield return Click(DesktopButton(DesktopSystemText.Cancel));Assert.That(pc.IsSeated,Is.True);
   yield return Click(DesktopButton("Iniciar"));yield return Click(DesktopButton(DesktopSystemText.Shutdown));yield return Click(DesktopButton(DesktopSystemText.Restart));yield return Until(()=>pc.State.visits==2&&pc.Ready,"restart returns to desktop");Assert.That(pc.Windows,Is.Empty);
   clock.State.minute=1550;Assert.That(game.save.Save(),Is.True);Assert.That(SaveSystem.TryReadCheckpoint(game.save.SavePath,out var data,"Chapter1Office",BeforeDemolitionCampaign.StoryId),Is.True);Assert.That(data.clock.minute,Is.EqualTo(1550));clock.State.minute=1512;Assert.That(game.save.RestoreSnapshot(data),Is.True);Assert.That(clock.State.minute,Is.EqualTo(1550));
   clock.LockAtSchool();Assert.That(clock.TimeText,Is.EqualTo("03:07"));yield return new WaitForSeconds(.1f);Assert.That(clock.State.minute,Is.EqualTo(1627));clock.Restore(old);yield return Capture("jung-desktop",true);
  }
 }
}
#endif
