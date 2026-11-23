# Real-time handoff

Spectral GT RS ships as a compact glTF 2.0 binary at `spectral_gt_rs_game_ready.glb`.

## Export profile

- Format: binary glTF 2.0 (`.glb`)
- Footprint: approximately 4.7 MB
- Authored scale: meters
- Vehicle front: `+X` in Blender; the browser test rotates the asset into its road-facing axis
- Export scope: `CAR_EXPORT` geometry only
- Excluded from the game handoff: studio cameras, render lights, night rig, and presentation stage
- Exported geometry: 263 objects, including 59 dedicated mechanical-detail objects
- Animation clips: `Doors_PingPong`, `Doors_PingPong.001`, `Hood_Service_PingPong`, `Steering_Input_PingPong`

## Runtime detail groups

The model is organized into body panels, glass, aero/trim, wheels/brakes, cockpit, lights, and mechanics. The mechanics group contains the undertray, front/rear subframes, control arms, toe links, dampers, coil rings, brake lines, radiator and engine-bay forms, turbo forms, and exhaust hardware.

Materials use procedural micro-surface breakup for paint, rubber, and leather, plus clearcoat, carbon weave, smoked glass, brushed metal, emissive lamps, and display materials. This keeps the published binary self-contained without external texture downloads.

## Engine integration

Import the GLB with the engine's standard glTF importer, preserve node hierarchy, and use the four named clips as optional presentation or interaction animations. The vehicle is intentionally delivered without a physics rig: attach the destination engine's wheel colliders, suspension constraints, drivetrain, and LOD policy to its existing visual nodes.

The public [drive test](https://jaronkbragg7337.github.io/spectral-gt-rs/drive.html) is a browser-only validation harness. It verifies that the published binary loads, renders in WebGL, advances under arcade input, reports telemetry, and plays the exported animation clips. `AUTO DRIVE` provides a repeatable motion check; W/A/S/D, Space, and R remain available for manual testing.

## CC0

The model, source, renders, and site are dedicated to the public domain under [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/). Third-party runtime libraries retain their own licenses.
