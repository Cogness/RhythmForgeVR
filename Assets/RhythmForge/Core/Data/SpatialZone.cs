using System;
using UnityEngine;

namespace RhythmForge.Core.Data
{
    [Serializable]
    public class SpatialZone
    {
        public string id;
        // Schema note: Plan #2 §3.2 specified PatternType targetType; we use string
        // targetRole for forward-compat with future role names like "Ornament" and
        // "Pad-Far" that don't map cleanly to PatternType.
        public string targetRole;
        public Vector3 center;
        public float radius = 0.8f;
        public float reverbBias;
        public float delayBias;
        public float gainBias = 1f;
        public float depthZThreshold;

        public bool MatchesTarget(PatternType type, Vector3? localPosition = null, ShapeFacetMode? facetMode = null)
        {
            if (localPosition.HasValue && depthZThreshold > 0f && localPosition.Value.z < depthZThreshold)
                return false;

            // Free mode: a single MusicalShape drives all three facets, so
            // the instance is relevant to every zone regardless of targetRole.
            if (facetMode == ShapeFacetMode.Free)
                return true;

            switch (type)
            {
                case PatternType.RhythmLoop:
                    return string.Equals(targetRole, "Rhythm", StringComparison.OrdinalIgnoreCase);
                case PatternType.MelodyLine:
                    return string.Equals(targetRole, "Melody", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(targetRole, "Ornament", StringComparison.OrdinalIgnoreCase);
                case PatternType.HarmonyPad:
                    return string.Equals(targetRole, "Harmony", StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }
    }
}
