using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;

namespace RhythmForge.Core.Sequencing.Jazz
{
    /// <summary>
    /// Jazz melody deriver: chromatic passing tones, blue notes (b3, b5, b7), swing articulation.
    /// Curvature → ornaments. Direction bias → ascending/descending phrase arc. Speed variance → staccato feel.
    /// </summary>
    public sealed class JazzMelodyDeriver : IMelodyDeriver
    {
        // Jazz blues scale intervals (relative to root): includes b3, b5, b7
        private static readonly int[] BluesIntervals = { 0, 3, 5, 6, 7, 10 };
        // Jazz major scale (Ionian) for bright passages
        private static readonly int[] JazzMajorIntervals = { 0, 2, 4, 5, 7, 9, 11 };

        public MelodyDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            ShapeProfile sp,
            SoundProfile sound,
            GenreProfile genre)
        {
            float sizeFactor = ShapeProfileSizing.GetSizeFactor(PatternType.MelodyLine, sp);
            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.MelodyLine, sp);
            int bars = metrics.length > 0.80f ? 4 : 2;
            int totalSteps = bars * AppStateFactory.BarSteps;
            int sliceCount = (metrics.length > 0.75f || sp.pathLength > 0.68f) ? 16 : 8;
            string presetId = genre.GetDefaultPresetId(PatternType.MelodyLine);

            var samples = StrokeAnalyzer.ResampleStroke(points, sliceCount);
            var notes = new List<MelodyNote>();

            float pitchCenter = metrics.minY + metrics.height / 2f;
            float verticalScale = 0.72f + sp.verticalSpan * 1.2f;

            var key = MusicalKeys.Get(keyName);

            // Jazz uses blues scale for dark shapes, jazz major for bright
            bool useBluesy = sp.tiltSigned < -0.1f || sp.angularity < 0.35f;
            int[] intervals = useBluesy ? BluesIntervals : JazzMajorIntervals;

            for (int i = 0; i < samples.Count; i++)
            {
                Vector2 prev = samples[Mathf.Max(0, i - 1)];
                Vector2 curr = samples[i];
                Vector2 next = samples[Mathf.Min(samples.Count - 1, i + 1)];

                float speed = Vector2.Distance(prev, next);
                float curvature = Mathf.Abs(next.y - curr.y) + Mathf.Abs(curr.y - prev.y);
                float centeredY = Mathf.Clamp(
                    (curr.y - pitchCenter) / Mathf.Max(metrics.height, 0.001f) * verticalScale + 0.5f,
                    0f, 0.999f);

                int midi = JazzPitch(centeredY, key, intervals);

                // Jazz articulation: staccato for fast notes, held for slow
                int durationBase = speed < 0.025f ? 4 : speed < 0.042f ? 2 : 1;
                int durationSteps = Mathf.Clamp(
                    Mathf.RoundToInt(durationBase + (1f - sp.speedVariance) * 1.5f + sizeFactor * 2f - sound.transientSharpness * 1.2f),
                    1, 6);

                float slope = Mathf.Clamp(
                    (next.y - prev.y) / Mathf.Max(metrics.height, 0.001f) * 4f,
                    -1f, 1f);

                int step = Mathf.FloorToInt((float)i / sliceCount * totalSteps);

                // Dynamic velocity for jazz expressiveness
                notes.Add(new MelodyNote
                {
                    step = step,
                    midi = midi,
                    durationSteps = durationSteps,
                    velocity = MathUtils.RoundTo(
                        Mathf.Clamp(0.38f + curvature * 0.78f + sp.curvatureMean * 0.18f, 0.28f, 0.94f), 2),
                    glide = MathUtils.RoundTo(slope * (0.18f + sound.filterMotion * 0.6f), 2)
                });
            }

            string styleWord = useBluesy ? "blues" : "bop";
            string articWord = sound.transientSharpness > 0.5f ? "crisp" : "smooth";
            return new MelodyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { styleWord, $"{sliceCount} notes", articWord },
                derivedSequence = new DerivedSequence
                {
                    kind = "melody",
                    totalSteps = totalSteps,
                    notes = notes
                },
                summary = $"{sizeWord} {styleWord} line, {sliceCount} notes, {bars} bars, {articWord} articulation.",
                details = "Blues scale for dark tilt, jazz major for bright. Curvature adds ornamentation, speed variance controls staccato-to-legato range."
            };
        }

        private static int JazzPitch(float relative, MusicalKey key, int[] intervals)
        {
            float bounded = Mathf.Clamp(relative, 0f, 0.999f);
            int stepsAcross = intervals.Length * 2;
            int degreeIndex = Mathf.FloorToInt((1f - bounded) * stepsAcross);
            int octave = degreeIndex / intervals.Length;
            int degree = degreeIndex % intervals.Length;
            return key.rootMidi + intervals[degree] + octave * 12;
        }
    }
}
