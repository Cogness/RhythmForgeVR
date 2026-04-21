using UnityEngine;

namespace RhythmForge.Core.Data
{
    public static class TypeColors
    {
        public static Color Percussion => VisualGrammarProfiles.GetTypeColor(PatternType.Percussion);
        public static Color Melody => VisualGrammarProfiles.GetTypeColor(PatternType.Melody);
        public static Color Harmony => VisualGrammarProfiles.GetTypeColor(PatternType.Harmony);
        public static Color Bass => VisualGrammarProfiles.GetTypeColor(PatternType.Bass);
        public static Color Groove => VisualGrammarProfiles.GetTypeColor(PatternType.Groove);

        public static Color RhythmLoop => Percussion;
        public static Color MelodyLine => Melody;
        public static Color HarmonyPad => Harmony;

        public static Color GetColor(PatternType type)
        {
            switch (PatternTypeCompatibility.Canonicalize(type))
            {
                case PatternType.Percussion: return Percussion;
                case PatternType.Melody: return Melody;
                case PatternType.Harmony: return Harmony;
                case PatternType.Bass: return Bass;
                case PatternType.Groove: return Groove;
                default: return Color.white;
            }
        }
    }
}
