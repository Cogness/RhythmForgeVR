#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;
using RhythmForge.Core.Sequencing.Jazz;
using RhythmForge.Core.Sequencing.NewAge;
using RhythmForge.Core.Session;

namespace RhythmForge.Editor
{
    /// <summary>
    /// Walkthrough tests verifying that Jazz and New Age derivers produce well-formed
    /// 8-bar output in correct registers with genre-appropriate preset IDs.
    /// </summary>
    public class JazzNewAgeDeriverTests
    {
        // ──────────────────────────────────────────────────────────────
        //  Shared helpers
        // ──────────────────────────────────────────────────────────────

        private static List<Vector2> Points() => new List<Vector2>
        {
            new Vector2(0f, 0f),
            new Vector2(0.4f, 0.8f),
            new Vector2(1f, 0.2f),
            new Vector2(0.6f, 0.9f)
        };

        private static StrokeMetrics Metrics() => new StrokeMetrics
        {
            length = 1f, averageSize = 0.42f, height = 1f, width = 1f
        };

        private static ShapeProfile NeutralShape() => new ShapeProfile
        {
            circularity = 0.5f, angularity = 0.4f, symmetry = 0.5f,
            verticalSpan = 0.5f, horizontalSpan = 0.5f, pathLength = 0.55f,
            speedVariance = 0.2f, curvatureMean = 0.3f, curvatureVariance = 0.1f,
            centroidHeight = 0.5f, tiltSigned = 0f, wobble = 0.1f
        };

        private static SoundProfile NeutralSound() => new SoundProfile
        {
            body = 0.5f, brightness = 0.4f, drive = 0.2f, transientSharpness = 0.3f,
            releaseBias = 0.5f, resonance = 0.25f, attackBias = 0.25f
        };

        // ──────────────────────────────────────────────────────────────
        //  Jazz rhythm
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void JazzRhythm_Produces8Bars_WithBackbeatFloor()
        {
            var genre = GenreRegistry.Get("jazz");
            var result = new JazzRhythmDeriver().Derive(Points(), Metrics(), NeutralShape(), NeutralSound(), genre);

            Assert.That(result.bars, Is.EqualTo(8));
            Assert.That(result.derivedSequence.totalSteps, Is.EqualTo(8 * AppStateFactory.BarSteps));
            Assert.That(result.derivedSequence.events, Is.Not.Empty);
            Assert.That(result.presetId, Is.EqualTo("jazz-brush"));

            // Every bar must have a kick on step 0 (beat 1).
            int barSteps = AppStateFactory.BarSteps;
            for (int bar = 0; bar < 8; bar++)
            {
                int beat1 = bar * barSteps;
                Assert.That(
                    result.derivedSequence.events.Exists(e => e.step == beat1 && e.lane == "kick"),
                    Is.True,
                    $"Missing kick on beat 1 of bar {bar}");
            }

            // Every bar must have a snare on beat 2 (step 4) and beat 4 (step 12).
            for (int bar = 0; bar < 8; bar++)
            {
                int beat2 = bar * barSteps + 4;
                int beat4 = bar * barSteps + 12;
                Assert.That(
                    result.derivedSequence.events.Exists(e => e.step == beat2 && e.lane == "snare"),
                    Is.True, $"Missing snare on beat 2 of bar {bar}");
                Assert.That(
                    result.derivedSequence.events.Exists(e => e.step == beat4 && e.lane == "snare"),
                    Is.True, $"Missing snare on beat 4 of bar {bar}");
            }
        }

        [Test]
        public void JazzRhythm_SwingIsWithinExpectedRange()
        {
            var result = new JazzRhythmDeriver().Derive(Points(), Metrics(), NeutralShape(), NeutralSound(), null);
            Assert.That(result.derivedSequence.swing, Is.InRange(0.22f, 0.38f));
        }

        // ──────────────────────────────────────────────────────────────
        //  Jazz melody
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void JazzMelody_Produces8Bars_InJazzRegister()
        {
            GenreRegistry.SetActive("jazz");
            var policy = GuidedPolicy.Get("jazz");
            var genre  = GenreRegistry.Get("jazz");
            HarmonicContextProvider.SetProgression(policy.CreateDefaultProgression());

            try
            {
                var result = new JazzMelodyDeriver().Derive(Points(), Metrics(), policy.keyName, NeutralShape(), NeutralSound(), genre);

                Assert.That(result.bars, Is.EqualTo(8));
                Assert.That(result.derivedSequence.notes, Is.Not.Empty);

                var (min, max) = RegisterPolicy.GetRange(PatternType.Melody, "jazz");
                foreach (var note in result.derivedSequence.notes)
                    Assert.That(note.midi, Is.InRange(min, max), $"Note midi {note.midi} out of jazz melody range [{min},{max}]");
            }
            finally
            {
                HarmonicContextProvider.Clear();
                GenreRegistry.SetActive("electronic");
            }
        }

        // ──────────────────────────────────────────────────────────────
        //  Jazz harmony
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void JazzHarmony_Produces8ChordSlots_InHarmonyRegister()
        {
            GenreRegistry.SetActive("jazz");
            var genre = GenreRegistry.Get("jazz");

            try
            {
                var result = genre.HarmonyDeriver.Derive(Points(), Metrics(), "D minor", NeutralShape(), NeutralSound(), genre);

                Assert.That(result.bars, Is.EqualTo(8));
                Assert.That(result.derivedSequence.chordEvents, Has.Count.EqualTo(8));

                var (min, max) = RegisterPolicy.GetRange(PatternType.Harmony, "jazz");
                foreach (var slot in result.derivedSequence.chordEvents)
                {
                    Assert.That(slot.voicing, Is.Not.Empty);
                    foreach (var midi in slot.voicing)
                        Assert.That(midi, Is.InRange(min, max), $"Jazz harmony voicing midi {midi} out of range [{min},{max}]");
                }
            }
            finally
            {
                GenreRegistry.SetActive("electronic");
            }
        }

        // ──────────────────────────────────────────────────────────────
        //  New Age rhythm
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void NewAgeRhythm_Produces8Bars_WithMeditativeFloor()
        {
            var genre = GenreRegistry.Get("newage");
            var result = new NewAgeRhythmDeriver().Derive(Points(), Metrics(), NeutralShape(), NeutralSound(), genre);

            Assert.That(result.bars, Is.EqualTo(8));
            Assert.That(result.derivedSequence.totalSteps, Is.EqualTo(8 * AppStateFactory.BarSteps));
            Assert.That(result.derivedSequence.events, Is.Not.Empty);
            Assert.That(result.presetId, Is.EqualTo("newage-bowl"));

            int barSteps = AppStateFactory.BarSteps;
            // Every bar must have a kick on beat 1 and shakers on beats 2 + 4.
            for (int bar = 0; bar < 8; bar++)
            {
                int beat1 = bar * barSteps;
                int beat2 = bar * barSteps + 4;
                int beat4 = bar * barSteps + 12;
                Assert.That(
                    result.derivedSequence.events.Exists(e => e.step == beat1 && e.lane == "kick"),
                    Is.True, $"Missing kick on beat 1 of bar {bar}");
                Assert.That(
                    result.derivedSequence.events.Exists(e => e.step == beat2 && e.lane == "hat"),
                    Is.True, $"Missing shaker on beat 2 of bar {bar}");
                Assert.That(
                    result.derivedSequence.events.Exists(e => e.step == beat4 && e.lane == "hat"),
                    Is.True, $"Missing shaker on beat 4 of bar {bar}");
            }
        }

        [Test]
        public void NewAgeRhythm_SwingIsNearZero()
        {
            var result = new NewAgeRhythmDeriver().Derive(Points(), Metrics(), NeutralShape(), NeutralSound(), null);
            Assert.That(result.derivedSequence.swing, Is.InRange(0f, 0.08f));
        }

        [Test]
        public void NewAgeRhythm_CircularShape_AddsBowlOnBeat3()
        {
            var circular = NeutralShape();
            circular.circularity = 0.8f;
            var genre = GenreRegistry.Get("newage");
            var result = new NewAgeRhythmDeriver().Derive(Points(), Metrics(), circular, NeutralSound(), genre);

            int barSteps = AppStateFactory.BarSteps;
            // Bowl (perc lane) should appear on beat 3 (step 8) in at least one bar.
            Assert.That(
                result.derivedSequence.events.Exists(e => e.lane == "perc" && e.step % barSteps == 8),
                Is.True, "Expected bowl perc hit on beat 3 for circular shape");
        }

        // ──────────────────────────────────────────────────────────────
        //  New Age melody
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void NewAgeMelody_Produces8Bars_InNewAgeRegister()
        {
            GenreRegistry.SetActive("newage");
            var policy = GuidedPolicy.Get("newage");
            var genre  = GenreRegistry.Get("newage");
            HarmonicContextProvider.SetProgression(policy.CreateDefaultProgression());

            try
            {
                var result = new NewAgeMelodyDeriver().Derive(Points(), Metrics(), policy.keyName, NeutralShape(), NeutralSound(), genre);

                Assert.That(result.bars, Is.EqualTo(8));
                Assert.That(result.derivedSequence.notes, Is.Not.Empty);

                var (min, max) = RegisterPolicy.GetRange(PatternType.Melody, "newage");
                foreach (var note in result.derivedSequence.notes)
                    Assert.That(note.midi, Is.InRange(min, max), $"Note midi {note.midi} out of newage melody range [{min},{max}]");
            }
            finally
            {
                HarmonicContextProvider.Clear();
                GenreRegistry.SetActive("electronic");
            }
        }

        // ──────────────────────────────────────────────────────────────
        //  New Age harmony
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void NewAgeHarmony_Produces8ChordSlots_InHarmonyRegister()
        {
            GenreRegistry.SetActive("newage");
            var genre = GenreRegistry.Get("newage");

            try
            {
                var result = genre.HarmonyDeriver.Derive(Points(), Metrics(), "C major", NeutralShape(), NeutralSound(), genre);

                Assert.That(result.bars, Is.EqualTo(8));
                Assert.That(result.derivedSequence.chordEvents, Has.Count.EqualTo(8));

                var (min, max) = RegisterPolicy.GetRange(PatternType.Harmony, "newage");
                foreach (var slot in result.derivedSequence.chordEvents)
                {
                    Assert.That(slot.voicing, Is.Not.Empty);
                    foreach (var midi in slot.voicing)
                        Assert.That(midi, Is.InRange(min, max), $"NewAge harmony voicing midi {midi} out of range [{min},{max}]");
                }
            }
            finally
            {
                GenreRegistry.SetActive("electronic");
            }
        }

        // ──────────────────────────────────────────────────────────────
        //  GuidedPolicy presets
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void GuidedPolicy_Jazz_HasExpectedBassPresetAndKey()
        {
            var policy = GuidedPolicy.Get("jazz");
            Assert.That(policy.defaultBassPresetId, Is.EqualTo("jazz-upright"));
            Assert.That(policy.keyName, Is.EqualTo("D minor"));
            Assert.That(policy.tempo, Is.EqualTo(110f));
            Assert.That(policy.bars, Is.EqualTo(8));
        }

        [Test]
        public void GuidedPolicy_NewAge_HasExpectedBassPresetAndKey()
        {
            var policy = GuidedPolicy.Get("newage");
            Assert.That(policy.defaultBassPresetId, Is.EqualTo("newage-subbass"));
            Assert.That(policy.keyName, Is.EqualTo("C major"));
            Assert.That(policy.tempo, Is.EqualTo(68f));
            Assert.That(policy.bars, Is.EqualTo(8));
        }

        [Test]
        public void GenreRegistry_Jazz_ExposesUprightBassPreset()
        {
            var preset = InstrumentPresets.Get("jazz-upright");
            Assert.That(preset, Is.Not.Null);
            Assert.That(preset.id, Is.EqualTo("jazz-upright"));
        }

        [Test]
        public void GenreRegistry_NewAge_ExposesSubBassPreset()
        {
            var preset = InstrumentPresets.Get("newage-subbass");
            Assert.That(preset, Is.Not.Null);
            Assert.That(preset.id, Is.EqualTo("newage-subbass"));
        }

        // ──────────────────────────────────────────────────────────────
        //  SetGenre guided re-seed
        // ──────────────────────────────────────────────────────────────

        [Test]
        public void SessionStore_SetGenre_GuidedMode_ReSeedsKeyAndTempo_Jazz()
        {
            var store = new SessionStore();
            var state = AppStateFactory.CreateEmpty();
            state.guidedMode = true;
            state.activeGenreId = "electronic";
            state.composition = GuidedDefaults.Create();
            state.key = state.composition.key;
            state.tempo = state.composition.tempo;
            store.LoadState(state);

            store.SetGenre("jazz");

            Assert.That(store.State.key, Is.EqualTo("D minor"));
            Assert.That(store.State.tempo, Is.EqualTo(110f));
            Assert.That(store.State.composition.progression, Is.Not.Null);
            Assert.That(store.State.composition.progression.bars, Is.EqualTo(8));
        }

        [Test]
        public void SessionStore_SetGenre_GuidedMode_ReSeedsKeyAndTempo_NewAge()
        {
            var store = new SessionStore();
            var state = AppStateFactory.CreateEmpty();
            state.guidedMode = true;
            state.activeGenreId = "electronic";
            state.composition = GuidedDefaults.Create();
            state.key = state.composition.key;
            state.tempo = state.composition.tempo;
            store.LoadState(state);

            store.SetGenre("newage");

            Assert.That(store.State.key, Is.EqualTo("C major"));
            Assert.That(store.State.tempo, Is.EqualTo(68f));
            Assert.That(store.State.composition.progression, Is.Not.Null);
            Assert.That(store.State.composition.progression.bars, Is.EqualTo(8));
        }
    }
}
#endif
