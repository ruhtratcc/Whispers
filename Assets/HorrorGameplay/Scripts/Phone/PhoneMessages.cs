// Gerencia entrega, notificações, leitura e salvamento das conversas.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorGame
{
    [DefaultExecutionOrder(130), DisallowMultipleComponent]
    public sealed class PhoneMessages : MonoBehaviour
    {
        public GameSession game;
        public PhoneMessageCatalog catalog;
        public Key openKey=Key.P;
        public Vector2 bannerSize=new Vector2(570,132), bannerOffset=new Vector2(0,-28);
        public Vector2 phoneSize=new Vector2(450,860), phoneOffset=new Vector2(430,0);
        [Range(.1f,1)] public float screenLightIntensity=.22f;
        public PhoneMessageState State {get;private set;}=new PhoneMessageState();
        public bool IsOpen=>view&&view.IsOpen;
        public bool BannerVisible=>view&&view.BannerVisible;
        public bool RoadRead=>State.received.Any(m=>m.id=="road.01"&&m.read);
        public bool Acquired=>State.acquired;
        public PhoneMessagesView View=>view;
        public event Action Changed, ConversationRead;
        PhoneMessagesView view;
        public BeforeDemolitionRoadFlow road;
        public BeforeDemolitionSchoolFlow school;
        public BeforeDemolitionTwoVoicesFlow loop;
        public BeforeDemolitionMirrorFlow mirror;
        Inventory inventory;
        NarrativeDirector director;
        readonly Dictionary<GameObject,bool> hiddenHud=new Dictionary<GameObject,bool>();
        bool initialized, restoring, legacy, notificationDirty;
        bool ownsInspection;
        Action overlayClosed;
        int inputAfterFrame;
        public bool CanReadInput=>Time.frameCount>inputAfterFrame;

        public static PhoneMessages For(GameSession session)=>session?session.GetComponent<PhoneMessages>():null;
        void Awake()
        {
            if(!game)game=GetComponent<GameSession>();
            if(!road)road=GetComponent<BeforeDemolitionRoadFlow>();if(!school)school=GetComponent<BeforeDemolitionSchoolFlow>();
            if(!loop)loop=GetComponentInChildren<BeforeDemolitionTwoVoicesFlow>(true);if(!mirror)mirror=GetComponentInChildren<BeforeDemolitionMirrorFlow>(true);
            director=game?game.narrative:null;inventory=game&&game.interaction?game.interaction.inventory:null;
        }
        void OnEnable()
        {
            if(game)game.StateChanged+=GameStateChanged;
            if(director){director.Initialized+=Initialize;director.SignalAccepted+=Signal;director.ChapterInitialized+=Chapter;}
            if(inventory)inventory.Changed+=InventoryChanged;
            if(loop)loop.LoopRepeated+=LoopRepeated;
        }
        void Start(){EnsureView();if(director&&director.IsInitialized)Initialize();}
        void EnsureView()
        {
            if(view||!catalog||!game)return;
            var root=new GameObject("Celular — mensagens",typeof(RectTransform));root.transform.SetParent(transform,false);
            view=root.AddComponent<PhoneMessagesView>();view.Initialize(this);
        }
        void Initialize()
        {
            initialized=director&&director.IsInitialized&&BeforeDemolitionCampaign.Is(director.book);
            if(!initialized)return;
            EnsureView();
            if(inventory&&inventory.Contains(BeforeDemolitionRoadFlow.EmailDeliveryItemId)&&!State.Has("road.01"))
            {
                bool wasRead=inventory.Contains("bd.note.email.08");
                Trigger(PhoneTrigger.RoadSong);
                foreach(var message in State.received){message.announced=true;message.read|=wasRead;}
            }
            if(road&&road.IsActive&&GameClock.Instance&&GameClock.Instance.State.locked)
            {GameClock.Instance.State.locked=false;GameClock.Instance.State.minute=1620;}
            Evaluate();
            if(legacy){foreach(var m in State.received)m.announced=true;legacy=false;}
            Changed?.Invoke();
        }
        void Chapter(string id)=>Evaluate();
        void Signal(string id)=>Evaluate();
        void InventoryChanged(){if(initialized&&!game.IsRestoringCheckpoint)Evaluate();}
        void LoopRepeated(int count){Trigger(count==1?PhoneTrigger.LoopFirst:PhoneTrigger.LoopLater);}
        void Evaluate()
        {
            if(!initialized||restoring||!director||!director.IsInitialized||game.IsRestoringCheckpoint)return;
            // The road exit requires pickup; legacy school saves did not record ownership.
            if(game.gameObject.scene.name=="SchoolCEEP")State.acquired=true;
            if(director.HasSignal("bd.school.secretary"))Trigger(PhoneTrigger.Secretary);
            if(school&&school.State.phoneAnswered)Trigger(PhoneTrigger.LibraryWarning);
            if(inventory)
            {
                string[] items=BeforeDemolitionFourStudentsFlow.StudentItemIds;
                for(int i=0;i<items.Length;i++)if(inventory.Contains(items[i]))Trigger((PhoneTrigger)((int)PhoneTrigger.Marta+i));
                if(items.All(inventory.Contains))Trigger(PhoneTrigger.FourGathered);
            }
            if(loop&&loop.State.loopCount>0)Trigger(PhoneTrigger.LoopFirst);
            if(loop&&loop.State.loopCount>1)Trigger(PhoneTrigger.LoopLater);
            if(director.Progress.chapterId==BeforeDemolitionCampaign.MirrorChapterId)Trigger(PhoneTrigger.Mirror);
            if(mirror&&director.HasSignal("bd.mirror.choice"))
                Trigger(mirror.Data.choice==BeforeDemolitionEndingChoice.Refuse?PhoneTrigger.Refuse:PhoneTrigger.Sign);
        }
        public void Trigger(PhoneTrigger trigger)
        {
            if(!catalog)return;
            // A new catalogue may add a message to a trigger already present in an older checkpoint.
            if(State.triggers.Contains(trigger)){DeliverEligible();return;}
            State.triggers.Add(trigger);DeliverEligible();SaveChanged();
        }
        void DeliverEligible()
        {
            if(!catalog)return;
            bool changed=false;
            foreach(var definition in catalog.messages)
            {
                if(definition.replyOnly||!State.triggers.Contains(definition.trigger)||State.Has(definition.id)||definition.onlyWhenOpened&&!IsOpen)continue;
                if(definition.typingSeconds>0)State.pending.Add(new PendingPhoneMessage{id=definition.id,seconds=definition.typingSeconds});
                else Receive(definition);
                changed=true;
            }
            AdvanceAutomaticReplies();
            if(changed){notificationDirty=true;Changed?.Invoke();}
        }
        void Receive(PhoneMessageDefinition definition)
        {
            int minute=definition.trigger==PhoneTrigger.RoadSong||definition.trigger==PhoneTrigger.RoadIgnored?1627:GameClock.Instance?GameClock.Instance.State.minute:1627;
            State.received.Add(new PhoneMessageReceipt{id=definition.id,minute=minute,read=IsOpen||definition.prematureRead,announced=IsOpen||definition.outgoing,sendingSeconds=definition.outgoing?.5f:0});
        }
        public PhoneReplyExchange AvailableReply=>catalog?.exchanges?.FirstOrDefault(e=>!e.automatic&&CanReply(e));
        bool CanReply(PhoneReplyExchange e)=>e!=null&&State.received.Any(m=>m.id==e.afterMessageId||(e.afterAny!=null&&e.afterAny.Contains(m.id)))
            &&!State.choices.Any(c=>c.exchangeId==e.id)&&e.sentIds!=null&&e.responseIds!=null&&e.sentIds.Length==e.responseIds.Length;
        void AdvanceAutomaticReplies()
        {
            if(catalog?.exchanges==null)return;
            foreach(var e in catalog.exchanges)if(e.automatic&&CanReply(e))SendReply(e,0);
        }
        public bool ChooseReply(string exchangeId,int option)
        {
            if(!IsOpen||game.State!=GameState.Inspecting)return false;
            var e=AvailableReply;if(e==null||e.id!=exchangeId||!SendReply(e,option))return false;
            Changed?.Invoke();SaveChanged();return true;
        }
        bool SendReply(PhoneReplyExchange exchange,int option)
        {
            if(!CanReply(exchange)||option<0||option>=exchange.sentIds.Length)return false;
            var sent=catalog.Find(exchange.sentIds[option]);var response=catalog.Find(exchange.responseIds[option]);
            if(sent==null||response==null)return false;
            State.choices.Add(new PhoneReplyChoice{exchangeId=exchange.id,option=option});
            if(IsOpen)AudioManager.Instance?.PlayCue("phone_send",game.player.transform.position,.14f);
            Receive(sent);State.pending.Add(new PendingPhoneMessage{id=response.id,seconds=1.2f});return true;
        }
        public PhoneMessageState Capture()=>State.Copy();
        public void Acquire()
        {
            if(Acquired)return;State.acquired=true;State.version=2;Changed?.Invoke();SaveChanged();
        }
        public void Restore(PhoneMessageState saved)
        {
            restoring=true;
            try
            {
                Close();if(saved!=null&&!saved.Valid)throw new ArgumentException("Mensagens inválidas.");
                State=saved?.Copy()??new PhoneMessageState();
                if(game&&game.gameObject.scene.name=="SchoolCEEP"||saved!=null&&saved.version==1&&saved.received.Count>0)State.acquired=true;
                State.version=2;
                legacy=saved==null||State.triggers.Count==0&&State.received.Count==0&&State.pending.Count==0;
            }
            finally{restoring=false;}
            notificationDirty=true;Changed?.Invoke();
        }
        void Update()
        {
            if(!initialized||!game||game.IsRestoringCheckpoint)return;
            bool active=game.State==GameState.Playing||IsOpen&&game.State==GameState.Inspecting;
            if(active)
            {
                bool changed=false;
                foreach(var receipt in State.received)if(receipt.sendingSeconds>0)
                {receipt.sendingSeconds=Mathf.Max(0,receipt.sendingSeconds-Time.deltaTime);if(receipt.sendingSeconds==0)changed=true;}
                for(int i=State.pending.Count-1;i>=0;i--)
                {
                    var pending=State.pending[i];pending.seconds=Mathf.Max(0,pending.seconds-Time.deltaTime);
                    if(pending.seconds>0)continue;
                    State.pending.RemoveAt(i);var definition=catalog.Find(pending.id);if(definition!=null)Receive(definition);changed=true;
                }
                if(changed){AdvanceAutomaticReplies();notificationDirty=true;Changed?.Invoke();SaveChanged();}
            }
            if(notificationDirty){notificationDirty=false;if(view)view.Refresh();}
            var keys=Keyboard.current;
            if(IsOpen&&CanReadInput&&game.State==GameState.Inspecting&&keys!=null&&keys.f10Key.wasPressedThisFrame)game.Pause();
            if(CanReadInput&&keys!=null&&keys[openKey].wasPressedThisFrame)
            {
                if(IsOpen)Close();
                else if(Acquired&&game.CanControl)RequestOpen();
            }
            if(IsOpen&&game.State!=GameState.Inspecting&&game.State!=GameState.Paused)Close();
            if (CanShowBanner && State.received.Any(m => !m.announced))
            {
                BannerAnnounced();
                if (road && road.IsActive && road.driver.Seated) road.OpenPhone(true);
                else OpenDirect();
            }
            if(view)view.Tick(Time.deltaTime);
        }
        public bool CanShowBanner=>Acquired&&initialized&&game&&game.CanControl&&!game.IsRestoringCheckpoint
            &&director&&!director.SequencePlaying&&(!road||!road.Glancing)&&!IsOpen;
        public void RequestOpen()
        {
            if(!Acquired||!game||!game.CanControl)return;
            if(road&&road.IsActive&&road.driver.Seated)road.OpenPhone();else OpenDirect();
        }
        public bool OpenDirect()
        {
            EnsureView();if(!Acquired||IsOpen||!view||!game.BeginInspection())return false;
            ownsInspection=true;return Present(520);
        }
        public bool OpenOverEnding(Action closed)
        {
            EnsureView();if(!Acquired||IsOpen||!view||game.State!=GameState.Inspecting||director.SequencePlaying)return false;
            ownsInspection=false;overlayClosed=closed;return Present(570);
        }
        bool Present(int order)
        {
            HideHud();view.Open(order);DeliverEligible();
            foreach(var message in State.received){message.read=true;message.announced=true;}
            view.Refresh();ConversationRead?.Invoke();SaveChanged();return true;
        }
        public void Close()
        {
            if(!IsOpen)return;
            view.Close();foreach(var pair in hiddenHud)if(pair.Key)pair.Key.SetActive(pair.Value);hiddenHud.Clear();
            if(game&&ownsInspection)game.EndInspection();var callback=overlayClosed;overlayClosed=null;callback?.Invoke();
        }
        void HideHud()
        {
            var h=game.hud;if(!h)return;
            var groups=new[]{h.objectiveGroup?h.objectiveGroup.gameObject:null,h.interactionGroup?h.interactionGroup.gameObject:null,
                h.batteryGroup?h.batteryGroup.gameObject:null,h.staminaGroup?h.staminaGroup.gameObject:null,
                h.crosshair?h.crosshair.gameObject:null,h.notification?h.notification.gameObject:null};
            foreach(var go in groups)if(go&&!hiddenHud.ContainsKey(go)){hiddenHud[go]=go.activeSelf;go.SetActive(false);}
        }
        public void RemindUnread()
        {
            foreach(var message in State.received)if(!message.read)message.announced=false;
            if(game&&game.player&&catalog)AudioManager.Instance?.Play(catalog.vibration,game.player.transform.position,.05f,AudioChannel.Phone,false);
            notificationDirty=true;Changed?.Invoke();
        }
        public void BannerAnnounced()
        {
            foreach(var message in State.received)if(!message.announced)message.announced=true;
            AudioManager.Instance?.Play(catalog.notification,game.player.transform.position,.16f,AudioChannel.SFX,false);
            if(game.hud&&game.hud.subtitleGroup&&game.hud.subtitleGroup.alpha<.05f)game.hud.ShowSubtitle(catalog.soundCaption,1.5f);
            SaveChanged();
        }
        // Keep old UI callbacks compatible; the composer never writes on its own.
        public void ClickComposer() { }
        void SaveChanged(){if(!restoring&&director&&director.IsInitialized&&!game.IsRestoringCheckpoint)director.ScheduleSave();}
        void GameStateChanged(GameState state){inputAfterFrame=Time.frameCount;}
        void OnDisable()
        {
            if(game)game.StateChanged-=GameStateChanged;
            Close();if(director){director.Initialized-=Initialize;director.SignalAccepted-=Signal;director.ChapterInitialized-=Chapter;}
            if(inventory)inventory.Changed-=InventoryChanged;if(loop)loop.LoopRepeated-=LoopRepeated;
        }
    }
}
