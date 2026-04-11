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
        private static List<InstrumentGroup> _groups;

        public static List<InstrumentGroup> All
        {
            get
            {
                if (_groups == null)
                    Initialize();
                return _groups;
            }
        }

        public static InstrumentGroup Get(string groupId)
        {
            foreach (var g in All)
            {
                if (g.id == groupId) return g;
            }
            return All[0];
        }

        private static void Initialize()
        {
            _groups = new List<InstrumentGroup>
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
        private static List<InstrumentPreset> _presets;

        public static List<InstrumentPreset> All
        {
            get
            {
                if (_presets == null)
                    Initialize();
                return _presets;
            }
        }

        public static InstrumentPreset Get(string presetId)
        {
            foreach (var p in All)
            {
                if (p.id == presetId) return p;
            }
            return All[0];
        }

        private static void Initialize()
        {
            _presets = new List<InstrumentPreset>
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
}
