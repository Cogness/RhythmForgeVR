using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Sequencing;

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
            StrokeCurve curve,
            StrokeMetrics metrics,
            string keyName,
            ShapeProfile sp,
            SoundProfile sound,
            GenreProfile genre)
        {
            var points = curve.projected;
            float sizeFactor = ShapeProfileSizing.GetSizeFactor(PatternType.MelodyLine, sp);
            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.MelodyLine, sp);
            int bars = metrics.length > 0.80f ? 4 : 2;
            int totalSteps = bars * AppStateFactory.BarSteps;
            int sliceCount = (metrics.length > 0.75f || sp.pathLength > 0.68f) ? 16 : 8;
            string presetId = genre.GetDefaultPresetId(PatternType.MelodyLine);

            var role    = ShapeRoleProvider.Current;
            var harmCtx = HarmonicContextProvider.Current;
            var key     = MusicalKeys.Get(keyName);
            var notes   = new List<MelodyNote>();

            if (role.index >= 2)
            {
                // Role 2+: one long root-note per bar, register-clamped, offset by 2/3 bar
                int rootMidi = harmCtx.HasChord ? harmCtx.rootMidi : key.rootMidi + 12;
                rootMidi = RegisterPolicy.Clamp(rootMidi, PatternType.MelodyLine, "jazz");
                int barStepsF = AppStateFactory.BarSteps;
                int roleOffsetF = barStepsF * 2 / 3;
                for (int bar = 0; bar < bars; bar++)
                {
                    notes.Add(new MelodyNote
                    {
                        step = (bar * barStepsF + roleOffsetF) % totalSteps,
                        midi = rootMidi,
                        durationSteps = barStepsF - 2,
                        velocity = MathUtils.RoundTo(0.24f + sp.verticalSpan * 0.08f, 2),
                        glide = 0f
                    });
                }
            }
            else
            {
                // Role 0: primary blues/bop line (existing)
                // Role 1: counter — half slices, octave up, chord-tones only, offset by 1/4 bar (jazz feel)
                int activeSliceCount = role.IsPrimary ? sliceCount : 4;
                var samples = StrokeAnalyzer.ResampleStroke(points, activeSliceCount);
                float pitchCenter  = metrics.minY + metrics.height / 2f;
                float verticalScale = 0.72f + sp.verticalSpan * 1.2f;
                int barSteps = AppStateFactory.BarSteps;
                int roleOffset = role.index == 1 ? barSteps / 4 : 0;

                bool useBluesy = sp.tiltSigned < -0.1f || sp.angularity < 0.35f;
                int[] intervals = useBluesy ? BluesIntervals : JazzMajorIntervals;

                for (int i = 0; i < samples.Count; i++)
                {
                    Vector2 prev = samples[Mathf.Max(0, i - 1)];
                    Vector2 curr = samples[i];
                    Vector2 next = samples[Mathf.Min(samples.Count - 1, i + 1)];

                    float speed     = Vector2.Distance(prev, next);
                    float curvature = Mathf.Abs(next.y - curr.y) + Mathf.Abs(curr.y - prev.y);
                    float centeredY = Mathf.Clamp(
                        (curr.y - pitchCenter) / Mathf.Max(metrics.height, 0.001f) * verticalScale + 0.5f,
                        0f, 0.999f);

                    int midi = JazzPitch(centeredY, key, intervals);

                    if (!role.IsPrimary)
                    {
                        midi += 12; // counter: one octave up
                        if (harmCtx.HasChord) midi = harmCtx.NearestChordTone(midi);
                    }
                    else
                    {
                        int step0 = Mathf.FloorToInt((float)i / activeSliceCount * totalSteps);
                        if (step0 % 4 == 0 && harmCtx.HasChord)
                            midi = harmCtx.NearestChordTone(midi);
                    }

                    midi = RegisterPolicy.Clamp(midi, PatternType.MelodyLine, "jazz");

                    int step = (Mathf.FloorToInt((float)i / activeSliceCount * totalSteps) + roleOffset) % totalSteps;
                    int durationBase  = speed < 0.025f ? 4 : speed < 0.042f ? 2 : 1;
                    int durationSteps = role.IsPrimary
                        ? Mathf.Clamp(Mathf.RoundToInt(durationBase + (1f - sp.speedVariance) * 1.5f + sizeFactor * 2f - sound.transientSharpness * 1.2f), 1, 6)
                        : Mathf.Clamp(Mathf.RoundToInt(totalSteps / activeSliceCount * 0.75f), 4, totalSteps / 2);

                    float slope = Mathf.Clamp((next.y - prev.y) / Mathf.Max(metrics.height, 0.001f) * 4f, -1f, 1f);

                    notes.Add(new MelodyNote
                    {
                        step = step,
                        midi = midi,
                        durationSteps = durationSteps,
                        velocity = MathUtils.RoundTo(
                            Mathf.Clamp(role.IsPrimary
                                ? 0.38f + curvature * 0.78f + sp.curvatureMean * 0.18f
                                : 0.26f + curvature * 0.50f,
                                0.22f, 0.88f), 2),
                        glide = MathUtils.RoundTo(slope * (0.18f + sound.filterMotion * 0.6f), 2)
                    });
                }

                bool useBluesy2 = sp.tiltSigned < -0.1f || sp.angularity < 0.35f;
                string styleWord2 = useBluesy2 ? "blues" : "bop";
                string articWord2 = sound.transientSharpness > 0.5f ? "crisp" : "smooth";
                string roleLabel2 = role.IsPrimary ? styleWord2 : "counter";
                return new MelodyDerivationResult
                {
                    bars = bars,
                    presetId = presetId,
                    tags = new List<string> { roleLabel2, $"{activeSliceCount} notes", articWord2 },
                    derivedSequence = new DerivedSequence { kind = "melody", totalSteps = totalSteps, notes = notes },
                    summary = $"{sizeWord} {roleLabel2} line, {activeSliceCount} notes, {bars} bars, {articWord2} articulation.",
                    details = "Blues scale for dark tilt, jazz major for bright. Curvature adds ornamentation, speed variance controls staccato-to-legato range."
                };
            }

            return new MelodyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { "fill", "sustained" },
                derivedSequence = new DerivedSequence { kind = "melody", totalSteps = totalSteps, notes = notes },
                summary = $"{sizeWord} fill line, {bars} bars, sustained root.",
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
