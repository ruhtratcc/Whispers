// Verifica EssentialEventsHaveRecordedClipsAndSpatialFilesAreMono, RequiredSurfacesHaveFourToSixDifferentFootfalls, MixerGraphIncludesEightBusesAndChildMixersFeedEffects, PhoneAndProceduralReplacementsUseRealAssetsAndLoopsArePrepared.

using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEditor;

namespace HorrorGame.Tests
{
    [Category("AudioPassData")]
    public sealed class AudioPassDataTests
    {
        const string Library="Assets/Audio/AudioCues.asset";
        [Test] public void EssentialEventsHaveRecordedClipsAndSpatialFilesAreMono()
        {
            var lib=AssetDatabase.LoadAssetAtPath<AudioCueLibrary>(Library);Assert.That(lib,Is.Not.Null);
            foreach(string id in new[]{"door_open","door_close","door_locked","whistle","heartbeat","phone_vibration","phone_notification","phone_typing","phone_send","car_door","pc_boot","pc_hd","pc_click","pc_error","mirror_shock","mirror_break","wall_collapse","tape_play","tape_stop"})Assert.That(lib.Find(id),Is.Not.Null,id);
            foreach(var cue in lib.cues.Where(c=>c.essential))
            {
                Assert.That(cue.sound,Is.Not.Null,cue.id);Assert.That(cue.sound.clips,Is.Not.Empty,cue.id);
                foreach(var clip in cue.sound.clips)
                {
                    Assert.That(clip,Is.Not.Null,cue.id);Assert.That(clip.length,Is.GreaterThan(.01f),cue.id);
                    if(cue.spatial)Assert.That(clip.channels,Is.EqualTo(1),cue.id+" must be mono in 3D");
                    string path=AssetDatabase.GetAssetPath(clip);Assert.That(path.StartsWith("Assets/Audio/"),Is.True,path);
                    Assert.That(((AudioImporter)AssetImporter.GetAtPath(path)).defaultSampleSettings.sampleRateOverride,Is.EqualTo(44100),path);
                }
            }
            Assert.That(lib.cues.Select(c=>c.id).Distinct().Count(),Is.EqualTo(lib.cues.Length));
        }
        [Test] public void RequiredSurfacesHaveFourToSixDifferentFootfalls()
        {
            var lib=AssetDatabase.LoadAssetAtPath<AudioCueLibrary>(Library);
            foreach(string surface in new[]{"wood","concrete","tile","carpet"})
            {
                var clips=lib.Find("steps_"+surface).sound.clips;
                Assert.That(clips.Length,Is.InRange(4,6),surface);Assert.That(clips.Distinct().Count(),Is.EqualTo(clips.Length));
            }
        }
        [Test] public void MixerGraphIncludesEightBusesAndChildMixersFeedEffects()
        {
            var main=AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/HorrorGameplay/Audio/Gameplay.mixer");
            var groups=main.FindMatchingGroups("").Select(g=>g.name).ToList();
            foreach(string name in new[]{"UI","PC","Phone"})
            {
                var mixer=AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Audio/Mixers/"+name+".mixer");
                Assert.That(mixer,Is.Not.Null);Assert.That(mixer.outputAudioMixerGroup.name,Is.EqualTo("SFX"));
                CollectionAssert.AreEqual(new[]{name},mixer.FindMatchingGroups("").Select(g=>g.name));groups.Add(name);
            }
            CollectionAssert.AreEquivalent(new[]{"Master","Music","Ambience","SFX","UI","Voice","PC","Phone"},groups);
            foreach(string p in new[]{"MusicVolume","SFXVolume","AmbienceVolume","VoiceVolume"})Assert.That(main.GetFloat(p,out _),Is.True,p);
        }
        [Test] public void PhoneAndProceduralReplacementsUseRealAssetsAndLoopsArePrepared()
        {
            var lib=AssetDatabase.LoadAssetAtPath<AudioCueLibrary>(Library);
            var phone=AssetDatabase.LoadAssetAtPath<PhoneMessageCatalog>("Assets/HorrorGameplay/Data/PhoneMessages/Messages.asset");
            foreach(var sound in new[]{phone.notification,phone.vibration,phone.typing})Assert.That(sound.clips.Any(c=>c),Is.True);
            Assert.That(phone.vibration.channel,Is.EqualTo(AudioChannel.Phone));
            foreach(string id in new[]{"interior_wind_loop","computer_fan_loop","cassette_hiss_loop","heartbeat","dawn_birds","car_interior","tension_music"})Assert.That(lib.Find(id).loopPrepared,Is.True,id);
            foreach(string path in new[]{"Assets/HorrorGameplay/Resources/VoiceRadioStatic.asset","Assets/HorrorGameplay/ItemInspection/Data/Tape playback.asset","Assets/SchoolCEEP/Data/ChapterThree/Heartbeat.asset"})
            {
                var set=AssetDatabase.LoadAssetAtPath<SoundSet>(path);Assert.That(set.clips.All(c=>c&&AssetDatabase.GetAssetPath(c).StartsWith("Assets/Audio/")),Is.True,path);
            }
        }
    }
}
