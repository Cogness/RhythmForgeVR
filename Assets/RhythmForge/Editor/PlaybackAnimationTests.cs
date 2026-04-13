#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;
using RhythmForge.UI;
using SequencerComponent = RhythmForge.Sequencer.Sequencer;

namespace RhythmForge.Editor
{
    public class PlaybackAnimationTests
    {
        private static readonly MethodInfo ScheduleCurrentStepMethod =
            typeof(SequencerComponent).GetMethod("ScheduleCurrentStep", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void TryGetPlaybackVisualState_ReturnsFalse_WhenTransportIsStopped()
        {
            var store = new SessionStore();
            var pattern = CreatePattern("rhythm-pattern", PatternType.RhythmLoop, new DerivedSequence
            {
                kind = "rhythm",
                totalSteps = AppStateFactory.BarSteps,
                events = new System.Collections.Generic.List<RhythmEvent>
                {
                    new RhythmEvent { step = 0, lane = "kick", velocity = 1f }
                }
            });
            var instance = AddPatternInstance(store, pattern, "scene-a");

            var sequencer = CreateSequencer(store);
            try
            {
                Assert.That(sequencer.TryGetPlaybackVisualState(pattern, instance.id, out var state), Is.False);
                Assert.That(state.isActive, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(sequencer.gameObject);
            }
        }

        [Test]
        public void MelodyVisualState_RemainsActive_ForScheduledDuration()
        {
            var store = new SessionStore();
            var pattern = CreatePattern("melody-pattern", PatternType.MelodyLine, new DerivedSequence
            {
                kind = "melody",
                totalSteps = AppStateFactory.BarSteps,
                notes = new System.Collections.Generic.List<MelodyNote>
                {
                    new MelodyNote { step = 0, midi = 64, durationSteps = 2, velocity = 1f, glide = 0f }
                }
            });
            var instance = AddPatternInstance(store, pattern, "scene-a");

            var sequencer = CreateSequencer(store);
            try
            {
                sequencer.Play();
                double startTime = sequencer.CurrentTransport.nextNoteTime;
                InvokeScheduleCurrentStep(sequencer, startTime);

                sequencer.DebugDspTime = startTime;
                sequencer.DebugVisualTime = startTime;
                Assert.That(sequencer.TryGetPlaybackVisualState(pattern, instance.id, out var onsetState), Is.True);
                Assert.That(onsetState.isActive, Is.True);

                float stepDuration = RhythmForge.Sequencer.SequencerClock.StepDuration(store.State.tempo);
                sequencer.DebugDspTime = startTime + stepDuration * 1.5f;
                sequencer.DebugVisualTime = startTime + stepDuration * 1.5f;
                Assert.That(sequencer.TryGetPlaybackVisualState(pattern, instance.id, out var sustainedState), Is.True);
                Assert.That(sustainedState.isActive, Is.True);

                sequencer.DebugDspTime = startTime + 0.8d;
                sequencer.DebugVisualTime = startTime + 0.8d;
                Assert.That(sequencer.TryGetPlaybackVisualState(pattern, instance.id, out var fadedState), Is.True);
                Assert.That(fadedState.isActive, Is.False);
                Assert.That(fadedState.sustainAmount, Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(sequencer.gameObject);
            }
        }

        [Test]
        public void HarmonyVisualState_RemainsActive_ForChordSustainWindow()
        {
            var store = new SessionStore();
            var pattern = CreatePattern("harmony-pattern", PatternType.HarmonyPad, new DerivedSequence
            {
                kind = "harmony",
                totalSteps = AppStateFactory.BarSteps,
                chord = new System.Collections.Generic.List<int> { 60, 64, 67, 71 }
            });
            var instance = AddPatternInstance(store, pattern, "scene-a");

            var sequencer = CreateSequencer(store);
            try
            {
                sequencer.Play();
                double startTime = sequencer.CurrentTransport.nextNoteTime;
                InvokeScheduleCurrentStep(sequencer, startTime);

                sequencer.DebugDspTime = startTime + 0.3d;
                sequencer.DebugVisualTime = startTime + 0.3d;
                Assert.That(sequencer.TryGetPlaybackVisualState(pattern, instance.id, out var activeState), Is.True);
                Assert.That(activeState.isActive, Is.True);

                sequencer.DebugDspTime = startTime + 2.9d;
                sequencer.DebugVisualTime = startTime + 2.9d;
                Assert.That(sequencer.TryGetPlaybackVisualState(pattern, instance.id, out var expiredState), Is.True);
                Assert.That(expiredState.isActive, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(sequencer.gameObject);
            }
        }

        [Test]
        public void VisualState_UsesPlaybackScene_NotStoreActiveScene()
        {
            var store = new SessionStore();
            store.SetActiveScene("scene-b");

            var pattern = CreatePattern("scene-b-pattern", PatternType.RhythmLoop, new DerivedSequence
            {
                kind = "rhythm",
                totalSteps = AppStateFactory.BarSteps,
                events = new System.Collections.Generic.List<RhythmEvent>
                {
                    new RhythmEvent { step = 0, lane = "kick", velocity = 1f }
                }
            });
            var instance = AddPatternInstance(store, pattern, "scene-b");

            var sequencer = CreateSequencer(store);
            try
            {
                sequencer.Play();
                double startTime = sequencer.CurrentTransport.nextNoteTime;
                InvokeScheduleCurrentStep(sequencer, startTime);

                store.SetActiveScene("scene-a");
                sequencer.DebugDspTime = startTime;
                sequencer.DebugVisualTime = startTime;

                Assert.That(sequencer.TryGetPlaybackVisualState(pattern, instance.id, out var state), Is.True);
                Assert.That(state.playbackSceneId, Is.EqualTo("scene-b"));
            }
            finally
            {
                Object.DestroyImmediate(sequencer.gameObject);
            }
        }

        [Test]
        public void PlaybackVisualSpec_DiffersByPatternType()
        {
            var sound = new SoundProfile
            {
                brightness = 0.62f,
                body = 0.74f,
                releaseBias = 0.58f,
                modDepth = 0.54f,
                filterMotion = 0.46f,
                grooveInstability = 0.52f,
                stereoSpread = 0.68f,
                reverbBias = 0.63f,
                transientSharpness = 0.7f
            };

            var rhythm = RhythmForge.Sequencer.PlaybackVisualSpec.FromSoundProfile(PatternType.RhythmLoop, sound);
            var melody = RhythmForge.Sequencer.PlaybackVisualSpec.FromSoundProfile(PatternType.MelodyLine, sound);
            var harmony = RhythmForge.Sequencer.PlaybackVisualSpec.FromSoundProfile(PatternType.HarmonyPad, sound);

            Assert.That(rhythm.markerScale, Is.GreaterThan(harmony.markerScale));
            Assert.That(harmony.haloStrength, Is.GreaterThan(rhythm.haloStrength));
            Assert.That(melody.secondaryStrength, Is.GreaterThan(rhythm.secondaryStrength));
        }

        [Test]
        public void PlaybackMarker_UsesArcLengthSampling_OnUnevenSegments()
        {
            var root = new GameObject("MarkerRoot");
            try
            {
                var line = root.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 3;
                line.SetPosition(0, Vector3.zero);
                line.SetPosition(1, new Vector3(3f, 0f, 0f));
                line.SetPosition(2, new Vector3(3f, 1f, 0f));

                var markerObject = new GameObject("Marker");
                markerObject.transform.SetParent(root.transform, false);
                var marker = markerObject.AddComponent<PlaybackMarker>();
                marker.SetTarget(line, Color.white);
                marker.ApplyState(0.5f, 1f, 1f);

                Assert.That(marker.transform.localPosition.x, Is.EqualTo(2f).Within(0.05f));
                Assert.That(marker.transform.localPosition.y, Is.EqualTo(0f).Within(0.05f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PatternVisualizer_SetPlaybackState_ActivatesPlaybackChildren()
        {
            var material = new Material(Shader.Find("Sprites/Default"));
            var go = new GameObject("PatternVisualizerPlaybackTest");
            try
            {
                var visualizer = go.AddComponent<PatternVisualizer>();
                var pattern = CreateVisualPattern(PatternType.HarmonyPad);
                var instance = new PatternInstance(pattern.id, "scene-a", Vector3.zero, 0.3f);
                visualizer.Initialize(pattern, instance, material);

                visualizer.SetPlaybackState(new RhythmForge.Sequencer.PatternPlaybackVisualState
                {
                    phase = 0.35f,
                    pulse = 0.8f,
                    isActive = true,
                    sustainAmount = 0.9f,
                    playbackSceneId = "scene-a",
                    visualSpec = RhythmForge.Sequencer.PlaybackVisualSpec.FromSoundProfile(pattern.type, pattern.soundProfile)
                });

                var halo = go.transform.Find("PlaybackHalo").GetComponent<LineRenderer>();
                var marker = go.transform.Find("PlaybackMarker");

                Assert.That(halo.enabled, Is.True);
                Assert.That(marker.gameObject.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(material);
            }
        }

        private static TestSequencer CreateSequencer(SessionStore store)
        {
            var go = new GameObject("PlaybackAnimationTests");
            var sequencer = go.AddComponent<TestSequencer>();
            sequencer.DebugDspTime = 100d;
            sequencer.DebugVisualTime = 100d;
            sequencer.Initialize(store);
            return sequencer;
        }

        private static void InvokeScheduleCurrentStep(TestSequencer sequencer, double time)
        {
            Assert.That(ScheduleCurrentStepMethod, Is.Not.Null);
            ScheduleCurrentStepMethod.Invoke(sequencer, new object[] { time });
        }

        private static PatternInstance AddPatternInstance(SessionStore store, PatternDefinition pattern, string sceneId)
        {
            store.State.patterns.Add(pattern);
            return store.SpawnPattern(pattern.id, sceneId, Vector3.zero, false);
        }

        private static PatternDefinition CreatePattern(string id, PatternType type, DerivedSequence sequence)
        {
            return new PatternDefinition
            {
                id = id,
                type = type,
                name = id,
                bars = 1,
                groupId = "lofi",
                presetId = "lofi-piano",
                color = Color.white,
                derivedSequence = sequence,
                soundProfile = new SoundProfile
                {
                    brightness = 0.55f,
                    body = 0.62f,
                    releaseBias = 0.46f,
                    modDepth = 0.34f,
                    filterMotion = 0.3f,
                    grooveInstability = 0.22f,
                    stereoSpread = 0.48f,
                    reverbBias = 0.44f,
                    transientSharpness = 0.5f
                }
            };
        }

        private static PatternDefinition CreateVisualPattern(PatternType type)
        {
            return new PatternDefinition
            {
                id = "visual-pattern",
                type = type,
                name = "Visual Pattern",
                bars = 1,
                groupId = "lofi",
                presetId = "lofi-pad",
                color = new Color(0.45f, 0.72f, 1f),
                points = new System.Collections.Generic.List<Vector2>
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0.2f),
                    new Vector2(0.8f, 1f),
                    new Vector2(0.2f, 0.85f)
                },
                shapeProfile = new ShapeProfile
                {
                    worldWidth = 0.22f,
                    worldHeight = 0.18f,
                    worldLength = 0.68f,
                    worldAverageSize = 0.2f,
                    worldMaxDimension = 0.22f
                },
                soundProfile = new SoundProfile
                {
                    brightness = 0.48f,
                    body = 0.72f,
                    releaseBias = 0.66f,
                    modDepth = 0.44f,
                    filterMotion = 0.42f,
                    stereoSpread = 0.7f,
                    reverbBias = 0.68f,
                    transientSharpness = 0.38f
                }
            };
        }

        private class TestSequencer : SequencerComponent
        {
            public double DebugDspTime { get; set; }
            public double DebugVisualTime { get; set; }

            protected override double GetDspTime() => DebugDspTime;
            protected override double GetVisualTimeSeconds() => DebugVisualTime;
        }
    }
}
#endif
