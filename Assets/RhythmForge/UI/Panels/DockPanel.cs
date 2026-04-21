using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.Session;
using RhythmForge.Interaction;

namespace RhythmForge.UI.Panels
{
    /// <summary>
    /// World-space Canvas panel with 3 tabs: Instruments, Patterns, Scenes.
    /// Anchored to user's left, provides pattern spawning and group selection.
    /// </summary>
    public class DockPanel : MonoBehaviour
    {
        [Header("Tab Buttons")]
        [SerializeField] private Button _instrumentsTab;
        [SerializeField] private Button _patternsTab;
        [SerializeField] private Button _scenesTab;

        [Header("Tab Panels")]
        [SerializeField] private GameObject _instrumentsPanel;
        [SerializeField] private GameObject _patternsPanel;
        [SerializeField] private GameObject _scenesPanel;

        [Header("Instruments Tab")]
        [SerializeField] private Transform _groupButtonContainer;
        [SerializeField] private Text _drawModeLabel;

        [Header("Patterns Tab")]
        [SerializeField] private Transform _patternListContainer;

        [Header("Scenes Tab")]
        [SerializeField] private Transform _sceneListContainer;

        [Header("Prefabs")]
        [SerializeField] private GameObject _groupButtonPrefab;
        [SerializeField] private GameObject _patternListItemPrefab;

        [Header("References")]
        [SerializeField] private Transform _lookAtTarget;

        private SessionStore _store;
        private DrawModeController _drawMode;
        private RhythmForgeEventBus _eventBus;
        private string _activeTab = "instruments";
        private bool _guidedMode;

        /// <summary>Called by RhythmForgeBootstrapper to inject all UI references (no prefabs required).</summary>
        public void SetUIRefs(
            Button instrumentsTab, Button patternsTab, Button scenesTab,
            GameObject instrumentsPanel, GameObject patternsPanel, GameObject scenesPanel,
            Transform groupButtonContainer, Text drawModeLabel,
            Transform patternListContainer, Transform sceneListContainer, Transform lookAt)
        {
            _instrumentsTab          = instrumentsTab;
            _patternsTab             = patternsTab;
            _scenesTab               = scenesTab;
            _instrumentsPanel        = instrumentsPanel;
            _patternsPanel           = patternsPanel;
            _scenesPanel             = scenesPanel;
            _groupButtonContainer    = groupButtonContainer;
            _drawModeLabel           = drawModeLabel;
            _patternListContainer    = patternListContainer;
            _sceneListContainer      = sceneListContainer;
            _lookAtTarget            = lookAt;
            _groupButtonPrefab       = null;
            _patternListItemPrefab   = null;
        }

        public void Initialize(SessionStore store, DrawModeController drawMode)
        {
            _store = store;
            _drawMode = drawMode;
            _eventBus = _store != null ? _store.EventBus : null;
            _guidedMode = _store != null && _store.State.guidedMode;

            if (_instrumentsTab) _instrumentsTab.onClick.AddListener(() => SetTab("instruments"));
            if (_patternsTab) _patternsTab.onClick.AddListener(() => SetTab("patterns"));
            if (_scenesTab) _scenesTab.onClick.AddListener(() => SetTab("scenes"));

            if (_eventBus != null)
            {
                _eventBus.Subscribe<SessionStateChangedEvent>(HandleSessionStateChanged);
                _eventBus.Subscribe<DrawModeChangedEvent>(HandleDrawModeChanged);
            }

            SetTab("instruments");
            Refresh();
        }

        public void SetGuidedMode(bool guidedMode)
        {
            _guidedMode = guidedMode;
            if (_scenesTab != null)
                _scenesTab.gameObject.SetActive(!guidedMode);
            if (guidedMode && _activeTab == "scenes")
                SetTab("instruments");

            RefreshDrawModeLabel();
        }

        private void OnDestroy()
        {
            if (_eventBus == null)
                return;

            _eventBus.Unsubscribe<SessionStateChangedEvent>(HandleSessionStateChanged);
            _eventBus.Unsubscribe<DrawModeChangedEvent>(HandleDrawModeChanged);
        }

        private void SetTab(string tab)
        {
            _activeTab = tab;
            if (_instrumentsPanel) _instrumentsPanel.SetActive(tab == "instruments");
            if (_patternsPanel) _patternsPanel.SetActive(tab == "patterns");
            if (_scenesPanel) _scenesPanel.SetActive(tab == "scenes");
        }

        private void OnModeChanged(PatternType mode)
        {
            RefreshDrawModeLabel();
        }

        private void HandleSessionStateChanged(SessionStateChangedEvent evt)
        {
            Refresh();
        }

        private void HandleDrawModeChanged(DrawModeChangedEvent evt)
        {
            OnModeChanged(evt.Mode);
        }

        private void Refresh()
        {
            if (_store == null) return;
            RefreshDrawModeLabel();
            RefreshInstruments();
            RefreshPatterns();
        }

        private void RefreshDrawModeLabel()
        {
            if (_drawModeLabel == null || _drawMode == null || _store == null)
                return;

            _guidedMode = _store.State.guidedMode;
            if (_guidedMode)
                _drawModeLabel.text = $"Current phase: {_store.GetCurrentPhase()}";
            else
                _drawModeLabel.text = $"Mode: {_drawMode.GetCurrentModeLabel()}";
        }

        private void RefreshInstruments()
        {
            if (_groupButtonContainer == null || _groupButtonPrefab == null) return;

            // Clear existing
            foreach (Transform child in _groupButtonContainer)
                Destroy(child.gameObject);

            foreach (var group in InstrumentGroups.All)
            {
                var go = Instantiate(_groupButtonPrefab, _groupButtonContainer);
                var label = go.GetComponentInChildren<Text>();
                if (label) label.text = group.name;

                var button = go.GetComponent<Button>();
                if (button)
                {
                    string gid = group.id;
                    button.onClick.AddListener(() => _store.SetActiveGroup(gid));

                    // Highlight active group
                    var colors = button.colors;
                    colors.normalColor = group.id == _store.State.activeGroupId
                        ? new Color(0.3f, 0.7f, 1f, 1f)
                        : new Color(0.22f, 0.22f, 0.28f, 1f);
                    button.colors = colors;
                }
            }
        }

        private void RefreshPatterns()
        {
            if (_patternListContainer == null || _patternListItemPrefab == null) return;

            // Clear existing
            foreach (Transform child in _patternListContainer)
                Destroy(child.gameObject);

            foreach (var pattern in _store.State.patterns)
            {
                var go = Instantiate(_patternListItemPrefab, _patternListContainer);

                var nameLabel = go.transform.Find("Name")?.GetComponent<Text>();
                if (nameLabel) nameLabel.text = pattern.name;

                var typeLabel = go.transform.Find("Type")?.GetComponent<Text>();
                if (typeLabel) typeLabel.text = pattern.type.ToString();

                var colorBar = go.transform.Find("ColorBar")?.GetComponent<Image>();
                if (colorBar) colorBar.color = pattern.color;

                // Spawn button
                var spawnBtn = go.transform.Find("SpawnButton")?.GetComponent<Button>();
                if (spawnBtn)
                {
                    string pid = pattern.id;
                    spawnBtn.onClick.AddListener(() => _store.SpawnPattern(pid));
                }

                // Clone button
                var cloneBtn = go.transform.Find("CloneButton")?.GetComponent<Button>();
                if (cloneBtn)
                {
                    string pid = pattern.id;
                    cloneBtn.onClick.AddListener(() => _store.ClonePattern(pid));
                }
            }
        }
    }
}
