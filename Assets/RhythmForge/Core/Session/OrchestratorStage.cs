using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Session
{
    /// <summary>
    /// Owns the conducting layer of the spatial orchestrator:
    /// <list type="bullet">
    ///   <item>Tracks the conductor origin (follows the user's head with a slow EMA).</item>
    ///   <item>Tracks the "focused zone" — the zone nearest the stylus tip.</item>
    ///   <item>Applies crescendo / fade / cutoff gain modifiers per zone, smoothly lerped each frame.</item>
    /// </list>
    /// Call <see cref="UpdateFrame"/> once per frame.
    /// Query <see cref="GetGainMod"/> from the audio engine before each voice play.
    /// </summary>
    public class OrchestratorStage
    {
        // ── Tuning ────────────────────────────────────────────────────────────────

        /// <summary>Gain target applied by a Crescendo gesture.</summary>
        private const float CrescendoTarget  = 1.40f;
        /// <summary>Speed (units/s) at which gain lerps toward its target.</summary>
        private const float FadeSpeed        = 0.18f;   // ≈5.5 s to go from 1 → 0
        /// <summary>Speed (units/s) at which gain lerps toward crescendo target.</summary>
        private const float CrescendoSpeed   = 0.09f;   // ≈4.5 s to go from 1 → 1.4
        /// <summary>After a cutoff the gain gradually returns to 1.0 over this many seconds.</summary>
        private const float CutoffRestoreDelay  = 4.0f;
        private const float CutoffRestoreSpeed  = 0.10f;
        /// <summary>Radius multiplier: a zone is "in focus" if stylus is within this * zone.radius.</summary>
        private const float FocusRadiusMult  = 1.5f;
        /// <summary>EMA alpha for conductor-origin tracking (per second, not per frame).</summary>
        private const float OriginEmaAlpha   = 0.05f;

        // ── Private state ─────────────────────────────────────────────────────────

        private readonly SpatialZoneController _zoneController;

        private readonly Dictionary<string, float> _gainCurrent = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _gainTarget  = new Dictionary<string, float>();
        // Timer tracking how long since a cutoff was applied (per zone)
        private readonly Dictionary<string, float> _cutoffTimer = new Dictionary<string, float>();

        private Vector3 _conductorOrigin;
        private bool    _originInitialized;

        private string  _focusedZoneId;

        // ── Construction ──────────────────────────────────────────────────────────

        /// <param name="zoneController">The existing zone controller (owns zone list).</param>
        public OrchestratorStage(SpatialZoneController zoneController)
        {
            _zoneController = zoneController;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>The zone ID currently closest to the stylus tip, or null if none is near.</summary>
        public string FocusedZoneId => _focusedZoneId;

        /// <summary>Current conductor origin in world space.</summary>
        public Vector3 ConductorOrigin => _conductorOrigin;

        /// <summary>
        /// Call once per frame.
        /// </summary>
        /// <param name="headPosition">User's head (CenterEye) world position.</param>
        /// <param name="stylusTip">Stylus tip world position.</param>
        /// <param name="deltaTime">Time.deltaTime.</param>
        public void UpdateFrame(Vector3 headPosition, Vector3 stylusTip, float deltaTime)
        {
            UpdateConductorOrigin(headPosition, deltaTime);
            UpdateFocus(stylusTip);
            UpdateGainMods(deltaTime);
        }

        /// <summary>
        /// Returns the gain multiplier (0–1.5) for the zone that contains <paramref name="instanceId"/>.
        /// Returns 1.0 if the instance is not in any zone.
        /// </summary>
        public float GetGainMod(string instanceId)
        {
            if (instanceId == null || _zoneController == null) return 1f;
            string zoneId = _zoneController.GetZoneId(instanceId);
            if (zoneId == null) return 1f;
            return _gainCurrent.TryGetValue(zoneId, out float g) ? g : 1f;
        }

        /// <summary>Trigger a crescendo on the specified zone (or all zones).</summary>
        public void ApplyCrescendo(string zoneId, bool allZones)
        {
            if (allZones)
                ForEachZone(z => SetGainTarget(z.id, CrescendoTarget));
            else if (zoneId != null)
                SetGainTarget(zoneId, CrescendoTarget);
        }

        /// <summary>Trigger a fade-to-mute on the specified zone (or all zones).</summary>
        public void ApplyFade(string zoneId, bool allZones)
        {
            if (allZones)
                ForEachZone(z => SetGainTarget(z.id, 0f));
            else if (zoneId != null)
                SetGainTarget(zoneId, 0f);
        }

        /// <summary>Immediately mute the specified zone (or all zones) without lerping.</summary>
        public void ApplyCutoff(string zoneId, bool allZones)
        {
            if (allZones)
            {
                ForEachZone(z =>
                {
                    _gainCurrent[z.id] = 0f;
                    _gainTarget[z.id]  = 0f;
                    _cutoffTimer[z.id] = 0f;
                });
            }
            else if (zoneId != null)
            {
                _gainCurrent[zoneId] = 0f;
                _gainTarget[zoneId]  = 0f;
                _cutoffTimer[zoneId] = 0f;
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private void UpdateConductorOrigin(Vector3 headPos, float dt)
        {
            if (!_originInitialized)
            {
                _conductorOrigin    = headPos;
                _originInitialized  = true;
                return;
            }
            // Exponential moving average — moves slowly so the origin follows
            // gross room repositioning but ignores normal head movement.
            float alpha = 1f - Mathf.Pow(1f - OriginEmaAlpha, dt);
            _conductorOrigin = Vector3.Lerp(_conductorOrigin, headPos, alpha);
        }

        private void UpdateFocus(Vector3 stylusTip)
        {
            if (_zoneController == null) { _focusedZoneId = null; return; }

            string bestId   = null;
            float  bestDist = float.MaxValue;

            foreach (var zone in _zoneController.Zones)
            {
                float dist = Vector3.Distance(stylusTip, zone.center);
                if (dist <= zone.radius * FocusRadiusMult && dist < bestDist)
                {
                    bestDist = dist;
                    bestId   = zone.id;
                }
            }
            _focusedZoneId = bestId;
        }

        private void UpdateGainMods(float dt)
        {
            if (_zoneController == null) return;

            foreach (var zone in _zoneController.Zones)
            {
                string id = zone.id;

                if (!_gainCurrent.ContainsKey(id)) _gainCurrent[id] = 1f;
                if (!_gainTarget.ContainsKey(id))  _gainTarget[id]  = 1f;

                float current = _gainCurrent[id];
                float target  = _gainTarget[id];

                // Choose lerp speed based on direction
                float speed;
                if (target <= 0f)
                    speed = FadeSpeed;   // fade to mute
                else if (target > 1f)
                    speed = CrescendoSpeed;
                else
                    speed = FadeSpeed;   // returning to unity

                _gainCurrent[id] = Mathf.MoveTowards(current, target, speed * dt);

                // After a cutoff (target == 0), schedule a return to 1.0 after a delay
                if (target <= 0f && _gainCurrent[id] <= 0.001f)
                {
                    if (!_cutoffTimer.ContainsKey(id)) _cutoffTimer[id] = 0f;
                    _cutoffTimer[id] += dt;
                    if (_cutoffTimer[id] >= CutoffRestoreDelay)
                    {
                        _gainTarget[id]  = 1f;
                        _cutoffTimer.Remove(id);
                    }
                }
                else if (Mathf.Approximately(current, target) && target > 0.9f && target < 1.1f)
                {
                    // Settled at unity — snap exactly to avoid float drift
                    _gainCurrent[id] = 1f;
                    _gainTarget[id]  = 1f;
                }
                else if (Mathf.Approximately(current, target) && target > 1.35f)
                {
                    // Crescendo settled — gradually return to 1.0
                    _gainTarget[id] = 1f;
                }
            }
        }

        private void SetGainTarget(string zoneId, float target)
        {
            _gainTarget[zoneId] = target;
            if (!_gainCurrent.ContainsKey(zoneId))
                _gainCurrent[zoneId] = 1f;
            // For fade: remove cutoff restore timer if a new fade is applied
            if (target <= 0f)
                _cutoffTimer.Remove(zoneId);
        }

        private void ForEachZone(System.Action<SpatialZone> action)
        {
            foreach (var zone in _zoneController.Zones)
                action(zone);
        }
    }
}
