// Coordena a câmera, o cursor, o monitor e o fluxo do computador.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace HorrorGame.Desktop
{
    [DefaultExecutionOrder(-50)]
    public sealed class RetroComputer:PersistentObject
    {
        public const string StateId="bd.office.pc";
        public bool endingAccess;
        public GameSession game;public BeforeDemolitionOfficeFlow office;public EmailInboxController inbox;public DesktopContent content;
        public Transform seatCameraPose,screenSurface;public Renderer screenRenderer;public Material crtMaterial;
        [Range(1,1.5f)]public float transitionSeconds=1.2f;
        [Range(15,80)]public float seatedFov=49;
        [Range(.1f,3)]public float cursorSensitivity=1;
        public Key pauseKey=Key.F10;
        public DesktopState State{get;private set;}=new DesktopState();
        public bool IsSeated{get;private set;}public bool Ready{get;private set;}
        public bool IsBooting=>IsSeated&&!Ready&&!closing;public DesktopBootStage BootStage=>bootView!=null?bootView.Stage:DesktopBootStage.Hidden;public float BootElapsed{get;private set;}
        public RenderTexture ScreenTexture{get;private set;}public Camera ScreenCamera{get;private set;}
        public RectTransform DesktopRoot{get;private set;}public DesktopPointer Pointer{get;private set;}
        public IReadOnlyDictionary<string,DesktopWindow> Windows=>windows;
        public event Action<string> DesktopEvent;
        readonly Dictionary<string,DesktopWindow> windows=new Dictionary<string,DesktopWindow>();
        DesktopStartMenu shellMenu;bool restartPending;
        DesktopContext context;Canvas screenCanvas,backendCanvas;GraphicRaycaster raycaster,backendRaycaster;
        RectTransform iconLayer,windowLayer,taskbar,startMenu,contextMenu,cursorRect,notificationPanel;RawImage wallpaper;TMP_Text tray,notification;
        UnityEngine.UI.Image cursorImage,trayMailIcon;DesktopEntry entry;Keyboard keyboard;
        Coroutine transition,forcedCursor,boot;DesktopBootView bootView;Vector3 cameraPosition;Quaternion cameraRotation;float cameraFov,desktopAge;
        bool cameraEnabled,backendCanvasEnabled,backendRaycasterEnabled,closing,exitControlLock,skipBoot,mailAnnounced;
        readonly Dictionary<GameObject,bool> hiddenHud=new Dictionary<GameObject,bool>();
        int inputAfterFrame;FirstPersonCamera view;UnityEngine.InputSystem.UI.InputSystemUIInputModule inputModule;
        bool inputModuleEnabled;AudioSource hum;AudioClip humClip;float noticeSeconds,keySoundAfter;string imageTitle;Texture2D imageTexture;
        public Texture2D RequestedImage=>imageTexture;public string RequestedImageTitle=>imageTitle;
        void Awake(){persistentId=StateId;if(!game)game=GetComponent<GameSession>();if(!office)office=GetComponent<BeforeDemolitionOfficeFlow>();if(!inbox&&office)inbox=office.inbox;if(crtMaterial)crtMaterial=new Material(crtMaterial);context=new DesktopContext{Host=this};}
        void OnEnable(){if(game)game.StateChanged+=SessionChanged;if(inbox)inbox.ReadFlagRaised+=EmailRead;}
        public bool Open()
        {
            if(IsSeated||closing||!content||!seatCameraPose||!game||(!endingAccess&&(!office||!office.IsActiveOffice))||!inbox||!game.CanControl)return false;
            if(!inbox.Open(EmailDevice.Desktop))return false;
            inbox.ExternalKeyboardInput=true;
            EnsureDesktop();view=game.player.view;cameraPosition=view.playerCamera.transform.localPosition;cameraRotation=view.playerCamera.transform.localRotation;cameraFov=view.playerCamera.fieldOfView;cameraEnabled=view.enabled;view.enabled=false;
            HideHud();
            backendCanvas=inbox.View.GetComponent<Canvas>();backendRaycaster=inbox.View.GetComponent<GraphicRaycaster>();
            if(backendCanvas){backendCanvasEnabled=backendCanvas.enabled;backendCanvas.enabled=false;}if(backendRaycaster){backendRaycasterEnabled=backendRaycaster.enabled;backendRaycaster.enabled=false;}
            inputModule=EventSystem.current?EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>():null;
            if(inputModule){inputModuleEnabled=inputModule.enabled;inputModule.enabled=false;}
            foreach(var id in windows.Keys.ToArray())CloseApp(id);
            State.visits=Mathf.Min(100000,State.visits+1);IsSeated=true;Ready=false;closing=false;skipBoot=mailAnnounced=false;BootElapsed=desktopAge=0;noticeSeconds=0;
            screenCanvas.gameObject.SetActive(true);ScreenCamera.enabled=true;SetDesktopVisible(false);bootView.SetStage(DesktopBootStage.Black);SetPower(false);LockCursor();PlaySound("click");
            hum=AudioManager.Instance?.Play(content.crtSound,screenSurface?screenSurface.position:transform.position,.05f,AudioChannel.Ambience,true);humClip=hum?hum.clip:null;if(hum)hum.loop=true;
            ApplyWallpaper();SaveChanged();transition=StartCoroutine(Blend(true));return true;
        }
        IEnumerator Blend(bool entering)
        {
            var cam=view.playerCamera;var fromP=cam.transform.position;var fromR=cam.transform.rotation;float fromFov=cam.fieldOfView,age=0;
            while(age<transitionSeconds)
            {
                if(!game||!view)yield break;
                if(game.State!=GameState.Paused)age+=Time.unscaledDeltaTime;
                float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/transitionSeconds));
                Vector3 toP=entering?seatCameraPose.position:cam.transform.parent.TransformPoint(cameraPosition);
                Quaternion toR=entering?seatCameraPose.rotation:cam.transform.parent.rotation*cameraRotation;
                cam.transform.SetPositionAndRotation(Vector3.Lerp(fromP,toP,t),Quaternion.Slerp(fromR,toR,t));cam.fieldOfView=Mathf.Lerp(fromFov,entering?seatedFov:cameraFov,t);yield return null;
            }
            transition=null;
            if(entering){boot=StartCoroutine(BootComputer());}
            else FinishClose();
        }
        void OnVisit()
        {
            if(State.visits>=2&&Once("desktop.cursor")){forcedCursor=StartCoroutine(CursorEvent());}
        }
        IEnumerator BootComputer()
        {
            bool full=State.visits==1;float duration=full?7:2.2f;BootElapsed=0;
            while(BootElapsed<duration&&!skipBoot)
            {
                if(game.State!=GameState.Paused)
                {
                    BootElapsed+=Time.unscaledDeltaTime;var stage=full?(BootElapsed<.35f?DesktopBootStage.Black:BootElapsed<2.65f?DesktopBootStage.Bios:BootElapsed<4.75f?DesktopBootStage.Splash:DesktopBootStage.Welcome):(BootElapsed<.2f?DesktopBootStage.Black:BootElapsed<.8f?DesktopBootStage.Bios:BootElapsed<1.45f?DesktopBootStage.Splash:DesktopBootStage.Welcome);
                    bootView.UpdateIdentity(State.visits,BootElapsed/duration);if(stage!=bootView.Stage){bootView.SetStage(stage);SetPower(stage!=DesktopBootStage.Black);if(stage==DesktopBootStage.Bios)PlaySound("disk");if(stage==DesktopBootStage.Splash)PlaySound("boot");}ApplyEffects();
                }
                yield return null;
            }
            boot=null;bootView.Hide();SetPower(true);Ready=true;SetDesktopVisible(true);inputAfterFrame=Time.frameCount;OnVisit();
        }
        public bool TrySkipBoot(){if(!IsBooting||boot==null||BootElapsed<1||game.State==GameState.Paused)return false;skipBoot=true;return true;}
        void SetDesktopVisible(bool visible){wallpaper.gameObject.SetActive(visible);iconLayer.gameObject.SetActive(visible);windowLayer.gameObject.SetActive(visible);taskbar.gameObject.SetActive(visible);cursorRect.gameObject.SetActive(visible);startMenu.gameObject.SetActive(false);notificationPanel.gameObject.SetActive(false);HideContextMenu();if(visible)RefreshTray();}
        void HideHud(){var h=game.hud;if(!h)return;var groups=new GameObject[]{h.objectiveGroup?h.objectiveGroup.gameObject:null,h.interactionGroup?h.interactionGroup.gameObject:null,h.batteryGroup?h.batteryGroup.gameObject:null,h.staminaGroup?h.staminaGroup.gameObject:null,h.crosshair?h.crosshair.gameObject:null,h.notification?h.notification.gameObject:null};foreach(var go in groups)if(go&&!hiddenHud.ContainsKey(go)){hiddenHud[go]=go.activeSelf;go.SetActive(false);}}
        IEnumerator CursorEvent()
        {Vector2 start=Pointer.Position,end=start+new Vector2(65,-17);float age=0;while(age<1&&IsSeated&&!closing){if(game.State!=GameState.Paused){age+=Time.unscaledDeltaTime;Pointer.SetPosition(Vector2.Lerp(start,end,Mathf.SmoothStep(0,1,age)));}yield return null;}forcedCursor=null;}
        bool Once(string id){if(State.eventIds.Contains(id))return false;State.eventIds.Add(id);DesktopEvent?.Invoke(id);SaveChanged();return true;}
        void EmailRead(string id)
        {
            if(id=="email.06.read"||id=="email.04.read")GameClock.Instance?.TriggerOfficeAnomaly(id=="email.04.read");
            if(id=="email.06.read"&&State.Unlock("desktop.wallpaper")){ApplyWallpaper();DesktopEvent?.Invoke("desktop.wallpaper");SaveChanged();}
            if(IsSeated)RefreshTray();
        }
        int UnreadCount=>inbox&&inbox.catalog&&inbox.State!=null?inbox.catalog.messages.Count(m=>inbox.State.IsDelivered(m.id)&&!inbox.State.IsRead(m.id)&&(inbox.enforceChronologicalReading||m.id!="email.02"||State.Has("trash.email.02"))):0;
        void Update()
        {
            if(!IsSeated||!isActiveAndEnabled)return;
            if(game.State==GameState.Paused)return;
            if(Time.frameCount<=inputAfterFrame)return;
            if(!closing&&!inbox.IsOpen){Close();return;}
            BindKeyboard();
            if(closing)return;
            if(keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame){Close();return;}
            if(keyboard!=null&&keyboard[pauseKey].wasPressedThisFrame){Pause();return;}
            if(!Ready){if((keyboard!=null&&keyboard.anyKey.wasPressedThisFrame)||(Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame))TrySkipBoot();return;}
            if(keyboard!=null&&entry&&entry.Field)
            {
                if(keyboard.backspaceKey.wasPressedThisFrame){var s=entry.Field.text;if(s.Length>0)entry.Field.SetTextWithoutNotify(s.Substring(0,s.Length-1));}
                if((keyboard.leftCtrlKey.isPressed||keyboard.rightCtrlKey.isPressed)&&keyboard.aKey.wasPressedThisFrame)entry.Field.SetTextWithoutNotify("");
                if(keyboard.enterKey.wasPressedThisFrame)entry.Submitted?.Invoke(entry.Field.text);
            }
            if(forcedCursor==null)Pointer.ReadMouse(cursorSensitivity);
            else Pointer.RefreshHover();
            foreach(var w in windows.Values.ToArray())if(w&&w.gameObject.activeInHierarchy)w.App?.Tick(Time.deltaTime);
            desktopAge+=Time.unscaledDeltaTime;if(!mailAnnounced&&desktopAge>=3){mailAnnounced=true;if(UnreadCount>0)Notify("Você tem "+UnreadCount+" mensagens não lidas.");}
            if(noticeSeconds>0){noticeSeconds-=Time.unscaledDeltaTime;if(noticeSeconds<=0&&notificationPanel)notificationPanel.gameObject.SetActive(false);}
            RefreshTray();ApplyEffects();
        }
        void BindKeyboard(){if(keyboard==Keyboard.current)return;if(keyboard!=null)keyboard.onTextInput-=Typed;keyboard=Keyboard.current;if(keyboard!=null)keyboard.onTextInput+=Typed;}
        void Typed(char ch)
        {
            if(!IsSeated||!Ready||closing||game.State!=GameState.Inspecting||!entry||!entry.Field||char.IsControl(ch))return;
            entry.Field.SetTextWithoutNotify((entry.Field.text+ch).Substring(0,Mathf.Min(240,entry.Field.text.Length+1)));
            if(Time.unscaledTime>=keySoundAfter){AudioManager.Instance?.PlayCue("pc_click",transform.position,.06f);keySoundAfter=Time.unscaledTime+.07f;}
        }
        public void FocusEntry(DesktopEntry value){entry=value;}
        public void ClearEntry()=>entry=null;
        public void Pause(){if(IsSeated&&!closing)game.Pause();}
        void SessionChanged(GameState state)
        {
            if(!IsSeated)return;
            if(state==GameState.Paused)
            {if(ScreenCamera)ScreenCamera.enabled=false;if(raycaster)raycaster.enabled=false;if(inputModule)inputModule.enabled=inputModuleEnabled;return;}
            if(state==GameState.Inspecting)
            {
                inputAfterFrame=Time.frameCount;LockCursor();if(inputModule)inputModule.enabled=false;if(ScreenCamera)ScreenCamera.enabled=true;
                if(raycaster)raycaster.enabled=true;return;
            }
            // An externally closed inbox is handled next Update; state callbacks also run during scene unload.
        }
        void LockCursor(){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        public void Close()
        {
            if(!IsSeated||closing)return;if(!isActiveAndEnabled){FinishClose();return;}Ready=false;closing=true;entry=null;
            if(transition!=null){StopCoroutine(transition);transition=null;}if(forcedCursor!=null){StopCoroutine(forcedCursor);forcedCursor=null;}
            if(boot!=null){StopCoroutine(boot);boot=null;}if(bootView!=null)bootView.Hide();SetPower(false);
            if(game.State==GameState.Playing){game.DisableControl();exitControlLock=true;}
            transition=StartCoroutine(Blend(false));
        }
        void FinishClose()
        {
            foreach(var pair in hiddenHud)if(pair.Key)pair.Key.SetActive(pair.Value);hiddenHud.Clear();
            foreach(var id in windows.Keys.ToArray())CloseApp(id);
            foreach(var w in windows.Values)if(w&&w.AppId=="media")w.App?.OnClose();
            if(view){var cam=view.playerCamera;cam.transform.localPosition=cameraPosition;cam.transform.localRotation=cameraRotation;cam.fieldOfView=cameraFov;view.enabled=cameraEnabled;}
            if(screenCanvas)screenCanvas.gameObject.SetActive(false);if(ScreenCamera)ScreenCamera.enabled=false;SetPower(false);
            if(hum&&hum.clip==humClip){hum.loop=false;hum.Stop();}hum=null;humClip=null;
            IsSeated=Ready=closing=false;entry=null;
            if(inbox&&inbox.View){inbox.ExternalKeyboardInput=false;if(backendCanvas)backendCanvas.enabled=backendCanvasEnabled;if(backendRaycaster)backendRaycaster.enabled=backendRaycasterEnabled;inbox.Close();}
            if(inputModule)inputModule.enabled=inputModuleEnabled;
            game?.EndInspection();if(restartPending){restartPending=false;StartCoroutine(RestartSession());}if(exitControlLock&&game&&game.State==GameState.Cutscene)game.EnableControl();exitControlLock=false;SaveChanged();
        }
        void EnsureDesktop()
        {
            if(screenCanvas)return;DesktopUI.Theme=content;
            int layer=LayerMask.NameToLayer("PCScreen");if(layer<0)throw new InvalidOperationException("PCScreen layer must be authored.");
            var cameraGo=new GameObject("PCScreen — câmera dedicada");cameraGo.transform.SetParent(transform,false);cameraGo.transform.position=new Vector3(10000,10000,10000);cameraGo.layer=layer;
            ScreenCamera=cameraGo.AddComponent<Camera>();ScreenCamera.orthographic=true;ScreenCamera.orthographicSize=384;ScreenCamera.nearClipPlane=.1f;ScreenCamera.farClipPlane=10;ScreenCamera.cullingMask=1<<layer;ScreenCamera.clearFlags=CameraClearFlags.SolidColor;ScreenCamera.backgroundColor=Color.black;ScreenCamera.allowHDR=ScreenCamera.allowMSAA=false;
            ScreenTexture=new RenderTexture(1024,768,16,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default){name="CRT desktop 1024x768",antiAliasing=1,filterMode=FilterMode.Point,useMipMap=false,autoGenerateMips=false,wrapMode=TextureWrapMode.Clamp,anisoLevel=0,useDynamicScale=false};ScreenTexture.Create();ScreenCamera.targetTexture=ScreenTexture;ScreenCamera.enabled=false;ScreenCamera.allowDynamicResolution=false;
            var pipeline=ScreenCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()??cameraGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();pipeline.renderPostProcessing=false;pipeline.renderShadows=false;pipeline.antialiasing=UnityEngine.Rendering.Universal.AntialiasingMode.None;
            var canvasGo=new GameObject("Desktop — canvas 4:3",typeof(RectTransform));canvasGo.layer=layer;canvasGo.transform.SetParent(cameraGo.transform,false);
            screenCanvas=canvasGo.AddComponent<Canvas>();screenCanvas.renderMode=RenderMode.ScreenSpaceCamera;screenCanvas.worldCamera=ScreenCamera;screenCanvas.planeDistance=1;screenCanvas.pixelPerfect=true;
            var scaler=canvasGo.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;scaler.scaleFactor=1;scaler.referenceResolution=new Vector2(1024,768);
            raycaster=canvasGo.AddComponent<GraphicRaycaster>();DesktopRoot=(RectTransform)canvasGo.transform;
            wallpaper=DesktopUI.Rect(DesktopRoot,"Papel de parede",0,0,1024,768).gameObject.AddComponent<RawImage>();wallpaper.raycastTarget=true;wallpaper.color=new Color(.33f,.48f,.60f,1);
            iconLayer=DesktopUI.Rect(DesktopRoot,"Ícones",0,0,1024,726);
            for(int i=0;i<content.apps.Length;i++){var def=content.apps[i];var icon=DesktopUI.Rect(iconLayer,def.title,14+(i/9)*98,10+(i%9)*78,84,76);icon.gameObject.AddComponent<UnityEngine.UI.Image>().color=Color.clear;var click=icon.gameObject.AddComponent<DesktopIcon>();click.Host=this;click.AppId=def.id;DesktopUI.Image(icon,def.icon,20,0,44,44);var label=DesktopUI.Label(icon,def.title,0,47,84,29,12);label.alignment=TextAlignmentOptions.Top;label.color=Color.white;}
            windowLayer=DesktopUI.Rect(DesktopRoot,"Janelas",0,0,1024,726);
            taskbar=DesktopUI.Panel(DesktopRoot,"Barra de tarefas",0,726,1024,42,content.taskbarSprite);
            var start=DesktopUI.Button(taskbar,"Iniciar",0,0,99,42,ToggleStartMenu);if(content.startSprite)start.GetComponent<UnityEngine.UI.Image>().sprite=content.startSprite;var startText=start.GetComponentInChildren<TMP_Text>();startText.fontSize=17;startText.fontStyle=FontStyles.Bold;startText.color=Color.white;
            trayMailIcon=DesktopUI.Image(taskbar,content.FindApp("mail")?.icon,873,10,22,22);
            tray=DesktopUI.Label(taskbar,GameClock.Instance?GameClock.Instance.TimeText:"01:12",899,10,121,26,16);tray.color=Color.white;
            shellMenu=new DesktopStartMenu(this,DesktopRoot);startMenu=shellMenu.Root;
            notificationPanel=DesktopUI.Panel(DesktopRoot,"Aviso do correio",752,630,264,85,content.windowSprite);var noticeGroup=notificationPanel.gameObject.AddComponent<CanvasGroup>();noticeGroup.blocksRaycasts=noticeGroup.interactable=false;DesktopUI.Image(notificationPanel,content.FindApp("mail")?.icon,10,10,22,22);notification=DesktopUI.Label(notificationPanel,"",42,10,210,65,13);notificationPanel.gameObject.SetActive(false);
            cursorRect=DesktopUI.Rect(DesktopRoot,"Cursor virtual",512,384,32,32);cursorImage=cursorRect.gameObject.AddComponent<UnityEngine.UI.Image>();cursorImage.raycastTarget=false;cursorImage.sprite=content.arrowCursor;
            Pointer=canvasGo.AddComponent<DesktopPointer>();Pointer.Initialize(this,raycaster,ScreenCamera);Pointer.SetPosition(new Vector2(512,384));
            bootView=new DesktopBootView(DesktopRoot,content);
            if(crtMaterial)crtMaterial.SetTexture("_MainTex",ScreenTexture);
            if(screenRenderer&&crtMaterial)screenRenderer.sharedMaterial=crtMaterial;
            ApplyWallpaper();screenCanvas.gameObject.SetActive(false);
        }
        public DesktopWindow OpenApp(string id,string argument=null)
        {
            if(!IsSeated||!Ready||game.State==GameState.Paused)return null;
            entry=null;
            if(windows.TryGetValue(id,out var existing)&&existing){existing.Restore();existing.App?.OnOpen(argument);LayerUI(existing);PlaySound("click");return existing;}
            var def=content.FindApp(id);if(def==null){ShowError("disk");return windows.TryGetValue("error.disk",out var unknown)?unknown:null;}
            DesktopApp app=id=="mail"?new DesktopMailApp():id=="documents"?new DesktopDocumentsApp():id=="images"?new DesktopImagesApp():id=="notepad"?new DesktopNotepadApp():id=="browser"?new DesktopBrowserApp():id=="media"?new DesktopMediaApp():id=="trash"?new DesktopTrashApp():id=="mines"?new DesktopMinesApp():id=="snake"?new DesktopSnakeApp():null;
            if(app==null){ShowError(id);return windows.TryGetValue("error."+id,out var unavailable)?unavailable:null;}
            shellMenu.Record(id);var window=CreateWindow(def,id);window.App=app;app.Context=context;app.Window=window;Canvas.ForceUpdateCanvases();app.Build();app.OnOpen(argument);LayerUI(window);ReflowTasks();PlaySound("disk");return window;
        }
        DesktopWindow CreateWindow(DesktopAppDefinition def,string id)
        {var r=DesktopUI.Panel(windowLayer,def.title,Mathf.Clamp(132+windows.Count*18,4,1020-def.defaultSize.x),Mathf.Clamp(24+windows.Count*18,4,720-def.defaultSize.y),def.defaultSize.x,def.defaultSize.y,content.frameSprite?content.frameSprite:content.windowSprite);var w=r.gameObject.AddComponent<DesktopWindow>();w.Initialize(this,def);windows[id]=w;w.TaskButton=DesktopUI.Button(taskbar,def.title,108+Mathf.Min(4,windows.Count-1)*138,6,133,30,()=>{if(w.Minimized)w.Restore();else if(w.IsFocused)w.Minimize();else w.Focus();});FocusWindow(w);return w;}
        public void FocusWindow(DesktopWindow window){if(window&&window.Minimized)window=null;if(!window)window=windows.Values.Where(w=>w&&!w.Minimized&&w.gameObject.activeSelf).OrderByDescending(w=>w.transform.GetSiblingIndex()).FirstOrDefault();if(window)window.transform.SetAsLastSibling();foreach(var w in windows.Values)if(w)w.SetFocused(w==window);RaiseCursor();}
        void ReflowTasks(){int i=0;float width=Mathf.Floor(Mathf.Clamp(710f/Mathf.Max(1,windows.Count),40,138));foreach(var w in windows.Values){if(!w||!w.TaskButton)continue;var r=(RectTransform)w.TaskButton.transform;r.anchoredPosition=new Vector2(106+i*width,-6);r.sizeDelta=new Vector2(width-3,30);var label=w.TaskButton.GetComponentInChildren<TMP_Text>();if(label){label.fontSize=13;label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Ellipsis;}i++;}}
        void LayerUI(DesktopWindow w){int layer=LayerMask.NameToLayer("PCScreen");foreach(var t in w.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;}
        public void CloseApp(string id){if(!windows.TryGetValue(id,out var w)||!w)return;w.App?.OnClose();if(w.TaskButton)Destroy(w.TaskButton.gameObject);w.gameObject.SetActive(false);Destroy(w.gameObject);windows.Remove(id);ReflowTasks();FocusWindow(null);entry=null;}
        IEnumerator RestartSession(){yield return null;yield return null;Open();}
        public void RequestShutdownDialog()
        {
            startMenu.gameObject.SetActive(false);if(windows.ContainsKey("shutdown")){windows["shutdown"].Focus();return;}
            var w=CreateWindow(new DesktopAppDefinition{id="shutdown",title=DesktopSystemText.SystemName,defaultSize=new Vector2(500,220)},"shutdown");
            DesktopUI.Label(w.body,DesktopSystemText.ShutdownQuestion,20,20,450,35,18);
            DesktopUI.Button(w.body,DesktopSystemText.Shutdown,15,90,145,35,Close);
            DesktopUI.Button(w.body,DesktopSystemText.Restart,170,90,145,35,()=>{restartPending=true;Close();});
            DesktopUI.Button(w.body,DesktopSystemText.Cancel,325,90,145,35,()=>CloseApp("shutdown"));ReflowTasks();
        }
        public void OpenSystemTool(string id)
        {
            startMenu.gameObject.SetActive(false);
            if(id=="search"){OpenApp("browser",DesktopSystemText.SearchHome);return;}
            if(id=="pictures"){OpenApp("images");return;}
            if(id=="music"){OpenApp("media");return;}
            if(id=="run"){
                if(windows.ContainsKey(id)){windows[id].Focus();return;}
                var w=CreateWindow(new DesktopAppDefinition{id=id,title=DesktopSystemText.Run,defaultSize=new Vector2(500,220)},id);
                DesktopUI.Label(w.body,DesktopSystemText.ProgramName,15,15,450,30,16);
                var input=DesktopUI.Entry(w.body,"",15,50,450,32,value=>RunProgram(value));
                DesktopUI.Button(w.body,DesktopSystemText.Open,320,105,145,32,()=>RunProgram(input.text));ReflowTasks();return;
            }
            ShowError(id=="control"?"computer":id);
        }
        void RunProgram(string value){var app=content.apps.FirstOrDefault(a=>a.id.Equals(value.Trim(),StringComparison.OrdinalIgnoreCase)||a.title.Equals(value.Trim(),StringComparison.OrdinalIgnoreCase));if(app!=null)OpenApp(app.id);else ShowError("disk");}
        public void ShowImage(Texture2D image,string title){imageTexture=image;imageTitle=title;OpenApp("images",title);}
        public void ShowError(string id)
        {
            if(!IsSeated||!Ready)return;var data=content.FindError(id);if(!data)return;string key="error."+id;
            if(windows.ContainsKey(key)){windows[key].Restore();return;}
            var def=new DesktopAppDefinition{id=key,title=data.title,defaultSize=new Vector2(495,250)};var w=CreateWindow(def,key);
            if(data.icon)DesktopUI.Image(w.body,data.icon,18,22,36,36);
            DesktopUI.Label(w.body,data.message+(id=="properties"&&GameClock.Instance?"\n"+GameClock.Instance.DateText+" "+GameClock.Instance.TimeText:""),68,20,397,115,18);
            var buttons=data.buttons!=null&&data.buttons.Length>0?data.buttons:new[]{"OK"};for(int i=0;i<buttons.Length;i++)DesktopUI.Button(w.body,buttons[i],345-i*125,151,115,32,()=>CloseApp(key));ReflowTasks();PlaySound("error");
        }
        public void Notify(string text){if(notification&&Ready){notification.text=text;notificationPanel.gameObject.SetActive(true);noticeSeconds=3;RaiseCursor();PlaySound("notification");}}
        void RefreshTray(){int count=mailAnnounced?UnreadCount:0;if(trayMailIcon)trayMailIcon.gameObject.SetActive(count>0);if(tray)tray.text=(count>0?count+"  |  ":"")+(endingAccess?"08:12":GameClock.Instance?GameClock.Instance.TimeText:"01:12");}
        void ApplyWallpaper(){if(wallpaper){bool school=State.Has("desktop.wallpaper")||inbox&&inbox.State!=null&&inbox.State.IsRead("email.06");wallpaper.texture=school&&content.schoolWallpaper?content.schoolWallpaper:content.wallpaper;wallpaper.color=Color.white;}}
        public void SelectIcon(DesktopIcon selected){foreach(var icon in DesktopRoot.GetComponentsInChildren<DesktopIcon>()){var image=icon.GetComponent<UnityEngine.UI.Image>();image.color=icon==selected?new Color32(0x31,0x6A,0xC5,255):Color.clear;}}
        void ApplyEffects(){if(crtMaterial)crtMaterial.SetFloat("_Intensity",content.crtStrength*(PresentationSettings.ReduceEffects ? .12f : 1));
            if(ScreenTexture&&view&&screenSurface){var cam=view.playerCamera;float height=Mathf.Abs(cam.WorldToScreenPoint(screenSurface.TransformPoint(new Vector3(0,.5f,0))).y-cam.WorldToScreenPoint(screenSurface.TransformPoint(new Vector3(0,-.5f,0))).y);ScreenTexture.filterMode=height+.5f<ScreenTexture.height?FilterMode.Bilinear:FilterMode.Point;}}
        void SetPower(bool on){if(crtMaterial)crtMaterial.SetFloat("_Powered",on?1:0);}
        void ToggleStartMenu(){startMenu.gameObject.SetActive(!startMenu.gameObject.activeSelf);RaiseCursor();}
        public void RaiseCursor(){if(IsBooting&&bootView!=null){bootView.Root.SetAsLastSibling();return;}if(taskbar)taskbar.SetAsLastSibling();if(notificationPanel&&notificationPanel.gameObject.activeSelf)notificationPanel.SetAsLastSibling();if(startMenu&&startMenu.gameObject.activeSelf)startMenu.SetAsLastSibling();if(contextMenu&&contextMenu.gameObject.activeSelf)contextMenu.SetAsLastSibling();if(cursorRect)cursorRect.SetAsLastSibling();}
        public void SetCursorVisual(Vector2 point,GameObject over,bool busy=false)
        {
            if(!cursorRect)return;bool text=over&&over.GetComponentInParent<DesktopEntry>();bool hand=over&&(over.GetComponentInParent<UnityEngine.UI.Button>()||over.GetComponentInParent<DesktopIcon>());var sprite=busy?content.waitCursor:text?content.textCursor:hand?content.handCursor:content.arrowCursor;cursorImage.sprite=sprite?sprite:content.arrowCursor;var spot=hand?content.handHotspot:content.arrowHotspot;cursorRect.anchoredPosition=new Vector2(point.x-spot.x,-(768-point.y)+spot.y);RaiseCursor();
        }
        public void OpenContextMenu(GameObject target,Vector2 point)
        {
            if(contextMenu)Destroy(contextMenu.gameObject);float x=Mathf.Clamp(point.x,0,804),y=Mathf.Clamp(768-point.y,0,580);contextMenu=DesktopUI.Panel(DesktopRoot,"Menu de contexto",x,y,216,128,content.windowSprite);
            var icon=target?target.GetComponentInParent<DesktopIcon>():null;
            DesktopUI.Button(contextMenu,icon?"Abrir":"Atualizar",5,5,206,34,()=>{if(icon)OpenApp(icon.AppId);else ApplyWallpaper();HideContextMenu();});
            DesktopUI.Button(contextMenu,"Propriedades",5,46,206,34,()=>{ShowError("properties");HideContextMenu();});
            DesktopUI.Button(contextMenu,"Novo",5,87,206,34,()=>{ShowError("new");HideContextMenu();});RaiseCursor();
        }
        public void HideContextMenu(){if(contextMenu){Destroy(contextMenu.gameObject);contextMenu=null;}}
        public void PlaySound(string kind)
        {if(!content)return;var sound=kind=="boot"?content.bootSound:(kind=="disk"||kind=="hd")?content.diskSound:kind=="error"?content.errorSound:kind=="notification"?content.notificationSound:content.clickSound;AudioManager.Instance?.Play(sound,screenSurface?screenSurface.position:transform.position,.14f,AudioChannel.PC,false);}
        public void SaveChanged(){if(!endingAccess&&game&&game.narrative&&game.narrative.IsInitialized&&!game.IsRestoringCheckpoint)game.narrative.ScheduleSave();}
        public override string CaptureState()=>JsonUtility.ToJson(State);
        public override void RestoreState(string json){State=DesktopState.Parse(json);ApplyWallpaper();}
        void OnDisable()
        {
            if(game)game.StateChanged-=SessionChanged;if(inbox)inbox.ReadFlagRaised-=EmailRead;if(keyboard!=null)keyboard.onTextInput-=Typed;
            if(IsSeated){StopAllCoroutines();transition=boot=forcedCursor=null;FinishClose();}
        }
        void OnDestroy(){foreach(var w in windows.Values)if(w)w.App?.OnClose();if(ScreenTexture){ScreenTexture.Release();Destroy(ScreenTexture);}if(crtMaterial)Destroy(crtMaterial);}
#if UNITY_EDITOR
        protected override void OnValidate(){persistentId=StateId;}
#endif
    }
    public sealed class DesktopIcon:MonoBehaviour,IPointerClickHandler
    {
        public RetroComputer Host;public string AppId;
        public void OnPointerClick(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left)return;Host.SelectIcon(this);if(e.clickCount>=2)Host.OpenApp(AppId);}
    }
}
