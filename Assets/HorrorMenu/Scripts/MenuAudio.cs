// Toca os sons de interface e do ambiente do menu.

using System.Collections;
using UnityEngine;

namespace SchoolMenu
{
    public sealed class MenuAudio : MonoBehaviour
    {
        [SerializeField]
        public AudioSource ambientSource, musicSource, effectsSource, distantSource;
        [SerializeField]
        public AudioClip ambientLoop, music, hoverTick, confirmThud;
        [SerializeField]
        public AudioClip[] distantSounds;
        [SerializeField]
        public UnityEngine.Audio.AudioMixer mixer;
        [SerializeField]
        public UnityEngine.Audio.AudioMixerGroup ambienceGroup, musicGroup, uiGroup;
        [SerializeField]
        Vector2 distantInterval = new Vector2(19, 43);
        [SerializeField, Range(0, 1)]
        float ambientGain = .26f, musicGain = .18f, distantGain = .18f, hoverGain = .19f, confirmGain = .28f;
        float effects = .8f, musicLevel, fade = 1, lastHover;
        void Start()
        {
            if (ambientSource && ambientLoop)
            {
                ambientSource.clip = ambientLoop;
                ambientSource.loop = true;
                ambientSource.Play();
            }

            if (musicSource && music)
            {
                musicSource.clip = music;
                musicSource.loop = true;
                musicSource.Play();
            }

            Apply();
            StartCoroutine(Distant());
        }

        public void Levels(float sfx, float musicVolume)
        {
            effects = sfx;
            musicLevel = musicVolume;
            Apply();
        }

        public void FadeLevel(float level)
        {
            fade = level;
            Apply();
        }

        void Apply()
        {
            if (mixer)
            {
                mixer.SetFloat("MusicVolume", musicLevel <= .0001f ? -80 : 20 * Mathf.Log10(musicLevel));
                mixer.SetFloat("SFXVolume", effects <= .0001f ? -80 : 20 * Mathf.Log10(effects));
                mixer.SetFloat("AmbienceVolume", effects <= .0001f ? -80 : 20 * Mathf.Log10(effects));
            }

            if (ambientSource)
            {
                ambientSource.outputAudioMixerGroup = ambienceGroup;
                ambientSource.volume = ambientGain * (mixer ? 1 : effects) * fade;
            }

            if (musicSource)
            {
                musicSource.outputAudioMixerGroup = musicGroup;
                musicSource.volume = musicGain * (mixer ? 1 : musicLevel) * fade;
            }

            if (effectsSource)
            {
                effectsSource.outputAudioMixerGroup = uiGroup;
                effectsSource.volume = (mixer ? 1 : effects) * fade;
            }

            if (distantSource)
            {
                distantSource.outputAudioMixerGroup = ambienceGroup;
                distantSource.volume = distantGain * (mixer ? 1 : effects) * fade;
            }
        }

        public void Hover()
        {
            if (Time.unscaledTime - lastHover < .065f)
                return;
            lastHover = Time.unscaledTime;
            if (effectsSource && hoverTick)
                effectsSource.PlayOneShot(hoverTick, hoverGain);
        }

        public void Confirm()
        {
            if (effectsSource && confirmThud)
                effectsSource.PlayOneShot(confirmThud, confirmGain);
        }

        IEnumerator Distant()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(Random.Range(distantInterval.x, distantInterval.y));
                if (distantSource && distantSounds != null && distantSounds.Length > 0)
                {
                    var clip = distantSounds[Random.Range(0, distantSounds.Length)];
                    if (clip)
                    {
                        distantSource.panStereo = Random.Range(-.6f, .6f);
                        distantSource.PlayOneShot(clip);
                    }
                }
            }
        }
    }
}
