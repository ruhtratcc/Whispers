// Verifica .

#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    public sealed partial class BeforeDemolitionFourStudentsTests
    {
        const string AccessOutput="C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/SchoolAccess20261105";

        [UnityTest,Category("Access20261105"),Timeout(180000)]
        public IEnumerator StairGateAndBothRampDoorsUseTheExistingKeyAndRestoreWithoutBypass()
        {
            Assert.That(flow.stairDoor.persistentId,Is.EqualTo("bd.door.stairs"));
            Assert.That(Vector3.Distance(flow.stairDoor.transform.position,new Vector3(-29.79f,0,57.55f)),Is.LessThan(.01f));
            Assert.That(flow.rampDoors.Length,Is.EqualTo(2));
            foreach(var d in flow.rampDoors){Assert.That(d.requiredKey,Is.EqualTo(BeforeDemolitionFourStudentsFlow.StairKeyId));Assert.That(d.locked,Is.True);d.Interact(game.interaction);Assert.That(d.IsOpen,Is.False);}
            game.player.Warp(new Vector3(-28.95f,.01f,55.9f),0);Physics.SyncTransforms();
            for(int i=0;i<30;i++){game.player.Controller.Move(new Vector3(0,-.02f,.08f));yield return null;}
            Assert.That(game.player.transform.position.z,Is.LessThan(57.3f),"Actual capsule cannot pass the closed gate");
            Assert.That(game.save.Save(),Is.True);Assert.That(game.save.TryReadCurrent(out var beforeKey),Is.True);
            foreach(string id in BeforeDemolitionFourStudentsFlow.StudentItemIds)Add(id);
            Add(BeforeDemolitionFourStudentsFlow.BiaTapeHeardId);
            var progress=SchoolPrefix();progress.milestones.Add(BeforeDemolitionFourStudentsFlow.GatheredSignal);progress.sequences.Add(BeforeDemolitionFourStudentsFlow.IntercomSequenceId);
            game.narrative.RestoreProgress(progress);game.narrative.InitializeAfterSession(true);
            yield return null;yield return null;
            var objectives=game.GetComponent<ObjectiveService>();Assert.That(objectives.CurrentTitle,Is.EqualTo("Procure a chave no refeitório."));
            Assert.That(flow.stairKeyVisual.activeSelf,Is.True);Assert.That(flow.stairKeyVisual.transform.position.z,Is.EqualTo(53.83f).Within(.02f));
            Add(BeforeDemolitionFourStudentsFlow.StairKeyId);flow.RestoreWorldSilently();yield return null;
            foreach(var door in flow.rampDoors.Concat(new[]{flow.stairDoor})){Assert.That(door.locked,Is.False);door.Interact(game.interaction);Assert.That(door.IsOpen,Is.True);}
            yield return new WaitForSeconds(1.4f);
            game.player.Warp(new Vector3(-28.95f,.01f,55.9f),0);Physics.SyncTransforms();
            for(int i=0;i<55;i++){game.player.Controller.Move(new Vector3(0,-.02f,.07f));yield return null;}
            Assert.That(game.player.transform.position.z,Is.GreaterThan(58.7f),"Opening the relocated gate restores the stair route");
            Assert.That(game.save.RestoreSnapshot(beforeKey),Is.True);game.narrative.InitializeAfterSession(true);yield return null;yield return null;
            foreach(var door in flow.rampDoors.Concat(new[]{flow.stairDoor})){Assert.That(door.locked,Is.True);Assert.That(door.IsOpen,Is.False);}
        }

        IEnumerator AccessMirrorReady()
        {
            var progress=SchoolPrefix();progress.chapterId=BeforeDemolitionMirrorFlow.ChapterId;
            progress.completedChapters.AddRange(new[]{BeforeDemolitionFourStudentsFlow.ChapterId,BeforeDemolitionTwoVoicesFlow.ChapterId});
            foreach(string id in BeforeDemolitionFourStudentsFlow.StudentItemIds)Add(id);
            foreach(string id in new[]{BeforeDemolitionFourStudentsFlow.BiaTapeHeardId,BeforeDemolitionFourStudentsFlow.StairKeyId,BeforeDemolitionTwoVoicesFlow.TapeId,BeforeDemolitionTwoVoicesFlow.TapeHeardId})Add(id);
            progress.milestones.AddRange(new[]{BeforeDemolitionFourStudentsFlow.GatheredSignal,BeforeDemolitionFourStudentsFlow.KeySignal,BeforeDemolitionFourStudentsFlow.UpstairsSignal,"bd.loop.escaped","bd.loop.tape","bd.loop.mirror",BeforeDemolitionMirrorFlow.SeenSignal,BeforeDemolitionMirrorFlow.BrokenSignal});
            progress.sequences.Add(BeforeDemolitionFourStudentsFlow.IntercomSequenceId);
            progress.sequences.Add(BeforeDemolitionMirrorFlow.OfferSequence);
            game.narrative.RestoreProgress(progress);
            game.GetComponent<BeforeDemolitionTwoVoicesFlow>().RestoreState(JsonUtility.ToJson(new BeforeDemolitionTwoVoicesState{loopCount=1}));
            var mirror=UnityEngine.Object.FindAnyObjectByType<BeforeDemolitionMirrorFlow>();
            mirror.RestoreState(JsonUtility.ToJson(new BeforeDemolitionMirrorFlow.State{stage=2}));
            game.narrative.InitializeAfterSession(true);
            yield return Until(()=>mirror.IsActive&&mirror.endingView&&mirror.endingView.IsOpen,"Native mirror choice opens",30);
        }

        [UnityTest, Category("Access20261105"), Timeout(480000)]
        public IEnumerator MirrorOpeningRevealsTheRoomAndOnlyTheOpeningIsPassable()
        {
            yield return AccessMirrorReady();
            var mirror = UnityEngine.Object.FindAnyObjectByType<BeforeDemolitionMirrorFlow>();
            var pane = mirror.glassVisual.GetComponentsInChildren<MeshRenderer>(true)
                .Single(renderer => renderer.name.EndsWith("mirror_glass_surface"));
            float x = pane.bounds.center.x;
            var origin = new Vector3(x, 5.45f, 90.1f);
            var light = mirror.revealVisual.GetComponentInChildren<Light>(true);
            Assert.That(light, Is.Not.Null);
            Assert.That(light.gameObject.activeInHierarchy, Is.False);
            Assert.That(Physics.RaycastAll(origin, Vector3.forward, 5, ~0, QueryTriggerInteraction.Ignore)
                .Any(hit => hit.collider.name == "Colisão do vidro intacto"), Is.True);
            Assert.That(UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Any(r => r.transform.parent && r.transform.parent.name == "chalkboard"
                    && Vector3.Distance(r.bounds.center, pane.bounds.center) < 1.1f), Is.False);

            BeforeDemolitionChapterFixture.Click(mirror.endingView.RefuseButton);
            yield return Until(() => game.CanControl && !game.narrative.SequencePlaying, "A recusa devolve o controle.", 100);
            Assert.That(mirror.glassVisual.activeInHierarchy, Is.False);
            Assert.That(light.gameObject.activeInHierarchy && light.enabled, Is.True);
            Assert.That(Physics.Raycast(origin, Vector3.forward, out var visible, 5, ~0, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(visible.collider.name, Is.EqualTo("Espaço oculto — fundo fechado"));
            Assert.That(Physics.Raycast(origin + Vector3.left * 1.7f, Vector3.forward, out var plaster, 5, ~0, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(plaster.point.z, Is.LessThan(91.1f));

            var controller = game.player.Controller;
            game.player.Warp(new Vector3(x - 1.7f, 3.99f, 90.15f), 0);
            Physics.SyncTransforms();
            for (int i = 0; i < 20; i++) controller.Move(Vector3.forward * .08f);
            Assert.That(game.player.transform.position.z, Is.LessThan(91.1f), "A cápsula é detida pela alvenaria.");
            game.player.Warp(new Vector3(x + .57f, 3.99f, 90.15f), 0);
            Physics.SyncTransforms();
            for (int i = 0; i < 30; i++) controller.Move(Vector3.forward * .08f);
            Assert.That(game.player.transform.position.z, Is.GreaterThan(91.65f), "A cápsula entra pelo vão revelado.");
        }

        [UnityTest,Category("Access20261105"),Timeout(240000)]
        public IEnumerator NativeSchoolGoodEndingAdvancesThroughTheWholeCutsceneAndFinalEmail()
        {
            yield return AccessMirrorReady();var mirror=UnityEngine.Object.FindAnyObjectByType<BeforeDemolitionMirrorFlow>();var epilogue=game.GetComponent<EndingEpilogue>();
            Assert.That(epilogue,Is.Not.Null);int completions=0;game.narrative.onChapterCompleted.AddListener(id=>completions++);
            game.save.flashlight.Restore(0,true);
            yield return null;
            Assert.That(game.save.flashlight.Battery01,Is.GreaterThanOrEqualTo(.249f),"The final chapter restores a safe minimum charge.");
            mirror.endingView.offerReminderSeconds = 10;
            yield return Until(() => AudioManager.Instance.GetComponentsInChildren<AudioSource>().Any(source => source.isPlaying && source.clip && source.clip.name == "espelho_lembrete_01"), "Helena speaks while the choice is open", 15);
            BeforeDemolitionChapterFixture.Click(mirror.endingView.RefuseButton);
            yield return Until(()=>game.CanControl&&!game.narrative.SequencePlaying,"Refusal returns control after its authored lines",65);
            Assert.That(mirror.glassVisual.activeSelf,Is.False);Assert.That(mirror.revealVisual.activeSelf,Is.True);
            Assert.That(Physics.Raycast(mirror.mirrorPlane.position + new Vector3(0,.15f,.92f),Vector3.forward,out var back,4,~0,QueryTriggerInteraction.Ignore),Is.True);
            Assert.That(back.collider.name,Is.EqualTo("Espaço oculto — fundo fechado"),"There is real empty volume behind the wall opening");
            var oldDoor=UnityEngine.Object.FindObjectsByType<InteractiveDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(d=>d.persistentId=="school.door.1.10");
            Assert.That(oldDoor.enabled,Is.False);Assert.That(oldDoor.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled),Is.True);
            yield return AimAt(mirror.returnedWhistleVisual.transform.position,new Vector3(mirror.transform.position.x,3.86f,mirror.transform.position.z+.02f));
            yield return Until(()=>game.interaction.Target==mirror,"The actual raycast reaches the belongings in the opening",6);
            game.interaction.Target.Interact(game.interaction);
            yield return Until(() => AudioManager.Instance.GetComponentsInChildren<AudioSource>().Any(source => source.isPlaying && source.clip && source.clip.name == "intercom_obrigada"), "Returning the whistle plays Helena's intercom line", 10);
            yield return Until(()=>epilogue.IsRunning,"Returning the whistle starts the real cutscene",20);
            Directory.CreateDirectory(AccessOutput);string trace=Path.Combine(AccessOutput,"native-ending-trace.txt");File.WriteAllText(trace,"");
            string previous="";float deadline=Time.realtimeSinceStartup+130;int first=Time.frameCount;
            var heard = new System.Collections.Generic.HashSet<string>();
            while(epilogue.Stage!="Email"&&Time.realtimeSinceStartup<deadline)
            {
                if(epilogue.Stage!=previous){previous=epilogue.Stage;File.AppendAllText(trace,Time.frameCount+" "+epilogue.Stage+Environment.NewLine);}
                if (Time.frameCount % 8 == 0)
                    foreach (var source in AudioManager.Instance.GetComponentsInChildren<AudioSource>())
                        if (source.isPlaying && source.clip && source.outputAudioMixerGroup == AudioManager.Instance.voiceGroup)
                            heard.Add(source.clip.name);
                yield return null;
            }
            Assert.That(heard, Does.Contain("wp_ex_epilogo_telefone_01"));
            Assert.That(heard, Does.Contain("wp_ex_epilogo_telefone_02"));
            Assert.That(heard, Does.Contain("wp_ex_epilogo_noticia"));
            Assert.That(epilogue.Stage,Is.EqualTo("Email"));Assert.That(Time.frameCount-first,Is.GreaterThan(10),"The Play loop keeps advancing during the cutscene");
            yield return Until(()=>epilogue.Computer&&epilogue.Computer.Ready&&epilogue.Computer.inbox.State.IsRead("ending.helena.reply"),"Final email is visible and read on the actual PC",40);
            Assert.That(game.player.view.playerCamera.fieldOfView,Is.EqualTo(49).Within(.01f));
            Assert.That(epilogue.SelectReply(1),Is.True);
            yield return Until(()=>game.narrative.Progress.endingSeen,"Final reply reaches credits and one completion",65);
            File.AppendAllText(trace,Time.frameCount+" "+epilogue.Stage+Environment.NewLine);
            Assert.That(epilogue.Stage,Is.EqualTo("Complete"));Assert.That(completions,Is.EqualTo(1));Assert.That(EndingEpilogue.Active,Is.False);
            Assert.That(game.save.Save(),Is.True);Assert.That(game.save.TryReadCurrent(out var saved),Is.True);Assert.That(saved.narrative.endingSeen,Is.True);
        }
    }
}
#endif
