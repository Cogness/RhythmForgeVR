using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Analysis;

namespace RhythmForge.Core.Sequencing
{
    public static class StrokeResampler
    {
        public static List<Vector2> Resample(List<Vector2> points, int sampleCount)
        {
            return StrokeAnalyzer.ResampleStroke(points, sampleCount);
        }

        public static List<Vector2> Normalize(List<Vector2> points, StrokeMetrics metrics)
        {
            return StrokeAnalyzer.NormalizePoints(points, metrics);
        }
    }
}
