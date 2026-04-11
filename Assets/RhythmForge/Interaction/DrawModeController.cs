using System;
using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Interaction
{
    public class DrawModeController : MonoBehaviour
    {
        private PatternType _currentMode = PatternType.RhythmLoop;

        public PatternType CurrentMode => _currentMode;

        public event Action<PatternType> OnModeChanged;

        public void SetMode(PatternType mode)
        {
            _currentMode = mode;
            OnModeChanged?.Invoke(_currentMode);
        }

        public void CycleMode()
        {
            PatternType nextMode;
            switch (_currentMode)
            {
                case PatternType.RhythmLoop:
                    nextMode = PatternType.MelodyLine;
                    break;
                case PatternType.MelodyLine:
                    nextMode = PatternType.HarmonyPad;
                    break;
                default:
                    nextMode = PatternType.RhythmLoop;
                    break;
            }
            SetMode(nextMode);
        }

        public Color GetCurrentColor()
        {
            return TypeColors.GetColor(_currentMode);
        }

        public string GetCurrentModeLabel()
        {
            return GetModeLabel(_currentMode);
        }

        public static string GetModeLabel(PatternType mode)
        {
            switch (mode)
            {
                case PatternType.RhythmLoop: return "Rhythm";
                case PatternType.MelodyLine: return "Melody";
                default: return "Harmony";
            }
        }
    }
}
