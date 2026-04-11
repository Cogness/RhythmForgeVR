using UnityEngine;

namespace RhythmForge.Core.Data
{
    public static class TypeColors
    {
        public static readonly Color RhythmLoop = HexColor("#51d7ff");
        public static readonly Color MelodyLine = HexColor("#f7c975");
        public static readonly Color HarmonyPad = HexColor("#62f3d3");

        public static Color GetColor(PatternType type)
        {
            switch (type)
            {
                case PatternType.RhythmLoop: return RhythmLoop;
                case PatternType.MelodyLine: return MelodyLine;
                case PatternType.HarmonyPad: return HarmonyPad;
                default: return Color.white;
            }
        }

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}
