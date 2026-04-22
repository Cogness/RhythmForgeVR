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

        public static List<ScheduledMelodyNote> Apply(
            IReadOnlyList<MelodyNote> notes,
            GrooveProfile groove,
            int totalSteps,
            ChordProgression progression = null)
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
                bool preserveTiming = ShouldPreserveTiming(note, totalSteps, progression);
                if (!preserveTiming && stride > 1 && i % stride != 0)
                    continue;

                int quantizedStep = QuantizeStep(note.step, gridStep, totalSteps, preserveTiming);
                float shiftedStep = preserveTiming
                    ? quantizedStep
                    : ApplySyncopation(quantizedStep, groove.syncopation, totalSteps);
                int effectiveStep = Mathf.Clamp(Mathf.FloorToInt(shiftedStep), 0, Mathf.Max(0, totalSteps - 1));
                float startDelaySteps = Mathf.Clamp(shiftedStep - effectiveStep, 0f, 0.99f);
                int accentStep = preserveTiming ? note.step : effectiveStep;
                float accent = accentCurve[Mathf.Abs(accentStep) % 4];

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

            if (groove.density > 1.05f)
                AddDenseRetriggers(scheduled, groove, totalSteps, gridStep);

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
            if (density >= 0.9f)
                return 1;

            if (density >= 0.66f)
                return 2;

            return 3;
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

        private static void AddDenseRetriggers(List<ScheduledMelodyNote> notes, GrooveProfile groove, int totalSteps, int gridStep)
        {
            if (notes == null || notes.Count == 0)
                return;

            int targetExtra = Mathf.Clamp(
                Mathf.RoundToInt(notes.Count * Mathf.Clamp01((groove.density - 1f) / 0.5f)),
                0,
                notes.Count);
            if (targetExtra <= 0)
                return;

            var additions = new List<ScheduledMelodyNote>();
            int cadenceStart = Mathf.Max(0, totalSteps - AppStateFactory.BarSteps);

            for (int i = 0; i < notes.Count && additions.Count < targetExtra; i++)
            {
                var source = notes[i];
                if (IsPhraseAnchor(source.step, totalSteps))
                    continue;
                if (source.step >= cadenceStart)
                    continue;

                float nextStart = i < notes.Count - 1
                    ? notes[i + 1].step + notes[i + 1].startDelaySteps
                    : totalSteps;
                int candidateStep = source.step + Mathf.Max(1, gridStep);
                if (candidateStep >= totalSteps)
                    continue;
                if (candidateStep >= Mathf.FloorToInt(nextStart))
                    continue;
                if (ContainsStep(notes, candidateStep) || ContainsStep(additions, candidateStep))
                    continue;

                int available = Mathf.Max(2, Mathf.FloorToInt(nextStart - candidateStep));
                additions.Add(new ScheduledMelodyNote
                {
                    step = candidateStep,
                    midi = source.midi,
                    durationSteps = Mathf.Clamp(Mathf.Min(source.durationSteps, 2 + gridStep), 2, available),
                    velocity = Mathf.Clamp(MathUtils.RoundTo(source.velocity * 0.78f, 2), 0.12f, 0.92f),
                    glide = source.glide * 0.65f,
                    startDelaySteps = Mathf.Clamp(groove.syncopation * 0.25f, 0f, 0.35f)
                });
            }

            if (additions.Count > 0)
                notes.AddRange(additions);
        }

        private static bool ContainsStep(List<ScheduledMelodyNote> notes, int step)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                if (notes[i].step == step)
                    return true;
            }

            return false;
        }

        private static bool IsPhraseAnchor(int step, int totalSteps)
        {
            if (step == 0)
                return true;

            int answerAnchor = AppStateFactory.BarSteps * 4;
            return totalSteps > answerAnchor && step == answerAnchor;
        }

        private static bool ShouldPreserveTiming(MelodyNote note, int totalSteps, ChordProgression progression)
        {
            if (IsPhraseAnchor(note.step, totalSteps))
                return true;

            if (note.step % 4 != 0)
                return false;

            return IsChordToneOfCurrentBar(note, progression);
        }

        private static bool IsChordToneOfCurrentBar(MelodyNote note, ChordProgression progression)
        {
            if (progression == null || progression.chords == null || progression.chords.Count == 0)
                return false;

            var slot = progression.GetSlotForBar(note.step / AppStateFactory.BarSteps);
            if (slot == null || slot.voicing == null || slot.voicing.Count == 0)
                return false;

            int pitchClass = ((note.midi % 12) + 12) % 12;
            for (int i = 0; i < slot.voicing.Count; i++)
            {
                if ((((slot.voicing[i] % 12) + 12) % 12) == pitchClass)
                    return true;
            }

            return false;
        }
    }
}
