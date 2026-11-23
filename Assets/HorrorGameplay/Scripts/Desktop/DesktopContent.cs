// Guarda o conteúdo, as imagens e os dados dos aplicativos.

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
namespace HorrorGame.Desktop
{
    public enum DesktopFileKind { Image, Report, LockedCase }
    [Serializable] public sealed class DesktopAppDefinition
    { public string id,title; public Sprite icon; public Vector2 defaultSize=new Vector2(790,490); }
    [Serializable] public sealed class DesktopFile
    { public string id,title,requiredFlag,text,emailId; public DesktopFileKind kind; public Texture2D texture; }
    [Serializable] public sealed class DesktopTrack
    { public string id,title; public AudioClip clip; public int unlockVisit=1; public float placeholderSeconds=20; }
    [CreateAssetMenu(menuName="Whispers/PC/Desktop content")]
    public sealed class DesktopContent : ScriptableObject
    {
        public DesktopAppDefinition[] apps=Array.Empty<DesktopAppDefinition>();
        public DesktopNote[] notes=Array.Empty<DesktopNote>(); public DesktopPage[] pages=Array.Empty<DesktopPage>();
        public DesktopError[] errors=Array.Empty<DesktopError>(); public DesktopFile[] files=Array.Empty<DesktopFile>();
        public DesktopTrack[] tracks=Array.Empty<DesktopTrack>();
        public Texture2D wallpaper,schoolWallpaper;
        public Sprite arrowCursor,handCursor,waitCursor,textCursor,windowSprite,buttonSprite,titleSprite,taskbarSprite,frameSprite,startSprite,closeSprite,inactiveTitleSprite;
        public Vector2 arrowHotspot,handHotspot=new Vector2(8,2);
        public TMP_FontAsset normalFont,childFont,retroFont;
        public SoundSet bootSound,clickSound,diskSound,errorSound,notificationSound,crtSound;
        [Range(0,1)]public float crtStrength=.35f;
        public DesktopAppDefinition FindApp(string id)=>apps.FirstOrDefault(a=>a.id==id);
        public DesktopError FindError(string id)=>errors.FirstOrDefault(e=>e&&e.id==id);
    }
    [Serializable] public sealed class DesktopState
    {
        public int version=1,visits,minesGames;
        public List<string> unlockedFiles=new List<string>(),eventIds=new List<string>();
        public bool Has(string id)=>unlockedFiles.Contains(id)||eventIds.Contains(id);
        public bool Unlock(string id){if(string.IsNullOrWhiteSpace(id)||Has(id))return false;unlockedFiles.Add(id);return true;}
        public bool Valid=>version==1&&visits>=0&&visits<=100000&&minesGames>=0&&minesGames<=1000000
            &&unlockedFiles!=null&&eventIds!=null&&unlockedFiles.Count<1024&&eventIds.Count<1024
            &&unlockedFiles.All(s=>!string.IsNullOrWhiteSpace(s))&&eventIds.All(s=>!string.IsNullOrWhiteSpace(s))
            &&unlockedFiles.Distinct().Count()==unlockedFiles.Count&&eventIds.Distinct().Count()==eventIds.Count;
        public static DesktopState Parse(string json)
        {var state=new DesktopState{version=0,unlockedFiles=null,eventIds=null};if(string.IsNullOrWhiteSpace(json))throw new ArgumentException("Estado do computador ausente.");
            // JsonUtility turns an explicit null collection into an empty collection.
            if(System.Text.RegularExpressions.Regex.IsMatch(json,"\"(?:unlockedFiles|eventIds)\"\\s*:\\s*null"))throw new ArgumentException("Listas do computador ausentes.");
            JsonUtility.FromJsonOverwrite(json,state);if(!state.Valid)throw new ArgumentException("Estado do computador inválido.");return state;}
    }
    public sealed class DesktopContext
    {
        public RetroComputer Host; public DesktopContent Data=>Host.content; public DesktopState State=>Host.State;
        public EmailInboxController Inbox=>Host.inbox;
        public void OpenApp(string id,string arg=null)=>Host.OpenApp(id,arg);
        public void Notify(string text)=>Host.Notify(text);
        public void ShowError(string id)=>Host.ShowError(id);
        public void Unlock(string flag){if(State.Unlock(flag))Changed();}
        public bool HasFlag(string flag)=>string.IsNullOrWhiteSpace(flag)||State.Has(flag)||Inbox.State!=null&&Inbox.State.IsRead(flag.Replace(".read",""));
        public void Changed()=>Host.SaveChanged();
        public void PlaySound(string kind)=>Host.PlaySound(kind);
    }
    public abstract class DesktopApp
    {
        public DesktopContext Context; public DesktopWindow Window; public RectTransform Body=>Window.body;
        public virtual void Build(){} public virtual void OnOpen(string argument){} public virtual void OnClose(){} public virtual void OnResize(){} public virtual void Tick(float deltaTime){}
    }
}
