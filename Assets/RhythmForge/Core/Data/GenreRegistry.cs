using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Sequencing;
using RhythmForge.Core.Sequencing.Electronic;
using RhythmForge.Core.Sequencing.NewAge;
using RhythmForge.Core.Sequencing.Jazz;

namespace RhythmForge.Core.Data
{
    /// <summary>
    /// Static registry holding all registered GenreProfiles and the currently active genre.
    /// Call SetActive() to switch genre. Subscribe to the session event bus for change notifications.
    /// </summary>
    public static class GenreRegistry
    {
        private static readonly Dictionary<string, GenreProfile> _profiles = new Dictionary<string, GenreProfile>();
        private static GenreProfile _active;

        static GenreRegistry()
        {
            Register(CreateElectronic());
            Register(CreateNewAge());
            Register(CreateJazz());
            _active = _profiles["electronic"];
        }

        public static void Register(GenreProfile profile)
        {
            if (profile != null)
                _profiles[profile.Id] = profile;
        }

        public static GenreProfile GetActive() => _active;

        public static GenreProfile Get(string id)
        {
            if (_profiles.TryGetValue(id, out var profile))
                return profile;
            return _active;
        }

        public static GenreProfile Get(MusicalGenre genre)
        {
            foreach (var p in _profiles.Values)
            {
                if (p.Genre == genre)
                    return p;
            }
            return _active;
        }

        public static void SetActive(string id)
        {
            if (_profiles.TryGetValue(id, out var profile))
                _active = profile;
        }

        public static void SetActive(MusicalGenre genre)
        {
            SetActive(Get(genre).Id);
        }

        public static IEnumerable<GenreProfile> All => _profiles.Values;

        // ──────────────────────────────────────────────────────────────
        //  Electronic genre definition (merges Lo-Fi, Trap, Dream)
        // ──────────────────────────────────────────────────────────────

        private static GenreProfile CreateElectronic()
        {
            var presets = new List<InstrumentPreset>
            {
                new InstrumentPreset("lofi-drums",  "Lo-Fi Drums",  "lofi-drums",  "electronic", 0.18f, 0.005f, 0.3f),
                new InstrumentPreset("lofi-piano",  "Lo-Fi Piano",  "lofi-piano",  "electronic", 0.22f, 0.01f,  0.6f),
                new InstrumentPreset("lofi-pad",    "Lo-Fi Pad",    "lofi-pad",    "electronic", 0.28f, 0.4f,   1.8f),
                new InstrumentPreset("trap-drums",  "Trap Drums",   "trap-drums",  "electronic", 0.14f, 0.003f, 0.2f),
                new InstrumentPreset("trap-bass",   "Trap Bass",    "trap-bass",   "electronic", 0.1f,  0.008f, 0.34f),
                new InstrumentPreset("trap-pad",    "Trap Pad",     "trap-pad",    "electronic", 0.2f,  0.3f,   1.4f),
                new InstrumentPreset("dream-perc",  "Dream Perc",   "dream-perc",  "electronic", 0.32f, 0.01f,  0.5f),
                new InstrumentPreset("dream-bell",  "Dream Bell",   "dream-bell",  "electronic", 0.34f, 0.006f, 0.8f),
                new InstrumentPreset("dream-pad",   "Dream Pad",    "dream-pad",   "electronic", 0.4f,  0.6f,   2.4f),
            };

            var defaultPresets = new Dictionary<PatternType, string>
            {
                { PatternType.RhythmLoop, "lofi-drums" },
                { PatternType.MelodyLine, "lofi-piano" },
                { PatternType.HarmonyPad, "lofi-pad"   },
            };

            var soundMappings = new Dictionary<PatternType, PatternSoundMappingProfile>
            {
                { PatternType.RhythmLoop, PatternSoundMappingProfile.CreateRhythmDefaults()  },
                { PatternType.MelodyLine, PatternSoundMappingProfile.CreateMelodyDefaults()  },
                { PatternType.HarmonyPad, PatternSoundMappingProfile.CreateHarmonyDefaults() },
            };

            return new GenreProfile(
                genre: MusicalGenre.Electronic,
                id: "electronic",
                displayName: "Electronic",
                description: "Lo-Fi, Trap & Dream synthesis",
                defaultTempo: 85f,
                defaultKey: "A minor",
                busFx: new GroupBusFx { reverb = 0.24f, delay = 0.16f },
                colorPalette: new PatternColorPalette
                {
                    rhythmLoop = ProfileColorUtility.HexColor("#51d7ff"),
                    melodyLine = ProfileColorUtility.HexColor("#f7c975"),
                    harmonyPad = ProfileColorUtility.HexColor("#62f3d3")
                },
                presets: presets,
                defaultPresetIds: defaultPresets,
                soundMappings: soundMappings,
                rhythmDeriver:  new ElectronicRhythmDeriver(),
                melodyDeriver:  new ElectronicMelodyDeriver(),
                harmonyDeriver: new ElectronicHarmonyDeriver());
        }

        // ──────────────────────────────────────────────────────────────
        //  New Age genre definition
        // ──────────────────────────────────────────────────────────────

        private static GenreProfile CreateNewAge()
        {
            var presets = new List<InstrumentPreset>
            {
                new InstrumentPreset("newage-bowl",    "Singing Bowl",  "newage-bowl",    "newage", 0.42f, 0.05f, 1.8f),
                new InstrumentPreset("newage-kalimba", "Kalimba",       "newage-kalimba", "newage", 0.28f, 0.006f, 0.35f),
                new InstrumentPreset("newage-drone",   "Drone Pad",     "newage-drone",   "newage", 0.52f, 0.45f, 2.2f),
            };

            var defaultPresets = new Dictionary<PatternType, string>
            {
                { PatternType.RhythmLoop, "newage-bowl"    },
                { PatternType.MelodyLine, "newage-kalimba" },
                { PatternType.HarmonyPad, "newage-drone"   },
            };

            var soundMappings = new Dictionary<PatternType, PatternSoundMappingProfile>
            {
                { PatternType.RhythmLoop, CreateNewAgeRhythmMapping()  },
                { PatternType.MelodyLine, CreateNewAgeMelodyMapping()  },
                { PatternType.HarmonyPad, CreateNewAgeHarmonyMapping() },
            };

            return new GenreProfile(
                genre: MusicalGenre.NewAge,
                id: "newage",
                displayName: "New Age",
                description: "Meditative bowls, kalimba & drone pads",
                defaultTempo: 68f,
                defaultKey: "C major",
                busFx: new GroupBusFx { reverb = 0.65f, delay = 0.30f },
                colorPalette: new PatternColorPalette
                {
                    rhythmLoop = ProfileColorUtility.HexColor("#a8d8b9"),
                    melodyLine = ProfileColorUtility.HexColor("#e8d4a2"),
                    harmonyPad = ProfileColorUtility.HexColor("#c5b3e6")
                },
                presets: presets,
                defaultPresetIds: defaultPresets,
                soundMappings: soundMappings,
                rhythmDeriver:  new NewAgeRhythmDeriver(),
                melodyDeriver:  new NewAgeMelodyDeriver(),
                harmonyDeriver: new NewAgeHarmonyDeriver());
        }

        private static PatternSoundMappingProfile CreateNewAgeRhythmMapping()
        {
            return new PatternSoundMappingProfile
            {
                brightness         = new SoundMetricWeights { constant = 0.08f, circularity = 0.14f, smoothness = 0.18f },
                resonance          = new SoundMetricWeights { constant = 0.28f, circularity = 0.32f, sizeFactor = 0.16f },
                drive              = new SoundMetricWeights { constant = 0.03f, angularity = 0.12f },
                attackBias         = new SoundMetricWeights { constant = 0.08f, smoothness = 0.22f, compactness = 0.14f },
                releaseBias        = new SoundMetricWeights { constant = 0.38f, circularity = 0.28f, sizeFactor = 0.44f },
                detune             = new SoundMetricWeights { constant = 0.04f, instability = 0.08f },
                modDepth           = new SoundMetricWeights { constant = 0.12f, circularity = 0.24f, sizeFactor = 0.22f },
                stereoSpread       = new SoundMetricWeights { constant = 0.18f, sizeFactor = 0.34f, horizontalSpan = 0.16f },
                grooveInstability  = new SoundMetricWeights { instability = 0.14f },
                delayBias          = new SoundMetricWeights { constant = 0.08f, sizeFactor = 0.18f },
                reverbBias         = new SoundMetricWeights { constant = 0.36f, circularity = 0.22f, sizeFactor = 0.42f },
                waveMorph          = new SoundMetricWeights { constant = 0.12f, smoothness = 0.18f },
                filterMotion       = new SoundMetricWeights { constant = 0.06f, instability = 0.12f, sizeFactor = 0.14f },
                transientSharpness = new SoundMetricWeights { constant = 0.08f, angularity = 0.16f, compactness = 0.1f },
                body               = new SoundMetricWeights { constant = 0.24f, circularity = 0.22f, sizeFactor = 0.54f }
            };
        }

        private static PatternSoundMappingProfile CreateNewAgeMelodyMapping()
        {
            return new PatternSoundMappingProfile
            {
                brightness         = new SoundMetricWeights { constant = 0.14f, centroidHeight = 0.18f, smoothness = 0.14f },
                resonance          = new SoundMetricWeights { constant = 0.18f, smoothness = 0.22f, sizeFactor = 0.14f },
                drive              = new SoundMetricWeights { constant = 0.03f, angularity = 0.08f },
                attackBias         = new SoundMetricWeights { constant = 0.06f, smoothness = 0.28f, sizeFactor = 0.14f },
                releaseBias        = new SoundMetricWeights { constant = 0.22f, smoothness = 0.28f, sizeFactor = 0.54f },
                detune             = new SoundMetricWeights { constant = 0.06f, curvatureVariance = 0.12f },
                modDepth           = new SoundMetricWeights { constant = 0.14f, curvatureMean = 0.16f, sizeFactor = 0.32f },
                stereoSpread       = new SoundMetricWeights { constant = 0.12f, horizontalSpan = 0.22f, sizeFactor = 0.44f },
                grooveInstability  = new SoundMetricWeights { constant = 0.02f, speedVariance = 0.08f },
                delayBias          = new SoundMetricWeights { constant = 0.08f, sizeFactor = 0.22f },
                reverbBias         = new SoundMetricWeights { constant = 0.28f, smoothness = 0.22f, sizeFactor = 0.44f },
                waveMorph          = new SoundMetricWeights { constant = 0.08f, smoothness = 0.22f },
                filterMotion       = new SoundMetricWeights { constant = 0.08f, curvatureMean = 0.18f, sizeFactor = 0.28f },
                transientSharpness = new SoundMetricWeights { constant = 0.06f, angularity = 0.14f, compactness = 0.12f },
                body               = new SoundMetricWeights { constant = 0.16f, smoothness = 0.24f, sizeFactor = 0.56f }
            };
        }

        private static PatternSoundMappingProfile CreateNewAgeHarmonyMapping()
        {
            return new PatternSoundMappingProfile
            {
                brightness         = new SoundMetricWeights { constant = 0.10f, centroidHeight = 0.12f, smoothness = 0.16f },
                resonance          = new SoundMetricWeights { constant = 0.22f, circularity = 0.28f, sizeFactor = 0.18f },
                drive              = new SoundMetricWeights { constant = 0.02f, angularity = 0.06f },
                attackBias         = new SoundMetricWeights { constant = 0.04f, smoothness = 0.32f, sizeFactor = 0.22f },
                releaseBias        = new SoundMetricWeights { constant = 0.32f, smoothness = 0.24f, sizeFactor = 0.62f },
                detune             = new SoundMetricWeights { constant = 0.12f, symmetryInverse = 0.22f, sizeFactor = 0.36f },
                modDepth           = new SoundMetricWeights { constant = 0.18f, smoothness = 0.22f, sizeFactor = 0.38f },
                stereoSpread       = new SoundMetricWeights { constant = 0.22f, horizontalSpan = 0.24f, sizeFactor = 0.54f },
                grooveInstability  = new SoundMetricWeights { constant = 0.01f, curvatureVariance = 0.06f },
                delayBias          = new SoundMetricWeights { constant = 0.12f, sizeFactor = 0.22f },
                reverbBias         = new SoundMetricWeights { constant = 0.44f, smoothness = 0.18f, sizeFactor = 0.58f },
                waveMorph          = new SoundMetricWeights { constant = 0.22f, smoothness = 0.28f },
                filterMotion       = new SoundMetricWeights { constant = 0.18f, circularity = 0.22f, sizeFactor = 0.38f },
                transientSharpness = new SoundMetricWeights { constant = 0.03f, angularity = 0.1f },
                body               = new SoundMetricWeights { constant = 0.22f, smoothness = 0.18f, sizeFactor = 0.64f }
            };
        }

        // ──────────────────────────────────────────────────────────────
        //  Jazz genre definition
        // ──────────────────────────────────────────────────────────────

        private static GenreProfile CreateJazz()
        {
            var presets = new List<InstrumentPreset>
            {
                new InstrumentPreset("jazz-brush",  "Brush Kit",   "jazz-brush",  "jazz", 0.18f, 0.003f, 0.26f),
                new InstrumentPreset("jazz-rhodes", "Rhodes",      "jazz-rhodes", "jazz", 0.24f, 0.008f, 0.54f),
                new InstrumentPreset("jazz-comp",   "Jazz Comp",   "jazz-comp",   "jazz", 0.28f, 0.4f,   1.6f),
            };

            var defaultPresets = new Dictionary<PatternType, string>
            {
                { PatternType.RhythmLoop, "jazz-brush"  },
                { PatternType.MelodyLine, "jazz-rhodes" },
                { PatternType.HarmonyPad, "jazz-comp"   },
            };

            var soundMappings = new Dictionary<PatternType, PatternSoundMappingProfile>
            {
                { PatternType.RhythmLoop, CreateJazzRhythmMapping()  },
                { PatternType.MelodyLine, CreateJazzMelodyMapping()  },
                { PatternType.HarmonyPad, CreateJazzHarmonyMapping() },
            };

            return new GenreProfile(
                genre: MusicalGenre.Jazz,
                id: "jazz",
                displayName: "Jazz",
                description: "Brush kit, Rhodes & jazz chord voicings",
                defaultTempo: 110f,
                defaultKey: "D minor",
                busFx: new GroupBusFx { reverb = 0.32f, delay = 0.18f },
                colorPalette: new PatternColorPalette
                {
                    rhythmLoop = ProfileColorUtility.HexColor("#e8a87c"),
                    melodyLine = ProfileColorUtility.HexColor("#d4a76a"),
                    harmonyPad = ProfileColorUtility.HexColor("#8ba4b8")
                },
                presets: presets,
                defaultPresetIds: defaultPresets,
                soundMappings: soundMappings,
                rhythmDeriver:  new JazzRhythmDeriver(),
                melodyDeriver:  new JazzMelodyDeriver(),
                harmonyDeriver: new JazzHarmonyDeriver());
        }

        private static PatternSoundMappingProfile CreateJazzRhythmMapping()
        {
            return new PatternSoundMappingProfile
            {
                brightness         = new SoundMetricWeights { constant = 0.18f, angularity = 0.28f, instability = 0.12f },
                resonance          = new SoundMetricWeights { constant = 0.16f, circularity = 0.18f, curvatureVariance = 0.14f },
                drive              = new SoundMetricWeights { constant = 0.14f, angularity = 0.36f, symmetryInverse = 0.14f },
                attackBias         = new SoundMetricWeights { constant = 0.22f, angularity = 0.42f, compactness = 0.2f },
                releaseBias        = new SoundMetricWeights { constant = 0.14f, smoothness = 0.22f, sizeFactor = 0.44f },
                detune             = new SoundMetricWeights { constant = 0.04f, instability = 0.16f },
                modDepth           = new SoundMetricWeights { constant = 0.08f, instability = 0.22f, sizeFactor = 0.16f },
                stereoSpread       = new SoundMetricWeights { constant = 0.1f, sizeFactor = 0.22f },
                grooveInstability  = new SoundMetricWeights { instability = 0.62f, sizeFactor = 0.22f },
                delayBias          = new SoundMetricWeights { constant = 0.04f, instability = 0.12f },
                reverbBias         = new SoundMetricWeights { constant = 0.08f, circularity = 0.1f, sizeFactor = 0.28f },
                waveMorph          = new SoundMetricWeights { constant = 0.12f, angularity = 0.32f },
                filterMotion       = new SoundMetricWeights { constant = 0.1f, instability = 0.18f, sizeFactor = 0.16f },
                transientSharpness = new SoundMetricWeights { constant = 0.22f, angularity = 0.48f, compactness = 0.18f },
                body               = new SoundMetricWeights { constant = 0.14f, circularity = 0.2f, sizeFactor = 0.54f }
            };
        }

        private static PatternSoundMappingProfile CreateJazzMelodyMapping()
        {
            return new PatternSoundMappingProfile
            {
                brightness         = new SoundMetricWeights { constant = 0.16f, angularity = 0.22f, centroidHeight = 0.14f, compactness = 0.22f },
                resonance          = new SoundMetricWeights { constant = 0.12f, curvatureMean = 0.28f, curvatureVariance = 0.12f },
                drive              = new SoundMetricWeights { constant = 0.12f, angularity = 0.32f, speedVariance = 0.14f },
                attackBias         = new SoundMetricWeights { constant = 0.18f, speedVariance = 0.36f, angularity = 0.18f },
                releaseBias        = new SoundMetricWeights { constant = 0.1f, smoothness = 0.2f, sizeFactor = 0.52f },
                detune             = new SoundMetricWeights { constant = 0.04f, curvatureVariance = 0.18f },
                modDepth           = new SoundMetricWeights { constant = 0.06f, curvatureMean = 0.22f, sizeFactor = 0.28f },
                stereoSpread       = new SoundMetricWeights { constant = 0.08f, horizontalSpan = 0.16f, sizeFactor = 0.38f },
                grooveInstability  = new SoundMetricWeights { constant = 0.08f, speedVariance = 0.28f },
                delayBias          = new SoundMetricWeights { constant = 0.06f, sizeFactor = 0.2f },
                reverbBias         = new SoundMetricWeights { constant = 0.1f, smoothness = 0.14f, sizeFactor = 0.34f },
                waveMorph          = new SoundMetricWeights { constant = 0.12f, angularity = 0.42f },
                filterMotion       = new SoundMetricWeights { constant = 0.12f, curvatureMean = 0.24f, sizeFactor = 0.28f },
                transientSharpness = new SoundMetricWeights { constant = 0.18f, angularity = 0.42f, speedVariance = 0.14f, compactness = 0.22f },
                body               = new SoundMetricWeights { constant = 0.12f, smoothness = 0.18f, sizeFactor = 0.56f }
            };
        }

        private static PatternSoundMappingProfile CreateJazzHarmonyMapping()
        {
            return new PatternSoundMappingProfile
            {
                brightness         = new SoundMetricWeights { constant = 0.14f, centroidHeight = 0.16f, tiltSignedAbs = 0.14f, compactness = 0.2f },
                resonance          = new SoundMetricWeights { constant = 0.12f, tiltSignedAbs = 0.26f, symmetryInverse = 0.12f },
                drive              = new SoundMetricWeights { constant = 0.08f, angularity = 0.22f, symmetryInverse = 0.1f },
                attackBias         = new SoundMetricWeights { constant = 0.12f, angularity = 0.26f, compactness = 0.18f },
                releaseBias        = new SoundMetricWeights { constant = 0.14f, smoothness = 0.18f, sizeFactor = 0.52f },
                detune             = new SoundMetricWeights { constant = 0.06f, symmetryInverse = 0.2f, horizontalSpan = 0.1f },
                modDepth           = new SoundMetricWeights { constant = 0.1f, tiltSignedAbs = 0.2f, sizeFactor = 0.32f },
                stereoSpread       = new SoundMetricWeights { constant = 0.12f, horizontalSpan = 0.18f, sizeFactor = 0.48f },
                grooveInstability  = new SoundMetricWeights { constant = 0.04f, curvatureVariance = 0.14f },
                delayBias          = new SoundMetricWeights { constant = 0.06f, tiltSignedAbs = 0.08f, sizeFactor = 0.14f },
                reverbBias         = new SoundMetricWeights { constant = 0.10f, smoothness = 0.14f, sizeFactor = 0.44f },
                waveMorph          = new SoundMetricWeights { constant = 0.14f, angularity = 0.2f, tiltSignedAbs = 0.12f },
                filterMotion       = new SoundMetricWeights { constant = 0.14f, tiltSignedAbs = 0.26f, symmetryInverse = 0.1f, sizeFactor = 0.34f },
                transientSharpness = new SoundMetricWeights { constant = 0.08f, angularity = 0.18f, compactness = 0.1f },
                body               = new SoundMetricWeights { constant = 0.16f, smoothness = 0.16f, sizeFactor = 0.62f }
            };
        }
    }
}
