#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Editor
{
    public class MelodyDeriverTests
    {
        [Test]
        public void StrongBeats_AreChordTonesOfCurrentBar()
        {
            var progression = GuidedDefaults.CreateDefaultProgression();
            var result = DeriveGuidedMelody(CreateShape(tiltSigned: 0.35f), progression);

            Assert.That(result.bars, Is.EqualTo(GuidedDefaults.Bars));
            Assert.That(result.derivedSequence.notes, Is.Not.Empty);

            foreach (var note in result.derivedSequence.notes)
            {
                if (note.step % 4 != 0)
                    continue;

                var slot = progression.GetSlotForBar(note.step / AppStateFactory.BarSteps);
                Assert.That(slot, Is.Not.Null);
                Assert.That(ContainsPitchClass(slot.voicing, note.midi), Is.True,
                    $"Expected note {note.midi} at step {note.step} to match bar {slot.barIndex} chord.");
            }
        }

        [Test]
        public void AllPitches_InGMajorScale()
        {
            var result = DeriveGuidedMelody(CreateShape(tiltSigned: -0.2f), GuidedDefaults.CreateDefaultProgression());

            foreach (var note in result.derivedSequence.notes)
                Assert.That(MusicalKeys.QuantizeToKey(note.midi, GuidedDefaults.Key), Is.EqualTo(note.midi));
        }

        [Test]
        public void Bar8FinalNote_LengthIsHalfNoteOrLonger()
        {
            var result = DeriveGuidedMelody(CreateShape(tiltSigned: 0.22f), GuidedDefaults.CreateDefaultProgression());
            var finalNote = result.derivedSequence.notes[result.derivedSequence.notes.Count - 1];

            Assert.That(result.derivedSequence.totalSteps, Is.EqualTo(GuidedDefaults.Bars * AppStateFactory.BarSteps));
            Assert.That(finalNote.step, Is.GreaterThanOrEqualTo(result.derivedSequence.totalSteps - AppStateFactory.BarSteps));
            Assert.That(finalNote.durationSteps, Is.GreaterThanOrEqualTo(8));
        }

        private static MelodyDerivationResult DeriveGuidedMelody(ShapeProfile shapeProfile, ChordProgression progression)
        {
            var points = CreatePoints();
            var metrics = StrokeAnalyzer.Analyze(points);
            var sound = new SoundProfile
            {
                filterMotion = 0.42f,
                transientSharpness = 0.32f
            };

            using (PatternContextScope.Push(
                progression.ToHarmonicContext(0),
                progression))
            {
                return MelodyDeriver.Derive(points, metrics, GuidedDefaults.Key, "lofi", shapeProfile, sound);
            }
        }

        private static List<Vector2> CreatePoints()
        {
            return new List<Vector2>
            {
                new Vector2(0.00f, 0.45f),
                new Vector2(0.08f, 0.56f),
                new Vector2(0.16f, 0.30f),
                new Vector2(0.24f, 0.64f),
                new Vector2(0.32f, 0.38f),
                new Vector2(0.40f, 0.70f),
                new Vector2(0.48f, 0.42f),
                new Vector2(0.56f, 0.74f),
                new Vector2(0.64f, 0.40f),
                new Vector2(0.72f, 0.66f),
                new Vector2(0.80f, 0.36f),
                new Vector2(0.88f, 0.58f),
                new Vector2(0.96f, 0.48f)
            };
        }

        private static ShapeProfile CreateShape(float tiltSigned)
        {
            return new ShapeProfile
            {
                verticalSpan = 0.72f,
                horizontalSpan = 0.68f,
                pathLength = 0.74f,
                speedVariance = 0.52f,
                curvatureMean = 0.41f,
                curvatureVariance = 0.33f,
                angularity = 0.37f,
                centroidHeight = 0.56f,
                tiltSigned = tiltSigned
            };
        }

        private static bool ContainsPitchClass(List<int> pitches, int midi)
        {
            if (pitches == null)
                return false;

            int targetClass = ((midi % 12) + 12) % 12;
            for (int i = 0; i < pitches.Count; i++)
            {
                if ((((pitches[i] % 12) + 12) % 12) == targetClass)
                    return true;
            }

            return false;
        }
    }
}
#endif
