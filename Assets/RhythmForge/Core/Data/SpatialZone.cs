using System;
using UnityEngine;

namespace RhythmForge.Core.Data
{
    [Serializable]
    public class SpatialZone
    {
        public string id;
        public string targetRole;
        public Vector3 center;
        public float radius = 0.8f;
        public float reverbBias;
        public float delayBias;
        public float gainBias = 1f;

        public bool MatchesTarget(PatternType type)
        {
            switch (type)
            {
                case PatternType.RhythmLoop:
                    return string.Equals(targetRole, "Rhythm", StringComparison.OrdinalIgnoreCase);
                case PatternType.MelodyLine:
                    return string.Equals(targetRole, "Melody", StringComparison.OrdinalIgnoreCase);
                case PatternType.HarmonyPad:
                    return string.Equals(targetRole, "Harmony", StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }
    }
}
