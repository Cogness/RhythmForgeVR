using System;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;

namespace RhythmForge.Core.Session
{
    public class SoundProfileResolver
    {
        private readonly Func<string, InstrumentPreset> _getPreset;

        public SoundProfileResolver(Func<string, InstrumentPreset> getPreset)
        {
            _getPreset = getPreset;
        }

        public string GetEffectivePresetId(PatternInstance instance, PatternDefinition pattern)
        {
            return !string.IsNullOrEmpty(instance.presetOverrideId) ? instance.presetOverrideId : pattern.presetId;
        }

        public SoundProfile GetEffectiveSoundProfile(PatternInstance instance, PatternDefinition pattern)
        {
            var preset = _getPreset(GetEffectivePresetId(instance, pattern));
            var geometry = pattern.soundProfile ?? SoundProfileMapper.Derive(pattern.type, pattern.shapeProfile);
            var presetBias = PresetBiasResolver.GetPresetBias(preset, pattern.type);
            return PresetBiasResolver.ResolveEffective(geometry, presetBias);
        }
    }
}
