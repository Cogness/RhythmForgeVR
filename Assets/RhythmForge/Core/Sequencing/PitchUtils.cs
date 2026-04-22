using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    public static class PitchUtils
    {
        public static float MidiToFrequency(int midi)
        {
            return 440f * Mathf.Pow(2f, (midi - 69f) / 12f);
        }

        public static int PitchFromRelative(float relative, string keyName)
        {
            return PitchFromRelative(relative, keyName, null);
        }

        /// <summary>
        /// Maps a 0..1 vertical position to a MIDI pitch drawn from <paramref name="scaleIntervals"/>
        /// relative to the key's root. When <paramref name="scaleIntervals"/> is null the key's
        /// diatonic scale is used (original behavior).
        /// </summary>
        public static int PitchFromRelative(float relative, string keyName, int[] scaleIntervals)
        {
            var key = MusicalKeys.Get(keyName);
            int[] scale = scaleIntervals != null && scaleIntervals.Length > 0 ? scaleIntervals : key.scale;
            float bounded = Mathf.Clamp(relative, 0f, 0.999f);
            int stepsAcross = scale.Length * 2;
            int degreeIndex = Mathf.FloorToInt((1f - bounded) * stepsAcross);
            int octave = degreeIndex / scale.Length;
            int degree = degreeIndex % scale.Length;
            return key.rootMidi + scale[degree] + octave * 12;
        }

        /// <summary>
        /// Snaps <paramref name="midi"/> to the nearest pitch class from <paramref name="scaleIntervals"/>
        /// relative to the key's root. When <paramref name="scaleIntervals"/> is null this falls back
        /// to <see cref="MusicalKeys.QuantizeToKey"/>.
        /// </summary>
        public static int QuantizeToScale(int midi, string keyName, int[] scaleIntervals)
        {
            if (scaleIntervals == null || scaleIntervals.Length == 0)
                return MusicalKeys.QuantizeToKey(midi, keyName);

            var key = MusicalKeys.Get(keyName);
            int rootClass = ((key.rootMidi % 12) + 12) % 12;

            int midiClass = ((midi % 12) + 12) % 12;

            int bestClass = (rootClass + scaleIntervals[0]) % 12;
            int bestDist = int.MaxValue;
            for (int i = 0; i < scaleIntervals.Length; i++)
            {
                int kc = (rootClass + scaleIntervals[i]) % 12;
                int dist = System.Math.Abs(kc - midiClass);
                if (dist > 6) dist = 12 - dist;
                if (dist < bestDist) { bestDist = dist; bestClass = kc; }
            }

            int octaveBase = midi - midiClass;
            int candidate = octaveBase + bestClass;
            if (candidate - midi > 6)  candidate -= 12;
            if (midi - candidate > 6)  candidate += 12;
            return candidate;
        }
    }
}
