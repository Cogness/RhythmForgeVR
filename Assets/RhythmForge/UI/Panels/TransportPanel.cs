using UnityEngine;
using UnityEngine.UI;
using RhythmForge.Core.Data;
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

        private SessionStore _store;
        private Sequencer.Sequencer _sequencer;
        private DrawModeController _drawMode;

        /// <summary>Called by RhythmForgeBootstrapper to inject UI element references.</summary>
        public void SetUIRefs(Button playStopButton, Text playStopLabel,
            Button modeButton, Text modeButtonLabel,
            Text bpmText, Text keyText, Text transportStatus)
        {
            _playStopButton  = playStopButton;
            _playStopLabel   = playStopLabel;
            _modeButton      = modeButton;
            _modeButtonLabel = modeButtonLabel;
            _bpmText         = bpmText;
            _keyText         = keyText;
            _transportStatus = transportStatus;
        }

        public void Initialize(SessionStore store, Sequencer.Sequencer sequencer, DrawModeController drawMode)
        {
            _store = store;
            _sequencer = sequencer;
            _drawMode = drawMode;

            if (_playStopButton)
                _playStopButton.onClick.AddListener(() => _sequencer.TogglePlayback());
            if (_modeButton && _drawMode != null)
                _modeButton.onClick.AddListener(() => _drawMode.CycleMode());

            if (_store != null) _store.OnStateChanged += Refresh;
            if (_sequencer != null) _sequencer.OnTransportChanged += Refresh;
            if (_drawMode != null) _drawMode.OnModeChanged += OnModeChanged;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_store != null) _store.OnStateChanged -= Refresh;
            if (_sequencer != null) _sequencer.OnTransportChanged -= Refresh;
            if (_drawMode != null) _drawMode.OnModeChanged -= OnModeChanged;
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
