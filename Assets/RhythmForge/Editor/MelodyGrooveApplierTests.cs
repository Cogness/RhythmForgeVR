#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Editor
{
    public class MelodyGrooveApplierTests
    {
        [Test]
        public void HighDensityGroove_KeepsMelodyPitchesIntact()
        {
            var notes = new List<MelodyNote>
            {
                new MelodyNote { step = 0, midi = 67, durationSteps = 4, velocity = 0.5f, glide = 0f },
                new MelodyNote { step = 10, midi = 69, durationSteps = 4, velocity = 0.5f, glide = 0f },
                new MelodyNote { step = 22, midi = 71, durationSteps = 4, velocity = 0.5f, glide = 0f },
                new MelodyNote { step = 34, midi = 74, durationSteps = 4, velocity = 0.5f, glide = 0f }
            };
            var groove = new GrooveProfile
            {
                density = 1.35f,
                syncopation = 0.25f,
                swing = 0.12f,
                quantizeGrid = 16,
                accentCurve = new[] { 1f, 0.7f, 0.85f, 0.7f }
            };

            var scheduled = MelodyGrooveApplier.Apply(notes, groove, GuidedDefaults.Bars * AppStateFactory.BarSteps);

            Assert.That(scheduled.Count, Is.GreaterThanOrEqualTo(notes.Count));
            Assert.That(scheduled.Select(note => note.midi).Distinct().OrderBy(midi => midi).ToArray(),
                Is.EqualTo(notes.Select(note => note.midi).Distinct().OrderBy(midi => midi).ToArray()));
        }

        [Test]
        public void AnchorNotes_NeverThinned()
        {
            var notes = new List<MelodyNote>
            {
                new MelodyNote { step = 0, midi = 67, durationSteps = 4, velocity = 0.5f, glide = 0f },
                new MelodyNote { step = 16, midi = 69, durationSteps = 4, velocity = 0.5f, glide = 0f },
                new MelodyNote { step = 48, midi = 71, durationSteps = 4, velocity = 0.5f, glide = 0f },
                new MelodyNote { step = 64, midi = 74, durationSteps = 4, velocity = 0.5f, glide = 0f },
                new MelodyNote { step = 80, midi = 76, durationSteps = 4, velocity = 0.5f, glide = 0f }
            };
            var groove = new GrooveProfile
            {
                density = 0.5f,
                syncopation = 0.18f,
                swing = 0.1f,
                quantizeGrid = 8,
                accentCurve = new[] { 1f, 0.7f, 0.85f, 0.7f }
            };

            var scheduled = MelodyGrooveApplier.Apply(notes, groove, GuidedDefaults.Bars * AppStateFactory.BarSteps);

            Assert.That(scheduled.Exists(note => note.step == 0), Is.True);
            Assert.That(scheduled.Exists(note => note.step == 64), Is.True);
        }

        [Test]
        public void SparseDensity_RemovesSomeNonAnchorNotes()
        {
            var notes = new List<MelodyNote>
            {
                new MelodyNote { step = 0, midi = 67, durationSteps = 4, velocity = 0.5f, glide = 0f },
                new MelodyNote { step = 8, midi = 69, durationSteps = 4, velocity = 0.5f, glide = 0f },
                new MelodyNote { step = 16, midi = 71, durationSteps = 4, velocity = 0.5f, glide = 0f },
                new MelodyNote { step = 24, midi = 72, durationSteps = 4, velocity = 0.5f, glide = 0f },
                new MelodyNote { step = 32, midi = 74, durationSteps = 4, velocity = 0.5f, glide = 0f },
                new MelodyNote { step = 40, midi = 76, durationSteps = 4, velocity = 0.5f, glide = 0f },
                new MelodyNote { step = 48, midi = 77, durationSteps = 4, velocity = 0.5f, glide = 0f },
                new MelodyNote { step = 64, midi = 79, durationSteps = 4, velocity = 0.5f, glide = 0f }
            };
            var groove = new GrooveProfile
            {
                density = 0.5f,
                syncopation = 0.1f,
                swing = 0.08f,
                quantizeGrid = 8,
                accentCurve = new[] { 1f, 0.7f, 0.85f, 0.7f }
            };

            var scheduled = MelodyGrooveApplier.Apply(notes, groove, GuidedDefaults.Bars * AppStateFactory.BarSteps);

            Assert.That(scheduled.Count, Is.LessThan(notes.Count));
            Assert.That(scheduled.Exists(note => note.step == 0), Is.True);
            Assert.That(scheduled.Exists(note => note.step == 64), Is.True);
        }
    }
}
#endif
