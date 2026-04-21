#if UNITY_EDITOR
using NUnit.Framework;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.Session;

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
    }
}
#endif
