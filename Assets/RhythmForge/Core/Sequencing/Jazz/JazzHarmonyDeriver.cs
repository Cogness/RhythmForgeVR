using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Core.Sequencing.Jazz
{
    /// <summary>
    /// Jazz harmony deriver: 7th, 9th, 13th chord voicings with ii-V-I flavors.
    /// Tilt → chord quality (maj7/min7/dom7/dim7). Horizontal span → voicing spread.
    /// Circularity → smoother block chord vs. angular for sus/altered.
    /// </summary>
    public sealed class JazzHarmonyDeriver : IHarmonyDeriver
    {
        public HarmonyDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            ShapeProfile sp,
            SoundProfile sound,
            GenreProfile genre)
        {
            float sizeFactor = ShapeProfileSizing.GetSizeFactor(PatternType.HarmonyPad, sp);
            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.HarmonyPad, sp);
            int bars = metrics.length > 1.20f ? 8 : metrics.length > 0.70f ? 4 : 2;
            int totalSteps = bars * AppStateFactory.BarSteps;
            string presetId = genre.GetDefaultPresetId(PatternType.HarmonyPad);

            int rootMidi = PitchUtils.PitchFromRelative(1f - sp.centroidHeight, keyName) - 12;
            rootMidi = MusicalKeys.QuantizeToKey(rootMidi, keyName);

            var role = ShapeRoleProvider.Current;
            List<int> chord;
            string flavor;

            if (role.index >= 2)
            {
                // Role 2+: bass pedal only
                flavor = "bass-pedal";
                int pedalMidi = RegisterPolicy.ClampBass(rootMidi - 12, "jazz");
                chord = new List<int> { pedalMidi };
            }
            else if (role.index == 1)
            {
                // Role 1: root+5th one octave below — no upper voice duplication
                flavor = "root5";
                int bassRoot = RegisterPolicy.ClampBass(rootMidi - 12, "jazz");
                int fifth    = RegisterPolicy.ClampBass(MusicalKeys.QuantizeToKey(bassRoot + 7, keyName), "jazz");
                chord = new List<int> { bassRoot, fifth };
            }
            else
            {
                // Role 0: full jazz chord voicing (existing)
                int[] scaleDegreeSteps;

                if (sp.tiltSigned > 0.35f)
                {
                    flavor = "maj7";
                    scaleDegreeSteps = new[] { 0, 2, 4, 6 };
                }
                else if (sp.tiltSigned > 0.1f)
                {
                    flavor = "dom7";
                    scaleDegreeSteps = new[] { 0, 2, 4, 6 };
                }
                else if (sp.tiltSigned < -0.3f)
                {
                    flavor = "dim7";
                    scaleDegreeSteps = new[] { 0, 2, 4, 5 };
                }
                else if (sp.circularity > 0.65f)
                {
                    flavor = "min7";
                    scaleDegreeSteps = new[] { 0, 2, 4, 6 };
                }
                else
                {
                    flavor = "min9";
                    scaleDegreeSteps = new[] { 0, 2, 4, 6, 1 };
                }

                chord = MusicalKeys.BuildScaleChord(rootMidi, keyName, scaleDegreeSteps);

                int spread = Mathf.RoundToInt(sp.horizontalSpan * 12f + sizeFactor * 6f);
                if (chord.Count > 2)
                    chord[2] += Mathf.RoundToInt(spread * 0.5f) / 12 * 12;
                if (chord.Count > 3)
                    chord[3] += (spread > 5 ? 12 : 0);

                if (sp.horizontalSpan > 0.6f || sizeFactor > 0.58f)
                    chord.Insert(0, rootMidi - 12);

                for (int n = 0; n < chord.Count; n++)
                    chord[n] = RegisterPolicy.Clamp(chord[n], PatternType.HarmonyPad, "jazz");
            }

            string tensionWord = flavor == "dim7" || flavor == "dom7" ? "tension" : "stable";
            return new HarmonyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { flavor, tensionWord, "jazz voicing" },
                derivedSequence = new DerivedSequence
                {
                    kind = "harmony",
                    totalSteps = totalSteps,
                    flavor = flavor,
                    rootMidi = rootMidi,
                    chord = chord
                },
                summary = $"{sizeWord} {flavor} voicing, {bars} bars, {Mathf.Round(sp.horizontalSpan * 100f)}% spread.",
                details = "Tilt controls chord family (maj7→dom7→min7→dim7). Horizontal span opens the voicing, size pushes the comping duration."
            };
        }
    }
}
