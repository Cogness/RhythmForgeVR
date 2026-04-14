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
                    if (spec.isNewAge) RenderSingingBowlStrike(spec, left, right, seed);
                    else               RenderKick(spec, left, right, seed);
                    break;
                case "snare":
                    if (spec.isJazz)   RenderBrushSnare(spec, left, right, seed);
                    else               RenderSnare(spec, left, right, seed);
                    break;
                case "hat":
                    if (spec.isNewAge) RenderShaker(spec, left, right, seed);
                    else if (spec.isJazz) RenderRideCymbal(spec, left, right, seed);
                    else               RenderHat(spec, left, right, seed);
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

        // ── New Age synthesis ──────────────────────────────────────────────────

        private static void RenderSingingBowlStrike(ResolvedVoiceSpec spec, float[] left, float[] right, int seed)
        {
            // Singing bowl: strong sine fundamental + weaker harmonic, very long slow decay
            float fundamentalFreq = 220f + spec.body * 180f + spec.brightness * 80f;
            float harmonicFreq    = fundamentalFreq * 2.74f; // non-integer partial for bowl character
            float decayTime = 0.9f + spec.releaseBias * 2.2f + spec.body * 0.8f;
            float phase1 = 0f, phase2 = 0f;

            // Very gentle attack
            float attackTime = 0.012f + spec.attackBias * 0.06f;

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SynthUtilities.SampleRate;
                float env = SynthUtilities.EnvelopeDecay(t, decayTime)
                          * Mathf.Clamp01(t / attackTime);
                float fund = Mathf.Sin(Mathf.PI * 2f * phase1) * 0.68f * env;
                float harm = Mathf.Sin(Mathf.PI * 2f * phase2) * 0.22f * SynthUtilities.EnvelopeDecay(t, decayTime * 0.7f);
                phase1 = SynthUtilities.AdvancePhase(phase1, fundamentalFreq);
                phase2 = SynthUtilities.AdvancePhase(phase2, harmonicFreq);

                float spreadL = spec.stereoSpread > 0.2f ? 1.06f : 1f;
                float spreadR = spec.stereoSpread > 0.2f ? 0.94f : 1f;
                left[i]  = (fund + harm) * spreadL;
                right[i] = (fund + harm) * spreadR;
            }
        }

        private static void RenderShaker(ResolvedVoiceSpec spec, float[] left, float[] right, int seed)
        {
            // Shaker: gentle filtered noise with soft envelope
            var rng = new System.Random(seed);
            var filterLeft = new SvfState();
            var filterRight = new SvfState();
            float cutoff = 3200f + spec.brightness * 2800f;
            float q = 0.5f + spec.resonance * 0.3f;
            float decayTime = 0.04f + spec.releaseBias * 0.1f + spec.body * 0.06f;

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SynthUtilities.SampleRate;
                float env = SynthUtilities.EnvelopeDecay(t, decayTime);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * env * 0.6f;
                left[i]  = SynthUtilities.ProcessFilter(ref filterLeft,  noise, cutoff,        q, VoiceFilterMode.BandPass);
                right[i] = SynthUtilities.ProcessFilter(ref filterRight, noise, cutoff * 1.03f, q, VoiceFilterMode.BandPass);
            }
        }

        // ── Jazz synthesis ─────────────────────────────────────────────────────

        private static void RenderBrushSnare(ResolvedVoiceSpec spec, float[] left, float[] right, int seed)
        {
            // Brush snare: very soft, airy filtered noise — much gentler than stick snare
            var rng = new System.Random(seed);
            var filterLeft = new SvfState();
            var filterRight = new SvfState();
            float noiseCutoff = 900f + spec.brightness * 2200f;
            float q = 0.55f + spec.resonance * 0.25f;
            float decayTime = 0.12f + spec.releaseBias * 0.22f;

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SynthUtilities.SampleRate;
                float env = SynthUtilities.EnvelopeDecay(t, decayTime);
                // Much softer than electronic snare
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * env * 0.42f;
                left[i]  = SynthUtilities.ProcessFilter(ref filterLeft,  noise, noiseCutoff,        q, VoiceFilterMode.BandPass);
                right[i] = SynthUtilities.ProcessFilter(ref filterRight, noise, noiseCutoff * 1.02f, q, VoiceFilterMode.BandPass);
            }
        }

        private static void RenderRideCymbal(ResolvedVoiceSpec spec, float[] left, float[] right, int seed)
        {
            // Ride cymbal: metallic shimmer with lower cutoff and longer sustain than hi-hat
            var rng = new System.Random(seed);
            var filterLeft = new SvfState();
            var filterRight = new SvfState();
            float cutoff = 4800f + spec.brightness * 2000f; // lower than electronic hat
            float q = 0.7f + spec.resonance * 0.35f;
            float decayTime = 0.18f + spec.releaseBias * 0.3f; // longer sustain = ride character

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SynthUtilities.SampleRate;
                float env = SynthUtilities.EnvelopeDecay(t, decayTime);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                // Metallic partials for ride bell character
                float bell = Mathf.Sin(Mathf.PI * 2f * 3400f * t) * 0.2f
                           + Mathf.Sin(Mathf.PI * 2f * 5100f * t) * 0.1f;
                float source = (noise * 0.65f + bell) * env;
                left[i]  = SynthUtilities.ProcessFilter(ref filterLeft,  source, cutoff,        q, VoiceFilterMode.HighPass);
                right[i] = SynthUtilities.ProcessFilter(ref filterRight, source, cutoff * 1.01f, q, VoiceFilterMode.HighPass);
            }
        }
    }
}
