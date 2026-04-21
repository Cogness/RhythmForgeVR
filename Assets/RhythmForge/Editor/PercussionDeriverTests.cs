#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Editor
{
    public class PercussionDeriverTests
    {
        [Test]
        public void KickOnStepZero_AlwaysPresent()
        {
            var result = Derive(CreateShape(aspectRatio: 0.4f, circularity: 0.86f, symmetry: 0.9f, angularity: 0.2f));

            Assert.That(HasLaneAtStep(result.derivedSequence.events, 0, "kick"), Is.True);
        }

        [Test]
        public void SnareOnStep8_AlwaysPresent()
        {
            var result = Derive(CreateShape(aspectRatio: 0.9f, circularity: 0.2f, symmetry: 0.95f, angularity: 0.1f));

            Assert.That(HasLaneAtStep(result.derivedSequence.events, 8, "snare"), Is.True);
        }

        [Test]
        public void Bar4AndBar8_ContainFillEvents()
        {
            var result = Derive(CreateShape(aspectRatio: 0.7f, circularity: 0.4f, symmetry: 0.3f, angularity: 0.8f));
            int barSteps = AppStateFactory.BarSteps;

            Assert.That(HasLaneAtStep(result.derivedSequence.events, barSteps * 3 + 14, "snare"), Is.True);
            Assert.That(HasLaneAtStep(result.derivedSequence.events, barSteps * 3 + 15, "snare"), Is.True);
            Assert.That(HasLaneAtStep(result.derivedSequence.events, barSteps * 7 + 13, "snare"), Is.True);
            Assert.That(HasLaneAtStep(result.derivedSequence.events, barSteps * 7 + 14, "snare"), Is.True);
            Assert.That(HasLaneAtStep(result.derivedSequence.events, barSteps * 7 + 15, "snare"), Is.True);
        }

        private static RhythmDerivationResult Derive(ShapeProfile shapeProfile)
        {
            return PercussionDeriver.Derive(
                new List<Vector2> { Vector2.zero, new Vector2(0.2f, 0.1f), new Vector2(0.4f, 0.3f), new Vector2(0.6f, 0.12f) },
                new StrokeMetrics { averageSize = 0.24f, length = 0.8f, closed = true },
                "lofi",
                shapeProfile,
                new SoundProfile
                {
                    body = 0.6f,
                    brightness = 0.5f,
                    grooveInstability = 0.18f,
                    transientSharpness = 0.44f,
                    drive = 0.38f
                });
        }

        private static ShapeProfile CreateShape(float aspectRatio, float circularity, float symmetry, float angularity)
        {
            return new ShapeProfile
            {
                aspectRatio = aspectRatio,
                circularity = circularity,
                symmetry = symmetry,
                angularity = angularity,
                wobble = 0.2f,
                curvatureVariance = 0.34f,
                centroidHeight = 0.5f,
                horizontalSpan = 0.6f,
                verticalSpan = 0.55f
            };
        }

        private static bool HasLaneAtStep(List<RhythmEvent> events, int step, string lane)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].step == step && events[i].lane == lane)
                    return true;
            }

            return false;
        }
    }
}
#endif
