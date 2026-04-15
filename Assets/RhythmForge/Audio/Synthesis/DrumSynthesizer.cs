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

        public static RawSamples RenderRaw(ResolvedVoiceSpec spec)
        {
            int sampleCount = SynthUtilities.SecondsToSamples(
                spec.durationSeconds + spec.releaseSeconds * 0.45f + AudioEffectsChain.GetAmbienceTail(spec) * 0.35f);
            var left  = new float[sampleCount];
            var right = new float[sampleCount];
            int seed  = SynthUtilities.ComputeSeed(spec.GetCacheKey());

            RenderIntoBuffers(spec, left, right, seed);

            return new RawSamples { name = $"Drum_{spec.lane}", left = left, right = right };
        }

        private static void RenderIntoBuffers(ResolvedVoiceSpec spec, float[] left, float[] right, int seed)
        {
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
        }

        public static AudioClip Render(ResolvedVoiceSpec spec)
        {
            int sampleCount = SynthUtilities.SecondsToSamples(
                spec.durationSeconds + spec.releaseSeconds * 0.45f + AudioEffectsChain.GetAmbienceTail(spec) * 0.35f);
            var left = new float[sampleCount];
            var right = new float[sampleCount];
            int seed = SynthUtilities.ComputeSeed(spec.GetCacheKey());

            RenderIntoBuffers(spec, left, right, seed);
            return SynthUtilities.BuildClip($"Drum_{spec.lane}", left, right);
        }

        private static void RenderKick(ResolvedVoiceSpec spec, float[] left, float[] right, int seed)
        {
            var rng = new System.Random(seed);
            float phase    = 0f;
            float subPhase = 0f;
            float clickLp  = 0f;

            float startFreq    = (spec.isTrap ? 120f : 90f) + spec.body * 50f;
            float endFreq      = 30f + spec.body * 16f;
            float pitchDur     = 0.10f + spec.releaseBias * 0.14f;
            float bodyDecay    = 0.16f + spec.releaseBias * 0.20f;
            float subDecay     = bodyDecay * 1.7f;
            float subGain      = 0.22f + spec.body * 0.12f;
            float clickDecay   = 0.005f + spec.transientSharpness * 0.006f;

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SynthUtilities.SampleRate;
                float pitchProg = pitchDur <= 0f ? 1f : Mathf.Clamp01(t / pitchDur);
                float freq = SynthUtilities.ExponentialLerp(startFreq, endFreq, pitchProg);

                // Pure sine body — no aliasing triangle
                float body = Mathf.Sin(Mathf.PI * 2f * phase) * Mathf.Exp(-t / Mathf.Max(0.04f, bodyDecay));
                phase = SynthUtilities.AdvancePhase(phase, freq);

                // Sub-octave sine layer for weight
                float sub = Mathf.Sin(Mathf.PI * 2f * subPhase) * subGain
                          * Mathf.Exp(-t / Mathf.Max(0.04f, subDecay));
                subPhase = SynthUtilities.AdvancePhase(subPhase, freq * 0.5f);

                // Noise click transient (high-pass filtered burst)
                float click = 0f;
                if (t <= 0.028f)
                {
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                    clickLp += 0.22f * (noise - clickLp);
                    float high = noise - clickLp;
                    click = high * (0.14f + spec.transientSharpness * 0.18f)
                          * Mathf.Exp(-t / clickDecay);
                }

                float sample = body + sub + click;
                left[i]  = sample;
                right[i] = sample;
            }
        }

        private static void RenderSnare(ResolvedVoiceSpec spec, float[] left, float[] right, int seed)
        {
            var rng = new System.Random(seed);
            var filterLeft  = new SvfState();
            var filterRight = new SvfState();
            float phase    = 0f;
            float fmPhase  = 0f; // FM modulator phase for body resonance
            float noiseCutoff = (spec.isTrap ? 1700f : 1100f) + spec.brightness * 2800f;
            float q           = 0.7f + spec.resonance * 0.35f;
            float bodyFreq    = 180f + spec.body * 120f;
            float fmRatio     = 1.82f; // inharmonic modulator for snare crack texture
            float fmDepth     = bodyFreq * (0.4f + spec.body * 0.6f);
            float noiseDecay  = 0.08f + spec.releaseBias * 0.14f;
            float bodyDecay   = 0.07f + spec.releaseBias * 0.10f;

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SynthUtilities.SampleRate;

                // Noise layer (snare wires)
                float noiseEnv     = SynthUtilities.EnvelopeDecay(t, noiseDecay);
                float noise        = (float)(rng.NextDouble() * 2.0 - 1.0) * noiseEnv;
                float filteredLeft  = SynthUtilities.ProcessFilter(ref filterLeft,  noise, noiseCutoff,        q, VoiceFilterMode.HighPass);
                float filteredRight = SynthUtilities.ProcessFilter(ref filterRight, noise, noiseCutoff * 1.02f, q, VoiceFilterMode.HighPass);

                float sampleLeft  = filteredLeft;
                float sampleRight = filteredRight;

                // FM body tone: carrier modulated by inharmonic ratio for snare crack
                float fmMod  = Mathf.Sin(Mathf.PI * 2f * fmPhase);
                float instFreq = bodyFreq + fmMod * fmDepth;
                fmPhase  = SynthUtilities.AdvancePhase(fmPhase, bodyFreq * fmRatio);
                float bodyEnv = SynthUtilities.EnvelopeDecay(t, bodyDecay);
                float tone = Mathf.Sin(Mathf.PI * 2f * phase) * 0.30f * bodyEnv;
                phase = SynthUtilities.AdvancePhase(phase, Mathf.Max(60f, instFreq));

                sampleLeft  += tone;
                sampleRight += tone;

                left[i]  = sampleLeft;
                right[i] = sampleRight;
            }
        }

        private static void RenderHat(ResolvedVoiceSpec spec, float[] left, float[] right, int seed)
        {
            var rng = new System.Random(seed);
            var filterLeft  = new SvfState();
            var filterRight = new SvfState();
            float cutoff = (spec.isDream ? 6000f : 7400f) + spec.brightness * 2400f;
            float q      = 0.75f + spec.resonance * 0.35f;
            float decay  = 0.025f + spec.transientSharpness * 0.055f;

            // 6-partial inharmonic metallic oscillators (classic cymbal synthesis ratios).
            // Ratios taken from Yamaha DX7 cymbal algorithm and Chowning FM cymbal research.
            float[] metalRatios = { 1.0f, 1.4142f, 1.5157f, 1.7411f, 2.0f, 2.4f };
            float[] metalDecays = { decay, decay * 0.9f, decay * 0.85f, decay * 0.8f, decay * 0.7f, decay * 0.6f };
            float[] metalAmps   = { 0.28f, 0.22f, 0.20f, 0.16f, 0.10f, 0.06f };
            float baseFreq = 220f * (1f + spec.brightness * 0.8f); // tunable metallic base
            float[] metalPhase = new float[6];

            for (int i = 0; i < left.Length; i++)
            {
                float t   = (float)i / SynthUtilities.SampleRate;
                float env = SynthUtilities.EnvelopeDecay(t, decay);

                // Metallic partials
                float metallic = 0f;
                for (int m = 0; m < 6; m++)
                {
                    metallic += Mathf.Sin(Mathf.PI * 2f * metalPhase[m])
                              * metalAmps[m]
                              * SynthUtilities.EnvelopeDecay(t, metalDecays[m]);
                    metalPhase[m] = SynthUtilities.AdvancePhase(metalPhase[m], baseFreq * metalRatios[m]);
                }

                // Noise component
                float noise  = (float)(rng.NextDouble() * 2.0 - 1.0) * env * 0.45f;
                float source = metallic + noise;

                left[i]  = SynthUtilities.ProcessFilter(ref filterLeft,  source, cutoff,        q, VoiceFilterMode.HighPass);
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
            // percTuningMidi is baked into the spec at resolve time (VoiceSpecResolver)
            // and included in the cache key, so changing the key root correctly busts the cache.
            float toneFrequency = spec.percTuningMidi > 0
                ? SynthUtilities.MidiToFrequency(spec.percTuningMidi) * (1f + spec.body * 0.5f)
                : 180f + spec.body * 220f;

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
            // Tibetan singing bowl: raised fundamental into real bowl range (400–1200 Hz),
            // four inharmonic partials with slow beating between partial 2a/2b for the
            // characteristic shimmering wobble of two bowl walls resonating against each other.
            float f0 = 440f + spec.body * 180f + spec.brightness * 140f; // real bowl range
            float f1 = f0 * 2.74f;   // inharmonic partial 1
            float f1b = f1 + 1.5f;   // slightly detuned twin → amplitude beating at ~1.5 Hz
            float f2 = f0 * 5.43f;   // inharmonic partial 2
            float f3 = f0 * 8.12f;   // inharmonic partial 3
            float decayTime = 0.6f + spec.releaseBias * 1.8f + spec.body * 0.6f; // shorter floor to avoid self-overlap
            float phase0 = 0f, phase1 = 0f, phase1b = 0f, phase2 = 0f, phase3 = 0f;

            float attackTime = 0.012f + spec.attackBias * 0.06f;

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SynthUtilities.SampleRate;
                float env  = SynthUtilities.EnvelopeDecay(t, decayTime) * Mathf.Clamp01(t / attackTime);
                float env1 = SynthUtilities.EnvelopeDecay(t, decayTime * 0.72f);
                float env2 = SynthUtilities.EnvelopeDecay(t, decayTime * 0.54f);
                float env3 = SynthUtilities.EnvelopeDecay(t, decayTime * 0.38f);

                float fund  = Mathf.Sin(Mathf.PI * 2f * phase0) * 0.62f * env;
                float harm1 = (Mathf.Sin(Mathf.PI * 2f * phase1) + Mathf.Sin(Mathf.PI * 2f * phase1b)) * 0.11f * env1;
                float harm2 = Mathf.Sin(Mathf.PI * 2f * phase2) * 0.06f * env2;
                float harm3 = Mathf.Sin(Mathf.PI * 2f * phase3) * 0.025f * env3;

                phase0  = SynthUtilities.AdvancePhase(phase0,  f0);
                phase1  = SynthUtilities.AdvancePhase(phase1,  f1);
                phase1b = SynthUtilities.AdvancePhase(phase1b, f1b);
                phase2  = SynthUtilities.AdvancePhase(phase2,  f2);
                phase3  = SynthUtilities.AdvancePhase(phase3,  f3);

                float s = fund + harm1 + harm2 + harm3;
                float spreadL = spec.stereoSpread > 0.2f ? 1.06f : 1f;
                float spreadR = spec.stereoSpread > 0.2f ? 0.94f : 1f;
                left[i]  = s * spreadL;
                right[i] = s * spreadR;
            }
        }

        private static void RenderShaker(ResolvedVoiceSpec spec, float[] left, float[] right, int seed)
        {
            // Shaker: gentle filtered noise with per-trigger deterministic pan
            // so multiple shaker voices feel like a hand-held shaker moving in space.
            var rng = new System.Random(seed);
            var filterLeft = new SvfState();
            var filterRight = new SvfState();
            float cutoff = 3200f + spec.brightness * 2800f;
            float q = 0.5f + spec.resonance * 0.3f;
            float decayTime = 0.04f + spec.releaseBias * 0.1f + spec.body * 0.06f;

            // Deterministic pan in ±0.35 — seeded so the same shaker event always pans the same way
            float pan = (float)(new System.Random(seed ^ 0x3A7F1C).NextDouble() * 2.0 - 1.0) * 0.35f;
            float gainL = 1f - Mathf.Max(0f,  pan);
            float gainR = 1f + Mathf.Min(0f, -pan);

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SynthUtilities.SampleRate;
                float env = SynthUtilities.EnvelopeDecay(t, decayTime);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * env * 0.6f;
                float fL = SynthUtilities.ProcessFilter(ref filterLeft,  noise, cutoff,        q, VoiceFilterMode.BandPass);
                float fR = SynthUtilities.ProcessFilter(ref filterRight, noise, cutoff * 1.03f, q, VoiceFilterMode.BandPass);
                left[i]  = fL * gainL;
                right[i] = fR * gainR;
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
