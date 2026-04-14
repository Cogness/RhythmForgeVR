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
    }
}
