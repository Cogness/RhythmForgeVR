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
            switch (_currentMode)
            {
                case PatternType.RhythmLoop:
                    _currentMode = PatternType.MelodyLine;
                    break;
                case PatternType.MelodyLine:
                    _currentMode = PatternType.HarmonyPad;
                    break;
                default:
                    _currentMode = PatternType.RhythmLoop;
                    break;
            }
            OnModeChanged?.Invoke(_currentMode);
        }

        public Color GetCurrentColor()
        {
            return TypeColors.GetColor(_currentMode);
        }

        public string GetCurrentModeLabel()
        {
            switch (_currentMode)
            {
                case PatternType.RhythmLoop: return "Rhythm";
                case PatternType.MelodyLine: return "Melody";
                default: return "Harmony";
            }
        }
    }
}
