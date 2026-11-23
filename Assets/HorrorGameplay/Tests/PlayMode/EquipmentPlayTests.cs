// Verifica .

#if UNITY_EDITOR
using System;using System.Collections;using System.IO;using System.Linq;using NUnit.Framework;using UnityEditor;using UnityEngine;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;using UnityEngine.TestTools;using UnityEngine.UI;
namespace HorrorGame.Tests{
 [Category("Equipment")]
 public sealed class EquipmentPlayTests{
  BeforeDemolitionChapterFixture f;EquipmentController e;FlashlightController lamp;WhistleSystem whistle;WhistleConfig config;EquipmentHud hud;InputReader input;
  Keyboard keyboard,oldKeyboard;Mouse mouse,oldMouse;InputSettings.BackgroundBehavior oldBackground;InputSettings.EditorInputBehaviorInPlayMode oldEditor;bool background;
  [UnitySetUp]public IEnumerator Setup(){
   f=new BeforeDemolitionChapterFixture("bd.school",Array.Empty<StoryMilestoneDefinition>());var scene=f.root.scene;scene.name="SchoolCEEP";
   oldBackground=InputSystem.settings.backgroundBehavior;oldEditor=InputSystem.settings.editorInputBehaviorInPlayMode;background=Application.runInBackground;
   InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;Application.runInBackground=true;
   oldKeyboard=Keyboard.current;oldMouse=Mouse.current;keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();InputSystem.EnableDevice(keyboard);InputSystem.EnableDevice(mouse);
   input=f.root.AddComponent<InputReader>();input.asset=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/HorrorGameplay/Input/HorrorInput.inputactions");f.game.input=input;
   lamp=f.player.gameObject.AddComponent<FlashlightController>();lamp.view=f.camera;lamp.input=input;lamp.handRig=f.Child("Lamp hand",f.camera.transform).transform;lamp.lamp=f.Child("Lamp",lamp.handRig).AddComponent<Light>();
   config=ScriptableObject.CreateInstance<WhistleConfig>();whistle=f.root.AddComponent<WhistleSystem>();whistle.game=f.game;whistle.config=config;whistle.handVisual=f.Child("Whistle hand",f.camera.transform).transform;f.whistle.system=whistle;
   e=f.root.AddComponent<EquipmentController>();e.game=f.game;e.inventory=f.inventory;e.flashlight=lamp;e.whistle=whistle;e.items=new[]{Load("LANTERNA"),Load("APITO"),Load("GRAMPO")};
   var canvas=f.Child("Equipment test canvas").AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
   hud=f.Child("Equipment HUD",canvas.transform).AddComponent<EquipmentHud>();hud.controller=e;hud.titleFont=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/HorrorGameplay/Data/Equipment/Medium SDF.asset");hud.subtitleFont=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/HorrorGameplay/Data/Equipment/Regular SDF.asset");e.hud=hud;hud.Build();
   yield return f.Activate();input.Actions.devices=new InputDevice[]{keyboard,mouse};e.Restore(new EquipmentState{initialized=true,flashlightOn=true},false);EquipmentPreferences.Set(EquipmentHudMode.Automatic,false,false);yield return null;
  }
  EquipmentDefinition Load(string name)=>AssetDatabase.LoadAssetAtPath<EquipmentDefinition>("Assets/HorrorGameplay/Data/Equipment/"+name+".asset");
  void Select(int slot){Assert.That(e.RequestSlot(slot),Is.True);Assert.That(e.Switching,Is.True);e.Advance(.5f);Assert.That(e.Switching,Is.False);}
  IEnumerator KeyPress(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;e.Advance(.5f);}
  [UnityTest]public IEnumerator NumberKeysNumpadAliasesAndPreviousHaveNoConflicts(){
   yield return KeyPress(Key.Digit1);Assert.That(e.ActiveItem.kind,Is.EqualTo(EquipmentKind.Flashlight));
   yield return KeyPress(Key.F);Assert.That(lamp.IsOn,Is.False);yield return KeyPress(Key.F);Assert.That(lamp.IsOn,Is.True);
   yield return KeyPress(Key.Numpad1);Assert.That(e.ActiveItem,Is.Null);yield return KeyPress(Key.Numpad2);Assert.That(whistle.Equipped,Is.True);
   yield return KeyPress(Key.Q);Assert.That(e.ActiveItem,Is.Null,"Q retains the old whistle equip/store action.");
   yield return KeyPress(Key.Digit1);yield return KeyPress(Key.Digit2);yield return KeyPress(Key.X);Assert.That(e.IsActive(EquipmentKind.Flashlight),Is.True);
   yield return KeyPress(Key.F);Assert.That(lamp.RequestedOn,Is.False);Select(2);yield return KeyPress(Key.F);Assert.That(lamp.RequestedOn,Is.False,"Old light action cannot toggle a stowed lamp.");
   yield return KeyPress(Key.Digit3);Assert.That(e.IsActive(EquipmentKind.Whistle),Is.True,"Missing slot 3 is ignored.");
  }
  [UnityTest]public IEnumerator WheelSkipsMissingSlotsCanReverseAndDoesNotStealUiScroll(){
   Select(1);Assert.That(e.Cycle(1),Is.True);e.Advance(.5f);Assert.That(e.IsActive(EquipmentKind.Whistle),Is.True);
   Assert.That(e.Cycle(1),Is.True);e.Advance(.5f);Assert.That(e.IsActive(EquipmentKind.Flashlight),Is.True);
   EquipmentPreferences.Set(EquipmentHudMode.Automatic,false,true);Assert.That(e.Cycle(1),Is.True);e.Advance(.5f);Assert.That(e.IsActive(EquipmentKind.Whistle),Is.True);
   Assert.That(f.game.BeginInspection(),Is.True);InputSystem.QueueStateEvent(mouse,new MouseState{scroll=new Vector2(0,120)});yield return null;yield return null;Assert.That(e.Switching,Is.False);Assert.That(e.IsActive(EquipmentKind.Whistle),Is.True);f.game.EndInspection();yield return null;yield return null;
   InputSystem.QueueStateEvent(mouse,new MouseState());Select(1);Cursor.lockState=CursorLockMode.Locked;InputSystem.QueueStateEvent(mouse,new MouseState{scroll=new Vector2(0,120)});yield return null;yield return null;InputSystem.QueueStateEvent(mouse,new MouseState());e.Advance(.5f);Assert.That(e.IsActive(EquipmentKind.Whistle),Is.True,"Actual mouse wheel goes through the Input System.");
  }
  [UnityTest]public IEnumerator OneHandBatteryAndRememberedSwitchStateWithOptionalLeftHand(){
   float initial=lamp.BatterySeconds;yield return new WaitForSeconds(.12f);Assert.That(lamp.BatterySeconds,Is.EqualTo(initial).Within(.005f));
   Select(1);yield return new WaitForSeconds(.12f);Assert.That(lamp.BatterySeconds,Is.LessThan(initial));
   Select(2);yield return null;Assert.That(lamp.IsOn,Is.False);Assert.That(lamp.handRig.gameObject.activeSelf,Is.False);Assert.That(whistle.handVisual.gameObject.activeSelf,Is.True);
   initial=lamp.BatterySeconds;yield return new WaitForSeconds(.12f);Assert.That(lamp.BatterySeconds,Is.EqualTo(initial).Within(.005f));
   Select(1);Assert.That(lamp.IsOn,Is.True,"Equipping remembers the player's switch state.");lamp.Toggle();Select(2);Select(1);Assert.That(lamp.IsOn,Is.False);
   lamp.Toggle();EquipmentPreferences.Set(EquipmentHudMode.Automatic,true,false);Select(2);yield return null;Assert.That(lamp.IsOn,Is.True);Assert.That(lamp.handRig.gameObject.activeSelf,Is.True);
   initial=lamp.BatterySeconds;yield return new WaitForSeconds(.12f);Assert.That(lamp.BatterySeconds,Is.LessThan(initial),"An optional lit left-hand lamp still consumes the existing battery.");
   Assert.That(e.RequestSlot(2),Is.True);e.Advance(.5f);Assert.That(lamp.IsOn,Is.False,"Empty hands also stow the optional left-hand lamp.");
  }
  [UnityTest]public IEnumerator SwitchingAndModalStatesBlockUseAndSelection(){
   Assert.That(e.RequestSlot(2),Is.True);Assert.That(whistle.Press(),Is.False);Assert.That(whistle.BlowTap(),Is.False);Assert.That(e.RequestSlot(1),Is.False);e.Advance(.5f);
   Assert.That(whistle.Press(),Is.True);Assert.That(e.RequestSlot(1),Is.False);whistle.Cancel();
   f.game.Pause();Assert.That(e.RequestSlot(1)||e.Cycle(1)||e.Previous(),Is.False);f.game.Resume();yield return null;yield return null;
   Assert.That(f.game.BeginInspection(),Is.True);Assert.That(e.RequestSlot(1)||e.Cycle(1)||e.Previous(),Is.False);f.game.EndInspection();yield return null;yield return null;
   f.game.DisableControl();Assert.That(e.RequestSlot(1),Is.False);f.game.EnableControl();yield return null;yield return null;
   Assert.That(e.RequestSlot(1),Is.True);f.game.Pause();float pose=e.LowerAmount;e.Advance(2);Assert.That(e.LowerAmount,Is.EqualTo(pose));f.game.Resume();yield return null;yield return null;e.Advance(.5f);
  }
  [UnityTest]public IEnumerator LeftMouseUsesTheHeldItemAndEStillInteracts(){
   Select(1);bool on=lamp.IsOn;
   InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return null;yield return null;
   Assert.That(lamp.IsOn,Is.EqualTo(!on));InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;yield return null;
   Select(2);int count=whistle.State.totalBlows;
   InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return null;yield return null;InputSystem.QueueStateEvent(mouse,new MouseState());
   yield return BeforeDemolitionChapterFixture.Until(()=>whistle.State.totalBlows==count+1&&!whistle.Busy,"Mouse tap emits one whistle",3);Assert.That(whistle.State.learnedTap,Is.True);
   whistle.State.cooldownRemaining=0;InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));
   yield return BeforeDemolitionChapterFixture.Until(()=>whistle.State.totalBlows==count+2&&!whistle.Busy,"A held mouse still emits exactly one tap",3);yield return new WaitForSeconds(.7f);Assert.That(whistle.State.totalBlows,Is.EqualTo(count+2));Assert.That(whistle.State.learnedHold,Is.False,"The current single-click design never sustains an input hold");
   InputSystem.QueueStateEvent(mouse,new MouseState());yield return BeforeDemolitionChapterFixture.Until(()=>!whistle.Busy,"Release mouse lowers whistle",3);
   var interaction=f.game.interaction;interaction.input=input;interaction.enabled=true;
   var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(f.root.transform,true);go.transform.position=f.camera.transform.position+f.camera.transform.forward;var drawer=go.AddComponent<InteractiveDrawer>();Physics.SyncTransforms();yield return null;yield return null;
   whistle.State.cooldownRemaining=0;InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return null;yield return null;Assert.That(drawer.IsOpen,Is.False,"Whistling must not also open the aimed drawer.");
   InputSystem.QueueStateEvent(mouse,new MouseState());whistle.Cancel();yield return KeyPress(Key.E);Assert.That(drawer.IsOpen,Is.True,"E keeps its ordinary world interaction.");
  }
  [UnityTest]public IEnumerator BoundIconsAndNormalTorchHaveNoRectanglesOrOnLabel(){
   Select(1);foreach(var row in hud.rows){row.icon.sprite=null;row.number.text="0";row.name.text="";}hud.RefreshPresentation();hud.Advance(.4f);
   for(int i=0;i<hud.rows.Length;i++){Assert.That(hud.rows[i].icon.sprite,Is.SameAs(e.items[i].icon));Assert.That(hud.rows[i].number.text,Is.EqualTo(e.items[i].slot.ToString()));Assert.That(hud.rows[i].name.text,Is.EqualTo(e.items[i].displayName));}
   Assert.That(hud.rows[0].status.gameObject.activeSelf,Is.False);lamp.Toggle();hud.Advance(.2f);Assert.That(hud.rows[0].status.gameObject.activeSelf,Is.False);
   lamp.Restore(5,true);hud.Advance(.2f);Assert.That(hud.rows[0].status.text,Is.EqualTo("FRACA"));yield return null;
  }
  [UnityTest]public IEnumerator SaveStateRoundTripLegacyDefaultsAndUnknownItemsStaySafe(){
   Select(1);lamp.Toggle();Select(2);var state=JsonUtility.FromJson<EquipmentState>(JsonUtility.ToJson(e.Capture()));
   Assert.That(state.Valid,Is.True);Assert.That(state.equippedId,Is.EqualTo("whistle"));Assert.That(state.flashlightOn,Is.False);
   Select(1);lamp.Toggle();e.Restore(state,true);Assert.That(e.IsActive(EquipmentKind.Whistle),Is.True);Assert.That(lamp.RequestedOn,Is.False);
   e.Restore(null,false);Assert.That(e.ActiveItem,Is.Null);e.Restore(null,true);Assert.That(e.IsActive(EquipmentKind.Flashlight),Is.True);
   e.Restore(new EquipmentState{initialized=true,equippedId="item-not-in-this-build"},false);Assert.That(e.ActiveItem,Is.Null);Assert.That(JsonUtility.FromJson<SaveData>("{\"version\":2}").equipment.initialized,Is.False);
   Assert.That(new EquipmentState{version=9}.Valid,Is.False);Assert.That(new EquipmentState{hudMode=(EquipmentHudMode)99}.Valid,Is.False);
   yield return null;
  }
  [UnityTest]public IEnumerator ARealDrawerInteractionBlocksSwitchingUntilItsTravelFinishes(){
   var interaction=f.game.interaction;interaction.input=input;interaction.enabled=true;
   var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(f.root.transform,true);go.transform.position=f.camera.transform.position+f.camera.transform.forward;
   var drawer=go.AddComponent<InteractiveDrawer>();drawer.localTravel=new Vector3(.3f,0,0);drawer.travelSmoothTime=.12f;
   Physics.SyncTransforms();yield return null;yield return null;Assert.That(interaction.Target,Is.SameAs(drawer));
   yield return KeyPress(Key.E);Assert.That(interaction.EquipmentInteractionBusy,Is.True);Assert.That(e.RequestSlot(1),Is.False);
   yield return BeforeDemolitionChapterFixture.Until(()=>!interaction.EquipmentInteractionBusy,"Drawer settles",4);Assert.That(e.RequestSlot(1),Is.True);e.Advance(.5f);
  }
  [UnityTest]public IEnumerator HudAutoHidePickupLowBatteryAndPreferences(){
   hud.ShowFor(2.5f);hud.Advance(.3f);Assert.That(hud.group.alpha,Is.EqualTo(1));hud.Advance(5);hud.Advance(.3f);Assert.That(hud.group.alpha,Is.Zero);
   Select(1);hud.Advance(.3f);Assert.That(hud.group.alpha,Is.EqualTo(1));
   hud.Advance(3);hud.Advance(.3f);lamp.Restore(5,true);hud.Advance(.3f);Assert.That(hud.group.alpha,Is.EqualTo(1));
   Assert.That(hud.rows[2].root.gameObject.activeSelf,Is.False);f.inventory.Add(new InventoryItem{id="lockpick"});hud.Advance(.1f);Assert.That(hud.rows[2].root.gameObject.activeSelf,Is.True);Assert.That(hud.Remaining,Is.GreaterThan(3));
   EquipmentPreferences.Set(EquipmentHudMode.Hidden,false,false);hud.Advance(.1f);Assert.That(hud.group.alpha,Is.Zero);
   EquipmentPreferences.Set(EquipmentHudMode.Always,false,false);hud.Advance(5);Assert.That(hud.group.alpha,Is.EqualTo(1));f.game.Pause();hud.Advance(.1f);Assert.That(hud.group.alpha,Is.Zero);f.game.Resume();yield return null;yield return null;
   Assert.That(hud.rows.SelectMany(r=>r.root.GetComponentsInChildren<Graphic>(true)).All(g=>!g.raycastTarget),Is.True);
  }
  [UnityTest]public IEnumerator WarmSelectionAllocationsAreMeasured(){
   for(int i=0;i<8;i++){e.RequestSlot(i%2+1);e.Advance(.5f);}long total=0,max=0;
   for(int i=0;i<64;i++){long start=GC.GetAllocatedBytesForCurrentThread();AssertRequest(i%2+1);long bytes=GC.GetAllocatedBytesForCurrentThread()-start;total+=bytes;max=Math.Max(max,bytes);}
   Directory.CreateDirectory("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/Equipment20261031");File.WriteAllText("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/Equipment20261031/allocation.json","{\"samples\":64,\"meanBytes\":"+(total/64d).ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"peakBytes\":"+max+",\"scope\":\"Warm RequestSlot+Advance with HUD event; excludes test assertions, deferred save/audio mixer and rendered frame\"}");yield return null;
  }
  [UnityTest]public IEnumerator EquipmentHudActuallyRendersAndFitsFourAspectRatios(){
   Select(1);EquipmentPreferences.Set(EquipmentHudMode.Always,false,false);
   var canvas=hud.GetComponentInParent<Canvas>();var camera=f.Child("HUD capture camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.04f);camera.cullingMask=1<<5;camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.enabled=false;
   canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
   foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=5;
   string dir="C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/EquipmentFix20261101";Directory.CreateDirectory(dir);
   foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1440,900),new Vector2Int(1024,768),new Vector2Int(2560,1080)}){
    var rt=new RenderTexture(size.x,size.y,24);Texture2D image=null;var old=RenderTexture.active;
    try{rt.Create();camera.targetTexture=rt;Canvas.ForceUpdateCanvases();hud.Advance(.4f);Canvas.ForceUpdateCanvases();yield return null;
     var corners=new Vector3[4];((RectTransform)hud.transform).GetWorldCorners(corners);
     foreach(var c in corners){var point=RectTransformUtility.WorldToScreenPoint(camera,c);Assert.That(point.x,Is.InRange(-2f,size.x+2f));Assert.That(point.y,Is.InRange(-2f,size.y+2f));}
     UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=rt,isPreview=false});RenderTexture.active=rt;
     image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();
     Assert.That(image.GetPixels32().Count(p=>p.r>100&&p.g>100&&p.b>100),Is.GreaterThan(300),"Actual UI pixels, not just a hierarchy.");
     File.WriteAllBytes(dir+"/hud-"+size.x+"x"+size.y+".png",image.EncodeToPNG());
    }finally{camera.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.Destroy(rt);if(image)UnityEngine.Object.Destroy(image);}
   }
  }
  void AssertRequest(int slot){if(!e.RequestSlot(slot))throw new Exception("Request unexpectedly blocked");e.Advance(.5f);}
  [UnityTearDown]public IEnumerator Cleanup(){
   if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();if(oldMouse!=null&&oldMouse.added)oldMouse.MakeCurrent();
   InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;Application.runInBackground=background;
   if(f!=null)yield return f.Dispose();if(config)UnityEngine.Object.Destroy(config);
  }
 }
 public sealed partial class BeforeDemolitionSchoolSceneTests{
  [UnityTest,Category("Equipment"),Timeout(90000)]public IEnumerator EquipmentLoadedSceneHasRealIconsNumbersAndNoOnLabel(){
   var e=game.equipment;yield return Until(()=>e.CanSwitch,"Native equipment ready");var source=e.hud;
   for(int i=0;i<source.rows.Length;i++){Assert.That(source.rows[i].icon.sprite,Is.SameAs(e.items[i].icon),"The saved scene binds icons after reload.");Assert.That(source.rows[i].number.text,Is.EqualTo(e.items[i].slot.ToString()));Assert.That(source.rows[i].name.text,Is.EqualTo(e.items[i].displayName));}
   if(!e.IsActive(EquipmentKind.Flashlight)){Assert.That(e.RequestSlot(1),Is.True);e.Advance(.5f);}game.save.flashlight.Restore(200,true);source.Advance(.4f);
   Assert.That(source.rows[0].status.gameObject.activeSelf,Is.True);
   Assert.That(source.rows[0].status.text,Is.EqualTo($"{Mathf.CeilToInt(game.save.flashlight.Battery01*100)}%"));
   Assert.That(source.rows[0].battery.All(bar=>!bar.gameObject.activeSelf),Is.True);
   var root=new GameObject("Native HUD capture",typeof(RectTransform),typeof(Canvas));root.layer=5;var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;
   var cameraObject=new GameObject("Native HUD camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.cullingMask=1<<5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.04f);canvas.worldCamera=camera;canvas.planeDistance=1;
   var copy=UnityEngine.Object.Instantiate(source.gameObject,root.transform,false);copy.GetComponent<EquipmentHud>().enabled=false;copy.GetComponent<CanvasGroup>().alpha=1;foreach(var t in copy.GetComponentsInChildren<Transform>(true))t.gameObject.layer=5;
   var rt=new RenderTexture(1920,1080,24);Texture2D image=null;var old=RenderTexture.active;
   try{rt.Create();camera.targetTexture=rt;Canvas.ForceUpdateCanvases();yield return null;UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=rt,isPreview=false});RenderTexture.active=rt;image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();Assert.That(image.GetPixels32().Count(p=>p.r>100&&p.g>100&&p.b>100),Is.GreaterThan(300));File.WriteAllBytes("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/EquipmentFix20261101/hud-from-saved-school.png",image.EncodeToPNG());}
   finally{camera.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.Destroy(rt);if(image)UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(cameraObject);}
  }
  [UnityTest,Category("Equipment"),Timeout(90000)]public IEnumerator EquipmentNativeCheckpointAndLegacyRestoreKeepTheLightAndWhistleCoherent(){
   var e=game.equipment;Assert.That(e&&e.hud,Is.True);yield return Until(()=>e.CanSwitch,"Equipment available");
   Assert.That(e.RequestSlot(2),Is.True);e.Advance(.5f);Assert.That(game.save.Save(),Is.True);Assert.That(game.save.TryReadCurrent(out var state),Is.True);Assert.That(state.equipment.equippedId,Is.EqualTo("whistle"));
   Assert.That(e.RequestSlot(1),Is.True);e.Advance(.5f);Assert.That(game.save.RestoreSnapshot(state),Is.True);Assert.That(e.IsActive(EquipmentKind.Whistle),Is.True);Assert.That(game.save.flashlight.IsOn,Is.False);
   state.equipment=null;state.flashlightOn=true;Assert.That(game.save.RestoreSnapshot(state),Is.True);Assert.That(e.IsActive(EquipmentKind.Flashlight),Is.True);Assert.That(game.save.flashlight.IsOn,Is.True);
   state.flashlightOn=false;Assert.That(game.save.RestoreSnapshot(state),Is.True);Assert.That(e.ActiveItem,Is.Null);
   for(int i=0;i<8;i++){e.RequestSlot(i%2+1);e.Advance(.5f);}long total=0,peak=0;
   for(int i=0;i<64;i++){long start=GC.GetAllocatedBytesForCurrentThread();if(!e.RequestSlot(i%2+1))throw new Exception("Native swap blocked");e.Advance(.5f);long bytes=GC.GetAllocatedBytesForCurrentThread()-start;total+=bytes;peak=Math.Max(peak,bytes);}
   File.WriteAllText("C:/Users/ruhtra/Documents/Codex/2026-09-29/oi/outputs/Equipment20261031/native-allocation.json","{\"samples\":64,\"meanBytes\":"+(total/64d).ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"peakBytes\":"+peak+",\"scope\":\"Warm native school RequestSlot+Advance, real HUD and recorded SFX/UI pool; synchronous batch excludes rendered frames, deferred save IO and mixer thread\"}");
  }
 }
 public sealed partial class BeforeDemolitionOfficeSceneTests{
  [UnityTest,Category("Equipment"),Timeout(90000)]public IEnumerator EquipmentOfficeTorchIsBlockedAndRealComputerAndPhoneSuppressSlots(){
   var e=game.equipment;yield return Until(()=>e.CanSwitch,"Office control");Assert.That(e.Owns(e.FindKind(EquipmentKind.Flashlight)),Is.False);Assert.That(e.RequestSlot(1),Is.False);Assert.That(game.save.flashlight.IsOn,Is.False);e.hud.ShowFor(5);e.hud.Advance(.4f);Assert.That(e.hud.group.alpha,Is.Zero,"No equipment HUD before the flashlight is collected.");
   var pc=UnityEngine.Object.FindAnyObjectByType<HorrorGame.Desktop.RetroComputer>();Assert.That(pc.Open(),Is.True);Assert.That(e.RequestSlot(2)||e.Cycle(1),Is.False);e.hud.Advance(.1f);Assert.That(e.hud.group.alpha,Is.Zero);pc.Close();yield return Until(()=>game.CanControl,"After PC",12);
   var phone=PhoneMessages.For(game);phone.State.acquired=true;Assert.That(phone.OpenDirect(),Is.True);Assert.That(e.RequestSlot(2)||e.Previous(),Is.False);e.hud.Advance(.1f);Assert.That(e.hud.group.alpha,Is.Zero);phone.Close();yield return Until(()=>game.CanControl,"After phone",12);
   game.Pause();game.hud.OpenOptions();yield return null;yield return null;
   var entry=game.hud.optionsPanel.GetComponentsInChildren<Button>(true).Single(b=>b.GetComponentInChildren<TMPro.TMP_Text>()&&b.GetComponentInChildren<TMPro.TMP_Text>().text=="Equipamentos");
   BeforeDemolitionChapterFixture.Click(entry);Assert.That(EquipmentOptionsPanel.TryClose(),Is.True,"The existing Options opens the equipment panel and Esc can close it first.");game.hud.CloseOptions();game.Resume();yield return null;
  }
 }
 public sealed partial class BeforeDemolitionRoadSceneTests{
  [UnityTest,Category("Equipment"),Timeout(90000)]public IEnumerator EquipmentRoadPickupSelectsTorchWithoutAllowingItemsWhileSeated(){
   var e=game.equipment;Assert.That(e,Is.Not.Null);Assert.That(e.Owns(e.FindKind(EquipmentKind.Flashlight)),Is.False);Assert.That(e.CanSwitch,Is.False);Assert.That(e.ReservesPrimaryClick,Is.False,"Seated equipment does not consume car interaction clicks.");e.hud.ShowFor(5);e.hud.Advance(.4f);Assert.That(e.hud.group.alpha,Is.Zero,"No HUD on the journey before pickup.");
   Fast(.2f);yield return Until(()=>flow.Parked,"Arrival",20);yield return Aim(BeforeDemolitionRoadAction.Flashlight);yield return KeyPress(Key.E);
   Assert.That(flow.HasTorch,Is.True);Assert.That(e.IsActive(EquipmentKind.Flashlight),Is.True);Assert.That(e.CanSwitch,Is.False);Assert.That(e.hud.Remaining,Is.GreaterThan(0));
   yield return OpenMessages();yield return CloseMessages();yield return Aim(BeforeDemolitionRoadAction.Exit);yield return KeyPress(Key.E);yield return Until(()=>e.CanSwitch,"Outside car",12);
   Assert.That(game.save.flashlight.IsOn,Is.True);Assert.That(game.save.flashlight.handRig.gameObject.activeInHierarchy,Is.True);Assert.That(e.RequestSlot(2),Is.True);e.Advance(.5f);Assert.That(game.save.flashlight.IsOn,Is.False);
  }
 }
}
#endif
