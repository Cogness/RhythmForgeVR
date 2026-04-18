using System;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.PatternBehavior;

namespace RhythmForge.Interaction
{
    public class DrawModeController : MonoBehaviour
    {
        private PatternType _currentMode = PatternType.RhythmLoop;
        private ShapeFacetMode _shapeMode = ShapeFacetMode.Free;
        private RhythmForgeEventBus _eventBus;

        public PatternType CurrentMode => _currentMode;
        public ShapeFacetMode ShapeMode => _shapeMode;

        public event Action<PatternType> OnModeChanged;
        public event Action<ShapeFacetMode> OnShapeModeChanged;

        public void SetEventBus(RhythmForgeEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public void SetMode(PatternType mode)
        {
            _currentMode = mode;
            OnModeChanged?.Invoke(_currentMode);
            _eventBus?.Publish(new DrawModeChangedEvent(_currentMode));
        }

        public void SetShapeMode(ShapeFacetMode mode)
        {
            _shapeMode = mode;
            OnShapeModeChanged?.Invoke(_shapeMode);
            _eventBus?.Publish(new DrawShapeModeChangedEvent(_shapeMode));
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

        public void CycleShapeMode()
        {
            int next = ((int)_shapeMode + 1) % 4;
            SetShapeMode((ShapeFacetMode)next);
        }

        public PatternType GetDominantType()
        {
            return _currentMode;
        }

        public Vector3 GetBondStrength()
        {
            switch (_shapeMode)
            {
                case ShapeFacetMode.SoloRhythm:  return new Vector3(1f, 0f, 0f);
                case ShapeFacetMode.SoloMelody:  return new Vector3(0f, 1f, 0f);
                case ShapeFacetMode.SoloHarmony: return new Vector3(0f, 0f, 1f);
                default:                        return new Vector3(1f, 1f, 1f);
            }
        }

        /// <summary>
        /// Phase D: Free mode routes through <see cref="BondStrengthResolver"/>
        /// to compute bondStrength from the stroke's 3D profile; Solo modes
        /// keep the one-hot vector returned by <see cref="GetBondStrength"/>.
        /// </summary>
        public bool IsFreeMode()
        {
            return _shapeMode == ShapeFacetMode.Free;
        }

        public Color GetCurrentColor()
        {
            return TypeColors.Blend(GetBondStrength());
        }

        public string GetCurrentModeLabel()
        {
            return GetModeLabel(_currentMode);
        }

        public static string GetModeLabel(PatternType mode)
        {
            return PatternBehaviorRegistry.Get(mode).DisplayName;
        }

        public string GetShapeModeLabel()
        {
            switch (_shapeMode)
            {
                case ShapeFacetMode.SoloRhythm:  return "Solo R";
                case ShapeFacetMode.SoloMelody:  return "Solo M";
                case ShapeFacetMode.SoloHarmony: return "Solo H";
                default:                        return "Free";
            }
        }
    }
}
