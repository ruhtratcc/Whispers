// Verifica .

#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    static class AudioPassSceneAudit
    {
        public static void Check(GameSession game,string name)
        {
            var audio=AudioManager.Instance;Assert.That(audio&&audio.cues&&audio.mixer,Is.True);
            Assert.That(game.GetComponent<SceneAudioDetails>(),Is.Not.Null);
            foreach(var group in new[]{audio.musicGroup,audio.sfxGroup,audio.voiceGroup,audio.ambienceGroup,audio.uiGroup,audio.pcGroup,audio.phoneGroup})Assert.That(group,Is.Not.Null);
            Assert.That(audio.ambience&&audio.ambience.clip&&audio.ambience.loop,Is.True,"Recorded bed is assigned and looped");
            var lines=Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(s=>
            {Assert.That(!s.isPlaying||s.clip,Is.True,s.name+" is playing without a clip");
                if(s.isPlaying)Assert.That(s.outputAudioMixerGroup,Is.Not.Null,s.name+" routing");
                return s.name+" | "+(s.clip?s.clip.name:"idle/optional slot")+" | playing="+s.isPlaying+" | group="+(s.outputAudioMixerGroup?s.outputAudioMixerGroup.name:"idle/unrouted")+" | 3D="+s.spatialBlend+" | loop="+s.loop;}).ToArray();
            string dir="C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/AudioPass20261029/Runtime";Directory.CreateDirectory(dir);
            File.WriteAllLines(Path.Combine(dir,name+".txt"),lines);
        }
    }
    public sealed partial class BeforeDemolitionRoadSceneTests
    {
        [UnityTest,Category("AudioPassScenes")]public IEnumerator RoadRecordedAudioIsAssignedAndRoutedWithoutChangingProgress()
        {
            var before=JsonUtility.ToJson(game.narrative.Progress);AudioPassSceneAudit.Check(game,"02-road");
            Assert.That(flow.radio.vibration,Is.Not.Null);Assert.That(flow.radio.RadioSource.outputAudioMixerGroup.name,Is.EqualTo("Voice"));
            yield return null;Assert.That(JsonUtility.ToJson(game.narrative.Progress),Is.EqualTo(before));
        }
    }
    public sealed partial class BeforeDemolitionSchoolSceneTests
    {
        [UnityTest,Category("AudioPassScenes")]public IEnumerator SchoolRecordedAudioAndRoomZonesAreReadyWithoutChangingProgress()
        {
            var before=JsonUtility.ToJson(game.narrative.Progress);AudioPassSceneAudit.Check(game,"03-school");
            Assert.That(Object.FindObjectsByType<AudioReverbZone>(FindObjectsSortMode.None).Length,Is.GreaterThanOrEqualTo(5));
            yield return null;Assert.That(JsonUtility.ToJson(game.narrative.Progress),Is.EqualTo(before));
        }
    }
    public sealed partial class DesktopOfficePlayTests
    {
        [UnityTest,Category("AudioPassScenes")]public IEnumerator OfficeRecordedAudioIsAssignedAndComputerUsesItsOwnBus()
        {
            AudioPassSceneAudit.Check(game,"01-office");
            var audio=AudioManager.Instance;var sound=audio.PlayCue("pc_boot",Vector3.zero);
            Assert.That(sound.outputAudioMixerGroup,Is.SameAs(audio.pcGroup));
            yield return null;
        }
    }
}
#endif
