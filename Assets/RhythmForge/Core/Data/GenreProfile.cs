using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Core.Data
{
    /// <summary>
    /// All configuration that defines a musical genre: instruments, sound mappings,
    /// derivation strategies, bus FX, visual color palette, and tempo/key defaults.
    /// </summary>
    public class GenreProfile
    {
        public MusicalGenre Genre { get; }
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public float DefaultTempo { get; }
        public string DefaultKey { get; }
        public GroupBusFx BusFx { get; }
        public PatternColorPalette ColorPalette { get; }

        private readonly List<InstrumentPreset> _presets;
        private readonly Dictionary<PatternType, string> _defaultPresetIds;
        private readonly Dictionary<PatternType, PatternSoundMappingProfile> _soundMappings;

        public IRhythmDeriver RhythmDeriver { get; }
        public IMelodyDeriver MelodyDeriver { get; }
        public IHarmonyDeriver HarmonyDeriver { get; }

        public GenreProfile(
            MusicalGenre genre,
            string id,
            string displayName,
            string description,
            float defaultTempo,
            string defaultKey,
            GroupBusFx busFx,
            PatternColorPalette colorPalette,
            List<InstrumentPreset> presets,
            Dictionary<PatternType, string> defaultPresetIds,
            Dictionary<PatternType, PatternSoundMappingProfile> soundMappings,
            IRhythmDeriver rhythmDeriver,
            IMelodyDeriver melodyDeriver,
            IHarmonyDeriver harmonyDeriver)
        {
            Genre = genre;
            Id = id;
            DisplayName = displayName;
            Description = description;
            DefaultTempo = defaultTempo;
            DefaultKey = defaultKey;
            BusFx = busFx;
            ColorPalette = colorPalette;
            _presets = presets;
            _defaultPresetIds = defaultPresetIds;
            _soundMappings = soundMappings;
            RhythmDeriver = rhythmDeriver;
            MelodyDeriver = melodyDeriver;
            HarmonyDeriver = harmonyDeriver;
        }

        public List<InstrumentPreset> GetPresets() => _presets;

        public string GetDefaultPresetId(PatternType type)
        {
            if (_defaultPresetIds.TryGetValue(PatternTypeCompatibility.Canonicalize(type), out var id))
                return id;

            if (PatternTypeCompatibility.IsMelodyFamily(type) &&
                _defaultPresetIds.TryGetValue(PatternType.Melody, out id))
                return id;

            if (_defaultPresetIds.TryGetValue(type, out id))
                return id;
            return _presets.Count > 0 ? _presets[0].id : string.Empty;
        }

        public PatternSoundMappingProfile GetSoundMapping(PatternType type)
        {
            if (_soundMappings.TryGetValue(PatternTypeCompatibility.Canonicalize(type), out var mapping))
                return mapping;

            if (PatternTypeCompatibility.IsMelodyFamily(type) &&
                _soundMappings.TryGetValue(PatternType.Melody, out mapping))
                return mapping;

            if (_soundMappings.TryGetValue(type, out mapping))
                return mapping;
            return PatternSoundMappingProfile.CreateRhythmDefaults();
        }
    }
}
