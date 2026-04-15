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

            var role = ShapeRoleProvider.Current;
            var key  = MusicalKeys.Get(keyName);
            var harmCtx = HarmonicContextProvider.Current;
            var notes = new List<MelodyNote>();

            if (role.index >= 2)
            {
                // Role 2+: one long sustained note per bar on the chord root — fills without competing
                int rootMidi = harmCtx.HasChord ? harmCtx.rootMidi : key.rootMidi + 12;
                rootMidi = RegisterPolicy.Clamp(rootMidi, PatternType.MelodyLine, "newage");
                int barSteps = AppStateFactory.BarSteps;
                int roleOffset = barSteps * 2 / 3;
                for (int bar = 0; bar < bars; bar++)
                {
                    notes.Add(new MelodyNote
                    {
                        step = (bar * barSteps + roleOffset) % totalSteps,
                        midi = rootMidi,
                        durationSteps = barSteps - 2,
                        velocity = MathUtils.RoundTo(0.20f + sp.verticalSpan * 0.08f, 2),
                        glide = 0f
                    });
                }
            }
            else
            {
                // Role 0: primary pentatonic line (existing behaviour)
                // Role 1: counter — sparser (every 3rd slice), chord tones only, octave higher
                int sliceCount = role.IsPrimary
                    ? ((metrics.length > 0.75f || sp.pathLength > 0.68f) ? 8 : 4)
                    : 3; // counter: 3 notes max

                var samples = StrokeAnalyzer.ResampleStroke(points, sliceCount);
                float pitchCenter = metrics.minY + metrics.height / 2f;
                float verticalScale = 0.55f + sp.verticalSpan * 0.9f;
                int roleOffset = role.index == 1 ? AppStateFactory.BarSteps / 3 : 0;

                for (int i = 0; i < samples.Count; i++)
                {
                    Vector2 prev = samples[Mathf.Max(0, i - 1)];
                    Vector2 curr = samples[i];
                    Vector2 next = samples[Mathf.Min(samples.Count - 1, i + 1)];

                    float centeredY = Mathf.Clamp(
                        (curr.y - pitchCenter) / Mathf.Max(metrics.height, 0.001f) * verticalScale + 0.5f,
                        0f, 0.999f);

                    int midi = PentatonicPitch(centeredY, key);

                    if (!role.IsPrimary)
                    {
                        // Counter voice: transpose up an octave and snap all notes to chord tones
                        midi += 12;
                        if (harmCtx.HasChord) midi = harmCtx.NearestChordTone(midi);
                    }
                    else
                    {
                        int step0 = Mathf.FloorToInt((float)i / sliceCount * totalSteps);
                        if (step0 % 4 == 0 && harmCtx.HasChord)
                            midi = harmCtx.NearestChordTone(midi);
                    }

                    // Clamp to register (G4–E6 for NewAge melody)
                    midi = RegisterPolicy.Clamp(midi, PatternType.MelodyLine, "newage");

                    int step = (Mathf.FloorToInt((float)i / sliceCount * totalSteps) + roleOffset) % totalSteps;
                    int durationSteps = role.IsPrimary
                        ? Mathf.Clamp(Mathf.RoundToInt(6f + (1f - sp.speedVariance) * 4f + sizeFactor * 6f - sound.transientSharpness * 2f), 4, 14)
                        : Mathf.Clamp(Mathf.RoundToInt(totalSteps / sliceCount * 0.8f), 6, totalSteps / 2);

                    float slope = Mathf.Clamp(
                        (next.y - prev.y) / Mathf.Max(metrics.height, 0.001f) * 2f,
                        -0.5f, 0.5f);

                    notes.Add(new MelodyNote
                    {
                        step = step,
                        midi = midi,
                        durationSteps = durationSteps,
                        velocity = MathUtils.RoundTo(
                            Mathf.Clamp(role.IsPrimary
                                ? 0.28f + (1f - sp.angularity) * 0.16f + sp.verticalSpan * 0.12f
                                : 0.20f + (1f - sp.angularity) * 0.10f,   // counter is softer
                                0.18f, 0.55f), 2),
                        glide = MathUtils.RoundTo(slope * (0.2f + sound.filterMotion * 0.3f), 2)
                    });
                }
            }

            string roleLabel = role.index == 0 ? "primary" : role.index == 1 ? "counter" : "fill";
            string flowWord  = sp.angularity < 0.4f ? "flowing" : "stepping";
            return new MelodyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { "pentatonic", roleLabel, flowWord },
                derivedSequence = new DerivedSequence
                {
                    kind = "melody",
                    totalSteps = totalSteps,
                    notes = notes
                },
                summary = $"{sizeWord} {roleLabel} kalimba, {bars} bars, {flowWord} phrasing.",
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
