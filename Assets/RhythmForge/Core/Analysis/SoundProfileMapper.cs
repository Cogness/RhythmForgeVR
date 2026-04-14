using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.PatternBehavior;

namespace RhythmForge.Core.Analysis
{
    public static class SoundProfileMapper
    {
        public static SoundProfile Derive(PatternType type, ShapeProfile sp)
        {
            return PatternBehaviorRegistry.Get(type).DeriveSoundProfile(sp);
        }

        internal static SoundProfile DeriveRhythm(ShapeProfile sp)
        {
            return SoundMappingProfiles.Get(PatternType.RhythmLoop).Evaluate(PatternType.RhythmLoop, sp);
        }

        internal static SoundProfile DeriveMelody(ShapeProfile sp)
        {
            return SoundMappingProfiles.Get(PatternType.MelodyLine).Evaluate(PatternType.MelodyLine, sp);
        }

        internal static SoundProfile DeriveHarmony(ShapeProfile sp)
        {
            return SoundMappingProfiles.Get(PatternType.HarmonyPad).Evaluate(PatternType.HarmonyPad, sp);
        }
    }
}
