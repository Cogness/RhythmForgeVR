using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Events;

namespace RhythmForge.UI.Panels
{
    /// <summary>
    /// World-space Canvas panel showing selected instance details:
    /// pattern identity, Shape DNA metrics, trait chips, and instance controls.
    /// </summary>
    public class InspectorPanel : MonoBehaviour
    {
        [Header("Pattern Identity")]
        [SerializeField] private Text _patternName;
        [SerializeField] private Text _patternType;
        [SerializeField] private Text _patternBars;
        [SerializeField] private Image _typeColorBar;

        [Header("Shape DNA")]
        [SerializeField] private Text _shapeSummary;
        [SerializeField] private Text _traitChipsText;
        [SerializeField] private List<Slider> _metricBars;
        [SerializeField] private List<Text> _metricLabels;

        [Header("Instance Controls")]
        [SerializeField] private Slider _depthSlider;
        [SerializeField] private Button _muteButton;
        [SerializeField] private Text _muteLabel;
        [SerializeField] private Button _removeButton;
        [SerializeField] private Button _duplicateButton;
        [SerializeField] private Dropdown _presetDropdown;

        [Header("Mix Readout")]
        [SerializeField] private Text _panText;
        [SerializeField] private Text _gainText;
        [SerializeField] private Text _brightnessText;

        [Header("References")]
        [SerializeField] private Transform _lookAtTarget;

        private SessionStore _store;
        private RhythmForgeEventBus _eventBus;
        private bool _updating;

        /// <summary>Called by RhythmForgeBootstrapper to inject all UI element references.</summary>
        public void SetUIRefs(
            Text patternName, Text patternType, Text patternBars, Image typeColorBar,
            Text shapeSummary, Text traitChips,
            List<Slider> metricBars, List<Text> metricLabels,
            Slider depthSlider, Button muteButton, Text muteLabel,
            Button removeButton, Button duplicateButton, Dropdown presetDropdown,
            Text panText, Text gainText, Text brightnessText, Transform lookAt)
        {
            _patternName     = patternName;
            _patternType     = patternType;
            _patternBars     = patternBars;
            _typeColorBar    = typeColorBar;
            _shapeSummary    = shapeSummary;
            _traitChipsText  = traitChips;
            _metricBars      = metricBars;
            _metricLabels    = metricLabels;
            _depthSlider     = depthSlider;
            _muteButton      = muteButton;
            _muteLabel       = muteLabel;
            _removeButton    = removeButton;
            _duplicateButton = duplicateButton;
            _presetDropdown  = presetDropdown;
            _panText         = panText;
            _gainText        = gainText;
            _brightnessText  = brightnessText;
            _lookAtTarget    = lookAt;
        }

        public void Initialize(SessionStore store)
        {
            _store = store;
            _eventBus = _store != null ? _store.EventBus : null;

            if (_depthSlider) _depthSlider.onValueChanged.AddListener(OnDepthChanged);
            if (_muteButton) _muteButton.onClick.AddListener(OnToggleMute);
            if (_removeButton) _removeButton.onClick.AddListener(OnRemove);
            if (_duplicateButton) _duplicateButton.onClick.AddListener(OnDuplicate);
            if (_presetDropdown)
            {
                _presetDropdown.ClearOptions();
                var options = new List<string> { "(default)" };
                foreach (var p in InstrumentPresets.All)
                    options.Add(p.label);
                _presetDropdown.AddOptions(options);
                _presetDropdown.onValueChanged.AddListener(OnPresetChanged);
            }

            if (_eventBus != null)
                _eventBus.Subscribe<SessionStateChangedEvent>(HandleSessionStateChanged);
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_eventBus != null)
                _eventBus.Unsubscribe<SessionStateChangedEvent>(HandleSessionStateChanged);
        }

        private void Update()
        {
            if (!gameObject.activeSelf || _lookAtTarget == null) return;
            Vector3 dir = _lookAtTarget.position - transform.position;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(-dir.normalized, Vector3.up);
        }

        private void Refresh()
        {
            if (_store == null) return;

            string instanceId = _store.State.selectedInstanceId;
            if (string.IsNullOrEmpty(instanceId))
            {
                gameObject.SetActive(false);
                return;
            }

            var instance = _store.GetInstance(instanceId);
            if (instance == null) { gameObject.SetActive(false); return; }

            var pattern = _store.GetPattern(instance.patternId);
            if (pattern == null) { gameObject.SetActive(false); return; }

            gameObject.SetActive(true);
            _updating = true;

            // Position near the instance
            transform.position = instance.position + Vector3.right * 0.25f + Vector3.up * 0.1f;

            // Pattern identity
            if (_patternName) _patternName.text = pattern.name;
            if (_patternType) _patternType.text = pattern.type.ToString();
            if (_patternBars) _patternBars.text = $"{pattern.bars} bars";
            if (_typeColorBar) _typeColorBar.color = pattern.color;

            // Shape DNA
            if (_shapeSummary) _shapeSummary.text = pattern.shapeSummary ?? "";

            // Trait chips
            if (_traitChipsText && pattern.soundProfile != null)
                _traitChipsText.text = BuildTraitChips(pattern.soundProfile);

            // Metric bars
            UpdateMetricBars(pattern);

            // Instance controls
            if (_depthSlider) _depthSlider.SetValueWithoutNotify(instance.depth);
            if (_muteLabel) _muteLabel.text = instance.muted ? "Unmute" : "Mute";

            // Preset dropdown
            if (_presetDropdown)
            {
                string currentPresetId = _store.GetEffectivePresetId(instance, pattern);
                int idx = 0;
                for (int i = 0; i < InstrumentPresets.All.Count; i++)
                {
                    if (InstrumentPresets.All[i].id == instance.presetOverrideId)
                    { idx = i + 1; break; }
                }
                _presetDropdown.SetValueWithoutNotify(idx);
            }

            // Mix readout
            if (_panText) _panText.text = $"Dir: {DescribeDirection(instance.position)}";
            if (_gainText) _gainText.text = $"Gain: {instance.gainTrim:F2}";
            if (_brightnessText) _brightnessText.text = $"Bright: {instance.brightness:F2}";

            _updating = false;
        }

        private void HandleSessionStateChanged(SessionStateChangedEvent evt)
        {
            Refresh();
        }

        private void UpdateMetricBars(PatternDefinition pattern)
        {
            if (pattern.shapeProfile == null) return;
            var sp = pattern.shapeProfile;

            string[] labels;
            float[] values;

            switch (pattern.type)
            {
                case PatternType.RhythmLoop:
                    labels = new[] { "Size", "Circularity", "Angularity", "Symmetry", "Wobble", "Path" };
                    values = new[] { ShapeProfileSizing.GetSizeFactor(pattern.type, sp), sp.circularity, sp.angularity, sp.symmetry, sp.wobble, sp.pathLength };
                    break;
                case PatternType.MelodyLine:
                    labels = new[] { "Size", "V-Span", "Angularity", "Curvature", "Speed Var", "Dir Bias" };
                    values = new[] { ShapeProfileSizing.GetSizeFactor(pattern.type, sp), sp.verticalSpan, sp.angularity, sp.curvatureMean,
                        sp.speedVariance, Mathf.Abs(sp.directionBias - 0.5f) * 2f };
                    break;
                default:
                    labels = new[] { "Size", "Width", "Height", "Tilt", "Symmetry", "Path" };
                    values = new[] { ShapeProfileSizing.GetSizeFactor(pattern.type, sp), sp.horizontalSpan, sp.centroidHeight,
                        Mathf.Abs(sp.tiltSigned), sp.symmetry, sp.pathLength };
                    break;
            }

            for (int i = 0; i < _metricBars.Count && i < values.Length; i++)
            {
                _metricBars[i].SetValueWithoutNotify(values[i]);
                if (i < _metricLabels.Count && _metricLabels[i] != null)
                    _metricLabels[i].text = labels[i];
            }
        }

        private string BuildTraitChips(SoundProfile sound)
        {
            var chips = new List<string>();
            if (sound.body > 0.6f) chips.Add("Heavy body");
            if (sound.transientSharpness > 0.58f) chips.Add("Sharp edge");
            if (sound.drive > 0.56f) chips.Add("Driven");
            if (sound.modDepth > 0.58f) chips.Add("Animated");
            if (sound.reverbBias > 0.58f) chips.Add("Bloom");
            if (sound.filterMotion > 0.58f) chips.Add("Moving filter");
            if (sound.delayBias > 0.46f) chips.Add("Echo pull");

            int max = Mathf.Min(chips.Count, 5);
            return string.Join("  |  ", chips.GetRange(0, max));
        }

        private string DescribeDirection(Vector3 worldPos)
        {
            if (_lookAtTarget == null)
                return "N/A";

            Vector3 relative = _lookAtTarget.InverseTransformPoint(worldPos);
            if (relative.sqrMagnitude < 0.0001f)
                return "Center";

            if (Mathf.Abs(relative.x) >= Mathf.Abs(relative.z))
                return relative.x >= 0f ? "Right" : "Left";

            return relative.z >= 0f ? "Front" : "Behind";
        }

        // --- Callbacks ---

        private void OnDepthChanged(float value)
        {
            if (_updating || _store == null) return;
            _store.UpdateInstance(_store.State.selectedInstanceId, depth: value);
        }

        private void OnToggleMute()
        {
            if (_store == null) return;
            var inst = _store.GetInstance(_store.State.selectedInstanceId);
            if (inst != null)
                _store.UpdateInstance(inst.id, muted: !inst.muted);
        }

        private void OnRemove()
        {
            if (_store == null) return;
            _store.RemoveInstance(_store.State.selectedInstanceId);
        }

        private void OnDuplicate()
        {
            if (_store == null) return;
            _store.DuplicateInstance(_store.State.selectedInstanceId);
        }

        private void OnPresetChanged(int value)
        {
            if (_updating || _store == null) return;
            string presetId = value > 0 && value <= InstrumentPresets.All.Count
                ? InstrumentPresets.All[value - 1].id : null;
            _store.SetPresetOverride(_store.State.selectedInstanceId, presetId);
        }
    }
}
