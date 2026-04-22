#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;

namespace RhythmForge.Editor
{
    public class StateMigratorTests
    {
        [Test]
        public void NormalizeState_CleansMembership_NormalizesDrawMode_AndResetsInvalidRotation()
        {
            var migrator = new StateMigrator();
            var state = AppStateFactory.CreateEmpty();
            state.version = 2;
            state.drawMode = "NotARealMode";
            state.activeSceneId = "scene-a";
            state.scenes[0].instanceIds.Add("missing-instance");

            var pattern = new PatternDefinition
            {
                id = "pattern-1",
                type = PatternType.HarmonyPad,
                name = "Pad",
                bars = 1,
                groupId = "dream",
                presetId = "dream-pad",
                shapeProfile = new ShapeProfile
                {
                    worldWidth = 0.2f,
                    worldHeight = 0.18f,
                    worldLength = 0.5f,
                    worldAverageSize = 0.19f,
                    worldMaxDimension = 0.2f
                },
                soundProfile = new SoundProfile(),
                derivedSequence = new DerivedSequence { kind = "harmony", totalSteps = AppStateFactory.BarSteps },
                hasRenderRotation = true,
                renderRotation = new Quaternion(0f, 0f, 0f, 0f),
                summary = "",
                details = ""
            };
            state.patterns.Add(pattern);

            var instance = new PatternInstance(pattern.id, "scene-b", Vector3.zero, 0.3f);
            state.instances.Add(instance);
            state.scenes[1].instanceIds.Clear();

            migrator.NormalizeState(state);

            Assert.That(state.version, Is.EqualTo(8));
            Assert.That(state.drawMode, Is.EqualTo(PatternType.Percussion.ToString()));
            Assert.That(state.scenes[0].instanceIds, Does.Not.Contain("missing-instance"));
            Assert.That(state.scenes[1].instanceIds, Does.Contain(instance.id));
            Assert.That(pattern.hasRenderRotation, Is.False);
            Assert.That(pattern.renderRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(state.composition, Is.Not.Null);
            Assert.That(state.guidedMode, Is.True);
        }

        [TestCase("RhythmLoop", PatternType.Percussion)]
        [TestCase("MelodyLine", PatternType.Melody)]
        [TestCase("HarmonyPad", PatternType.Harmony)]
        [TestCase("Bass", PatternType.Bass)]
        [TestCase("Groove", PatternType.Groove)]
        public void NormalizeState_NormalizesLegacyAndCanonicalDrawModes(string serializedMode, PatternType expectedMode)
        {
            var migrator = new StateMigrator();
            var state = AppStateFactory.CreateEmpty();
            state.version = 5;
            state.drawMode = serializedMode;

            migrator.NormalizeState(state);

            Assert.That(state.drawMode, Is.EqualTo(expectedMode.ToString()));
        }

        [Test]
        public void NormalizeState_BackfillsComposition_WithoutOverwritingLegacyTempoKeyGenreOrHarmony()
        {
            var migrator = new StateMigrator();
            var state = AppStateFactory.CreateEmpty();
            state.version = 7;
            state.tempo = 72f;
            state.key = "A minor";
            state.activeGenreId = "jazz";
            state.guidedMode = false;
            state.composition = null;
            state.harmonicContext = new HarmonicContext
            {
                rootMidi = 57,
                chordTones = new System.Collections.Generic.List<int> { 57, 60, 64 },
                flavor = "minor"
            };

            migrator.NormalizeState(state);

            Assert.That(state.version, Is.EqualTo(8));
            Assert.That(state.guidedMode, Is.True);
            Assert.That(state.tempo, Is.EqualTo(72f));
            Assert.That(state.key, Is.EqualTo("A minor"));
            Assert.That(state.activeGenreId, Is.EqualTo("jazz"));
            Assert.That(state.harmonicContext.rootMidi, Is.EqualTo(57));
            Assert.That(state.composition, Is.Not.Null);
            Assert.That(state.composition.tempo, Is.EqualTo(GuidedDefaults.Tempo));
            Assert.That(state.composition.key, Is.EqualTo(GuidedDefaults.Key));
            Assert.That(state.composition.progression, Is.Not.Null);
        }

        [Test]
        public void NormalizeState_RebuildsHarmonyChordEventsFromLegacyHarmonicContext_WhenMissing()
        {
            var migrator = new StateMigrator();
            var state = AppStateFactory.CreateEmpty();
            state.version = 7;
            state.harmonicContext = new HarmonicContext
            {
                rootMidi = 60,
                chordTones = new System.Collections.Generic.List<int> { 60, 64, 67, 71 },
                flavor = "maj7"
            };
            state.composition.progression = new ChordProgression
            {
                bars = 0,
                chords = new System.Collections.Generic.List<ChordSlot>()
            };

            var harmony = new PatternDefinition
            {
                id = "harmony-1",
                type = PatternType.Harmony,
                name = "Harmony-01",
                bars = 4,
                derivedSequence = new DerivedSequence
                {
                    kind = "harmony",
                    totalSteps = 0,
                    chordEvents = null
                }
            };

            state.patterns.Add(harmony);
            state.composition.SetPatternId(CompositionPhase.Harmony, harmony.id);

            migrator.NormalizeState(state);

            Assert.That(harmony.derivedSequence, Is.Not.Null);
            Assert.That(harmony.derivedSequence.totalSteps, Is.EqualTo(4 * AppStateFactory.BarSteps));
            Assert.That(harmony.derivedSequence.chordEvents, Has.Count.EqualTo(4));
            Assert.That(harmony.derivedSequence.chordEvents[0].rootMidi, Is.EqualTo(60));
            Assert.That(harmony.derivedSequence.chordEvents[0].flavor, Is.EqualTo("maj7"));
            Assert.That(harmony.derivedSequence.chordEvents[0].voicing, Is.EquivalentTo(new[] { 60, 64, 67, 71 }));
            Assert.That(state.composition.progression.chords, Has.Count.EqualTo(4));
            Assert.That(state.composition.progression.chords[0].rootMidi, Is.EqualTo(60));
        }
    }
}
#endif
