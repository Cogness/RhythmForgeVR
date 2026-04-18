using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    /// <summary>
    /// Phase D: derives Free-mode <c>bondStrength</c> from the stroke's 2D
    /// <see cref="ShapeProfile"/> + 3D <see cref="ShapeProfile3D"/>. Formula
    /// follows the unified-shape plan:
    /// <list type="bullet">
    ///   <item><c>rhythmW  ∝ angularity + thicknessVariance + (1 − planarity)</c></item>
    ///   <item><c>melodyW  ∝ elongation3D + (1 − circularity) + pathLength*</c></item>
    ///   <item><c>harmonyW ∝ circularity + closedness + depthSpan + planarity</c></item>
    /// </list>
    /// Each weight is clamped to <c>[0.15, 0.85]</c> before normalisation so no
    /// facet is ever completely silent in Free mode — the three facets are
    /// always audible; <see cref="MusicalShapeBehavior"/> only skips a facet
    /// whose weight is exactly zero (used by Solo modes).
    ///
    /// Solo modes bypass this helper entirely — <see cref="ShapeFacetMode.SoloRhythm"/> /
    /// <see cref="ShapeFacetMode.SoloMelody"/> / <see cref="ShapeFacetMode.SoloHarmony"/>
    /// pass an explicit one-hot vector
    /// into <see cref="UnifiedDerivationRequest.bondStrength"/> with
    /// <see cref="UnifiedDerivationRequest.freeMode"/> = <c>false</c>.
    /// </summary>
    public static class BondStrengthResolver
    {
        private const float MinWeight = 0.15f;
        private const float MaxWeight = 0.85f;

        /// <summary>
        /// Default Free-mode vector when neither profile is available.
        /// Equal thirds so the three facets play at balanced velocities.
        /// </summary>
        public static readonly Vector3 DefaultFree = new Vector3(1f / 3f, 1f / 3f, 1f / 3f);

        public static Vector3 Resolve(ShapeProfile profile, ShapeProfile3D profile3D)
        {
            // Pull the 2D signals (always populated).
            float angularity  = profile != null ? Mathf.Clamp01(profile.angularity)  : 0.5f;
            float circularity = profile != null ? Mathf.Clamp01(profile.circularity) : 0.5f;
            float closedness  = profile != null ? Mathf.Clamp01(profile.closedness)  : 0.5f;

            // Path length is in world metres; normalise relative to max-dim so a
            // long ribbony stroke pushes melody regardless of absolute scale.
            float pathLenNorm = 0.5f;
            if (profile != null && profile.worldMaxDimension > 0.0001f)
                pathLenNorm = Mathf.Clamp01(profile.pathLength / (profile.worldMaxDimension * 6f));

            // Pull the 3D signals (may be null on legacy pre-v7 draft paths).
            float thicknessVar = profile3D != null ? Mathf.Clamp01(profile3D.thicknessVariance) : 0.3f;
            float planarity    = profile3D != null ? Mathf.Clamp01(profile3D.planarity)         : 0.7f;
            float elongation3D = profile3D != null ? Mathf.Clamp01(profile3D.elongation3D)      : 0.5f;
            float depthSpan    = profile3D != null ? Mathf.Clamp01(profile3D.depthSpan)         : 0.25f;

            float rhythm  = angularity + thicknessVar + (1f - planarity);
            float melody  = elongation3D + (1f - circularity) + pathLenNorm;
            float harmony = circularity + closedness + depthSpan + planarity;

            // Soft-normalise to 0..1 range per component, then clamp floor/ceil
            // so no facet ever vanishes (would starve the ensemble) or dominates
            // absolutely (would mask the other two).
            rhythm  = Mathf.Clamp(rhythm  / 3f, MinWeight, MaxWeight);
            melody  = Mathf.Clamp(melody  / 3f, MinWeight, MaxWeight);
            harmony = Mathf.Clamp(harmony / 4f, MinWeight, MaxWeight);

            // Final L1 normalisation so the three weights sum to 1.
            float sum = rhythm + melody + harmony;
            if (sum < 0.0001f)
                return DefaultFree;

            return new Vector3(rhythm / sum, melody / sum, harmony / sum);
        }
    }
}
