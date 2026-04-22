using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    public static class GrooveShapeMapper
    {
        private static readonly float[] BaseAccentCurve = { 1f, 0.7f, 0.85f, 0.7f };

        public static GrooveProfile Map(ShapeProfile shapeProfile)
        {
            shapeProfile = shapeProfile ?? new ShapeProfile();

            float density = Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(shapeProfile.pathLength));
            float syncopation = Mathf.Lerp(0f, 0.5f, Mathf.Clamp01(shapeProfile.angularity));
            float swing = Mathf.Lerp(0f, 0.42f, Mathf.Clamp01(shapeProfile.curvatureVariance));
            int quantizeGrid = shapeProfile.speedVariance > 0.65f ? 16 : 8;
            float accentAmplitude = Mathf.Lerp(0.6f, 1.4f, Mathf.Clamp01(shapeProfile.verticalSpan));

            return new GrooveProfile
            {
                density = MathUtils.RoundTo(density, 2),
                syncopation = MathUtils.RoundTo(syncopation, 2),
                swing = MathUtils.RoundTo(swing, 2),
                quantizeGrid = quantizeGrid,
                accentCurve = BuildAccentCurve(accentAmplitude)
            };
        }

        private static float[] BuildAccentCurve(float accentAmplitude)
        {
            var accents = new float[BaseAccentCurve.Length];
            for (int i = 0; i < BaseAccentCurve.Length; i++)
            {
                float deviationFromDownbeat = BaseAccentCurve[i] - 1f;
                accents[i] = Mathf.Clamp(MathUtils.RoundTo(1f + deviationFromDownbeat * accentAmplitude, 2), 0.45f, 1.25f);
            }

            return accents;
        }
    }
}
