using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;

namespace RhythmForge.Core.Sequencing.NewAge
{
    /// <summary>
    /// New Age rhythm deriver: sparse, meditative patterns using soft mallet, shaker, and singing bowl timbres.
    /// Larger shapes → more sparse (breathing space). Circular shapes → bowl-dominant. Angular → shaker accents.
    /// </summary>
    public sealed class NewAgeRhythmDeriver : IRhythmDeriver
    {
        public RhythmDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            ShapeProfile sp,
            SoundProfile sound,
            GenreProfile genre)
        {
            float sizeFactor = ShapeProfileSizing.GetSizeFactor(PatternType.RhythmLoop, sp);
            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.RhythmLoop, sp);
            int bars = metrics.averageSize > 0.30f ? 4 : 2;
            int totalSteps = bars * AppStateFactory.BarSteps;
            string presetId = genre.GetDefaultPresetId(PatternType.RhythmLoop);

            // Sparse density: 2-6 events per bar (meditative space between events)
            int density = Mathf.Clamp(
                Mathf.RoundToInt(2f + sp.angularity * 2f + (1f - sp.circularity) * 1.5f + sizeFactor * 1.5f),
                2, 6);

            // No swing — flowing, even timing; very slight natural wobble
            float swing = MathUtils.RoundTo(sp.wobble * 0.06f, 2);

            var events = new List<RhythmEvent>();

            for (int bar = 0; bar < bars; bar++)
            {
                int offset = bar * AppStateFactory.BarSteps;

                // Singing bowl (kick lane) — on beats 0 and 8 only; skip beat 8 for very circular (flowing) shapes
                events.Add(new RhythmEvent
                {
                    step = offset + 0,
                    lane = "kick",
                    velocity = MathUtils.RoundTo(0.38f + sound.body * 0.28f, 2),
                    microShift = 0f
                });

                if (sp.circularity < 0.72f)
                {
                    events.Add(new RhythmEvent
                    {
                        step = offset + 8,
                        lane = "kick",
                        velocity = MathUtils.RoundTo(0.26f + sound.body * 0.18f, 2),
                        microShift = MathUtils.RoundTo(sp.wobble * 0.02f, 3)
                    });
                }

                // Shaker (hat lane) — gentle, sparse; more events for angular shapes
                int[] shakerSteps;
                if (sp.angularity > 0.6f)
                    shakerSteps = new[] { 4, 10, 14 };
                else if (sp.symmetry > 0.65f)
                    shakerSteps = new[] { 6 };
                else
                    shakerSteps = new[] { 4, 12 };

                foreach (int step in shakerSteps)
                {
                    events.Add(new RhythmEvent
                    {
                        step = offset + step,
                        lane = "hat",
                        velocity = MathUtils.RoundTo(0.18f + (1f - sp.angularity) * 0.1f, 2),
                        microShift = MathUtils.RoundTo(
                            Mathf.Sin((step + 1f) * 0.73f + sp.wobble * 3f) * 0.02f, 3)
                    });
                }

                // Soft mallet/perc — on downbeats for dense shapes
                if (density >= 4 && (bar == 0 || sp.symmetry < 0.5f))
                {
                    events.Add(new RhythmEvent
                    {
                        step = offset + 4,
                        lane = "perc",
                        velocity = MathUtils.RoundTo(0.22f + sound.body * 0.14f, 2),
                        microShift = 0f
                    });
                }
            }

            string densityWord = density <= 3 ? "sparse" : "flowing";
            return new RhythmDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { "meditation", densityWord, sp.circularity > 0.65f ? "bowl" : "shaker" },
                derivedSequence = new DerivedSequence
                {
                    kind = "rhythm",
                    totalSteps = totalSteps,
                    swing = swing,
                    events = events
                },
                summary = $"{sizeWord} breath pattern, {bars} bars, {density} touches, {densityWord} space.",
                details = "Circularity drives bowl resonance, angularity shapes shaker presence, size determines breath space between beats."
            };
        }
    }
}
