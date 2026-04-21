namespace RhythmForge.Core.Data
{
    public static class PatternTypeCompatibility
    {
        public static PatternType Canonicalize(PatternType type)
        {
            switch (type)
            {
                case PatternType.RhythmLoop:
                    return PatternType.Percussion;
                case PatternType.MelodyLine:
                    return PatternType.Melody;
                case PatternType.HarmonyPad:
                    return PatternType.Harmony;
                default:
                    return type;
            }
        }

        public static bool IsPercussion(PatternType type)
        {
            return Canonicalize(type) == PatternType.Percussion;
        }

        public static bool IsMelodyFamily(PatternType type)
        {
            switch (Canonicalize(type))
            {
                case PatternType.Melody:
                case PatternType.Bass:
                case PatternType.Groove:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsHarmony(PatternType type)
        {
            return Canonicalize(type) == PatternType.Harmony;
        }

        public static string GetDisplayName(PatternType type)
        {
            switch (Canonicalize(type))
            {
                case PatternType.Percussion: return "Percussion";
                case PatternType.Melody: return "Melody";
                case PatternType.Harmony: return "Harmony";
                case PatternType.Bass: return "Bass";
                case PatternType.Groove: return "Groove";
                default: return type.ToString();
            }
        }
    }
}
