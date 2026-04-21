using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    /// <summary>
    /// Register (MIDI range) policy per mode and genre. Keeps rhythm/melody/harmony from
    /// colliding in the same frequency band, regardless of how large or tall the user's shape is.
    /// When a derived pitch is out of range, octave-shift until it fits — the note stays in-scale.
    /// </summary>
    public static class RegisterPolicy
    {
        // MIDI numbers: 36=C2, 48=C3, 60=C4, 72=C5, 84=C6
        // Tuples: (minMidi, maxMidi) inclusive.

        // Electronic
        private const int Elec_BassMin = 28,  Elec_BassMax = 52;  // E1..E3
        private const int Elec_HarmMin = 48,  Elec_HarmMax = 69;  // C3..A4
        private const int Elec_MelMin  = 57,  Elec_MelMax  = 84;  // A3..C6

        // NewAge
        private const int NA_BassMin = 33,  NA_BassMax = 52;      // A1..E3
        private const int NA_HarmMin = 48,  NA_HarmMax = 67;      // C3..G4
        private const int NA_MelMin  = 67,  NA_MelMax  = 88;      // G4..E6

        // Jazz
        private const int Jz_BassMin = 33,  Jz_BassMax = 52;      // A1..E3
        private const int Jz_HarmMin = 48,  Jz_HarmMax = 69;      // C3..A4
        private const int Jz_MelMin  = 64,  Jz_MelMax  = 88;      // E4..E6

        public static (int min, int max) GetRange(PatternType mode, string genreId)
        {
            bool newAge = genreId == "newage";
            bool jazz   = genreId == "jazz";

            switch (PatternTypeCompatibility.Canonicalize(mode))
            {
                case PatternType.Melody:
                case PatternType.Bass:
                case PatternType.Groove:
                    if (newAge) return (NA_MelMin,  NA_MelMax);
                    if (jazz)   return (Jz_MelMin,  Jz_MelMax);
                    return (Elec_MelMin, Elec_MelMax);

                case PatternType.Harmony:
                    if (newAge) return (NA_HarmMin, NA_HarmMax);
                    if (jazz)   return (Jz_HarmMin, Jz_HarmMax);
                    return (Elec_HarmMin, Elec_HarmMax);

                default:
                    if (newAge) return (NA_BassMin, NA_BassMax);
                    if (jazz)   return (Jz_BassMin, Jz_BassMax);
                    return (Elec_BassMin, Elec_BassMax);
            }
        }

        public static (int min, int max) GetBassRange(string genreId)
        {
            bool newAge = genreId == "newage";
            bool jazz   = genreId == "jazz";

            if (newAge) return (NA_BassMin, NA_BassMax);
            if (jazz)   return (Jz_BassMin, Jz_BassMax);
            return (Elec_BassMin, Elec_BassMax);
        }

        /// <summary>
        /// Transpose midi up/down by whole octaves until it lies within [min, max].
        /// Preserves pitch class so the result stays in the same key/scale.
        /// </summary>
        public static int Clamp(int midi, PatternType mode, string genreId)
        {
            var (min, max) = GetRange(mode, genreId);
            return ClampToRange(midi, min, max);
        }

        public static int ClampBass(int midi, string genreId)
        {
            var (min, max) = GetBassRange(genreId);
            return ClampToRange(midi, min, max);
        }

        private static int ClampToRange(int midi, int min, int max)
        {
            while (midi < min) midi += 12;
            while (midi > max) midi -= 12;
            if (midi < min) midi = min;
            return midi;
        }
    }
}
