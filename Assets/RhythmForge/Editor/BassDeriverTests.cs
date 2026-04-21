#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Editor
{
    public class BassDeriverTests
    {
        [Test]
        public void Beat1OfEveryBar_EqualsProgressionRoot()
        {
            var progression = GuidedDefaults.CreateDefaultProgression();
            var result = DeriveGuidedBass(CreateShape(directionBias: 0.88f), progression);

            Assert.That(result.bars, Is.EqualTo(GuidedDefaults.Bars));
            Assert.That(result.derivedSequence.notes, Is.Not.Empty);

            for (int barIndex = 0; barIndex < GuidedDefaults.Bars; barIndex++)
            {
                int beatOneStep = barIndex * AppStateFactory.BarSteps;
                var note = result.derivedSequence.notes.Find(n => n.step == beatOneStep);
                Assert.That(note.step, Is.EqualTo(beatOneStep), $"Expected a bass note on bar {barIndex + 1} beat 1.");

                int expectedRoot = RegisterPolicy.ClampBass(progression.GetSlotForBar(barIndex).rootMidi, GuidedDefaults.ActiveGenreId);
                Assert.That(note.midi, Is.EqualTo(expectedRoot), $"Expected bar {barIndex + 1} beat 1 to match the progression root.");
            }
        }

        [Test]
        public void AllPitches_InGMajor_OrPassingChromaticAllowedOnBar4Beat4()
        {
            var progression = GuidedDefaults.CreateDefaultProgression();
            var result = DeriveGuidedBass(CreateShape(directionBias: 0.92f, angularity: 0.7f), progression);

            foreach (var note in result.derivedSequence.notes)
            {
                bool inKey = MusicalKeys.QuantizeToKey(note.midi, GuidedDefaults.Key) == note.midi;
                if (inKey)
                    continue;

                int barIndex = note.step / AppStateFactory.BarSteps;
                int beatOffset = note.step % AppStateFactory.BarSteps;
                Assert.That(barIndex == 3 || barIndex == 7, Is.True, $"Unexpected chromatic bass note {note.midi} at step {note.step}.");
                Assert.That(beatOffset, Is.EqualTo(14), $"Chromatic approach note {note.midi} should only appear on the final eighth before the next bar.");
            }
        }

        private static MelodyDerivationResult DeriveGuidedBass(ShapeProfile shapeProfile, ChordProgression progression)
        {
            var points = CreatePoints();
            var metrics = StrokeAnalyzer.Analyze(points);
            var sound = new SoundProfile
            {
                body = 0.58f,
                transientSharpness = 0.3f
            };

            using (PatternContextScope.Push(
                ShapeRole.Primary,
                progression.ToHarmonicContext(0),
                progression))
            {
                return BassDeriver.Derive(points, metrics, GuidedDefaults.Key, "lofi", shapeProfile, sound);
            }
        }

        private static List<Vector2> CreatePoints()
        {
            return new List<Vector2>
            {
                new Vector2(0.00f, 0.20f),
                new Vector2(0.12f, 0.28f),
                new Vector2(0.24f, 0.18f),
                new Vector2(0.36f, 0.30f),
                new Vector2(0.48f, 0.16f),
                new Vector2(0.60f, 0.34f),
                new Vector2(0.72f, 0.22f),
                new Vector2(0.84f, 0.36f),
                new Vector2(0.96f, 0.26f)
            };
        }

        private static ShapeProfile CreateShape(float directionBias, float angularity = 0.42f)
        {
            return new ShapeProfile
            {
                verticalSpan = 0.74f,
                horizontalSpan = 0.44f,
                pathLength = 0.82f,
                speedVariance = 0.46f,
                curvatureMean = 0.34f,
                curvatureVariance = 0.28f,
                angularity = angularity,
                centroidHeight = 0.42f,
                directionBias = directionBias,
                tiltSigned = 0.05f
            };
        }
    }
}
#endif
