// Ferramenta de preparação ou verificação usada no Editor.

using System;
using System.IO;
using System.Linq;
using HorrorGame;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorGame.Editor
{
    public static class GameplayBuilder
    {
        const string ScenePath = GameplayAssets.Root + "Scenes/TestScene.unity";
        static int worldLayer, playerLayer, propsLayer, interactionLayer, enemyLayer;
        static Material concrete, wood, metal, paint;
        static SoundSet creak, impact, pickup, click, gasp, scare;
        static GameObject Empty(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go;
        }

        static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 size, Material material, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static void SavePrefab(GameObject go, string name)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, GameplayAssets.Root + "Prefabs/" + name + ".prefab", InteractionMode.AutomatedAction);
        }

        static AudioSource Loop(string name, Transform parent, AudioClip clip)
        {
            var go = Empty(name, parent, Vector3.zero);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.clip = clip;
            source.loop = true;
            source.volume = 0;
            return source;
        }

        public static FirstPersonMotor Player(Transform parent, InputActionAsset inputAsset, FootstepLibrary library)
        {
            var go = Empty("Player", parent, new Vector3(0, .05f, -8));
            go.tag = "Player";
            go.layer = playerLayer;
            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = .32f;
            controller.center = new Vector3(0, .9f, 0);
            controller.stepOffset = .3f;
            controller.slopeLimit = 45;
            controller.skinWidth = .035f;
            controller.minMoveDistance = 0;
            var input = go.AddComponent<InputReader>();
            input.asset = inputAsset;
            var motor = go.AddComponent<FirstPersonMotor>();
            motor.input = input;
            motor.collisionMask = ~(1 << playerLayer);
            var cameraObject = Empty("Player Camera", go.transform, new Vector3(0, 1.64f, 0));
            cameraObject.tag = "MainCamera";
            cameraObject.layer = playerLayer;
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = .04f;
            camera.farClipPlane = 80;
            camera.fieldOfView = 49;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.006f, .009f, .015f);
            camera.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var cameraRig = cameraObject.AddComponent<FirstPersonCamera>();
            cameraRig.motor = motor;
            cameraRig.playerCamera = camera;
            motor.view = cameraRig;
            var inventory = go.AddComponent<Inventory>();
            var grabber = go.AddComponent<PhysicsGrabber>();
            grabber.view = camera;
            grabber.motor = motor;
            grabber.obstructionMask = (1 << worldLayer) | (1 << propsLayer) | (1 << interactionLayer);
            var interaction = go.AddComponent<PlayerInteraction>();
            interaction.input = input;
            interaction.view = camera;
            interaction.inventory = inventory;
            interaction.motor = motor;
            interaction.grabber = grabber;
            interaction.mask = ~(1 << playerLayer);
            var steps = go.AddComponent<FootstepAudio>();
            steps.motor = motor;
            steps.library = library;
            var hand = Empty("Flashlight hand", go.transform, new Vector3(.16f, 1.48f, .25f));
            hand.layer = playerLayer;
            var lamp = hand.AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.intensity = 90;
            lamp.range = 17;
            lamp.spotAngle = 52;
            lamp.innerSpotAngle = 28;
            lamp.color = new Color(.88f, .93f, 1);
            lamp.shadows = LightShadows.Soft;
            var flashlight = go.AddComponent<FlashlightController>();
            flashlight.input = input;
            flashlight.view = camera;
            flashlight.lamp = lamp;
            flashlight.handRig = hand.transform;
            flashlight.click = click;
            SavePrefab(go, "Player");
            return motor;
        }

        static GameObject Door(Transform parent, string name, Vector3 position, bool locked, float width = 1.2f, float height = 2.1f)
        {
            var root = Empty(name, parent, position);
            root.layer = interactionLayer;
            var leaf = Cube("Door leaf", root.transform, new Vector3(width * .5f, height * .5f, 0), new Vector3(width, height, .1f), wood, interactionLayer);
            Cube("Handle", root.transform, new Vector3(width * .85f, height * .48f, -.08f), new Vector3(.11f, .045f, .07f), metal, interactionLayer);
            var script = root.AddComponent<InteractiveDoor>();
            script.hinge = root.transform;
            script.leafCollider = leaf.GetComponent<BoxCollider>();
            script.locked = locked;
            script.playerMask = 1 << playerLayer;
            script.creak = creak;
            script.lockedSound = click;
            script.persistentId = name;
            var obstacle = leaf.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = Vector3.zero;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            SavePrefab(root, name == "Locked school door" ? "Door" : "CabinetDoor");
            return root;
        }

        static GameObject Prop(Transform parent, string name, Vector3 position, PrimitiveType type, float mass)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = propsLayer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = type == PrimitiveType.Cylinder ? new Vector3(.7f, .45f, .7f) : new Vector3(.7f, .7f, .7f);
            go.GetComponent<Renderer>().sharedMaterial = type == PrimitiveType.Cylinder ? metal : wood;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearDamping = .3f;
            rb.angularDamping = .4f;
            var materialPath = GameplayAssets.Root + "Materials/PhysicsProp.asset";
            var physics = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(materialPath);
            if (!physics)
            {
                physics = new PhysicsMaterial("Physics prop")
                {
                    dynamicFriction = .45f,
                    staticFriction = .55f,
                    bounciness = .03f
                };
                AssetDatabase.CreateAsset(physics, materialPath);
            }

            go.GetComponent<Collider>().sharedMaterial = physics;
            go.AddComponent<PhysicsPickup>().displayName = name;
            go.AddComponent<ImpactAudio>().sound = impact;
            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            SavePrefab(go, type == PrimitiveType.Cylinder ? "PhysicsBarrel" : "PhysicsBox");
            return go;
        }

        static void Drawer(Transform parent)
        {
            var root = Empty("Drawer cabinet", parent, new Vector3(4, 0, 0));
            root.layer = interactionLayer;
            Cube("Back", root.transform, new Vector3(0, .65f, .25f), new Vector3(1.4f, 1.3f, .12f), wood, interactionLayer);
            Cube("Top", root.transform, new Vector3(0, 1.3f, 0), new Vector3(1.4f, .1f, .6f), wood, interactionLayer);
            Cube("Side L", root.transform, new Vector3(-.68f, .65f, 0), new Vector3(.08f, 1.3f, .6f), wood, interactionLayer);
            Cube("Side R", root.transform, new Vector3(.68f, .65f, 0), new Vector3(.08f, 1.3f, .6f), wood, interactionLayer);
            var drawer = Empty("Sliding drawer", root.transform, new Vector3(0, .85f, -.1f));
            Cube("Front", drawer.transform, new Vector3(0, 0, -.22f), new Vector3(1.25f, .45f, .06f), wood, interactionLayer);
            Cube("Tray", drawer.transform, new Vector3(0, -.2f, 0), new Vector3(1.25f, .06f, .48f), metal, interactionLayer);
            var script = root.AddComponent<InteractiveDrawer>();
            script.drawer = drawer.transform;
            script.sound = creak;
            script.persistentId = "demo-drawer";
            var cabinetObstacle = root.AddComponent<NavMeshObstacle>();
            cabinetObstacle.shape = NavMeshObstacleShape.Box; cabinetObstacle.center = new Vector3(0,.65f,0); cabinetObstacle.size = new Vector3(1.4f,1.3f,.65f); cabinetObstacle.carving = true;
            SavePrefab(root, "Drawer");
        }

        static HidingSpot Hide(Transform parent, string name, Vector3 position, bool underBed)
        {
            var root = Empty(name, parent, position);
            root.layer = interactionLayer;
            if (underBed)
            {
                Cube("Mattress block", root.transform, new Vector3(0, .85f, 0), new Vector3(1.8f, .2f, 2), paint, interactionLayer);
                foreach (float x in new[]
                {
                    -.8f,
                    .8f
                }

                )
                    foreach (float z in new[]
                    {
                        -.9f,
                        .9f
                    }

                    )
                        Cube("Leg", root.transform, new Vector3(x, .4f, z), new Vector3(.12f, .8f, .12f), metal, interactionLayer);
            }
            else
            {
                Cube("Back", root.transform, new Vector3(0, 1.1f, .5f), new Vector3(1.3f, 2.2f, .08f), wood, interactionLayer);
                Cube("Side left", root.transform, new Vector3(-.65f, 1.1f, 0), new Vector3(.08f, 2.2f, 1), wood, interactionLayer);
                Cube("Side right", root.transform, new Vector3(.65f, 1.1f, 0), new Vector3(.08f, 2.2f, 1), wood, interactionLayer);
                Cube("Top", root.transform, new Vector3(0, 2.2f, 0), new Vector3(1.3f, .08f, 1), wood, interactionLayer);
                Cube("Front left", root.transform, new Vector3(-.36f, 1.1f, -.5f), new Vector3(.55f, 2.2f, .06f), wood, interactionLayer);
                Cube("Front right", root.transform, new Vector3(.36f, 1.1f, -.5f), new Vector3(.55f, 2.2f, .06f), wood, interactionLayer);
            }

            var hide = root.AddComponent<HidingSpot>();
            hide.displayName = underBed ? "espaço sob a cama" : "armário";
            hide.enterSound = creak;
            hide.gaspSound = gasp;
            hide.cameraPose = Empty("Hidden camera pose", root.transform, new Vector3(0, underBed ? .42f : 1.55f, -.38f)).transform;
            hide.cameraPose.localRotation = Quaternion.Euler(0, 180, 0);
            hide.bodyPose = Empty("Hidden body pose", root.transform, Vector3.zero).transform;
            hide.exitPose = Empty("Safe exit", root.transform, new Vector3(0, .05f, -1.4f)).transform;
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box; obstacle.carving = true;
            obstacle.center = new Vector3(0, underBed ? .6f : 1.1f, 0);
            obstacle.size = underBed ? new Vector3(1.8f,1.2f,2) : new Vector3(1.4f,2.2f,1.1f);
            SavePrefab(root, underBed ? "HidingUnderBed" : "HidingCloset");
            return hide;
        }

        static CollectibleItem Item(Transform parent, string name, Vector3 position, CollectibleKind kind, string id, Vector3 scale)
        {
            var go = Cube(name, parent, position, scale, kind == CollectibleKind.Key ? metal : paint, interactionLayer);
            var item = go.AddComponent<CollectibleItem>();
            item.kind = kind;
            item.itemId = id;
            item.title = name;
            item.persistentId = "demo-" + id;
            item.pickupSound = pickup;
            if (kind == CollectibleKind.Note)
                item.noteText = "Se o sinal tocar depois da meia-noite, não abra a porta.\n\nA chave ficou na mesa. Eu ouvi alguém andando, mas não havia ninguém no corredor.";
            SavePrefab(go, kind.ToString());
            return item;
        }

        static void Enemy(Transform parent, FirstPersonMotor player)
        {
            var go = Empty("Placeholder enemy", parent, new Vector3(8, 0, 10));
            go.layer = enemyLayer;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Capsule model — replace later";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = Vector3.up;
            body.layer = enemyLayer;
            body.GetComponent<Renderer>().sharedMaterial = paint;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = .35f;
            agent.height = 1.9f;
            agent.baseOffset = 0;
            agent.stoppingDistance = .65f;
            agent.acceleration = 7;
            agent.angularSpeed = 140;
            var brain = go.AddComponent<EnemyBrain>();
            brain.eyes = Empty("Enemy eyes", go.transform, new Vector3(0, 1.65f, 0)).transform;
            brain.visionMask = ~(1 << enemyLayer);
            brain.footsteps = GameplayAssets.Steps().entries[1].steps;
            SavePrefab(go, "Enemy");
            brain.player = player;
            var routes = Empty("Patrol markers", parent, Vector3.zero).transform;
            brain.patrolPoints = new[]
            {
                new Vector3(9, 0, 10),
                new Vector3(6, 0, 12),
                new Vector3(-6, 0, 12),
                new Vector3(-9, 0, 8)
            }.Select((p, i) => Empty("Patrol " + i, routes, p).transform).ToArray();
        }

        [MenuItem("Tools/Whispers/Create Test Scene")]
        public static void BuildMenu() => Build();
        public static string Build()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("TestScene already exists. This command does not overwrite authored scenes.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the current scene first.");
            GameplayAssets.Folders();
            worldLayer = GameplayAssets.Layer("World");
            playerLayer = GameplayAssets.Layer("Player");
            propsLayer = GameplayAssets.Layer("PhysicsProps");
            interactionLayer = GameplayAssets.Layer("Interactable");
            enemyLayer = GameplayAssets.Layer("Enemy");
            concrete = GameplayAssets.Material("Test concrete", new Color(.23f, .25f, .28f));
            wood = GameplayAssets.Material("Test wood", new Color(.21f, .14f, .095f));
            metal = GameplayAssets.Material("Test metal", new Color(.22f, .26f, .29f), .6f, .35f);
            paint = GameplayAssets.Material("Test paint", new Color(.35f, .39f, .4f));
            creak = GameplayAssets.Sound("Door creak", .7f, 140, 1);
            impact = GameplayAssets.Sound("Physics impact", .3f, 85, 0);
            pickup = GameplayAssets.Sound("Pickup", .15f, 420, 0);
            click = GameplayAssets.Sound("Flashlight click", .085f, 380, 0);
            gasp = GameplayAssets.Sound("Breathing gasp", .8f, 60, 2);
            scare = GameplayAssets.Sound("Scare sting", 1, 350, 1);
            var library = GameplayAssets.Steps();
            var inputAsset = GameplayAssets.Input();
            var mixer = GameplayAssets.Mixer();
            var calm = GameplayAssets.Sound("Calm drone", 8, 55, 4, 1);
            var tense = GameplayAssets.Sound("Tension drone", 8, 83, 4, 1);
            var whispers = GameplayAssets.Sound("Distant whisper placeholder", 1.8f, 70, 2);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.06f, .072f, .09f);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.017f, .025f, .035f);
            RenderSettings.fogDensity = .018f;
            var systems = Empty("Gameplay systems — drag this prefab into your map", null, Vector3.zero);
            var session = systems.AddComponent<GameSession>();
            session.scenes = GameplayAssets.Scenes();
            session.player = Player(systems.transform, inputAsset, library);
            session.input = session.player.GetComponent<InputReader>();
            session.interaction = session.player.GetComponent<PlayerInteraction>();
            var manager = Empty("AudioManager", systems.transform, Vector3.zero).AddComponent<AudioManager>();
            manager.mixer = mixer;
            manager.musicGroup = mixer.FindMatchingGroups("Music").First();
            manager.sfxGroup = mixer.FindMatchingGroups("SFX").First();
            manager.ambienceGroup = mixer.FindMatchingGroups("Ambience").First();
            manager.voiceGroup = mixer.FindMatchingGroups("Voice").First();
            manager.calmMusic = Loop("Calm music", manager.transform, calm.clips[0]);
            manager.tenseMusic = Loop("Tension music", manager.transform, tense.clips[0]);
            manager.ambience = Loop("Ambient bed", manager.transform, null);
            manager.breathing = Loop("Exhausted breathing", manager.transform, null);
            var volume = Empty("Gameplay global Volume", systems.transform, Vector3.zero).AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 5;
            volume.sharedProfile = GameplayAssets.Profile();
            var atmosphere = systems.AddComponent<HorrorAtmosphere>();
            atmosphere.player = session.player;
            atmosphere.flashlight = session.player.GetComponent<FlashlightController>();
            atmosphere.volume = volume;
            session.atmosphere = atmosphere;
            var save = systems.AddComponent<SaveSystem>();
            save.player = session.player;
            save.inventory = session.interaction.inventory;
            save.flashlight = atmosphere.flashlight;
            save.atmosphere = atmosphere;
            session.save = save;
            session.deathSound = scare;
            session.hud = GameplayUI.Create(session, systems.transform);
            SavePrefab(systems, "GameplayRoot");
            var blockout = Empty("Test blockout — replace with your map", null, Vector3.zero);
            var surface = blockout.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = 1 << worldLayer;
            surface.overrideVoxelSize = true;
            surface.voxelSize = .12f;
            Cube("Base floor", blockout.transform, new Vector3(0, -.2f, 0), new Vector3(30, .4f, 30), concrete, worldLayer).AddComponent<SurfaceType>().surface = GroundSurface.Concrete;
            var colors = new[]
            {
                new Color(.25f, .16f, .1f),
                new Color(.26f, .28f, .3f),
                new Color(.22f, .17f, .12f),
                new Color(.12f, .19f, .11f),
                new Color(.2f, .25f, .29f),
                new Color(.35f, .37f, .37f)
            };
            for (int i = 0; i < 6; i++)
            {
                var mat = GameplayAssets.Material("Surface " + (GroundSurface)i, colors[i], i == 4 ? .65f : 0);
                var pad = Cube("Surface " + (GroundSurface)i, blockout.transform, new Vector3((i % 3 - 1) * 4, .006f, -7 + (i / 3) * 4), new Vector3(3.8f, .012f, 3.8f), mat, worldLayer);
                pad.AddComponent<SurfaceType>().surface = (GroundSurface)i;
            }

            Cube("Back wall", blockout.transform, new Vector3(0, 1.5f, 15), new Vector3(30, 3, .3f), concrete, worldLayer);
            Cube("Front wall", blockout.transform, new Vector3(0, 1.5f, -15), new Vector3(30, 3, .3f), concrete, worldLayer);
            Cube("Left wall", blockout.transform, new Vector3(-15, 1.5f, 0), new Vector3(.3f, 3, 30), concrete, worldLayer);
            Cube("Right wall", blockout.transform, new Vector3(15, 1.5f, 0), new Vector3(.3f, 3, 30), concrete, worldLayer);
            Cube("Door wall left", blockout.transform, new Vector3(-7.75f, 1.5f, 5), new Vector3(14.5f, 3, .25f), concrete, worldLayer);
            Cube("Door wall right", blockout.transform, new Vector3(8.2f, 1.5f, 5), new Vector3(13.6f, 3, .25f), concrete, worldLayer);
            Cube("Door lintel", blockout.transform, new Vector3(.6f, 2.6f, 5), new Vector3(1.2f, .8f, .25f), concrete, worldLayer);
            for (int i = 0; i < 4; i++)
                Cube("Step " + i, blockout.transform, new Vector3(7, (i + 1) * .09f, -3 + i * .55f), new Vector3(2, (i + 1) * .18f, .55f), concrete, worldLayer);
            var ramp = Cube("Slope test", blockout.transform, new Vector3(-10, .6f, -3), new Vector3(2, .15f, 4), concrete, worldLayer);
            ramp.transform.rotation = Quaternion.Euler(-17, 0, 0);
            Cube("Crouch ceiling", blockout.transform, new Vector3(-4, 1.25f, -2), new Vector3(2, .2f, 2.5f), concrete, worldLayer);
            var props = Empty("Interactive test objects", null, Vector3.zero);
            Door(props.transform, "Locked school door", new Vector3(0, 0, 5), true);
            Door(props.transform, "Cabinet door", new Vector3(6, 0, 2), false, .75f, 1.3f);
            Drawer(props.transform);
            Prop(props.transform, "Caixa de madeira", new Vector3(-2, .5f, -4), PrimitiveType.Cube, 6);
            Prop(props.transform, "Barril", new Vector3(2, .5f, -2), PrimitiveType.Cylinder, 12);
            Item(props.transform, "Chave da escola", new Vector3(-1.5f, .85f, 0), CollectibleKind.Key, "school_key", new Vector3(.18f, .035f, .06f));
            Item(props.transform, "Bilhete", new Vector3(.1f, .85f, 0), CollectibleKind.Note, "warning_note", new Vector3(.28f, .02f, .38f));
            Item(props.transform, "Bateria", new Vector3(1.5f, .85f, 0), CollectibleKind.Battery, "spare_battery", new Vector3(.12f, .16f, .12f));
            Cube("Item table", blockout.transform, new Vector3(0, .4f, 0), new Vector3(4, .8f, 1), wood, worldLayer);
            Hide(props.transform, "Closet hiding spot", new Vector3(-7, 0, 1), false);
            Hide(props.transform, "Bed hiding spot", new Vector3(7, 0, -8), true);
            var fillGo = Empty("Gameplay visibility fill", null, Vector3.zero);
            fillGo.transform.rotation = Quaternion.Euler(48, -35, 0);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(.72f, .8f, 1);
            fill.intensity = .35f;
            fill.shadows = LightShadows.None;
            var lightGo = Empty("Dim test light", null, new Vector3(0, 2.8f, -1));
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = 3;
            light.range = 9;
            light.color = new Color(.65f, .75f, .9f);
            light.shadows = LightShadows.Soft;
            lightGo.AddComponent<FlickerLight>();
            var distant = Empty("Distant creaks and whispers", null, new Vector3(-8, 1.5f, 12));
            var random = distant.AddComponent<RandomScareAudio>();
            random.sounds = whispers;
            random.channel = AudioChannel.Voice;
            var drip = Empty("Spatial dripping placeholder", null, new Vector3(10, 1, 0));
            var source = drip.AddComponent<AudioSource>();
            source.minDistance = 1;
            source.maxDistance = 15;
            drip.AddComponent<SpatialAmbience>();
            var events = Empty("Test event zones", null, Vector3.zero);
            var checkpoint = Empty("Checkpoint", events.transform, new Vector3(.6f, 1.2f, 7));
            var checkpointCollider = checkpoint.AddComponent<BoxCollider>();
            checkpointCollider.isTrigger = true;
            checkpointCollider.size = new Vector3(3, 2.4f, 1);
            var checkpointScript = checkpoint.AddComponent<Checkpoint>();
            checkpointScript.respawnPose = Empty("Checkpoint respawn", events.transform, new Vector3(.6f, .05f, 7.5f)).transform;
            var objectiveZone = Empty("Objective and subtitle zone", events.transform, new Vector3(0, 1, 1.8f));
            var objectiveCollider = objectiveZone.AddComponent<BoxCollider>();
            objectiveCollider.isTrigger = true;
            objectiveCollider.size = new Vector3(5, 2, 1);
            var trigger = objectiveZone.AddComponent<EventZone>();
            UnityEventTools.AddStringPersistentListener(trigger.onEnter, session.hud.ShowObjective, "Use a chave na porta. O inimigo escuta passos e impactos.");
            UnityEventTools.AddStringPersistentListener(trigger.onEnter, session.hud.Say, "Ouviu isso?");
            var scareZone = Empty("Jumpscare test", events.transform, new Vector3(.6f, 1, 10));
            var scareCollider = scareZone.AddComponent<BoxCollider>();
            scareCollider.isTrigger = true;
            scareCollider.size = new Vector3(2, 2, 1);
            var jump = scareZone.AddComponent<Jumpscare>();
            jump.sound = scare;
            UnityEventTools.AddPersistentListener(jump.onScare, lightGo.GetComponent<FlickerLight>().BurnOut);
            EditorSceneManager.SaveScene(scene);
            surface.BuildNavMesh();
            if (!surface.navMeshData || NavMesh.CalculateTriangulation().vertices.Length == 0)
                throw new InvalidOperationException("Test NavMesh bake failed.");
            string navPath = GameplayAssets.Root + "Data/TestNavMesh.asset";
            AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            Enemy(props.transform, session.player);
            GameplayAssets.AssignIds();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == ScenePath))
                list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Selection.activeGameObject = systems;
            return "TestScene, GameplayRoot, Player, interaction prefabs, input, mixer, placeholder sounds and NavMesh created.";
        }
    }
}

