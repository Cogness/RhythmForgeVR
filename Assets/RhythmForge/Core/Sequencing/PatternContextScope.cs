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

        public static PatternContextScope Push(ShapeRole role, HarmonicContext harmonicContext)
        {
            ShapeRoleProvider.Set(role);
            HarmonicContextProvider.Set(harmonicContext);
            return new PatternContextScope(true);
        }

        public static PatternContextScope ForPattern(AppState state, PatternDefinition pattern)
        {
            return Push(ResolveRole(state, pattern), CloneHarmonicContext(state?.harmonicContext));
        }

        /// <summary>
        /// Phase C alias for <see cref="ForPattern"/>. Semantically identical —
        /// both resolve the scene-wide per-shape role — but the name documents
        /// that roles are no longer per-type.
        /// </summary>
        public static PatternContextScope ForShape(AppState state, PatternDefinition pattern)
        {
            return ForPattern(state, pattern);
        }

        public void Dispose()
        {
            if (!_active)
                return;

            ShapeRoleProvider.Clear();
            HarmonicContextProvider.Clear();
        }

        /// <summary>
        /// Phase C: role is now scene-wide (counts ALL patterns, not just
        /// same-type). A mixed scene with 2 rhythm + 2 melody patterns produces
        /// indices 0..3 across the four shapes — stacking lead / counter /
        /// pedal roles regardless of facet. Pre-Phase-C this filtered by type,
        /// which caused the "two role-0 primaries" collision the musical-coherence
        /// refactor already solved for same-type; widening closes that gap for
        /// cross-type scenes too.
        /// </summary>
        public static ShapeRole ResolveRole(AppState state, PatternDefinition pattern)
        {
            if (state?.patterns == null || pattern == null)
                return ShapeRole.Primary;

            int count = 0;
            int index = 0;
            bool found = false;

            foreach (var candidate in state.patterns)
            {
                if (candidate == null)
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
    }
}
