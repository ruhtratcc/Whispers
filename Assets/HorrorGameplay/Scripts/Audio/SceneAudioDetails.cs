// Aplica detalhes sonoros próprios de cada cenário.

using System;
using UnityEngine;

namespace HorrorGame
{
    [DisallowMultipleComponent,DefaultExecutionOrder(160)]
    public sealed class SceneAudioDetails : MonoBehaviour
    {
        [Serializable] public sealed class Area
        {
            public string name;
            public Bounds bounds;
            [Range(0,1)] public float ambience=.45f;
        }
        public GameSession game;
        public Area[] areas=Array.Empty<Area>();
        AudioManager audio;
        BeforeDemolitionRoadFlow road;
        BeforeDemolitionMirrorFlow mirror;
        bool dawnPlayed;
        AudioSource mirrorTone;
        AudioClip toneClip;
        int mirrorStage=-1;
        string scene;
        void Start()
        {
            if(!game)game=GetComponent<GameSession>();audio=AudioManager.Instance;scene=gameObject.scene.name;
            road=GetComponent<BeforeDemolitionRoadFlow>();mirror=GetComponent<BeforeDemolitionMirrorFlow>();
        }
        void Update()
        {
            if(!game||!audio)return;
            float layer=scene=="Chapter1Office"?.25f:scene=="RoadJourney"?.12f:.45f;
            if(scene=="SchoolCEEP"&&game.player)
            {
                foreach(var area in areas)if(area!=null&&area.bounds.Contains(game.player.transform.position)){layer=area.ambience;break;}
                if(game.player.transform.position.y>3)layer*=.8f;
            }
            if(road&&road.Parked)layer=0;
            audio.SetAmbienceLayer(layer);
            bool nearMirror=mirror&&mirror.IsActive&&mirror.Data.stage==0&&mirror.mirrorPlane&&game.player&&Vector3.Distance(game.player.transform.position,mirror.mirrorPlane.position)<4&&game.CanControl;
            if(nearMirror)
            {
                if(!mirrorTone&&audio.cues&&audio.cues.tensionMusic){toneClip=audio.cues.tensionMusic;mirrorTone=audio.PlayClip(toneClip,mirror.mirrorPlane.position,0,1,AudioChannel.Music,false);}
                if(mirrorTone&&mirrorTone.clip==toneClip)mirrorTone.volume=Mathf.MoveTowards(mirrorTone.volume,.07f,Time.deltaTime*.018f);
            }
            else if(mirrorTone){if(mirrorTone.clip==toneClip)mirrorTone.Stop();mirrorTone=null;}
            if(mirror&&mirrorStage==0&&mirror.Data.stage==1&&!game.IsRestoringCheckpoint)audio.PlayCue("ui_click",game.player.transform.position,.12f);
            if(mirror)mirrorStage=mirror.Data.stage;
            if(road&&road.Parked&&audio.ambience)audio.ambience.Stop();
            else if(road&&audio.ambience&&audio.ambience.clip&&!audio.ambience.isPlaying&&!AudioListener.pause)audio.ambience.Play();
            if(!dawnPlayed&&mirror&&mirror.Data.choice==BeforeDemolitionEndingChoice.Refuse&&game.narrative.Progress.endingSeen)
            {dawnPlayed=true;audio.PlayCue("dawn_birds",game.player.transform.position,.15f);}
        }
        void OnDisable(){if(mirrorTone&&mirrorTone.clip==toneClip)mirrorTone.Stop();mirrorTone=null;}
    }
}
