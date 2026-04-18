using System;

namespace RhythmForge.Core.Data
{
    /// <summary>
    /// Additive 3D-only shape features. Lives alongside <see cref="ShapeProfile"/> on
    /// <see cref="PatternDefinition"/>; does NOT duplicate the 2D fields. Null on legacy
    /// (pre-unified-shape) patterns and on synthetic/demo patterns. Computed by
    /// <c>ShapeProfile3DCalculator</c> from the raw 3D stroke + pressure + stylus rotation
    /// + per-sample timestamp stream captured in <c>StrokeCapture</c>.
    ///
    /// Phase A: this field is populated but NOT read by any deriver. Phase B onwards
    /// consumes it to drive the unified shape deriver.
    /// </summary>
    [Serializable]
    public class ShapeProfile3D
    {
        // --- Pressure (thickness) ---
        public float thicknessMean;       // 0..1 average pen pressure along the stroke
        public float thicknessVariance;   // 0..1 normalised std-dev of pressure
        public float thicknessPeak;       // 0..1 peak pen pressure along the stroke

        // --- Stylus tilt ---
        public float tiltMean;            // 0..1 average angle between stylus-up and referenceUp
        public float tiltVariance;        // 0..1 normalised std-dev of that angle

        // --- 3D geometry (from PCA of world positions) ---
        public float depthSpan;           // 0..1 minor-axis extent / worldMaxDimension
        public float planarity;           // 0..1 (1 = flat, 0 = volumetric)
        public float elongation3D;        // 0..1 (major - mid) / major
        public float centroidDepth;       // 0..1 position of centroid along referenceUp

        // --- Temporal + topological ---
        public float helicity;            // -1..1 net signed rotation around PCA major axis / 2π
        public float temporalEvenness;    // 0..1 (1 = uniform inter-sample dt, 0 = very uneven)
        public float passCount;           // 0..1 normalised count of 3D self-intersections
        public bool ornamentFlag;
        public bool accentFlag;

        public ShapeProfile3D Clone()
        {
            return new ShapeProfile3D
            {
                thicknessMean = thicknessMean,
                thicknessVariance = thicknessVariance,
                thicknessPeak = thicknessPeak,
                tiltMean = tiltMean,
                tiltVariance = tiltVariance,
                depthSpan = depthSpan,
                planarity = planarity,
                elongation3D = elongation3D,
                centroidDepth = centroidDepth,
                helicity = helicity,
                temporalEvenness = temporalEvenness,
                passCount = passCount,
                ornamentFlag = ornamentFlag,
                accentFlag = accentFlag
            };
        }
    }
}
