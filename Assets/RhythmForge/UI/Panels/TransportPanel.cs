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

        private bool _showParams = false;

        public event Action<bool> OnParamsVisibilityChanged;

        // ── Merged-panel section refs (populated by RhythmForgeBootstrapper) ──
        [SerializeField] private RectTransform _mergedCanvasRect;
        [SerializeField] private RectTransform _dockSection;
        [SerializeField] private RectTransform _genreSection;
        [SerializeField] private RectTransform _transportSection;
        [SerializeField] private RectTransform _phaseSection;
        [SerializeField] private RectTransform _sceneSection;
        [SerializeField] private RectTransform _arrangementSection;

        // Merged-panel layout constants (pixels, with canvas pivot at bottom-left).
        //
        // Layout (guided mode, total 1088 × 450):
        //
        //   ┌────────────┬──────────────────────────────┐
        //   │            │  Genre      740×200          │ y = H - 200
        //   │            ├──────────────────────────────┤
        //   │  Dock      │  Transport  740×100          │ y = H - 300
        //   │  340×H     ├──────────────────────────────┤
        //   │            │  Phase      740×150 (guided) │
        //   │            │  Scene+Arr  740×170 (free)   │ y = 0
        //   └────────────┴──────────────────────────────┘
        //
        public const float DockSectionW        = 340f;
        public const float DockGutter          = 8f;
        public const float RightColumnW        = 740f;
        public const float MergedWidth         = DockSectionW + DockGutter + RightColumnW; // 1088
        public const float GenreSectionH       = 200f;
        public const float TransportSectionH   = 100f;
        public const float PhaseSectionH       = 150f;
        public const float SceneSectionH       = 70f;
        public const float ArrangementSectionH = 100f;
        public const float GuidedTotalH        = GenreSectionH + TransportSectionH + PhaseSectionH;      // 450
        public const float FreeTotalH          = GenreSectionH + TransportSectionH + SceneSectionH + ArrangementSectionH; // 470

        private SessionStore _store;
        private Sequencer.Sequencer _sequencer;
        private DrawModeController _drawMode;
        private RhythmForgeEventBus _eventBus;
        private bool _guidedMode;
        // Tracks how many pixels (in canvas-local space) we've shifted the canvas DOWN from its
        // registered baseline to keep the Transport strip's top edge visually anchored when the
        // panel grows/shrinks with mode changes. 0 for guided (baseline), 20 for free.
        private float _currentAppliedOffsetPixels;
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
        /// Injects the merged Transport canvas + section RectTransforms so the panel can
        /// resize itself when switching between guided and free modes.
        /// </summary>
        public void SetSectionRefs(
            RectTransform mergedCanvasRect,
            RectTransform dockSection,
            RectTransform genreSection,
            RectTransform transportSection,
            RectTransform phaseSection,
            RectTransform sceneSection,
            RectTransform arrangementSection)
        {
            _mergedCanvasRect    = mergedCanvasRect;
            _dockSection         = dockSection;
            _genreSection        = genreSection;
            _transportSection    = transportSection;
            _phaseSection        = phaseSection;
            _sceneSection        = sceneSection;
            _arrangementSection  = arrangementSection;
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
            ApplyMergedLayout();
            Refresh();
        }

        /// <summary>
        /// Resizes the merged Transport canvas and repositions its sections so the Transport
        /// strip always occupies the top of the panel. Guided mode shows the Phase section
        /// below Transport; free mode swaps in the Scene + Arrangement sections.
        ///
        /// The canvas's world position is shifted so the Transport strip's top edge stays
        /// visually anchored — the panel grows DOWNWARD into empty space when entering free
        /// mode (20 px taller), and shrinks back when returning to guided. The applied shift
        /// is tracked so repeated calls (or re-calls after <see cref="ResetAfterReposition"/>)
        /// converge on the correct offset without accumulating drift.
        /// </summary>
        private void ApplyMergedLayout()
        {
            if (_mergedCanvasRect == null || _transportSection == null) return;

            // Target canvas-local pixel offset DOWN from the registered baseline.
            // Baseline corresponds to guided mode (canvas built at height GuidedTotalH).
            float targetOffsetPixels = _guidedMode ? 0f : (FreeTotalH - GuidedTotalH); // 0 or 20
            float deltaPixels = targetOffsetPixels - _currentAppliedOffsetPixels;
            if (Mathf.Abs(deltaPixels) > 0.01f)
            {
                // TransformVector applies the canvas's scale (0.001) and rotation, giving
                // the correct world-space down offset for the canvas's current orientation.
                _mergedCanvasRect.position +=
                    _mergedCanvasRect.TransformVector(new Vector3(0f, -deltaPixels, 0f));
                _currentAppliedOffsetPixels = targetOffsetPixels;
            }

            float newHeight = _guidedMode ? GuidedTotalH : FreeTotalH;
            _mergedCanvasRect.sizeDelta = new Vector2(MergedWidth, newHeight);

            // Dock spans the FULL canvas height on the left.
            if (_dockSection != null)
            {
                _dockSection.anchoredPosition = new Vector2(0f, 0f);
                _dockSection.sizeDelta = new Vector2(DockSectionW, newHeight);
            }

            // Genre stays at the TOP of the right column.
            if (_genreSection != null)
            {
                _genreSection.anchoredPosition = new Vector2(
                    _genreSection.anchoredPosition.x, newHeight - GenreSectionH);
            }

            // Transport sits directly below Genre.
            _transportSection.anchoredPosition = new Vector2(
                _transportSection.anchoredPosition.x,
                newHeight - GenreSectionH - TransportSectionH);

            // Phase / Scene+Arrangement fill the remaining bottom of the right column. We only
            // adjust Y — the X offsets were set at build time so each section is horizontally
            // positioned within the right column of the 1088-wide canvas.
            if (_guidedMode)
            {
                if (_phaseSection != null)
                    _phaseSection.anchoredPosition = new Vector2(_phaseSection.anchoredPosition.x, 0f);
            }
            else
            {
                if (_sceneSection != null)
                    _sceneSection.anchoredPosition = new Vector2(
                        _sceneSection.anchoredPosition.x, ArrangementSectionH);
                if (_arrangementSection != null)
                    _arrangementSection.anchoredPosition = new Vector2(
                        _arrangementSection.anchoredPosition.x, 0f);
            }
        }

        /// <summary>
        /// Called by the bootstrapper after <c>RepositionPanels</c> has placed the canvas at
        /// its registered baseline (guided-mode position). Resets our tracking so the next
        /// <see cref="ApplyMergedLayout"/> re-applies the mode-appropriate offset from that
        /// fresh baseline.
        /// </summary>
        public void ResetAfterReposition()
        {
            _currentAppliedOffsetPixels = 0f;
            ApplyMergedLayout();
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
