using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Core.Sequencing.Jazz
{
    /// <summary>
    /// Jazz rhythm deriver (guided): 8-bar loop built on the beginner-safe backbeat
    /// floor (kick on 1+3, brush snare on 2+4) with a swung ride pattern and brush
    /// fills on bars 4 and 8. Shape traits layer ghost hits and off-beat ride
    /// additions on top of the backbone instead of replacing it.
    /// </summary>
    public sealed class JazzRhythmDeriver : IRhythmDeriver
    {
        // Jazz swing: triplet feel — every other 8th note pushed ~33% late.
        private const float SwingBase = 0.28f;

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
            int bars = GuidedPolicy.Get("jazz").bars;
            int totalSteps = bars * AppStateFactory.BarSteps;
            string presetId = genre != null
                ? genre.GetDefaultPresetId(PatternType.RhythmLoop)
                : "jazz-brush";

            float swing = MathUtils.RoundTo(
                Mathf.Clamp(SwingBase + sound.grooveInstability * 0.12f + sp.wobble * 0.06f, 0.22f, 0.38f), 2);

            int barSteps = AppStateFactory.BarSteps;
            var events = new List<RhythmEvent>();

            for (int bar = 0; bar < bars; bar++)
            {
                int offset = bar * barSteps;

                // Backbone: kick on beats 1 + 3.
                events.Add(new RhythmEvent
                {
                    step = offset + 0,
                    lane = "kick",
                    velocity = MathUtils.RoundTo(0.46f + sound.body * 0.18f, 2),
                    microShift = 0f
                });
                events.Add(new RhythmEvent
                {
                    step = offset + 8,
                    lane = "kick",
                    velocity = MathUtils.RoundTo(0.38f + sound.body * 0.14f, 2),
                    microShift = 0f
                });

                // Backbone: brush snare on beats 2 + 4.
                events.Add(new RhythmEvent
                {
                    step = offset + 4,
                    lane = "snare",
                    velocity = MathUtils.RoundTo(0.52f + sound.transientSharpness * 0.2f, 2),
                    microShift = MathUtils.RoundTo(swing * 0.02f, 3)
                });
                events.Add(new RhythmEvent
                {
                    step = offset + 12,
                    lane = "snare",
                    velocity = MathUtils.RoundTo(0.48f + sound.transientSharpness * 0.18f, 2),
                    microShift = MathUtils.RoundTo(swing * 0.02f, 3)
                });

                // Ride on every beat.
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

                // Shape additions layered on top — never replacing the backbone.
                if (sp.circularity > 0.55f)
                {
                    foreach (int step in new[] { 2, 6, 10, 14 })
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

                if (sp.angularity > 0.5f || sizeFactor > 0.52f || sp.wobble > 0.4f)
                {
                    foreach (int step in new[] { 3, 11 })
                    {
                        events.Add(new RhythmEvent
                        {
                            step = offset + step,
                            lane = "snare",
                            velocity = MathUtils.RoundTo(0.18f + sound.drive * 0.1f, 2),
                            microShift = MathUtils.RoundTo(swing * 0.08f, 3)
                        });
                    }
                }

                // Turnaround fills on bar 4 and bar 8.
                if (bar == 3 || bar == 7)
                    AddBrushFill(events, offset, bar == 7, sound);
            }

            events.Sort((a, b) =>
            {
                int stepCompare = a.step.CompareTo(b.step);
                if (stepCompare != 0)
                    return stepCompare;
                return string.CompareOrdinal(a.lane, b.lane);
            });

            string swingWord = swing > 0.32f ? "heavy swing" : "light swing";
            return new RhythmDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { "backbeat", swingWord, sp.angularity > 0.5f ? "cross-stick" : "smooth" },
                derivedSequence = new DerivedSequence
                {
                    kind = "rhythm",
                    totalSteps = totalSteps,
                    swing = swing,
                    events = events
                },
                summary = $"{sizeWord} jazz backbone, {bars} bars, {swingWord} ({Mathf.Round(swing * 100f)}%).",
                details = "Kick on 1+3, brush snare on 2+4, ride on every beat. Circularity adds off-beat ride fill, angularity adds ghost snares. Bars 4 and 8 close with a brush swirl."
            };
        }

        private static void AddBrushFill(List<RhythmEvent> events, int offset, bool finalBar, SoundProfile sound)
        {
            int[] snareSteps = finalBar ? new[] { 13, 14, 15 } : new[] { 14, 15 };
            float startVelocity = finalBar ? 0.46f : 0.42f;
            for (int i = 0; i < snareSteps.Length; i++)
            {
                events.Add(new RhythmEvent
                {
                    step = offset + snareSteps[i],
                    lane = "snare",
                    velocity = MathUtils.RoundTo(startVelocity + i * 0.05f + sound.transientSharpness * 0.14f, 2),
                    microShift = 0f
                });
            }

            events.Add(new RhythmEvent
            {
                step = offset + 15,
                lane = "perc",
                velocity = MathUtils.RoundTo(0.3f + sound.drive * 0.14f, 2),
                microShift = 0f
            });
        }
    }
}
