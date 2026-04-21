using System.Collections.Generic;
using RhythmForge.Core;

namespace RhythmForge.Core.Data
{
    public static class GuidedDefaults
    {
        public const string Key = "G major";
        public const float Tempo = 100f;
        public const int Bars = 8;
        public const string ActiveGenreId = "electronic";

        public static Composition Create()
        {
            return new Composition
            {
                id = MathUtils.CreateId("composition"),
                tempo = Tempo,
                key = Key,
                bars = Bars,
                progression = CreateDefaultProgression(),
                groove = null,
                phasePatternIds = new List<CompositionPhasePatternRef>(),
                currentPhase = CompositionPhase.Harmony
            };
        }

        public static ChordProgression CreateDefaultProgression()
        {
            return new ChordProgression
            {
                bars = Bars,
                chords = new List<ChordSlot>
                {
                    CreateSlot(0, 67, "major"),
                    CreateSlot(1, 64, "minor"),
                    CreateSlot(2, 60, "major"),
                    CreateSlot(3, 62, "major"),
                    CreateSlot(4, 67, "major"),
                    CreateSlot(5, 64, "minor"),
                    CreateSlot(6, 60, "major"),
                    CreateSlot(7, 62, "major")
                }
            };
        }

        private static ChordSlot CreateSlot(int barIndex, int rootMidi, string flavor)
        {
            return new ChordSlot
            {
                barIndex = barIndex,
                rootMidi = rootMidi,
                flavor = flavor,
                voicing = MusicalKeys.BuildScaleChord(rootMidi, Key, new[] { 0, 2, 4 })
            };
        }
    }
}
