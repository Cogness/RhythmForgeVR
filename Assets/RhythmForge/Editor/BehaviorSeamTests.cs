#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Audio;
using RhythmForge.Core.Data;
using RhythmForge.Core.PatternBehavior;
using RhythmForge.Core.PatternBehavior.Behaviors;
using RhythmForge.Sequencer;

namespace RhythmForge.Editor
{
    public class BehaviorSeamTests
    {
        [Test]
        public void BassBehavior_Schedule_UsesPlayBass()
        {
            var behavior = new BassBehavior();
            var dispatcher = new FakeAudioDispatcher();
            var pattern = new PatternDefinition
            {
                id = "bass-1",
                type = PatternType.Bass,
                name = "Bass",
                groupId = "lofi",
                presetId = "trap-bass",
                color = Color.red,
                shapeProfile = new ShapeProfile(),
                soundProfile = new SoundProfile(),
                derivedSequence = new DerivedSequence
                {
                    kind = "melody",
                    totalSteps = GuidedDefaults.Bars * AppStateFactory.BarSteps,
                    notes = new System.Collections.Generic.List<MelodyNote>
                    {
                        new MelodyNote { step = 0, midi = 43, durationSteps = 8, velocity = 0.5f, glide = 0f }
                    }
                }
            };

            behavior.Schedule(new PatternSchedulingContext
            {
                pattern = pattern,
                instance = new PatternInstance(pattern.id, "scene-a", Vector3.zero, 0.3f),
                localStep = 0,
                stepDuration = 0.15f,
                scheduledTime = 1d,
                sound = new SoundProfile(),
                preset = InstrumentPresets.Get("trap-bass"),
                group = InstrumentGroups.Get("lofi"),
                appState = AppStateFactory.CreateEmpty(),
                audioDispatcher = dispatcher
            });

            Assert.That(dispatcher.PlayBassCalls, Is.EqualTo(1));
            Assert.That(dispatcher.PlayMelodyCalls, Is.EqualTo(0));
        }

        [Test]
        public void GrooveBehavior_AdjustVisualSpec_UsesDedicatedGrooveProfile()
        {
            var profile = ScriptableObject.CreateInstance<VisualGrammarProfileAsset>();
            profile.melodyLine.markerScaleAdd = 0f;
            profile.groove.markerScaleAdd = 0.3f;
            VisualGrammarProfiles.SetActiveProfile(profile);

            try
            {
                var behavior = new GrooveBehavior();
                var baseSpec = new PlaybackVisualSpec { markerScale = 0.1f, haloStrength = 0.2f, secondaryStrength = 0.1f };
                var sound = new SoundProfile();

                var actual = behavior.AdjustVisualSpec(baseSpec, sound);
                var expected = profile.groove.Apply(baseSpec, sound);

                Assert.That(actual.markerScale, Is.EqualTo(expected.markerScale).Within(0.0001f));
            }
            finally
            {
                VisualGrammarProfiles.SetActiveProfile(null);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void BassBehavior_AdjustVisualSpec_UsesDedicatedBassProfile()
        {
            var profile = ScriptableObject.CreateInstance<VisualGrammarProfileAsset>();
            profile.melodyLine.markerScaleAdd = 0f;
            profile.bass.markerScaleAdd = 0.25f;
            VisualGrammarProfiles.SetActiveProfile(profile);

            try
            {
                var behavior = new BassBehavior();
                var baseSpec = new PlaybackVisualSpec { markerScale = 0.1f, haloStrength = 0.2f, secondaryStrength = 0.1f };
                var sound = new SoundProfile();

                var actual = behavior.AdjustVisualSpec(baseSpec, sound);
                var expected = profile.bass.Apply(baseSpec, sound);

                Assert.That(actual.markerScale, Is.EqualTo(expected.markerScale).Within(0.0001f));
            }
            finally
            {
                VisualGrammarProfiles.SetActiveProfile(null);
                Object.DestroyImmediate(profile);
            }
        }

        private sealed class FakeAudioDispatcher : IAudioDispatcher
        {
            public int PlayBassCalls { get; private set; }
            public int PlayMelodyCalls { get; private set; }

            public void PlayDrum(InstrumentPreset preset, string lane, float velocity, float pan, float brightness, float depth, float fxSend, SoundProfile soundProfile, float startDelay = 0f)
            {
            }

            public void PlayMelody(InstrumentPreset preset, int midi, float velocity, float duration, float pan, float brightness, float depth, float fxSend, SoundProfile soundProfile, float glide = 0f, float startDelay = 0f)
            {
                PlayMelodyCalls++;
            }

            public void PlayBass(InstrumentPreset preset, int midi, float velocity, float duration, float pan, float brightness, float depth, float fxSend, SoundProfile soundProfile, float glide = 0f, float startDelay = 0f)
            {
                PlayBassCalls++;
            }

            public void PlayChord(InstrumentPreset preset, System.Collections.Generic.List<int> chord, float velocity, float duration, float pan, float brightness, float depth, float fxSend, SoundProfile soundProfile)
            {
            }
        }
    }
}
#endif
