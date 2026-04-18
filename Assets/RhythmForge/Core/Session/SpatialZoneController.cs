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

        private readonly Dictionary<string, string> _instanceZoneIds = new Dictionary<string, string>();
        private readonly Dictionary<string, SpatialZoneVisualizer> _visualizers = new Dictionary<string, SpatialZoneVisualizer>();

        private SessionStore _store;
        private SpatialZoneLayout _layout;
        private AudioEngine _audioEngine;

        public static SpatialZoneController Shared { get; private set; }

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

        public bool TryGetDefaultPlacementFor(PatternType type, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (_layout?.Zones == null)
                return false;

            foreach (var zone in _layout.Zones)
            {
                if (zone != null && zone.MatchesTarget(type))
                {
                    worldPosition = transform.TransformPoint(zone.center);
                    return true;
                }
            }

            return false;
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
                    Destroy(existing.gameObject);
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
    }
}
