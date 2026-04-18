#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Session;
using RhythmForge.UI;

namespace RhythmForge.Editor
{
    public class ShapeSizeBehaviorTests
    {
        [Test]
        public void ShapeProfileCalculator_CapturesWorldSize_WithoutChangingTopology()
        {
            var smallPoints = GenerateCircle(24, 0.08f);
            var largePoints = GenerateCircle(24, 0.22f);

            var smallMetrics = StrokeAnalyzer.Analyze(smallPoints);
            var largeMetrics = StrokeAnalyzer.Analyze(largePoints);

            var smallProfile = ShapeProfileCalculator.Derive(
                StrokeAnalyzer.NormalizePoints(smallPoints, smallMetrics), smallMetrics, PatternType.RhythmLoop);
            var largeProfile = ShapeProfileCalculator.Derive(
                StrokeAnalyzer.NormalizePoints(largePoints, largeMetrics), largeMetrics, PatternType.RhythmLoop);

            Assert.That(largeProfile.worldAverageSize, Is.GreaterThan(smallProfile.worldAverageSize * 2f));
            Assert.That(largeProfile.worldMaxDimension, Is.GreaterThan(smallProfile.worldMaxDimension * 2f));
            Assert.That(Mathf.Abs(largeProfile.circularity - smallProfile.circularity), Is.LessThan(0.05f));
            Assert.That(Mathf.Abs(largeProfile.aspectRatio - smallProfile.aspectRatio), Is.LessThan(0.05f));
        }

        [Test]
        public void LargerRhythmLoop_ProducesFullerAndLooserSoundProfile()
        {
            var smallSound = DeriveSound(PatternType.RhythmLoop, GenerateCircle(24, 0.08f));
            var largeSound = DeriveSound(PatternType.RhythmLoop, GenerateCircle(24, 0.22f));

            Assert.That(largeSound.body, Is.GreaterThan(smallSound.body));
            Assert.That(largeSound.releaseBias, Is.GreaterThan(smallSound.releaseBias));
            Assert.That(largeSound.reverbBias, Is.GreaterThan(smallSound.reverbBias));
            Assert.That(largeSound.grooveInstability, Is.GreaterThan(smallSound.grooveInstability));
            Assert.That(smallSound.transientSharpness, Is.GreaterThan(largeSound.transientSharpness));
            Assert.That(smallSound.attackBias, Is.GreaterThan(largeSound.attackBias));
        }

        [Test]
        public void LargerMelodyLine_ProducesWiderAndMoreSustainedSoundProfile()
        {
            var smallSound = DeriveSound(PatternType.MelodyLine, GenerateWave(32, 0.16f, 0.04f));
            var largeSound = DeriveSound(PatternType.MelodyLine, GenerateWave(32, 0.46f, 0.12f));

            Assert.That(largeSound.body, Is.GreaterThan(smallSound.body));
            Assert.That(largeSound.releaseBias, Is.GreaterThan(smallSound.releaseBias));
            Assert.That(largeSound.stereoSpread, Is.GreaterThan(smallSound.stereoSpread));
            Assert.That(largeSound.modDepth, Is.GreaterThan(smallSound.modDepth));
            Assert.That(smallSound.brightness, Is.GreaterThan(largeSound.brightness));
            Assert.That(smallSound.attackBias, Is.GreaterThan(largeSound.attackBias));
        }

        [Test]
        public void LargerHarmonyPad_ProducesBroaderAndBloomierSoundProfile()
        {
            var smallSound = DeriveSound(PatternType.HarmonyPad, GeneratePadStroke(20, 0.14f, 0.04f));
            var largeSound = DeriveSound(PatternType.HarmonyPad, GeneratePadStroke(20, 0.42f, 0.14f));

            Assert.That(largeSound.body, Is.GreaterThan(smallSound.body));
            Assert.That(largeSound.releaseBias, Is.GreaterThan(smallSound.releaseBias));
            Assert.That(largeSound.stereoSpread, Is.GreaterThan(smallSound.stereoSpread));
            Assert.That(largeSound.reverbBias, Is.GreaterThan(smallSound.reverbBias));
            Assert.That(largeSound.filterMotion, Is.GreaterThan(smallSound.filterMotion));
            Assert.That(smallSound.brightness, Is.GreaterThan(largeSound.brightness));
        }

        [Test]
        public void SessionStore_LoadState_BackfillsLegacySizeMetrics_AndRefreshesDescriptors()
        {
            var state = AppStateFactory.CreateEmpty();
            state.version = 3;

            var pattern = new PatternDefinition
            {
                id = "pattern-legacy",
                type = PatternType.RhythmLoop,
                name = "Legacy Beat",
                bars = 2,
                points = new List<Vector2>
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 0f)
                },
                derivedSequence = new DerivedSequence { kind = "rhythm", totalSteps = AppStateFactory.BarSteps },
                shapeProfile = new ShapeProfile
                {
                    circularity = 0.82f,
                    angularity = 0.22f,
                    symmetry = 0.78f,
                    aspectRatio = 0.94f,
                    wobble = 0.18f,
                    curvatureVariance = 0.16f,
                    horizontalSpan = 0.60f,
                    verticalSpan = 0.40f,
                    pathLength = 0.50f
                },
                soundProfile = new SoundProfile(),
                shapeSummary = "old summary",
                summary = "Closed loop, 2 bars, 8 accents.",
                details = "Circularity drives kick weight."
            };

            state.patterns.Add(pattern);

            var store = new SessionStore();
            store.LoadState(state);

            var loaded = store.GetPattern("pattern-legacy");
            Assert.That(store.State.version, Is.EqualTo(10));
            Assert.That(loaded.shapeProfile.worldWidth, Is.EqualTo(0.57f).Within(0.02f));
            Assert.That(loaded.shapeProfile.worldHeight, Is.EqualTo(0.38f).Within(0.02f));
            Assert.That(loaded.shapeProfile.worldLength, Is.EqualTo(1.375f).Within(0.05f));
            Assert.That(loaded.shapeProfile.worldMaxDimension, Is.GreaterThan(0.5f));
            Assert.That(loaded.soundProfile.body, Is.GreaterThan(0.05f));
            Assert.That(loaded.shapeSummary, Does.Contain(ShapeProfileSizing.DescribeSize(loaded.type, loaded.shapeProfile)));
            Assert.That(loaded.details, Does.Contain("Shape DNA:"));
            Assert.That(loaded.summary.StartsWith("compact ") || loaded.summary.StartsWith("medium ") || loaded.summary.StartsWith("expanded "), Is.True);
            Assert.That(loaded.derivedSequence.kind, Is.EqualTo("rhythm"));
        }

        [Test]
        public void PatternVisualizer_UsesWorldSize_ForRenderColliderAndLabelOffset()
        {
            var material = new Material(Shader.Find("Sprites/Default"));
            var smallResult = BuildVisualizerMetrics(0.12f, material);
            var largeResult = BuildVisualizerMetrics(0.36f, material);

            Assert.That(largeResult.renderedWidth, Is.GreaterThan(smallResult.renderedWidth * 2f));
            Assert.That(largeResult.colliderRadius, Is.GreaterThan(smallResult.colliderRadius));
            Assert.That(largeResult.labelOffsetY, Is.LessThan(smallResult.labelOffsetY));
        }

        private static SoundProfile DeriveSound(PatternType type, List<Vector2> points)
        {
            var metrics = StrokeAnalyzer.Analyze(points);
            var profile = ShapeProfileCalculator.Derive(StrokeAnalyzer.NormalizePoints(points, metrics), metrics, type);
            return SoundProfileMapper.Derive(type, profile);
        }

        private static (float renderedWidth, float colliderRadius, float labelOffsetY) BuildVisualizerMetrics(float worldSize, Material material)
        {
            var go = new GameObject("PatternVisualizerTest");
            try
            {
                var visualizer = go.AddComponent<PatternVisualizer>();
                var pattern = new PatternDefinition
                {
                    id = "pattern",
                    type = PatternType.RhythmLoop,
                    color = Color.white,
                    points = new List<Vector2>
                    {
                        new Vector2(0f, 0f),
                        new Vector2(1f, 0f),
                        new Vector2(1f, 1f),
                        new Vector2(0f, 1f),
                        new Vector2(0f, 0f)
                    },
                    shapeProfile = new ShapeProfile
                    {
                        worldWidth = worldSize,
                        worldHeight = worldSize,
                        worldLength = worldSize * 3.6f,
                        worldAverageSize = worldSize,
                        worldMaxDimension = worldSize
                    }
                };

                var instance = new PatternInstance("pattern", "scene-a", Vector3.zero, 0.3f);
                visualizer.Initialize(pattern, instance, material);

                var line = go.GetComponent<LineRenderer>();
                float minX = float.MaxValue;
                float maxX = float.MinValue;
                for (int i = 0; i < line.positionCount; i++)
                {
                    Vector3 position = line.GetPosition(i);
                    minX = Mathf.Min(minX, position.x);
                    maxX = Mathf.Max(maxX, position.x);
                }

                var collider = go.GetComponent<SphereCollider>();
                var label = go.transform.Find("ParamLabel");
                return (maxX - minX, collider.radius, label.localPosition.y);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static List<Vector2> GenerateCircle(int pointCount, float radius)
        {
            var points = new List<Vector2>(pointCount + 1);
            for (int i = 0; i <= pointCount; i++)
            {
                float angle = (float)i / pointCount * Mathf.PI * 2f;
                points.Add(new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
            }
            return points;
        }

        private static List<Vector2> GenerateWave(int pointCount, float width, float amplitude)
        {
            var points = new List<Vector2>(pointCount);
            for (int i = 0; i < pointCount; i++)
            {
                float t = (float)i / (pointCount - 1);
                points.Add(new Vector2((t - 0.5f) * width, Mathf.Sin(t * Mathf.PI * 3f) * amplitude));
            }
            return points;
        }

        private static List<Vector2> GeneratePadStroke(int pointCount, float width, float height)
        {
            var points = new List<Vector2>(pointCount);
            for (int i = 0; i < pointCount; i++)
            {
                float t = (float)i / (pointCount - 1);
                float x = (t - 0.5f) * width;
                float y = (t - 0.5f) * height + Mathf.Sin(t * Mathf.PI * 2f) * height * 0.3f;
                points.Add(new Vector2(x, y));
            }
            return points;
        }
    }
}
#endif
