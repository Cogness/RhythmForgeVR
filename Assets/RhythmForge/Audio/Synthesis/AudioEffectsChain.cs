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
            float driveAmount = 1f + spec.drive * 1.8f + (spec.isTrap ? 0.32f : spec.isLoFi ? 0.12f : 0f);
            // New Age: no drive saturation; Jazz: very mild warmth
            if (spec.isNewAge) driveAmount = Mathf.Min(driveAmount, 1.08f);
            if (spec.isJazz)   driveAmount = Mathf.Min(driveAmount, 1.18f);
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
            float genreDelayScale = spec.isNewAge ? 1.6f : spec.isJazz ? 0.8f : 1.0f;
            float genreBloomScale = spec.isNewAge ? 2.2f : spec.isJazz ? 1.1f : 1.0f;

            float delayMix = spec.fxSend * genreDelayScale * (spec.patternType == PatternType.RhythmLoop
                ? 0.04f + spec.delayBias * 0.08f
                : 0.07f + spec.delayBias * 0.14f);
            float bloomMix = spec.fxSend * genreBloomScale * (spec.patternType == PatternType.HarmonyPad
                ? 0.12f + spec.reverbBias * 0.2f
                : 0.05f + spec.reverbBias * 0.12f);

            if (delayMix <= 0.001f && bloomMix <= 0.001f)
                return;

            int shortDelay = SynthUtilities.SecondsToSamples(spec.patternType == PatternType.RhythmLoop ? 0.045f : 0.075f + spec.delayBias * 0.1f);
            int longDelay = SynthUtilities.SecondsToSamples(spec.patternType == PatternType.HarmonyPad ? 0.16f + spec.delayBias * 0.16f : 0.11f + spec.delayBias * 0.08f);
            int bloomA = SynthUtilities.SecondsToSamples(0.028f + spec.reverbBias * 0.02f);
            int bloomB = SynthUtilities.SecondsToSamples(0.061f + spec.reverbBias * 0.05f);
            int bloomC = SynthUtilities.SecondsToSamples(0.11f + spec.reverbBias * 0.08f);

            for (int i = 0; i < left.Length; i++)
            {
                if (delayMix > 0.001f)
                {
                    if (i + shortDelay < left.Length)
                    {
                        left[i + shortDelay] += left[i] * delayMix * 0.22f;
                        right[i + shortDelay] += right[i] * delayMix * 0.18f;
                    }
                    if (i + longDelay < left.Length)
                    {
                        left[i + longDelay] += right[i] * delayMix * 0.12f;
                        right[i + longDelay] += left[i] * delayMix * 0.12f;
                    }
                }

                if (bloomMix > 0.001f)
                {
                    if (i + bloomA < left.Length)
                    {
                        left[i + bloomA] += left[i] * bloomMix * 0.14f;
                        right[i + bloomA] += right[i] * bloomMix * 0.14f;
                    }
                    if (i + bloomB < left.Length)
                    {
                        left[i + bloomB] += right[i] * bloomMix * 0.09f;
                        right[i + bloomB] += left[i] * bloomMix * 0.09f;
                    }
                    if (i + bloomC < left.Length)
                    {
                        left[i + bloomC] += left[i] * bloomMix * 0.06f;
                        right[i + bloomC] += right[i] * bloomMix * 0.06f;
                    }
                }
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
