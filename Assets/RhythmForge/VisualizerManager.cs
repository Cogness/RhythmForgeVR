using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;
using RhythmForge.UI;

namespace RhythmForge
{
    internal sealed class VisualizerManager
    {
        private readonly SessionStore _store;
        private readonly Sequencer.Sequencer _sequencer;
        private readonly Transform _instanceContainer;
        private readonly Transform _userHead;
        private readonly Func<PatternType, Material> _getMaterialForType;
        private readonly Dictionary<string, PatternVisualizer> _visualizers = new Dictionary<string, PatternVisualizer>();

        public VisualizerManager(
            SessionStore store,
            Sequencer.Sequencer sequencer,
            Transform instanceContainer,
            Transform userHead,
            Func<PatternType, Material> getMaterialForType)
        {
            _store = store;
            _sequencer = sequencer;
            _instanceContainer = instanceContainer;
            _userHead = userHead;
            _getMaterialForType = getMaterialForType;
        }

        public void RebuildInstanceVisuals(bool showParamLabels)
        {
            if (_store == null)
                return;

            string visibleSceneId = ResolveVisibleSceneId();
            var sceneInstances = _store.GetSceneInstances(visibleSceneId);
            var activeIds = new HashSet<string>();

            foreach (var instance in sceneInstances)
            {
                activeIds.Add(instance.id);
                var pattern = _store.GetPattern(instance.patternId);
                if (pattern == null)
                    continue;

                var effectiveSound = _store.GetEffectiveSoundProfile(instance, pattern);
                if (_visualizers.TryGetValue(instance.id, out var existing))
                {
                    existing.RefreshGeometry(pattern, instance, _getMaterialForType(pattern.type), _userHead);
                    existing.SetMuted(instance.muted);
                    existing.SetSelected(instance.id == _store.State.selectedInstanceId);
                    existing.SetParameterLabelVisible(showParamLabels);
                    existing.UpdateParameterData(pattern.type, pattern.shapeProfile, effectiveSound);
                    continue;
                }

                var visualObject = new GameObject($"Instance_{instance.id}");
                if (_instanceContainer)
                    visualObject.transform.SetParent(_instanceContainer);

                var visualizer = visualObject.AddComponent<PatternVisualizer>();
                visualizer.Initialize(pattern, instance, _getMaterialForType(pattern.type), _userHead);
                visualizer.SetMuted(instance.muted);
                visualizer.SetSelected(instance.id == _store.State.selectedInstanceId);
                visualizer.SetParameterLabelVisible(showParamLabels);
                visualizer.UpdateParameterData(pattern.type, pattern.shapeProfile, effectiveSound);
                _visualizers[instance.id] = visualizer;
            }

            var toRemove = new List<string>();
            foreach (var pair in _visualizers)
            {
                if (!activeIds.Contains(pair.Key))
                    toRemove.Add(pair.Key);
            }

            foreach (var id in toRemove)
            {
                if (!_visualizers.TryGetValue(id, out var visualizer))
                    continue;

                UnityEngine.Object.Destroy(visualizer.gameObject);
                _visualizers.Remove(id);
            }
        }

        public void UpdatePlaybackVisuals()
        {
            if (_store == null)
                return;

            foreach (var pair in _visualizers)
            {
                var instance = _store.GetInstance(pair.Key);
                var pattern = instance != null ? _store.GetPattern(instance.patternId) : null;
                if (instance == null || pattern == null)
                    continue;

                if (_sequencer != null && _sequencer.TryGetPlaybackVisualState(pattern, pair.Key, out var state))
                {
                    pair.Value.SetPlaybackState(state);
                    continue;
                }

                var effectiveSound = _store.GetEffectiveSoundProfile(instance, pattern);
                pair.Value.SetPlaybackState(
                    Sequencer.PatternPlaybackVisualState.CreateInactive(
                        pattern.type,
                        effectiveSound,
                        _sequencer?.GetPlaybackSceneId()));
            }
        }

        public string ResolveVisibleSceneId()
        {
            if (_sequencer != null && _sequencer.IsPlaying)
                return _sequencer.GetPlaybackSceneId() ?? _store.State.activeSceneId;

            return _store.State.activeSceneId;
        }

        public void SetParameterLabelVisible(bool visible)
        {
            foreach (var visualizer in _visualizers.Values)
                visualizer.SetParameterLabelVisible(visible);
        }

        public void Dispose()
        {
            foreach (var visualizer in _visualizers.Values)
            {
                if (visualizer != null)
                    UnityEngine.Object.Destroy(visualizer.gameObject);
            }

            _visualizers.Clear();
        }
    }
}
