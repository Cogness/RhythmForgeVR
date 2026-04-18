using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;

namespace RhythmForge.Core.Sequencing
{
    public struct MelodyDerivationResult
    {
        public int bars;
        public string presetId;
        public List<string> tags;
        public DerivedSequence derivedSequence;
        public string summary;
        public string details;
    }

    public static class MelodyDeriver
    {
        // World-space thresholds (pilot: length > 880px → 4 bars, length > 820 → 16 slices)
        private const float FourBarThreshold = 0.80f;
        private const float SixteenSliceThreshold = 0.75f;

        public static MelodyDerivationResult Derive(
            StrokeCurve curve, StrokeMetrics metrics,
            string keyName, string groupId,
            ShapeProfile sp, SoundProfile sound)
        {
            var points = curve.projected;
            float sizeFactor = ShapeProfileSizing.GetSizeFactor(PatternType.MelodyLine, sp);
            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.MelodyLine, sp);
            int bars = metrics.length > FourBarThreshold ? 4 : 2;
            int totalSteps = bars * AppStateFactory.BarSteps;

            var group = InstrumentGroups.Get(groupId);
            string presetId = group.defaultPresetByType.MelodyLine;

            var role    = ShapeRoleProvider.Current;
            var harmCtx = HarmonicContextProvider.Current;
            var notes   = new List<MelodyNote>();

            if (role.index >= 2)
            {
                // Role 2+: one long root-note per bar, register-clamped, offset by 2/3 bar
                var key2 = MusicalKeys.Get(keyName);
                int rootMidi = harmCtx.HasChord ? harmCtx.rootMidi : key2.rootMidi + 12;
                rootMidi = RegisterPolicy.Clamp(rootMidi, PatternType.MelodyLine, "electronic");
                int barSteps2 = AppStateFactory.BarSteps;
                int roleOffset2 = barSteps2 * 2 / 3;
                for (int bar = 0; bar < bars; bar++)
                {
                    notes.Add(new MelodyNote
                    {
                        step = (bar * barSteps2 + roleOffset2) % totalSteps,
                        midi = rootMidi,
                        durationSteps = barSteps2 - 2,
                        velocity = MathUtils.RoundTo(0.24f + sp.verticalSpan * 0.08f, 2),
                        glide = 0f
                    });
                }
            }
            else
            {
                // Role 0: primary (existing behaviour, full slice count)
                // Role 1: counter — half slices, octave up, chord-tones only, offset by 1/3 bar
                int sliceCount = role.IsPrimary
                    ? ((metrics.length > SixteenSliceThreshold || sp.pathLength > 0.68f) ? 16 : 8)
                    : 4;

                var samples = StrokeAnalyzer.ResampleStroke(points, sliceCount);
                float pitchCenter  = metrics.minY + metrics.height / 2f;
                float verticalScale = 0.72f + sp.verticalSpan * 1.2f;
                int barSteps = AppStateFactory.BarSteps;
                int roleOffset = role.index == 1 ? barSteps / 3 : 0;

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

                    int midi = PitchUtils.PitchFromRelative(centeredY, keyName);

                    if (!role.IsPrimary)
                    {
                        midi += 12; // counter: one octave up
                        if (harmCtx.HasChord) midi = harmCtx.NearestChordTone(midi);
                    }
                    else
                    {
                        int step0 = Mathf.FloorToInt((float)i / sliceCount * totalSteps);
                        if (step0 % 4 == 0 && harmCtx.HasChord)
                            midi = harmCtx.NearestChordTone(midi);
                    }

                    midi = RegisterPolicy.Clamp(midi, PatternType.MelodyLine, "electronic");

                    int step = (Mathf.FloorToInt((float)i / sliceCount * totalSteps) + roleOffset) % totalSteps;
                    int durationBase  = speed < 0.025f ? 6 : speed < 0.042f ? 4 : 2;
                    int durationSteps = role.IsPrimary
                        ? Mathf.Clamp(Mathf.RoundToInt(durationBase + (1f - sp.speedVariance) * 1.2f - sound.transientSharpness * 1.1f + sizeFactor * 2f), 2, 7)
                        : Mathf.Clamp(Mathf.RoundToInt(totalSteps / sliceCount * 0.75f), 4, totalSteps / 2);

                    float slope = Mathf.Clamp(
                        (next.y - prev.y) / Mathf.Max(metrics.height, 0.001f) * 4f, -1f, 1f);

                    notes.Add(new MelodyNote
                    {
                        step = step,
                        midi = midi,
                        durationSteps = durationSteps,
                        velocity = MathUtils.RoundTo(
                            Mathf.Clamp(role.IsPrimary
                                ? 0.34f + curvature * 0.86f + sp.curvatureMean * 0.16f
                                : 0.24f + curvature * 0.60f,
                                0.22f, 0.88f), 2),
                        glide = MathUtils.RoundTo(slope * (0.3f + sound.filterMotion * 0.85f), 2)
                    });
                }
            }

            string roleLabel = role.index == 0 ? "primary" : role.index == 1 ? "counter" : "fill";
            string modWord   = sound.modDepth > 0.56f ? "animated" : "steady";
            return new MelodyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { roleLabel, modWord },
                derivedSequence = new DerivedSequence
                {
                    kind = "melody",
                    totalSteps = totalSteps,
                    notes = notes
                },
                summary = $"{sizeWord} {roleLabel} line, {bars} bars, contour {Mathf.Round(sp.verticalSpan * 100f)}%, {modWord} voice.",
                details = "Size pushes sustain, width, and modulation depth, while vertical span drives octave reach and direction bias adds glide pull into notes."
            };
        }
    }
}
