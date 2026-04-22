using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Core.Sequencing.Jazz
{
    /// <summary>
    /// Jazz rhythm deriver: ride-dominant with swing feel, brush snare, and soft kick.
    /// Circularity → smoother ride. Angularity → cross-stick and accents. Size → fills and complexity.
    /// </summary>
    public sealed class JazzRhythmDeriver : IRhythmDeriver
    {
        // Jazz swing: triplet feel — every other 8th note pushed ~33% late
        private const float SwingBase = 0.28f;

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

            // Jazz swing: base swing + shape instability contribution
            float swing = MathUtils.RoundTo(
                Mathf.Clamp(SwingBase + sound.grooveInstability * 0.12f + sp.wobble * 0.06f, 0.22f, 0.38f), 2);

            int barSteps = AppStateFactory.BarSteps;
            var events = new List<RhythmEvent>();

            for (int bar = 0; bar < bars; bar++)
            {
                int offset = bar * barSteps;
                int[] rideSteps = { 0, 4, 8, 12 };
                foreach (int step in rideSteps)
                {
                    events.Add(new RhythmEvent
                    {
                        step = offset + step,
                        lane = "hat",
                        velocity = MathUtils.RoundTo(0.42f + sound.brightness * 0.14f, 2),
                        microShift = step % 8 == 4 ? MathUtils.RoundTo(swing * 0.04f, 3) : 0f
                    });
                }

                if (sp.circularity > 0.55f)
                {
                    int[] offSteps = { 2, 6, 10, 14 };
                    foreach (int step in offSteps)
                    {
                        events.Add(new RhythmEvent
                        {
                            step = offset + step,
                            lane = "hat",
                            velocity = MathUtils.RoundTo(0.22f + (1f - sp.angularity) * 0.1f, 2),
                            microShift = MathUtils.RoundTo(swing * 0.06f, 3)
                        });
                    }
                }

                events.Add(new RhythmEvent { step = offset + 0, lane = "kick", velocity = MathUtils.RoundTo(0.34f + sound.body * 0.18f, 2), microShift = 0f });
                if (sp.angularity > 0.45f)
                    events.Add(new RhythmEvent { step = offset + 8, lane = "kick", velocity = MathUtils.RoundTo(0.22f + sound.body * 0.12f, 2), microShift = 0f });

                events.Add(new RhythmEvent { step = offset + 8, lane = "snare", velocity = MathUtils.RoundTo(0.44f + sound.transientSharpness * 0.2f, 2), microShift = MathUtils.RoundTo(swing * 0.02f, 3) });

                if (sizeFactor > 0.52f || sp.wobble > 0.4f)
                {
                    foreach (int step in new[] { 3, 11 })
                        events.Add(new RhythmEvent { step = offset + step, lane = "snare", velocity = MathUtils.RoundTo(0.16f + sound.drive * 0.1f, 2), microShift = MathUtils.RoundTo(swing * 0.08f, 3) });
                }

                if (sizeFactor > 0.65f && bar == bars - 1)
                    events.Add(new RhythmEvent { step = offset + 14, lane = "perc", velocity = MathUtils.RoundTo(0.28f + sound.drive * 0.14f, 2), microShift = 0f });
            }

            string swingWord  = swing > 0.32f ? "heavy swing" : "light swing";
            return new RhythmDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { "ride pattern", swingWord, sp.angularity > 0.5f ? "cross-stick" : "smooth" },
                derivedSequence = new DerivedSequence
                {
                    kind = "rhythm",
                    totalSteps = totalSteps,
                    swing = swing,
                    events = events
                },
                summary = $"{sizeWord} jazz ride pattern, {bars} bars, {swingWord} ({Mathf.Round(swing * 100f)}%).",
                details = "Ride cymbal drives the pulse with jazz swing. Circularity adds off-beat smoothness, angularity brings cross-stick accents and ghost notes."
            };
        }
    }
}
