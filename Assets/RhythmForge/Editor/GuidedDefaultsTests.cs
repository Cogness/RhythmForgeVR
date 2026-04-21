#if UNITY_EDITOR
using NUnit.Framework;
using RhythmForge.Core.Data;

namespace RhythmForge.Editor
{
    public class GuidedDefaultsTests
    {
        [Test]
        public void Create_DefaultComposition_SeedsGMajor100BpmEightBars()
        {
            var composition = GuidedDefaults.Create();

            Assert.That(composition, Is.Not.Null);
            Assert.That(composition.tempo, Is.EqualTo(100f));
            Assert.That(composition.key, Is.EqualTo("G major"));
            Assert.That(composition.bars, Is.EqualTo(8));
            Assert.That(composition.currentPhase, Is.EqualTo(CompositionPhase.Harmony));
            Assert.That(composition.groove, Is.Null);
            Assert.That(composition.phasePatternIds, Is.Empty);
            Assert.That(composition.progression, Is.Not.Null);
            Assert.That(composition.progression.chords, Has.Count.EqualTo(8));
        }

        [Test]
        public void DefaultProgression_RootsFollowIviIVV()
        {
            var progression = GuidedDefaults.CreateDefaultProgression();
            var expectedRoots = new[] { 67, 64, 60, 62, 67, 64, 60, 62 };

            Assert.That(progression.chords, Has.Count.EqualTo(expectedRoots.Length));
            for (int i = 0; i < expectedRoots.Length; i++)
            {
                Assert.That(progression.chords[i].barIndex, Is.EqualTo(i));
                Assert.That(progression.chords[i].rootMidi, Is.EqualTo(expectedRoots[i]));
            }
        }

        [Test]
        public void DefaultProgression_AllVoicingPitchesStayInGMajor()
        {
            var progression = GuidedDefaults.CreateDefaultProgression();

            foreach (var chord in progression.chords)
            {
                Assert.That(chord.voicing, Is.Not.Null);
                Assert.That(chord.voicing.Count, Is.GreaterThanOrEqualTo(3));
                foreach (var midi in chord.voicing)
                    Assert.That(MusicalKeys.QuantizeToKey(midi, GuidedDefaults.Key), Is.EqualTo(midi));
            }
        }
    }
}
#endif
