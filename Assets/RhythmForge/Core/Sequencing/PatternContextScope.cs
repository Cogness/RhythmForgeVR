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
            ShapeRole role,
            HarmonicContext harmonicContext,
            ChordProgression progression = null)
        {
            ShapeRoleProvider.Set(role);
            HarmonicContextProvider.Set(harmonicContext);
            HarmonicContextProvider.SetProgression(CloneProgression(progression));
            return new PatternContextScope(true);
        }

        public static PatternContextScope ForPattern(AppState state, PatternDefinition pattern)
        {
            return Push(
                ResolveRole(state, pattern),
                CloneHarmonicContext(state?.harmonicContext),
                state != null && state.guidedMode ? CloneProgression(state.composition?.progression) : null);
        }

        public void Dispose()
        {
            if (!_active)
                return;

            ShapeRoleProvider.Clear();
            HarmonicContextProvider.Clear();
        }

        public static ShapeRole ResolveRole(AppState state, PatternDefinition pattern)
        {
            if (state?.patterns == null || pattern == null)
                return ShapeRole.Primary;

            int count = 0;
            int index = 0;
            bool found = false;

            foreach (var candidate in state.patterns)
            {
                if (candidate?.type != pattern.type)
                    continue;

                if (!found && candidate.id == pattern.id)
                {
                    index = count;
                    found = true;
                }

                count++;
            }

            if (count <= 0)
                return ShapeRole.Primary;

            return new ShapeRole
            {
                index = found ? index : 0,
                count = count
            };
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
