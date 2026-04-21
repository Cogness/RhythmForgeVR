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
        [SerializeField] private List<Button> _clearButtons = new List<Button>();
        [SerializeField] private Button _playPieceButton;
        [SerializeField] private Text _playPieceLabel;
        [SerializeField] private Color _currentColor = new Color(0.98f, 0.82f, 0.28f, 1f);
        [SerializeField] private Color _filledColor = new Color(0.24f, 0.72f, 0.44f, 1f);
        [SerializeField] private Color _emptyColor = new Color(0.25f, 0.25f, 0.3f, 1f);

        private SessionStore _store;
        private PhaseController _phaseController;
        private RhythmForge.Sequencer.Sequencer _sequencer;
        private RhythmForgeEventBus _eventBus;

        public void SetUIRefs(
            Text currentPhaseBanner,
            List<Button> phaseButtons,
            List<Text> phaseLabels,
            List<Button> clearButtons,
            Button playPieceButton,
            Text playPieceLabel)
        {
            _currentPhaseBanner = currentPhaseBanner;
            _phaseButtons = phaseButtons;
            _phaseLabels = phaseLabels;
            _clearButtons = clearButtons;
            _playPieceButton = playPieceButton;
            _playPieceLabel = playPieceLabel;
        }

        public void Initialize(SessionStore store, PhaseController phaseController, RhythmForge.Sequencer.Sequencer sequencer)
        {
            _store = store;
            _phaseController = phaseController;
            _sequencer = sequencer;
            _eventBus = store != null ? store.EventBus : null;

            var phases = CompositionPhaseExtensions.All;
            for (int i = 0; i < _phaseButtons.Count && i < phases.Length; i++)
            {
                int index = i;
                if (_phaseButtons[i] != null)
                    _phaseButtons[i].onClick.AddListener(() => _phaseController?.GoToPhase(phases[index]));
                if (i < _clearButtons.Count && _clearButtons[i] != null)
                    _clearButtons[i].onClick.AddListener(() => _store?.ClearPhase(phases[index]));
            }

            if (_playPieceButton != null)
                _playPieceButton.onClick.AddListener(() => _sequencer?.TogglePlayback());

            if (_eventBus != null)
            {
                _eventBus.Subscribe<SessionStateChangedEvent>(HandleSessionStateChanged);
                _eventBus.Subscribe<PhaseChangedEvent>(HandlePhaseChanged);
                _eventBus.Subscribe<TransportChangedEvent>(HandleTransportChanged);
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (_eventBus == null)
                return;

            _eventBus.Unsubscribe<SessionStateChangedEvent>(HandleSessionStateChanged);
            _eventBus.Unsubscribe<PhaseChangedEvent>(HandlePhaseChanged);
            _eventBus.Unsubscribe<TransportChangedEvent>(HandleTransportChanged);
        }

        private void HandleSessionStateChanged(SessionStateChangedEvent evt)
        {
            Refresh();
        }

        private void HandlePhaseChanged(PhaseChangedEvent evt)
        {
            Refresh();
        }

        private void HandleTransportChanged(TransportChangedEvent evt)
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
                bool isPending = _store.IsPhasePending(phase);
                string stateLabel = isCurrent ? "Current" : isFilled ? "Filled" : "Empty";
                if (isPending)
                    stateLabel = $"{stateLabel} • Pending";
                Color color = isCurrent ? _currentColor : isFilled ? _filledColor : _emptyColor;

                if (i < _phaseLabels.Count && _phaseLabels[i] != null)
                    _phaseLabels[i].text = $"{phase}\n{stateLabel}";

                ApplyButtonStyle(_phaseButtons[i], color);

                if (i < _clearButtons.Count && _clearButtons[i] != null)
                    _clearButtons[i].interactable = isFilled;
            }

            if (_playPieceLabel != null)
                _playPieceLabel.text = _sequencer != null && _sequencer.IsPlaying ? "Stop Piece" : "Play Piece";
        }

        private bool HasCommittedPattern(Composition composition, CompositionPhase phase)
        {
            if (composition == null || _store == null)
                return false;

            return _store.HasCommittedPhase(phase);
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
