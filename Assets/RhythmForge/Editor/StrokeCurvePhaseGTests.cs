#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Editor
{
    /// <summary>
    /// Phase G verification: <see cref="StrokeCurve"/> carrier preserves
    /// the pre-refactor 2D projection math, <c>DraftBuilder</c> persists
    /// center-relative 3D <see cref="PatternDefinition.worldPoints"/> on fresh
    /// drafts, and legacy 2D-only call sites still work via
    /// <see cref="StrokeCurve.FromLegacy2D"/>.
    /// </summary>
    public class StrokeCurvePhaseGTests
    {
        [Test]
        public void FromSamples_ProjectionMatchesPreRefactorDotProduct()
        {
            // Three samples off-plane in world space. The projection onto
            // (right, up) must reproduce the exact dot-product output of the
            // removed StrokeCapture.ProjectTo2D helper.
            var center = new Vector3(1f, 2f, 3f);
            var right = new Vector3(1f, 0f, 0f);
            var up = new Vector3(0f, 1f, 0f);
            var samples = new List<StrokeSample>
            {
                Sample(new Vector3(1.5f, 2.25f, 3.9f)),
                Sample(new Vector3(0.5f, 2.75f, 2.1f)),
                Sample(new Vector3(2.0f, 1.5f, 3.5f)),
            };

            var curve = StrokeCurve.FromSamples(samples, center, right, up);

            Assert.That(curve.projected.Count, Is.EqualTo(3));
            // (worldPos - center) · right, (worldPos - center) · up
            AssertV2(curve.projected[0], new Vector2(0.5f, 0.25f));
            AssertV2(curve.projected[1], new Vector2(-0.5f, 0.75f));
            AssertV2(curve.projected[2], new Vector2(1.0f, -0.5f));
        }

        [Test]
        public void FromLegacy2D_RetainsPointsAndHasEmptySamples()
        {
            var points = new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 1f) };
            var curve = StrokeCurve.FromLegacy2D(points);

            Assert.That(curve.projected, Is.SameAs(points));
            Assert.That(curve.samples, Is.Not.Null);
            Assert.That(curve.samples.Count, Is.EqualTo(0));
        }

        [Test]
        public void DraftBuilder_WithRichSamples_EmitsCenterRelativeWorldPoints()
        {
            var store = NewStore();
            var center = new Vector3(5f, 1f, 2f);
            var right = Vector3.right;
            var up = Vector3.up;

            // Simple 8-point circle in the x/y plane, offset by `center`
            var samples = new List<StrokeSample>(8);
            for (int i = 0; i < 8; i++)
            {
                float t = i / 8f * Mathf.PI * 2f;
                var world = center + new Vector3(Mathf.Cos(t) * 0.14f, Mathf.Sin(t) * 0.14f, 0.02f);
                samples.Add(Sample(world));
            }
            // Same 2D projection that StrokeCurve.FromSamples would compute —
            // DraftBuilder will recompute it, so any legacy rawPoints value
            // would be overwritten.
            List<Vector2> projected = null; // not needed, richSamples path wins

            var draft = DraftBuilder.BuildFromStroke(
                PatternType.RhythmLoop, projected, center, Quaternion.identity,
                store.State, store,
                richSamples: samples, referenceUp: Vector3.up,
                bondStrength: null, freeMode: false,
                strokeRight: right, strokeUp: up);

            Assert.That(draft.success, Is.True);
            Assert.That(draft.worldPoints, Is.Not.Null);
            Assert.That(draft.worldPoints.Count, Is.EqualTo(samples.Count));

            // Each stored point is center-relative — verify against the raw
            // sample positions.
            for (int i = 0; i < samples.Count; i++)
            {
                var expected = samples[i].worldPos - center;
                var actual = draft.worldPoints[i];
                Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-5f),
                    $"worldPoints[{i}] must equal worldPos - center");
            }
        }

        [Test]
        public void DraftBuilder_LegacyOverload_LeavesWorldPointsNull()
        {
            var store = NewStore();
            var points = GenerateCircle(24, 0.14f);

            // 6-arg overload (no richSamples) — this is the path AlgorithmTest
            // and any pre-Phase-A call site uses.
            var draft = DraftBuilder.BuildFromStroke(
                PatternType.RhythmLoop, points, Vector3.zero, Quaternion.identity,
                store.State, store);

            Assert.That(draft.success, Is.True);
            Assert.That(draft.worldPoints, Is.Null,
                "No rich samples supplied => no worldPoints persisted (visualizer falls back to 2D).");
        }

        // ────────────────────────── helpers ──────────────────────────

        private static StrokeSample Sample(Vector3 worldPos)
        {
            return new StrokeSample
            {
                worldPos = worldPos,
                pressure = 1f,
                stylusRot = Quaternion.identity,
                timestamp = 0.0
            };
        }

        private static void AssertV2(Vector2 actual, Vector2 expected)
        {
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(1e-5f),
                $"expected {expected} got {actual}");
        }

        private static List<Vector2> GenerateCircle(int count, float radius)
        {
            var list = new List<Vector2>(count);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count * Mathf.PI * 2f;
                list.Add(new Vector2(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius));
            }
            return list;
        }

        private static SessionStore NewStore()
        {
            var store = new SessionStore();
            store.LoadState(AppStateFactory.CreateEmpty());
            return store;
        }
    }
}
#endif
