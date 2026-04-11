using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Analysis
{
    public static class ShapeProfileCalculator
    {
        public static ShapeProfile Derive(List<Vector2> points, StrokeMetrics metrics, PatternType type)
        {
            if (points == null || points.Count == 0)
            {
                return new ShapeProfile
                {
                    closedness = 0f, circularity = 0f, aspectRatio = 0f,
                    angularity = 0f, symmetry = 0.5f, verticalSpan = 0f,
                    horizontalSpan = 0f, pathLength = 0f, speedVariance = 0f,
                    curvatureMean = 0f, curvatureVariance = 0f,
                    centroidHeight = 0.5f, directionBias = 0.5f,
                    tilt = 0.5f, tiltSigned = 0f, wobble = 0f
                };
            }

            float width = Mathf.Max(0.001f, metrics.width);
            float height = Mathf.Max(0.001f, metrics.height);

            // Centroid
            float cx = 0f, cy = 0f;
            foreach (var p in points) { cx += p.x; cy += p.y; }
            cx /= points.Count;
            cy /= points.Count;
            Vector2 centroid = new Vector2(cx, cy);

            // Radii
            var radii = new List<float>(points.Count);
            foreach (var p in points)
                radii.Add(Vector2.Distance(p, centroid));
            float meanRadius = MathUtils.Mean(radii);

            // Close distance
            float closeDistance = Vector2.Distance(points[0], points[points.Count - 1]);

            // Segment lengths and turns
            var segmentLengths = new List<float>(points.Count - 1);
            var turns = new List<float>();

            for (int i = 1; i < points.Count; i++)
                segmentLengths.Add(Vector2.Distance(points[i - 1], points[i]));

            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector2 prev = points[i - 1];
                Vector2 curr = points[i];
                Vector2 next = points[i + 1];
                float angleA = Mathf.Atan2(curr.y - prev.y, curr.x - prev.x);
                float angleB = Mathf.Atan2(next.y - curr.y, next.x - curr.x);
                turns.Add(MathUtils.WrapAngle(angleB - angleA));
            }

            float meanSegLen = MathUtils.Mean(segmentLengths);
            var absTurns = new List<float>(turns.Count);
            foreach (var t in turns) absTurns.Add(Mathf.Abs(t));

            // Normalized path length (pilot divides by 2.75 with pixel scale;
            // for world-space we use the same normalization range, thresholded at 1)
            float pathLength = Mathf.Clamp01(metrics.length / 2.75f);

            // Closedness
            float closedness = Mathf.Clamp01(1f - closeDistance / 0.28f);

            // Circularity
            float perimeter = metrics.length + (type == PatternType.RhythmLoop ? closeDistance : 0f);
            float circularity = perimeter > 0f
                ? Mathf.Clamp01((4f * Mathf.PI * MathUtils.PolygonArea(points)) / Mathf.Pow(perimeter, 2f) / 0.9f)
                : 0f;

            // Aspect ratio
            float aspectRatio = Mathf.Clamp01(Mathf.Min(width, height) / Mathf.Max(width, height));

            // Curvature
            float curvatureMean = Mathf.Clamp01(MathUtils.Mean(absTurns) / (Mathf.PI * 0.55f));
            float curvatureVariance = Mathf.Clamp01(MathUtils.StandardDeviation(absTurns) / (Mathf.PI * 0.4f));

            // Angularity
            float angularity = Mathf.Clamp01(curvatureMean * 0.72f + curvatureVariance * 0.52f);

            // Symmetry
            float symmetry = DeriveSymmetryScore(points, centroid);

            // Direction bias
            float directionBias = Mathf.Clamp01(
                (points[points.Count - 1].x - points[0].x) / (width + 0.001f) * 0.5f + 0.5f);

            // Tilt signed
            float tiltSigned = Mathf.Clamp(
                (points[points.Count - 1].y - points[0].y) / (height + 0.001f), -1f, 1f);

            // Speed variance
            float speedVariance = Mathf.Clamp01(
                MathUtils.StandardDeviation(segmentLengths) / Mathf.Max(meanSegLen, 0.001f) / 0.95f);

            // Wobble
            float wobble = Mathf.Clamp01(
                MathUtils.StandardDeviation(radii) / Mathf.Max(meanRadius, 0.001f) / 0.4f);

            return new ShapeProfile
            {
                closedness = closedness,
                circularity = type == PatternType.RhythmLoop
                    ? circularity
                    : Mathf.Clamp01(circularity * 0.35f + closedness * 0.3f),
                aspectRatio = aspectRatio,
                angularity = angularity,
                symmetry = symmetry,
                verticalSpan = Mathf.Clamp01(height / 0.95f),
                horizontalSpan = Mathf.Clamp01(width / 0.95f),
                pathLength = pathLength,
                speedVariance = speedVariance,
                curvatureMean = curvatureMean,
                curvatureVariance = curvatureVariance,
                centroidHeight = Mathf.Clamp01(1f - centroid.y),
                directionBias = directionBias,
                tilt = Mathf.Clamp01((tiltSigned + 1f) / 2f),
                tiltSigned = tiltSigned,
                wobble = wobble
            };
        }

        private static float DeriveSymmetryScore(List<Vector2> points, Vector2 centroid)
        {
            var samples = StrokeAnalyzer.ResampleStroke(points, 20);
            if (samples.Count == 0) return 0.5f;

            float error = 0f;
            for (int i = 0; i < samples.Count; i++)
            {
                Vector2 sample = samples[i];
                Vector2 mirrored = samples[samples.Count - 1 - i];
                float sampleDx = sample.x - centroid.x;
                float mirroredDx = mirrored.x - centroid.x;
                float sampleDy = sample.y - centroid.y;
                float mirroredDy = mirrored.y - centroid.y;
                error += Mathf.Abs(Mathf.Abs(sampleDx) - Mathf.Abs(mirroredDx))
                       + Mathf.Abs(sampleDy + mirroredDy) * 0.65f;
            }

            return Mathf.Clamp01(1f - error / (samples.Count * 0.55f));
        }
    }
}
