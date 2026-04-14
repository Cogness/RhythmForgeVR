using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmForge.Core.Data
{
    [Serializable]
    public class InstrumentGroup
    {
        public string id;
        public string name;
        public GroupDefaultPresets defaultPresetByType;
        public GroupBusFx busFx;
        public Color[] swatches;
    }

    [Serializable]
    public class GroupDefaultPresets
    {
        public string RhythmLoop;
        public string MelodyLine;
        public string HarmonyPad;

        public string GetDefault(PatternType type)
        {
            switch (type)
            {
                case PatternType.RhythmLoop: return RhythmLoop;
                case PatternType.MelodyLine: return MelodyLine;
                case PatternType.HarmonyPad: return HarmonyPad;
                default: return RhythmLoop;
            }
        }
    }

    [Serializable]
    public class GroupBusFx
    {
        public float reverb;
        public float delay;
    }

    public static class InstrumentGroups
    {
        public static List<InstrumentGroup> All
        {
            get => InstrumentRegistryRuntime.GetGroups();
        }

        public static InstrumentGroup Get(string groupId)
        {
            foreach (var g in All)
            {
                if (g.id == groupId) return g;
            }
            return All[0];
        }

        public static void SetRegistry(InstrumentRegistryAsset registry)
        {
            InstrumentRegistryRuntime.SetActiveRegistry(registry);
        }

        internal static List<InstrumentGroup> CreateDefaults()
        {
            return new List<InstrumentGroup>
            {
                new InstrumentGroup
                {
                    id = "lofi",
                    name = "Lo-Fi Kit",
                    defaultPresetByType = new GroupDefaultPresets
                    {
                        RhythmLoop = "lofi-drums",
                        MelodyLine = "lofi-piano",
                        HarmonyPad = "lofi-pad"
                    },
                    busFx = new GroupBusFx { reverb = 0.24f, delay = 0.16f },
                    swatches = new[] { HexColor("#51d7ff"), HexColor("#f7c975"), HexColor("#62f3d3") }
                },
                new InstrumentGroup
                {
                    id = "trap",
                    name = "Trap Kit",
                    defaultPresetByType = new GroupDefaultPresets
                    {
                        RhythmLoop = "trap-drums",
                        MelodyLine = "trap-bass",
                        HarmonyPad = "trap-pad"
                    },
                    busFx = new GroupBusFx { reverb = 0.12f, delay = 0.28f },
                    swatches = new[] { HexColor("#ff5151"), HexColor("#ffb347"), HexColor("#ff6bcb") }
                },
                new InstrumentGroup
                {
                    id = "dream",
                    name = "Dream Ensemble",
                    defaultPresetByType = new GroupDefaultPresets
                    {
                        RhythmLoop = "dream-perc",
                        MelodyLine = "dream-bell",
                        HarmonyPad = "dream-pad"
                    },
                    busFx = new GroupBusFx { reverb = 0.42f, delay = 0.22f },
                    swatches = new[] { HexColor("#b19cd9"), HexColor("#77dd77"), HexColor("#aec6cf") }
                }
            };
        }

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }

    public static class InstrumentPresets
    {
        public static List<InstrumentPreset> All
        {
            get => InstrumentRegistryRuntime.GetPresets();
        }

        public static InstrumentPreset Get(string presetId)
        {
            foreach (var p in All)
            {
                if (p.id == presetId) return p;
            }
            return All[0];
        }

        public static InstrumentPreset GetDefaultForVoice(string voiceType)
        {
            foreach (var preset in All)
            {
                if (preset.voiceType == voiceType)
                    return preset;
            }

            return All.Count > 0 ? All[0] : null;
        }

        public static void SetRegistry(InstrumentRegistryAsset registry)
        {
            InstrumentRegistryRuntime.SetActiveRegistry(registry);
        }

        internal static List<InstrumentPreset> CreateDefaults()
        {
            return new List<InstrumentPreset>
            {
                new InstrumentPreset("lofi-drums", "Lo-Fi Drums", "lofi-drums", "lofi", 0.18f, 0.005f, 0.3f),
                new InstrumentPreset("lofi-piano", "Lo-Fi Piano", "lofi-piano", "lofi", 0.22f, 0.01f, 0.6f),
                new InstrumentPreset("lofi-pad", "Lo-Fi Pad", "lofi-pad", "lofi", 0.28f, 0.4f, 1.8f),
                new InstrumentPreset("trap-drums", "Trap Drums", "trap-drums", "trap", 0.14f, 0.003f, 0.2f),
                new InstrumentPreset("trap-bass", "Trap Bass", "trap-bass", "trap", 0.1f, 0.008f, 0.34f),
                new InstrumentPreset("trap-pad", "Trap Pad", "trap-pad", "trap", 0.2f, 0.3f, 1.4f),
                new InstrumentPreset("dream-perc", "Dream Perc", "dream-perc", "dream", 0.32f, 0.01f, 0.5f),
                new InstrumentPreset("dream-bell", "Dream Bell", "dream-bell", "dream", 0.34f, 0.006f, 0.8f),
                new InstrumentPreset("dream-pad", "Dream Pad", "dream-pad", "dream", 0.4f, 0.6f, 2.4f),
            };
        }
    }

    internal static class InstrumentRegistryRuntime
    {
        private const string RegistryResourcePath = "RhythmForge/InstrumentRegistry";
        private const string GroupResourcesPath = "RhythmForge/InstrumentGroups";
        private const string PresetResourcesPath = "RhythmForge/InstrumentPresets";

        private static InstrumentRegistryAsset _activeRegistry;
        private static List<InstrumentGroup> _groups;
        private static List<InstrumentPreset> _presets;

        public static void SetActiveRegistry(InstrumentRegistryAsset registry)
        {
            _activeRegistry = registry;
            _groups = null;
            _presets = null;
        }

        public static List<InstrumentGroup> GetGroups()
        {
            EnsureLoaded();
            return _groups;
        }

        public static List<InstrumentPreset> GetPresets()
        {
            EnsureLoaded();
            return _presets;
        }

        private static void EnsureLoaded()
        {
            if (_groups != null && _presets != null)
                return;

            if (TryLoadFromRegistry(_activeRegistry) || TryLoadFromRegistry(Resources.Load<InstrumentRegistryAsset>(RegistryResourcePath)))
                return;

            if (TryLoadFromResources())
                return;

            _groups = InstrumentGroups.CreateDefaults();
            _presets = InstrumentPresets.CreateDefaults();
        }

        private static bool TryLoadFromRegistry(InstrumentRegistryAsset registry)
        {
            if (registry == null)
                return false;

            var groups = new List<InstrumentGroup>();
            var presets = new List<InstrumentPreset>();

            if (registry.groups != null)
            {
                foreach (var groupAsset in registry.groups)
                {
                    if (groupAsset != null)
                        groups.Add(groupAsset.ToRuntime());
                }
            }

            if (registry.presets != null)
            {
                foreach (var presetAsset in registry.presets)
                {
                    if (presetAsset != null)
                        presets.Add(presetAsset.ToRuntime());
                }
            }

            if (groups.Count == 0 || presets.Count == 0)
                return false;

            _groups = groups;
            _presets = presets;
            return true;
        }

        private static bool TryLoadFromResources()
        {
            var groupAssets = Resources.LoadAll<InstrumentGroupAsset>(GroupResourcesPath);
            var presetAssets = Resources.LoadAll<InstrumentPresetAsset>(PresetResourcesPath);

            if (groupAssets == null || groupAssets.Length == 0 || presetAssets == null || presetAssets.Length == 0)
                return false;

            _groups = new List<InstrumentGroup>(groupAssets.Length);
            foreach (var groupAsset in groupAssets)
            {
                if (groupAsset != null)
                    _groups.Add(groupAsset.ToRuntime());
            }

            _presets = new List<InstrumentPreset>(presetAssets.Length);
            foreach (var presetAsset in presetAssets)
            {
                if (presetAsset != null)
                    _presets.Add(presetAsset.ToRuntime());
            }

            return _groups.Count > 0 && _presets.Count > 0;
        }
    }
}
