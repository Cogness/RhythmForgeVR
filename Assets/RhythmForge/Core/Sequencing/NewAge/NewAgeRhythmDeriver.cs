using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Core.Sequencing.NewAge
{
    /// <summary>
    /// New Age rhythm deriver (guided): 8-bar meditative loop built on the
    /// backbeat floor (soft kick on beat 1, shaker on beats 2+4) with optional
    /// bowl resonance on beat 3 and mallet swells on bars 4 and 8.
    /// Shape traits layer additions on top; the floor is never removed.
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
            sp = sp ?? new ShapeProfile();
            sound = sound ?? new SoundProfile();

            float sizeFactor = ShapeProfileSizing.GetSizeFactor(PatternType.RhythmLoop, sp);
            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.RhythmLoop, sp);
            int bars = GuidedPolicy.Get("newage").bars;
            int totalSteps = bars * AppStateFactory.BarSteps;
            string presetId = genre != null
                ? genre.GetDefaultPresetId(PatternType.RhythmLoop)
                : "newage-bowl";

            // No swing — flowing, even timing; very slight natural wobble
            float swing = MathUtils.RoundTo(
                Mathf.Clamp(sp.wobble * 0.04f, 0f, 0.08f), 2);

            int barSteps = AppStateFactory.BarSteps;
            var events = new List<RhythmEvent>();

            for (int bar = 0; bar < bars; bar++)
            {
                int offset = bar * barSteps;

                // Backbone: soft kick on beat 1.
                events.Add(new RhythmEvent
                {
                    step = offset + 0,
                    lane = "kick",
                    velocity = MathUtils.RoundTo(0.34f + sound.body * 0.20f, 2),
                    microShift = 0f
                });

                // Backbone: shaker on beats 2 + 4.
                events.Add(new RhythmEvent
                {
                    step = offset + 4,
                    lane = "hat",
                    velocity = MathUtils.RoundTo(0.22f + (1f - sp.angularity) * 0.10f, 2),
                    microShift = MathUtils.RoundTo(Mathf.Sin(bar * 1.31f + sp.wobble * 2f) * 0.018f, 3)
                });
                events.Add(new RhythmEvent
                {
                    step = offset + 12,
                    lane = "hat",
                    velocity = MathUtils.RoundTo(0.20f + (1f - sp.angularity) * 0.08f, 2),
                    microShift = MathUtils.RoundTo(Mathf.Sin(bar * 1.31f + sp.wobble * 2f) * 0.018f, 3)
                });

                // Shape addition: bowl resonance on beat 3 when circularity is high.
                if (sp.circularity > 0.55f)
                {
                    events.Add(new RhythmEvent
                    {
                        step = offset + 8,
                        lane = "perc",
                        velocity = MathUtils.RoundTo(0.24f + sp.circularity * 0.14f + sound.body * 0.10f, 2),
                        microShift = 0f
                    });
                }

                // Shape addition: shaker accent on the "and" of beat 3 for angular shapes.
                if (sp.angularity > 0.5f)
                {
                    events.Add(new RhythmEvent
                    {
                        step = offset + 10,
                        lane = "hat",
                        velocity = MathUtils.RoundTo(0.16f + sp.angularity * 0.08f, 2),
                        microShift = MathUtils.RoundTo(sp.wobble * 0.015f, 3)
                    });
                }

                // Mallet swell on bars 4 and 8 (turnaround).
                if (bar == 3 || bar == 7)
                    AddMalletSwell(events, offset, bar == 7, sound);
            }

            events.Sort((a, b) =>
            {
                int stepCompare = a.step.CompareTo(b.step);
                if (stepCompare != 0)
                    return stepCompare;
                return string.CompareOrdinal(a.lane, b.lane);
            });

            string densityWord = sp.circularity > 0.55f ? "resonant" : "sparse";
            return new RhythmDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { "meditation", densityWord, "bowl" },
                derivedSequence = new DerivedSequence
                {
                    kind = "rhythm",
                    totalSteps = totalSteps,
                    swing = swing,
                    events = events
                },
                summary = $"{sizeWord} meditative floor, {bars} bars, {densityWord}.",
                details = "Soft kick on beat 1, shaker on 2+4. Circularity adds bowl on beat 3, angularity adds shaker accents. Bars 4 and 8 close with a mallet swell."
            };
        }

        private static void AddMalletSwell(List<RhythmEvent> events, int offset, bool finalBar, SoundProfile sound)
        {
            int[] malletSteps = finalBar ? new[] { 13, 14, 15 } : new[] { 14, 15 };
            float startVelocity = finalBar ? 0.36f : 0.30f;
            for (int i = 0; i < malletSteps.Length; i++)
            {
                events.Add(new RhythmEvent
                {
                    step = offset + malletSteps[i],
                    lane = "perc",
                    velocity = MathUtils.RoundTo(startVelocity + i * 0.04f + sound.body * 0.10f, 2),
                    microShift = 0f
                });
            }
        }
    }
}
