#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;
using RhythmForge.Sequencer;

namespace RhythmForge.Editor
{
    public class PlaybackVisualTrackerDirectTests
    {
        [Test]
        public void RecordTrigger_CreatesPulseThatExpiresWhenPruned()
        {
            var tracker = new PlaybackVisualTracker();

            tracker.RecordTrigger("instance-1", scheduledTime: 10.25d, activeDuration: 0.4f, dspTime: 10d, visualTime: 20d);

            Assert.That(tracker.GetPulse("instance-1", 20.25d), Is.EqualTo(1f).Within(0.001f));
            Assert.That(tracker.GetPulse("instance-1", 20.49d), Is.GreaterThan(0f));

            tracker.Prune(21d, 10d, 0.25f);

            Assert.That(tracker.GetPulse("instance-1", 21d), Is.EqualTo(0f));
        }

        [Test]
        public void TryGetPlaybackVisualState_UsesScheduledStepAndSustainWindow()
        {
            var store = new SessionStore();
            var pattern = new PatternDefinition
            {
                id = "pattern-1",
                type = PatternType.RhythmLoop,
                name = "Beat",
                groupId = "lofi",
                presetId = "lofi-drums",
                color = Color.white,
                shapeProfile = new ShapeProfile(),
                soundProfile = new SoundProfile(),
                derivedSequence = new DerivedSequence
                {
                    kind = "rhythm",
                    totalSteps = AppStateFactory.BarSteps,
                    events = new List<RhythmEvent>
                    {
                        new RhythmEvent { step = 0, lane = "kick", velocity = 1f }
                    }
                }
            };
            store.State.patterns.Add(pattern);

            var instance = new PatternInstance(pattern.id, "scene-a", Vector3.zero, 0.3f);
            store.State.instances.Add(instance);
            store.State.scenes[0].instanceIds.Add(instance.id);

            var tracker = new PlaybackVisualTracker();
            var transport = new Transport
            {
                playing = true,
                mode = "scene",
                sceneStep = 0,
                playbackSceneId = "scene-a"
            };

            tracker.RecordScheduledTransportStep(transport, 5d, "scene-a");
            tracker.RecordTrigger(instance.id, scheduledTime: 5d, activeDuration: 0.5f, dspTime: 5d, visualTime: 50d);

            bool found = tracker.TryGetPlaybackVisualState(
                pattern,
                instance.id,
                store,
                transport,
                "scene-a",
                dspTime: 5.125d,
                visualTime: 50.1d,
                stepDuration: 0.25f,
                out var state);

            Assert.That(found, Is.True);
            Assert.That(state.isActive, Is.True);
            Assert.That(state.pulse, Is.GreaterThan(0f));
            Assert.That(state.sustainAmount, Is.GreaterThan(0f));
            Assert.That(state.phase, Is.GreaterThan(0f));
            Assert.That(state.phase, Is.LessThan(0.1f));
            Assert.That(state.playbackSceneId, Is.EqualTo("scene-a"));
        }
    }
}
#endif
