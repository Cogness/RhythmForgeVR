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
                    tilt = 0.5f, tiltSigned = 0f, wobble = 0f,
                    worldWidth = 0f, worldHeight = 0f, worldLength = 0f,
                    worldAverageSize = 0f, worldMaxDimension = 0f
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
            bool percussionLike = PatternTypeCompatibility.IsPercussion(type);
            float perimeter = metrics.length + (percussionLike ? closeDistance : 0f);
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
                circularity = percussionLike
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
                wobble = wobble,
                worldWidth = metrics.width,
                worldHeight = metrics.height,
                worldLength = metrics.length,
                worldAverageSize = metrics.averageSize,
                worldMaxDimension = Mathf.Max(metrics.width, metrics.height)
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

    public static class ShapeProfileSizing
    {
        public const float LegacySpanNormalization = 0.95f;
        public const float LegacyPathNormalization = 2.75f;

        public static float GetSizeFactor(PatternType type, ShapeProfile sp)
        {
            if (sp == null)
                return 0f;

            float width = Mathf.Max(0f, sp.worldWidth);
            float height = Mathf.Max(0f, sp.worldHeight);
            float length = Mathf.Max(0f, sp.worldLength);
            float averageSize = Mathf.Max(0f, sp.worldAverageSize);
            float maxDimension = Mathf.Max(0f, sp.worldMaxDimension);

            switch (PatternTypeCompatibility.Canonicalize(type))
            {
                case PatternType.Percussion:
                    return NormalizeRange(Mathf.Max(averageSize, maxDimension * 0.92f), 0.14f, 0.52f);

                case PatternType.Melody:
                case PatternType.Bass:
                case PatternType.Groove:
                {
                    float melodicExtent = Mathf.Max(length * 0.7f, averageSize * 1.15f + maxDimension * 0.2f);
                    return NormalizeRange(melodicExtent, 0.18f, 1.05f);
                }

                default:
                {
                    float harmonicExtent = Mathf.Max(length * 0.58f, averageSize * 1.35f + width * 0.22f + height * 0.22f);
                    return NormalizeRange(harmonicExtent, 0.16f, 0.92f);
                }
            }
        }

        public static string DescribeSize(PatternType type, ShapeProfile sp)
        {
            float sizeFactor = GetSizeFactor(type, sp);
            if (sizeFactor < 0.33f) return "compact";
            if (sizeFactor > 0.68f) return "expanded";
            return "medium";
        }

        public static void BackfillLegacyWorldMetrics(ShapeProfile sp)
        {
            if (sp == null) return;

            sp.worldWidth = BackfillLegacyMetric(sp.worldWidth, sp.horizontalSpan, LegacySpanNormalization, 1.08f);
            sp.worldHeight = BackfillLegacyMetric(sp.worldHeight, sp.verticalSpan, LegacySpanNormalization, 1.08f);
            sp.worldLength = BackfillLegacyMetric(sp.worldLength, sp.pathLength, LegacyPathNormalization, 1.06f);

            float fallbackAverage = (Mathf.Max(0f, sp.worldWidth) + Mathf.Max(0f, sp.worldHeight)) * 0.5f;
            sp.worldAverageSize = sp.worldAverageSize > 0.0001f
                ? sp.worldAverageSize
                : fallbackAverage > 0.0001f ? fallbackAverage : sp.worldLength * 0.18f;

            sp.worldMaxDimension = sp.worldMaxDimension > 0.0001f
                ? sp.worldMaxDimension
                : Mathf.Max(Mathf.Max(sp.worldWidth, sp.worldHeight), sp.worldAverageSize);
        }

        private static float BackfillLegacyMetric(float currentValue, float normalizedValue, float range, float saturatedBoost)
        {
            if (currentValue > 0.0001f)
                return currentValue;

            if (normalizedValue <= 0.0001f)
                return 0f;

            if (normalizedValue >= 0.999f)
                return range * saturatedBoost;

            return normalizedValue * range;
        }

        private static float NormalizeRange(float value, float min, float max)
        {
            if (max <= min)
                return 0f;

            return Mathf.Clamp01((value - min) / (max - min));
        }
    }
}
