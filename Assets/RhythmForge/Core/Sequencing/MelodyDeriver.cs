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

    /// <summary>
    /// Per-genre overrides for the guided melody derivation. Leaves all fields at
    /// their Electronic defaults when null is passed. Jazz/NewAge derivers populate
    /// <see cref="scaleIntervals"/>, <see cref="genreId"/>, and optionally
    /// <see cref="presetId"/> to keep the guided invariants (phrase anchors, strong-beat
    /// chord-tone lock, answer lift, cadence hold) while swapping the in-key scale and
    /// register clamp for their own idiomatic choices.
    /// </summary>
    public sealed class MelodyDerivationOptions
    {
        /// <summary>
        /// Custom scale intervals (relative to the key's root, 0..11 pitch classes)
        /// used for non-strong-beat pitches. When null the key's diatonic scale is used.
        /// </summary>
        public int[] scaleIntervals;

        /// <summary>Genre id for <see cref="RegisterPolicy.Clamp"/>. Null -> active guided genre.</summary>
        public string genreId;

        /// <summary>Explicit preset id override. Null -> genre's default melody preset.</summary>
        public string presetId;

        /// <summary>Optional tag override written to the derived sequence (e.g. "blues", "pentatonic").</summary>
        public string styleTag;
    }

    public static class MelodyDeriver
    {
        private const float FourBarThreshold = 0.80f;
        private const float SixteenSliceThreshold = 0.75f;
        private const int StrongBeatStepSize = 4;

        private static string GuidedGenreId => GuidedPolicy.Active.genreId;

        public static MelodyDerivationResult Derive(
            List<Vector2> points, StrokeMetrics metrics,
            string keyName, string groupId,
            ShapeProfile sp, SoundProfile sound)
        {
            var progression = HarmonicContextProvider.CurrentProgression;
            if (progression != null && progression.chords != null && progression.chords.Count > 0)
                return DeriveGuided(points, metrics, keyName, groupId, sp, sound, progression, null);

            return DeriveLegacy(points, metrics, keyName, groupId, sp, sound);
        }

        /// <summary>
        /// Guided melody derivation with an optional per-genre options bundle. Used by
        /// <c>JazzMelodyDeriver</c> and <c>NewAgeMelodyDeriver</c> to reuse the guided
        /// invariants while overriding the in-key scale, register clamp genre, and preset.
        /// </summary>
        public static MelodyDerivationResult DeriveGuided(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            string groupId,
            ShapeProfile sp,
            SoundProfile sound,
            ChordProgression progression,
            MelodyDerivationOptions options)
        {
            sp = sp ?? new ShapeProfile();
            sound = sound ?? new SoundProfile();
            options = options ?? new MelodyDerivationOptions();
            string effectiveGenreId = !string.IsNullOrEmpty(options.genreId) ? options.genreId : GuidedGenreId;
            int[] scaleIntervals = options.scaleIntervals;

            float sizeFactor = ShapeProfileSizing.GetSizeFactor(PatternType.Melody, sp);
            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.Melody, sp);
            int bars = progression != null && progression.bars > 0 ? progression.bars : GuidedDefaults.Bars;
            int totalSteps = bars * AppStateFactory.BarSteps;
            string presetId = ResolveMelodyPresetId(groupId, options);

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

                int midi = PitchUtils.PitchFromRelative(centeredY, keyName, scaleIntervals);
                if (liftAnswerPhrase && barIndex >= 4)
                    midi = TransposeByScaleDegrees(midi, keyName, 2, scaleIntervals);

                bool isStrongBeat = step % StrongBeatStepSize == 0;
                midi = isStrongBeat && harmCtx.HasChord
                    ? harmCtx.NearestChordTone(midi)
                    : PitchUtils.QuantizeToScale(midi, keyName, scaleIntervals);
                midi = RegisterPolicy.Clamp(midi, PatternType.Melody, effectiveGenreId);
                midi = isStrongBeat && harmCtx.HasChord
                    ? harmCtx.NearestChordTone(midi)
                    : PitchUtils.QuantizeToScale(midi, keyName, scaleIntervals);

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

            EnsurePhraseAnchor(notes, progression, keyName, 0, totalSteps, effectiveGenreId);
            int answerAnchorStep = AppStateFactory.BarSteps * 4;
            if (totalSteps > answerAnchorStep)
                EnsurePhraseAnchor(notes, progression, keyName, answerAnchorStep, totalSteps, effectiveGenreId);

            FitDurationsToPhrase(notes, totalSteps);
            EnsureCadenceHold(notes, totalSteps);

            string motionWord = sp.angularity > 0.6f ? "edged" : "smooth";
            string answerWord = liftAnswerPhrase ? "lifted answer" : "grounded answer";
            string styleTag = !string.IsNullOrEmpty(options.styleTag) ? options.styleTag : "lead";
            return new MelodyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { styleTag, motionWord, answerWord },
                derivedSequence = new DerivedSequence
                {
                    kind = "melody",
                    totalSteps = totalSteps,
                    notes = notes
                },
                summary = $"{sizeWord} lead line, {bars} bars, strong beats locked to the harmony, {answerWord}.",
                details = "The stroke contour chooses in-key pitch motion, strong beats snap to the current bar's chord tones, phrase anchors are guaranteed on bars 1 and 5, speed variance opens 16th-note placement, and a positive tilt lifts the answer phrase across bars 5 to 8."
            };
        }

        private static string ResolveMelodyPresetId(string groupId, MelodyDerivationOptions options)
        {
            if (options != null && !string.IsNullOrEmpty(options.presetId))
                return options.presetId;
            var group = InstrumentGroups.Get(groupId);
            return group.defaultPresetByType.GetDefault(PatternType.Melody);
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

            var harmCtx = HarmonicContextProvider.Current;
            var notes   = new List<MelodyNote>();
            int sliceCount = metrics.length > SixteenSliceThreshold || sp.pathLength > 0.68f ? 16 : 8;
            var samples = StrokeAnalyzer.ResampleStroke(points, sliceCount);
            float pitchCenter  = metrics.minY + metrics.height / 2f;
            float verticalScale = 0.72f + sp.verticalSpan * 1.2f;

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
                int step = Mathf.FloorToInt((float)i / sliceCount * totalSteps);
                if (step % 4 == 0 && harmCtx.HasChord)
                    midi = harmCtx.NearestChordTone(midi);

                midi = RegisterPolicy.Clamp(midi, PatternType.MelodyLine, "electronic");

                int durationBase  = speed < 0.025f ? 6 : speed < 0.042f ? 4 : 2;
                int durationSteps = Mathf.Clamp(Mathf.RoundToInt(durationBase + (1f - sp.speedVariance) * 1.2f - sound.transientSharpness * 1.1f + sizeFactor * 2f), 2, 7);

                float slope = Mathf.Clamp(
                    (next.y - prev.y) / Mathf.Max(metrics.height, 0.001f) * 4f, -1f, 1f);

                notes.Add(new MelodyNote
                {
                    step = step,
                    midi = midi,
                    durationSteps = durationSteps,
                    velocity = MathUtils.RoundTo(
                        Mathf.Clamp(0.34f + curvature * 0.86f + sp.curvatureMean * 0.16f, 0.22f, 0.88f), 2),
                    glide = MathUtils.RoundTo(slope * (0.3f + sound.filterMotion * 0.85f), 2)
                });
            }

            string modWord   = sound.modDepth > 0.56f ? "animated" : "steady";
            return new MelodyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { "lead", modWord },
                derivedSequence = new DerivedSequence
                {
                    kind = "melody",
                    totalSteps = totalSteps,
                    notes = notes
                },
                summary = $"{sizeWord} lead line, {bars} bars, contour {Mathf.Round(sp.verticalSpan * 100f)}%, {modWord} voice.",
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

        private static void EnsurePhraseAnchor(
            List<MelodyNote> notes,
            ChordProgression progression,
            string keyName,
            int anchorStep,
            int totalSteps,
            string genreId = null)
        {
            if (notes == null || totalSteps <= 0 || anchorStep < 0 || anchorStep >= totalSteps)
                return;

            int anchorIndex = FindNoteIndexAtStep(notes, anchorStep);
            int targetMidi = ResolveAnchorMidi(notes, progression, keyName, anchorStep, genreId);
            float velocity = ResolveAnchorVelocity(notes, anchorStep);

            if (anchorIndex >= 0)
            {
                var anchor = notes[anchorIndex];
                anchor.midi = targetMidi;
                anchor.durationSteps = Mathf.Max(anchor.durationSteps, 4);
                anchor.velocity = Mathf.Max(anchor.velocity, velocity);
                notes[anchorIndex] = anchor;
                return;
            }

            notes.Add(new MelodyNote
            {
                step = anchorStep,
                midi = targetMidi,
                durationSteps = Mathf.Clamp(4, 2, Mathf.Max(2, totalSteps - anchorStep)),
                velocity = velocity,
                glide = 0f
            });

            notes.Sort((a, b) => a.step.CompareTo(b.step));
        }

        private static int FindNoteIndexAtStep(List<MelodyNote> notes, int step)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                if (notes[i].step == step)
                    return i;
            }

            return -1;
        }

        private static int ResolveAnchorMidi(
            List<MelodyNote> notes,
            ChordProgression progression,
            string keyName,
            int anchorStep,
            string genreId)
        {
            string effectiveGenreId = string.IsNullOrEmpty(genreId) ? GuidedGenreId : genreId;
            int fallbackMidi = MusicalKeys.Get(keyName).rootMidi;
            int sourceMidi = fallbackMidi;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < notes.Count; i++)
            {
                int distance = Mathf.Abs(notes[i].step - anchorStep);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                sourceMidi = notes[i].midi;
            }

            var slot = progression?.GetSlotForBar(anchorStep / AppStateFactory.BarSteps);
            if (slot != null && slot.voicing != null && slot.voicing.Count > 0)
            {
                var harmonicContext = progression.ToHarmonicContext(anchorStep / AppStateFactory.BarSteps);
                int clamped = RegisterPolicy.Clamp(sourceMidi, PatternType.Melody, effectiveGenreId);
                return harmonicContext.NearestChordTone(clamped);
            }

            int quantized = MusicalKeys.QuantizeToKey(sourceMidi, keyName);
            return RegisterPolicy.Clamp(quantized, PatternType.Melody, effectiveGenreId);
        }

        private static float ResolveAnchorVelocity(List<MelodyNote> notes, int anchorStep)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                if (Mathf.Abs(notes[i].step - anchorStep) <= 4)
                    return Mathf.Clamp(MathUtils.RoundTo(notes[i].velocity + 0.06f, 2), 0.38f, 0.88f);
            }

            return 0.62f;
        }

        private static int TransposeByScaleDegrees(int midi, string keyName, int degreeSteps)
        {
            return TransposeByScaleDegrees(midi, keyName, degreeSteps, null);
        }

        private static int TransposeByScaleDegrees(int midi, string keyName, int degreeSteps, int[] scaleIntervals)
        {
            int quantized = PitchUtils.QuantizeToScale(midi, keyName, scaleIntervals);
            int direction = degreeSteps >= 0 ? 1 : -1;
            int remaining = Mathf.Abs(degreeSteps);
            int current = quantized;

            while (remaining > 0)
            {
                current += direction;
                while (PitchUtils.QuantizeToScale(current, keyName, scaleIntervals) != current)
                    current += direction;
                remaining--;
            }

            return current;
        }
    }
}
