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
            float sizeFactor = ShapeProfileSizing.GetSizeFactor(type, sp);

            switch (type)
            {
                case PatternType.RhythmLoop:
                    return DeriveRhythm(sp, smoothness, asymmetry, sizeFactor);
                case PatternType.MelodyLine:
                    return DeriveMelody(sp, smoothness, asymmetry, tiltAmount, sizeFactor);
                default:
                    return DeriveHarmony(sp, smoothness, asymmetry, tiltAmount, sizeFactor);
            }
        }

        private static SoundProfile DeriveRhythm(ShapeProfile sp, float smoothness, float asymmetry, float sizeFactor)
        {
            float instability = Mathf.Clamp01(
                sp.wobble * 0.7f + asymmetry * 0.55f + sp.curvatureVariance * 0.35f);
            float compactness = 1f - sizeFactor;

            return new SoundProfile
            {
                brightness = Mathf.Clamp01(0.16f + sp.angularity * 0.4f + instability * 0.14f + compactness * 0.24f),
                resonance = Mathf.Clamp01(0.14f + sp.angularity * 0.26f + sp.curvatureVariance * 0.18f + sizeFactor * 0.12f),
                drive = Mathf.Clamp01(0.1f + sp.angularity * 0.52f + asymmetry * 0.18f + sizeFactor * 0.12f),
                attackBias = Mathf.Clamp01(0.26f + sp.angularity * 0.54f + compactness * 0.24f + (1f - sp.circularity) * 0.08f),
                releaseBias = Mathf.Clamp01(0.1f + sp.circularity * 0.34f + sp.symmetry * 0.08f + sizeFactor * 0.54f),
                detune = Mathf.Clamp01(0.04f + instability * 0.22f + sizeFactor * 0.08f),
                modDepth = Mathf.Clamp01(0.06f + instability * 0.34f + sizeFactor * 0.18f),
                stereoSpread = Mathf.Clamp01(0.08f + (1f - sp.aspectRatio) * 0.2f + asymmetry * 0.12f + sizeFactor * 0.26f),
                grooveInstability = Mathf.Clamp01(instability * 0.78f + sizeFactor * 0.26f),
                delayBias = Mathf.Clamp01(0.03f + instability * 0.14f + sizeFactor * 0.16f),
                reverbBias = Mathf.Clamp01(0.04f + sp.circularity * 0.12f + asymmetry * 0.08f + sizeFactor * 0.48f),
                waveMorph = Mathf.Clamp01(0.18f + sp.angularity * 0.58f + compactness * 0.08f),
                filterMotion = Mathf.Clamp01(0.08f + instability * 0.26f + sizeFactor * 0.18f),
                transientSharpness = Mathf.Clamp01(0.26f + sp.angularity * 0.58f + compactness * 0.22f),
                body = Mathf.Clamp01(0.12f + sp.circularity * 0.28f + sp.symmetry * 0.08f + sizeFactor * 0.62f)
            };
        }

        private static SoundProfile DeriveMelody(ShapeProfile sp, float smoothness, float asymmetry, float tiltAmount, float sizeFactor)
        {
            float contourPull = Mathf.Abs(sp.directionBias - 0.5f) * 2f;
            float compactness = 1f - sizeFactor;

            return new SoundProfile
            {
                brightness = Mathf.Clamp01(0.18f + sp.angularity * 0.28f + sp.centroidHeight * 0.12f + sp.verticalSpan * 0.08f + compactness * 0.32f),
                resonance = Mathf.Clamp01(0.14f + sp.curvatureMean * 0.3f + sp.curvatureVariance * 0.14f + compactness * 0.08f),
                drive = Mathf.Clamp01(0.06f + sp.angularity * 0.28f + sp.speedVariance * 0.12f + compactness * 0.12f),
                attackBias = Mathf.Clamp01(0.14f + sp.speedVariance * 0.34f + sp.angularity * 0.14f + compactness * 0.24f),
                releaseBias = Mathf.Clamp01(0.1f + smoothness * 0.24f + sp.horizontalSpan * 0.06f + sizeFactor * 0.56f),
                detune = Mathf.Clamp01(0.06f + sp.curvatureVariance * 0.28f + asymmetry * 0.14f + sizeFactor * 0.14f),
                modDepth = Mathf.Clamp01(0.08f + sp.curvatureMean * 0.2f + contourPull * 0.18f + sizeFactor * 0.36f),
                stereoSpread = Mathf.Clamp01(0.08f + sp.horizontalSpan * 0.18f + contourPull * 0.08f + sizeFactor * 0.46f),
                grooveInstability = Mathf.Clamp01(0.06f + sp.speedVariance * 0.22f + compactness * 0.06f),
                delayBias = Mathf.Clamp01(0.06f + contourPull * 0.18f + sizeFactor * 0.24f),
                reverbBias = Mathf.Clamp01(0.08f + smoothness * 0.16f + sp.verticalSpan * 0.08f + sizeFactor * 0.4f),
                waveMorph = Mathf.Clamp01(0.14f + sp.angularity * 0.5f + compactness * 0.08f),
                filterMotion = Mathf.Clamp01(0.1f + contourPull * 0.26f + sp.curvatureVariance * 0.12f + sizeFactor * 0.34f),
                transientSharpness = Mathf.Clamp01(0.16f + sp.angularity * 0.38f + sp.speedVariance * 0.12f + compactness * 0.28f),
                body = Mathf.Clamp01(0.1f + smoothness * 0.2f + sp.verticalSpan * 0.08f + sizeFactor * 0.58f)
            };
        }

        private static SoundProfile DeriveHarmony(ShapeProfile sp, float smoothness, float asymmetry, float tiltAmount, float sizeFactor)
        {
            float compactness = 1f - sizeFactor;

            return new SoundProfile
            {
                brightness = Mathf.Clamp01(0.12f + sp.centroidHeight * 0.18f + sp.angularity * 0.12f + tiltAmount * 0.12f + compactness * 0.26f),
                resonance = Mathf.Clamp01(0.08f + tiltAmount * 0.3f + asymmetry * 0.14f + compactness * 0.08f),
                drive = Mathf.Clamp01(0.04f + sp.angularity * 0.16f + asymmetry * 0.08f + compactness * 0.06f),
                attackBias = Mathf.Clamp01(0.1f + sp.angularity * 0.24f + asymmetry * 0.08f + compactness * 0.22f),
                releaseBias = Mathf.Clamp01(0.16f + sp.pathLength * 0.08f + smoothness * 0.16f + sizeFactor * 0.58f),
                detune = Mathf.Clamp01(0.08f + asymmetry * 0.26f + sp.horizontalSpan * 0.08f + sizeFactor * 0.42f),
                modDepth = Mathf.Clamp01(0.12f + tiltAmount * 0.22f + asymmetry * 0.16f + sizeFactor * 0.36f),
                stereoSpread = Mathf.Clamp01(0.14f + sp.horizontalSpan * 0.16f + sizeFactor * 0.56f),
                grooveInstability = Mathf.Clamp01(0.02f + sp.curvatureVariance * 0.12f),
                delayBias = Mathf.Clamp01(0.06f + tiltAmount * 0.1f + sizeFactor * 0.16f),
                reverbBias = Mathf.Clamp01(0.12f + sp.pathLength * 0.08f + smoothness * 0.12f + sizeFactor * 0.58f),
                waveMorph = Mathf.Clamp01(0.18f + sp.angularity * 0.22f + tiltAmount * 0.1f + sizeFactor * 0.08f),
                filterMotion = Mathf.Clamp01(0.16f + tiltAmount * 0.3f + asymmetry * 0.12f + sizeFactor * 0.4f),
                transientSharpness = Mathf.Clamp01(0.06f + sp.angularity * 0.22f + compactness * 0.12f),
                body = Mathf.Clamp01(0.14f + smoothness * 0.18f + sp.verticalSpan * 0.06f + sizeFactor * 0.66f)
            };
        }
    }
}
