using UnityEngine;

namespace RhythmForge.Core.Sequencing
{
    /// <summary>
    /// Phase E: per-facet velocity scaling driven by <see cref="Data.MusicalShape.bondStrength"/>.
    /// Applied at schedule-time in <see cref="PatternBehavior.Behaviors.MusicalShapeBehavior"/>
    /// (NOT baked into the derived sequences) so bondStrength stays a live
    /// mixer knob — a future UI slider re-triggers nothing.
    ///
    /// Formula: <c>final = Clamp01(velocity * (0.4 + 0.6 * weight))</c>
    /// <list type="bullet">
    ///   <item>weight = 1.0 → ×1.00  (legacy one-hot dominant — bit-identical to pre-Phase-E)</item>
    ///   <item>weight = 0.85 (Free max) → ×0.91</item>
    ///   <item>weight = 0.33 (equal thirds) → ×0.60</item>
    ///   <item>weight = 0.15 (Free floor) → ×0.49</item>
    ///   <item>weight = 0.00 (Solo silent facet) → ×0.40 — but the caller skips via the
    ///         <c>bondStrength.X &gt; 0f</c> gate in <c>MusicalShapeBehavior</c>, so this
    ///         multiplier is never actually applied on Solo-silenced facets.</item>
    /// </list>
    /// Three functions instead of one so a future tuning pass can give harmony
    /// a gentler taper (it starts at <c>0.38f</c> and has the longest post-chain
    /// attenuation) without touching the rhythm/melody call sites.
    /// </summary>
    public static class BondStrengthVelocity
    {
        private const float Floor = 0.4f;
        private const float Span  = 0.6f;

        public static float ScaleRhythm(float velocity, float weight)
        {
            return Mathf.Clamp01(velocity * (Floor + Span * weight));
        }

        public static float ScaleMelody(float velocity, float weight)
        {
            return Mathf.Clamp01(velocity * (Floor + Span * weight));
        }

        public static float ScaleHarmony(float baseVelocity, float weight)
        {
            return Mathf.Clamp01(baseVelocity * (Floor + Span * weight));
        }
    }
}
