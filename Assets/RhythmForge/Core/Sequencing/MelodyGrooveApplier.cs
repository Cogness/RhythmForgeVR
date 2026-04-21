using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    public struct ScheduledMelodyNote
    {
        public int step;
        public int midi;
        public int durationSteps;
        public float velocity;
        public float glide;
        public float startDelaySteps;
    }

    public static class MelodyGrooveApplier
    {
        private static readonly float[] DefaultAccentCurve = { 1f, 0.7f, 0.85f, 0.7f };

        public static List<ScheduledMelodyNote> Apply(IReadOnlyList<MelodyNote> notes, GrooveProfile groove, int totalSteps)
        {
            var scheduled = new List<ScheduledMelodyNote>();
            if (notes == null || notes.Count == 0)
                return scheduled;

            if (groove == null)
            {
                for (int i = 0; i < notes.Count; i++)
                {
                    var note = notes[i];
                    scheduled.Add(new ScheduledMelodyNote
                    {
                        step = note.step,
                        midi = note.midi,
                        durationSteps = note.durationSteps,
                        velocity = note.velocity,
                        glide = note.glide,
                        startDelaySteps = 0f
                    });
                }

                return scheduled;
            }

            int stride = ResolveStride(groove.density);
            int gridStep = groove.quantizeGrid >= 16 ? 1 : 2;
            float[] accentCurve = groove.accentCurve != null && groove.accentCurve.Length >= 4
                ? groove.accentCurve
                : DefaultAccentCurve;

            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                bool isAnchor = IsPhraseAnchor(note.step, totalSteps);
                if (!isAnchor && stride > 1 && i % stride != 0)
                    continue;

                int quantizedStep = QuantizeStep(note.step, gridStep, totalSteps, isAnchor);
                float shiftedStep = ApplySyncopation(quantizedStep, groove.syncopation, totalSteps);
                int effectiveStep = Mathf.Clamp(Mathf.FloorToInt(shiftedStep), 0, Mathf.Max(0, totalSteps - 1));
                float startDelaySteps = Mathf.Clamp(shiftedStep - effectiveStep, 0f, 0.99f);
                float accent = accentCurve[Mathf.Abs(effectiveStep) % 4];

                scheduled.Add(new ScheduledMelodyNote
                {
                    step = effectiveStep,
                    midi = note.midi,
                    durationSteps = note.durationSteps,
                    velocity = Mathf.Clamp(MathUtils.RoundTo(note.velocity * accent, 2), 0.12f, 1f),
                    glide = note.glide,
                    startDelaySteps = startDelaySteps
                });
            }

            scheduled.Sort((a, b) =>
            {
                int stepCompare = a.step.CompareTo(b.step);
                if (stepCompare != 0)
                    return stepCompare;

                return a.startDelaySteps.CompareTo(b.startDelaySteps);
            });

            FitDurations(scheduled, stride, totalSteps);
            EnsureCadenceHold(scheduled, totalSteps);
            return scheduled;
        }

        private static int ResolveStride(float density)
        {
            if (density >= 0.99f)
                return 1;

            return Mathf.Clamp(Mathf.RoundToInt(1f / Mathf.Max(0.01f, density)), 1, 4);
        }

        private static int QuantizeStep(int step, int gridStep, int totalSteps, bool preserveAnchor)
        {
            if (preserveAnchor || gridStep <= 1)
                return Mathf.Clamp(step, 0, Mathf.Max(0, totalSteps - 1));

            int quantized = Mathf.RoundToInt((float)step / gridStep) * gridStep;
            return Mathf.Clamp(quantized, 0, Mathf.Max(0, totalSteps - 1));
        }

        private static float ApplySyncopation(int step, float syncopation, int totalSteps)
        {
            int beatOffset = Mathf.Abs(step) % 4;
            if (beatOffset == 0 || syncopation <= 0.001f)
                return step;

            float signedOffset = beatOffset == 2 ? syncopation : -syncopation;
            float shifted = step + signedOffset;
            float beatStart = step - beatOffset;
            float beatEnd = Mathf.Min(totalSteps - 0.001f, beatStart + 3.999f);
            return Mathf.Clamp(shifted, beatStart, beatEnd);
        }

        private static void FitDurations(List<ScheduledMelodyNote> notes, int stride, int totalSteps)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                float currentStart = notes[i].step + notes[i].startDelaySteps;
                float nextStart = i < notes.Count - 1
                    ? notes[i + 1].step + notes[i + 1].startDelaySteps
                    : totalSteps;
                int maxDuration = Mathf.Max(2, Mathf.FloorToInt(nextStart - currentStart));

                var note = notes[i];
                if (stride > 1)
                    note.durationSteps = Mathf.Max(note.durationSteps, maxDuration);
                note.durationSteps = Mathf.Clamp(note.durationSteps, 2, maxDuration);
                notes[i] = note;
            }
        }

        private static void EnsureCadenceHold(List<ScheduledMelodyNote> notes, int totalSteps)
        {
            if (notes == null || notes.Count == 0)
                return;

            int cadenceStart = Mathf.Max(0, totalSteps - AppStateFactory.BarSteps);
            int lastIndex = notes.Count - 1;
            if (notes[lastIndex].step < cadenceStart)
                return;

            var cadence = notes[lastIndex];
            int available = Mathf.Max(8, totalSteps - cadence.step);
            cadence.durationSteps = Mathf.Clamp(Mathf.Max(cadence.durationSteps, 8), 8, available);
            notes[lastIndex] = cadence;
        }

        private static bool IsPhraseAnchor(int step, int totalSteps)
        {
            if (step == 0)
                return true;

            int answerAnchor = AppStateFactory.BarSteps * 4;
            return totalSteps > answerAnchor && step == answerAnchor;
        }
    }
}
