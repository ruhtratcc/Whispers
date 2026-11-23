// Centraliza preferências de redução de efeitos e apresentação.

using System;
using UnityEngine;

namespace HorrorGame
{
    public static class PresentationSettings
    {
        public const float DefaultMusic = .35f;
        public static event Action Changed;
        static bool loaded, reduced;
        static float gamma, size, background;
        public static int Version { get; private set; }
        public static float Gamma { get { Load(); return gamma; } }
        public static float SubtitleSize { get { Load(); return size; } }
        public static float SubtitleBackground { get { Load(); return background; } }
        public static bool ReduceEffects { get { Load(); return reduced; } }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { loaded = false; Version = 0; Changed = null; }
        static float Read(string key, float fallback, float min, float max)
        {
            float value = PlayerPrefs.GetFloat("HorrorMenu." + key, fallback);
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        }
        static void Load() { if (!loaded) Reload(); }
        public static void Reload()
        {
            gamma = Read("Gamma", 1, .8f, 1.2f);
            size = Read("SubtitleSize", 1, 1, 1.5f);
            background = Read("SubtitleBackground", .55f, 0, .85f);
            reduced = PlayerPrefs.GetInt("HorrorMenu.ReduceEffects", 0) == 1;
            loaded = true; Version++; Changed?.Invoke();
        }
        static void Save(string key, float value, float fallback, float min, float max)
        {
            PlayerPrefs.SetFloat("HorrorMenu." + key, float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max));
            PlayerPrefs.Save(); Reload();
        }
        public static void SetGamma(float value) => Save("Gamma", value, 1, .8f, 1.2f);
        public static void SetSubtitleSize(float value) => Save("SubtitleSize", value, 1, 1, 1.5f);
        public static void SetSubtitleBackground(float value) => Save("SubtitleBackground", value, .55f, 0, .85f);
        public static void SetReduceEffects(bool value)
        { PlayerPrefs.SetInt("HorrorMenu.ReduceEffects", value ? 1 : 0); PlayerPrefs.Save(); Reload(); }
    }
}
