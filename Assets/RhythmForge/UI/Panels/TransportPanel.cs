using UnityEngine;
using UnityEngine.UI;
using RhythmForge.Core.Session;

namespace RhythmForge.UI.Panels
{
    /// <summary>
    /// World-space Canvas panel showing Play/Stop, BPM, and Key controls.
    /// </summary>
    public class TransportPanel : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Button _playStopButton;
        [SerializeField] private Text _playStopLabel;
        [SerializeField] private Text _bpmText;
        [SerializeField] private Text _keyText;
        [SerializeField] private Text _transportStatus;

        private SessionStore _store;
        private Sequencer.Sequencer _sequencer;

        public void Initialize(SessionStore store, Sequencer.Sequencer sequencer)
        {
            _store = store;
            _sequencer = sequencer;

            if (_playStopButton)
                _playStopButton.onClick.AddListener(() => _sequencer.TogglePlayback());

            _store.OnStateChanged += Refresh;
            _sequencer.OnTransportChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_store != null) _store.OnStateChanged -= Refresh;
            if (_sequencer != null) _sequencer.OnTransportChanged -= Refresh;
        }

        private void Refresh()
        {
            if (_store == null) return;

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
    }
}
