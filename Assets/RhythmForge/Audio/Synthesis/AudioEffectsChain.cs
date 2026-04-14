using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Audio
{
    internal static class AudioEffectsChain
    {
        public static void ApplyVoiceChain(ResolvedVoiceSpec spec, float[] left, float[] right)
        {
            var leftState = new SvfState();
            var rightState = new SvfState();
            float baseCutoff = (spec.patternType == PatternType.RhythmLoop
                    ? 500f
                    : spec.patternType == PatternType.HarmonyPad ? 320f : 700f)
                + (spec.positionBrightness * 0.4f + spec.brightness * 0.6f)
                * (spec.patternType == PatternType.HarmonyPad ? 4200f : 6400f);

            if (spec.isLoFi)
                baseCutoff *= 0.82f;
            if (spec.isTrap)
                baseCutoff *= 1.06f;
            if (spec.isDream)
                baseCutoff *= 0.94f;
            // New Age: very soft, no harsh highs
            if (spec.isNewAge)
                baseCutoff *= 0.72f;
            // Jazz: warm mid-range cut
            if (spec.isJazz)
                baseCutoff *= 0.88f;

            float resonanceQ = 0.18f + spec.resonance * 0.8f;
            float motionRate = spec.patternType == PatternType.RhythmLoop
                ? 3f + spec.modDepth * 10f
                : spec.patternType == PatternType.MelodyLine ? 1.2f + spec.modDepth * 7f
                : 0.25f + spec.modDepth * 2.2f;
            // New Age: very slow filter motion for breathing feel
            if (spec.isNewAge)
                motionRate *= 0.2f;
            float motionDepth = spec.filterMotion > 0.08f
                ? 0.04f + spec.filterMotion * 0.18f
                : 0f;
            // Drive kept very gentle — heavy saturation was the main source of metallic coloration.
            float driveAmount = 1f + spec.drive * 0.55f + (spec.isTrap ? 0.12f : spec.isLoFi ? 0.06f : 0f);
            if (spec.isNewAge) driveAmount = Mathf.Min(driveAmount, 1.04f);
            if (spec.isJazz)   driveAmount = Mathf.Min(driveAmount, 1.10f);
            float spreadOffset = 1f + spec.stereoSpread * 0.08f;

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SynthUtilities.SampleRate;
                float motion = motionDepth > 0f
                    ? 1f + Mathf.Sin(Mathf.PI * 2f * motionRate * t) * motionDepth
                    : 1f;
                float cutoffLeft = Mathf.Clamp(baseCutoff * motion / spreadOffset, 90f, 12000f);
                float cutoffRight = Mathf.Clamp(baseCutoff * motion * spreadOffset, 90f, 12000f);

                float processedLeft = SynthUtilities.ProcessFilter(ref leftState, left[i], cutoffLeft, resonanceQ, spec.filterMode);
                float processedRight = SynthUtilities.ProcessFilter(ref rightState, right[i], cutoffRight, resonanceQ, spec.filterMode);

                left[i] = SynthUtilities.SoftClip(processedLeft * driveAmount);
                right[i] = SynthUtilities.SoftClip(processedRight * driveAmount);
            }
        }

        public static void ApplyAmbience(ResolvedVoiceSpec spec, float[] left, float[] right)
        {
            float genreReverbScale = spec.isNewAge ? 1.8f : spec.isJazz ? 0.9f : 1.0f;
            float genreDelayScale  = spec.isNewAge ? 1.4f : spec.isJazz ? 0.7f : 1.0f;

            float reverbMix = spec.fxSend * genreReverbScale * (spec.patternType == PatternType.HarmonyPad
                ? 0.18f + spec.reverbBias * 0.22f
                : 0.06f + spec.reverbBias * 0.14f);
            float delayMix = spec.fxSend * genreDelayScale * (spec.patternType == PatternType.RhythmLoop
                ? 0.04f + spec.delayBias * 0.07f
                : 0.06f + spec.delayBias * 0.12f);

            // Pre-delay tap
            if (delayMix > 0.001f)
            {
                int preDelay = SynthUtilities.SecondsToSamples(
                    spec.patternType == PatternType.RhythmLoop ? 0.04f : 0.07f + spec.delayBias * 0.09f);
                for (int i = 0; i < left.Length; i++)
                {
                    if (i + preDelay < left.Length)
                    {
                        left[i + preDelay]  += left[i]  * delayMix * 0.20f;
                        right[i + preDelay] += right[i] * delayMix * 0.16f;
                    }
                }
            }

            if (reverbMix <= 0.002f)
                return;

            // ── Schroeder reverb: 4 parallel comb filters + 2 series allpass ──────────────
            // Delay lengths are prime-spaced (in samples) to avoid metallic comb coloration.
            float roomScale = 0.7f + spec.reverbBias * 0.3f;
            int[] combDelays = {
                SynthUtilities.SecondsToSamples(0.0297f * roomScale),
                SynthUtilities.SecondsToSamples(0.0371f * roomScale),
                SynthUtilities.SecondsToSamples(0.0411f * roomScale),
                SynthUtilities.SecondsToSamples(0.0437f * roomScale)
            };
            float reverbTime = 0.4f + spec.reverbBias * 1.4f + (spec.isNewAge ? 0.8f : 0f);
            float[] combFeedback = new float[4];
            for (int c = 0; c < 4; c++)
                combFeedback[c] = Mathf.Pow(0.001f, (float)combDelays[c] / SynthUtilities.SampleRate / Mathf.Max(0.1f, reverbTime));

            int[] apDelays = {
                SynthUtilities.SecondsToSamples(0.0127f * roomScale),
                SynthUtilities.SecondsToSamples(0.0090f * roomScale)
            };
            const float apFeedback = 0.7f;

            // Allocate reverb buffers
            float[][] combBufL = new float[4][];
            float[][] combBufR = new float[4][];
            for (int c = 0; c < 4; c++)
            {
                combBufL[c] = new float[combDelays[c]];
                combBufR[c] = new float[combDelays[c]];
            }
            float[][] apBufL = { new float[apDelays[0]], new float[apDelays[1]] };
            float[][] apBufR = { new float[apDelays[0]], new float[apDelays[1]] };
            int[] combIdx = new int[4];
            int[] apIdx   = { 0, 0 };

            float[] reverbL = new float[left.Length];
            float[] reverbR = new float[right.Length];

            for (int i = 0; i < left.Length; i++)
            {
                float inL = left[i];
                float inR = right[i];

                // 4 parallel combs
                float combOutL = 0f, combOutR = 0f;
                for (int c = 0; c < 4; c++)
                {
                    float readL = combBufL[c][combIdx[c]];
                    float readR = combBufR[c][combIdx[c]];
                    combBufL[c][combIdx[c]] = inL + readL * combFeedback[c];
                    combBufR[c][combIdx[c]] = inR + readR * combFeedback[c];
                    combOutL += readL;
                    combOutR += readR;
                    combIdx[c] = (combIdx[c] + 1) % combDelays[c];
                }
                combOutL *= 0.25f;
                combOutR *= 0.25f;

                // 2 series allpass filters
                for (int a = 0; a < 2; a++)
                {
                    float bufOutL = apBufL[a][apIdx[a]];
                    float bufOutR = apBufR[a][apIdx[a]];
                    float feedL   = combOutL + bufOutL * apFeedback;
                    float feedR   = combOutR + bufOutR * apFeedback;
                    apBufL[a][apIdx[a]] = feedL;
                    apBufR[a][apIdx[a]] = feedR;
                    combOutL = bufOutL - apFeedback * feedL;
                    combOutR = bufOutR - apFeedback * feedR;
                    apIdx[a] = (apIdx[a] + 1) % apDelays[a];
                }

                reverbL[i] = combOutL;
                reverbR[i] = combOutR;
            }

            // Mix reverb into output
            for (int i = 0; i < left.Length; i++)
            {
                left[i]  += reverbL[i] * reverbMix;
                right[i] += reverbR[i] * reverbMix;
            }
        }

        public static float GetAmbienceTail(ResolvedVoiceSpec spec)
        {
            return spec.fxSend * (0.08f + spec.reverbBias * 0.18f + spec.delayBias * 0.16f)
                + (spec.patternType == PatternType.HarmonyPad ? 0.12f : 0.04f);
        }

        public static void NormalizeStereo(float[] left, float[] right)
        {
            float max = 0f;
            for (int i = 0; i < left.Length; i++)
            {
                max = Mathf.Max(max, Mathf.Abs(left[i]), Mathf.Abs(right[i]));
            }

            if (max <= 0.96f)
                return;

            float scale = 0.96f / max;
            for (int i = 0; i < left.Length; i++)
            {
                left[i] *= scale;
                right[i] *= scale;
            }
        }
    }
}
