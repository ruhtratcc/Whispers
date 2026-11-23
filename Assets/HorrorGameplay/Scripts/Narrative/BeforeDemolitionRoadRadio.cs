// Troca rádio, noticiário, músicas, chamadas e a cantiga.

using System;
using UnityEngine;

namespace HorrorGame
{
    public sealed class BeforeDemolitionRoadRadio : MonoBehaviour
    {
        [Header("Human recordings — missing clips use the approved caption prototype")]
        public AudioClip news, announcement, thirdAnnouncement, nurserySong, vibration;
        public AudioClip hauntedChant;
        [SerializeField] float[] recordedNewsStarts = Array.Empty<float>();
        [Header("News, music stations, power off, then the impossible nursery tune")]
        public bool musicInterlude = true;
        public AudioClip[] musicTracks = new AudioClip[3];
        public AudioClip[] musicReactionVoices = new AudioClip[3];
        public AudioClip[] musicFollowupVoices = new AudioClip[3];
        public AudioClip radioOffVoice;
        public AudioClip restartReactionVoice, hauntedSongVoice, failedPowerVoice;
        [Tooltip("Enable only after replacing the old recordings with the revised lines.")] public bool revisedReactionsRecorded;
        [Header("Radio controls and arrival")]
        [Min(.1f)] public float entryFadeSeconds = .5f;
        [Min(2)] public float outsideRadioDistance = 32;
        public float Volume { get; private set; } = .75f;
        public bool EntryFaded { get; private set; }
        [Min(.1f)] public float restartStaticSeconds=1.6f;
        [Min(.1f)] public float hauntedAttemptDelay=18;
        [Min(.1f)] public float musicReactionDelay = 3;
        [Min(.1f)] public float musicFollowupDelay = 14;
        static readonly string[] MusicReactions = {"Não tô com cabeça pra isso.","Essa também não.","Não... chega."};
        static readonly string[] MusicFollowups = {"Vou tentar a próxima.","Só mais uma.","Prefiro o silêncio."};
        [Min(60)] public float newsSeconds = 75;
        [Min(1)] public float announcementSeconds = 12;
        [Min(2)] public float silenceSeconds = 2;
        [Min(10)] public float songSeconds = 22;
        [Range(.4f, 1)] public float thirdVoicePitch = .65f;
        public Transform radioAnchor, phoneAnchor;
        public AudioSource RadioSource { get; private set; }
        public AudioSource PhoneSource { get; private set; }
        public AudioSource RestartStaticSource { get; private set; }
        public AudioSource TuningSource => tuning;
        public bool HasFinalRecordings => news&&nurserySong&&(musicInterlude
            ? musicTracks!=null&&musicTracks.Length>=3&&Array.TrueForAll(musicTracks,c=>c)
            : announcement);
        public int PhaseStarts { get; private set; }
        public int VibrationCount { get; private set; }
        AudioSource buzz, tuning;
        AudioSource reactionVoice;
        bool musicReactionPresented;
        bool musicFollowupPresented;
        bool restartReactionPresented, hauntedSongReactionPresented;
        GameSession game;
        int appliedPhase = -1, newsLine = -1;
        float tuningAge;
        bool paused, newsPlaybackStarted;
        BeforeDemolitionRoadFlow road;
        Transform listener;
        AudioLowPassFilter radioLowPass;
        AudioReverbFilter radioRoom;
        bool entryFadeRequested;
        float entryGain = 1;
        public bool IsMusicPhase(RoadRadioPhase phase) => musicInterlude&&phase>=RoadRadioPhase.FirstAnnouncement&&phase<=RoadRadioPhase.ThirdAnnouncement;
        public AudioClip MusicClip(RoadRadioPhase phase)
        {
            int index=(int)phase-(int)RoadRadioPhase.FirstAnnouncement;
            return IsMusicPhase(phase)&&musicTracks!=null&&index<musicTracks.Length?musicTracks[index]:null;
        }

        AudioClip RoadChant => hauntedChant ? hauntedChant : nurserySong;

        public float Duration(RoadRadioPhase phase, bool skippedMusic = false)
        {
            switch (phase)
            {
                case RoadRadioPhase.News: return Mathf.Max(.05f, news ? news.length : newsSeconds);
                case RoadRadioPhase.FirstAnnouncement:
                case RoadRadioPhase.SecondAnnouncement: return musicInterlude?Mathf.Max(.05f,announcementSeconds):Mathf.Max(.05f, Mathf.Max(announcementSeconds, announcement ? announcement.length : 0));
                case RoadRadioPhase.ThirdAnnouncement:
                    if(musicInterlude)return Mathf.Max(.05f,announcementSeconds);
                    var clip = thirdAnnouncement ? thirdAnnouncement : announcement;
                    return Mathf.Max(.05f, Mathf.Max(announcementSeconds / Mathf.Max(.4f, thirdVoicePitch), clip ? clip.length / Mathf.Max(.4f, thirdVoicePitch) : 0));
                case RoadRadioPhase.Silence: return Mathf.Max(.05f, silenceSeconds);
                case RoadRadioPhase.Song: return skippedMusic ? 14 : Mathf.Max(.05f, songSeconds);
                default: return 0;
            }
        }
        public float TotalSeconds { get { float total = 0; for (int i = 0; i < 6; i++) total += Duration((RoadRadioPhase)i); return total; } }
        public float Progress(BeforeDemolitionRoadState state)
        {
            if (state.phase == RoadRadioPhase.Parked) return 1;
            if (state.skippedMusic)
                return Mathf.Lerp(state.skipStartProgress, 1, state.phaseAge / Duration(RoadRadioPhase.Song, true));
            float elapsed = state.phaseAge;
            for (int i = 0; i < (int)state.phase; i++) elapsed += Duration((RoadRadioPhase)i);
            return Mathf.Clamp01(elapsed / Mathf.Max(.1f, TotalSeconds));
        }
        public void Initialize(GameSession session)
        {
            if (RadioSource) return;
            game = session;
            road = game ? game.GetComponent<BeforeDemolitionRoadFlow>() : null;
            listener = game && game.player && game.player.view && game.player.view.playerCamera ? game.player.view.playerCamera.transform : null;
            RadioSource = Source("Rádio do carro", radioAnchor, .37f);
            RadioSource.gameObject.AddComponent<VoiceAudioPresentation>().Configure(RadioSource, VoicePlaybackStyle.Radio, false);
            RadioSource.volume = .37f * Volume;
            foreach (var source in RadioSource.GetComponentsInChildren<AudioSource>(true)) ConfigureOutsideSource(source);
            radioLowPass = RadioSource.GetComponent<AudioLowPassFilter>();
            radioRoom = RadioSource.GetComponent<AudioReverbFilter>();
            PhoneSource = Source("Chamada sem nome", phoneAnchor, .23f);
            PhoneSource.gameObject.AddComponent<VoiceAudioPresentation>().Configure(PhoneSource, VoicePlaybackStyle.Telephone, false);
            PhoneSource.volume = .23f;
            buzz = Source("Vibração do celular", phoneAnchor, .15f); buzz.spatialBlend = 1;
            AudioManager.Instance?.Route(buzz,AudioChannel.Phone);
            tuning = Source("Botões do rádio — interferência", radioAnchor, .22f);
            reactionVoice=Source("João — comentários no carro",null,.75f);reactionVoice.spatialBlend=0;
            AudioManager.Instance?.Route(reactionVoice,AudioChannel.Voice);
            var noise = Resources.Load<SoundSet>("VoiceRadioStatic");
            if (noise && noise.clips != null) tuning.clip = Array.Find(noise.clips, c => c);
            RestartStaticSource=Source("Rádio — retorno com chiado",radioAnchor,.16f);RestartStaticSource.clip=tuning.clip;
            ConfigureOutsideSource(tuning); ConfigureOutsideSource(RestartStaticSource);
        }
        void ConfigureOutsideSource(AudioSource source)
        {
            source.spatialBlend = 1; source.minDistance = 1;
            source.maxDistance = Mathf.Max(2, outsideRadioDistance); source.rolloffMode = AudioRolloffMode.Logarithmic;
        }
        public void SetVolume(float value, bool feedback = false)
        {
            if (!float.IsFinite(value)) return;
            Volume = Mathf.Clamp(value, .18f, 1);
            ApplyGain();
            if (feedback) AudioManager.Instance?.PlayCue("ui_click", radioAnchor ? radioAnchor.position : transform.position, .3f);
        }
        public void BeginEntryFade() { if (!EntryFaded) entryFadeRequested = true; }
        void ApplyGain()
        {
            if (!RadioSource) return;
            float outside = road && road.Exited && listener
                ? Mathf.InverseLerp(1, Mathf.Max(2, outsideRadioDistance), Vector3.Distance(listener.position, RadioSource.transform.position)) : 0;
            RadioSource.volume = (tuningAge > 0 ? .04f : IsMusicPhase((RoadRadioPhase)appliedPhase) ? .27f : .37f)
                * Volume * entryGain * Mathf.Lerp(1, .7f, outside);
            if (tuning) tuning.volume = .22f * Volume * entryGain;
            if (RestartStaticSource) RestartStaticSource.volume = .16f * Volume * entryGain;
            if (radioLowPass) radioLowPass.cutoffFrequency = Mathf.Lerp(3800, 650, Mathf.Sqrt(outside));
            if (radioRoom)
            {
                radioRoom.enabled = road && road.Exited;
                radioRoom.reverbLevel = Mathf.Lerp(-2800, -1600, outside);
            }
        }
        AudioSource Source(string name, Transform anchor, float volume)
        {
            var go = new GameObject(name); go.transform.SetParent(anchor ? anchor : transform, false);
            var source = go.AddComponent<AudioSource>(); source.playOnAwake = false; source.volume = volume;
            source.spatialBlend = .7f; source.minDistance = .3f; source.maxDistance = 5; source.dopplerLevel = 0;
            if (AudioManager.Instance) source.outputAudioMixerGroup = AudioManager.Instance.sfxGroup;
            return source;
        }
        public void ApplyPhase(RoadRadioPhase phase, float age, bool reconstruct = false, bool skippedMusic = false)
        {
            if (!RadioSource) return;
            bool useRestartStatic = phase == RoadRadioPhase.Song && musicInterlude && !skippedMusic;
            AudioClip chant = RoadChant;
            if ((int)phase != appliedPhase || reconstruct)
            {
                bool keepSong = phase == RoadRadioPhase.Parked && !reconstruct && RadioSource.clip == chant && RadioSource.isPlaying;
                if (!keepSong) StopSource(RadioSource);
                StopSource(reactionVoice);if(RestartStaticSource)RestartStaticSource.Stop();appliedPhase = (int)phase; newsLine = -1;
                newsPlaybackStarted = false;
                entryFadeRequested = EntryFaded = false; entryGain = 1;
                if(tuning)tuning.Stop();tuningAge=0;
                musicReactionPresented=reconstruct&&age>=musicReactionDelay;
                musicFollowupPresented=reconstruct&&age>=musicFollowupDelay;
                restartReactionPresented=reconstruct&&age>=3.5f;hauntedSongReactionPresented=reconstruct&&age>=10;
                if (!reconstruct) PhaseStarts++;
                AudioClip clip = IsMusicPhase(phase)?MusicClip(phase):phase == RoadRadioPhase.News ? news
                    : phase == RoadRadioPhase.ThirdAnnouncement ? (thirdAnnouncement ? thirdAnnouncement : announcement)
                    : phase == RoadRadioPhase.FirstAnnouncement || phase == RoadRadioPhase.SecondAnnouncement ? announcement
                    : phase == RoadRadioPhase.Parked || phase == RoadRadioPhase.Song && (!useRestartStatic || age >= restartStaticSeconds) ? chant : null;
                if (useRestartStatic && age < restartStaticSeconds && RestartStaticSource && RestartStaticSource.clip)
                {RestartStaticSource.loop=true;RestartStaticSource.time=age%RestartStaticSource.clip.length;RestartStaticSource.Play();}
                if (phase == RoadRadioPhase.Silence || phase == RoadRadioPhase.Parked)
                { StopSource(PhoneSource); if (buzz) buzz.Stop(); if (tuning) tuning.Stop(); tuningAge = 0; }
                if (clip && !keepSong)
                {
                    RadioSource.clip = clip;
                    RadioSource.loop = IsMusicPhase(phase) || phase == RoadRadioPhase.Song || phase == RoadRadioPhase.Parked;
                    RadioSource.pitch = phase == RoadRadioPhase.Parked ? .88f : !musicInterlude&&phase == RoadRadioPhase.ThirdAnnouncement ? thirdVoicePitch : 1;
                    float clipAge = useRestartStatic ? Mathf.Max(0, age - restartStaticSeconds) : age;
                    if (phase == RoadRadioPhase.News)
                        RadioSource.timeSamples = Mathf.Clamp(Mathf.RoundToInt(clipAge * clip.frequency), 0, Mathf.Max(0, clip.samples - 1));
                    else
                        RadioSource.time = RadioSource.loop ? clipAge % clip.length : Mathf.Min(clipAge * RadioSource.pitch, clip.length - .01f);
                    RadioSource.Play();
                }
                ApplyGain();
                if (IsMusicPhase(phase))
                {
                    game.hud?.ShowSubtitle("", .01f);
                    if (!reconstruct && phase != RoadRadioPhase.FirstAnnouncement) TuneMusic();
                }
                else if (phase >= RoadRadioPhase.FirstAnnouncement && phase <= RoadRadioPhase.ThirdAnnouncement)
                    game.hud?.ShowSubtitle("Rádio: " + BeforeDemolitionContent.RadioAnnouncement, Mathf.Max(.1f, Duration(phase) - age));
                else if (phase == RoadRadioPhase.Song)
                    game.hud?.ShowSubtitle(useRestartStatic ? "[o rádio liga sozinho. Chiado.]" : ChantCaption, 3);
                else if(phase==RoadRadioPhase.Silence&&musicInterlude&&!reconstruct)Say("Desligou. Melhor assim.",UpdatedVoice(radioOffVoice),Mathf.Max(.1f,silenceSeconds));
                else if (phase == RoadRadioPhase.Silence || phase == RoadRadioPhase.Parked) game.hud?.ShowSubtitle("", .01f);
            }
            if(IsMusicPhase(phase)&&!musicReactionPresented&&age>=musicReactionDelay)
            {
                musicReactionPresented=true;int index=(int)phase-(int)RoadRadioPhase.FirstAnnouncement;
                var voice=musicReactionVoices!=null&&index<musicReactionVoices.Length?UpdatedVoice(musicReactionVoices[index]):null;
                Say(MusicReactions[index],voice,voice?voice.length:3.5f);
            }
            if(IsMusicPhase(phase)&&!musicFollowupPresented&&age>=musicFollowupDelay)
            {
                musicFollowupPresented=true;int index=(int)phase-(int)RoadRadioPhase.FirstAnnouncement;
                var voice=musicFollowupVoices!=null&&index<musicFollowupVoices.Length?UpdatedVoice(musicFollowupVoices[index]):null;
                Say(MusicFollowups[index],voice,voice?voice.length:4.5f);
            }
            if (phase == RoadRadioPhase.News)
                PresentNewsSubtitle(age);
            if (useRestartStatic)
            {
                if (age >= restartStaticSeconds)
                {
                    if (RestartStaticSource && RestartStaticSource.isPlaying) RestartStaticSource.Stop();
                    if (chant && RadioSource.clip != chant)
                    {
                        RadioSource.clip = chant;
                        RadioSource.loop = true;
                        RadioSource.time = (age - restartStaticSeconds) % chant.length;
                        RadioSource.Play();
                        game.hud?.ShowSubtitle(ChantCaption, 3);
                    }
                }
                if(!restartReactionPresented&&age>=3.5f){restartReactionPresented=true;Say("Eu não liguei isso.",UpdatedVoice(restartReactionVoice),3);}
                if(!hauntedSongReactionPresented&&age>=10){hauntedSongReactionPresented=true;Say("De onde saiu essa voz?",UpdatedVoice(hauntedSongVoice),3);}
            }
            if (phase == RoadRadioPhase.Song && RadioSource.clip)
                RadioSource.pitch = .88f + .025f * Mathf.Sin(age * 2.1f) + .008f * Mathf.Sin(age * 8.7f);
        }
        string ChantCaption => hauntedChant ? "[canto em latim]" : "[cantiga infantil lenta e desafinada]";

        public bool TryGetNewsPlaybackAge(out float age)
        {
            age = 0;
            if (paused || !news || !RadioSource || RadioSource.clip != news || news.loadState != AudioDataLoadState.Loaded || news.frequency <= 0)
                return false;

            if (RadioSource.isPlaying)
            {
                newsPlaybackStarted = true;
                age = RadioSource.timeSamples / (float)news.frequency;
                return true;
            }
            if (!newsPlaybackStarted) return false;
            age = news.length;
            return true;
        }

        void PresentNewsSubtitle(float age)
        {
            if (TryGetNewsPlaybackAge(out float playbackAge)) age = playbackAge;
            if (news && age >= news.length) return;
            var normal = BeforeDemolitionContent.NormalNews;
            bool timed = news && recordedNewsStarts != null && recordedNewsStarts.Length == normal.Length + 1;
            int line;
            float end;
            if (timed)
            {
                line = recordedNewsStarts.Length - 1;
                while (line >= 0 && recordedNewsStarts[line] > age) line--;
                if (line < 0) return;
                end = line + 1 < recordedNewsStarts.Length ? recordedNewsStarts[line + 1] : news.length;
            }
            else
            {
                float secondsPerLine = Duration(RoadRadioPhase.News) / normal.Length;
                line = Mathf.Min(normal.Length - 1, Mathf.FloorToInt(age / secondsPerLine));
                end = (line + 1) * secondsPerLine;
            }
            if (line == newsLine) return;
            newsLine = line;
            string caption = line < normal.Length ? normal[line] : BeforeDemolitionContent.RadioAnnouncement;
            game.hud?.ShowSubtitle("Rádio: " + caption, Mathf.Max(.1f, end - age));
        }
        public void Tick(bool running, float dt)
        {
            if (!RadioSource) return;
            if (buzz && PhoneMessages.For(game)?.Acquired == true) buzz.Stop();
            if (!running && !paused) { foreach (var s in new[] { RadioSource, PhoneSource, buzz, tuning,reactionVoice,RestartStaticSource }) if (s && s.isPlaying) s.Pause(); paused = true; }
            else if (running && paused)
            {
                foreach (var s in new[] { RadioSource, PhoneSource, buzz, tuning, reactionVoice, RestartStaticSource }) if (s && s.clip) s.UnPause();
                if (appliedPhase == (int)RoadRadioPhase.News) newsLine = -1;
                paused = false;
            }
            if (!running) return;
            if (entryFadeRequested)
            {
                entryGain = Mathf.MoveTowards(entryGain, 0, Mathf.Max(0, dt) / Mathf.Max(.1f, entryFadeSeconds));
                if (entryGain <= 0 && !EntryFaded) { EntryFaded = true; StopSource(RadioSource); StopSource(PhoneSource); }
            }
            tuningAge = Mathf.Max(0, tuningAge - dt);
            if (tuningAge <= 0 && tuning && tuning.isPlaying) tuning.Stop();
            ApplyGain();
        }
        public void AttemptTune()
        {
            if (!tuning || tuningAge > 0) return;
            AudioManager.Instance?.PlayCue("ui_click",radioAnchor?radioAnchor.position:transform.position,.3f);
            tuningAge = .45f; if (tuning.clip) { tuning.time = 0; tuning.Play(); }
            ApplyGain();
            game.hud?.Notify("O rádio chia e volta para a mesma estação.");
        }
        public void TuneMusic()
        {
            tuningAge=.32f;ApplyGain();
            AudioManager.Instance?.PlayCue("ui_click",radioAnchor?radioAnchor.position:transform.position,.45f);
            if(tuning&&tuning.clip){tuning.time=0;tuning.Play();}
        }
        public void RefuseHauntedPower(bool first)
        {
            AttemptTune();
            if(first)Say("Não desliga...",UpdatedVoice(failedPowerVoice),4);
        }
        AudioClip UpdatedVoice(AudioClip clip) => revisedReactionsRecorded ? clip : null;
        void Say(string text,AudioClip voice,float seconds)
        {
            game.hud?.ShowSubtitle("João: "+text,voice ? Mathf.Max(seconds, voice.length) : seconds);
            if(!voice||!reactionVoice)return;reactionVoice.Stop();reactionVoice.clip=voice;reactionVoice.Play();
        }
        public void Vibrate()
        {
            if (!buzz || PhoneMessages.For(game)?.Acquired == true) return;
            buzz.clip = vibration;
            var messages = PhoneMessages.For(game);
            if (!buzz.clip && messages && messages.catalog.vibration) buzz.clip = messages.catalog.vibration.Next();
            buzz.volume = VibrationCount > 0 ? .23f : .15f;
            buzz.pitch = VibrationCount > 0 ? .98f : 1;
            if (buzz.clip) buzz.Play(); VibrationCount++;
        }
        public void BeginCall()
        {
            if (!PhoneSource || !nurserySong) return;
            PhoneSource.clip = nurserySong; PhoneSource.loop = true; PhoneSource.pitch = .94f;
            PhoneSource.time = ((RadioSource && RadioSource.clip == nurserySong ? RadioSource.time : 0) + .47f) % nurserySong.length;
            PhoneSource.Play();
        }
        public void EndCall() => StopSource(PhoneSource);
        static void StopSource(AudioSource source)
        {
            if (!source) return;
            source.Stop(); source.loop=false;source.clip = null;
            foreach (var child in source.GetComponentsInChildren<AudioSource>(true)) if (child != source) child.Stop();
        }
        public void StopAll()
        {
            StopSource(RadioSource); StopSource(PhoneSource); if (buzz) buzz.Stop(); if (tuning) tuning.Stop();
            StopSource(reactionVoice);
            if(RestartStaticSource)RestartStaticSource.Stop();
            paused = false; appliedPhase = -1; entryFadeRequested = EntryFaded = false; entryGain = 1;
        }
        void OnDisable() => StopAll();
    }
}
