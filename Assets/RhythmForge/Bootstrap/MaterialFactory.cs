using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Bootstrap
{
    /// <summary>
    /// Creates Unity Materials from code at runtime — no .mat assets needed.
    /// </summary>
    public static class MaterialFactory
    {
        // Pattern type colors (match TypeColors.cs)
        public static readonly Color RhythmColor = new Color(0.20f, 0.85f, 1.00f, 1f);  // cyan
        public static readonly Color MelodyColor  = new Color(0.97f, 0.78f, 0.46f, 1f);  // amber
        public static readonly Color HarmonyColor = new Color(0.38f, 0.95f, 0.83f, 1f);  // teal

        // UI accent colors
        public static readonly Color PanelBg       = new Color(0.08f, 0.08f, 0.12f, 0.88f);
        public static readonly Color ButtonDefault  = new Color(0.18f, 0.18f, 0.24f, 1f);
        public static readonly Color ButtonActive   = new Color(0.24f, 0.72f, 0.96f, 1f);
        public static readonly Color ButtonDanger   = new Color(0.80f, 0.22f, 0.22f, 1f);
        public static readonly Color White          = Color.white;

        private static Shader _spritesDefault;
        private static Shader SpritesDefault => _spritesDefault != null
            ? _spritesDefault
            : (_spritesDefault = Shader.Find("Sprites/Default"));

        private static Shader _uiDefault;
        private static Shader UIDefault => _uiDefault != null
            ? _uiDefault
            : (_uiDefault = Shader.Find("UI/Default"));

        public static Material CreateStrokeMaterial(PatternType type)
        {
            return CreateStrokeMaterial(GetTypeColor(type));
        }

        public static Material CreateStrokeMaterial(Color color)
        {
            var mat = new Material(SpritesDefault);
            mat.color = color;
            return mat;
        }

        public static Material CreateUIPanel()
        {
            var mat = new Material(UIDefault);
            return mat;
        }

        public static Color GetTypeColor(PatternType type)
        {
            switch (type)
            {
                case PatternType.RhythmLoop: return RhythmColor;
                case PatternType.MelodyLine: return MelodyColor;
                case PatternType.HarmonyPad: return HarmonyColor;
                default: return RhythmColor;
            }
        }
    }
}
