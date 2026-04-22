using System;
using UnityEngine;
using UnityEngine.UI;
using RhythmForge.Bootstrap;
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
        [SerializeField] private Button _viewModeButton;
        [SerializeField] private Text _viewModeLabel;

        private bool _showParams = true;

        public event Action<bool> OnParamsVisibilityChanged;

        private SessionStore _store;
        private Sequencer.Sequencer _sequencer;
        private DrawModeController _drawMode;
        private RhythmForgeEventBus _eventBus;
        private bool _guidedMode;
        private ImmersionController _immersion;

        // Visual palette for the view-mode button.
        private static readonly Color ImmersedBg    = new Color(0.18f, 0.22f, 0.32f, 1f); // indigo
        private static readonly Color PassthroughBg = new Color(0.26f, 0.46f, 0.58f, 1f); // teal-ish

        /// <summary>Called by RhythmForgeBootstrapper to inject UI element references.</summary>
        public void SetUIRefs(Button playStopButton, Text playStopLabel,
            Button modeButton, Text modeButtonLabel,
            Text bpmText, Text keyText, Text transportStatus,
            Button toggleParamsButton = null, Text toggleParamsLabel = null,
            Button viewModeButton = null, Text viewModeLabel = null)
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
            _viewModeButton      = viewModeButton;
            _viewModeLabel       = viewModeLabel;
        }

        /// <summary>
        /// Binds the view-mode button to an <see cref="ImmersionController"/>.
        /// Must be called after <see cref="Initialize"/>. Safe to call with null.
        /// </summary>
        public void BindImmersion(ImmersionController controller)
        {
            _immersion = controller;
            if (_viewModeButton != null)
            {
                _viewModeButton.onClick.AddListener(() => _immersion?.Toggle());
            }
            if (_immersion != null)
            {
                _immersion.OnModeChanged += HandleImmersionChanged;
                HandleImmersionChanged(_immersion.PassthroughEnabled);
            }
        }

        public void Initialize(SessionStore store, Sequencer.Sequencer sequencer, DrawModeController drawMode)
        {
            _store = store;
            _sequencer = sequencer;
            _drawMode = drawMode;
            _eventBus = _store != null ? _store.EventBus : null;
            _guidedMode = _store != null && _store.State.guidedMode;

            if (_playStopButton)
                _playStopButton.onClick.AddListener(() => _sequencer.TogglePlayback());
            if (_modeButton && _drawMode != null)
                _modeButton.onClick.AddListener(CycleModeIfAllowed);
            if (_toggleParamsButton)
                _toggleParamsButton.onClick.AddListener(ToggleParams);

            RefreshParamsButton();

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
            if (_immersion != null)
                _immersion.OnModeChanged -= HandleImmersionChanged;

            if (_eventBus == null)
                return;

            _eventBus.Unsubscribe<SessionStateChangedEvent>(HandleSessionStateChanged);
            _eventBus.Unsubscribe<TransportChangedEvent>(HandleTransportChanged);
            _eventBus.Unsubscribe<DrawModeChangedEvent>(HandleDrawModeChanged);
        }

        private void HandleImmersionChanged(bool passthrough)
        {
            if (_viewModeLabel != null)
                _viewModeLabel.text = passthrough ? "View\nPass-Thru" : "View\nImmersed";

            if (_viewModeButton != null)
            {
                Color bg = passthrough ? PassthroughBg : ImmersedBg;
                var colors = _viewModeButton.colors;
                colors.normalColor = bg;
                colors.highlightedColor = Color.Lerp(bg, Color.white, 0.25f);
                colors.pressedColor = Color.Lerp(bg, Color.black, 0.25f);
                _viewModeButton.colors = colors;

                var image = _viewModeButton.GetComponent<Image>();
                if (image != null)
                    image.color = bg;
            }
        }

        private void OnModeChanged(PatternType mode)
        {
            RefreshModeButton(mode);
        }

        public void SetGuidedMode(bool guidedMode)
        {
            _guidedMode = guidedMode;
            Refresh();
        }

        private void Refresh()
        {
            RefreshTransportStatus();
            RefreshModeButton(_drawMode != null ? _drawMode.CurrentMode : PatternType.Percussion);
        }

        private void RefreshTransportStatus()
        {
            if (_store == null || _sequencer == null) return;

            _guidedMode = _store.State.guidedMode;
            var composition = _store.GetComposition();
            float tempo = _guidedMode && composition != null ? composition.tempo : _store.State.tempo;
            string key = _guidedMode && composition != null ? composition.key : _store.State.key;

            if (_playStopLabel)
                _playStopLabel.text = _sequencer.IsPlaying ? "Stop" : "Play";
            if (_bpmText)
                _bpmText.text = $"{tempo:F0} BPM";
            if (_keyText)
                _keyText.text = key;

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

        public bool ShowParams => _showParams;

        private void CycleModeIfAllowed()
        {
            if (_guidedMode)
                return;

            _drawMode?.CycleMode();
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

        private void RefreshModeButton(PatternType mode)
        {
            if (_modeButtonLabel != null)
                _modeButtonLabel.text = _guidedMode ? "Phase\nLocked" : $"Mode\n{DrawModeController.GetModeLabel(mode)}";

            if (_modeButton == null) return;

            _modeButton.interactable = !_guidedMode;

            Color color = _guidedMode
                ? new Color(0.38f, 0.38f, 0.44f, 1f)
                : TypeColors.GetColor(mode);
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
