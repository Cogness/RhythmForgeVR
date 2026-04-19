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
        [SerializeField] private Button _shapeModeButton;
        [SerializeField] private Text _shapeModeButtonLabel;
        [SerializeField] private Text _bpmText;
        [SerializeField] private Text _keyText;
        [SerializeField] private Text _transportStatus;
        [SerializeField] private Button _toggleParamsButton;
        [SerializeField] private Text _toggleParamsLabel;
        [SerializeField] private Button _conductingButton;
        [SerializeField] private Text _conductingLabel;

        private bool _showParams = true;
        private bool _conductingModeOn;
        private float _gesturePulse;

        public event Action<bool> OnParamsVisibilityChanged;

        private SessionStore _store;
        private Sequencer.Sequencer _sequencer;
        private DrawModeController _drawMode;
        private RhythmForgeEventBus _eventBus;

        /// <summary>Called by RhythmForgeBootstrapper to inject UI element references.</summary>
        public void SetUIRefs(Button playStopButton, Text playStopLabel,
            Button modeButton, Text modeButtonLabel,
            Button shapeModeButton, Text shapeModeButtonLabel,
            Text bpmText, Text keyText, Text transportStatus,
            Button toggleParamsButton = null, Text toggleParamsLabel = null,
            Button conductingButton = null, Text conductingLabel = null)
        {
            _playStopButton      = playStopButton;
            _playStopLabel       = playStopLabel;
            _modeButton          = modeButton;
            _modeButtonLabel     = modeButtonLabel;
            _shapeModeButton     = shapeModeButton;
            _shapeModeButtonLabel = shapeModeButtonLabel;
            _bpmText             = bpmText;
            _keyText             = keyText;
            _transportStatus     = transportStatus;
            _toggleParamsButton  = toggleParamsButton;
            _toggleParamsLabel   = toggleParamsLabel;
            _conductingButton    = conductingButton;
            _conductingLabel     = conductingLabel;
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
            if (_shapeModeButton && _drawMode != null)
                _shapeModeButton.onClick.AddListener(() => _drawMode.CycleShapeMode());
            if (_toggleParamsButton)
                _toggleParamsButton.onClick.AddListener(ToggleParams);
            if (_conductingButton)
                _conductingButton.onClick.AddListener(ToggleConducting);

            RefreshParamsButton();

            if (_eventBus != null)
            {
                _eventBus.Subscribe<SessionStateChangedEvent>(HandleSessionStateChanged);
                _eventBus.Subscribe<TransportChangedEvent>(HandleTransportChanged);
                _eventBus.Subscribe<DrawModeChangedEvent>(HandleDrawModeChanged);
                _eventBus.Subscribe<DrawShapeModeChangedEvent>(HandleDrawShapeModeChanged);
                _eventBus.Subscribe<ConductingModeChangedEvent>(HandleConductingModeChanged);
                _eventBus.Subscribe<ConductorGestureFiredEvent>(HandleConductorGestureFired);
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
            _eventBus.Unsubscribe<DrawShapeModeChangedEvent>(HandleDrawShapeModeChanged);
            _eventBus.Unsubscribe<ConductingModeChangedEvent>(HandleConductingModeChanged);
            _eventBus.Unsubscribe<ConductorGestureFiredEvent>(HandleConductorGestureFired);
        }

        private void OnModeChanged(PatternType mode)
        {
            RefreshModeButton(mode);
        }

        private void Refresh()
        {
            RefreshTransportStatus();
            RefreshModeButton(_drawMode != null ? _drawMode.CurrentMode : PatternType.RhythmLoop);
            RefreshShapeModeButton(_drawMode != null ? _drawMode.ShapeMode : ShapeFacetMode.Free);
            RefreshConductingButton();
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

        private void ToggleConducting()
        {
            _conductingModeOn = !_conductingModeOn;
            RefreshConductingButton();
            _eventBus?.Publish(new ConductingModeChangedEvent(_conductingModeOn));
        }

        private void RefreshParamsButton()
        {
            if (_toggleParamsLabel != null)
                _toggleParamsLabel.text = _showParams ? "Params\nON" : "Params\nOFF";
        }

        public bool ShowParams => _showParams;

        private void Update()
        {
            if (_gesturePulse <= 0f)
                return;

            _gesturePulse = Mathf.MoveTowards(_gesturePulse, 0f, Time.deltaTime / 0.20f);
            RefreshConductingButton();
        }

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

        private void HandleDrawShapeModeChanged(DrawShapeModeChangedEvent evt)
        {
            RefreshShapeModeButton(evt.Mode);
        }

        private void HandleConductingModeChanged(ConductingModeChangedEvent evt)
        {
            _conductingModeOn = evt.On;
            RefreshConductingButton();
        }

        private void HandleConductorGestureFired(ConductorGestureFiredEvent evt)
        {
            _gesturePulse = 1f;
            RefreshConductingButton();
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

        private void RefreshShapeModeButton(ShapeFacetMode mode)
        {
            if (_shapeModeButtonLabel != null && _drawMode != null)
                _shapeModeButtonLabel.text = $"Shape\n{_drawMode.GetShapeModeLabel()}";

            if (_shapeModeButton == null || _drawMode == null)
                return;

            Color color = TypeColors.Blend(_drawMode.GetBondStrength());
            var colors = _shapeModeButton.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.25f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
            _shapeModeButton.colors = colors;

            var image = _shapeModeButton.GetComponent<Image>();
            if (image != null)
                image.color = color;
        }

        private void RefreshConductingButton()
        {
            if (_conductingLabel != null)
                _conductingLabel.text = _conductingModeOn ? "Conduct\nON" : "Conduct\nOFF";

            if (_conductingButton == null)
                return;

            Color offColor = new Color(0.32f, 0.34f, 0.40f);
            Color onColor = new Color(0.96f, 0.58f, 0.18f);
            Color color = Color.Lerp(_conductingModeOn ? onColor : offColor, Color.white, _gesturePulse * 0.35f);

            var colors = _conductingButton.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            _conductingButton.colors = colors;

            var image = _conductingButton.GetComponent<Image>();
            if (image != null)
                image.color = color;
        }
    }
}
