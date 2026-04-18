using System;
using UnityEngine;
using UnityEngine.UI;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.Session;
using RhythmForge.Interaction;

namespace RhythmForge.UI.Panels
{
    /// <summary>
    /// World-space Canvas panel showing Play/Stop, draw mode, BPM, and Key controls.
    /// </summary>
    public class TransportPanel : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Button _playStopButton;
        [SerializeField] private Text _playStopLabel;
        [SerializeField] private Button _modeButton;
        [SerializeField] private Text _modeButtonLabel;
        [SerializeField] private Text _bpmText;
        [SerializeField] private Text _keyText;
        [SerializeField] private Text _transportStatus;
        [SerializeField] private Button _toggleParamsButton;
        [SerializeField] private Text _toggleParamsLabel;
        [SerializeField] private Button _conductButton;
        [SerializeField] private Text _conductLabel;

        private bool _showParams = true;
        private bool _conductingMode = false;

        public event Action<bool> OnParamsVisibilityChanged;
        public event Action<bool> OnConductingModeChanged;

        public bool IsConductingMode => _conductingMode;

        private SessionStore _store;
        private Sequencer.Sequencer _sequencer;
        private DrawModeController _drawMode;
        private RhythmForgeEventBus _eventBus;

        /// <summary>Called by RhythmForgeBootstrapper to inject UI element references.</summary>
        public void SetUIRefs(Button playStopButton, Text playStopLabel,
            Button modeButton, Text modeButtonLabel,
            Text bpmText, Text keyText, Text transportStatus,
            Button toggleParamsButton = null, Text toggleParamsLabel = null,
            Button conductButton = null, Text conductLabel = null)
        {
            _playStopButton      = playStopButton;
            _playStopLabel       = playStopLabel;
            _modeButton          = modeButton;
            _modeButtonLabel     = modeButtonLabel;
            _bpmText             = bpmText;
            _keyText             = keyText;
            _transportStatus     = transportStatus;
            _toggleParamsButton  = toggleParamsButton;
            _toggleParamsLabel   = toggleParamsLabel;
            _conductButton       = conductButton;
            _conductLabel        = conductLabel;
        }

        public void Initialize(SessionStore store, Sequencer.Sequencer sequencer, DrawModeController drawMode)
        {
            _store = store;
            _sequencer = sequencer;
            _drawMode = drawMode;
            _eventBus = _store != null ? _store.EventBus : null;

            if (_playStopButton)
                _playStopButton.onClick.AddListener(() => _sequencer.TogglePlayback());
            if (_modeButton && _drawMode != null)
                _modeButton.onClick.AddListener(() => _drawMode.CycleMode());
            if (_toggleParamsButton)
                _toggleParamsButton.onClick.AddListener(ToggleParams);
            if (_conductButton)
                _conductButton.onClick.AddListener(ToggleConducting);

            RefreshParamsButton();
            RefreshConductButton();

            if (_eventBus != null)
            {
                _eventBus.Subscribe<SessionStateChangedEvent>(HandleSessionStateChanged);
                _eventBus.Subscribe<TransportChangedEvent>(HandleTransportChanged);
                _eventBus.Subscribe<DrawModeChangedEvent>(HandleDrawModeChanged);
            }
            Refresh();
        }

        private void OnDestroy()
        {
            if (_eventBus == null)
                return;

            _eventBus.Unsubscribe<SessionStateChangedEvent>(HandleSessionStateChanged);
            _eventBus.Unsubscribe<TransportChangedEvent>(HandleTransportChanged);
            _eventBus.Unsubscribe<DrawModeChangedEvent>(HandleDrawModeChanged);
        }

        private void OnModeChanged(PatternType mode)
        {
            RefreshModeButton(mode);
        }

        private void Refresh()
        {
            RefreshTransportStatus();
            RefreshModeButton(_drawMode != null ? _drawMode.CurrentMode : PatternType.RhythmLoop);
        }

        private void RefreshTransportStatus()
        {
            if (_store == null || _sequencer == null) return;

            if (_playStopLabel)
                _playStopLabel.text = _sequencer.IsPlaying ? "Stop" : "Play";
            if (_bpmText)
                _bpmText.text = $"{_store.State.tempo:F0} BPM";
            if (_keyText)
                _keyText.text = _store.State.key;

            if (_transportStatus)
            {
                if (!_sequencer.IsPlaying)
                    _transportStatus.text = "Idle";
                else
                {
                    var scene = _store.GetScene(_sequencer.GetPlaybackSceneId());
                    string sceneName = scene?.name ?? "";
                    _transportStatus.text = $"Playing {sceneName} - Bar {_sequencer.CurrentTransport.absoluteBar}";
                }
            }
        }

        private void ToggleParams()
        {
            _showParams = !_showParams;
            RefreshParamsButton();
            OnParamsVisibilityChanged?.Invoke(_showParams);
            _eventBus?.Publish(new ParameterLabelsVisibilityChangedEvent(_showParams));
        }

        private void RefreshParamsButton()
        {
            if (_toggleParamsLabel != null)
                _toggleParamsLabel.text = _showParams ? "Params\nON" : "Params\nOFF";
        }

        private void ToggleConducting()
        {
            _conductingMode = !_conductingMode;
            RefreshConductButton();
            OnConductingModeChanged?.Invoke(_conductingMode);
        }

        private void RefreshConductButton()
        {
            if (_conductLabel != null)
                _conductLabel.text = _conductingMode ? "Conduct\nON" : "Conduct\nOFF";

            if (_conductButton == null) return;
            var colors = _conductButton.colors;
            colors.normalColor = _conductingMode
                ? new Color(0.2f, 0.7f, 0.5f, 1f)   // teal when active
                : new Color(0.22f, 0.25f, 0.32f, 1f); // dark when inactive
            _conductButton.colors = colors;
            var img = _conductButton.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = colors.normalColor;
        }

        public bool ShowParams => _showParams;

        private void HandleSessionStateChanged(SessionStateChangedEvent evt)
        {
            Refresh();
        }

        private void HandleTransportChanged(TransportChangedEvent evt)
        {
            Refresh();
        }

        private void HandleDrawModeChanged(DrawModeChangedEvent evt)
        {
            OnModeChanged(evt.Mode);
        }

        private void RefreshModeButton(PatternType mode)
        {
            if (_modeButtonLabel != null)
                _modeButtonLabel.text = $"Mode\n{DrawModeController.GetModeLabel(mode)}";

            if (_modeButton == null) return;

            Color color = TypeColors.GetColor(mode);
            var colors = _modeButton.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.25f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
            _modeButton.colors = colors;

            var image = _modeButton.GetComponent<Image>();
            if (image != null)
                image.color = color;
        }
    }
}
