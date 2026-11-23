// Ferramenta de preparação da cena e da apresentação do menu.

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SchoolMenu.Editor
{
    public static class CorridorMistPass
    {
        const string Root = "Assets/HorrorMenu";
        static Texture2D MistTexture()
        {
            string path = Root + "/Art/CorridorMist.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture) return texture;
            const int size = 128;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Soft uneven mist";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size, v = (y + .5f) / size;
                float dx = (u - .5f) * 2, dy = (v - .5f) * 2;
                float edge = Mathf.Pow(Mathf.Clamp01(1 - dx * dx - dy * dy), 2.2f);
                float wisps = Mathf.PerlinNoise(u * 4.2f + 8.1f, v * 3.7f + 2.4f) * .7f
                            + Mathf.PerlinNoise(u * 9.1f + 3.2f, v * 7.3f + 4.8f) * .3f;
                float alpha = edge * Mathf.SmoothStep(.04f, .9f, wisps);
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        public static string Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != Root + "/Scenes/MainMenu.unity" || EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Open MainMenu outside Play Mode.");
            var environment = GameObject.Find("School corridor").transform;
            var mist = environment.Find("Drifting corridor mist");
            if (!mist)
            {
                mist = new GameObject("Drifting corridor mist", typeof(ParticleSystem)).transform;
                mist.SetParent(environment, false);
            }
            mist.localPosition = new Vector3(0, .85f, 11.5f);
            var particles = mist.GetComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed = false;
            particles.randomSeed = 271;
            var main = particles.main;
            main.duration = 40;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(32, 44);
            main.startSpeed = 0;
            main.maxParticles = 28;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(2.6f, 4.6f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(.65f, 1.3f);
            main.startSizeZ = 1;
            main.startRotation = new ParticleSystem.MinMaxCurve(-.08f, .08f);
            main.startColor = new Color(.65f, .69f, .65f, .18f);
            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = .58f;
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(3.5f, .85f, 17);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-.035f, .035f);
            velocity.y = new ParticleSystem.MinMaxCurve(.001f, .009f);
            velocity.z = new ParticleSystem.MinMaxCurve(-.018f, .018f);
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = .055f;
            noise.frequency = .18f;
            noise.scrollSpeed = .035f;
            noise.damping = true;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.85f, .22f),
                        new GradientAlphaKey(.8f, .7f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;

            string materialPath = Root + "/Art/CorridorMist.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.name = "Corridor mist";
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", MistTexture());
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cull", 0);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SoftParticlesEnabled", 1);
            material.SetFloat("_SoftParticlesNearFadeDistance", 0);
            material.SetFloat("_SoftParticlesFarFadeDistance", .8f);
            material.SetVector("_SoftParticleFadeParams", new Vector4(0, 1.25f, 0, 0));
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_SOFTPARTICLES_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = 3000;
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("DepthOnly", false);
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // Keep the distance haze restrained; the moving wisps provide the visible mist.
            RenderSettings.fogDensity = .042f;
            RenderSettings.fogColor = new Color(.036f, .043f, .040f);
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(particles);
            EditorUtility.SetDirty(renderer);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "Subtle drifting wisps saved in the corridor.";
        }
    }
}
