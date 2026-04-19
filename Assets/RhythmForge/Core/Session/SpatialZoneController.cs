using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Audio;
using RhythmForge.Core.Data;
using RhythmForge.UI;

namespace RhythmForge.Core.Session
{
    public class SpatialZoneController : MonoBehaviour
    {
        private const float BoundaryDeadband = 0.10f;
        private const string AllZonesSentinel = "*";

        private struct ConductorState
        {
            public float liveGainMult;
            public float liveReverbBoost;
            public float decayStartGainMult;
            public float decayStartReverbBoost;
            public int decayStartBar;
            public int decayBars;
            public bool liveCutArmed;
            public int cutActiveBar;
        }

        private readonly Dictionary<string, string> _instanceZoneIds = new Dictionary<string, string>();
        private readonly Dictionary<string, SpatialZoneVisualizer> _visualizers = new Dictionary<string, SpatialZoneVisualizer>();
        private readonly Dictionary<string, ConductorState> _conductorStates = new Dictionary<string, ConductorState>();

        private SessionStore _store;
        private SpatialZoneLayout _layout;
        private AudioEngine _audioEngine;
        private int _currentAbsoluteBar = 1;

        public static SpatialZoneController Shared { get; private set; }
        public SessionStore Store => _store;

        public void Initialize(SessionStore store, SpatialZoneLayout layout, Transform headTransform, AudioEngine audioEngine = null)
        {
            _store = store;
            _layout = layout != null ? layout : (_layout != null ? _layout : SpatialZoneLayout.CreateDefault());
            BindAudioEngine(audioEngine);
            RebuildVisualizers();

            if (headTransform != null)
            {
                Vector3 forward = headTransform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

                Vector3 position = transform.position;
                position.x = headTransform.position.x;
                position.z = headTransform.position.z;
                transform.position = position;
            }

            RefreshAllAssignments();
        }

        public void BindAudioEngine(AudioEngine audioEngine)
        {
            if (_audioEngine != null)
                _audioEngine.OnEventScheduled -= HandleEventScheduled;

            _audioEngine = audioEngine;

            if (_audioEngine != null)
                _audioEngine.OnEventScheduled += HandleEventScheduled;
        }

        public bool TryGetZoneFor(string instanceId, out SpatialZone zone)
        {
            zone = null;
            if (string.IsNullOrEmpty(instanceId) || _layout?.Zones == null)
                return false;

            if (!_instanceZoneIds.TryGetValue(instanceId, out var zoneId))
                return false;

            zone = GetZoneById(zoneId);
            return zone != null;
        }

        public SpatialZone GetZoneFor(string instanceId)
        {
            TryGetZoneFor(instanceId, out var zone);
            return zone;
        }

        public bool TryGetDefaultPlacementFor(PatternType type, Vector3? sourceWorldPosition, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (_layout?.Zones == null)
                return false;

            Vector3? localPosition = null;
            if (sourceWorldPosition.HasValue)
                localPosition = transform.InverseTransformPoint(sourceWorldPosition.Value);

            SpatialZone best = null;
            float bestDistance = float.MaxValue;
            foreach (var zone in _layout.Zones)
            {
                if (zone == null || !zone.MatchesTarget(type, localPosition))
                    continue;

                if (!localPosition.HasValue)
                {
                    best = zone;
                    break;
                }

                float distance = Vector3.Distance(zone.center, localPosition.Value);
                if (distance >= bestDistance)
                    continue;

                best = zone;
                bestDistance = distance;
            }

            if (best == null)
                return false;

            worldPosition = transform.TransformPoint(best.center);
            return true;
        }

        public bool TryGetDefaultPlacementFor(PatternType type, out Vector3 worldPosition)
        {
            return TryGetDefaultPlacementFor(type, null, out worldPosition);
        }

        public SpatialZone ResolveZoneForPosition(string instanceId, Vector3 worldPosition)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;

            if (_layout?.Zones == null || _layout.Zones.Count == 0)
            {
                _instanceZoneIds.Remove(instanceId);
                return null;
            }

            SpatialZone current = GetZoneFor(instanceId);
            if (current != null)
            {
                float retainRadius = Mathf.Max(0.01f, current.radius + BoundaryDeadband);
                if ((worldPosition - transform.TransformPoint(current.center)).sqrMagnitude <= retainRadius * retainRadius)
                {
                    _instanceZoneIds[instanceId] = current.id;
                    return current;
                }
            }

            SpatialZone best = null;
            float bestDistanceSq = float.MaxValue;
            foreach (var zone in _layout.Zones)
            {
                if (zone == null)
                    continue;

                Vector3 center = transform.TransformPoint(zone.center);
                float distanceSq = (worldPosition - center).sqrMagnitude;
                if (distanceSq > zone.radius * zone.radius || distanceSq >= bestDistanceSq)
                    continue;

                best = zone;
                bestDistanceSq = distanceSq;
            }

            if (best != null)
                _instanceZoneIds[instanceId] = best.id;
            else
                _instanceZoneIds.Remove(instanceId);

            return best;
        }

        public void RefreshAllAssignments()
        {
            if (_store?.State?.instances == null)
                return;

            var liveIds = new HashSet<string>();
            foreach (var instance in _store.State.instances)
            {
                if (instance == null || string.IsNullOrEmpty(instance.id))
                    continue;

                liveIds.Add(instance.id);
                var zone = ResolveZoneForPosition(instance.id, instance.position);
                instance.currentZoneId = zone?.id;
            }

            var staleIds = new List<string>();
            foreach (var pair in _instanceZoneIds)
            {
                if (!liveIds.Contains(pair.Key))
                    staleIds.Add(pair.Key);
            }

            foreach (var id in staleIds)
                _instanceZoneIds.Remove(id);
        }

        public void Recentre(Pose headPose)
        {
            Vector3 forward = headPose.rotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

            Vector3 position = transform.position;
            position.x = headPose.position.x;
            position.z = headPose.position.z;
            transform.position = position;

            RefreshVisualizerTransforms();
            RefreshAllAssignments();
        }

        public void ApplyConductorGesture(string zoneIdOrAll, ConductorGestureKind kind, float magnitude)
        {
            if (_layout?.Zones == null)
                return;

            if (string.Equals(zoneIdOrAll, AllZonesSentinel, System.StringComparison.Ordinal))
            {
                foreach (var zone in _layout.Zones)
                {
                    if (zone != null && !string.IsNullOrEmpty(zone.id))
                    {
                        ApplyConductorGestureToZone(zone.id, kind, magnitude);
                        if (_visualizers.TryGetValue(zone.id, out var allVisualizer))
                            allVisualizer.Pulse();
                    }
                }
                return;
            }

            ApplyConductorGestureToZone(zoneIdOrAll, kind, magnitude);
            if (_visualizers.TryGetValue(zoneIdOrAll, out var visualizer))
                visualizer.Pulse();
        }

        public void GetLiveBiases(string zoneId, out float gainMult, out float reverbBoost, out bool cutActive)
        {
            gainMult = 1f;
            reverbBoost = 0f;
            cutActive = false;

            if (string.IsNullOrEmpty(zoneId))
                return;

            var state = GetOrCreateConductorState(zoneId);
            gainMult = Mathf.Max(0.01f, state.liveGainMult);
            reverbBoost = Mathf.Max(0f, state.liveReverbBoost);
            cutActive = state.cutActiveBar == _currentAbsoluteBar;
        }

        public void OnBarStart(int absoluteBar, double dspTime)
        {
            _currentAbsoluteBar = Mathf.Max(1, absoluteBar);
            if (_layout?.Zones == null)
                return;

            foreach (var zone in _layout.Zones)
            {
                if (zone == null || string.IsNullOrEmpty(zone.id))
                    continue;

                var state = GetOrCreateConductorState(zone.id);

                if (state.decayBars > 0)
                {
                    float progress = Mathf.Clamp01((_currentAbsoluteBar - state.decayStartBar) / (float)state.decayBars);
                    state.liveGainMult = Mathf.Lerp(state.decayStartGainMult, 1f, progress);
                    state.liveReverbBoost = Mathf.Lerp(state.decayStartReverbBoost, 0f, progress);
                    if (progress >= 1f)
                    {
                        state.liveGainMult = 1f;
                        state.liveReverbBoost = 0f;
                        state.decayBars = 0;
                    }
                }

                if (state.liveCutArmed)
                {
                    state.cutActiveBar = _currentAbsoluteBar;
                    state.liveCutArmed = false;
                }
                else if (state.cutActiveBar > 0 && _currentAbsoluteBar > state.cutActiveBar)
                {
                    state.cutActiveBar = -1;
                }

                _conductorStates[zone.id] = state;
            }
        }

        public string GetClosestZoneId(Vector3 worldPosition, float maxDistanceMeters)
        {
            if (_layout?.Zones == null || _layout.Zones.Count == 0)
                return null;

            string bestZoneId = null;
            float bestDistanceSq = maxDistanceMeters * maxDistanceMeters;
            foreach (var zone in _layout.Zones)
            {
                if (zone == null || string.IsNullOrEmpty(zone.id))
                    continue;

                float distanceSq = (worldPosition - transform.TransformPoint(zone.center)).sqrMagnitude;
                if (distanceSq > bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestZoneId = zone.id;
            }

            return bestZoneId;
        }

        private void Awake()
        {
            Shared = this;
        }

        private void LateUpdate()
        {
            RefreshAllAssignments();
        }

        private void OnDestroy()
        {
            if (Shared == this)
                Shared = null;

            if (_audioEngine != null)
                _audioEngine.OnEventScheduled -= HandleEventScheduled;
        }

        private SpatialZone GetZoneById(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId) || _layout?.Zones == null)
                return null;

            foreach (var zone in _layout.Zones)
            {
                if (zone != null && zone.id == zoneId)
                    return zone;
            }

            return null;
        }

        private void RebuildVisualizers()
        {
            foreach (var existing in _visualizers.Values)
            {
                if (existing != null)
                {
                    if (Application.isPlaying)
                        Destroy(existing.gameObject);
                    else
                        DestroyImmediate(existing.gameObject);
                }
            }
            _visualizers.Clear();

            if (_layout?.Zones == null)
                return;

            foreach (var zone in _layout.Zones)
            {
                if (zone == null || string.IsNullOrEmpty(zone.id))
                    continue;

                var go = new GameObject(zone.id);
                go.transform.SetParent(transform, false);
                var visualizer = go.AddComponent<SpatialZoneVisualizer>();
                visualizer.Initialize(zone);
                _visualizers[zone.id] = visualizer;
                GetOrCreateConductorState(zone.id);
            }
        }

        private void RefreshVisualizerTransforms()
        {
            foreach (var visualizer in _visualizers.Values)
                visualizer?.RefreshPose();
        }

        private void HandleEventScheduled(string instanceId)
        {
            if (!TryGetZoneFor(instanceId, out var zone))
                return;

            if (_visualizers.TryGetValue(zone.id, out var visualizer))
                visualizer.Pulse();
        }

        private void ApplyConductorGestureToZone(string zoneId, ConductorGestureKind kind, float magnitude)
        {
            if (string.IsNullOrEmpty(zoneId))
                return;

            var state = GetOrCreateConductorState(zoneId);
            switch (kind)
            {
                case ConductorGestureKind.LiftTendu:
                    state.liveGainMult = Mathf.Clamp(state.liveGainMult * Mathf.Lerp(1.10f, 1.35f, magnitude), 0.5f, 1.35f);
                    state.decayStartGainMult = state.liveGainMult;
                    state.decayStartReverbBoost = state.liveReverbBoost;
                    state.decayStartBar = _currentAbsoluteBar;
                    state.decayBars = 4;
                    break;
                case ConductorGestureKind.FadePlie:
                    state.liveGainMult = Mathf.Clamp(state.liveGainMult * Mathf.Lerp(0.90f, 0.50f, magnitude), 0.5f, 1.35f);
                    state.decayStartGainMult = state.liveGainMult;
                    state.decayStartReverbBoost = state.liveReverbBoost;
                    state.decayStartBar = _currentAbsoluteBar;
                    state.decayBars = 4;
                    break;
                case ConductorGestureKind.CutOff:
                    state.liveCutArmed = true;
                    break;
            }

            _conductorStates[zoneId] = state;
        }

        private ConductorState GetOrCreateConductorState(string zoneId)
        {
            if (!_conductorStates.TryGetValue(zoneId, out var state))
            {
                state = new ConductorState
                {
                    liveGainMult = 1f,
                    liveReverbBoost = 0f,
                    decayStartGainMult = 1f,
                    decayStartReverbBoost = 0f,
                    decayStartBar = _currentAbsoluteBar,
                    decayBars = 0,
                    liveCutArmed = false,
                    cutActiveBar = -1
                };
                _conductorStates[zoneId] = state;
            }

            return state;
        }
    }
}
