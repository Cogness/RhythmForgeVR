using System;
using UnityEngine;

namespace RhythmForge.Core.Analysis
{
    /// <summary>
    /// Computes per-sample kinematic features (tiltXY, speed) and aggregate
    /// features from a captured StrokeKinematics record.
    /// </summary>
    public static class StrokeKinematicsAnalyzer
    {
        public static KinematicFeatures Analyze(StrokeKinematics k, Vector3 planeRight, Vector3 planeUp)
        {
            if (k == null || k.points.Count == 0)
                return new KinematicFeatures();

            // --- Fill per-point tiltXY and speed ---
            for (int i = 0; i < k.points.Count; i++)
            {
                var pt = k.points[i];

                // Tilt: project stylus forward (rotation * Vector3.forward) onto stroke plane
                Vector3 stylusForward = pt.rotation * Vector3.forward;
                pt.tiltXY = new Vector2(
                    Vector3.Dot(stylusForward, planeRight),
                    Vector3.Dot(stylusForward, planeUp)
                );

                // Speed: distance / time delta
                if (i > 0)
                {
                    float dt = pt.time - k.points[i - 1].time;
                    float dist = Vector3.Distance(pt.position, k.points[i - 1].position);
                    pt.speed = dt > 0.0001f ? dist / dt : 0f;
                }
                else
                {
                    pt.speed = 0f;
                }
            }

            // --- Aggregate features ---
            int n = k.points.Count;
            float pressureSum = 0f, pressureSumSq = 0f, pressurePeak = 0f;
            float tiltMagSum = 0f, tiltMagSumSq = 0f;
            float speedSum = 0f, speedPeak = 0f;

            for (int i = 0; i < n; i++)
            {
                var pt = k.points[i];
                pressureSum += pt.pressure;
                pressureSumSq += pt.pressure * pt.pressure;
                if (pt.pressure > pressurePeak) pressurePeak = pt.pressure;

                float tiltMag = pt.tiltXY.magnitude;
                tiltMagSum += tiltMag;
                tiltMagSumSq += tiltMag * tiltMag;

                if (i > 0) // skip index 0 (speed=0 initialization)
                {
                    speedSum += pt.speed;
                    if (pt.speed > speedPeak) speedPeak = pt.speed;
                }
            }

            float invN = 1f / n;
            float pressureMean = pressureSum * invN;
            float pressureVar = Mathf.Max(0f, pressureSumSq * invN - pressureMean * pressureMean);
            float tiltMean = tiltMagSum * invN;
            float tiltVar = Mathf.Max(0f, tiltMagSumSq * invN - tiltMean * tiltMean);
            float speedMean = n > 1 ? speedSum / (n - 1) : 0f;

            // pressureSlopeEnd: compare last-quarter average to overall mean
            int tailStart = Mathf.Max(0, n * 3 / 4);
            float tailSum = 0f;
            int tailCount = 0;
            for (int i = tailStart; i < n; i++) { tailSum += k.points[i].pressure; tailCount++; }
            float pressureSlopeEnd = tailCount > 0 ? (tailSum / tailCount) - pressureMean : 0f;

            // speedTailOff: compare last-quarter speed to mean speed
            int speedTailStart = Mathf.Max(1, n * 3 / 4);
            float speedTailSum = 0f;
            int speedTailCount = 0;
            for (int i = speedTailStart; i < n; i++) { speedTailSum += k.points[i].speed; speedTailCount++; }
            float speedTailOff = speedTailCount > 0 ? speedMean - (speedTailSum / speedTailCount) : 0f;

            return new KinematicFeatures
            {
                pressureMean = pressureMean,
                pressureVariance = pressureVar,
                pressurePeak = pressurePeak,
                pressureSlopeEnd = pressureSlopeEnd,
                tiltMean = tiltMean,
                tiltVariance = tiltVar,
                speedMean = speedMean > 0f ? speedMean / 2f : 0f, // normalize to ~0-1 range (2 m/s typical)
                speedPeak = speedPeak > 0f ? speedPeak / 4f : 0f,
                speedTailOff = Mathf.Clamp01(speedTailOff / 1f),
                strokeSeconds = k.totalTime
            };
        }
    }

    public struct KinematicFeatures
    {
        public float pressureMean;       // 0–1
        public float pressureVariance;   // 0–1
        public float pressurePeak;       // 0–1
        public float pressureSlopeEnd;   // negative = fading stroke, positive = building
        public float tiltMean;           // magnitude 0–1 (1 = fully in-plane)
        public float tiltVariance;       // 0–1
        public float speedMean;          // normalized m/s (0–1 approx)
        public float speedPeak;          // normalized m/s
        public float speedTailOff;       // 0 = maintained speed, 1 = strong deceleration
        public float strokeSeconds;
    }
}
