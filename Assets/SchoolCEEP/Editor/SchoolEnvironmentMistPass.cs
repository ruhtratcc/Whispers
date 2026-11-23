// Ferramenta da escola para construção ou acabamento.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HorrorGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SchoolCEEP.Editor
{
    /// <summary>
    /// Scene-owned, low-lying wisps reuse the existing menu texture, without changing
    /// that texture/material or adding camera overlays. Emitters stay in the world.
    /// The caller owns the final save after inspecting the returned audit.
    /// </summary>
    public static class SchoolEnvironmentMistPass
    {
        public const string GroupName = "Ambiente — névoa localizada";
        const string TexturePath = "Assets/HorrorMenu/Art/CorridorMist.asset";
        const string SourceUnitName = "Sala — cadeira azul com carteira";
        const float NearFade = 2, FarFade = 5, SoftFade = .8f;
        const int MaximumPerEmitter = 16;

        [Serializable] public sealed class EmitterResult
        {
            public string name;
            public Vector3 position, shapeSize, maximumBillboard;
            public float floor, alpha;
            public int maximumParticles;
            public bool worldSpace, environmentParent, supported, containedInRoom;
        }
        [Serializable] public sealed class Report
        {
            public int revision = 1;
            public string scene, texture, material, protectedBefore, protectedAfter;
            public bool passed, protectedArchitectureAndStoryPreserved, worldSpace, environmentParent;
            public bool noNewColliders, noNewLights, nearCameraClear, softParticlesEnabled, depthTextureRequested;
            public bool attachedDustDisabled, allEmitterCentersSupported, allRoomWispsContained;
            public int classroomEmitters, exteriorEmitters, emitters, maximumLiveParticles, colliders, lights, attachedDustCount;
            public float cameraNearFade, cameraFarFade, maximumAlpha, fogStart, fogEnd;
            public Color fogColor;
            public EmitterResult[] emitterResults;
            public string[] skipped, errors, notes;
        }
        sealed class Placement
        {
            public string name;
            public Vector3 position, shapeSize, maximumBillboard;
            public float floor, alpha;
            public int maximumParticles;
            public bool room;
            public Bounds bounds;
        }

        public static Report Apply()
        {
            var scene = SceneManager.GetActiveScene();
            Transform parent = scene.path == SchoolIntegrationPass.ScenePath
                ? scene.GetRootGameObjects().Single(g => g.name.StartsWith("CEEP", StringComparison.Ordinal)).transform : null;
            return ApplyToScene(scene, parent);
        }

        public static Report Apply(Transform root)
        {
            if (!root || EditorUtility.IsPersistent(root)) throw new ArgumentException("A loaded scene root is required.");
            return ApplyToScene(root.gameObject.scene, root.gameObject.scene.path == SchoolIntegrationPass.ScenePath ? root : null);
        }

        static Report ApplyToScene(Scene scene, Transform parent)
        {
            Require(scene, true);
            var previous = FindGroup(scene);
            if (previous) return AuditScene(scene);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!texture || !shader || !shader.isSupported)
                throw new InvalidDataException("The existing CorridorMist texture and supported URP particle shader are required.");
            var game = All<GameSession>(scene).Single();
            if (!game.player || !game.player.view || !game.player.view.playerCamera)
                throw new InvalidOperationException("Preserve the scene's existing player camera.");
            bool school = scene.path == SchoolIntegrationPass.ScenePath;
            var skipped = new List<string>();
            Physics.SyncTransforms();
            var placements = school ? SchoolPlacements(scene, parent, skipped) : RoadPlacements(scene, skipped);
            if (placements.Count == 0 || school && placements.Count(p => p.room) != 27)
                throw new InvalidOperationException("Expected all 27 validated classroom centers, or at least one supported arrival emitter.");
            string protectedBefore = ProtectedSignature(scene);
            string materialPath = (school ? "Assets/SchoolCEEP/Materials/Refinement" : "Assets/RoadChapter/Data/Refinement") + "/EnvironmentMist.mat";
            var material = ResolveMaterial(materialPath, shader, texture);
            var cameraData = game.player.view.playerCamera.GetUniversalAdditionalCameraData();
            var oldDepth = cameraData.requiresDepthOption;
            bool oldFog = RenderSettings.fog;
            var oldMode = RenderSettings.fogMode; var oldColor = RenderSettings.fogColor;
            float oldStart = RenderSettings.fogStartDistance, oldEnd = RenderSettings.fogEndDistance;
            var dust = All<SchoolFlashlightDust>(scene);
            var oldDustEnabled = dust.Select(d => d.enabled).ToArray();
            var oldDustAwake = dust.Select(d => d.particles && d.particles.main.playOnAwake).ToArray();
            var group = new GameObject(GroupName).transform;
            SceneManager.MoveGameObjectToScene(group.gameObject, scene);
            if (parent) group.SetParent(parent, false);
            try
            {
                // Soft intersections only require this scene camera's depth texture.
                cameraData.requiresDepthOption = CameraOverrideOption.On;
                Record(cameraData);
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 14; RenderSettings.fogEndDistance = school ? 110 : 85;
                RenderSettings.fogColor = school ? new Color(.012f, .018f, .017f) : new Color(.010f, .015f, .016f);
                foreach (var attached in dust)
                {
                    if (attached.particles)
                    {
                        attached.particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var main = attached.particles.main; main.playOnAwake = false;
                        Record(attached.particles);
                    }
                    attached.enabled = false;
                    Record(attached);
                }
                int seed = 911;
                foreach (var placement in placements) CreateEmitter(group, placement, material, seed++);
                EditorSceneManager.MarkSceneDirty(scene);
                var report = AuditScene(scene);
                report.protectedBefore = protectedBefore;
                report.protectedAfter = ProtectedSignature(scene);
                report.protectedArchitectureAndStoryPreserved = report.protectedBefore == report.protectedAfter;
                report.skipped = skipped.ToArray();
                report.passed &= report.protectedArchitectureAndStoryPreserved;
                if (!report.passed) throw new InvalidOperationException("Environment mist audit failed: " + string.Join("; ", report.errors));
                return report;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(group.gameObject);
                cameraData.requiresDepthOption = oldDepth; Record(cameraData);
                RenderSettings.fog = oldFog; RenderSettings.fogMode = oldMode; RenderSettings.fogColor = oldColor;
                RenderSettings.fogStartDistance = oldStart; RenderSettings.fogEndDistance = oldEnd;
                for (int i = 0; i < dust.Length; i++)
                {
                    dust[i].enabled = oldDustEnabled[i];
                    if (dust[i].particles) { var main = dust[i].particles.main; main.playOnAwake = oldDustAwake[i]; Record(dust[i].particles); }
                    Record(dust[i]);
                }
                throw;
            }
        }

        static List<Placement> SchoolPlacements(Scene scene, Transform root, List<string> skipped)
        {
            if (!root) throw new InvalidOperationException("Expected the finished school architecture root.");
            string path = SchoolMapBuilder.Output + "/School-classroom-photo-report.json";
            if (!File.Exists(path)) throw new InvalidDataException("The validated classroom catalogue is required: " + path);
            var catalogue = JsonUtility.FromJson<SchoolClassroomPhotoPass.Report>(File.ReadAllText(path));
            if (catalogue == null || catalogue.rooms == null) throw new InvalidDataException("Missing classroom catalogue.");
            var units = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == SourceUnitName).ToArray();
            var result = new List<Placement>();
            foreach (var source in catalogue.rooms)
            {
                string name = source.name + " — pavimento " + source.floor;
                var roomUnits = units.Where(u => u.parent && u.parent.name == name).ToArray();
                if (roomUnits.Length == 0) continue;
                if (roomUnits.Select(u => u.parent).Distinct().Count() != 1 || source.adaptedPdfBox == null || source.adaptedPdfBox.Length != 4)
                    throw new InvalidDataException("Ambiguous validated classroom: " + name);
                float expected = roomUnits.Select(u => u.position.y).OrderBy(y => y).ElementAt(roomUnits.Length / 2);
                var box = source.adaptedPdfBox;
                var nw = SchoolMapBuilder.Point(box[0], box[1], expected);
                var se = SchoolMapBuilder.Point(box[2], box[3], expected);
                var bounds = new Bounds((nw + se) * .5f, new Vector3(se.x - nw.x, 2.4f, nw.z - se.z));
                var center = new Vector3(source.center.x, expected, source.center.z);
                if (!TryFloor(scene, center, expected, out float floor))
                    throw new InvalidOperationException("Classroom mist requires the actual supported floor: " + name);
                // The entire billboard, emitter box and slow drift fit inside the registered room.
                // Reserve the billboard half-width on both axes because billboards turn toward the viewer.
                var size = new Vector3(3, .62f, 1);
                float padding = size.x + .7f;
                var shape = new Vector3(Mathf.Max(0, bounds.size.x - padding), .10f, Mathf.Max(0, bounds.size.z - padding));
                center.x = bounds.center.x; center.z = bounds.center.z;
                result.Add(new Placement { name = "Sala — névoa baixa — " + name, room = true, bounds = bounds,
                    position = new Vector3(center.x, floor + .43f, center.z), floor = floor,
                    shapeSize = shape, maximumBillboard = size, alpha = .11f, maximumParticles = 6 });
            }
            var candidates = new[]
            {
                new KeyValuePair<string, Vector3>("Entrada — ar no pátio", new Vector3(.55f, 0, 13)),
                new KeyValuePair<string, Vector3>("Hall — junto à entrada", new Vector3(.55f, .644f, 26)),
                new KeyValuePair<string, Vector3>("Corredor central — ar parado", SchoolMapBuilder.Point(1416, 1037, .644f)),
                new KeyValuePair<string, Vector3>("Cantina — passagem exterior", SchoolMapBuilder.Point(1458, 848, .644f)),
                new KeyValuePair<string, Vector3>("Caminho da quadra — ar baixo", SchoolMapBuilder.Point(2080, 1240, 0)),
            };
            foreach (var candidate in candidates)
            {
                if (!TryFloor(scene, candidate.Value, candidate.Value.y, out float floor))
                { skipped.Add(candidate.Key + ": no supported ground at the proposed location."); continue; }
                result.Add(new Placement { name = candidate.Key, floor = floor, position = new Vector3(candidate.Value.x, floor + .37f, candidate.Value.z),
                    shapeSize = new Vector3(.3f, .08f, .65f), maximumBillboard = new Vector3(2.4f, .55f, 1), alpha = .12f, maximumParticles = 8 });
            }
            return result;
        }

        static List<Placement> RoadPlacements(Scene scene, List<string> skipped)
        {
            var flow = All<RoadJourneyFlow>(scene).Single();
            if (!flow.gate) throw new InvalidOperationException("Preserve the original arrival gate.");
            var result = new List<Placement>();
            var gate = flow.gate.transform.position;
            // The three fixed volumes sit on the arrival pavement, not around the moving car.
            foreach (var offset in new[] { new Vector3(-2, 0, -7), new Vector3(2, 0, -7), new Vector3(0, 0, -15) })
            {
                var center = new Vector3(gate.x + offset.x, 0, gate.z + offset.z);
                string name = "Chegada — névoa no passeio " + (result.Count + 1);
                if (!TryFloor(scene, center, 0, out float floor)) { skipped.Add(name + ": no supported pavement."); continue; }
                result.Add(new Placement { name = name, floor = floor, position = new Vector3(center.x, floor + .35f, center.z),
                    shapeSize = new Vector3(.45f, .06f, 2.5f), maximumBillboard = new Vector3(3.2f, .55f, 1), alpha = .12f, maximumParticles = 10 });
            }
            return result;
        }

        static void CreateEmitter(Transform parent, Placement p, Material material, int seed)
        {
            var go = new GameObject(p.name, typeof(ParticleSystem));
            go.transform.SetParent(parent, false); go.transform.position = p.position;
            var particles = go.GetComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed = false; particles.randomSeed = (uint)seed;
            var main = particles.main;
            main.duration = 32; main.loop = true; main.prewarm = true; main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
            main.startLifetime = new ParticleSystem.MinMaxCurve(22, 28); main.startSpeed = 0;
            main.maxParticles = p.maximumParticles; main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(p.maximumBillboard.x * .75f, p.maximumBillboard.x);
            main.startSizeY = new ParticleSystem.MinMaxCurve(p.maximumBillboard.y * .72f, p.maximumBillboard.y); main.startSizeZ = 1;
            main.startRotation = new ParticleSystem.MinMaxCurve(-.025f, .025f);
            // Dark, alpha-blended wisps do not light the school or produce a grey screen wash.
            main.startColor = new Color(.065f, .082f, .076f, p.alpha);
            var emission = particles.emission; emission.enabled = true; emission.rateOverTime = p.room ? .055f : .10f;
            var shape = particles.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = p.shapeSize;
            var velocity = particles.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-.003f, .003f); velocity.y = new ParticleSystem.MinMaxCurve(0, .001f);
            velocity.z = new ParticleSystem.MinMaxCurve(-.002f, .002f);
            var fade = particles.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.85f, .18f), new GradientAlphaKey(.75f, .72f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View; renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            // Fixed custom bounds prevent these small, low emitters being treated as screen-wide fog.
            float padding = p.maximumBillboard.x * .5f + .18f;
            renderer.localBounds = new Bounds(new Vector3(0, .02f, 0),
                new Vector3(p.shapeSize.x + padding * 2, p.shapeSize.y + p.maximumBillboard.y + .12f, p.shapeSize.z + padding * 2));
            Record(particles, renderer);
        }

        static Material ResolveMaterial(string path, Shader shader, Texture2D texture)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader) { name = "Environment mist — restrained world wisps" }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SoftParticlesEnabled", 1); material.SetFloat("_SoftParticlesNearFadeDistance", 0); material.SetFloat("_SoftParticlesFarFadeDistance", SoftFade);
            material.SetVector("_SoftParticleFadeParams", new Vector4(0, 1 / SoftFade, 0, 0));
            material.SetFloat("_CameraFadingEnabled", 1); material.SetFloat("_CameraNearFadeDistance", NearFade); material.SetFloat("_CameraFarFadeDistance", FarFade);
            // URP particle shaders consume the reciprocal fade interval, not the far distance itself.
            material.SetVector("_CameraFadeParams", new Vector4(NearFade, 1 / (FarFade - NearFade), 0, 0));
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.EnableKeyword("_SOFTPARTICLES_ON"); material.EnableKeyword("_FADING_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON"); material.DisableKeyword("_ALPHAMODULATE_ON");
            material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = 3000;
            material.SetShaderPassEnabled("ShadowCaster", false); material.SetShaderPassEnabled("DepthOnly", false);
            EditorUtility.SetDirty(material); return material;
        }

        public static Report Audit() => AuditScene(SceneManager.GetActiveScene());
        public static Report Audit(Transform root)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            return AuditScene(root.gameObject.scene);
        }
        static Report AuditScene(Scene scene)
        {
            Require(scene, false);
            var group = FindGroup(scene);
            if (!group) throw new InvalidOperationException("No scene-owned environment mist to audit.");
            var report = new Report { scene = scene.path, texture = TexturePath, worldSpace = true, environmentParent = true,
                allEmitterCentersSupported = true, allRoomWispsContained = true, maximumAlpha = 0, skipped = Array.Empty<string>() };
            var errors = new List<string>(); var results = new List<EmitterResult>();
            Physics.SyncTransforms();
            foreach (var particles in group.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main; var shape = particles.shape; var renderer = particles.GetComponent<ParticleSystemRenderer>();
                bool room = particles.name.StartsWith("Sala — névoa baixa — ", StringComparison.Ordinal);
                bool environment = !particles.GetComponentInParent<Camera>() && !particles.GetComponentInParent<FirstPersonMotor>();
                var floorGuess = particles.transform.position - Vector3.up * (room ? .43f : scene.path == SchoolIntegrationPass.ScenePath ? .37f : .35f);
                bool supported = TryFloor(scene, floorGuess, floorGuess.y, out float floor);
                var size = new Vector3(main.startSizeX.constantMax, main.startSizeY.constantMax, main.startSizeZ.constantMax);
                bool contained = !room || RoomContains(scene, particles.name, particles.transform.position, shape.scale, size.x);
                results.Add(new EmitterResult { name = particles.name, position = particles.transform.position, floor = floor,
                    shapeSize = shape.scale, maximumBillboard = size, maximumParticles = main.maxParticles, alpha = main.startColor.color.a,
                    worldSpace = main.simulationSpace == ParticleSystemSimulationSpace.World, environmentParent = environment, supported = supported, containedInRoom = contained });
                report.worldSpace &= main.simulationSpace == ParticleSystemSimulationSpace.World;
                report.environmentParent &= environment; report.allEmitterCentersSupported &= supported; report.allRoomWispsContained &= contained;
                if (main.maxParticles > MaximumPerEmitter || main.maxParticles < 1) errors.Add("Uncapped emitter: " + particles.name);
                if (!renderer || !renderer.sharedMaterial) errors.Add("Missing environment particle material: " + particles.name);
                else if (report.material == null) report.material = AssetDatabase.GetAssetPath(renderer.sharedMaterial);
                else if (report.material != AssetDatabase.GetAssetPath(renderer.sharedMaterial)) errors.Add("Unexpected unshared mist material.");
                report.maximumAlpha = Mathf.Max(report.maximumAlpha, main.startColor.color.a);
                report.maximumLiveParticles += main.maxParticles;
                if (room) report.classroomEmitters++; else report.exteriorEmitters++;
            }
            report.emitters = results.Count; report.emitterResults = results.ToArray();
            report.colliders = group.GetComponentsInChildren<Collider>(true).Length; report.lights = group.GetComponentsInChildren<Light>(true).Length;
            report.noNewColliders = report.colliders == 0; report.noNewLights = report.lights == 0;
            var material = AssetDatabase.LoadAssetAtPath<Material>(report.material);
            report.cameraNearFade = material ? material.GetFloat("_CameraNearFadeDistance") : 0;
            report.cameraFarFade = material ? material.GetFloat("_CameraFarFadeDistance") : 0;
            report.nearCameraClear = material && material.GetFloat("_CameraFadingEnabled") > .5f && material.IsKeywordEnabled("_FADING_ON")
                && Mathf.Abs(report.cameraNearFade - NearFade) < .001f && Mathf.Abs(report.cameraFarFade - FarFade) < .001f
                && (material.GetVector("_CameraFadeParams") - new Vector4(NearFade, 1 / (FarFade - NearFade), 0, 0)).sqrMagnitude < .000001f;
            report.softParticlesEnabled = material && material.GetFloat("_SoftParticlesEnabled") > .5f && material.IsKeywordEnabled("_SOFTPARTICLES_ON")
                && material.GetTexture("_BaseMap") == AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath)
                && (material.GetVector("_SoftParticleFadeParams") - new Vector4(0, 1 / SoftFade, 0, 0)).sqrMagnitude < .000001f;
            var game = All<GameSession>(scene).Single();
            report.depthTextureRequested = game.player.view.playerCamera.GetUniversalAdditionalCameraData().requiresDepthOption == CameraOverrideOption.On;
            var dust = All<SchoolFlashlightDust>(scene); report.attachedDustCount = dust.Length;
            report.attachedDustDisabled = dust.All(d => !d.enabled && (!d.particles || !d.particles.main.playOnAwake
                && !d.particles.isPlaying && d.particles.particleCount == 0));
            report.fogStart = RenderSettings.fogStartDistance; report.fogEnd = RenderSettings.fogEndDistance; report.fogColor = RenderSettings.fogColor;
            bool school = scene.path == SchoolIntegrationPass.ScenePath;
            if (!RenderSettings.fog || RenderSettings.fogMode != FogMode.Linear || Mathf.Abs(report.fogStart - 14) > .001f
                || Mathf.Abs(report.fogEnd - (school ? 110 : 85)) > .001f) errors.Add("Unexpected distance-fog configuration.");
            if (school && report.classroomEmitters != 27) errors.Add("Not all 27 registered classrooms have restrained world wisps.");
            if (report.emitters == 0 || !report.worldSpace || !report.environmentParent || !report.noNewColliders || !report.noNewLights
                || !report.allEmitterCentersSupported || !report.allRoomWispsContained || !report.nearCameraClear || !report.softParticlesEnabled
                || !report.depthTextureRequested || !report.attachedDustDisabled || report.maximumAlpha > .13f)
                errors.Add("World placement, support, camera fade, alpha budget or attached-dust isolation failed.");
            report.protectedBefore = report.protectedAfter = ProtectedSignature(scene); report.protectedArchitectureAndStoryPreserved = true;
            report.errors = errors.ToArray(); report.passed = errors.Count == 0;
            report.notes = new[]
            {
                "Wisps use the existing CorridorMist texture and native URP particle shader; no download, new renderer feature, camera overlay or menu asset modification.",
                "Fixed scene parents, World simulation, depth-tested transparent billboards, soft geometry intersections and camera fade from 2 to 5 m. First 14 m of distance fog remain clear.",
                "Per-room emission averages about one wisp; 6-particle room caps and 8/10-particle circulation/arrival caps. Off-screen particle simulation pauses and catches up. No physics, shadows, lights or runtime controller.",
                "Billboards are atmospheric wisps, not ray-marched volumetric lighting. Native visual inspection and actual performance measurement remain the caller's responsibility."
            };
            return report;
        }

        static bool RoomContains(Scene scene, string emitterName, Vector3 position, Vector3 shape, float width)
        {
            string path = SchoolMapBuilder.Output + "/School-classroom-photo-report.json";
            var catalogue = JsonUtility.FromJson<SchoolClassroomPhotoPass.Report>(File.ReadAllText(path));
            string name = emitterName.Substring("Sala — névoa baixa — ".Length);
            var room = catalogue.rooms.Single(r => r.name + " — pavimento " + r.floor == name);
            var box = room.adaptedPdfBox;
            var nw = SchoolMapBuilder.Point(box[0], box[1]); var se = SchoolMapBuilder.Point(box[2], box[3]);
            float padding = width * .5f + .18f;
            return position.x - shape.x * .5f - padding >= nw.x + .10f && position.x + shape.x * .5f + padding <= se.x - .10f
                && position.z - shape.z * .5f - padding >= se.z + .10f && position.z + shape.z * .5f + padding <= nw.z - .10f;
        }
        static bool TryFloor(Scene scene, Vector3 p, float expected, out float y)
        {
            foreach (var hit in Physics.RaycastAll(new Vector3(p.x, expected + .45f, p.z), Vector3.down, 1.15f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                var body = hit.collider; var b = body.bounds; string n = body.name.ToLowerInvariant();
                bool floor = b.size.y < 1 && b.size.x > .5f && b.size.z > .5f
                    && (n.Contains("piso") || n.Contains("pavimento") || n.Contains("laje") || n.Contains("patamar") || n.Contains("plataforma")
                        || n.Contains("passeio") || n.Contains("terreno") || n.Contains("chão") || n.Contains("concreto"))
                    && !n.Contains("cobertura") && !n.Contains("forro");
                if (body.gameObject.scene == scene && body.enabled && body.gameObject.activeInHierarchy && floor
                    && hit.normal.y > .97f && Mathf.Abs(hit.point.y - expected) < .22f) { y = hit.point.y; return true; }
            }
            y = expected; return false;
        }
        static string ProtectedSignature(Scene scene)
        {
            var lines = new List<string>();
            foreach (var body in All<Collider>(scene))
                lines.Add("P|" + Hierarchy(body.transform) + "|" + body.GetType().Name + "|" + EditorJsonUtility.ToJson(body) + "|" + body.transform.localToWorldMatrix);
            foreach (var renderer in All<MeshRenderer>(scene))
            {
                var mesh = renderer.GetComponent<MeshFilter>();
                lines.Add("M|" + Hierarchy(renderer.transform) + "|" + renderer.enabled + "|" + renderer.transform.localToWorldMatrix
                    + "|" + (mesh ? AssetDatabase.GetAssetPath(mesh.sharedMesh) : "") + "|" + string.Join(",", renderer.sharedMaterials.Select(m => AssetDatabase.GetAssetPath(m))));
            }
            foreach (var script in All<MonoBehaviour>(scene).Where(s => s && !(s is SchoolFlashlightDust) && !(s is UniversalAdditionalCameraData)))
                lines.Add("S|" + Hierarchy(script.transform) + "|" + script.GetType().FullName + "|" + EditorJsonUtility.ToJson(script));
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines.OrderBy(v => v, StringComparer.Ordinal))))).Replace("-", "");
        }
        static string Hierarchy(Transform t) => t.parent ? Hierarchy(t.parent) + "/" + t.name : t.name;
        static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
        static Transform FindGroup(Scene scene) => All<Transform>(scene).SingleOrDefault(t => t.name == GroupName);
        static void Require(Scene scene, bool clean)
        {
            if (!scene.IsValid() || !scene.isLoaded || SceneManager.GetActiveScene() != scene
                || scene.path != SchoolIntegrationPass.ScenePath && scene.path != "Assets/RoadChapter/Scenes/RoadJourney.unity"
                || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || clean && scene.isDirty)
                throw new InvalidOperationException("Open a clean saved SchoolCEEP or RoadJourney scene outside Play/compilation before applying environment mist.");
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        static void Record(params UnityEngine.Object[] objects)
        {
            foreach (var item in objects) { EditorUtility.SetDirty(item); if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item); }
        }
    }
}
