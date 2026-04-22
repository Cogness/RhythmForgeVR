using System;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    public readonly struct PatternContextScope : IDisposable
    {
        private readonly bool _active;

        private PatternContextScope(bool active)
        {
            _active = active;
        }

        public static PatternContextScope Push(
            HarmonicContext harmonicContext,
            ChordProgression progression = null)
        {
            HarmonicContextProvider.Set(harmonicContext);
            HarmonicContextProvider.SetProgression(CloneProgression(progression));
            return new PatternContextScope(true);
        }

        public static PatternContextScope ForPattern(AppState state, PatternDefinition pattern)
        {
            return Push(
                CloneHarmonicContext(state?.harmonicContext),
                state != null && state.guidedMode ? CloneProgression(state.composition?.progression) : null);
        }

        public void Dispose()
        {
            if (!_active)
                return;

            HarmonicContextProvider.Clear();
        }

        public static HarmonicContext CloneHarmonicContext(HarmonicContext harmonicContext)
        {
            return harmonicContext?.Clone() ?? new HarmonicContext();
        }

        public static ChordProgression CloneProgression(ChordProgression progression)
        {
            return progression?.Clone();
        }
    }
}
