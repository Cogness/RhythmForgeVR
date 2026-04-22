using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    public static class BassDeriver
    {
        private const int StepsPerBar = AppStateFactory.BarSteps;

        private static string GuidedGenreId => GuidedPolicy.Active.genreId;
        private static string DefaultPresetId => GuidedPolicy.Active.defaultBassPresetId;

        public static MelodyDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            string groupId,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile)
        {
            shapeProfile = shapeProfile ?? new ShapeProfile();
            soundProfile = soundProfile ?? new SoundProfile();
            var progression = HarmonicContextProvider.CurrentProgression;
            if (progression == null || progression.chords == null || progression.chords.Count == 0)
                progression = CreateFallbackProgression(keyName);

            int bars = progression.bars > 0 ? progression.bars : GuidedDefaults.Bars;
            int totalSteps = bars * StepsPerBar;
            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.Bass, shapeProfile);
            float directionSigned = (shapeProfile.directionBias - 0.5f) * 2f;
            bool ascendingWalk = directionSigned > 0.3f;
            bool descendingWalk = directionSigned < -0.3f;
            bool addFifth = shapeProfile.verticalSpan > 0.5f;
            bool busy = shapeProfile.pathLength > 0.68f;
            bool mediumDensity = !busy && (shapeProfile.pathLength > 0.36f || addFifth);
            int holdDuration = ResolveHoldDuration(shapeProfile.horizontalSpan, busy, mediumDensity);

            var notes = new List<MelodyNote>(bars * 4);
            for (int barIndex = 0; barIndex < bars; barIndex++)
            {
                var slot = progression.GetSlotForBar(barIndex) ?? progression.GetSlotForBar(0);
                if (slot == null)
                    continue;

                int barStart = barIndex * StepsPerBar;
                int rootMidi = RegisterPolicy.ClampBass(slot.rootMidi, GuidedGenreId);
                AddOrReplaceNote(notes, barStart, rootMidi, holdDuration, ResolveVelocity(0.58f, shapeProfile, soundProfile), 0f);

                if (busy)
                {
                    int reinforceStep = barStart + 4;
                    AddOrReplaceNote(notes, reinforceStep, rootMidi, 4, ResolveVelocity(0.5f, shapeProfile, soundProfile), 0f);
                }

                if (addFifth || mediumDensity || busy)
                {
                    int fifthMidi = RegisterPolicy.ClampBass(
                        MusicalKeys.QuantizeToKey(rootMidi + 7, keyName),
                        GuidedGenreId);
                    int beatThreeStep = barStart + 8;
                    AddOrReplaceNote(notes, beatThreeStep, fifthMidi, busy ? 4 : Mathf.Max(4, holdDuration / 2), ResolveVelocity(0.52f, shapeProfile, soundProfile), 0f);
                }

                if (busy && (ascendingWalk || descendingWalk))
                    AddWalkIntoNextBar(notes, progression, barIndex, keyName, ascendingWalk, shapeProfile, soundProfile);
            }

            FinalizeDurations(notes, totalSteps);

            string motionTag = ascendingWalk ? "ascending walk" : descendingWalk ? "descending walk" : "rooted";
            string densityTag = busy ? "walking eighths" : mediumDensity ? "root and fifth" : "root holds";
            return new MelodyDerivationResult
            {
                bars = bars,
                presetId = DefaultPresetId,
                tags = new List<string> { "bass", motionTag, densityTag },
                derivedSequence = new DerivedSequence
                {
                    kind = "bass",
                    totalSteps = totalSteps,
                    notes = notes
                },
                summary = $"{sizeWord} bass line, {bars} bars, beat-1 roots locked to the progression with {densityTag}.",
                details = "Direction bias steers upward or downward approach motion, vertical span adds the fifth on beat 3, horizontal span lengthens or shortens sustains, and path length opens denser bass movement while keeping bar-1 and bar-5 roots anchored."
            };
        }

        private static void AddWalkIntoNextBar(
            List<MelodyNote> notes,
            ChordProgression progression,
            int barIndex,
            string keyName,
            bool ascending,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile)
        {
            var currentSlot = progression.GetSlotForBar(barIndex);
            var nextSlot = progression.GetSlotForBar(barIndex + 1);
            if (currentSlot == null || nextSlot == null)
                return;

            int barStart = barIndex * StepsPerBar;
            int currentRoot = RegisterPolicy.ClampBass(currentSlot.rootMidi, GuidedGenreId);
            int nextRoot = RegisterPolicy.ClampBass(nextSlot.rootMidi, GuidedGenreId);
            int walkTarget = ResolveWalkTarget(currentRoot, nextRoot, keyName, ascending);
            bool useChromaticLead = ShouldUseChromaticLead(barIndex, shapeProfile, ascending);
            int direction = ascending ? 1 : -1;

            int leadStep = barStart + 14;
            int setupStep = barStart + 12;
            int leadMidi = useChromaticLead
                ? RegisterPolicy.ClampBass(walkTarget - direction, GuidedGenreId)
                : MoveByScaleDegrees(walkTarget, keyName, -direction);
            int setupMidi = MoveByScaleDegrees(leadMidi, keyName, -direction);

            AddOrReplaceNote(notes, setupStep, setupMidi, 2, ResolveVelocity(0.46f, shapeProfile, soundProfile), 0f);
            AddOrReplaceNote(notes, leadStep, leadMidi, 2, ResolveVelocity(0.5f, shapeProfile, soundProfile), 0f);
        }

        private static int ResolveWalkTarget(int currentRoot, int nextRoot, string keyName, bool ascending)
        {
            var range = RegisterPolicy.GetBassRange(GuidedGenreId);
            int target = nextRoot;

            if (ascending)
            {
                while (target <= currentRoot)
                    target += 12;

                if (target > range.max)
                    target = FindNearestInKey(range.max, keyName, -1);
            }
            else
            {
                while (target >= currentRoot)
                    target -= 12;

                if (target < range.min)
                    target = FindNearestInKey(range.min, keyName, 1);
            }

            return RegisterPolicy.ClampBass(target, GuidedGenreId);
        }

        private static bool ShouldUseChromaticLead(int barIndex, ShapeProfile shapeProfile, bool ascending)
        {
            if (!ascending)
                return false;

            return shapeProfile.angularity > 0.52f && (barIndex == 3 || barIndex == 7);
        }

        private static int ResolveHoldDuration(float horizontalSpan, bool busy, bool mediumDensity)
        {
            if (busy)
                return horizontalSpan > 0.62f ? 8 : 4;
            if (mediumDensity)
                return horizontalSpan > 0.62f ? 12 : 6;
            return horizontalSpan > 0.7f ? 16 : horizontalSpan > 0.4f ? 12 : 8;
        }

        private static float ResolveVelocity(float baseVelocity, ShapeProfile shapeProfile, SoundProfile soundProfile)
        {
            return Mathf.Clamp(
                baseVelocity +
                shapeProfile.angularity * 0.08f +
                shapeProfile.pathLength * 0.04f +
                soundProfile.body * 0.06f,
                0.32f,
                0.82f);
        }

        private static void AddOrReplaceNote(List<MelodyNote> notes, int step, int midi, int durationSteps, float velocity, float glide)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                if (notes[i].step != step)
                    continue;

                notes[i] = new MelodyNote
                {
                    step = step,
                    midi = midi,
                    durationSteps = durationSteps,
                    velocity = MathUtils.RoundTo(velocity, 2),
                    glide = MathUtils.RoundTo(glide, 2)
                };
                return;
            }

            notes.Add(new MelodyNote
            {
                step = step,
                midi = midi,
                durationSteps = durationSteps,
                velocity = MathUtils.RoundTo(velocity, 2),
                glide = MathUtils.RoundTo(glide, 2)
            });
        }

        private static void FinalizeDurations(List<MelodyNote> notes, int totalSteps)
        {
            notes.Sort((a, b) => a.step.CompareTo(b.step));
            for (int i = 0; i < notes.Count; i++)
            {
                int nextStep = i < notes.Count - 1 ? notes[i + 1].step : totalSteps;
                var note = notes[i];
                int maxDuration = Mathf.Max(2, nextStep - note.step);
                note.durationSteps = Mathf.Clamp(note.durationSteps, 2, maxDuration);
                notes[i] = note;
            }
        }

        private static int MoveByScaleDegrees(int midi, string keyName, int degreeSteps)
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

            return RegisterPolicy.ClampBass(current, GuidedGenreId);
        }

        private static int FindNearestInKey(int startMidi, string keyName, int direction)
        {
            int current = startMidi;
            while (MusicalKeys.QuantizeToKey(current, keyName) != current)
                current += direction >= 0 ? 1 : -1;
            return current;
        }

        private static ChordProgression CreateFallbackProgression(string keyName)
        {
            var harmonicContext = HarmonicContextProvider.Current;
            int rootMidi = harmonicContext != null ? harmonicContext.rootMidi : MusicalKeys.Get(keyName).rootMidi;
            string flavor = harmonicContext != null && !string.IsNullOrEmpty(harmonicContext.flavor)
                ? harmonicContext.flavor
                : "major";
            var voicing = harmonicContext != null && harmonicContext.HasChord
                ? new List<int>(harmonicContext.chordTones)
                : MusicalKeys.BuildScaleChord(rootMidi, keyName, new[] { 0, 2, 4 });

            var progression = new ChordProgression
            {
                bars = GuidedDefaults.Bars,
                chords = new List<ChordSlot>()
            };

            for (int barIndex = 0; barIndex < GuidedDefaults.Bars; barIndex++)
            {
                progression.chords.Add(new ChordSlot
                {
                    barIndex = barIndex,
                    rootMidi = rootMidi,
                    flavor = flavor,
                    voicing = new List<int>(voicing)
                });
            }

            return progression;
        }
    }
}
