using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmForge.Core.Data
{
    [CreateAssetMenu(menuName = "RhythmForge/Instrument Group")]
    public class InstrumentGroupAsset : ScriptableObject
    {
        public string id;
        public string displayName;
        public GroupDefaultPresets defaultPresetByType = new GroupDefaultPresets();
        public GroupBusFx busFx = new GroupBusFx();
        public Color[] swatches = new Color[0];

        public InstrumentGroup ToRuntime()
        {
            return new InstrumentGroup
            {
                id = id,
                name = string.IsNullOrEmpty(displayName) ? id : displayName,
                defaultPresetByType = Clone(defaultPresetByType),
                busFx = Clone(busFx),
                swatches = swatches != null ? (Color[])swatches.Clone() : new Color[0]
            };
        }

        private static GroupDefaultPresets Clone(GroupDefaultPresets defaults)
        {
            defaults = defaults ?? new GroupDefaultPresets();
            return new GroupDefaultPresets
            {
                RhythmLoop = defaults.RhythmLoop,
                MelodyLine = defaults.MelodyLine,
                HarmonyPad = defaults.HarmonyPad
            };
        }

        private static GroupBusFx Clone(GroupBusFx busFx)
        {
            busFx = busFx ?? new GroupBusFx();
            return new GroupBusFx
            {
                reverb = busFx.reverb,
                delay = busFx.delay
            };
        }
    }

    [CreateAssetMenu(menuName = "RhythmForge/Instrument Preset")]
    public class InstrumentPresetAsset : ScriptableObject
    {
        public string id;
        public string label;
        public string voiceType;
        public string groupId;
        public float fxSend;
        public PresetEnvelope envelope = new PresetEnvelope();

        public InstrumentPreset ToRuntime()
        {
            return new InstrumentPreset
            {
                id = id,
                label = string.IsNullOrEmpty(label) ? id : label,
                voiceType = voiceType,
                groupId = groupId,
                fxSend = fxSend,
                envelope = envelope != null
                    ? new PresetEnvelope { attack = envelope.attack, release = envelope.release }
                    : new PresetEnvelope()
            };
        }
    }

    [CreateAssetMenu(menuName = "RhythmForge/Instrument Registry")]
    public class InstrumentRegistryAsset : ScriptableObject
    {
        public List<InstrumentGroupAsset> groups = new List<InstrumentGroupAsset>();
        public List<InstrumentPresetAsset> presets = new List<InstrumentPresetAsset>();
    }
}
