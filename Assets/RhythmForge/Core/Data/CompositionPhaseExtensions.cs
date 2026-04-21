namespace RhythmForge.Core.Data
{
    public static class CompositionPhaseExtensions
    {
        private static readonly CompositionPhase[] OrderedPhases =
        {
            CompositionPhase.Harmony,
            CompositionPhase.Melody,
            CompositionPhase.Groove,
            CompositionPhase.Bass,
            CompositionPhase.Percussion
        };

        public static CompositionPhase[] All => OrderedPhases;

        public static PatternType ToPatternType(this CompositionPhase phase)
        {
            switch (phase)
            {
                case CompositionPhase.Harmony:
                    return PatternType.Harmony;
                case CompositionPhase.Melody:
                    return PatternType.Melody;
                case CompositionPhase.Groove:
                    return PatternType.Groove;
                case CompositionPhase.Bass:
                    return PatternType.Bass;
                case CompositionPhase.Percussion:
                default:
                    return PatternType.Percussion;
            }
        }

        public static CompositionPhase ToCompositionPhase(this PatternType type)
        {
            switch (PatternTypeCompatibility.Canonicalize(type))
            {
                case PatternType.Harmony:
                    return CompositionPhase.Harmony;
                case PatternType.Melody:
                    return CompositionPhase.Melody;
                case PatternType.Groove:
                    return CompositionPhase.Groove;
                case PatternType.Bass:
                    return CompositionPhase.Bass;
                case PatternType.Percussion:
                default:
                    return CompositionPhase.Percussion;
            }
        }
    }
}
