// Guarda mensagens, fotos e gatilhos de entrega do celular.

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace HorrorGame
{
    public enum PhoneTrigger { RoadSong, RoadIgnored, Secretary, LibraryWarning, Marta, Caio, Luan, Bia, FourGathered, LoopFirst, LoopLater, Mirror, Refuse, Sign, ZoneLibrary, ZoneCanteen, ZoneStairs, ZoneMirror, RouteFour, RouteLoop, ZoneArts, ZoneComputing }

    [Serializable]
    public sealed class PhoneMessageDefinition
    {
        public string id;
        public PhoneTrigger trigger;
        [TextArea] public string text;
        public bool child, prematureRead, photo, onlyWhenOpened;
        public bool replyOnly, outgoing;
        [Range(0,30)] public float typingSeconds;
        public Texture2D image;
    }

    [Serializable] public sealed class PhoneReplyExchange
    {
        public string id, afterMessageId;
        public string[] afterAny=Array.Empty<string>();
        public bool automatic;
        public string[] sentIds=Array.Empty<string>(), responseIds=Array.Empty<string>();
    }

    [CreateAssetMenu(menuName="Whispers/Phone/Messages")]
    public sealed class PhoneMessageCatalog : ScriptableObject
    {
        public PhoneMessageDefinition[] messages = Array.Empty<PhoneMessageDefinition>();
        public PhoneReplyExchange[] exchanges=Array.Empty<PhoneReplyExchange>();
        public TMP_FontAsset font, childFont;
        public Sprite roundedPanel, icon, windowDot;
        public SoundSet notification, vibration, typing;
        public string appName="Mensagens", sender="Número desconhecido", number="(00) 00307-0307";
        public string now="agora", today="Hoje", read="Lida", typingLabel="digitando…", composer="Mensagem";
        public string autoTyped="", noSignal="Sem sinal", close="Fechar [P / Esc]";
        public string soundCaption="[O celular vibra.]", photoPlaceholder="Imagem ainda não disponível.";
        public string singleMessage="1 mensagem", multipleMessages="{0} mensagens", empty="Nenhuma mensagem por enquanto.";
        public string viewMessage="Ver mensagem";
        public string sending="Enviando…", delivered="Entregue";
        public string signal="Sinal: 3/4", messagesRead="Mensagens lidas", newMessages="Novas mensagens";
        public string inventoryTitle="Mensagens no celular", inventoryText="Um número desconhecido escreveu no celular.";
        [Range(0,100)] public int batteryPercent=76;
        public PhoneMessageDefinition Find(string id)=>Array.Find(messages,m=>m!=null&&m.id==id);
    }

    [Serializable] public sealed class PhoneMessageReceipt
    {
        public string id;
        public int minute;
        public bool read, announced;
        public float sendingSeconds;
    }
    [Serializable] public sealed class PhoneReplyChoice
    {
        public string exchangeId;
        public int option;
    }
    [Serializable] public sealed class PendingPhoneMessage
    {
        public string id;
        public float seconds;
    }
    [Serializable] public sealed class PhoneMessageState
    {
        public int version=2;
        public bool composerUsed, acquired;
        public List<PhoneTrigger> triggers=new List<PhoneTrigger>();
        public List<PhoneMessageReceipt> received=new List<PhoneMessageReceipt>();
        public List<PendingPhoneMessage> pending=new List<PendingPhoneMessage>();
        public List<PhoneReplyChoice> choices=new List<PhoneReplyChoice>();
        public bool Has(string id)=>received.Any(m=>m.id==id)||pending.Any(m=>m.id==id);
        public bool Valid=>(version==1||version==2)&&triggers!=null&&received!=null&&pending!=null&&choices!=null&&triggers.Count<=32
            &&choices.Count<=16&&choices.All(c=>c!=null&&!string.IsNullOrWhiteSpace(c.exchangeId)&&c.exchangeId.Length<=80&&c.option>=0&&c.option<8)
            &&choices.Select(c=>c.exchangeId).Distinct().Count()==choices.Count
            &&triggers.All(t=>Enum.IsDefined(typeof(PhoneTrigger),t))&&triggers.Distinct().Count()==triggers.Count
            &&received.Count+pending.Count<=128
            &&received.All(m=>m!=null&&!string.IsNullOrWhiteSpace(m.id)&&m.id.Length<=80&&m.minute>=0&&m.minute<=2880&&float.IsFinite(m.sendingSeconds)&&m.sendingSeconds>=0&&m.sendingSeconds<=1)
            &&pending.All(m=>m!=null&&!string.IsNullOrWhiteSpace(m.id)&&m.id.Length<=80&&float.IsFinite(m.seconds)&&m.seconds>=0&&m.seconds<=30)
            &&received.Select(m=>m.id).Concat(pending.Select(m=>m.id)).Distinct().Count()==received.Count+pending.Count;
        public PhoneMessageState Copy()=>JsonUtility.FromJson<PhoneMessageState>(JsonUtility.ToJson(this));
    }
}
