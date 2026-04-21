using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
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
        private const float FourBarThreshold = 0.80f;
        private const float SixteenSliceThreshold = 0.75f;
        private const string GuidedGenreId = "electronic";
        private const int StrongBeatStepSize = 4;

        public static MelodyDerivationResult Derive(
            List<Vector2> points, StrokeMetrics metrics,
            string keyName, string groupId,
            ShapeProfile sp, SoundProfile sound)
        {
            var progression = HarmonicContextProvider.CurrentProgression;
            if (progression != null && progression.chords != null && progression.chords.Count > 0)
                return DeriveGuided(points, metrics, keyName, groupId, sp, sound, progression);

            return DeriveLegacy(points, metrics, keyName, groupId, sp, sound);
        }

        private static MelodyDerivationResult DeriveGuided(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            string groupId,
            ShapeProfile sp,
            SoundProfile sound,
            ChordProgression progression)
        {
            float sizeFactor = ShapeProfileSizing.GetSizeFactor(PatternType.Melody, sp);
            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.Melody, sp);
            int bars = progression != null && progression.bars > 0 ? progression.bars : GuidedDefaults.Bars;
            int totalSteps = bars * AppStateFactory.BarSteps;
            var group = InstrumentGroups.Get(groupId);
            string presetId = group.defaultPresetByType.GetDefault(PatternType.Melody);

            int sampleCount = DetermineGuidedSampleCount(metrics, sp);
            int quantizeGrid = sp.speedVariance > 0.65f ? 1 : 2;
            bool liftAnswerPhrase = sp.tiltSigned > 0f;
            var samples = StrokeAnalyzer.ResampleStroke(points, sampleCount);
            var notes = new List<MelodyNote>(samples.Count);

            float pitchCenter = metrics.minY + metrics.height / 2f;
            float verticalScale = 0.78f + sp.verticalSpan * 1.1f;

            for (int i = 0; i < samples.Count; i++)
            {
                Vector2 prev = samples[Mathf.Max(0, i - 1)];
                Vector2 curr = samples[i];
                Vector2 next = samples[Mathf.Min(samples.Count - 1, i + 1)];

                int rawStep = Mathf.RoundToInt((float)i / Mathf.Max(1, sampleCount - 1) * (totalSteps - 1));
                int step = QuantizeStep(rawStep, quantizeGrid, totalSteps);
                int barIndex = Mathf.Clamp(step / AppStateFactory.BarSteps, 0, Mathf.Max(0, bars - 1));

                if (notes.Count > 0 && step <= notes[notes.Count - 1].step)
                    step = Mathf.Min(totalSteps - 1, notes[notes.Count - 1].step + quantizeGrid);

                barIndex = Mathf.Clamp(step / AppStateFactory.BarSteps, 0, Mathf.Max(0, bars - 1));
                HarmonicContextProvider.SetFromProgression(progression, barIndex);
                var harmCtx = HarmonicContextProvider.Current;

                float centeredY = Mathf.Clamp(
                    (curr.y - pitchCenter) / Mathf.Max(metrics.height, 0.001f) * verticalScale + 0.5f,
                    0f, 0.999f);

                int midi = PitchUtils.PitchFromRelative(centeredY, keyName);
                if (liftAnswerPhrase && (barIndex == 4 || barIndex == 5))
                    midi = TransposeByScaleDegrees(midi, keyName, 2);

                bool isStrongBeat = step % StrongBeatStepSize == 0;
                midi = isStrongBeat && harmCtx.HasChord
                    ? harmCtx.NearestChordTone(midi)
                    : MusicalKeys.QuantizeToKey(midi, keyName);
                midi = RegisterPolicy.Clamp(midi, PatternType.Melody, GuidedGenreId);
                midi = isStrongBeat && harmCtx.HasChord
                    ? harmCtx.NearestChordTone(midi)
                    : MusicalKeys.QuantizeToKey(midi, keyName);

                float speed = Vector2.Distance(prev, next);
                float curvature = Mathf.Abs(next.y - curr.y) + Mathf.Abs(curr.y - prev.y);
                int durationSteps = ResolveGuidedDurationSteps(i, step, totalSteps, speed, sp, sound, sizeFactor);
                float slope = Mathf.Clamp(
                    (next.y - prev.y) / Mathf.Max(metrics.height, 0.001f) * 4f, -1f, 1f);
                float velocity = Mathf.Clamp(
                    0.34f +
                    curvature * 0.62f +
                    sp.curvatureMean * 0.10f +
                    (isStrongBeat ? 0.08f : 0f),
                    0.28f,
                    0.88f);

                notes.Add(new MelodyNote
                {
                    step = step,
                    midi = midi,
                    durationSteps = durationSteps,
                    velocity = MathUtils.RoundTo(velocity, 2),
                    glide = MathUtils.RoundTo(slope * (0.24f + sound.filterMotion * 0.75f), 2)
                });
            }

            FitDurationsToPhrase(notes, totalSteps);
            EnsureCadenceHold(notes, totalSteps);

            string motionWord = sp.angularity > 0.6f ? "edged" : "smooth";
            string answerWord = liftAnswerPhrase ? "lifted answer" : "grounded answer";
            return new MelodyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { "lead", motionWord, answerWord },
                derivedSequence = new DerivedSequence
                {
                    kind = "melody",
                    totalSteps = totalSteps,
                    notes = notes
                },
                summary = $"{sizeWord} lead line, {bars} bars, strong beats locked to the harmony, {answerWord}.",
                details = "The stroke contour chooses in-key pitch motion, strong beats snap to the current bar's chord tones, speed variance opens 16th-note placement, and a positive tilt lifts the answer phrase in bars 5 and 6."
            };
        }

        private static MelodyDerivationResult DeriveLegacy(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            string groupId,
            ShapeProfile sp,
            SoundProfile sound)
        {
            float sizeFactor = ShapeProfileSizing.GetSizeFactor(PatternType.MelodyLine, sp);
            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.MelodyLine, sp);
            int bars = metrics.length > FourBarThreshold ? 4 : 2;
            int totalSteps = bars * AppStateFactory.BarSteps;

            var group = InstrumentGroups.Get(groupId);
            string presetId = group.defaultPresetByType.MelodyLine;

            var role    = ShapeRoleProvider.Current;
            var harmCtx = HarmonicContextProvider.Current;
            var notes   = new List<MelodyNote>();

            if (role.index >= 2)
            {
                // Role 2+: one long root-note per bar, register-clamped, offset by 2/3 bar
                var key2 = MusicalKeys.Get(keyName);
                int rootMidi = harmCtx.HasChord ? harmCtx.rootMidi : key2.rootMidi + 12;
                rootMidi = RegisterPolicy.Clamp(rootMidi, PatternType.MelodyLine, "electronic");
                int barSteps2 = AppStateFactory.BarSteps;
                int roleOffset2 = barSteps2 * 2 / 3;
                for (int bar = 0; bar < bars; bar++)
                {
                    notes.Add(new MelodyNote
                    {
                        step = (bar * barSteps2 + roleOffset2) % totalSteps,
                        midi = rootMidi,
                        durationSteps = barSteps2 - 2,
                        velocity = MathUtils.RoundTo(0.24f + sp.verticalSpan * 0.08f, 2),
                        glide = 0f
                    });
                }
            }
            else
            {
                // Role 0: primary (existing behaviour, full slice count)
                // Role 1: counter — half slices, octave up, chord-tones only, offset by 1/3 bar
                int sliceCount = role.IsPrimary
                    ? ((metrics.length > SixteenSliceThreshold || sp.pathLength > 0.68f) ? 16 : 8)
                    : 4;

                var samples = StrokeAnalyzer.ResampleStroke(points, sliceCount);
                float pitchCenter  = metrics.minY + metrics.height / 2f;
                float verticalScale = 0.72f + sp.verticalSpan * 1.2f;
                int barSteps = AppStateFactory.BarSteps;
                int roleOffset = role.index == 1 ? barSteps / 3 : 0;

                for (int i = 0; i < samples.Count; i++)
                {
                    Vector2 prev = samples[Mathf.Max(0, i - 1)];
                    Vector2 curr = samples[i];
                    Vector2 next = samples[Mathf.Min(samples.Count - 1, i + 1)];

                    float speed     = Vector2.Distance(prev, next);
                    float curvature = Mathf.Abs(next.y - curr.y) + Mathf.Abs(curr.y - prev.y);
                    float centeredY = Mathf.Clamp(
                        (curr.y - pitchCenter) / Mathf.Max(metrics.height, 0.001f) * verticalScale + 0.5f,
                        0f, 0.999f);

                    int midi = PitchUtils.PitchFromRelative(centeredY, keyName);

                    if (!role.IsPrimary)
                    {
                        midi += 12; // counter: one octave up
                        if (harmCtx.HasChord) midi = harmCtx.NearestChordTone(midi);
                    }
                    else
                    {
                        int step0 = Mathf.FloorToInt((float)i / sliceCount * totalSteps);
                        if (step0 % 4 == 0 && harmCtx.HasChord)
                            midi = harmCtx.NearestChordTone(midi);
                    }

                    midi = RegisterPolicy.Clamp(midi, PatternType.MelodyLine, "electronic");

                    int step = (Mathf.FloorToInt((float)i / sliceCount * totalSteps) + roleOffset) % totalSteps;
                    int durationBase  = speed < 0.025f ? 6 : speed < 0.042f ? 4 : 2;
                    int durationSteps = role.IsPrimary
                        ? Mathf.Clamp(Mathf.RoundToInt(durationBase + (1f - sp.speedVariance) * 1.2f - sound.transientSharpness * 1.1f + sizeFactor * 2f), 2, 7)
                        : Mathf.Clamp(Mathf.RoundToInt(totalSteps / sliceCount * 0.75f), 4, totalSteps / 2);

                    float slope = Mathf.Clamp(
                        (next.y - prev.y) / Mathf.Max(metrics.height, 0.001f) * 4f, -1f, 1f);

                    notes.Add(new MelodyNote
                    {
                        step = step,
                        midi = midi,
                        durationSteps = durationSteps,
                        velocity = MathUtils.RoundTo(
                            Mathf.Clamp(role.IsPrimary
                                ? 0.34f + curvature * 0.86f + sp.curvatureMean * 0.16f
                                : 0.24f + curvature * 0.60f,
                                0.22f, 0.88f), 2),
                        glide = MathUtils.RoundTo(slope * (0.3f + sound.filterMotion * 0.85f), 2)
                    });
                }
            }

            string roleLabel = role.index == 0 ? "primary" : role.index == 1 ? "counter" : "fill";
            string modWord   = sound.modDepth > 0.56f ? "animated" : "steady";
            return new MelodyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { roleLabel, modWord },
                derivedSequence = new DerivedSequence
                {
                    kind = "melody",
                    totalSteps = totalSteps,
                    notes = notes
                },
                summary = $"{sizeWord} {roleLabel} line, {bars} bars, contour {Mathf.Round(sp.verticalSpan * 100f)}%, {modWord} voice.",
                details = "Size pushes sustain, width, and modulation depth, while vertical span drives octave reach and direction bias adds glide pull into notes."
            };
        }

        private static int DetermineGuidedSampleCount(StrokeMetrics metrics, ShapeProfile sp)
        {
            int baseCount = metrics.length > SixteenSliceThreshold || sp.pathLength > 0.68f ? 24 : 16;
            if (sp.speedVariance > 0.65f)
                baseCount += 8;
            return Mathf.Clamp(baseCount, 16, 32);
        }

        private static int QuantizeStep(int rawStep, int quantizeGrid, int totalSteps)
        {
            if (quantizeGrid <= 1)
                return Mathf.Clamp(rawStep, 0, totalSteps - 1);

            int quantized = Mathf.RoundToInt((float)rawStep / quantizeGrid) * quantizeGrid;
            return Mathf.Clamp(quantized, 0, totalSteps - 1);
        }

        private static int ResolveGuidedDurationSteps(
            int noteIndex,
            int step,
            int totalSteps,
            float speed,
            ShapeProfile sp,
            SoundProfile sound,
            float sizeFactor)
        {
            int[] allowedDurations = { 2, 4, 6, 8 };
            int duration = speed < 0.02f
                ? 8
                : speed < 0.04f ? 6 : speed < 0.08f ? 4 : 2;

            duration += sp.speedVariance < 0.3f ? 2 : 0;
            duration += sizeFactor > 0.6f ? 2 : 0;
            duration -= sound.transientSharpness > 0.65f ? 2 : 0;
            duration = Mathf.Clamp(duration, 2, 8);

            int snapped = allowedDurations[0];
            int bestDistance = int.MaxValue;
            for (int i = 0; i < allowedDurations.Length; i++)
            {
                int distance = Mathf.Abs(allowedDurations[i] - duration);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    snapped = allowedDurations[i];
                }
            }

            if (noteIndex == 0 || step == 0)
                snapped = Mathf.Max(snapped, 4);

            return Mathf.Clamp(snapped, 2, Mathf.Max(2, totalSteps - step));
        }

        private static void FitDurationsToPhrase(List<MelodyNote> notes, int totalSteps)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                int nextStep = i < notes.Count - 1 ? notes[i + 1].step : totalSteps;
                int maxDuration = Mathf.Max(2, nextStep - notes[i].step);
                notes[i].durationSteps = Mathf.Clamp(notes[i].durationSteps, 2, maxDuration);
            }
        }

        private static void EnsureCadenceHold(List<MelodyNote> notes, int totalSteps)
        {
            if (notes == null || notes.Count == 0)
                return;

            int cadenceStart = Mathf.Max(0, totalSteps - AppStateFactory.BarSteps);
            int latestHalfNoteStart = Mathf.Max(0, totalSteps - 8);
            MelodyNote finalNote = notes[notes.Count - 1];
            if (finalNote.step < cadenceStart)
            {
                finalNote.step = cadenceStart;
                notes[notes.Count - 1] = finalNote;
            }

            if (finalNote.step > latestHalfNoteStart)
            {
                finalNote.step = latestHalfNoteStart;
                notes[notes.Count - 1] = finalNote;
            }

            for (int i = notes.Count - 2; i >= 0; i--)
            {
                if (notes[i].step < finalNote.step)
                    break;

                notes.RemoveAt(i);
            }

            finalNote = notes[notes.Count - 1];
            int available = Mathf.Max(8, totalSteps - finalNote.step);
            finalNote.durationSteps = Mathf.Clamp(Mathf.Max(finalNote.durationSteps, 8), 8, available);
            notes[notes.Count - 1] = finalNote;

            FitDurationsToPhrase(notes, totalSteps);
        }

        private static int TransposeByScaleDegrees(int midi, string keyName, int degreeSteps)
        {
            int quantized = MusicalKeys.QuantizeToKey(midi, keyName);
            int direction = degreeSteps >= 0 ? 1 : -1;
            int remaining = Mathf.Abs(degreeSteps);
            int current = quantized;

            while (remaining > 0)
            {
                current += direction;
                while (MusicalKeys.QuantizeToKey(current, keyName) != current)
                    current += direction;
                remaining--;
            }

            return current;
        }
    }
}
