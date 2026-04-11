using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Analysis
{
    public static class SoundProfileMapper
    {
        public static SoundProfile Derive(PatternType type, ShapeProfile sp)
        {
            float smoothness = 1f - sp.angularity;
            float asymmetry = 1f - sp.symmetry;
            float tiltAmount = Mathf.Abs(sp.tiltSigned);

            switch (type)
            {
                case PatternType.RhythmLoop:
                    return DeriveRhythm(sp, smoothness, asymmetry);
                case PatternType.MelodyLine:
                    return DeriveMelody(sp, smoothness, asymmetry, tiltAmount);
                default:
                    return DeriveHarmony(sp, smoothness, asymmetry, tiltAmount);
            }
        }

        private static SoundProfile DeriveRhythm(ShapeProfile sp, float smoothness, float asymmetry)
        {
            float instability = Mathf.Clamp01(
                sp.wobble * 0.7f + asymmetry * 0.55f + sp.curvatureVariance * 0.35f);

            return new SoundProfile
            {
                brightness = Mathf.Clamp01(0.22f + sp.angularity * 0.45f + instability * 0.16f),
                resonance = Mathf.Clamp01(0.16f + sp.angularity * 0.34f + sp.curvatureVariance * 0.24f),
                drive = Mathf.Clamp01(0.12f + sp.angularity * 0.68f + asymmetry * 0.22f),
                attackBias = Mathf.Clamp01(0.24f + sp.angularity * 0.6f + (1f - sp.circularity) * 0.12f),
                releaseBias = Mathf.Clamp01(0.22f + sp.circularity * 0.6f + sp.symmetry * 0.16f),
                detune = Mathf.Clamp01(0.05f + instability * 0.26f),
                modDepth = Mathf.Clamp01(0.08f + instability * 0.54f),
                stereoSpread = Mathf.Clamp01(0.12f + (1f - sp.aspectRatio) * 0.34f + asymmetry * 0.14f),
                grooveInstability = instability,
                delayBias = Mathf.Clamp01(0.04f + (1f - sp.aspectRatio) * 0.18f + instability * 0.18f),
                reverbBias = Mathf.Clamp01(0.08f + sp.circularity * 0.18f + asymmetry * 0.14f),
                waveMorph = Mathf.Clamp01(0.2f + sp.angularity * 0.62f),
                filterMotion = Mathf.Clamp01(0.1f + instability * 0.44f),
                transientSharpness = Mathf.Clamp01(0.25f + sp.angularity * 0.7f),
                body = Mathf.Clamp01(0.24f + sp.circularity * 0.58f + sp.symmetry * 0.14f)
            };
        }

        private static SoundProfile DeriveMelody(ShapeProfile sp, float smoothness, float asymmetry, float tiltAmount)
        {
            float contourPull = Mathf.Abs(sp.directionBias - 0.5f) * 2f;

            return new SoundProfile
            {
                brightness = Mathf.Clamp01(0.24f + sp.angularity * 0.4f + sp.centroidHeight * 0.14f + sp.verticalSpan * 0.16f),
                resonance = Mathf.Clamp01(0.16f + sp.curvatureMean * 0.44f + sp.curvatureVariance * 0.18f),
                drive = Mathf.Clamp01(0.08f + sp.angularity * 0.44f + sp.speedVariance * 0.2f),
                attackBias = Mathf.Clamp01(0.18f + sp.speedVariance * 0.52f + sp.angularity * 0.18f),
                releaseBias = Mathf.Clamp01(0.2f + smoothness * 0.56f + sp.horizontalSpan * 0.12f),
                detune = Mathf.Clamp01(0.08f + sp.curvatureVariance * 0.48f + asymmetry * 0.18f),
                modDepth = Mathf.Clamp01(0.12f + sp.curvatureMean * 0.36f + contourPull * 0.26f),
                stereoSpread = Mathf.Clamp01(0.14f + sp.horizontalSpan * 0.44f + contourPull * 0.12f),
                grooveInstability = Mathf.Clamp01(0.08f + sp.speedVariance * 0.28f),
                delayBias = Mathf.Clamp01(0.12f + contourPull * 0.34f + sp.horizontalSpan * 0.1f),
                reverbBias = Mathf.Clamp01(0.12f + smoothness * 0.28f + sp.verticalSpan * 0.16f),
                waveMorph = Mathf.Clamp01(0.16f + sp.angularity * 0.62f),
                filterMotion = Mathf.Clamp01(0.16f + contourPull * 0.42f + sp.curvatureVariance * 0.16f),
                transientSharpness = Mathf.Clamp01(0.2f + sp.angularity * 0.56f + sp.speedVariance * 0.18f),
                body = Mathf.Clamp01(0.18f + smoothness * 0.46f + sp.verticalSpan * 0.12f)
            };
        }

        private static SoundProfile DeriveHarmony(ShapeProfile sp, float smoothness, float asymmetry, float tiltAmount)
        {
            return new SoundProfile
            {
                brightness = Mathf.Clamp01(0.16f + sp.centroidHeight * 0.24f + sp.angularity * 0.18f + tiltAmount * 0.18f),
                resonance = Mathf.Clamp01(0.1f + tiltAmount * 0.44f + asymmetry * 0.2f),
                drive = Mathf.Clamp01(0.05f + sp.angularity * 0.26f + asymmetry * 0.12f),
                attackBias = Mathf.Clamp01(0.12f + sp.angularity * 0.42f + asymmetry * 0.12f),
                releaseBias = Mathf.Clamp01(0.32f + sp.pathLength * 0.28f + smoothness * 0.28f),
                detune = Mathf.Clamp01(0.12f + asymmetry * 0.42f + sp.horizontalSpan * 0.16f),
                modDepth = Mathf.Clamp01(0.16f + tiltAmount * 0.32f + asymmetry * 0.24f),
                stereoSpread = Mathf.Clamp01(0.24f + sp.horizontalSpan * 0.48f),
                grooveInstability = Mathf.Clamp01(0.02f + sp.curvatureVariance * 0.18f),
                delayBias = Mathf.Clamp01(0.08f + tiltAmount * 0.18f),
                reverbBias = Mathf.Clamp01(0.24f + sp.pathLength * 0.36f + smoothness * 0.18f),
                waveMorph = Mathf.Clamp01(0.22f + sp.angularity * 0.36f + tiltAmount * 0.18f),
                filterMotion = Mathf.Clamp01(0.22f + tiltAmount * 0.5f + asymmetry * 0.14f),
                transientSharpness = Mathf.Clamp01(0.08f + sp.angularity * 0.32f),
                body = Mathf.Clamp01(0.28f + smoothness * 0.42f + sp.verticalSpan * 0.12f)
            };
        }
    }
}
