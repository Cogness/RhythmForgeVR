using System.Collections.Generic;
using UnityEngine;
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
            List<Vector2> points, StrokeMetrics metrics,
            string keyName, string groupId,
            ShapeProfile sp, SoundProfile sound)
        {
            int bars = metrics.length > FourBarThreshold ? 4 : 2;
            int totalSteps = bars * AppStateFactory.BarSteps;
            int sliceCount = (metrics.length > SixteenSliceThreshold || sp.pathLength > 0.68f) ? 16 : 8;

            var samples = StrokeAnalyzer.ResampleStroke(points, sliceCount);
            var group = InstrumentGroups.Get(groupId);
            string presetId = group.defaultPresetByType.MelodyLine;

            var notes = new List<MelodyNote>();
            float pitchCenter = metrics.minY + metrics.height / 2f;
            float verticalScale = 0.72f + sp.verticalSpan * 1.2f;

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

                int midi = PitchUtils.PitchFromRelative(centeredY, keyName);

                // World-space speed thresholds (pilot: 24px, 40px)
                int durationBase = speed < 0.025f ? 6 : speed < 0.042f ? 4 : 2;
                int durationSteps = Mathf.Clamp(
                    Mathf.RoundToInt(durationBase + (1f - sp.speedVariance) * 1.5f - sound.transientSharpness * 1.2f),
                    2, 7);

                float slope = Mathf.Clamp(
                    (next.y - prev.y) / Mathf.Max(metrics.height, 0.001f) * 4f,
                    -1f, 1f);

                int step = Mathf.FloorToInt((float)i / sliceCount * totalSteps);

                notes.Add(new MelodyNote
                {
                    step = step,
                    midi = midi,
                    durationSteps = durationSteps,
                    velocity = MathUtils.RoundTo(
                        Mathf.Clamp(0.34f + curvature * 0.86f + sp.curvatureMean * 0.16f, 0.3f, 0.96f), 2),
                    glide = MathUtils.RoundTo(slope * (0.3f + sound.filterMotion * 0.85f), 2)
                });
            }

            string modWord = sound.modDepth > 0.56f ? "animated" : "steady";
            string voiceWord = sound.modDepth > 0.58f ? "modulated" : "contained";
            return new MelodyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { "line", $"{sliceCount} slices", modWord },
                derivedSequence = new DerivedSequence
                {
                    kind = "melody",
                    totalSteps = totalSteps,
                    notes = notes
                },
                summary = $"{sliceCount} slices, {bars} bars, wide contour {Mathf.Round(sp.verticalSpan * 100f)}%, {voiceWord} voice.",
                details = "Vertical span drives octave reach, angularity sharpens tone, and direction bias adds glide pull into notes."
            };
        }
    }
}
