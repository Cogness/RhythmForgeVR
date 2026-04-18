using System;
using System.Collections.Generic;
using RhythmForge.Core.Data;
using RhythmForge.Core.PatternBehavior.Behaviors;

namespace RhythmForge.Core.PatternBehavior
{
    public static class PatternBehaviorRegistry
    {
        private static readonly Dictionary<PatternType, IPatternBehavior> _behaviors = new Dictionary<PatternType, IPatternBehavior>();
        private static readonly List<PatternType> _orderedTypes = new List<PatternType>();
        // Phase C: singleton behavior that fans out all three MusicalShape facets.
        // Not registered in the per-type dictionary — routed to by GetForPattern
        // whenever <c>pattern.musicalShape != null</c>.
        private static readonly MusicalShapeBehavior _musicalShapeBehavior = new MusicalShapeBehavior();

        static PatternBehaviorRegistry()
        {
#pragma warning disable CS0618 // Legacy behaviors are still the scheduling path for pre-v7 (musicalShape==null) patterns.
            Register(new RhythmLoopBehavior());
            Register(new MelodyLineBehavior());
            Register(new HarmonyPadBehavior());
#pragma warning restore CS0618
        }

        public static void Register(IPatternBehavior behavior)
        {
            if (behavior == null)
                throw new ArgumentNullException(nameof(behavior));

            bool exists = _behaviors.ContainsKey(behavior.Type);
            _behaviors[behavior.Type] = behavior;
            if (!exists)
                _orderedTypes.Add(behavior.Type);
        }

        public static IPatternBehavior Get(PatternType type)
        {
            if (_behaviors.TryGetValue(type, out var behavior))
                return behavior;

            throw new KeyNotFoundException($"No pattern behavior registered for {type}.");
        }

        /// <summary>
        /// Phase C router. Returns the unified <see cref="MusicalShapeBehavior"/>
        /// when the pattern carries a <see cref="MusicalShape"/> (i.e. was drafted
        /// post-Phase-B and not migrated from a legacy single-facet save). Falls
        /// back to the per-type behavior otherwise so legacy saves keep playing
        /// identically.
        /// </summary>
        public static IPatternBehavior GetForPattern(PatternDefinition pattern)
        {
            if (pattern == null)
                throw new ArgumentNullException(nameof(pattern));

            if (pattern.musicalShape != null && pattern.musicalShape.facets != null)
                return _musicalShapeBehavior;

            return Get(pattern.type);
        }

        public static IReadOnlyList<PatternType> GetRegisteredTypes()
        {
            return _orderedTypes;
        }
    }
}
