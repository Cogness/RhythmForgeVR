using System;
using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Audio
{
    internal static class DrumSynthesizer
    {
        public static AudioClip GenerateKick(float duration = 0.32f, float drive = 1.4f)
        {
            var preset = InstrumentPresets.Get("lofi-drums");
            var spec = VoiceSpecResolver.ResolveDrum(
                "kick",
                preset,
                SynthUtilities.CreateProfile(
                    body: 0.72f,
                    brightness: 0.34f,
                    drive: Mathf.Clamp01(0.18f + (drive - 1f) * 0.3f),
                    releaseBias: Mathf.Clamp01(duration / 0.5f),
                    transientSharpness: 0.46f,
                    resonance: 0.24f),
                0.55f,
                preset.fxSend);
            return Render(spec);
        }

        public static AudioClip GenerateSnare(float duration = 0.22f)
        {
            var preset = InstrumentPresets.Get("lofi-drums");
            var spec = VoiceSpecResolver.ResolveDrum(
                "snare",
                preset,
                SynthUtilities.CreateProfile(
                    body: 0.52f,
                    brightness: 0.58f,
                    drive: 0.28f,
                    releaseBias: Mathf.Clamp01(duration / 0.4f),
                    transientSharpness: 0.58f,
                    resonance: 0.36f),
                0.6f,
                preset.fxSend);
            return Render(spec);
        }

        public static AudioClip GenerateHat(float duration = 0.08f)
        {
            var preset = InstrumentPresets.Get("lofi-drums");
            var spec = VoiceSpecResolver.ResolveDrum(
                "hat",
                preset,
                SynthUtilities.CreateProfile(
                    body: 0.18f,
                    brightness: 0.8f,
                    drive: 0.18f,
                    releaseBias: Mathf.Clamp01(duration / 0.2f),
                    transientSharpness: 0.74f,
                    resonance: 0.52f),
                0.78f,
                preset.fxSend);
            return Render(spec);
        }

        public static AudioClip Render(ResolvedVoiceSpec spec)
        {
            int sampleCount = SynthUtilities.SecondsToSamples(
                spec.durationSeconds + spec.releaseSeconds * 0.45f + AudioEffectsChain.GetAmbienceTail(spec) * 0.35f);
            var left = new float[sampleCount];
            var right = new float[sampleCount];
            int seed = SynthUtilities.ComputeSeed(spec.GetCacheKey());

            switch (spec.lane)
            {
                case "kick":
                    RenderKick(spec, left, right, seed);
                    break;
                case "snare":
                    RenderSnare(spec, left, right, seed);
                    break;
                case "hat":
                    RenderHat(spec, left, right, seed);
                    break;
                default:
                    RenderPercussion(spec, left, right, seed);
                    break;
            }

            AudioEffectsChain.ApplyVoiceChain(spec, left, right);
            AudioEffectsChain.ApplyAmbience(spec, left, right);
            AudioEffectsChain.NormalizeStereo(left, right);
            return SynthUtilities.BuildClip($"Drum_{spec.lane}", left, right);
        }

        private static void RenderKick(ResolvedVoiceSpec spec, float[] left, float[] right, int seed)
        {
            var rng = new System.Random(seed);
            float phase = 0f;
            float clickLp = 0f;

            float startFrequency = (spec.isTrap ? 122f : 96f) + spec.body * 52f;
            float endFrequency = 34f + spec.body * 18f;
            float pitchDuration = 0.12f + spec.releaseBias * 0.12f;
            float bodyDecay = 0.14f + spec.releaseBias * 0.18f;

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SynthUtilities.SampleRate;
                float pitchProgress = pitchDuration <= 0f ? 1f : Mathf.Clamp01(t / pitchDuration);
                float frequency = SynthUtilities.ExponentialLerp(startFrequency, endFrequency, pitchProgress);
                VoiceWaveform waveform = spec.transientSharpness > 0.58f
                    ? VoiceWaveform.Triangle
                    : spec.body > 0.62f ? VoiceWaveform.Sine : VoiceWaveform.Triangle;

                float body = SynthUtilities.SampleWave(waveform, phase) * Mathf.Exp(-t / Mathf.Max(0.04f, bodyDecay));
                phase = SynthUtilities.AdvancePhase(phase, frequency);

                float sub = spec.body > 0.52f
                    ? Mathf.Sin(Mathf.PI * 2f * (frequency * 0.5f) * t) * 0.18f * Mathf.Exp(-t / (bodyDecay * 1.6f))
                    : 0f;

                float click = 0f;
                if (t <= 0.03f)
                {
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                    clickLp += 0.18f * (noise - clickLp);
                    float high = noise - clickLp;
                    float clickCut = 1800f + spec.transientSharpness * 5000f;
                    click = high * (0.12f + spec.transientSharpness * 0.2f) *
                        Mathf.Exp(-t / Mathf.Max(0.004f, 0.008f + 2000f / Mathf.Max(2000f, clickCut * 2f)));
                }

                float sample = body + sub + click;
                left[i] = sample;
                right[i] = sample;
            }
        }

        private static void RenderSnare(ResolvedVoiceSpec spec, float[] left, float[] right, int seed)
        {
            var rng = new System.Random(seed);
            var filterLeft = new SvfState();
            var filterRight = new SvfState();
            float phase = 0f;
            float noiseCutoff = (spec.isTrap ? 1700f : 1100f) + spec.brightness * 2800f;
            float q = 0.7f + spec.resonance * 0.35f;
            float bodyFrequency = 180f + spec.body * 120f;

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SynthUtilities.SampleRate;
                float noiseEnv = SynthUtilities.EnvelopeDecay(t, 0.08f + spec.releaseBias * 0.14f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * noiseEnv;
                float filteredLeft = SynthUtilities.ProcessFilter(ref filterLeft, noise, noiseCutoff, q, VoiceFilterMode.HighPass);
                float filteredRight = SynthUtilities.ProcessFilter(ref filterRight, noise, noiseCutoff * 1.02f, q, VoiceFilterMode.HighPass);

                float sampleLeft = filteredLeft;
                float sampleRight = filteredRight;

                if (spec.body > 0.44f)
                {
                    float tone = SynthUtilities.SampleWave(VoiceWaveform.Triangle, phase) * 0.34f * SynthUtilities.EnvelopeDecay(t, 0.1f);
                    phase = SynthUtilities.AdvancePhase(phase, bodyFrequency);
                    sampleLeft += tone;
                    sampleRight += tone;
                }

                left[i] = sampleLeft;
                right[i] = sampleRight;
            }
        }

        private static void RenderHat(ResolvedVoiceSpec spec, float[] left, float[] right, int seed)
        {
            var rng = new System.Random(seed);
            var filterLeft = new SvfState();
            var filterRight = new SvfState();
            float cutoff = (spec.isDream ? 6200f : 7600f) + spec.brightness * 2400f;
            float q = 0.8f + spec.resonance * 0.4f;

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SynthUtilities.SampleRate;
                float env = SynthUtilities.EnvelopeDecay(t, 0.03f + spec.transientSharpness * 0.06f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float shimmer = Mathf.Sin(Mathf.PI * 2f * 8372f * t) * 0.15f
                              + Mathf.Sin(Mathf.PI * 2f * 10548f * t) * 0.1f;
                float source = (noise * 0.78f + shimmer) * env;
                left[i] = SynthUtilities.ProcessFilter(ref filterLeft, source, cutoff, q, VoiceFilterMode.HighPass);
                right[i] = SynthUtilities.ProcessFilter(ref filterRight, source, cutoff * 1.01f, q, VoiceFilterMode.HighPass);
            }
        }

        private static void RenderPercussion(ResolvedVoiceSpec spec, float[] left, float[] right, int seed)
        {
            var rng = new System.Random(seed);
            var filterLeft = new SvfState();
            var filterRight = new SvfState();
            float cutoff = 1000f + spec.body * 600f + spec.brightness * 2200f;
            float q = 0.8f + spec.resonance * 0.42f;
            float phase = 0f;
            float toneFrequency = 180f + spec.body * 220f;

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SynthUtilities.SampleRate;
                float env = SynthUtilities.EnvelopeDecay(t, 0.08f + spec.releaseBias * 0.12f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * env;
                float tone = SynthUtilities.SampleWave(VoiceWaveform.Triangle, phase) * 0.28f * SynthUtilities.EnvelopeDecay(t, 0.11f);
                phase = SynthUtilities.AdvancePhase(phase, toneFrequency);

                float source = noise * 0.54f + tone;
                left[i] = SynthUtilities.ProcessFilter(ref filterLeft, source, cutoff, q, VoiceFilterMode.BandPass);
                right[i] = SynthUtilities.ProcessFilter(ref filterRight, source, cutoff * 1.03f, q, VoiceFilterMode.BandPass);
            }
        }
    }
}
