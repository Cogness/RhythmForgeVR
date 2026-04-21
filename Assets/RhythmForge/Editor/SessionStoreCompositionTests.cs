#if UNITY_EDITOR
using NUnit.Framework;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.Session;
using UnityEngine;
using System.Collections.Generic;

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
    }
}
#endif
