// Guarda as informações do apito usadas no checkpoint.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame
{
    [Serializable] public sealed class WhistleState
    {
        public int version=2,totalBlows;
        public bool owned,learnedTap,learnedHold,hesitated,inputHintShown,pickupLinePlayed,reserveBatteryUsed;
        public bool loreKnown,discovered,schoolHesitated,triedOutside,plainCommentPlayed,secretaryWhispered;
        public float tension,cooldownRemaining,rareCommentRemaining;
        public List<string> responders=new List<string>();
        public bool Valid
        {
            get
            {
                if((version!=1&&version!=2)||totalBlows<0||totalBlows>1000000||!float.IsFinite(tension)||tension<0||tension>100
                    ||!float.IsFinite(cooldownRemaining)||cooldownRemaining<0||cooldownRemaining>600
                    ||!float.IsFinite(rareCommentRemaining)||rareCommentRemaining<0||rareCommentRemaining>600
                    ||(discovered&&!loreKnown)||responders==null||responders.Count>512||(!owned&&(totalBlows>0||learnedTap||learnedHold)))return false;
                for(int i=0;i<responders.Count;i++){if(string.IsNullOrWhiteSpace(responders[i])||responders[i].Length>160)return false;for(int j=0;j<i;j++)if(responders[i]==responders[j])return false;}
                return true;
            }
        }
        public void Cool(float seconds,WhistleConfig config)
        {
            if(!float.IsFinite(seconds)||seconds<=0||!config)return;
            tension=Mathf.Max(0,tension-seconds*config.stressCoolingPerSecond);
            cooldownRemaining=Mathf.Max(0,cooldownRemaining-seconds);
            rareCommentRemaining=Mathf.Max(0,rareCommentRemaining-seconds);
        }
        public WhistleState Copy()=>new WhistleState{version=version,totalBlows=totalBlows,owned=owned,learnedTap=learnedTap,learnedHold=learnedHold,hesitated=hesitated,inputHintShown=inputHintShown,pickupLinePlayed=pickupLinePlayed,loreKnown=loreKnown,discovered=discovered,schoolHesitated=schoolHesitated,triedOutside=triedOutside,plainCommentPlayed=plainCommentPlayed,secretaryWhispered=secretaryWhispered,reserveBatteryUsed=reserveBatteryUsed,tension=tension,cooldownRemaining=cooldownRemaining,rareCommentRemaining=rareCommentRemaining,responders=new List<string>(responders)};
    }
}
