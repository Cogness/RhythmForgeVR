#if UNITY_EDITOR
using NUnit.Framework;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Editor
{
    public class HarmonicContextProviderTests
    {
        [Test]
        public void FromProgression_BarIndex2_ReturnsCMajorChordTones()
        {
            var progression = GuidedDefaults.CreateDefaultProgression();

            var context = HarmonicContextProvider.FromProgression(progression, 2);

            Assert.That(context.rootMidi, Is.EqualTo(60));
            CollectionAssert.AreEqual(new[] { 60, 64, 67 }, context.chordTones);
            Assert.That(context.flavor, Is.EqualTo("major"));
        }

        [Test]
        public void FromProgression_BarIndex3_ReturnsDMajorChordTones()
        {
            var progression = GuidedDefaults.CreateDefaultProgression();

            var context = HarmonicContextProvider.FromProgression(progression, 3);

            Assert.That(context.rootMidi, Is.EqualTo(62));
            CollectionAssert.AreEqual(new[] { 62, 66, 69 }, context.chordTones);
            Assert.That(context.flavor, Is.EqualTo("major"));
        }

        [Test]
        public void FromProgression_WrapsOutOfRangeBarIndices()
        {
            var progression = GuidedDefaults.CreateDefaultProgression();

            var negative = HarmonicContextProvider.FromProgression(progression, -1);
            var overflow = HarmonicContextProvider.FromProgression(progression, 10);

            Assert.That(negative.rootMidi, Is.EqualTo(62));
            Assert.That(overflow.rootMidi, Is.EqualTo(60));
        }
    }
}
#endif
