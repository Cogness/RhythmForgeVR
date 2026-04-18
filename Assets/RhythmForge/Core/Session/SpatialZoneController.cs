using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;

namespace RhythmForge.Core.Session
{
    /// <summary>
    /// Tracks which SpatialZone each pattern instance occupies and exposes per-instance
    /// bus FX overrides for the audio engine.
    /// </summary>
    public class SpatialZoneController
    {
        private readonly List<SpatialZone> _zones;
        private readonly Dictionary<string, string> _instanceZoneMap = new Dictionary<string, string>();
        private RhythmForgeEventBus _eventBus;

        public IReadOnlyList<SpatialZone> Zones => _zones;

        public SpatialZoneController(List<SpatialZone> zones, RhythmForgeEventBus eventBus = null)
        {
            _zones    = zones ?? new List<SpatialZone>();
            _eventBus = eventBus;
        }

        /// <summary>
        /// Call every frame (or on instance move) with the current set of instances.
        /// Updates zone membership and fires SpatialZoneChangedEvent when membership changes.
        /// </summary>
        public void EvaluateAll(IEnumerable<PatternInstance> instances)
        {
            foreach (var instance in instances)
                EvaluateOne(instance);
        }

        private void EvaluateOne(PatternInstance instance)
        {
            string newZoneId = null;
            float nearest = float.MaxValue;

            foreach (var zone in _zones)
            {
                float dist = Vector3.Distance(instance.position, zone.center);
                if (dist <= zone.radius && dist < nearest)
                {
                    nearest  = dist;
                    newZoneId = zone.id;
                }
            }

            _instanceZoneMap.TryGetValue(instance.id, out string prevZoneId);

            if (newZoneId != prevZoneId)
            {
                if (newZoneId == null)
                    _instanceZoneMap.Remove(instance.id);
                else
                    _instanceZoneMap[instance.id] = newZoneId;

                _eventBus?.Publish(new SpatialZoneChangedEvent(instance.id, prevZoneId, newZoneId));
            }
        }

        /// <summary>
        /// Returns the bus FX contribution for the given instance, or a zero profile
        /// if the instance is not inside any zone.
        /// </summary>
        public ZoneBusFxProfile GetZoneFx(string instanceId)
        {
            if (instanceId == null || !_instanceZoneMap.TryGetValue(instanceId, out string zoneId))
                return default;

            foreach (var zone in _zones)
                if (zone.id == zoneId)
                    return zone.busFx;

            return default;
        }

        /// <summary>
        /// Returns the current zone id for an instance, or null if it is outside all zones.
        /// </summary>
        public string GetZoneId(string instanceId)
        {
            _instanceZoneMap.TryGetValue(instanceId, out var zoneId);
            return zoneId;
        }

        /// <summary>
        /// Returns a spawn position biased toward the zone whose defaultType matches
        /// the given pattern type. Falls back to strokeCenter if no matching zone exists.
        /// </summary>
        public Vector3 GetDefaultSpawnPosition(PatternType type, Vector3 strokeCenter)
        {
            SpatialZone best = null;
            foreach (var zone in _zones)
            {
                if (zone.defaultType == type)
                {
                    if (best == null ||
                        Vector3.Distance(strokeCenter, zone.center) <
                        Vector3.Distance(strokeCenter, best.center))
                        best = zone;
                }
            }

            if (best == null) return strokeCenter;

            // Nudge 40% toward the zone center so the spawn is biased but not teleported.
            return Vector3.Lerp(strokeCenter, best.center, 0.40f);
        }

        /// <summary>Sets the event bus (can be set after construction once the store is ready).</summary>
        public void SetEventBus(RhythmForgeEventBus bus) => _eventBus = bus;
    }
}
