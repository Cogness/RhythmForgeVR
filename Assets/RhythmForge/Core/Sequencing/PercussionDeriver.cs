using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    public static class PercussionDeriver
    {
        public static RhythmDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string groupId,
            ShapeProfile sp,
            SoundProfile sound)
        {
            float sizeFactor = ShapeProfileSizing.GetSizeFactor(PatternType.Percussion, sp);
            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.Percussion, sp);
            int bars = GuidedDefaults.Bars;
            int totalSteps = bars * AppStateFactory.BarSteps;
            var group = InstrumentGroups.Get(groupId);
            string presetId = group.defaultPresetByType.GetDefault(PatternType.Percussion);

            int density = Mathf.Clamp(
                Mathf.RoundToInt(6f + sp.angularity * 4f + (1f - sp.symmetry) * 3f + sp.wobble * 3f + sizeFactor * 4f),
                6,
                14);
            float swing = MathUtils.RoundTo(
                Mathf.Clamp(sound.grooveInstability * 0.34f + sp.wobble * 0.08f + sizeFactor * 0.06f, 0f, 0.42f),
                2);

            var events = new List<RhythmEvent>();
            int barSteps = AppStateFactory.BarSteps;

            int[] kickPattern;
            if (sp.aspectRatio < 0.52f)
                kickPattern = new[] { 0, 6, 10, 13 };
            else if (sp.circularity > 0.75f)
                kickPattern = new[] { 0, 8, 12 };
            else
                kickPattern = new[] { 0, 7, 10, 13 };

            int[] snarePattern = sp.symmetry > 0.6f ? new[] { 8 } : new[] { 5, 8, 13 };
            int hatStride = (sp.angularity > 0.68f || sizeFactor > 0.68f) ? 1 : 2;
            int[] ghostSteps = { 3, 11, 15 };

            for (int bar = 0; bar < bars; bar++)
            {
                int offset = bar * barSteps;

                for (int i = 0; i < kickPattern.Length; i++)
                {
                    int step = kickPattern[i];
                    events.Add(new RhythmEvent
                    {
                        step = offset + step,
                        lane = "kick",
                        velocity = MathUtils.RoundTo(0.6f + sound.body * 0.3f - i * 0.05f, 2),
                        microShift = MathUtils.RoundTo(Mathf.Sin((step + 1f) * 1.13f + sp.wobble * 5f) * sound.grooveInstability * 0.05f, 3)
                    });
                }

                for (int i = 0; i < snarePattern.Length; i++)
                {
                    int step = snarePattern[i];
                    events.Add(new RhythmEvent
                    {
                        step = offset + step,
                        lane = "snare",
                        velocity = MathUtils.RoundTo(0.56f + sound.transientSharpness * 0.26f - i * 0.05f, 2),
                        microShift = MathUtils.RoundTo(Mathf.Sin((step + 3f) * 0.92f + sp.angularity * 4f) * sound.grooveInstability * 0.08f, 3)
                    });
                }

                for (int step = 0; step < barSteps; step += hatStride)
                {
                    if (Contains(kickPattern, step) || Contains(snarePattern, step))
                        continue;

                    float emphasis = step % 4 == 0 ? 0.08f : 0f;
                    events.Add(new RhythmEvent
                    {
                        step = offset + step,
                        lane = step % 2 == 0 ? "hat" : "perc",
                        velocity = MathUtils.RoundTo(0.24f + sound.brightness * 0.22f + emphasis, 2),
                        microShift = MathUtils.RoundTo(Mathf.Sin((step + 2f) * 1.47f + sp.curvatureVariance * 5f) * sound.grooveInstability * 0.18f, 3)
                    });
                }

                if (density > 11 || sp.symmetry < 0.45f || sizeFactor > 0.72f)
                {
                    for (int i = 0; i < ghostSteps.Length; i++)
                    {
                        int step = ghostSteps[i];
                        events.Add(new RhythmEvent
                        {
                            step = offset + step,
                            lane = step == 11 ? "snare" : "perc",
                            velocity = MathUtils.RoundTo(0.26f + sound.drive * 0.18f, 2),
                            microShift = MathUtils.RoundTo(Mathf.Sin((step + 4f) * 1.81f) * sound.grooveInstability * 0.12f, 3)
                        });
                    }
                }

                AddGuidedFills(events, bar, offset, sound);
            }

            EnsureAnchorEvents(events, sound);
            events.Sort((a, b) =>
            {
                int stepCompare = a.step.CompareTo(b.step);
                if (stepCompare != 0)
                    return stepCompare;

                return string.CompareOrdinal(a.lane, b.lane);
            });

            string angularWord = sp.angularity > 0.6f ? "sharp" : "round";
            return new RhythmDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string>
                {
                    "full",
                    $"{density} accents",
                    sound.drive > 0.58f ? "heated" : "steady",
                    "guided"
                },
                derivedSequence = new DerivedSequence
                {
                    kind = "rhythm",
                    totalSteps = totalSteps,
                    swing = swing,
                    events = events
                },
                summary = $"{sizeWord} percussion loop, {bars} bars, {density} accents, swing {Mathf.Round(swing * 100f)}%, {angularWord} DNA.",
                details = "Aspect ratio and circularity reshape the kick pattern, symmetry adds or removes snare ghosting, angularity can double the hat grid, and guided fills reinforce the phase transitions into bars 5 and 1."
            };
        }

        private static void AddGuidedFills(List<RhythmEvent> events, int barIndex, int offset, SoundProfile sound)
        {
            if (barIndex == 3)
            {
                AddSnareFill(events, offset, 14, 0.48f + sound.transientSharpness * 0.16f);
                AddSnareFill(events, offset, 15, 0.54f + sound.transientSharpness * 0.18f);
                events.Add(new RhythmEvent
                {
                    step = offset + AppStateFactory.BarSteps,
                    lane = "snare",
                    velocity = MathUtils.RoundTo(0.6f + sound.drive * 0.16f, 2),
                    microShift = 0f
                });
            }
            else if (barIndex == 7)
            {
                AddSnareFill(events, offset, 13, 0.46f + sound.transientSharpness * 0.16f);
                AddSnareFill(events, offset, 14, 0.52f + sound.transientSharpness * 0.18f);
                AddSnareFill(events, offset, 15, 0.58f + sound.transientSharpness * 0.2f);
                events.Add(new RhythmEvent
                {
                    step = offset + 15,
                    lane = "snare",
                    velocity = MathUtils.RoundTo(0.62f + sound.drive * 0.18f, 2),
                    microShift = 0f
                });
            }
        }

        private static void AddSnareFill(List<RhythmEvent> events, int offset, int step, float velocity)
        {
            events.Add(new RhythmEvent
            {
                step = offset + step,
                lane = "snare",
                velocity = MathUtils.RoundTo(velocity, 2),
                microShift = 0f
            });
        }

        private static void EnsureAnchorEvents(List<RhythmEvent> events, SoundProfile sound)
        {
            EnsureLaneAtStep(events, 0, "kick", 0.72f + sound.body * 0.18f);
            EnsureLaneAtStep(events, 8, "snare", 0.66f + sound.transientSharpness * 0.18f);
        }

        private static void EnsureLaneAtStep(List<RhythmEvent> events, int step, string lane, float velocity)
        {
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (evt.step != step || evt.lane != lane)
                    continue;

                evt.velocity = Mathf.Max(evt.velocity, MathUtils.RoundTo(velocity, 2));
                events[i] = evt;
                return;
            }

            events.Add(new RhythmEvent
            {
                step = step,
                lane = lane,
                velocity = MathUtils.RoundTo(velocity, 2),
                microShift = 0f
            });
        }

        private static bool Contains(int[] array, int value)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == value)
                    return true;
            }

            return false;
        }
    }
}
