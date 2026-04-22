using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Core.Sequencing.NewAge
{
    /// <summary>
    /// New Age melody deriver: pentatonic-only, long sustains, minimal velocity variation, gentle glide.
    /// Large shapes → fewer, longer notes. Smooth strokes → legato phrasing. Vertical span → octave reach.
    /// </summary>
    public sealed class NewAgeMelodyDeriver : IMelodyDeriver
    {
        // Pentatonic intervals (major pentatonic: 0, 2, 4, 7, 9)
        private static readonly int[] PentatonicIntervals = { 0, 2, 4, 7, 9 };

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
            string presetId = genre.GetDefaultPresetId(PatternType.MelodyLine);

            var key  = MusicalKeys.Get(keyName);
            var harmCtx = HarmonicContextProvider.Current;
            var notes = new List<MelodyNote>();
            int sliceCount = (metrics.length > 0.75f || sp.pathLength > 0.68f) ? 8 : 4;
            var samples = StrokeAnalyzer.ResampleStroke(points, sliceCount);
            float pitchCenter = metrics.minY + metrics.height / 2f;
            float verticalScale = 0.55f + sp.verticalSpan * 0.9f;

            for (int i = 0; i < samples.Count; i++)
            {
                Vector2 prev = samples[Mathf.Max(0, i - 1)];
                Vector2 curr = samples[i];
                Vector2 next = samples[Mathf.Min(samples.Count - 1, i + 1)];

                float centeredY = Mathf.Clamp(
                    (curr.y - pitchCenter) / Mathf.Max(metrics.height, 0.001f) * verticalScale + 0.5f,
                    0f, 0.999f);

                int midi = PentatonicPitch(centeredY, key);
                int step = Mathf.FloorToInt((float)i / sliceCount * totalSteps);
                if (step % 4 == 0 && harmCtx.HasChord)
                    midi = harmCtx.NearestChordTone(midi);

                midi = RegisterPolicy.Clamp(midi, PatternType.MelodyLine, "newage");

                int durationSteps = Mathf.Clamp(Mathf.RoundToInt(6f + (1f - sp.speedVariance) * 4f + sizeFactor * 6f - sound.transientSharpness * 2f), 4, 14);
                float slope = Mathf.Clamp(
                    (next.y - prev.y) / Mathf.Max(metrics.height, 0.001f) * 2f,
                    -0.5f, 0.5f);

                notes.Add(new MelodyNote
                {
                    step = step,
                    midi = midi,
                    durationSteps = durationSteps,
                    velocity = MathUtils.RoundTo(Mathf.Clamp(0.28f + (1f - sp.angularity) * 0.16f + sp.verticalSpan * 0.12f, 0.18f, 0.55f), 2),
                    glide = MathUtils.RoundTo(slope * (0.2f + sound.filterMotion * 0.3f), 2)
                });
            }

            string flowWord  = sp.angularity < 0.4f ? "flowing" : "stepping";
            return new MelodyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { "pentatonic", "lead", flowWord },
                derivedSequence = new DerivedSequence
                {
                    kind = "melody",
                    totalSteps = totalSteps,
                    notes = notes
                },
                summary = $"{sizeWord} lead kalimba, {bars} bars, {flowWord} phrasing.",
                details = "Pentatonic scale ensures harmonic safety. Smoothness drives legato feel, vertical span controls octave range, size breathes note length."
            };
        }

        private static int PentatonicPitch(float relative, MusicalKey key)
        {
            float bounded = Mathf.Clamp(relative, 0f, 0.999f);
            int stepsAcross = PentatonicIntervals.Length * 2;
            int degreeIndex = Mathf.FloorToInt((1f - bounded) * stepsAcross);
            int octave = degreeIndex / PentatonicIntervals.Length;
            int degree = degreeIndex % PentatonicIntervals.Length;
            return key.rootMidi + PentatonicIntervals[degree] + octave * 12;
        }
    }
}
