using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Analysis;

namespace RhythmForge.Core.Sequencing
{
    /// <summary>
    /// Phase G carrier: the single input every sub-deriver receives.
    /// Bundles the raw 3D stroke stream (<see cref="samples"/>) with a
    /// pre-computed 2D projection on the best-fit stroke plane
    /// (<see cref="projected"/>) plus the plane basis, so that:
    /// <list type="bullet">
    /// <item>Rhythm / Harmony derivers ignore everything but
    /// <see cref="StrokeMetrics"/> (no change in behaviour).</item>
    /// <item>Melody derivers can call <c>StrokeAnalyzer.ResampleStroke(curve.projected,...)</c>
    /// and stay bit-identical to the pre-refactor 2D path.</item>
    /// <item>Future derivers can opt into the real 3D stream through
    /// <see cref="samples"/> without another signature break.</item>
    /// </list>
    /// <para>
    /// Legacy rederivation paths that only have a saved <c>List&lt;Vector2&gt;</c>
    /// use <see cref="FromLegacy2D"/>; fresh drafts and v8+ saves use
    /// <see cref="FromSamples"/>.
    /// </para>
    /// </summary>
    public struct StrokeCurve
    {
        /// <summary>Raw 3D capture samples. Empty when constructed from a legacy 2D list.</summary>
        public IReadOnlyList<StrokeSample> samples;

        /// <summary>Arc-length-preserving 2D projection of the stroke onto its best-fit plane.</summary>
        public IReadOnlyList<Vector2> projected;

        public Vector3 planeCenter;
        public Vector3 planeRight;
        public Vector3 planeUp;

        private static readonly IReadOnlyList<StrokeSample> EmptySamples = new StrokeSample[0];

        /// <summary>
        /// Build a curve from the full 3D stroke stream. Computes the
        /// projected 2D list using the same dot-product onto the stroke
        /// plane basis that <c>StrokeCapture.ProjectTo2D</c> used before
        /// Phase G.
        /// </summary>
        public static StrokeCurve FromSamples(
            IReadOnlyList<StrokeSample> samples,
            Vector3 center, Vector3 right, Vector3 up)
        {
            int n = samples != null ? samples.Count : 0;
            var projected = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 relative = samples[i].worldPos - center;
                projected[i] = new Vector2(
                    Vector3.Dot(relative, right),
                    Vector3.Dot(relative, up));
            }
            return new StrokeCurve
            {
                samples = samples ?? EmptySamples,
                projected = projected,
                planeCenter = center,
                planeRight = right,
                planeUp = up
            };
        }

        /// <summary>
        /// Legacy adapter: wrap an already-projected 2D point list (e.g. a v7
        /// save) when no 3D samples are available. <see cref="samples"/> is
        /// empty; the plane basis is the world axes. Used by
        /// <c>SessionStore.RederivePatternsBackground</c> on pre-Phase-G saves
        /// — produces bit-identical audio to pre-refactor rederivation.
        /// </summary>
        public static StrokeCurve FromLegacy2D(IReadOnlyList<Vector2> projected)
        {
            return new StrokeCurve
            {
                samples = EmptySamples,
                projected = projected,
                planeCenter = Vector3.zero,
                planeRight = Vector3.right,
                planeUp = Vector3.up
            };
        }
    }
}
