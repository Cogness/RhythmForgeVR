#if UNITY_EDITOR
using NUnit.Framework;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.Session;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace RhythmForge.Editor
{
    public class SessionStoreCompositionTests
    {
        [Test]
        public void CreateEmptyState_HasGuidedComposition()
        {
            var store = new SessionStore();

            Assert.That(store.State.guidedMode, Is.True);
            Assert.That(store.State.composition, Is.Not.Null);
            Assert.That(store.State.composition.key, Is.EqualTo(GuidedDefaults.Key));
            Assert.That(store.State.composition.tempo, Is.EqualTo(GuidedDefaults.Tempo));
            Assert.That(store.GetHarmonicContextForBar(0).rootMidi, Is.EqualTo(67));
        }

        [Test]
        public void UpdateProgression_StoresProgression_RefreshesBarZeroContext_PublishesEvent()
        {
            var store = new SessionStore();
            var progression = GuidedDefaults.CreateDefaultProgression();
            progression.chords[0].rootMidi = 60;
            progression.chords[0].voicing = MusicalKeys.BuildScaleChord(60, GuidedDefaults.Key, new[] { 0, 2, 4 });

            ChordProgressionChangedEvent? published = null;
            store.EventBus.Subscribe<ChordProgressionChangedEvent>(evt => published = evt);

            store.UpdateProgression(progression);

            Assert.That(store.GetComposition().progression.chords[0].rootMidi, Is.EqualTo(60));
            Assert.That(store.GetHarmonicContext().rootMidi, Is.EqualTo(60));
            Assert.That(published.HasValue, Is.True);
            Assert.That(published.Value.Progression, Is.Not.Null);
            Assert.That(published.Value.Progression.chords[0].rootMidi, Is.EqualTo(60));
        }

        [Test]
        public void UpdateGroove_StoresProfile_WithoutPublishingChordEvent()
        {
            var store = new SessionStore();
            var groove = new GrooveProfile
            {
                density = 1.2f,
                syncopation = 0.25f,
                swing = 0.18f,
                quantizeGrid = 16,
                accentCurve = new[] { 1f, 0.7f, 0.85f, 0.7f }
            };

            bool published = false;
            store.EventBus.Subscribe<ChordProgressionChangedEvent>(_ => published = true);

            store.UpdateGroove(groove);

            Assert.That(store.GetComposition().groove, Is.Not.Null);
            Assert.That(store.GetComposition().groove.density, Is.EqualTo(1.2f));
            Assert.That(store.GetComposition().groove.quantizeGrid, Is.EqualTo(16));
            Assert.That(published, Is.False);
        }

        [Test]
        public void CommitDraft_ForMelody_PublishesMelodyCommittedEvent()
        {
            var store = new SessionStore();
            MelodyCommittedEvent? published = null;
            store.EventBus.Subscribe<MelodyCommittedEvent>(evt => published = evt);

            var draft = new DraftResult
            {
                success = true,
                type = PatternType.Melody,
                name = "Melody-01",
                bars = GuidedDefaults.Bars,
                tempoBase = GuidedDefaults.Tempo,
                key = GuidedDefaults.Key,
                groupId = "lofi",
                presetId = "lofi-piano",
                points = new List<Vector2> { Vector2.zero, Vector2.right * 0.4f, new Vector2(0.8f, 0.2f) },
                renderRotation = Quaternion.identity,
                hasRenderRotation = true,
                spawnPosition = Vector3.zero,
                derivedSequence = new DerivedSequence
                {
                    kind = "melody",
                    totalSteps = GuidedDefaults.Bars * AppStateFactory.BarSteps,
                    notes = new List<MelodyNote>
                    {
                        new MelodyNote { step = 0, midi = 67, durationSteps = 8, velocity = 0.48f }
                    }
                },
                tags = new List<string> { "lead" },
                color = Color.yellow,
                shapeProfile = new ShapeProfile(),
                soundProfile = new SoundProfile(),
                shapeSummary = "steady lead line",
                summary = "melody phrase",
                details = "details"
            };

            var instance = store.CommitDraft(draft, duplicate: false);

            Assert.That(instance, Is.Not.Null);
            Assert.That(published.HasValue, Is.True);
            Assert.That(published.Value.PatternId, Is.EqualTo(instance.patternId));
        }

        [Test]
        public void CommitDraft_ForGroove_StoresProfile_AndPublishesGrooveCommittedEvent()
        {
            var store = new SessionStore();
            GrooveCommittedEvent? published = null;
            store.EventBus.Subscribe<GrooveCommittedEvent>(evt => published = evt);

            var draft = new DraftResult
            {
                success = true,
                type = PatternType.Groove,
                name = "Groove-01",
                bars = GuidedDefaults.Bars,
                tempoBase = GuidedDefaults.Tempo,
                key = GuidedDefaults.Key,
                groupId = "lofi",
                presetId = "lofi-piano",
                points = new List<Vector2> { Vector2.zero, Vector2.right * 0.4f, new Vector2(0.8f, 0.2f) },
                renderRotation = Quaternion.identity,
                hasRenderRotation = true,
                spawnPosition = Vector3.zero,
                derivedSequence = new DerivedSequence
                {
                    kind = "groove",
                    totalSteps = 0,
                    grooveProfile = new GrooveProfile
                    {
                        density = 1.1f,
                        syncopation = 0.22f,
                        swing = 0.16f,
                        quantizeGrid = 16,
                        accentCurve = new[] { 1f, 0.72f, 0.9f, 0.72f }
                    }
                },
                tags = new List<string> { "groove" },
                color = Color.yellow,
                shapeProfile = new ShapeProfile(),
                soundProfile = new SoundProfile(),
                shapeSummary = "focused groove line",
                summary = "groove profile",
                details = "details"
            };

            var instance = store.CommitDraft(draft, duplicate: false);

            Assert.That(instance, Is.Not.Null);
            Assert.That(store.GetComposition().groove, Is.Not.Null);
            Assert.That(store.GetComposition().groove.density, Is.EqualTo(1.1f));
            Assert.That(store.GetComposition().GetPatternId(CompositionPhase.Groove), Is.EqualTo(instance.patternId));
            Assert.That(published.HasValue, Is.True);
            Assert.That(published.Value.PatternId, Is.EqualTo(instance.patternId));
        }

        [Test]
        public void CommitDraft_ForGroove_MarksMelodyAndPercussionScheduleDirty_AndPublishesInvalidationEvents()
        {
            var store = new SessionStore();
            var melody = CreatePhasePattern("melody-1", PatternType.Melody);
            var percussion = CreatePhasePattern("perc-1", PatternType.Percussion);

            store.State.patterns.Add(melody);
            store.State.patterns.Add(percussion);
            store.GetComposition().SetPatternId(CompositionPhase.Melody, melody.id);
            store.GetComposition().SetPatternId(CompositionPhase.Percussion, percussion.id);

            var published = new List<PhaseInvalidationChangedEvent>();
            store.EventBus.Subscribe<PhaseInvalidationChangedEvent>(evt => published.Add(evt));

            var draft = new DraftResult
            {
                success = true,
                type = PatternType.Groove,
                name = "Groove-01",
                bars = GuidedDefaults.Bars,
                tempoBase = GuidedDefaults.Tempo,
                key = GuidedDefaults.Key,
                groupId = "lofi",
                presetId = "lofi-piano",
                points = new List<Vector2> { Vector2.zero, Vector2.right * 0.4f, new Vector2(0.8f, 0.2f) },
                renderRotation = Quaternion.identity,
                hasRenderRotation = true,
                spawnPosition = Vector3.zero,
                derivedSequence = new DerivedSequence
                {
                    kind = "groove",
                    totalSteps = GuidedDefaults.Bars * AppStateFactory.BarSteps,
                    grooveProfile = new GrooveProfile
                    {
                        density = 1.1f,
                        syncopation = 0.22f,
                        swing = 0.16f,
                        quantizeGrid = 16,
                        accentCurve = new[] { 1f, 0.72f, 0.9f, 0.72f }
                    }
                },
                tags = new List<string> { "groove" },
                color = Color.yellow,
                shapeProfile = new ShapeProfile(),
                soundProfile = new SoundProfile(),
                shapeSummary = "focused groove line",
                summary = "groove profile",
                details = "details"
            };

            store.CommitDraft(draft, duplicate: false);

            Assert.That(store.GetPhaseInvalidation(CompositionPhase.Melody), Is.EqualTo(PhaseInvalidationKind.ScheduleDirty));
            Assert.That(store.GetPhaseInvalidation(CompositionPhase.Percussion), Is.EqualTo(PhaseInvalidationKind.ScheduleDirty));
            Assert.That(published.Any(evt => evt.Phase == CompositionPhase.Melody && evt.Kind == PhaseInvalidationKind.ScheduleDirty), Is.True);
            Assert.That(published.Any(evt => evt.Phase == CompositionPhase.Percussion && evt.Kind == PhaseInvalidationKind.ScheduleDirty), Is.True);
        }

        [Test]
        public void GuidedDemoComposition_SeedsFoundation_LeavesPhasesEmpty()
        {
            var store = new SessionStore();
            var state = GuidedDemoComposition.CreateDemoState(store);

            Assert.That(state.guidedMode, Is.True);
            Assert.That(state.patterns, Is.Empty);
            Assert.That(state.instances, Is.Empty);
            Assert.That(state.composition, Is.Not.Null);
            Assert.That(state.composition.currentPhase, Is.EqualTo(CompositionPhase.Harmony));
            Assert.That(state.composition.progression, Is.Not.Null);
            Assert.That(state.composition.phasePatternIds, Is.Empty);
        }

        [Test]
        public void ClearPhase_ForGroove_RemovesPatternReference_AndProfile()
        {
            var store = new SessionStore();
            var pattern = CreatePhasePattern("groove-1", PatternType.Groove);
            store.State.patterns.Add(pattern);
            store.State.instances.Add(new PatternInstance(pattern.id, store.State.activeSceneId, Vector3.zero, 0.3f));
            store.State.scenes[0].instanceIds.Add(store.State.instances[0].id);
            store.GetComposition().SetPatternId(CompositionPhase.Groove, pattern.id);
            store.GetComposition().groove = new GrooveProfile
            {
                density = 1.2f,
                syncopation = 0.18f,
                swing = 0.11f,
                quantizeGrid = 16,
                accentCurve = new[] { 1f, 0.7f, 0.85f, 0.7f }
            };

            store.ClearPhase(CompositionPhase.Groove);

            Assert.That(store.GetComposition().GetPatternId(CompositionPhase.Groove), Is.Null);
            Assert.That(store.GetComposition().groove, Is.Null);
            Assert.That(store.GetPattern(pattern.id), Is.Null);
            Assert.That(store.State.instances, Is.Empty);
        }

        [Test]
        public void ClearPhase_ForHarmony_ResetsProgression_AndMarksDependentPhasesPending()
        {
            var store = new SessionStore();
            var harmony = CreatePhasePattern("harmony-1", PatternType.Harmony);
            var melody = CreatePhasePattern("melody-1", PatternType.Melody);
            var bass = CreatePhasePattern("bass-1", PatternType.Bass);
            var published = new List<PhaseInvalidationChangedEvent>();

            store.State.patterns.Add(harmony);
            store.State.patterns.Add(melody);
            store.State.patterns.Add(bass);
            store.GetComposition().SetPatternId(CompositionPhase.Harmony, harmony.id);
            store.GetComposition().SetPatternId(CompositionPhase.Melody, melody.id);
            store.GetComposition().SetPatternId(CompositionPhase.Bass, bass.id);
            store.EventBus.Subscribe<PhaseInvalidationChangedEvent>(evt => published.Add(evt));

            store.GetComposition().progression.chords[0].flavor = "maj7";

            store.ClearPhase(CompositionPhase.Harmony);

            Assert.That(store.GetComposition().GetPatternId(CompositionPhase.Harmony), Is.Null);
            Assert.That(store.GetPattern(harmony.id), Is.Null);
            Assert.That(store.GetComposition().progression.chords[0].flavor, Is.EqualTo(GuidedDefaults.CreateDefaultProgression().chords[0].flavor));
            Assert.That(store.GetPhaseInvalidation(CompositionPhase.Melody), Is.EqualTo(PhaseInvalidationKind.AsyncRederive));
            Assert.That(store.GetPhaseInvalidation(CompositionPhase.Bass), Is.EqualTo(PhaseInvalidationKind.AsyncRederive));
            Assert.That(published.Any(evt => evt.Phase == CompositionPhase.Melody && evt.Kind == PhaseInvalidationKind.AsyncRederive), Is.True);
            Assert.That(published.Any(evt => evt.Phase == CompositionPhase.Bass && evt.Kind == PhaseInvalidationKind.AsyncRederive), Is.True);
        }

        private static PatternDefinition CreatePhasePattern(string id, PatternType type)
        {
            return new PatternDefinition
            {
                id = id,
                type = type,
                name = id,
                bars = GuidedDefaults.Bars,
                tempoBase = GuidedDefaults.Tempo,
                key = GuidedDefaults.Key,
                groupId = "lofi",
                presetId = "lofi-piano",
                points = new List<Vector2> { Vector2.zero, Vector2.right * 0.4f, new Vector2(0.8f, 0.2f) },
                renderRotation = Quaternion.identity,
                hasRenderRotation = true,
                derivedSequence = new DerivedSequence
                {
                    kind = type == PatternType.Groove ? "groove" : "melody",
                    totalSteps = GuidedDefaults.Bars * AppStateFactory.BarSteps,
                    grooveProfile = type == PatternType.Groove
                        ? new GrooveProfile
                        {
                            density = 1f,
                            syncopation = 0.1f,
                            swing = 0.1f,
                            quantizeGrid = 8,
                            accentCurve = new[] { 1f, 0.7f, 0.85f, 0.7f }
                        }
                        : null
                },
                tags = new List<string> { "test" },
                color = Color.white,
                shapeProfile = new ShapeProfile(),
                soundProfile = new SoundProfile(),
                shapeSummary = "test shape",
                summary = "summary",
                details = "details"
            };
        }
    }
}
#endif
