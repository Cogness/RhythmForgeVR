using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Analysis
{
    [Serializable]
    public class StrokeMetrics
    {
        public float minX, maxX, minY, maxY;
        public float width, height;
        public float centerX, centerY;
        public float length;
        public float averageSize;
        public float wobble;
        public bool closed;
        public float tilt;
    }

    public static class StrokeAnalyzer
    {
        // World-space threshold for closed stroke detection (pilot: 64px)
        private const float CloseDistanceThreshold = 0.08f;

        public static StrokeMetrics Analyze(List<Vector2> points)
        {
            if (points == null || points.Count < 2)
            {
                return new StrokeMetrics();
            }

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            foreach (var p in points)
            {
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }

            float w = Mathf.Max(0.0001f, maxX - minX);
            float h = Mathf.Max(0.0001f, maxY - minY);
            float cx = minX + w / 2f;
            float cy = minY + h / 2f;

            float length = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                length += Vector2.Distance(points[i - 1], points[i]);
            }

            float averageSize = (w + h) / 2f;
            Vector2 center = new Vector2(cx, cy);

            List<float> radii = new List<float>(points.Count);
            foreach (var p in points)
            {
                radii.Add(Vector2.Distance(p, center));
            }

            float meanRadius = MathUtils.Mean(radii);
            float variance = 0f;
            foreach (var r in radii)
            {
                float diff = r - meanRadius;
                variance += diff * diff;
            }
            variance /= Mathf.Max(1f, radii.Count);
            float wobble = Mathf.Sqrt(variance) / Mathf.Max(1f, meanRadius);

            float closeDistance = Vector2.Distance(points[0], points[points.Count - 1]);
            bool closed = closeDistance < Mathf.Min(CloseDistanceThreshold, averageSize * 0.32f);

            float dx = points[points.Count - 1].x - points[0].x;
            float dy = points[points.Count - 1].y - points[0].y;
            float tilt = Mathf.Atan2(dy, dx);

            return new StrokeMetrics
            {
                minX = minX, maxX = maxX,
                minY = minY, maxY = maxY,
                width = w, height = h,
                centerX = cx, centerY = cy,
                length = length,
                averageSize = averageSize,
                wobble = wobble,
                closed = closed,
                tilt = tilt
            };
        }

        public static List<Vector2> NormalizePoints(List<Vector2> points, StrokeMetrics metrics)
        {
            float size = Mathf.Max(metrics.width, metrics.height);
            float originX = metrics.centerX - size / 2f;
            float originY = metrics.centerY - size / 2f;

            var normalized = new List<Vector2>(points.Count);
            foreach (var p in points)
            {
                normalized.Add(new Vector2(
                    MathUtils.RoundTo((p.x - originX) / size, 4),
                    MathUtils.RoundTo((p.y - originY) / size, 4)
                ));
            }
            return normalized;
        }

        public static List<Vector2> ResampleStroke(List<Vector2> points, int sampleCount)
        {
            if (points.Count <= 1)
                return new List<Vector2>(points);

            var segments = new List<float>(points.Count) { 0f };
            float totalLength = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                totalLength += Vector2.Distance(points[i - 1], points[i]);
                segments.Add(totalLength);
            }

            var result = new List<Vector2>(sampleCount);
            for (int s = 0; s < sampleCount; s++)
            {
                float target = (s / Mathf.Max(1f, sampleCount - 1f)) * totalLength;
                int segIndex = 1;
                while (segIndex < segments.Count && segments[segIndex] < target)
                    segIndex++;

                int currentIndex = Mathf.Clamp(segIndex, 1, points.Count - 1);
                float prevLen = segments[currentIndex - 1];
                float nextLen = segments[currentIndex];
                float local = Mathf.Approximately(nextLen, prevLen)
                    ? 0f
                    : (target - prevLen) / (nextLen - prevLen);

                result.Add(Vector2.Lerp(points[currentIndex - 1], points[currentIndex], local));
            }
            return result;
        }
    }
}
