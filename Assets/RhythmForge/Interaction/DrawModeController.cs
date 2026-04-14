using System;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.PatternBehavior;

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
            var registeredTypes = PatternBehaviorRegistry.GetRegisteredTypes();
            if (registeredTypes.Count == 0)
            {
                SetMode(PatternType.RhythmLoop);
                return;
            }

            int currentIndex = 0;
            for (int i = 0; i < registeredTypes.Count; i++)
            {
                if (registeredTypes[i] == _currentMode)
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = (currentIndex + 1) % registeredTypes.Count;
            SetMode(registeredTypes[nextIndex]);
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
            return PatternBehaviorRegistry.Get(mode).DisplayName;
        }
    }
}
