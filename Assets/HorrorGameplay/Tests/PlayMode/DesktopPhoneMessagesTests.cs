// Verifica .

#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
 public sealed partial class DesktopOfficePlayTests
 {
  [UnityTest,Category("PhoneMessagesOffice"),Timeout(120000)]
  public IEnumerator ComputerDefersThePhoneBannerWithoutConsumingItsTypingOrChangingEmails()
  {
   var phone=PhoneMessages.For(game);Assert.That(phone,Is.Not.Null);
   phone.Restore(new PhoneMessageState{acquired=true});
   var before=pc.inbox.CaptureState();yield return Enter();
   phone.Trigger(PhoneTrigger.Secretary);yield return new WaitForSeconds(.2f);
   Assert.That(phone.BannerVisible||phone.IsOpen,Is.False);Assert.That(phone.State.received.Any(m=>!m.announced),Is.True);
   Assert.That(phone.State.pending.Single().seconds,Is.EqualTo(8));
   yield return Press(Key.P);Assert.That(phone.IsOpen,Is.False);Assert.That(pc.IsSeated,Is.True);
   pc.Close();yield return Until(()=>!pc.IsSeated&&phone.IsOpen,"Queued conversation opens only after leaving the PC");
   CollectionAssert.AreEqual(before.deliveredIds,pc.inbox.State.deliveredIds);
   CollectionAssert.AreEqual(before.readIds,pc.inbox.State.readIds);
   Assert.That(phone.IsOpen,Is.True,"Incoming message opens the phone without a shortcut");
   yield return Press(Key.P);yield return Until(()=>game.CanControl,"Phone closes");
  }
 }
}
#endif
