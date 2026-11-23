// Controla os ratos, seus trajetos e sons.

using System;
using UnityEngine;

namespace HorrorGame
{
    public sealed class RatScurry : MonoBehaviour
    {
        [Serializable]
        public sealed class Route
        {
            public Vector3 start, corner, end;
        }

        public GameSession game;
        public GameObject model;
        public AudioClip[] paws = Array.Empty<AudioClip>(), squeaks = Array.Empty<AudioClip>();
        public Route[] routes = Array.Empty<Route>();
        public float speed = 3.4f, reach = 16;
        public bool Running => running;

        GameObject rat;
        AudioSource feet, voice;
        Route route;
        bool running;
        float distance;
        int previous = -1;
        public bool TryRun()
        {
            if (running || !game || !game.CanControl || !model || routes.Length == 0)
                return false;
            Vector3 player = game.player.transform.position;
            int pick = -1;
            float best = reach;
            for (int i = 0; i < routes.Length; i++)
            {
                float d = Vector3.Distance(player, routes[i].corner);
                if (i != previous && d > 2 && d < best && Mathf.Abs(player.y - routes[i].start.y) < 1)
                {
                    best = d;
                    pick = i;
                }
            }

            if (pick < 0)
                return false;
            if (!rat)
            {
                rat = Instantiate(model);
                rat.name = "Rato — corrida pelo canto";
                // Only the moving sound is perceived; turning the camera must never reveal the model.
                foreach (var renderer in rat.GetComponentsInChildren<Renderer>(true))
                    renderer.forceRenderingOff = true;
                feet = rat.AddComponent<AudioSource>();
                feet.playOnAwake = false;
                feet.spatialBlend = 1;
                feet.loop = true;
                feet.rolloffMode = AudioRolloffMode.Logarithmic;
                feet.minDistance = .7f;
                feet.maxDistance = 14;
                feet.dopplerLevel = 0;
                voice = rat.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1;
                voice.minDistance = .7f;
                voice.maxDistance = 13;
                voice.dopplerLevel = 0;
                AudioManager.Instance?.Route(feet, AudioChannel.Ambience);
                AudioManager.Instance?.Route(voice, AudioChannel.Ambience);
            }

            previous = pick;
            route = routes[pick];
            distance = 0;
            running = true;
            rat.SetActive(true);
            rat.transform.position = route.start;
            var gait = rat.GetComponentInChildren<Animation>();
            if (gait)
            {
                gait.wrapMode = WrapMode.Loop;
                gait.Play();
                foreach (AnimationState clip in gait)
                    clip.speed = 2.2f;
            }

            if (paws.Length > 0)
            {
                feet.clip = paws[UnityEngine.Random.Range(0, paws.Length)];
                feet.volume = .28f;
                feet.pitch = UnityEngine.Random.Range(.94f, 1.08f);
                feet.Play();
            }

            if (squeaks.Length > 0 && UnityEngine.Random.value < .45f)
            {
                voice.clip = squeaks[UnityEngine.Random.Range(0, squeaks.Length)];
                voice.volume = .16f;
                voice.Play();
            }

            return true;
        }

        void Update()
        {
            if (!running || !game)
                return;
            if (!game.CanControl)
            {
                if (feet.isPlaying)
                    feet.Pause();
                return;
            }

            if (feet.clip && !feet.isPlaying)
                feet.UnPause();
            distance += speed * Time.deltaTime;
            float first = Vector3.Distance(route.start, route.corner), second = Vector3.Distance(route.corner, route.end);
            Vector3 next = distance < first ? Vector3.Lerp(route.start, route.corner, distance / Mathf.Max(.01f, first)) : Vector3.Lerp(route.corner, route.end, (distance - first) / Mathf.Max(.01f, second));
            Vector3 direction = next - rat.transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > .00001f)
                rat.transform.rotation = Quaternion.LookRotation(direction);
            rat.transform.position = next;
            if (distance >= first + second)
                StopRun();
        }

        void StopRun()
        {
            running = false;
            if (feet)
                feet.Stop();
            if (voice)
                voice.Stop();
            if (rat)
                rat.SetActive(false);
        }

        void OnDisable() => StopRun();
        void OnDestroy()
        {
            if (rat)
                Destroy(rat);
        }
    }
}
