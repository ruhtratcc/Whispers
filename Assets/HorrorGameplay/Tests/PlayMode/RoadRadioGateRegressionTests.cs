// Verifica .

#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    public sealed partial class BeforeDemolitionRoadSceneTests
    {
        [UnityTest, Category("RoadRadioGate20261104"), Timeout(90000)]
        public IEnumerator PhysicalRadioButtonsAreVisibleAndTheExistingActionsWorkThroughRaycasts()
        {
            foreach(var action in new[]{BeforeDemolitionRoadAction.RadioPower,BeforeDemolitionRoadAction.RadioStation,BeforeDemolitionRoadAction.RadioVolumeDown,BeforeDemolitionRoadAction.RadioVolumeUp})
            {
                var target=Target(action);var visual=target.transform.Find("Botão físico do rádio");
                Assert.That(visual,Is.Not.Null,action.ToString());
                Assert.That(visual.GetComponentsInChildren<Renderer>().All(r=>r.enabled&&!r.sharedMaterial.IsKeywordEnabled("_EMISSION")),Is.True);
                Assert.That(target.GetComponent<BoxCollider>().bounds.size.x,Is.InRange(.027f,.035f));
                Vector3 mounted=flow.driver.carRoot.InverseTransformPoint(target.transform.position);
                Assert.That(mounted.x,Is.InRange(-.09f,.07f));Assert.That(mounted.z,Is.InRange(.44f,.49f));
                float before=flow.radio.Volume;yield return Aim(action);yield return KeyPress(Key.E);
                if(action==BeforeDemolitionRoadAction.RadioVolumeDown)Assert.That(flow.radio.Volume,Is.LessThan(before));
                else if(action==BeforeDemolitionRoadAction.RadioVolumeUp)Assert.That(flow.radio.Volume,Is.GreaterThan(before));
                else Assert.That(game.hud.notification.text,Does.Contain("mesma estação"));
            }
            yield return Aim(BeforeDemolitionRoadAction.RadioStation);
            Capture("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RoadFix20261104/radio-in-car");
        }

        [UnityTest, Category("RoadRadioGate20261104"), Timeout(180000)]
        public IEnumerator WalkingUpToTheClosedGateLoadsTheSchoolWithoutCrossingItsSolidLeaf()
        {
            Fast(.2f);yield return Until(()=>flow.Parked,"Arrival",20);
            yield return Aim(BeforeDemolitionRoadAction.Flashlight);yield return KeyPress(Key.E);
            yield return OpenMessages();yield return CloseMessages();
            yield return Aim(BeforeDemolitionRoadAction.Exit);yield return KeyPress(Key.E);
            yield return Until(()=>!flow.driver.Seated&&game.CanControl,"Exit");
            var gate=flow.driver.gate.transform;Vector3 approach=gate.position-gate.forward*1.2f+Vector3.up*.02f;
            game.player.Warp(approach,gate.eulerAngles.y);Physics.SyncTransforms();
            Assert.That(flow.radio.EntryFaded,Is.False);
            Assert.That(gate.GetComponentsInChildren<Collider>().Any(c=>c.enabled&&!c.isTrigger),Is.True,"Reproduce the physical closed leaf.");
            Press(Key.W);
            var trace=new StringBuilder();float start=Time.realtimeSinceStartup,next=0;
            while(Time.realtimeSinceStartup-start<120 && !(SceneManager.GetActiveScene().name=="SchoolCEEP"&&GameSession.Instance&&GameSession.Instance.CanControl&&GameSession.Instance.narrative.IsInitialized))
            {
                if(Time.realtimeSinceStartup-start>=next)
                {
                    next+=1;var current=GameSession.Instance;
                    trace.Append("t=").Append(next).Append(" scene=").Append(SceneManager.GetActiveScene().name).Append(" blocked=").Append(SceneTransitionService.ControlsBlocked);
                    if(current&&current.player)trace.Append(" pos=").Append(current.player.transform.position).Append(" can=").Append(current.CanControl).Append(" state=").Append(current.State).Append(" keyW=").Append(keyboard.wKey.isPressed).Append(" controller=").Append(current.player.Controller.enabled).Append(" grounded=").Append(current.player.Controller.isGrounded).Append(" step=").Append(current.player.Controller.stepOffset).Append(" radius=").Append(current.player.Controller.radius).Append(" velocity=").Append(current.player.Controller.velocity);
                    if(flow)trace.Append(" gateLocal=").Append(gate.InverseTransformPoint(game.player.transform.position)).Append(" faded=").Append(flow.radio.EntryFaded).Append(" next=").Append(game.narrative.NextSignal);
                    trace.AppendLine();
                }
                yield return null;
            }
            File.WriteAllText("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RoadFix20261104/walk-trace.txt",trace.ToString());
            if(SceneManager.GetActiveScene().name=="RoadJourney")Capture("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/RoadFix20261104/gate-walk-failure");
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("SchoolCEEP"),trace.ToString());
            Press();yield return null;
            Assert.That(GameSession.Instance.narrative.HasSignal("bd.road.school-gate"),Is.True);
            Assert.That(SaveSystem.TestSaveDirectory,Is.EqualTo(temporary));
        }
    }
}
#endif
