#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Analysis;

namespace RhythmForge.Editor
{
    public class StrokeKinematicsAnalyzerTests
    {
        [Test]
        public void Analyze_EmptyKinematics_ReturnsZeroFeatures()
        {
            // Null kinematics
            var featuresNull = StrokeKinematicsAnalyzer.Analyze(null, Vector3.right, Vector3.up);
            Assert.That(featuresNull.pressureMean, Is.EqualTo(0f));
            Assert.That(featuresNull.tiltMean, Is.EqualTo(0f));
            Assert.That(featuresNull.speedMean, Is.EqualTo(0f));

            // Empty kinematics (no points)
            var empty = new StrokeKinematics();
            var featuresEmpty = StrokeKinematicsAnalyzer.Analyze(empty, Vector3.right, Vector3.up);
            Assert.That(featuresEmpty.pressureMean, Is.EqualTo(0f));
            Assert.That(featuresEmpty.tiltMean, Is.EqualTo(0f));
            Assert.That(featuresEmpty.speedMean, Is.EqualTo(0f));
            Assert.That(featuresEmpty.strokeSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void Analyze_ConstantPressure_PressureMeanEqualsConstant()
        {
            const float targetPressure = 0.7f;
            var k = BuildConstantPressureKinematics(targetPressure, pointCount: 10);

            var features = StrokeKinematicsAnalyzer.Analyze(k, Vector3.right, Vector3.up);

            Assert.That(features.pressureMean, Is.EqualTo(targetPressure).Within(0.001f));
            Assert.That(features.pressurePeak, Is.EqualTo(targetPressure).Within(0.001f));
        }

        [Test]
        public void Analyze_FlatStrokeForwardTilt_TiltMeanNonZero()
        {
            // Stylus pointing along planeRight (Quaternion.identity * Vector3.forward = Vector3.forward)
            // Use planeRight = Vector3.forward so that dot product is 1
            Vector3 planeRight = Vector3.forward;
            Vector3 planeUp = Vector3.up;

            var k = new StrokeKinematics();
            // All points have identity rotation, meaning stylus forward = Vector3.forward
            for (int i = 0; i < 5; i++)
            {
                k.AddPoint(new Vector3(i * 0.01f, 0f, 0f), 0.5f, 0f, Quaternion.identity, i * 0.1f);
            }

            var features = StrokeKinematicsAnalyzer.Analyze(k, planeRight, planeUp);

            // tiltXY.x = Dot(forward, planeRight=forward) = 1.0, so tiltMag = 1.0
            Assert.That(features.tiltMean, Is.GreaterThan(0f));
            // Specifically expect ~1.0 (stylus fully along planeRight)
            Assert.That(features.tiltMean, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void Analyze_DeceleratingStroke_SpeedTailOffPositive()
        {
            // Build a stroke where early segments are fast and later segments are slow
            var k = new StrokeKinematics();

            // 12 points: first 6 spread apart (high speed), last 6 clustered (low speed)
            // Using time intervals: all 0.1s apart so dt is constant
            // early: 0.05m per 0.1s = 0.5 m/s; late: 0.002m per 0.1s = 0.02 m/s
            float time = 0f;
            float x = 0f;
            for (int i = 0; i < 6; i++)
            {
                k.AddPoint(new Vector3(x, 0f, 0f), 0.5f, 0f, Quaternion.identity, time);
                x += 0.05f;
                time += 0.1f;
            }
            for (int i = 0; i < 6; i++)
            {
                k.AddPoint(new Vector3(x, 0f, 0f), 0.5f, 0f, Quaternion.identity, time);
                x += 0.002f;
                time += 0.1f;
            }

            var features = StrokeKinematicsAnalyzer.Analyze(k, Vector3.right, Vector3.up);

            Assert.That(features.speedTailOff, Is.GreaterThan(0f),
                "Decelerating stroke should produce speedTailOff > 0");
        }

        [Test]
        public void Analyze_FadingPressure_PressureSlopeEndNegative()
        {
            // Pressure starts high and fades in the last quarter
            var k = new StrokeKinematics();
            int n = 12;
            for (int i = 0; i < n; i++)
            {
                // First 9 points: pressure = 0.9; last 3 points (last quarter): pressure = 0.1
                float pressure = i < 9 ? 0.9f : 0.1f;
                k.AddPoint(new Vector3(i * 0.01f, 0f, 0f), pressure, 0f, Quaternion.identity, i * 0.1f);
            }

            var features = StrokeKinematicsAnalyzer.Analyze(k, Vector3.right, Vector3.up);

            Assert.That(features.pressureSlopeEnd, Is.LessThan(0f),
                "Fading pressure in the last quarter should give a negative pressureSlopeEnd");
        }

        // ----- Helpers -----

        private static StrokeKinematics BuildConstantPressureKinematics(float pressure, int pointCount)
        {
            var k = new StrokeKinematics();
            for (int i = 0; i < pointCount; i++)
            {
                k.AddPoint(new Vector3(i * 0.01f, 0f, 0f), pressure, 0f, Quaternion.identity, i * 0.1f);
            }
            return k;
        }
    }
}
#endif
