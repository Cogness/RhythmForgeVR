using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;

namespace RhythmForge.UI.Panels
{
    /// <summary>
    /// World-space Canvas panel showing Scene A-D buttons for switching.
    /// </summary>
    public class SceneStripPanel : MonoBehaviour
    {
        [Header("Scene Buttons")]
        [SerializeField] private List<Button> _sceneButtons;
        [SerializeField] private List<Text> _sceneLabels;

        [Header("Visual")]
        [SerializeField] private Color _activeColor = new Color(0.3f, 0.85f, 1f, 1f);
        [SerializeField] private Color _inactiveColor = new Color(0.25f, 0.25f, 0.3f, 1f);
        [SerializeField] private Color _queuedColor = new Color(1f, 0.8f, 0.2f, 1f);

        private SessionStore _store;
        private Sequencer.Sequencer _sequencer;
        private string[] _sceneIds = { "scene-a", "scene-b", "scene-c", "scene-d" };

        /// <summary>Called by RhythmForgeBootstrapper to inject UI button and label references.</summary>
        public void SetUIRefs(List<Button> buttons, List<Text> labels)
        {
            _sceneButtons = buttons;
            _sceneLabels  = labels;
        }

        public void Initialize(SessionStore store, Sequencer.Sequencer sequencer)
        {
            _store = store;
            _sequencer = sequencer;

            for (int i = 0; i < _sceneButtons.Count && i < _sceneIds.Length; i++)
            {
                int index = i;
                _sceneButtons[i].onClick.AddListener(() => OnSceneClicked(index));
            }

            _store.OnStateChanged += Refresh;
            _sequencer.OnTransportChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_store != null) _store.OnStateChanged -= Refresh;
            if (_sequencer != null) _sequencer.OnTransportChanged -= Refresh;
        }

        private void OnSceneClicked(int index)
        {
            if (index < 0 || index >= _sceneIds.Length) return;

            if (_sequencer.IsPlaying && !_sequencer.HasArrangement())
                _store.QueueScene(_sceneIds[index]);
            else
                _store.SetActiveScene(_sceneIds[index]);
        }

        private void Refresh()
        {
            if (_store == null) return;

            for (int i = 0; i < _sceneButtons.Count && i < _sceneIds.Length; i++)
            {
                bool isActive = _store.State.activeSceneId == _sceneIds[i];
                bool isQueued = _store.State.queuedSceneId == _sceneIds[i];

                var colors = _sceneButtons[i].colors;
                colors.normalColor = isQueued ? _queuedColor : isActive ? _activeColor : _inactiveColor;
                _sceneButtons[i].colors = colors;

                if (i < _sceneLabels.Count && _sceneLabels[i] != null)
                {
                    var scene = _store.GetScene(_sceneIds[i]);
                    string label = scene?.name ?? $"Scene {(char)('A' + i)}";
                    if (isActive && _sequencer.IsPlaying)
                        label += " \u25B6"; // play symbol
                    _sceneLabels[i].text = label;
                }
            }
        }
    }
}
