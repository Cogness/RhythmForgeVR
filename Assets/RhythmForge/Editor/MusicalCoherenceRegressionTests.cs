#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using RhythmForge;
using RhythmForge.Audio;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.Sequencing;
using RhythmForge.Core.Session;

namespace RhythmForge.Editor
{
    public class MusicalCoherenceRegressionTests
    {
        private static readonly FieldInfo VoiceCacheField =
            typeof(SamplePlayer).GetField("_voiceCache", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo PendingPlaysField =
            typeof(SamplePlayer).GetField("_pendingPlays", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo ActiveRenderCountField =
            typeof(SamplePlayer).GetField("_activeRenderCount", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo PoolField =
            typeof(SamplePlayer).GetField("_pool", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo SamplePlayerUpdateMethod =
            typeof(SamplePlayer).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo HandleGenreChangedMethod =
            typeof(RhythmForgeManager).GetMethod("HandleGenreChanged", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void GuidedElectronicHarmony_UsesProgressionRootsAndHarmonyRegister()
        {
            var points = CreatePoints();
            var metrics = new StrokeMetrics { length = 1.0f, averageSize = 0.42f, height = 1f, width = 1f };
            var shape = CreateHarmonyShape();
            var sound = new SoundProfile { reverbBias = 0.4f, filterMotion = 0.3f };
            var result = HarmonyDeriver.Derive(points, metrics, GuidedDefaults.Key, "electronic", shape, sound);
            var harmonyRange = RegisterPolicy.GetRange(PatternType.Harmony, "electronic");
            var expectedRoots = new[] { 67, 64, 60, 62, 67, 64, 60, 62 };

            Assert.That(result.bars, Is.EqualTo(8));
            Assert.That(result.derivedSequence.chordEvents, Has.Count.EqualTo(8));

            for (int i = 0; i < result.derivedSequence.chordEvents.Count; i++)
            {
                var slot = result.derivedSequence.chordEvents[i];
                Assert.That(slot.rootMidi, Is.EqualTo(expectedRoots[i]));
                Assert.That(slot.voicing.Count, Is.GreaterThan(0));
                foreach (var midi in slot.voicing)
                    Assert.That(midi, Is.InRange(harmonyRange.min, harmonyRange.max));
            }
        }

        [TestCase("newage")]
        [TestCase("jazz")]
        public void LegacyGenreHarmony_UsesChordEventsWithinExpectedRegisters(string genreId)
        {
            var genre = GenreRegistry.Get(genreId);
            var points = CreatePoints();
            var metrics = new StrokeMetrics { length = 1.0f, averageSize = 0.42f, height = 1f, width = 1f };
            var shape = CreateHarmonyShape();
            var sound = new SoundProfile { reverbBias = 0.4f, filterMotion = 0.3f };
            var result = genre.HarmonyDeriver.Derive(points, metrics, "C major", shape, sound, genre);

            var harmonyRange = RegisterPolicy.GetRange(PatternType.Harmony, genreId);
            Assert.That(result.derivedSequence.chordEvents, Is.Not.Null);
            Assert.That(result.derivedSequence.chordEvents, Has.Count.EqualTo(result.bars));

            for (int i = 0; i < result.derivedSequence.chordEvents.Count; i++)
            {
                var slot = result.derivedSequence.chordEvents[i];
                Assert.That(slot, Is.Not.Null);
                Assert.That(slot.voicing, Is.Not.Empty);
                foreach (var midi in slot.voicing)
                    Assert.That(midi, Is.InRange(harmonyRange.min, harmonyRange.max));
            }
        }

        [Test]
        public void HarmonicContextProvider_IsThreadLocal()
        {
            try
            {
                HarmonicContextProvider.Set(new HarmonicContext
                {
                    rootMidi = 65,
                    chordTones = new List<int> { 65, 69, 72 },
                    flavor = "sus2"
                });
                HarmonicContextProvider.SetProgression(GuidedDefaults.CreateDefaultProgression());

                HarmonicContext workerContext = null;
                ChordProgression workerProgression = null;

                Task.Run(() =>
                {
                    workerContext = HarmonicContextProvider.Current;
                    workerProgression = HarmonicContextProvider.CurrentProgression;
                }).Wait();

                Assert.That(workerContext, Is.Not.Null);
                Assert.That(workerContext.HasChord, Is.False);
                Assert.That(workerProgression, Is.Null);
            }
            finally
            {
                HarmonicContextProvider.Clear();
            }
        }

        [Test]
        public void SessionStore_SetGenre_RederivesPatterns_AndPropagatesHarmonyContext()
        {
            var store = new SessionStore();
            var state = AppStateFactory.CreateEmpty();
            state.key = "C major";
            state.activeGenreId = "electronic";
            state.patterns = new List<PatternDefinition>
            {
                CreatePattern("h1", PatternType.HarmonyPad, CreateHarmonyShape(), CreatePoints()),
                CreatePattern("m1", PatternType.MelodyLine, CreateMelodyShape(), CreatePoints()),
                CreatePattern("m2", PatternType.MelodyLine, CreateMelodyShape(), CreatePoints()),
                CreatePattern("m3", PatternType.MelodyLine, CreateMelodyShape(), CreatePoints())
            };

            store.LoadState(state);

            bool completed = false;
            store.OnGenreRederived += _ => completed = true;
            store.SetGenre("newage");

            DateTime deadline = DateTime.UtcNow.AddSeconds(3);
            while (!completed && DateTime.UtcNow < deadline)
            {
                store.Tick();
                Thread.Sleep(10);
            }
            store.Tick();

            Assert.That(completed, Is.True);

            var harmony = store.GetPattern("h1");
            var melody0 = store.GetPattern("m1");
            var melody1 = store.GetPattern("m2");
            var melody2 = store.GetPattern("m3");

            Assert.That(harmony.derivedSequence.chordEvents, Is.Not.Null);
            Assert.That(harmony.derivedSequence.chordEvents, Is.Not.Empty);

            foreach (var melody in new[] { melody0, melody1, melody2 })
            {
                CollectionAssert.Contains(melody.tags, "lead");
                Assert.That(melody.derivedSequence.notes, Is.Not.Empty);

                foreach (var note in melody.derivedSequence.notes)
                {
                    if (note.step % 4 != 0)
                        continue;

                    Assert.That(ContainsPitchClass(harmony.derivedSequence.chordEvents[0].voicing, note.midi), Is.True);
                }
            }
        }

        [Test]
        public void SamplePlayer_InvalidateAll_ClearsPendingWork_AndDropsStaleRenders()
        {
            var go = new GameObject("SamplePlayerInvalidateAll");
            var player = go.AddComponent<SamplePlayer>();
            player.Configure();

            try
            {
                player.PlayNote(
                    InstrumentPresets.Get("dream-pad"),
                    60,
                    0.72f,
                    1.8f,
                    0f,
                    0.52f,
                    0.8f,
                    0.42f,
                    CreateSoundProfile(),
                    PatternType.HarmonyPad);

                Assert.That(GetPendingPlays(player).Count, Is.GreaterThanOrEqualTo(1));

                player.InvalidateAll();

                Assert.That(GetVoiceCache(player).Count, Is.EqualTo(0));
                Assert.That(GetPendingPlays(player).Count, Is.EqualTo(0));

                DateTime deadline = DateTime.UtcNow.AddSeconds(3);
                while (GetActiveRenderCount(player) > 0 && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(20);
                    SamplePlayerUpdateMethod.Invoke(player, null);
                }
                SamplePlayerUpdateMethod.Invoke(player, null);

                Assert.That(GetActiveRenderCount(player), Is.EqualTo(0));
                Assert.That(GetVoiceCache(player).Count, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SamplePlayer_RefreshPendingWork_KeepsCacheWhileClearingQueuedPlayback()
        {
            var go = new GameObject("SamplePlayerRefreshPendingWork");
            var player = go.AddComponent<SamplePlayer>();
            player.Configure();

            try
            {
                player.PlayNote(
                    InstrumentPresets.Get("dream-pad"),
                    60,
                    0.72f,
                    1.8f,
                    0f,
                    0.52f,
                    0.8f,
                    0.42f,
                    CreateSoundProfile(),
                    PatternType.HarmonyPad);

                DateTime deadline = DateTime.UtcNow.AddSeconds(3);
                while (GetVoiceCache(player).Count == 0 && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(20);
                    SamplePlayerUpdateMethod.Invoke(player, null);
                }

                Assert.That(GetVoiceCache(player).Count, Is.GreaterThan(0));

                player.PlayNote(
                    InstrumentPresets.Get("dream-pad"),
                    64,
                    0.72f,
                    1.6f,
                    0f,
                    0.52f,
                    0.8f,
                    0.42f,
                    CreateSoundProfile(),
                    PatternType.HarmonyPad);

                Assert.That(GetPendingPlays(player).Count, Is.GreaterThanOrEqualTo(1));

                player.RefreshPendingWork();

                Assert.That(GetPendingPlays(player).Count, Is.EqualTo(0));
                Assert.That(GetVoiceCache(player).Count, Is.GreaterThan(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GenreChangedEvent_ReroutesSamplePlayerToNewMixerGroup()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/RhythmForge/Audio/RhythmForgeMixer.mixer");
            Assert.That(mixer, Is.Not.Null);

            var playerGo = new GameObject("MixerRoutePlayer");
            var player = playerGo.AddComponent<SamplePlayer>();
            var engineGo = new GameObject("MixerRouteEngine");
            var engine = engineGo.AddComponent<AudioEngine>();
            var managerGo = new GameObject("MixerRouteManager");
            var manager = managerGo.AddComponent<RhythmForgeManager>();

            try
            {
                player.Configure(mixer);
                engine.Configure(player, mixer);
                manager.Configure(
                    new ManagerSubsystems { audioEngine = engine, samplePlayer = player },
                    new ManagerPanels(),
                    null,
                    null);

                HandleGenreChangedMethod.Invoke(manager, new object[] { new GenreChangedEvent("electronic", "newage") });

                var pool = (IList)PoolField.GetValue(player);
                Assert.That(pool.Count, Is.GreaterThan(0));

                var source = (AudioSource)pool[0];
                Assert.That(source.outputAudioMixerGroup, Is.Not.Null);
                Assert.That(source.outputAudioMixerGroup.name, Is.EqualTo("NewAge"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerGo);
                UnityEngine.Object.DestroyImmediate(engineGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
            }
        }

        private static PatternDefinition CreatePattern(string id, PatternType type, ShapeProfile shapeProfile, List<Vector2> points)
        {
            return new PatternDefinition
            {
                id = id,
                type = type,
                name = id,
                bars = 2,
                tempoBase = 68f,
                key = "C major",
                groupId = "electronic",
                genreId = "electronic",
                presetId = "placeholder",
                points = new List<Vector2>(points),
                derivedSequence = new DerivedSequence { kind = type == PatternType.HarmonyPad ? "harmony" : "melody", totalSteps = AppStateFactory.BarSteps },
                tags = new List<string>(),
                color = Color.white,
                shapeProfile = shapeProfile,
                soundProfile = CreateSoundProfile(),
                shapeSummary = id,
                summary = id,
                details = id
            };
        }

        private static ShapeProfile CreateHarmonyShape()
        {
            return new ShapeProfile
            {
                circularity = 0.62f,
                angularity = 0.28f,
                symmetry = 0.56f,
                verticalSpan = 0.44f,
                horizontalSpan = 0.38f,
                pathLength = 0.64f,
                speedVariance = 0.24f,
                curvatureMean = 0.32f,
                curvatureVariance = 0.12f,
                centroidHeight = 0.16f,
                tiltSigned = 0.24f,
                wobble = 0.08f
            };
        }

        private static ShapeProfile CreateMelodyShape()
        {
            return new ShapeProfile
            {
                circularity = 0.34f,
                angularity = 0.26f,
                symmetry = 0.42f,
                verticalSpan = 0.74f,
                horizontalSpan = 0.48f,
                pathLength = 0.72f,
                speedVariance = 0.22f,
                curvatureMean = 0.38f,
                curvatureVariance = 0.14f,
                centroidHeight = 0.48f,
                tiltSigned = 0.06f,
                wobble = 0.12f
            };
        }

        private static SoundProfile CreateSoundProfile()
        {
            return new SoundProfile
            {
                body = 0.5f,
                brightness = 0.42f,
                drive = 0.2f,
                releaseBias = 0.52f,
                transientSharpness = 0.3f,
                resonance = 0.24f,
                attackBias = 0.24f,
                detune = 0.22f,
                modDepth = 0.26f,
                stereoSpread = 0.4f,
                waveMorph = 0.32f,
                filterMotion = 0.24f,
                delayBias = 0.18f,
                reverbBias = 0.4f
            };
        }

        private static List<Vector2> CreatePoints()
        {
            return new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(0.45f, 0.85f),
                new Vector2(1f, 0.2f),
                new Vector2(0.7f, 0.9f)
            };
        }

        private static bool ContainsPitchClass(List<int> pitches, int midi)
        {
            int targetClass = ((midi % 12) + 12) % 12;
            for (int i = 0; i < pitches.Count; i++)
            {
                if ((((pitches[i] % 12) + 12) % 12) == targetClass)
                    return true;
            }

            return false;
        }

        private static IDictionary GetVoiceCache(SamplePlayer player)
        {
            Assert.That(VoiceCacheField, Is.Not.Null);
            return (IDictionary)VoiceCacheField.GetValue(player);
        }

        private static IList GetPendingPlays(SamplePlayer player)
        {
            Assert.That(PendingPlaysField, Is.Not.Null);
            return (IList)PendingPlaysField.GetValue(player);
        }

        private static int GetActiveRenderCount(SamplePlayer player)
        {
            Assert.That(ActiveRenderCountField, Is.Not.Null);
            return (int)ActiveRenderCountField.GetValue(player);
        }
    }
}
#endif
