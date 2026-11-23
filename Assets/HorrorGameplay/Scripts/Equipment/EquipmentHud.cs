// Mostra o item, a carga e o tempo de espera quando necessário.

using System;
using TMPro;
using UnityEngine;

namespace HorrorGame
{
    [DefaultExecutionOrder(400),DisallowMultipleComponent]
    public sealed class EquipmentHud : MonoBehaviour
    {
        [Serializable] public sealed class Row
        {
            public RectTransform root;
            public UnityEngine.UI.Image icon,marker;
            public TMP_Text number,name,status;
            public UnityEngine.UI.Image[] battery;
            [NonSerialized] public bool selected;
        }
        public EquipmentController controller;
        public TMP_FontAsset titleFont,subtitleFont;
        public CanvasGroup group;
        public RectTransform rail;
        public Row[] rows=Array.Empty<Row>();
        [Min(0)] public int rightMargin=42,bottomMargin=72;
        [Range(0,1)] public float verticalPosition=0;
        [Min(.1f)] public float hideSeconds=2.5f;
        public Color ice=new Color(.86f,.92f,.94f),accent=new Color(.75f,.85f,.91f);
        float remaining;
        bool wasLow;
        Canvas canvas;
        PhoneMessages phone;
        public float Remaining=>remaining;
        public bool Suppressed=>!controller||!controller.HasFlashlight||!controller.game||!controller.game.CanControl||controller.game.IsBlackingOut
            ||controller.game.narrative&&controller.game.narrative.SequencePlaying
            ||controller.game.hud&&controller.game.hud.SubtitleVisible
            ||phone&&(phone.IsOpen||phone.BannerVisible);

        public void Build()
        {
            if(rows.Length>0)return;
            group=GetComponent<CanvasGroup>();if(!group)group=gameObject.AddComponent<CanvasGroup>();
            group.alpha=0;group.blocksRaycasts=group.interactable=false;
            rail=Rect("Linha",transform,new Vector2(1,180),new Vector2(-20,0));rail.pivot=new Vector2(.5f,0);
            rail.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.86f,.92f,.94f,.28f);
            rows=new Row[controller.items.Length];
            for(int i=0;i<rows.Length;i++)
            {
                var item=controller.items[i];var r=rows[i]=new Row();r.root=Rect("Slot "+item.slot,transform,new Vector2(270,88),Vector2.zero);
                r.root.pivot=new Vector2(1,0);
                r.icon=Image("Ícone",r.root,new Vector2(104,44),new Vector2(-112,38));r.icon.sprite=item.icon;r.icon.preserveAspect=true;
                r.marker=Image("Losango",r.root,new Vector2(7,7),new Vector2(-20,43));r.marker.rectTransform.localEulerAngles=new Vector3(0,0,45);
                r.number=Text("Número",r.root,item.slot.ToString(),new Vector2(24,30),new Vector2(0,43),18,false);
                r.name=Text("Nome",r.root,item.displayName,new Vector2(205,30),new Vector2(-132,14),22,true);r.name.characterSpacing=3;
                r.status=Text("Estado",r.root,"",new Vector2(205,22),new Vector2(-132,-9),15,false);
            }
        }
        static RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 position)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(1,0);r.sizeDelta=size;r.anchoredPosition=position;return r;
        }
        static UnityEngine.UI.Image Image(string name,Transform parent,Vector2 size,Vector2 position)
        {
            var r=Rect(name,parent,size,position);r.pivot=new Vector2(.5f,.5f);
            var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.raycastTarget=false;return image;
        }
        TMP_Text Text(string name,Transform parent,string value,Vector2 size,Vector2 position,int points,bool title)
        {
            var r=Rect(name,parent,size,position);r.pivot=new Vector2(.5f,.5f);
            var text=r.gameObject.AddComponent<TextMeshProUGUI>();text.font=title?titleFont:subtitleFont;
            text.text=value;text.fontSize=points;text.enableAutoSizing=false;text.extraPadding=false;
            text.alignment=TextAlignmentOptions.Right;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.NoWrap;return text;
        }
        public void RefreshPresentation()
        {
            if(!controller)return;
            // Bind after scene deserialization as well as construction; missing sprites must never draw white rectangles.
            for(int i=0;i<rows.Length&&i<controller.items.Length;i++)
            {
                var item=controller.items[i];var row=rows[i];if(!item||row==null)continue;
                if(row.icon){row.icon.sprite=item.icon;row.icon.enabled=item.icon;row.icon.preserveAspect=true;}
                if(row.number)row.number.SetText("{0}",item.slot);
                if(row.name)row.name.text=item.displayName;
                if (row.battery != null)
                    foreach (var bar in row.battery)
                        if (bar) bar.gameObject.SetActive(false);
            }
        }
        void Awake(){canvas=GetComponentInParent<Canvas>();phone=controller?controller.GetComponent<PhoneMessages>():null;RefreshPresentation();}
        void OnEnable(){RefreshPresentation();if(controller)controller.Changed+=Changed;ShowFor(3.5f);}
        void OnDisable(){if(controller)controller.Changed-=Changed;}
        void Changed()=>ShowFor(hideSeconds);
        public void ShowFor(float seconds){remaining=Mathf.Max(remaining,seconds);}
        void Update()=>Advance(Time.unscaledDeltaTime);
        public void Advance(float seconds)
        {
            if(!group||!controller||rows.Length==0)return;
            bool low=controller.flashlight&&controller.flashlight.Battery01<.2f;
            if(low&&!wasLow)ShowFor(hideSeconds);wasLow=low;
            float cooldown = controller.whistle ? controller.whistle.State.cooldownRemaining : 0;
            bool suppressed=Suppressed;int count=0;
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows[i];var item=controller.items[i];bool owns=controller.Owns(i);row.root.gameObject.SetActive(owns);if(!owns)continue;
                bool selected=i==controller.ActiveIndex;bool available=controller.Available(i);
                row.root.anchoredPosition=new Vector2(0,count++*96+24);
                row.icon.rectTransform.sizeDelta=selected?new Vector2(124,48):new Vector2(80,32);
                var goal=new Vector2(-112-(selected&&!PresentationSettings.ReduceEffects?10:0),43);
                row.icon.rectTransform.anchoredPosition=new Vector2(Mathf.Round(Mathf.MoveTowards(row.icon.rectTransform.anchoredPosition.x,goal.x,seconds*120)),goal.y);
                row.icon.color=available?new Color(ice.r,ice.g,ice.b,selected?.95f:.28f):new Color(ice.r,ice.g,ice.b,.12f);
                row.marker.color=selected?accent:new Color(ice.r,ice.g,ice.b,.36f);row.number.color=selected?ice:new Color(ice.r,ice.g,ice.b,.45f);
                row.name.gameObject.SetActive(selected||!available);row.name.color=available?ice:new Color(ice.r,ice.g,ice.b,.3f);
                row.status.color=new Color(.6f,.65f,.68f,.85f);
                string status=!available?"NÃO AQUI":controller.Switching?"AGUARDE":item.kind==EquipmentKind.Flashlight
                    ? $"{Mathf.CeilToInt(controller.flashlight.Battery01 * 100)}%"
                    :item.kind==EquipmentKind.Whistle?cooldown>0?$"{Mathf.CeilToInt(cooldown)} s":controller.whistle&&!controller.whistle.State.discovered?"DO LUAN":"PRONTO":item.inventoryItemId==LockpickSystem.ItemId&&controller.game.lockpick?controller.game.lockpick.CountLabel:"PRONTO";
                if(row.status.text!=status)row.status.text=status;
                row.status.gameObject.SetActive((selected||!available)&&!string.IsNullOrEmpty(status));
                row.selected=selected;
            }
            rail.sizeDelta=new Vector2(1,Mathf.Max(0,(count-1)*96+36));rail.anchoredPosition=new Vector2(-20,44);
            Position(count);
            bool showHud=!suppressed&&count>0&&EquipmentPreferences.Mode!=EquipmentHudMode.Hidden
                &&(EquipmentPreferences.Mode==EquipmentHudMode.Always||remaining>0||cooldown>0);
            group.alpha=suppressed||EquipmentPreferences.Mode==EquipmentHudMode.Hidden?0:Mathf.MoveTowards(group.alpha,showHud?1:0,seconds*(PresentationSettings.ReduceEffects?12:5));
            if(!suppressed)remaining=Mathf.Max(0,remaining-seconds);
        }
        void Position(int count)
        {
            var rect=(RectTransform)transform;var area=Screen.safeArea;
            float width=Mathf.Max(1,Screen.width),height=Mathf.Max(1,Screen.height);
            rect.anchorMin=rect.anchorMax=new Vector2(area.xMax/width,area.yMin/height);rect.pivot=new Vector2(1,0);
            float available=canvas?((RectTransform)canvas.transform).rect.height*area.height/height:1080;
            const float hudScale = 1.16f;
            rect.localScale = new Vector3(hudScale, hudScale, 1);
            rect.sizeDelta=new Vector2(270,Mathf.Max(120,count*96+24));
            rect.anchoredPosition=new Vector2(-rightMargin,Mathf.Round(Mathf.Clamp(bottomMargin+verticalPosition*available,24,Mathf.Max(24,available-rect.sizeDelta.y*hudScale-24))));
        }
    }
}
