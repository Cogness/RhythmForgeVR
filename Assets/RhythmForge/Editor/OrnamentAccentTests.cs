#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Audio;
using RhythmForge.Core.Data;
using RhythmForge.Core.PatternBehavior;
using RhythmForge.Core.PatternBehavior.Behaviors;
using RhythmForge.Sequencer;

namespace RhythmForge.Editor
{
    public class OrnamentAccentTests
    {
        [Test]
        public void RhythmFlags_AddGhostAndFlamHits()
        {
            var dispatcher = new FakeAudioDispatcher();
            var behavior = new MusicalShapeBehavior();
            var context = CreateContext(dispatcher, BuildRhythmPattern());

            context.localStep = 1;
            behavior.Schedule(context);
            context.localStep = 7;
            behavior.Schedule(context);

            Assert.That(dispatcher.DrumCalls.Count, Is.EqualTo(2));
            Assert.That(dispatcher.DrumCalls[0].lane, Is.EqualTo("kick"));
            Assert.That(dispatcher.DrumCalls[1].lane, Is.EqualTo("perc"));
        }

        [Test]
        public void MelodyFlags_AddPassingToneAndAccentStab()
        {
            var dispatcher = new FakeAudioDispatcher();
            var behavior = new MusicalShapeBehavior();
            var context = CreateContext(dispatcher, BuildMelodyPattern());

            context.localStep = 1;
            behavior.Schedule(context);
            context.localStep = 5;
            behavior.Schedule(context);

            Assert.That(dispatcher.MelodyCalls.Count, Is.EqualTo(2));
            Assert.That(dispatcher.MelodyCalls[0].midi, Is.EqualTo(62));
            Assert.That(dispatcher.MelodyCalls[1].midi, Is.EqualTo(68));
        }

        [Test]
        public void HarmonyFlags_AddAccentStabAndShimmerLayer()
        {
            var dispatcher = new FakeAudioDispatcher();
            var behavior = new MusicalShapeBehavior();
            var context = CreateContext(dispatcher, BuildHarmonyPattern());

            context.localStep = 0;
            behavior.Schedule(context);

            Assert.That(dispatcher.ChordCalls.Count, Is.EqualTo(2));
            Assert.That(dispatcher.MelodyCalls.Count, Is.EqualTo(1));
            Assert.That(dispatcher.MelodyCalls[0].midi, Is.EqualTo(67));
        }

        private static PatternSchedulingContext CreateContext(FakeAudioDispatcher dispatcher, PatternDefinition pattern)
        {
            return new PatternSchedulingContext
            {
                pattern = pattern,
                instance = new PatternInstance(pattern.id, "scene-a", Vector3.zero, 0.3f),
                localStep = 0,
                stepDuration = 0.25f,
                scheduledTime = 0.0,
                sound = new SoundProfile(),
                preset = InstrumentPresets.Get("lofi-piano"),
                group = new InstrumentGroup { busFx = new GroupBusFx() },
                transport = new Transport(),
                appState = AppStateFactory.CreateEmpty(),
                audioDispatcher = dispatcher,
                presetLookup = InstrumentPresets.Get
            };
        }

        private static PatternDefinition BuildRhythmPattern()
        {
            return new PatternDefinition
            {
                id = "rhythm-shape",
                type = PatternType.RhythmLoop,
                shapeProfile3D = new ShapeProfile3D { ornamentFlag = true, accentFlag = true },
                musicalShape = new MusicalShape
                {
                    bondStrength = new Vector3(1f, 0f, 0f),
                    profile3D = new ShapeProfile3D { ornamentFlag = true, accentFlag = true },
                    facets = new DerivedShapeSequence
                    {
                        rhythm = new RhythmSequence
                        {
                            totalSteps = 16,
                            events = new List<RhythmEvent>
                            {
                                new RhythmEvent { step = 0, lane = "kick", velocity = 1f },
                                new RhythmEvent { step = 4, lane = "snare", velocity = 0.8f },
                                new RhythmEvent { step = 8, lane = "perc", velocity = 0.7f }
                            }
                        },
                        melody = new MelodySequence(),
                        harmony = new HarmonySequence()
                    }
                }
            };
        }

        private static PatternDefinition BuildMelodyPattern()
        {
            return new PatternDefinition
            {
                id = "melody-shape",
                type = PatternType.MelodyLine,
                shapeProfile3D = new ShapeProfile3D { ornamentFlag = true, accentFlag = true },
                musicalShape = new MusicalShape
                {
                    bondStrength = new Vector3(0f, 1f, 0f),
                    profile3D = new ShapeProfile3D { ornamentFlag = true, accentFlag = true },
                    facets = new DerivedShapeSequence
                    {
                        rhythm = new RhythmSequence(),
                        melody = new MelodySequence
                        {
                            totalSteps = 16,
                            notes = new List<MelodyNote>
                            {
                                new MelodyNote { step = 0, midi = 60, durationSteps = 2, velocity = 0.8f },
                                new MelodyNote { step = 4, midi = 64, durationSteps = 2, velocity = 0.9f }
                            }
                        },
                        harmony = new HarmonySequence()
                    }
                }
            };
        }

        private static PatternDefinition BuildHarmonyPattern()
        {
            return new PatternDefinition
            {
                id = "harmony-shape",
                type = PatternType.HarmonyPad,
                shapeProfile3D = new ShapeProfile3D { ornamentFlag = true, accentFlag = true },
                musicalShape = new MusicalShape
                {
                    bondStrength = new Vector3(0f, 0f, 1f),
                    profile3D = new ShapeProfile3D { ornamentFlag = true, accentFlag = true },
                    facets = new DerivedShapeSequence
                    {
                        rhythm = new RhythmSequence(),
                        melody = new MelodySequence(),
                        harmony = new HarmonySequence
                        {
                            totalSteps = 16,
                            events = new List<HarmonyEvent>
                            {
                                new HarmonyEvent
                                {
                                    step = 0,
                                    durationSteps = 16,
                                    rootMidi = 60,
                                    chord = new List<int> { 60, 64, 67 }
                                }
                            }
                        }
                    }
                }
            };
        }

        private sealed class FakeAudioDispatcher : IAudioDispatcher
        {
            public readonly List<(string lane, float velocity)> DrumCalls = new List<(string lane, float velocity)>();
            public readonly List<(int midi, float velocity)> MelodyCalls = new List<(int midi, float velocity)>();
            public readonly List<(List<int> chord, float velocity)> ChordCalls = new List<(List<int> chord, float velocity)>();

            public void PlayDrum(
                InstrumentPreset preset,
                string lane,
                float velocity,
                float gainTrim,
                float brightness,
                float reverbSend,
                float delaySend,
                SoundProfile soundProfile,
                string instanceId = null)
            {
                DrumCalls.Add((lane, velocity));
            }

            public void PlayMelody(
                InstrumentPreset preset,
                int midi,
                float velocity,
                float duration,
                float gainTrim,
                float brightness,
                float reverbSend,
                float delaySend,
                SoundProfile soundProfile,
                float glide = 0f,
                string instanceId = null)
            {
                MelodyCalls.Add((midi, velocity));
            }

            public void PlayChord(
                InstrumentPreset preset,
                List<int> chord,
                float velocity,
                float duration,
                float gainTrim,
                float brightness,
                float reverbSend,
                float delaySend,
                SoundProfile soundProfile,
                string instanceId = null)
            {
                ChordCalls.Add((new List<int>(chord), velocity));
            }
        }
    }
}
#endif
