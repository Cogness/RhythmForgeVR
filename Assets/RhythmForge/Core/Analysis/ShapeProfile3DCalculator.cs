using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Analysis
{
    /// <summary>
    /// A single point sample from <c>StrokeCapture</c>: world position + pen pressure
    /// + stylus rotation + time-since-stroke-start. Parallel arrays compacted into one
    /// struct for the draft pipeline to hand off to <see cref="ShapeProfile3DCalculator"/>.
    /// </summary>
    public struct StrokeSample
    {
        public Vector3 worldPos;
        public float pressure;
        public Quaternion stylusRot;
        public double timestamp;
        public bool ornamentFlag;
        public bool accentFlag;
    }

    /// <summary>
    /// Computes <see cref="ShapeProfile3D"/> from the raw 3D stroke stream. Pure
    /// static, deterministic, quantised to 2 decimals so repeated strokes produce
    /// identical profiles.
    ///
    /// Phase A produces the profile but nothing reads it yet.
    /// </summary>
    public static class ShapeProfile3DCalculator
    {
        private const float Epsilon = 1e-6f;
        private const int MaxPassCount = 20;

        /// <summary>
        /// Derives a full <see cref="ShapeProfile3D"/>. Returns an empty profile when
        /// <paramref name="samples"/> is null or contains fewer than 2 points.
        /// </summary>
        public static ShapeProfile3D Derive(
            IReadOnlyList<StrokeSample> samples,
            ShapeProfile planar2D,
            Vector3 referenceUp,
            bool ornamentFlag = false,
            bool accentFlag = false)
        {
            if (samples == null || samples.Count < 2)
                return new ShapeProfile3D();

            int n = samples.Count;

            // --- 1. Pressure stats ---
            float pMean = 0f;
            float pPeak = 0f;
            for (int i = 0; i < n; i++) pMean += samples[i].pressure;
            pMean /= n;
            for (int i = 0; i < n; i++) pPeak = Mathf.Max(pPeak, samples[i].pressure);

            float pVarSum = 0f;
            for (int i = 0; i < n; i++)
            {
                float d = samples[i].pressure - pMean;
                pVarSum += d * d;
            }
            float pStdDev = Mathf.Sqrt(pVarSum / n);
            float thicknessVariance = Mathf.Clamp01(pStdDev / Mathf.Max(pMean, Epsilon));

            // --- 2. Tilt stats ---
            Vector3 refUp = referenceUp.sqrMagnitude > Epsilon ? referenceUp.normalized : Vector3.up;
            float[] tiltAngles = new float[n];
            float tiltSum = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 stylusUp = samples[i].stylusRot * Vector3.up;
                if (stylusUp.sqrMagnitude < Epsilon) stylusUp = Vector3.up;
                float angleDeg = Vector3.Angle(stylusUp, refUp);
                float a = Mathf.Clamp01(angleDeg / 180f);
                tiltAngles[i] = a;
                tiltSum += a;
            }
            float tiltMean = tiltSum / n;
            float tiltVarSum = 0f;
            for (int i = 0; i < n; i++)
            {
                float d = tiltAngles[i] - tiltMean;
                tiltVarSum += d * d;
            }
            float tiltVariance = Mathf.Clamp01(Mathf.Sqrt(tiltVarSum / n));

            // --- 3. PCA on 3D positions ---
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < n; i++) centroid += samples[i].worldPos;
            centroid /= n;

            float cxx = 0f, cxy = 0f, cxz = 0f, cyy = 0f, cyz = 0f, czz = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = samples[i].worldPos - centroid;
                cxx += p.x * p.x;
                cxy += p.x * p.y;
                cxz += p.x * p.z;
                cyy += p.y * p.y;
                cyz += p.y * p.z;
                czz += p.z * p.z;
            }
            float inv = 1f / n;
            cxx *= inv; cxy *= inv; cxz *= inv;
            cyy *= inv; cyz *= inv; czz *= inv;

            Vector3 eigenvalues;
            Vector3 axisMajor, axisMid, axisMinor;
            JacobiEigen(cxx, cxy, cxz, cyy, cyz, czz,
                out eigenvalues, out axisMajor, out axisMid, out axisMinor);

            // Eigenvalues of the covariance matrix are variances along each axis;
            // extent ≈ 2·stddev covers roughly the spread of the point cloud.
            float majorExtent = Mathf.Sqrt(Mathf.Max(0f, eigenvalues.x)) * 2f;
            float midExtent = Mathf.Sqrt(Mathf.Max(0f, eigenvalues.y)) * 2f;
            float minorExtent = Mathf.Sqrt(Mathf.Max(0f, eigenvalues.z)) * 2f;

            float worldMaxDim = Mathf.Max(
                planar2D != null ? planar2D.worldMaxDimension : 0f,
                majorExtent);

            float elongation3D = majorExtent > Epsilon
                ? Mathf.Clamp01((majorExtent - midExtent) / majorExtent)
                : 0f;
            float planarity = midExtent > Epsilon
                ? Mathf.Clamp01(1f - minorExtent / midExtent)
                : 1f;
            float depthSpan = Mathf.Clamp01(minorExtent / Mathf.Max(worldMaxDim, Epsilon));

            // --- 4. Centroid depth along referenceUp ---
            float minOnUp = float.MaxValue;
            float maxOnUp = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                float h = Vector3.Dot(samples[i].worldPos - centroid, refUp);
                if (h < minOnUp) minOnUp = h;
                if (h > maxOnUp) maxOnUp = h;
            }
            float heightRange = maxOnUp - minOnUp;
            float centroidDepth = heightRange > Epsilon
                ? Mathf.Clamp01((-minOnUp) / heightRange)
                : 0.5f;

            // --- 5. Helicity — net rotation around PCA major axis ---
            Vector3 basisA = axisMid;
            if (basisA.sqrMagnitude < Epsilon)
                basisA = Vector3.Cross(axisMajor, Vector3.up);
            if (basisA.sqrMagnitude < Epsilon)
                basisA = Vector3.Cross(axisMajor, Vector3.right);
            basisA.Normalize();
            Vector3 basisB = Vector3.Cross(axisMajor, basisA);
            if (basisB.sqrMagnitude < Epsilon)
                basisB = axisMinor;
            basisB.Normalize();

            float totalAngle = 0f;
            float prevAngle = 0f;
            bool firstSampleSeen = false;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = samples[i].worldPos - centroid;
                Vector3 flat = p - Vector3.Dot(p, axisMajor) * axisMajor;
                if (flat.sqrMagnitude < Epsilon) continue;
                float a = Mathf.Atan2(Vector3.Dot(flat, basisB), Vector3.Dot(flat, basisA));
                if (firstSampleSeen)
                {
                    float delta = a - prevAngle;
                    if (delta > Mathf.PI) delta -= 2f * Mathf.PI;
                    else if (delta < -Mathf.PI) delta += 2f * Mathf.PI;
                    totalAngle += delta;
                }
                prevAngle = a;
                firstSampleSeen = true;
            }
            float helicity = Mathf.Clamp(totalAngle / (2f * Mathf.PI), -1f, 1f);

            // --- 6. Temporal evenness ---
            float tevenness;
            if (n < 3)
            {
                tevenness = 1f;
            }
            else
            {
                double dtMean = 0.0;
                int dtCount = n - 1;
                for (int i = 1; i < n; i++) dtMean += samples[i].timestamp - samples[i - 1].timestamp;
                dtMean /= dtCount;

                double varSum = 0.0;
                for (int i = 1; i < n; i++)
                {
                    double d = (samples[i].timestamp - samples[i - 1].timestamp) - dtMean;
                    varSum += d * d;
                }
                double std = System.Math.Sqrt(varSum / dtCount);
                tevenness = dtMean > Epsilon
                    ? Mathf.Clamp01(1f - (float)(std / dtMean))
                    : 1f;
            }

            // --- 7. Pass count (3D self-intersections, bounded) ---
            float pathLen = planar2D != null && planar2D.worldLength > 0.0001f
                ? planar2D.worldLength
                : Mathf.Max(majorExtent, Epsilon);
            float passThresh = Mathf.Max(0.005f, pathLen / 80f);
            int passes = 0;
            for (int i = 0; i < n - 1 && passes < MaxPassCount; i++)
            {
                int j = i + 3;
                while (j < n - 1 && passes < MaxPassCount)
                {
                    float d = SegmentSegmentDistance(
                        samples[i].worldPos, samples[i + 1].worldPos,
                        samples[j].worldPos, samples[j + 1].worldPos);
                    if (d < passThresh)
                    {
                        passes++;
                        j += 3; // skip a few ahead to avoid double-counting the same crossing
                    }
                    else
                    {
                        j++;
                    }
                }
            }
            float passCount = Mathf.Clamp01((float)passes / (float)MaxPassCount);

            bool derivedOrnament = ornamentFlag;
            bool derivedAccent = accentFlag;
            for (int i = 0; i < n; i++)
            {
                derivedOrnament |= samples[i].ornamentFlag;
                derivedAccent |= samples[i].accentFlag;
            }

            return new ShapeProfile3D
            {
                thicknessMean = Quantize(Mathf.Clamp01(pMean)),
                thicknessVariance = Quantize(thicknessVariance),
                thicknessPeak = Quantize(Mathf.Clamp01(pPeak)),
                tiltMean = Quantize(tiltMean),
                tiltVariance = Quantize(tiltVariance),
                depthSpan = Quantize(depthSpan),
                planarity = Quantize(planarity),
                elongation3D = Quantize(elongation3D),
                centroidDepth = Quantize(centroidDepth),
                helicity = QuantizeSigned(helicity),
                temporalEvenness = Quantize(tevenness),
                passCount = Quantize(passCount),
                ornamentFlag = derivedOrnament,
                accentFlag = derivedAccent
            };
        }

        private static float Quantize(float v)
        {
            return Mathf.Round(Mathf.Clamp01(v) * 100f) / 100f;
        }

        private static float QuantizeSigned(float v)
        {
            return Mathf.Round(Mathf.Clamp(v, -1f, 1f) * 100f) / 100f;
        }

        // --- Jacobi eigendecomposition for symmetric 3x3 ---
        // Returns eigenvalues sorted descending (x = major, y = mid, z = minor)
        // and the three corresponding unit eigenvectors.
        private static void JacobiEigen(
            float cxx, float cxy, float cxz, float cyy, float cyz, float czz,
            out Vector3 eigenvalues,
            out Vector3 axisMajor, out Vector3 axisMid, out Vector3 axisMinor)
        {
            float[,] A = new float[3, 3];
            A[0, 0] = cxx; A[0, 1] = cxy; A[0, 2] = cxz;
            A[1, 0] = cxy; A[1, 1] = cyy; A[1, 2] = cyz;
            A[2, 0] = cxz; A[2, 1] = cyz; A[2, 2] = czz;

            float[,] V = new float[3, 3];
            V[0, 0] = 1f; V[0, 1] = 0f; V[0, 2] = 0f;
            V[1, 0] = 0f; V[1, 1] = 1f; V[1, 2] = 0f;
            V[2, 0] = 0f; V[2, 1] = 0f; V[2, 2] = 1f;

            const int maxSweeps = 15;
            for (int sweep = 0; sweep < maxSweeps; sweep++)
            {
                float off = Mathf.Abs(A[0, 1]) + Mathf.Abs(A[0, 2]) + Mathf.Abs(A[1, 2]);
                if (off < 1e-10f) break;

                for (int p = 0; p < 2; p++)
                {
                    for (int q = p + 1; q < 3; q++)
                    {
                        float apq = A[p, q];
                        if (Mathf.Abs(apq) < 1e-10f) continue;

                        float app = A[p, p];
                        float aqq = A[q, q];
                        float theta = (aqq - app) / (2f * apq);
                        float t = theta >= 0f
                            ? 1f / (theta + Mathf.Sqrt(1f + theta * theta))
                            : 1f / (theta - Mathf.Sqrt(1f + theta * theta));
                        float c = 1f / Mathf.Sqrt(1f + t * t);
                        float s = t * c;

                        A[p, p] = app - t * apq;
                        A[q, q] = aqq + t * apq;
                        A[p, q] = 0f;
                        A[q, p] = 0f;

                        for (int r = 0; r < 3; r++)
                        {
                            if (r != p && r != q)
                            {
                                float arp = A[r, p];
                                float arq = A[r, q];
                                A[r, p] = c * arp - s * arq;
                                A[p, r] = A[r, p];
                                A[r, q] = s * arp + c * arq;
                                A[q, r] = A[r, q];
                            }
                            float vrp = V[r, p];
                            float vrq = V[r, q];
                            V[r, p] = c * vrp - s * vrq;
                            V[r, q] = s * vrp + c * vrq;
                        }
                    }
                }
            }

            float e0 = A[0, 0], e1 = A[1, 1], e2 = A[2, 2];
            int i0 = 0, i1 = 1, i2 = 2;

            // Sort descending by eigenvalue, carrying original column indices.
            if (e1 > e0)
            {
                float tf = e0; e0 = e1; e1 = tf;
                int ti = i0; i0 = i1; i1 = ti;
            }
            if (e2 > e0)
            {
                float tf = e0; e0 = e2; e2 = tf;
                int ti = i0; i0 = i2; i2 = ti;
            }
            if (e2 > e1)
            {
                float tf = e1; e1 = e2; e2 = tf;
                int ti = i1; i1 = i2; i2 = ti;
            }

            eigenvalues = new Vector3(e0, e1, e2);
            axisMajor = SafeNormalize(new Vector3(V[0, i0], V[1, i0], V[2, i0]), Vector3.right);
            axisMid   = SafeNormalize(new Vector3(V[0, i1], V[1, i1], V[2, i1]), Vector3.up);
            axisMinor = SafeNormalize(new Vector3(V[0, i2], V[1, i2], V[2, i2]), Vector3.forward);
        }

        private static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
        {
            return v.sqrMagnitude > Epsilon ? v.normalized : fallback;
        }

        // Shortest distance between two 3D line segments. Standard geometric algorithm
        // (Real-Time Collision Detection by Ericson, §5.1.9).
        private static float SegmentSegmentDistance(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
        {
            Vector3 d1 = p2 - p1;
            Vector3 d2 = p4 - p3;
            Vector3 r = p1 - p3;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);

            float s, t;
            if (a <= Epsilon && e <= Epsilon)
                return (p1 - p3).magnitude;

            if (a <= Epsilon)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= Epsilon)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom != 0f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }

            Vector3 closest1 = p1 + d1 * s;
            Vector3 closest2 = p3 + d2 * t;
            return (closest1 - closest2).magnitude;
        }
    }
}
