// Distribui eventos de ruído do mundo.

using System;
using UnityEngine;

namespace HorrorGame
{
    public enum NoiseKind
    {
        Footstep,
        Landing,
        Impact,
        Door,
        Breath,
        Scare
    }

    public readonly struct NoiseEvent
    {
        public readonly Vector3 position;
        public readonly float radius;
        public readonly GameObject source;
        public readonly NoiseKind kind;
        public NoiseEvent(Vector3 p, float r, GameObject s, NoiseKind k)
        {
            position = p;
            radius = r;
            source = s;
            kind = k;
        }
    }

    public static class NoiseSystem
    {
        public static event Action<NoiseEvent> Emitted;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Emitted = null;
        public static void Emit(Vector3 position, float radius, GameObject source, NoiseKind kind)
        {
            if (radius > 0)
                Emitted?.Invoke(new NoiseEvent(position, radius, source, kind));
        }
    }
}
