using UnityEngine;

namespace RhythmForge.Core.Data
{
    public static class TypeColors
    {
        public static Color RhythmLoop => VisualGrammarProfiles.GetTypeColor(PatternType.RhythmLoop);
        public static Color MelodyLine => VisualGrammarProfiles.GetTypeColor(PatternType.MelodyLine);
        public static Color HarmonyPad => VisualGrammarProfiles.GetTypeColor(PatternType.HarmonyPad);

        public static Color GetColor(PatternType type)
        {
            switch (type)
            {
                case PatternType.RhythmLoop: return RhythmLoop;
                case PatternType.MelodyLine: return MelodyLine;
                case PatternType.HarmonyPad: return HarmonyPad;
                default: return Color.white;
            }
        }

        /// <summary>
        /// Phase E: blend the three facet colors by <paramref name="bondStrength"/>
        /// so a shape's stroke color previews its audible mix before playback.
        /// Rhythm-heavy shapes look warm, melody-heavy cyan, harmony-heavy purple.
        /// The weights are assumed L1-normalised (as produced by
        /// <see cref="RhythmForge.Core.Sequencing.BondStrengthResolver"/>); a
        /// guard renormalises if they aren't, and an all-zero input falls back
        /// to <see cref="Color.white"/>.
        /// </summary>
        public static Color Blend(Vector3 bondStrength)
        {
            float r = Mathf.Max(0f, bondStrength.x);
            float g = Mathf.Max(0f, bondStrength.y);
            float b = Mathf.Max(0f, bondStrength.z);
            float sum = r + g + b;
            if (sum < 0.0001f)
                return Color.white;
            r /= sum; g /= sum; b /= sum;

            Color cr = RhythmLoop;
            Color cm = MelodyLine;
            Color ch = HarmonyPad;
            return new Color(
                cr.r * r + cm.r * g + ch.r * b,
                cr.g * r + cm.g * g + ch.g * b,
                cr.b * r + cm.b * g + ch.b * b,
                cr.a * r + cm.a * g + ch.a * b);
        }
    }
}
