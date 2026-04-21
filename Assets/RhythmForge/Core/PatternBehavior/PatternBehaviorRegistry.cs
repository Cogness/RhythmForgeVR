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

        static PatternBehaviorRegistry()
        {
            Register(new RhythmLoopBehavior());
            Register(new MelodyLineBehavior());
            Register(new HarmonyPadBehavior());
            Register(new BassBehavior());
            Register(new GrooveBehavior());
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

        public static IReadOnlyList<PatternType> GetRegisteredTypes()
        {
            return _orderedTypes;
        }
    }
}
