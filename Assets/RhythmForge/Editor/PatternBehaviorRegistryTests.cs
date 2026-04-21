#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using RhythmForge.Core.Data;
using RhythmForge.Core.PatternBehavior;

namespace RhythmForge.Editor
{
    public class PatternBehaviorRegistryTests
    {
        [Test]
        public void AllFivePhaseTypesResolveToABehavior()
        {
            var expectedTypes = new[]
            {
                PatternType.Percussion,
                PatternType.Melody,
                PatternType.Harmony,
                PatternType.Bass,
                PatternType.Groove
            };

            IReadOnlyList<PatternType> registeredTypes = PatternBehaviorRegistry.GetRegisteredTypes();

            CollectionAssert.AreEqual(expectedTypes, registeredTypes);

            foreach (var type in expectedTypes)
            {
                var behavior = PatternBehaviorRegistry.Get(type);
                Assert.That(behavior, Is.Not.Null);
                Assert.That(behavior.Type, Is.EqualTo(type));
            }
        }
    }
}
#endif
