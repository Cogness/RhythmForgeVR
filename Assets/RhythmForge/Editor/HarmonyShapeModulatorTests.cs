#if UNITY_EDITOR
using NUnit.Framework;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Editor
{
    public class HarmonyShapeModulatorTests
    {
        [Test]
        public void AllOutputPitches_InGMajor()
        {
            var progression = HarmonyShapeModulator.Modulate(
                new StrokeMetrics(),
                new ShapeProfile
                {
                    tiltSigned = 0.35f,
                    verticalSpan = 0.72f,
                    horizontalSpan = 0.58f,
                    centroidHeight = 0.81f,
                    symmetry = 0.32f
                },
                GuidedDefaults.CreateDefaultProgression(),
                GuidedDefaults.Key,
                "electronic");

            foreach (var slot in progression.chords)
            {
                Assert.That(slot.voicing, Is.Not.Null);
                Assert.That(slot.voicing.Count, Is.GreaterThanOrEqualTo(3));
                foreach (var midi in slot.voicing)
                    Assert.That(MusicalKeys.QuantizeToKey(midi, GuidedDefaults.Key), Is.EqualTo(midi));
            }
        }

        [Test]
        public void Roots_AlwaysMatchIviIVVLoop()
        {
            var progression = HarmonyShapeModulator.Modulate(
                new StrokeMetrics(),
                new ShapeProfile
                {
                    tiltSigned = -0.4f,
                    verticalSpan = 0.9f,
                    horizontalSpan = 0.85f,
                    centroidHeight = 0.15f,
                    symmetry = 0.2f
                },
                GuidedDefaults.CreateDefaultProgression(),
                GuidedDefaults.Key,
                "electronic");

            var expectedRoots = new[] { 67, 64, 60, 62, 67, 64, 60, 62 };
            Assert.That(progression.chords, Has.Count.EqualTo(expectedRoots.Length));
            for (int i = 0; i < expectedRoots.Length; i++)
                Assert.That(progression.chords[i].rootMidi, Is.EqualTo(expectedRoots[i]));
        }

        [Test]
        public void Bars4And8_AlwaysIncludeCadenceLift()
        {
            var defaults = GuidedDefaults.CreateDefaultProgression();
            var progression = HarmonyShapeModulator.Modulate(
                new StrokeMetrics(),
                new ShapeProfile
                {
                    tiltSigned = 0f,
                    verticalSpan = 0.3f,
                    horizontalSpan = 0.3f,
                    centroidHeight = 0.5f,
                    symmetry = 0.9f
                },
                defaults,
                GuidedDefaults.Key,
                GuidedDefaults.ActiveGenreId);

            Assert.That(progression.chords[3].voicing.Count, Is.GreaterThan(defaults.chords[3].voicing.Count));
            Assert.That(progression.chords[7].voicing.Count, Is.GreaterThan(defaults.chords[7].voicing.Count));
        }
    }
}
#endif
