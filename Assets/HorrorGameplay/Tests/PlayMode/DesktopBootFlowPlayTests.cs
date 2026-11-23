// Verifica .

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using HorrorGame.Desktop;

namespace HorrorGame.Tests
{
    public sealed partial class DesktopOfficePlayTests
    {
        [UnityTest,Category("CRTBoot"),Timeout(120000)]
        public IEnumerator FirstBootShowsEveryStageThenRevisitsSkipToAnEmptyImmersiveDesktop()
        {
            pc.transitionSeconds=1.2f;
            var hud=game.hud;
            var hidden=new[]{hud.objectiveGroup?hud.objectiveGroup.gameObject:null,
                hud.interactionGroup?hud.interactionGroup.gameObject:null,hud.batteryGroup?hud.batteryGroup.gameObject:null,
                hud.staminaGroup?hud.staminaGroup.gameObject:null,hud.crosshair?hud.crosshair.gameObject:null,
                hud.notification?hud.notification.gameObject:null}.Where(item=>item).Distinct().ToArray();
            var original=hidden.ToDictionary(item=>item,item=>item.activeSelf);
            float started=Time.realtimeSinceStartup;
            Assert.That(pc.Open(),Is.True);Assert.That(pc.IsBooting,Is.True);Assert.That(pc.Ready,Is.False);
            Assert.That(pc.TrySkipBoot(),Is.False,"The full first boot is not the short resumable boot.");
            yield return null;
            foreach(var element in hidden)Assert.That(element.activeInHierarchy,Is.False,"Game HUD must be hidden: "+element.name);
            var seen=new HashSet<DesktopBootStage>();bool paused=false;
            while(!pc.Ready&&Time.realtimeSinceStartup-started<12)
            {
                var stage=pc.BootStage;
                if(seen.Add(stage))yield return Capture("boot-first-"+stage.ToString().ToLowerInvariant(),false);
                if(!paused&&stage==DesktopBootStage.Bios)
                {
                    paused=true;yield return Press(Key.F10);Assert.That(game.State,Is.EqualTo(GameState.Paused));
                    float elapsed=pc.BootElapsed;yield return new WaitForSecondsRealtime(.15f);
                    Assert.That(pc.BootElapsed,Is.EqualTo(elapsed).Within(.025f),"A paused boot must not advance.");
                    yield return Press(Key.Escape);Assert.That(game.State,Is.EqualTo(GameState.Inspecting));Assert.That(pc.IsSeated,Is.True);
                }
                yield return null;
            }
            Assert.That(pc.Ready,Is.True);Assert.That(Time.realtimeSinceStartup-started,Is.InRange(6,10));
            foreach(var expected in new[]{DesktopBootStage.Black,DesktopBootStage.Bios,DesktopBootStage.Splash,DesktopBootStage.Welcome})
                Assert.That(seen,Does.Contain(expected),"The first boot must visibly show "+expected);
            Assert.That(paused,Is.True);Assert.That(pc.Windows,Is.Empty);
            Assert.That(pc.DesktopRoot.GetComponentsInChildren<DesktopIcon>().Length,Is.EqualTo(pc.content.apps.Length));
            var texts=pc.DesktopRoot.GetComponentsInChildren<TMP_Text>(true);
            Assert.That(texts.Any(text=>text.text=="Reduzir efeitos CRT"||text.text=="Pausar jogo — F10"||text.text=="Desligar / levantar"),Is.False);
            Assert.That(texts.Any(text=>text.gameObject.activeInHierarchy&&text.text=="João de Claus"),Is.False,"The account belongs to login, not an always-visible game menu.");
            string firstHint=texts.Where(text=>text.gameObject.activeInHierarchy).Select(text=>text.text)
                .FirstOrDefault(text=>text.IndexOf("Esc",StringComparison.OrdinalIgnoreCase)>=0&&text.IndexOf("clique",StringComparison.OrdinalIgnoreCase)>=0);
            var tray=pc.DesktopRoot.Find("Barra de tarefas");Assert.That(tray,Is.Not.Null);
            var envelope=tray.GetComponentsInChildren<Image>(true).Single(image=>image.sprite==pc.content.FindApp("mail").icon);
            Assert.That(envelope.gameObject.activeInHierarchy,Is.False,"New mail must arrive a few seconds after the desktop, not during boot.");
            yield return new WaitForSecondsRealtime(3.25f);
            Assert.That(envelope.gameObject.activeInHierarchy,Is.True);
            if(firstHint!=null)Assert.That(pc.DesktopRoot.GetComponentsInChildren<TMP_Text>().Any(text=>text.text==firstHint),Is.False,"The one-time hint lasts only three seconds.");
            Assert.That(pc.Windows,Is.Empty,"A new-mail notification must not launch Correio.");
            yield return Capture("desktop-empty-after-boot",true);

            var computerIcon=pc.DesktopRoot.GetComponentsInChildren<DesktopIcon>().Single(icon=>icon.AppId=="computer");
            var iconRect=(RectTransform)computerIcon.transform;var iconPoint=DesktopPoint(iconRect,iconRect.rect.center);
            pc.Pointer.ClickAt(iconPoint);pc.Pointer.ClickAt(iconPoint);yield return null;yield return null;
            Assert.That(pc.Windows.Count,Is.GreaterThan(0),"Meu Computador must provide an explicit error instead of doing nothing.");
            Assert.That(pc.Windows.Keys.All(id=>id.StartsWith("error.",StringComparison.Ordinal)),Is.True);
            foreach(string id in pc.Windows.Keys.ToArray())pc.CloseApp(id);yield return null;

            pc.OpenApp("notepad");Assert.That(pc.Windows.ContainsKey("notepad"),Is.True);
            yield return Click(DesktopButton("Iniciar"));yield return Click(DesktopButton("Desligar"));yield return Click(DesktopButton("Desligar"));
            yield return Until(()=>!pc.IsSeated&&game.CanControl,"Start shutdown returning control");
            foreach(var element in hidden)if(element)Assert.That(element.activeSelf,Is.EqualTo(original[element]),"HUD state must be restored: "+element.name);

            started=Time.realtimeSinceStartup;Assert.That(pc.Open(),Is.True);Assert.That(pc.TrySkipBoot(),Is.False);
            yield return Until(()=>pc.BootElapsed>=1&&!pc.Ready,"short boot skip becomes available");
            yield return Press(Key.Space);yield return Until(()=>pc.Ready,"skip short boot with a real key");
            Assert.That(Time.realtimeSinceStartup-started,Is.InRange(2,4));
            Assert.That(pc.State.visits,Is.EqualTo(2));Assert.That(pc.Windows,Is.Empty,"Previous windows must not reappear on another visit.");
            if(firstHint!=null)Assert.That(pc.DesktopRoot.GetComponentsInChildren<TMP_Text>().Any(text=>text.text==firstHint),Is.False,"The control hint must not repeat on a later visit.");
            yield return Press(Key.Escape);yield return Until(()=>!pc.IsSeated&&game.CanControl,"Escape leaving desktop");
            Assert.That(game.player.view.enabled,Is.True);Assert.That(game.player.view.playerCamera.fieldOfView,Is.EqualTo(49).Within(.02f));
        }
    }
}
#endif
