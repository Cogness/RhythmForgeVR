#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;

namespace RhythmForge.Editor
{
    public class ShapeProfile3DCalculatorTests
    {
        [Test]
        public void Derive_CapturesPeakPressureAndExpressionFlags()
        {
            var samples = new List<StrokeSample>
            {
                new StrokeSample
                {
                    worldPos = new Vector3(0f, 0f, 0f),
                    pressure = 0.2f,
                    stylusRot = Quaternion.identity,
                    timestamp = 0.0,
                    ornamentFlag = true
                },
                new StrokeSample
                {
                    worldPos = new Vector3(0.1f, 0f, 0f),
                    pressure = 0.8f,
                    stylusRot = Quaternion.Euler(15f, 0f, 0f),
                    timestamp = 0.1,
                    accentFlag = true
                },
                new StrokeSample
                {
                    worldPos = new Vector3(0.2f, 0.05f, 0.02f),
                    pressure = 0.5f,
                    stylusRot = Quaternion.Euler(30f, 0f, 0f),
                    timestamp = 0.2
                }
            };

            var profile = ShapeProfile3DCalculator.Derive(
                samples,
                new ShapeProfile { worldMaxDimension = 0.2f, worldLength = 0.25f },
                Vector3.up);

            Assert.That(profile.thicknessPeak, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(profile.thicknessPeak, Is.GreaterThanOrEqualTo(profile.thicknessMean));
            Assert.That(profile.ornamentFlag, Is.True);
            Assert.That(profile.accentFlag, Is.True);
        }
    }
}
#endif
