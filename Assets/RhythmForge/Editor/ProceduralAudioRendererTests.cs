#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Audio;
using RhythmForge.Core.Data;

namespace RhythmForge.Editor
{
    public class ProceduralAudioRendererTests
    {
        private static readonly MethodInfo GetOrCreateClipMethod =
            typeof(SamplePlayer).GetMethod("GetOrCreateClip", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo VoiceCacheField =
            typeof(SamplePlayer).GetField("_voiceCache", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo MaxCachedClipsField =
            typeof(SamplePlayer).GetField("_maxCachedClips", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void RenderedVoices_AreFinite_NonSilent_AndMatchExpectedLength()
        {
            var drumSpec = VoiceSpecResolver.ResolveDrum(
                "kick",
                InstrumentPresets.Get("lofi-drums"),
                CreateProfile(body: 0.68f, brightness: 0.36f, drive: 0.28f, releaseBias: 0.54f,
                    transientSharpness: 0.44f, resonance: 0.22f),
                0.52f,
                0.18f);

            var melodySpec = VoiceSpecResolver.ResolveMelody(
                InstrumentPresets.Get("lofi-piano"),
                CreateProfile(body: 0.48f, brightness: 0.44f, drive: 0.24f, releaseBias: 0.42f,
                    transientSharpness: 0.34f, resonance: 0.28f, attackBias: 0.32f,
                    detune: 0.16f, modDepth: 0.18f, stereoSpread: 0.24f, waveMorph: 0.4f),
                64,
                0.72f,
                0.56f,
                0.22f,
                0.08f);

            var harmonySpec = VoiceSpecResolver.ResolveHarmony(
                InstrumentPresets.Get("dream-pad"),
                CreateProfile(body: 0.72f, brightness: 0.34f, drive: 0.16f, releaseBias: 0.8f,
                    transientSharpness: 0.28f, resonance: 0.3f, attackBias: 0.18f,
                    detune: 0.3f, modDepth: 0.42f, stereoSpread: 0.78f, waveMorph: 0.62f,
                    filterMotion: 0.48f, delayBias: 0.34f, reverbBias: 0.72f),
                57,
                1.8f,
                0.48f,
                0.42f);

            AssertHealthyClip(ProceduralSynthesizer.RenderDrum(drumSpec), ExpectedDrumSamples(drumSpec));
            AssertHealthyClip(ProceduralSynthesizer.RenderTone(melodySpec), ExpectedToneSamples(melodySpec));
            AssertHealthyClip(ProceduralSynthesizer.RenderTone(harmonySpec), ExpectedToneSamples(harmonySpec));
        }

        [Test]
        public void BrighterMelodySpec_ProducesBrighterOutput()
        {
            var preset = InstrumentPresets.Get("lofi-piano");

            var darkSpec = VoiceSpecResolver.ResolveMelody(
                preset,
                CreateProfile(body: 0.52f, brightness: 0.12f, drive: 0.2f, releaseBias: 0.36f,
                    transientSharpness: 0.28f, resonance: 0.16f, detune: 0.12f,
                    modDepth: 0.12f, stereoSpread: 0.18f, waveMorph: 0.22f, filterMotion: 0.1f),
                64,
                0.6f,
                0.36f,
                0.16f);

            var brightSpec = VoiceSpecResolver.ResolveMelody(
                preset,
                CreateProfile(body: 0.4f, brightness: 0.88f, drive: 0.24f, releaseBias: 0.36f,
                    transientSharpness: 0.46f, resonance: 0.42f, detune: 0.12f,
                    modDepth: 0.12f, stereoSpread: 0.18f, waveMorph: 0.62f, filterMotion: 0.44f),
                64,
                0.6f,
                0.72f,
                0.16f);

            float darkBrightness = HighFrequencyProxy(ProceduralSynthesizer.RenderTone(darkSpec));
            float brightBrightness = HighFrequencyProxy(ProceduralSynthesizer.RenderTone(brightSpec));

            Assert.That(brightBrightness, Is.GreaterThan(darkBrightness * 1.1f));
        }

        [TestCase("kick")]
        [TestCase("snare")]
        public void HigherBodyAndReleaseBias_IncreaseDrumWeightAndTail(string lane)
        {
            var preset = InstrumentPresets.Get("lofi-drums");

            var lightSpec = VoiceSpecResolver.ResolveDrum(
                lane,
                preset,
                CreateProfile(body: 0.18f, brightness: 0.52f, drive: 0.18f, releaseBias: 0.12f,
                    transientSharpness: 0.24f, resonance: 0.18f),
                0.5f,
                0.14f);

            var heavySpec = VoiceSpecResolver.ResolveDrum(
                lane,
                preset,
                CreateProfile(body: 0.84f, brightness: 0.42f, drive: 0.32f, releaseBias: 0.84f,
                    transientSharpness: 0.52f, resonance: 0.32f),
                0.5f,
                0.14f);

            var lightClip = ProceduralSynthesizer.RenderDrum(lightSpec);
            var heavyClip = ProceduralSynthesizer.RenderDrum(heavySpec);

            Assert.That(LowFrequencyProxy(heavyClip), Is.GreaterThan(LowFrequencyProxy(lightClip) * 1.08f));
            Assert.That(TailRms(heavyClip, 0.08f), Is.GreaterThan(TailRms(lightClip, 0.08f) * 1.2f));
        }

        [Test]
        public void HarmonyRender_IsWiderAndLongerThanMelodyRender()
        {
            var profile = CreateProfile(body: 0.62f, brightness: 0.38f, drive: 0.16f, releaseBias: 0.72f,
                transientSharpness: 0.28f, resonance: 0.26f, attackBias: 0.24f,
                detune: 0.34f, modDepth: 0.44f, stereoSpread: 0.82f, waveMorph: 0.54f,
                filterMotion: 0.4f, delayBias: 0.3f, reverbBias: 0.68f);

            var melodyClip = ProceduralSynthesizer.RenderTone(
                VoiceSpecResolver.ResolveMelody(InstrumentPresets.Get("dream-bell"), profile, 67, 0.9f, 0.52f, 0.34f));
            var harmonyClip = ProceduralSynthesizer.RenderTone(
                VoiceSpecResolver.ResolveHarmony(InstrumentPresets.Get("dream-pad"), profile, 67, 0.9f, 0.52f, 0.34f));

            Assert.That(harmonyClip.length, Is.GreaterThan(melodyClip.length + 0.2f));
            Assert.That(StereoWidth(harmonyClip), Is.GreaterThan(StereoWidth(melodyClip) * 1.12f));
        }

        [Test]
        public void PresetFamilies_ProduceDistinctTonalOutputs()
        {
            var profile = CreateProfile(body: 0.54f, brightness: 0.5f, drive: 0.22f, releaseBias: 0.52f,
                transientSharpness: 0.34f, resonance: 0.28f, attackBias: 0.24f,
                detune: 0.22f, modDepth: 0.24f, stereoSpread: 0.42f, waveMorph: 0.4f,
                filterMotion: 0.24f, delayBias: 0.2f, reverbBias: 0.3f);

            var lofiClip = ProceduralSynthesizer.RenderTone(
                VoiceSpecResolver.ResolveHarmony(InstrumentPresets.Get("lofi-pad"), profile, 60, 1.2f, 0.48f, 0.28f));
            var trapClip = ProceduralSynthesizer.RenderTone(
                VoiceSpecResolver.ResolveHarmony(InstrumentPresets.Get("trap-pad"), profile, 60, 1.2f, 0.48f, 0.28f));
            var dreamClip = ProceduralSynthesizer.RenderTone(
                VoiceSpecResolver.ResolveHarmony(InstrumentPresets.Get("dream-pad"), profile, 60, 1.2f, 0.48f, 0.28f));

            Assert.That(MeanAbsoluteDifference(lofiClip, trapClip), Is.GreaterThan(0.01f));
            Assert.That(MeanAbsoluteDifference(lofiClip, dreamClip), Is.GreaterThan(0.01f));
            Assert.That(MeanAbsoluteDifference(trapClip, dreamClip), Is.GreaterThan(0.01f));
        }

        [Test]
        public void SamplePlayer_ReusesCachedClip_ForIdenticalQuantizedSpec()
        {
            var player = CreatePlayer();
            try
            {
                var spec = VoiceSpecResolver.ResolveMelody(
                    InstrumentPresets.Get("lofi-piano"),
                    CreateProfile(body: 0.46f, brightness: 0.4f, drive: 0.18f, releaseBias: 0.38f,
                        transientSharpness: 0.28f, resonance: 0.2f),
                    62,
                    0.64f,
                    0.48f,
                    0.22f);

                var first = InvokeGetOrCreateClip(player, spec);
                var second = InvokeGetOrCreateClip(player, spec);

                Assert.That(second, Is.SameAs(first));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void SamplePlayer_DoesNotAliasDifferentVoiceBuckets()
        {
            var player = CreatePlayer();
            try
            {
                var darkSpec = VoiceSpecResolver.ResolveMelody(
                    InstrumentPresets.Get("lofi-piano"),
                    CreateProfile(body: 0.54f, brightness: 0.12f, drive: 0.18f, releaseBias: 0.38f,
                        transientSharpness: 0.22f, resonance: 0.16f),
                    62,
                    0.64f,
                    0.24f,
                    0.18f);

                var brightSpec = VoiceSpecResolver.ResolveMelody(
                    InstrumentPresets.Get("lofi-piano"),
                    CreateProfile(body: 0.36f, brightness: 0.88f, drive: 0.24f, releaseBias: 0.38f,
                        transientSharpness: 0.46f, resonance: 0.4f),
                    62,
                    0.64f,
                    0.78f,
                    0.18f);

                var darkClip = InvokeGetOrCreateClip(player, darkSpec);
                var brightClip = InvokeGetOrCreateClip(player, brightSpec);

                Assert.That(brightClip, Is.Not.SameAs(darkClip));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player.gameObject);
            }
        }

        [Test]
        public void SamplePlayer_EvictsOldestCacheEntry_WhenCapacityIsExceeded()
        {
            var player = CreatePlayer();
            try
            {
                Assert.That(MaxCachedClipsField, Is.Not.Null);
                MaxCachedClipsField.SetValue(player, 2);

                var specA = VoiceSpecResolver.ResolveMelody(
                    InstrumentPresets.Get("lofi-piano"),
                    CreateProfile(body: 0.42f, brightness: 0.3f, drive: 0.18f, releaseBias: 0.32f,
                        transientSharpness: 0.26f, resonance: 0.16f),
                    60,
                    0.48f,
                    0.4f,
                    0.18f);

                var specB = VoiceSpecResolver.ResolveMelody(
                    InstrumentPresets.Get("lofi-piano"),
                    CreateProfile(body: 0.48f, brightness: 0.46f, drive: 0.18f, releaseBias: 0.32f,
                        transientSharpness: 0.28f, resonance: 0.2f),
                    64,
                    0.48f,
                    0.5f,
                    0.18f);

                var specC = VoiceSpecResolver.ResolveMelody(
                    InstrumentPresets.Get("lofi-piano"),
                    CreateProfile(body: 0.56f, brightness: 0.68f, drive: 0.22f, releaseBias: 0.4f,
                        transientSharpness: 0.36f, resonance: 0.32f),
                    67,
                    0.72f,
                    0.66f,
                    0.18f);

                InvokeGetOrCreateClip(player, specA);
                InvokeGetOrCreateClip(player, specB);
                InvokeGetOrCreateClip(player, specC);

                var cache = GetVoiceCache(player);
                Assert.That(cache.Count, Is.EqualTo(2));
                Assert.That(cache.Contains(specA.GetCacheKey()), Is.False);
                Assert.That(cache.Contains(specB.GetCacheKey()), Is.True);
                Assert.That(cache.Contains(specC.GetCacheKey()), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player.gameObject);
            }
        }

        private static SamplePlayer CreatePlayer()
        {
            var go = new GameObject("ProceduralAudioRendererTests");
            var player = go.AddComponent<SamplePlayer>();
            player.Configure();
            return player;
        }

        private static AudioClip InvokeGetOrCreateClip(SamplePlayer player, ResolvedVoiceSpec spec)
        {
            Assert.That(GetOrCreateClipMethod, Is.Not.Null);
            return (AudioClip)GetOrCreateClipMethod.Invoke(player, new object[] { spec });
        }

        private static IDictionary GetVoiceCache(SamplePlayer player)
        {
            Assert.That(VoiceCacheField, Is.Not.Null);
            return (IDictionary)VoiceCacheField.GetValue(player);
        }

        private static void AssertHealthyClip(AudioClip clip, int expectedSamples)
        {
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.channels, Is.EqualTo(2));
            Assert.That(clip.samples, Is.EqualTo(expectedSamples));

            var data = ReadLeftChannel(clip);
            float peak = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                Assert.That(!float.IsNaN(data[i]) && !float.IsInfinity(data[i]), Is.True);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }

            Assert.That(peak, Is.GreaterThan(0.001f));
        }

        private static float HighFrequencyProxy(AudioClip clip)
        {
            var data = ReadLeftChannel(clip);
            float total = 0f;

            for (int i = 1; i < data.Length; i++)
                total += Mathf.Abs(data[i] - data[i - 1]);

            return total / Mathf.Max(1, data.Length - 1);
        }

        private static float LowFrequencyProxy(AudioClip clip)
        {
            var data = ReadLeftChannel(clip);
            float alpha = 1f - Mathf.Exp(-2f * Mathf.PI * 260f / ProceduralSynthesizer.SampleRate);
            float state = 0f;
            float sumSquares = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                state += alpha * (data[i] - state);
                sumSquares += state * state;
            }

            return Mathf.Sqrt(sumSquares / Mathf.Max(1, data.Length));
        }

        private static float TailRms(AudioClip clip, float tailSeconds)
        {
            var data = ReadLeftChannel(clip);
            int tailSamples = Mathf.Clamp(Mathf.RoundToInt(tailSeconds * ProceduralSynthesizer.SampleRate), 1, data.Length);
            int start = data.Length - tailSamples;
            float sumSquares = 0f;

            for (int i = start; i < data.Length; i++)
                sumSquares += data[i] * data[i];

            return Mathf.Sqrt(sumSquares / tailSamples);
        }

        private static float StereoWidth(AudioClip clip)
        {
            ReadStereoChannels(clip, out var left, out var right);
            float sumSquares = 0f;

            for (int i = 0; i < left.Length; i++)
            {
                float diff = left[i] - right[i];
                sumSquares += diff * diff;
            }

            return Mathf.Sqrt(sumSquares / Mathf.Max(1, left.Length));
        }

        private static float MeanAbsoluteDifference(AudioClip a, AudioClip b)
        {
            var aData = ReadLeftChannel(a);
            var bData = ReadLeftChannel(b);
            int sampleCount = Mathf.Min(Mathf.Min(aData.Length, bData.Length), ProceduralSynthesizer.SampleRate);
            float total = 0f;

            for (int i = 0; i < sampleCount; i++)
                total += Mathf.Abs(aData[i] - bData[i]);

            return total / Mathf.Max(1, sampleCount);
        }

        private static float[] ReadLeftChannel(AudioClip clip)
        {
            ReadStereoChannels(clip, out var left, out _);
            return left;
        }

        private static void ReadStereoChannels(AudioClip clip, out float[] left, out float[] right)
        {
            var interleaved = new float[clip.samples * clip.channels];
            clip.GetData(interleaved, 0);

            left = new float[clip.samples];
            right = new float[clip.samples];

            if (clip.channels == 1)
            {
                for (int i = 0; i < clip.samples; i++)
                    left[i] = right[i] = interleaved[i];
                return;
            }

            for (int i = 0; i < clip.samples; i++)
            {
                left[i] = interleaved[i * clip.channels];
                right[i] = interleaved[i * clip.channels + 1];
            }
        }

        private static int ExpectedToneSamples(ResolvedVoiceSpec spec)
        {
            float seconds = spec.durationSeconds + spec.releaseSeconds + GetAmbienceTail(spec);
            return Mathf.Max(64, Mathf.RoundToInt(seconds * ProceduralSynthesizer.SampleRate));
        }

        private static int ExpectedDrumSamples(ResolvedVoiceSpec spec)
        {
            float seconds = spec.durationSeconds + spec.releaseSeconds * 0.45f + GetAmbienceTail(spec) * 0.35f;
            return Mathf.Max(64, Mathf.RoundToInt(seconds * ProceduralSynthesizer.SampleRate));
        }

        private static float GetAmbienceTail(ResolvedVoiceSpec spec)
        {
            return spec.fxSend * (0.08f + spec.reverbBias * 0.18f + spec.delayBias * 0.16f)
                + (spec.patternType == PatternType.HarmonyPad ? 0.12f : 0.04f);
        }

        private static SoundProfile CreateProfile(
            float body,
            float brightness,
            float drive,
            float releaseBias,
            float transientSharpness,
            float resonance,
            float attackBias = 0.28f,
            float detune = 0.18f,
            float modDepth = 0.2f,
            float stereoSpread = 0.2f,
            float waveMorph = 0.24f,
            float filterMotion = 0.18f,
            float delayBias = 0.14f,
            float reverbBias = 0.18f)
        {
            return new SoundProfile
            {
                body = body,
                brightness = brightness,
                drive = drive,
                releaseBias = releaseBias,
                transientSharpness = transientSharpness,
                resonance = resonance,
                attackBias = attackBias,
                detune = detune,
                modDepth = modDepth,
                stereoSpread = stereoSpread,
                waveMorph = waveMorph,
                filterMotion = filterMotion,
                delayBias = delayBias,
                reverbBias = reverbBias
            };
        }
    }
}
#endif
