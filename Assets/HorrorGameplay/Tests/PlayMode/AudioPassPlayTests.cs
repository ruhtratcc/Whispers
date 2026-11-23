// Verifica .

#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;

namespace HorrorGame.Tests
{
    [Category("AudioPass")]
    public sealed class AudioPassPlayTests
    {
        GameObject root;
        AudioManager audio;
        AudioListener listener;
        AudioMixer mixer;
        float scale, volume;
        bool paused;
        readonly Dictionary<string,float> levels=new Dictionary<string,float>();
        [UnitySetUp] public IEnumerator SetUp()
        {
            Assert.That(GameSession.Instance,Is.Null,"Use a blank scene without touching a personal profile.");
            scale=Time.timeScale;volume=AudioListener.volume;paused=AudioListener.pause;
            Time.timeScale=1;AudioListener.pause=false;
            root=new GameObject("Recorded audio regression");
            listener=new GameObject("Listener").AddComponent<AudioListener>();listener.transform.SetParent(root.transform);
            audio=root.AddComponent<AudioManager>();
            mixer=AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/HorrorGameplay/Audio/Gameplay.mixer");audio.mixer=mixer;
            foreach(string p in new[]{"MusicVolume","AmbienceVolume","SFXVolume","VoiceVolume"}){mixer.GetFloat(p,out float v);levels[p]=v;}
            audio.musicGroup=Group(mixer,"Music");audio.ambienceGroup=Group(mixer,"Ambience");audio.voiceGroup=Group(mixer,"Voice");audio.sfxGroup=Group(mixer,"SFX");
            audio.pcGroup=Group(AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Audio/Mixers/PC.mixer"),"PC");
            audio.phoneGroup=Group(AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Audio/Mixers/Phone.mixer"),"Phone");
            audio.cues=AssetDatabase.LoadAssetAtPath<AudioCueLibrary>("Assets/Audio/AudioCues.asset");
            yield return null;
        }
        static AudioMixerGroup Group(AudioMixer m,string n)=>System.Array.Find(m.FindMatchingGroups(""),g=>g.name==n);
        [UnityTest] public IEnumerator PoolResetsLoopsAndRoutesPhoneAndComputerSeparately()
        {
            var first=audio.PlayCue("cassette_hiss_loop",Vector3.zero);Assert.That(first,Is.Not.Null);
            first.loop=true;first.panStereo=.7f;first.Stop();
            var next=audio.PlayCue("phone_vibration",Vector3.zero);
            Assert.That(next,Is.SameAs(first));Assert.That(next.loop,Is.False);Assert.That(next.panStereo,Is.Zero);
            Assert.That(next.outputAudioMixerGroup,Is.SameAs(audio.phoneGroup));
            var pc=audio.PlayCue("pc_boot",Vector3.zero);Assert.That(pc.outputAudioMixerGroup,Is.SameAs(audio.pcGroup));
            Assert.That(root.GetComponentsInChildren<AudioSource>(true).Length,Is.EqualTo(24));yield return null;
        }
        [UnityTest] public IEnumerator SpatialSoundUsesLogRolloffAndFallsSilentBeyondItsRange()
        {
            var source=audio.PlayClip(audio.cues.Find("cassette_hiss_loop").sound.clips[0],Vector3.right*100,.4f,1,AudioChannel.SFX,true);
            Assert.That(source.rolloffMode,Is.EqualTo(AudioRolloffMode.Logarithmic));
            yield return new WaitForSecondsRealtime(.3f);Assert.That(source.volume,Is.Zero);
            source.transform.position=Vector3.right;
            yield return new WaitForSecondsRealtime(.2f);Assert.That(source.volume,Is.EqualTo(.4f).Within(.001f));
        }
        [UnityTest] public IEnumerator ImportantVoiceDucksBedsAndRecoversWithoutChangingSavedSliders()
        {
            float music=PlayerPrefs.GetFloat("HorrorMenu.Music",PresentationSettings.DefaultMusic);
            float ambient=PlayerPrefs.GetFloat("HorrorMenu.Ambience",PlayerPrefs.GetFloat("HorrorMenu.SFX",.8f));
            var voice=new GameObject("Voice routing fixture").AddComponent<AudioSource>();voice.transform.SetParent(root.transform);
            voice.clip=audio.cues.Find("cassette_hiss_loop").sound.clips[0];voice.volume=0;voice.loop=true;
            audio.Route(voice,AudioChannel.Voice);voice.Play();
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(audio.DuckLevel,Is.EqualTo(.48f).Within(.01f));Assert.That(voice.outputAudioMixerGroup,Is.SameAs(audio.voiceGroup));
            mixer.GetFloat("MusicVolume",out float db);Assert.That(db,Is.EqualTo(Db(music*.48f)).Within(.1f));
            mixer.GetFloat("AmbienceVolume",out db);Assert.That(db,Is.EqualTo(Db(ambient*.48f)).Within(.1f));
            voice.Stop();yield return new WaitForSecondsRealtime(.95f);Assert.That(audio.DuckLevel,Is.EqualTo(1).Within(.01f));
            Assert.That(PlayerPrefs.GetFloat("HorrorMenu.Music",PresentationSettings.DefaultMusic),Is.EqualTo(music));
        }
        static float Db(float f)=>f<=.0001f?-80:Mathf.Log10(f)*20;
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(root)Object.Destroy(root);yield return null;
            if(mixer)foreach(var p in levels)mixer.SetFloat(p.Key,p.Value);levels.Clear();
            Time.timeScale=scale;AudioListener.volume=volume;AudioListener.pause=paused;
        }
    }
}
#endif
