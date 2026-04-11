using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmForge.Core
{
    public static class MathUtils
    {
        public static float Clamp(float value, float min, float max)
        {
            return Mathf.Clamp(value, min, max);
        }

        public static float Clamp01(float value)
        {
            return Mathf.Clamp01(value);
        }

        public static int ClampInt(int value, int min, int max)
        {
            return Mathf.Clamp(value, min, max);
        }

        public static float Dist(Vector2 a, Vector2 b)
        {
            return Vector2.Distance(a, b);
        }

        public static float Lerp(float a, float b, float t)
        {
            return Mathf.Lerp(a, b, t);
        }

        public static float RoundTo(float value, int decimals)
        {
            float mult = Mathf.Pow(10f, decimals);
            return Mathf.Round(value * mult) / mult;
        }

        public static float PolygonArea(List<Vector2> points)
        {
            if (points.Count < 3) return 0f;
            float area = 0f;
            int n = points.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += points[i].x * points[j].y;
                area -= points[j].x * points[i].y;
            }
            return Mathf.Abs(area) * 0.5f;
        }

        public static float StandardDeviation(List<float> values)
        {
            if (values.Count < 2) return 0f;
            float mean = Mean(values);
            float sumSqDiff = 0f;
            foreach (var v in values)
            {
                float diff = v - mean;
                sumSqDiff += diff * diff;
            }
            return Mathf.Sqrt(sumSqDiff / values.Count);
        }

        public static float Mean(List<float> values)
        {
            if (values.Count == 0) return 0f;
            float sum = 0f;
            foreach (var v in values)
                sum += v;
            return sum / values.Count;
        }

        public static float WrapAngle(float angle)
        {
            while (angle > Mathf.PI) angle -= 2f * Mathf.PI;
            while (angle < -Mathf.PI) angle += 2f * Mathf.PI;
            return angle;
        }

        public static string CreateId(string prefix)
        {
            return $"{prefix}-{Guid.NewGuid().ToString("N").Substring(0, 12)}";
        }

        public static float MidiToFrequency(int midi)
        {
            return 440f * Mathf.Pow(2f, (midi - 69f) / 12f);
        }
    }
}
