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
    /// Dock panel integrated into the merged Transport mega-panel.
    /// Instruments tab shows one card per composition phase with instrument preset and
    /// key musical details. Patterns and Scenes tabs are reserved for future use.
    /// </summary>
    public class DockPanel : MonoBehaviour
    {
        /// <summary>UI element references for a single phase card in the Instruments tab.</summary>
        public struct PhaseCardUI
        {
            public Image  background;
            public Image  accentBar;
            public Text   phaseLabel;
            public Text   detailsText;   // preset · key · bars · tempo
            public Text   summaryText;   // pattern.summary
        }

        [Header("Tab Buttons")]
        [SerializeField] private Button _instrumentsTab;
        [SerializeField] private Button _patternsTab;
        [SerializeField] private Button _scenesTab;

        [Header("Tab Panels")]
        [SerializeField] private GameObject _instrumentsPanel;
        [SerializeField] private GameObject _patternsPanel;
        [SerializeField] private GameObject _scenesPanel;

        [Header("Instruments Tab")]
        [SerializeField] private Text _drawModeLabel;

        [Header("References")]
        [SerializeField] private Transform _lookAtTarget;

        private readonly List<PhaseCardUI> _phaseCardSlots = new List<PhaseCardUI>();

        private SessionStore _store;
        private DrawModeController _drawMode;
        private RhythmForgeEventBus _eventBus;
        private string _activeTab = "instruments";
        private bool _guidedMode;

        // ── Phase display names and canonical order ──────────────────────────
        private static readonly CompositionPhase[] PhaseOrder =
            CompositionPhaseExtensions.All;

        private static readonly Dictionary<CompositionPhase, string> PhaseDisplayNames =
            new Dictionary<CompositionPhase, string>
            {
                { CompositionPhase.Harmony,    "Harmony"    },
                { CompositionPhase.Melody,     "Melody"     },
                { CompositionPhase.Groove,     "Groove"     },
                { CompositionPhase.Bass,       "Bass"       },
                { CompositionPhase.Percussion, "Percussion" },
            };

        // Accent colours per phase (matches PhasePanel colours).
        private static readonly Dictionary<CompositionPhase, Color> PhaseAccentColors =
            new Dictionary<CompositionPhase, Color>
            {
                { CompositionPhase.Harmony,    new Color(0.53f, 0.40f, 0.85f) },
                { CompositionPhase.Melody,     new Color(0.25f, 0.72f, 0.98f) },
                { CompositionPhase.Groove,     new Color(0.98f, 0.70f, 0.22f) },
                { CompositionPhase.Bass,       new Color(0.35f, 0.85f, 0.55f) },
                { CompositionPhase.Percussion, new Color(0.95f, 0.35f, 0.35f) },
            };

        private static readonly Color CardBgCommitted   = new Color(0.14f, 0.16f, 0.22f, 1f);
        private static readonly Color CardBgUncommitted = new Color(0.10f, 0.10f, 0.13f, 1f);
        private static readonly Color TextDim           = new Color(0.45f, 0.48f, 0.55f, 1f);
        private static readonly Color TextBright        = new Color(0.90f, 0.93f, 1.00f, 1f);
        private static readonly Color TextMuted         = new Color(0.65f, 0.68f, 0.75f, 1f);

        /// <summary>Called by RhythmForgeBootstrapper to inject all UI references.</summary>
        public void SetUIRefs(
            Button instrumentsTab, Button patternsTab, Button scenesTab,
            GameObject instrumentsPanel, GameObject patternsPanel, GameObject scenesPanel,
            Text drawModeLabel,
            List<PhaseCardUI> phaseCardSlots,
            Transform lookAt)
        {
            _instrumentsTab = instrumentsTab;
            _patternsTab    = patternsTab;
            _scenesTab      = scenesTab;
            _instrumentsPanel = instrumentsPanel;
            _patternsPanel    = patternsPanel;
            _scenesPanel      = scenesPanel;
            _drawModeLabel    = drawModeLabel;
            _lookAtTarget     = lookAt;

            _phaseCardSlots.Clear();
            if (phaseCardSlots != null)
                _phaseCardSlots.AddRange(phaseCardSlots);
        }

        public void Initialize(SessionStore store, DrawModeController drawMode)
        {
            _store    = store;
            _drawMode = drawMode;
            _eventBus = _store != null ? _store.EventBus : null;
            _guidedMode = _store != null && _store.State.guidedMode;

            if (_instrumentsTab) _instrumentsTab.onClick.AddListener(() => SetTab("instruments"));
            if (_patternsTab)    _patternsTab.onClick.AddListener(()    => SetTab("patterns"));
            if (_scenesTab)      _scenesTab.onClick.AddListener(()      => SetTab("scenes"));

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
            if (_eventBus == null) return;
            _eventBus.Unsubscribe<SessionStateChangedEvent>(HandleSessionStateChanged);
            _eventBus.Unsubscribe<DrawModeChangedEvent>(HandleDrawModeChanged);
        }

        private void SetTab(string tab)
        {
            _activeTab = tab;
            if (_instrumentsPanel) _instrumentsPanel.SetActive(tab == "instruments");
            if (_patternsPanel)    _patternsPanel.SetActive(tab == "patterns");
            if (_scenesPanel)      _scenesPanel.SetActive(tab == "scenes");

            // Highlight the active tab button.
            SetTabHighlight(_instrumentsTab, tab == "instruments");
            SetTabHighlight(_patternsTab,    tab == "patterns");
            SetTabHighlight(_scenesTab,      tab == "scenes");
        }

        private static void SetTabHighlight(Button btn, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img == null) return;
            img.color = active
                ? new Color(0.25f, 0.55f, 0.95f, 1f)
                : new Color(0.18f, 0.20f, 0.26f, 1f);
        }

        private void HandleSessionStateChanged(SessionStateChangedEvent evt) => Refresh();

        private void HandleDrawModeChanged(DrawModeChangedEvent evt) => RefreshDrawModeLabel();

        private void Refresh()
        {
            if (_store == null) return;
            RefreshDrawModeLabel();
            RefreshInstruments();
        }

        private void RefreshDrawModeLabel()
        {
            if (_drawModeLabel == null || _drawMode == null || _store == null)
                return;

            // Always show the draw mode. The "current phase" info is already displayed in the
            // Phase section of the merged Transport panel, so we drop the duplicate here.
            _guidedMode = _store.State.guidedMode;
            _drawModeLabel.text = $"Mode: {_drawMode.GetCurrentModeLabel()}";
        }

        private void RefreshInstruments()
        {
            if (_phaseCardSlots.Count == 0 || _store == null) return;

            var composition = _store.GetComposition();
            var genre       = GenreRegistry.GetActive();

            for (int i = 0; i < PhaseOrder.Length && i < _phaseCardSlots.Count; i++)
            {
                var phase = PhaseOrder[i];
                var card  = _phaseCardSlots[i];

                bool committed = _store.HasCommittedPhase(phase);
                string patternId = composition.GetPatternId(phase);
                PatternDefinition pat = committed && !string.IsNullOrEmpty(patternId)
                    ? _store.GetPattern(patternId)
                    : null;

                // ── Background ──────────────────────────────────────────────
                if (card.background != null)
                    card.background.color = committed ? CardBgCommitted : CardBgUncommitted;

                // ── Left accent bar ─────────────────────────────────────────
                if (card.accentBar != null)
                {
                    var accent = PhaseAccentColors.TryGetValue(phase, out var c) ? c : Color.gray;
                    card.accentBar.color = committed ? accent : new Color(accent.r, accent.g, accent.b, 0.25f);
                }

                // ── Phase label (e.g. "● HARMONY") ─────────────────────────
                if (card.phaseLabel != null)
                {
                    string name = PhaseDisplayNames.TryGetValue(phase, out var n) ? n : phase.ToString();
                    card.phaseLabel.text  = $"● {name.ToUpper()}";
                    card.phaseLabel.color = committed ? TextBright : TextDim;
                }

                // ── Details line (preset · key · bars · tempo) ──────────────
                if (card.detailsText != null)
                {
                    if (pat != null)
                    {
                        string presetLabel = GetPresetLabel(pat.presetId, genre, phase);
                        string key         = string.IsNullOrEmpty(pat.key) ? "—" : pat.key;
                        int    bars        = pat.bars > 0 ? pat.bars : 4;
                        int    tempo       = pat.tempoBase > 0f ? Mathf.RoundToInt(pat.tempoBase) : Mathf.RoundToInt(_store.State.tempo);
                        card.detailsText.text  = $"{presetLabel}  ·  {key}  ·  {bars} bars  ·  {tempo} BPM";
                        card.detailsText.color = TextMuted;
                    }
                    else
                    {
                        card.detailsText.text  = committed ? "—" : "Not yet composed";
                        card.detailsText.color = TextDim;
                    }
                }

                // ── Summary text ────────────────────────────────────────────
                if (card.summaryText != null)
                {
                    if (pat != null && !string.IsNullOrEmpty(pat.summary))
                    {
                        string s = pat.summary.Length > 72 ? pat.summary.Substring(0, 69) + "…" : pat.summary;
                        card.summaryText.text  = s;
                        card.summaryText.color = TextMuted;
                    }
                    else
                    {
                        card.summaryText.text = string.Empty;
                    }
                }
            }
        }

        // Returns a human-readable preset label, falling back to the genre's default for the phase type.
        private static string GetPresetLabel(string presetId, GenreProfile genre, CompositionPhase phase)
        {
            if (!string.IsNullOrEmpty(presetId))
            {
                var preset = InstrumentPresets.Get(presetId);
                if (preset != null && !string.IsNullOrEmpty(preset.label))
                    return preset.label;
            }

            // Fallback: use genre default preset label.
            var patType = phase.ToPatternType();
            string defaultId = genre?.GetDefaultPresetId(patType) ?? string.Empty;
            if (!string.IsNullOrEmpty(defaultId))
            {
                var def = InstrumentPresets.Get(defaultId);
                if (def != null && !string.IsNullOrEmpty(def.label))
                    return def.label;
            }

            return "—";
        }
    }
}
