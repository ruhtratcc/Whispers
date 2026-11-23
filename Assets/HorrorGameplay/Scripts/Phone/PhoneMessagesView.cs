// Monta a interface das conversas do celular.

using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HorrorGame
{
    public sealed class PhoneMessagesView : MonoBehaviour
    {
        public bool IsOpen{get;private set;}
        public bool BannerVisible=>bannerActive&&banner.gameObject.activeInHierarchy;
        public ScrollRect ConversationScroll=>scroll;
        public Button BannerButton{get;private set;}
        public Button ComposerButton{get;private set;}
        PhoneMessages owner;
        PhoneMessageCatalog data;
        RectTransform banner,phone,content,contactNumber,choicesPanel,list,screen;
        public RectTransform Window=>phone;
        public RectTransform Header{get;private set;}
        public Button CloseButton{get;private set;}
        public Button MinimizeButton{get;private set;}
        public Button ResetButton{get;private set;}
        public Button ContactButton{get;private set;}
        public const string PositionKey="Whispers.PhoneWindow.Position";
        Canvas canvas;
        ScrollRect scroll;
        TMP_Text preview,composer,typingDots;
        Light glow;
        bool bannerActive,freeCursor,viewEnabled;
        float bannerAge;
        int bannerCount,openedFrame;
        Coroutine scrollRoutine;
        Vector2 defaultPosition,dragStart,windowStart;
        bool dragging,scrollWasEnabled;
        int lastTypingDot=-1;

        public void Initialize(PhoneMessages service)
        {
            owner=service;data=owner.catalog;
            canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=520;canvas.pixelPerfect=true;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=1;
            gameObject.AddComponent<GraphicRaycaster>();
            banner=Box(transform,"Notificação de mensagens",owner.bannerSize,owner.bannerOffset,new Color(1,1,1,.92f));
            banner.anchorMin=banner.anchorMax=banner.pivot=new Vector2(.5f,1);
            Shadow(banner);
            var appIcon=Box(banner,"Ícone de mensagens",new Vector2(28,28),new Vector2(22,-16),new Color(.17f,.63f,.29f));TopLeft(appIcon);
            if(data.icon){var image=appIcon.GetComponent<Image>();image.sprite=data.icon;image.type=Image.Type.Simple;image.color=Color.white;image.preserveAspect=true;}
            Label(banner,data.appName,new Vector2(owner.bannerSize.x-120,28),new Vector2(61,-15),20,new Color(.34f,.34f,.34f));
            var now=Label(banner,data.now,new Vector2(85,28),new Vector2(owner.bannerSize.x-105,-15),18,new Color(.4f,.4f,.4f));now.alignment=TextAlignmentOptions.TopRight;
            var title=Label(banner,data.sender,new Vector2(owner.bannerSize.x-44,32),new Vector2(22,-49),22,Color.black);title.fontStyle=FontStyles.Bold;
            preview=Label(banner,"",new Vector2(owner.bannerSize.x-44,39),new Vector2(22,-82),22,new Color(.15f,.15f,.15f));preview.textWrappingMode=TextWrappingModes.NoWrap;preview.overflowMode=TextOverflowModes.Ellipsis;
            BannerButton=banner.gameObject.AddComponent<Button>();BannerButton.targetGraphic=banner.GetComponent<Image>();BannerButton.onClick.AddListener(owner.RequestOpen);banner.gameObject.SetActive(false);

            // Keep the phone above the existing subtitle strip without moving the game's HUD.
            float captionTop=240;
            if(owner.game.hud&&owner.game.hud.subtitle){var corners=new Vector3[4];owner.game.hud.subtitle.rectTransform.GetWorldCorners(corners);captionTop=Mathf.Clamp(corners.Max(c=>c.y)/Mathf.Max(1,Screen.height)*1080,0,450);}
            var size=owner.phoneSize;size.y=Mathf.Min(size.y,1080-captionTop-50);
            var position=owner.phoneOffset;position.y=Mathf.Clamp(position.y,captionTop+20-540+size.y*.5f,520-size.y*.5f);
            defaultPosition=position;
            phone=Rect(transform,"Celular de João",size,position);
            RestoreWindowPosition();
            screen=Box(phone,"Tela",size,Vector2.zero,new Color(.97f,.97f,.96f));Shadow(screen);
            float width=screen.rect.width,height=screen.rect.height;
            Header=Box(screen,"Cabeçalho",new Vector2(width,52),Vector2.zero,new Color(.94f,.94f,.94f));TopLeft(Header);
            Header.gameObject.AddComponent<PhoneWindowDrag>().view=this;
            CloseButton=WindowDot("Fechar",18,new Color(.94f,.3f,.27f),owner.Close);
            MinimizeButton=WindowDot("Minimizar",38,new Color(.97f,.72f,.16f),owner.Close);
            ResetButton=WindowDot("Restaurar",58,new Color(.19f,.75f,.32f),ResetWindowPosition);
            var contactLabel=Label(Header,data.sender,new Vector2(width-160,36),new Vector2(80,-8),20,new Color(.11f,.11f,.12f));contactLabel.alignment=TextAlignmentOptions.Center;contactLabel.fontWeight=FontWeight.SemiBold;contactLabel.raycastTarget=true;
            ContactButton=contactLabel.gameObject.AddComponent<Button>();ContactButton.targetGraphic=contactLabel;ContactButton.onClick.AddListener(()=>{contactNumber.gameObject.SetActive(!contactNumber.gameObject.activeSelf);UpdateListLayout();});
            contactNumber=Box(screen,"Informações do contato",new Vector2(width-24,34),new Vector2(12,-52),Color.white);TopLeft(contactNumber);
            var number=Label(contactNumber,data.number,new Vector2(width-40,30),new Vector2(8,-2),14,new Color(.4f,.4f,.4f));number.alignment=TextAlignmentOptions.Center;contactNumber.gameObject.SetActive(false);
            list=Box(screen,"Conversa",new Vector2(width-24,height-128),new Vector2(12,-70),Color.white);TopLeft(list);
            scroll=list.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=35;scroll.movementType=ScrollRect.MovementType.Clamped;
            var viewport=Box(list,"Viewport",list.sizeDelta,Vector2.zero,Color.white);TopLeft(viewport);viewport.gameObject.AddComponent<Mask>().showMaskGraphic=false;
            content=Rect(viewport,"Mensagens recebidas",new Vector2(list.rect.width,0),Vector2.zero);TopLeft(content);
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(8,8,12,16);layout.spacing=4;layout.childControlHeight=layout.childControlWidth=true;layout.childForceExpandHeight=false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=content;
            choicesPanel=Rect(screen,"Respostas de João",new Vector2(width-24,0),new Vector2(12,-height+59));TopLeft(choicesPanel);
            var composerRect=Box(screen,"Barra de mensagem",new Vector2(width-24,48),new Vector2(12,-height+59),new Color(.9f,.9f,.9f));TopLeft(composerRect);
            composer=Label(composerRect,data.composer,new Vector2(width-48,40),new Vector2(12,-4),20,new Color(.5f,.5f,.5f));
            ComposerButton=composerRect.gameObject.AddComponent<Button>();ComposerButton.targetGraphic=composerRect.GetComponent<Image>();ComposerButton.interactable=false;var composerColors=ComposerButton.colors;composerColors.disabledColor=Color.white;ComposerButton.colors=composerColors;
            var lightRoot=new GameObject("Luz fraca do celular");lightRoot.transform.SetParent(owner.game.player.view.playerCamera.transform,false);lightRoot.transform.localPosition=new Vector3(.12f,-.15f,.15f);
            glow=lightRoot.AddComponent<Light>();glow.type=LightType.Point;glow.range=1.5f;glow.color=new Color(.65f,.78f,1);glow.intensity=owner.screenLightIntensity;glow.shadows=LightShadows.None;glow.enabled=false;
            phone.gameObject.SetActive(false);owner.Changed+=Refresh;
        }
        Button WindowDot(string name,float x,Color color,UnityEngine.Events.UnityAction action)
        {
            var hit=Rect(Header,name,new Vector2(20,32),new Vector2(x-4,-9));TopLeft(hit);
            var dot=Box(hit,"Bolinha",new Vector2(12,12),new Vector2(4,-10),color);TopLeft(dot);
            if(data.windowDot){dot.GetComponent<Image>().sprite=data.windowDot;dot.GetComponent<Image>().type=Image.Type.Simple;}
            var shine=Box(dot,"Brilho",new Vector2(5,2),new Vector2(3,-2),new Color(1,1,1,.38f));TopLeft(shine);shine.GetComponent<Image>().raycastTarget=false;
            var ray=hit.gameObject.AddComponent<Image>();ray.color=Color.clear;
            var button=hit.gameObject.AddComponent<Button>();button.targetGraphic=dot.GetComponent<Image>();
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.18f,1.18f,1.18f);button.colors=colors;button.onClick.AddListener(action);return button;
        }
        static void TopLeft(RectTransform rect){rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);}
        RectTransform Rect(Transform parent,string name,Vector2 size,Vector2 position)
        {var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=size;rect.anchoredPosition=position;return rect;}
        RectTransform Box(Transform parent,string name,Vector2 size,Vector2 position,Color color)
        {var rect=Rect(parent,name,size,position);var image=rect.gameObject.AddComponent<Image>();image.sprite=data.roundedPanel;image.type=Image.Type.Sliced;image.color=color;return rect;}
        void Shadow(RectTransform root){var shadow=root.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.24f);shadow.effectDistance=new Vector2(3,-5);shadow.useGraphicAlpha=true;}
        TMP_Text Label(Transform parent,string text,Vector2 size,Vector2 position,int fontSize,Color color,bool child=false)
        {var rect=Rect(parent,"Texto",size,position);TopLeft(rect);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.font=child?data.childFont:data.font;label.text=text;label.fontSize=fontSize;label.color=color;label.enableAutoSizing=false;label.extraPadding=false;label.richText=false;label.raycastTarget=false;label.overflowMode=TextOverflowModes.Truncate;return label;}
        public void Open(int order=520){canvas.sortingOrder=order;IsOpen=true;openedFrame=Time.frameCount;bannerActive=false;banner.gameObject.SetActive(false);phone.gameObject.SetActive(true);if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);Refresh();}
        public void Close(){if(dragging)EndWindowDrag();IsOpen=false;phone.gameObject.SetActive(false);if(glow)glow.enabled=false;composer.text=data.composer;}
        public void Refresh()
        {
            if(!IsOpen)return;
            for(int i=content.childCount-1;i>=0;i--){content.GetChild(i).gameObject.SetActive(false);Destroy(content.GetChild(i).gameObject);}
            typingDots=null;lastTypingDot=-1;
            if(owner.State.received.Count==0)Stamp(data.empty);
            int previousMinute=-1;bool previousOutgoing=false,first=true;
            foreach(var receipt in owner.State.received)
            {
                var definition=data.Find(receipt.id);if(definition==null)continue;
                var date=GameClock.NightStart.AddMinutes(receipt.minute);
                string day=GameClock.Instance&&date.Date==GameClock.Instance.Now.Date?data.today:date.ToString("dd/MM/yyyy");
                bool senderChange=!first&&previousOutgoing!=definition.outgoing;
                if(first||previousMinute!=receipt.minute||senderChange||definition.prematureRead)
                    Stamp(day+" "+date.ToString("HH:mm")+(definition.prematureRead?" · "+data.read:""));
                first=false;previousMinute=receipt.minute;previousOutgoing=definition.outgoing;
                if(definition.photo)
                {
                    float width=Mathf.Floor((list.rect.width-16)*.65f);
                    float aspect=definition.image?(float)definition.image.width/definition.image.height:4f/3;
                    float height=Mathf.Ceil(width/aspect);
                    var row=MessageRow(receipt.id,height);
                    var imageRect=Box(row,"Foto",new Vector2(width,height),Vector2.zero,new Color(.13f,.15f,.16f));TopLeft(imageRect);
                    if(definition.image){imageRect.gameObject.AddComponent<Mask>().showMaskGraphic=false;var child=Rect(imageRect,"Imagem",imageRect.sizeDelta,Vector2.zero);var image=child.gameObject.AddComponent<RawImage>();image.texture=definition.image;image.raycastTarget=false;var fitter=child.gameObject.AddComponent<AspectRatioFitter>();fitter.aspectRatio=aspect;fitter.aspectMode=AspectRatioFitter.AspectMode.FitInParent;}
                    else Label(imageRect,data.photoPlaceholder,new Vector2(width-28,height-16),new Vector2(14,-8),18,new Color(.85f,.85f,.85f));
                }
                else
                {
                    Bubble(receipt.id,definition.text,definition.outgoing,definition.child);
                }
                if(definition.outgoing)Stamp(receipt.sendingSeconds>0?data.sending:data.delivered,true);
            }
            if(owner.State.pending.Count>0)typingDots=Bubble("Digitando","...",false,false);
            RefreshChoices();UpdateListLayout();
            if(scrollRoutine!=null)StopCoroutine(scrollRoutine);scrollRoutine=StartCoroutine(ScrollToBottom());
        }
        RectTransform MessageRow(string id,float height)
        {
            var row=Rect(content,id,new Vector2(list.rect.width-16,height),Vector2.zero);row.gameObject.AddComponent<LayoutElement>().preferredHeight=height;
            return row;
        }
        void Stamp(string text,bool right=false)
        {
            var row=MessageRow("Carimbo",right?26:34);
            var label=Label(row,text,new Vector2(list.rect.width-32,right?26:30),new Vector2(8,-2),14,new Color(.43f,.43f,.46f));
            label.alignment=right?TextAlignmentOptions.Right:TextAlignmentOptions.Center;
        }
        TMP_Text Bubble(string id,string text,bool outgoing,bool child)
        {
            float maxWidth=Mathf.Floor((list.rect.width-16)*.75f);
            var row=MessageRow(id,0);
            var bubble=Box(row,outgoing?"Enviada":"Recebida",Vector2.one,Vector2.zero,outgoing?new Color(.04f,.52f,1):child?new Color(.81f,.82f,.83f):new Color(.91f,.91f,.92f));TopLeft(bubble);bubble.GetComponent<Image>().raycastTarget=false;
            var label=Label(bubble,text,new Vector2(maxWidth-28,1000),new Vector2(14,-8),child?22:20,outgoing?Color.white:new Color(.11f,.11f,.12f),child);
            float width=Mathf.Min(maxWidth,Mathf.Max(48,Mathf.Ceil(label.GetPreferredValues(text).x)+28));
            float height=Mathf.Max(42,Mathf.Ceil(label.GetPreferredValues(text,width-28,10000).y)+16);
            bubble.sizeDelta=new Vector2(width,height);label.rectTransform.sizeDelta=new Vector2(width-28,height-16);
            if(outgoing){bubble.anchorMin=bubble.anchorMax=bubble.pivot=new Vector2(1,1);bubble.anchoredPosition=Vector2.zero;}
            if(child)bubble.localRotation=Quaternion.Euler(0,0,-1.1f);
            row.GetComponent<LayoutElement>().preferredHeight=height;
            return label;
        }
        void RefreshChoices()
        {
            for(int i=choicesPanel.childCount-1;i>=0;i--){choicesPanel.GetChild(i).gameObject.SetActive(false);Destroy(choicesPanel.GetChild(i).gameObject);}
            var exchange=owner.AvailableReply;int count=exchange?.sentIds.Length??0;
            choicesPanel.sizeDelta=new Vector2(screen.rect.width-24,count*51);
            choicesPanel.anchoredPosition=new Vector2(12,-screen.rect.height+65+count*51);
            for(int i=0;i<count;i++)
            {
                int option=i;var rect=Box(choicesPanel,"Resposta "+i,new Vector2(choicesPanel.rect.width,47),new Vector2(0,-i*51),new Color(.88f,.94f,1));TopLeft(rect);
                var label=Label(rect,data.Find(exchange.sentIds[i])?.text??"",new Vector2(rect.rect.width-20,43),new Vector2(10,-2),18,new Color(.03f,.3f,.6f));label.alignment=TextAlignmentOptions.Center;
                var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.onClick.AddListener(()=>owner.ChooseReply(exchange.id,option));
            }
        }
        void UpdateListLayout()
        {
            float top=contactNumber.gameObject.activeSelf?104:70;
            list.anchoredPosition=new Vector2(12,-top);list.sizeDelta=new Vector2(screen.rect.width-24,screen.rect.height-top-65-choicesPanel.rect.height);
            scroll.viewport.sizeDelta=list.sizeDelta;
        }
        IEnumerator ScrollToBottom(){yield return null;Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(content);scroll.verticalNormalizedPosition=0;scrollRoutine=null;}
        public void BeginWindowDrag(PointerEventData e)
        {
            if(!IsOpen||owner.game.State!=GameState.Inspecting||e.button!=PointerEventData.InputButton.Left)return;
            var button=e.pointerPressRaycast.gameObject?e.pointerPressRaycast.gameObject.GetComponentInParent<Button>():null;
            if(button==CloseButton||button==MinimizeButton||button==ResetButton)return;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,e.position,e.pressEventCamera,out dragStart))return;
            dragging=true;windowStart=phone.anchoredPosition;scrollWasEnabled=scroll.enabled;scroll.StopMovement();scroll.enabled=false;
        }
        public void DragWindow(PointerEventData e)
        {
            if(!dragging||owner.game.State!=GameState.Inspecting)return;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,e.position,e.pressEventCamera,out var point))SetWindowPosition(windowStart+point-dragStart);
        }
        public void EndWindowDrag()
        {
            if(!dragging)return;dragging=false;scroll.enabled=scrollWasEnabled;SaveWindowPosition();
        }
        public void ResetWindowPosition(){SetWindowPosition(defaultPosition);SaveWindowPosition();}
        void SetWindowPosition(Vector2 position)
        {
            var area=((RectTransform)canvas.transform).rect.size;
            float xLimit=(area.x+phone.rect.width)*.5f-80;
            position.x=Mathf.Clamp(position.x,-xLimit,xLimit);
            position.y=Mathf.Clamp(position.y,-area.y*.5f-phone.rect.height*.5f+52,area.y*.5f-phone.rect.height*.5f+18);
            phone.anchoredPosition=new Vector2(Mathf.Round(position.x),Mathf.Round(position.y));
        }
        void RestoreWindowPosition()
        {
            // Tests redirect only these new preference keys; the player's game save is never read here.
            string key=PreferenceKey;
            var p=new Vector2(PlayerPrefs.GetFloat(key+"X",defaultPosition.x),PlayerPrefs.GetFloat(key+"Y",defaultPosition.y));
            if(!float.IsFinite(p.x)||!float.IsFinite(p.y))p=defaultPosition;
            Canvas.ForceUpdateCanvases();SetWindowPosition(p);
        }
        string PreferenceKey {
            get {
#if UNITY_EDITOR
                return PositionKey+(SaveSystem.TestSaveDirectory!=null?".Test":"");
#else
                return PositionKey;
#endif
            }
        }
        void SaveWindowPosition(){var p=phone.anchoredPosition;PlayerPrefs.SetFloat(PreferenceKey+"X",p.x);PlayerPrefs.SetFloat(PreferenceKey+"Y",p.y);PlayerPrefs.Save();}
        public void Tick(float delta)
        {
            bool show=IsOpen&&owner.game.State==GameState.Inspecting;phone.gameObject.SetActive(show);if(glow)glow.enabled=show;
            if(show)
            {
                if(typingDots){int dots=1+Mathf.FloorToInt(Time.time*2)%3;if(dots!=lastTypingDot){typingDots.text=new string('.',dots);lastTypingDot=dots;}}
                if(Time.frameCount>openedFrame&&owner.CanReadInput&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)owner.Close();
            }
            bool canShow=owner.CanShowBanner;
            if(canShow)
            {
                var waiting=owner.State.received.Where(m=>!m.announced).ToArray();
                if(waiting.Length>0)
                {
                    if(!bannerActive){bannerActive=true;bannerAge=0;bannerCount=0;}
                    else bannerAge=Mathf.Min(bannerAge,.4f);
                    bannerCount+=waiting.Length;
                    var last=data.Find(waiting[waiting.Length-1].id);
                    preview.text=bannerCount>1?string.Format(data.multipleMessages,bannerCount):last!=null&&!last.photo?last.text:data.singleMessage;
                    owner.BannerAnnounced();
                }
                if(bannerActive){bannerAge+=delta;if(bannerAge>=4.65f)bannerActive=false;}
            }
            banner.gameObject.SetActive(canShow&&bannerActive);
            if(bannerActive)
            {
                float t=Mathf.Clamp01(bannerAge/.4f);float ease=1+2.70158f*Mathf.Pow(t-1,3)+1.70158f*Mathf.Pow(t-1,2);
                float exit=Mathf.Clamp01((bannerAge-4.3f)/.35f);banner.anchoredPosition=owner.bannerOffset+Vector2.up*(owner.bannerSize.y+50)*(1-ease+exit*exit);
            }
            // FPS capture normally locks the mouse; Alt temporarily frees it for the banner button.
            bool alt=BannerVisible&&Keyboard.current!=null&&Keyboard.current.leftAltKey.isPressed;
            if(alt&&!freeCursor){freeCursor=true;viewEnabled=owner.game.player.view.enabled;owner.game.player.view.enabled=false;owner.game.input.EnableGameplay(false);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            else if(!alt&&freeCursor){freeCursor=false;owner.game.player.view.enabled=viewEnabled;owner.game.input.EnableGameplay(owner.game.State==GameState.Playing);if(owner.game.State==GameState.Playing){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}}
        }
        void OnDestroy(){if(owner)owner.Changed-=Refresh;if(glow)Destroy(glow.gameObject);if(freeCursor&&owner&&owner.game&&owner.game.player){owner.game.player.view.enabled=viewEnabled;owner.game.input.EnableGameplay(owner.game.State==GameState.Playing);}}
    }
}
