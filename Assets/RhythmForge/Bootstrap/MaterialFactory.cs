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
        public static Color RhythmColor => TypeColors.RhythmLoop;
        public static Color MelodyColor => TypeColors.MelodyLine;
        public static Color HarmonyColor => TypeColors.HarmonyPad;

        // UI accent colors
        public static Color PanelBg => VisualGrammarProfiles.GetUI().panelBg;
        public static Color ButtonDefault => VisualGrammarProfiles.GetUI().buttonDefault;
        public static Color ButtonActive => VisualGrammarProfiles.GetUI().buttonActive;
        public static Color ButtonDanger => VisualGrammarProfiles.GetUI().buttonDanger;
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
            return TypeColors.GetColor(type);
        }
    }
}
