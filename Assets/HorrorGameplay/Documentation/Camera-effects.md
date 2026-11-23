# Chapter camera treatment and scare response

Install scripts under `Assets/HorrorGameplay/Scripts/Player` (camera), `Scripts/Systems` (Jumpscare), and `Scripts/Rendering` (two Vintage files). Shader goes to `Assets/HorrorGameplay/Shaders/VintageCamera.shader`. Install the native helper in `Assets/HorrorGameplay/Editor` and tests in the existing EditMode/PlayMode test folders. Existing runtime assembly already references URP/Core.

Native integration calls `HorrorGame.Editor.VintageCameraSetup.EnsureRendererFeatures()`, then `Configure(playerCamera, chapterIndex)` with 0 for Chapter1Office and 1 for the TestScene verification fixture. Call `ClearDuplicateImageEffects(game.atmosphere.volume)` to remove old Volume grain/chroma. The runtime atmosphere must also stop assigning its old chromatic intensity: the new image pass owns chromatic separation. No exposure, bloom, light, colour grade or map changes are required.

Narrative integration: `camera.GetComponent<VintageCameraEffect>()?.ConfigureForChapter(book.ChapterIndex(progress.chapterId));`. The office scene is excluded even if its chapter index is incorrectly changed. The filter requires no key, recording UI or overlay. Signal motion freezes when gameplay time is paused. Overlay HUD draws after image treatment.

`FirstPersonCamera.Shake(strength, duration)` remains the public entry point. Bigger/longer impulses add bounded pitch/yaw/roll with damped recovery; short landing impulses keep their original positional shake and cannot erase a stronger ongoing scare. Cutscene camera poses and hiding peek remain usable; pause freezes motion and held breath reduces hiding movement. `Snap` resets accumulated response for restart/warp. `Jumpscare.Fire` also invokes `HorrorAtmosphere.ReactToScare` for recorded human breathing supplied by the audio pass.

For Editor previews, `Camera.CopyFrom` alone does not copy the filter component. Call `VintageCameraSetup.CopyPreviewConfiguration(source, preview)` after placing the preview camera in the source scene. Normal office captures remain clear; capture a configured TestScene base camera for the image comparison.

## RenderGraph review

1. **Validation summary:** API compilation against Unity 6000.6.3f1 / URP17.6 succeeded. One material blit processes only marked base Game cameras before post-processing. SceneView, reflections, overlay cameras, menu and prologue skip it.
2. **Confirmed issues:** none after explicit source/parameters/resolution wiring. Legacy Volume chromatic assignments must be removed by the coordinated atmosphere change.
3. **Risky assumptions:** RenderGraph is enabled; this implementation intentionally does not supply the obsolete compatibility Execute path. Shader/GPU visual proof remains a native Editor step. XR/dynamic-resolution layout is inherited, but only desktop non-XR is in scope and tested.
4. **Recommended checks:** import shader and reject ShaderUtil errors; capture unfiltered/filtered TestScene with the same camera/light/volume; confirm nonblank image and mild image delta; confirm office index0 has EffectiveIntensity0; run the two gate tests and three scare tests. No old door/salinha tests required.
5. **Wiring:** AddBlitPass explicitly binds the active source as `_BlitTexture`, writes a distinct graph-derived destination, supplies all scalar/vector properties and source resolution, then assigns `resourceData.cameraColor` for downstream consumers. The helper owns the static execution and fresh pass data. No globals, depth/normal requests, opaque-colour copy or copy-back pass. The graph descriptor preserves format, dimensions, XR and dynamic scaling; depth and MSAA are intentionally removed for image treatment. Material and parameter block belong to each camera component; material is released on destroy.
6. **Missing information:** native shader compilation, captures and PlayMode results must be recorded after import. No GPU/FPS benchmark claimed.

Primary API reference: [Unity RenderGraph blit guide](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/render-graph-blit.html). Installed package `RenderGraphUtilsBlit.cs` confirms source binding, static render function and per-camera material property block support. Installed ScriptableRenderPass API confirms `requiresIntermediateTexture`; the pass reads the current colour target and does not request an unnecessary `_CameraOpaqueTexture`.

Offline verification produced four assemblies (runtime, Editor helper, EditTests, PlayTests) with zero compiler errors or warnings. This validates C#/Unity APIs and test source compilation; it does not claim native tests have already run.
