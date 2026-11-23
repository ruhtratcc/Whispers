// Fornece as falas associadas ao uso do grampo.

using System;
using UnityEngine;

namespace HorrorGame
{
    [CreateAssetMenu(menuName = "Whispers/Lockpick dialogue")]
    public sealed class LockpickDialogue : ScriptableObject
    {
        public LockpickLine[] lines = Array.Empty<LockpickLine>();
        public LockpickLine Find(string trigger, int variation)
        {
            int count = 0;
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].trigger == trigger)
                    count++;
            if (count == 0)
                return null;
            int index = Math.Abs(variation % count);
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].trigger == trigger && index-- == 0)
                    return lines[i];
            return null;
        }
    }
}
