using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Core.Sequencing.NewAge
{
    /// <summary>
    /// New Age harmony deriver: open, spacious voicings (sus2/sus4/5ths), long sustained drones.
    /// Tilt → modal color (light vs. dark). Horizontal span → voicing width. Size → bars.
    /// </summary>
    public sealed class NewAgeHarmonyDeriver : IHarmonyDeriver
    {
        public HarmonyDerivationResult Derive(
            StrokeCurve curve,
            StrokeMetrics metrics,
            string keyName,
            ShapeProfile sp,
            SoundProfile sound,
            GenreProfile genre)
        {
            float sizeFactor = ShapeProfileSizing.GetSizeFactor(PatternType.HarmonyPad, sp);
            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.HarmonyPad, sp);
            // New Age always holds longer — 4 or 8 bars
            int bars = metrics.length > 0.70f ? 8 : 4;
            int totalSteps = bars * AppStateFactory.BarSteps;
            string presetId = genre.GetDefaultPresetId(PatternType.HarmonyPad);

            int rootMidi = PitchUtils.PitchFromRelative(1f - sp.centroidHeight, keyName) - 12;
            rootMidi = MusicalKeys.QuantizeToKey(rootMidi, keyName);

            var role = ShapeRoleProvider.Current;

            List<int> chord;
            string flavor;

            if (role.index >= 2)
            {
                // Role 2+: pure bass pedal — root two octaves down, no voicing above it
                flavor = "bass-pedal";
                int pedalMidi = RegisterPolicy.ClampBass(rootMidi - 12, "newage");
                chord = new List<int> { pedalMidi };
            }
            else if (role.index == 1)
            {
                // Role 1: root + 5th drone one octave below the primary — never duplicates role 0's top voice
                flavor = "root5";
                int bassRoot = RegisterPolicy.ClampBass(rootMidi - 12, "newage");
                int fifth    = RegisterPolicy.ClampBass(MusicalKeys.QuantizeToKey(bassRoot + 7, keyName), "newage");
                chord = new List<int> { bassRoot, fifth };
            }
            else
            {
                // Role 0: full sus voicing (existing behaviour)
                int[] scaleDegreeSteps;
                if (sp.tiltSigned > 0.22f)
                {
                    flavor = "sus2";
                    scaleDegreeSteps = new[] { 0, 1, 4, 7 };
                }
                else if (sp.tiltSigned < -0.18f)
                {
                    flavor = "sus4";
                    scaleDegreeSteps = new[] { 0, 3, 4, 7 };
                }
                else
                {
                    flavor = "drone5";
                    scaleDegreeSteps = new[] { 0, 4, 7, 11 };
                }

                chord = MusicalKeys.BuildScaleChord(rootMidi, keyName, scaleDegreeSteps);

                int spread = Mathf.RoundToInt(sp.horizontalSpan * 8f + sizeFactor * 4f);
                if (chord.Count > 0)
                    chord[chord.Count - 1] += Mathf.RoundToInt(spread * 0.3f) / 12 * 12;

                if (sizeFactor > 0.55f || sp.horizontalSpan > 0.6f)
                    chord.Insert(0, rootMidi - 12);

                // Register-clamp each note
                for (int n = 0; n < chord.Count; n++)
                    chord[n] = RegisterPolicy.Clamp(chord[n], PatternType.HarmonyPad, "newage");
            }

            string depthWord = sound.reverbBias > 0.5f ? "deep bloom" : "open space";
            return new HarmonyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { flavor, depthWord, "meditative" },
                derivedSequence = new DerivedSequence
                {
                    kind = "harmony",
                    totalSteps = totalSteps,
                    flavor = flavor,
                    rootMidi = rootMidi,
                    chord = chord
                },
                summary = $"{sizeWord} {flavor} drone, {bars} bars, open {Mathf.Round(sp.horizontalSpan * 100f)}% spread.",
                details = "Open voicings avoid dissonance. Tilt steers modal flavor, size gives breath length, and horizontal span opens the overtone space."
            };
        }
    }
}
