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
            var key = MusicalKeys.Get(keyName);
            float bounded = Mathf.Clamp(relative, 0f, 0.999f);
            int stepsAcross = key.scale.Length * 2;
            int degreeIndex = Mathf.FloorToInt((1f - bounded) * stepsAcross);
            int octave = degreeIndex / key.scale.Length;
            int degree = degreeIndex % key.scale.Length;
            return key.rootMidi + key.scale[degree] + octave * 12;
        }
    }
}
