using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.Session;
using RhythmForge.Interaction;

namespace RhythmForge.UI.Panels
{
    public class PhasePanel : MonoBehaviour
    {
        [SerializeField] private Text _currentPhaseBanner;
        [SerializeField] private List<Button> _phaseButtons = new List<Button>();
        [SerializeField] private List<Text> _phaseLabels = new List<Text>();
        [SerializeField] private Color _currentColor = new Color(0.98f, 0.82f, 0.28f, 1f);
        [SerializeField] private Color _filledColor = new Color(0.24f, 0.72f, 0.44f, 1f);
        [SerializeField] private Color _emptyColor = new Color(0.25f, 0.25f, 0.3f, 1f);

        private SessionStore _store;
        private PhaseController _phaseController;
        private RhythmForgeEventBus _eventBus;

        public void SetUIRefs(Text currentPhaseBanner, List<Button> phaseButtons, List<Text> phaseLabels)
        {
            _currentPhaseBanner = currentPhaseBanner;
            _phaseButtons = phaseButtons;
            _phaseLabels = phaseLabels;
        }

        public void Initialize(SessionStore store, PhaseController phaseController)
        {
            _store = store;
            _phaseController = phaseController;
            _eventBus = store != null ? store.EventBus : null;

            var phases = CompositionPhaseExtensions.All;
            for (int i = 0; i < _phaseButtons.Count && i < phases.Length; i++)
            {
                int index = i;
                if (_phaseButtons[i] != null)
                    _phaseButtons[i].onClick.AddListener(() => _phaseController?.GoToPhase(phases[index]));
            }

            if (_eventBus != null)
            {
                _eventBus.Subscribe<SessionStateChangedEvent>(HandleSessionStateChanged);
                _eventBus.Subscribe<PhaseChangedEvent>(HandlePhaseChanged);
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (_eventBus == null)
                return;

            _eventBus.Unsubscribe<SessionStateChangedEvent>(HandleSessionStateChanged);
            _eventBus.Unsubscribe<PhaseChangedEvent>(HandlePhaseChanged);
        }

        private void HandleSessionStateChanged(SessionStateChangedEvent evt)
        {
            Refresh();
        }

        private void HandlePhaseChanged(PhaseChangedEvent evt)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_store == null)
                return;

            var composition = _store.GetComposition();
            CompositionPhase currentPhase = composition.currentPhase;

            if (_currentPhaseBanner != null)
                _currentPhaseBanner.text = $"Current phase: {currentPhase}";

            var phases = CompositionPhaseExtensions.All;
            for (int i = 0; i < _phaseButtons.Count && i < phases.Length; i++)
            {
                CompositionPhase phase = phases[i];
                bool isCurrent = phase == currentPhase;
                bool isFilled = HasCommittedPattern(composition, phase);
                string stateLabel = isCurrent ? "Current" : isFilled ? "Filled" : "Empty";
                Color color = isCurrent ? _currentColor : isFilled ? _filledColor : _emptyColor;

                if (i < _phaseLabels.Count && _phaseLabels[i] != null)
                    _phaseLabels[i].text = $"{phase}\n{stateLabel}";

                ApplyButtonStyle(_phaseButtons[i], color);
            }
        }

        private bool HasCommittedPattern(Composition composition, CompositionPhase phase)
        {
            if (composition == null)
                return false;

            string patternId = composition.GetPatternId(phase);
            if (string.IsNullOrEmpty(patternId))
                return false;

            return _store.GetPattern(patternId) != null;
        }

        private static void ApplyButtonStyle(Button button, Color color)
        {
            if (button == null)
                return;

            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
            button.colors = colors;

            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = color;
        }
    }
}
