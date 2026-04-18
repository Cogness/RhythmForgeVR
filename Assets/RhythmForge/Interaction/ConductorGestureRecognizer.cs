using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmForge.Interaction
{
    /// <summary>
    /// Conducting gestures that the recognizer can detect.
    /// </summary>
    public enum ConductorGesture
    {
        /// <summary>Horizontal oscillation — nudges BPM toward the sway period.</summary>
        Sway,
        /// <summary>Slow upward extension — triggers a crescendo on the focused zone.</summary>
        Lift,
        /// <summary>Downward motion with fading pressure — fades the focused zone to mute.</summary>
        Fade,
        /// <summary>Sharp lateral chop above speed threshold — cuts the focused zone immediately.</summary>
        Cutoff
    }

    /// <summary>
    /// One recorded stylus pose sample used by the gesture recognizer.
    /// </summary>
    public struct ConductorPoseFrame
    {
        public Vector3 position;
        public float   pressure;
        public float   timestamp;
    }

    /// <summary>
    /// Lightweight heuristic gesture recognizer for conductor motions.
    /// No MonoBehaviour — tick it by calling <see cref="AddSample"/> each frame
    /// when conducting mode is active and the stylus is not drawing.
    /// Read detected gestures by calling <see cref="ConsumePendingGesture"/>.
    /// </summary>
    public class ConductorGestureRecognizer
    {
        // ── Tuning constants ────────────────────────────────────────────────────

        private const float HistoryWindow      = 3.0f;   // seconds of history kept
        private const float SwayWindow         = 2.0f;   // s — reversals counted inside this window
        private const float SwayMinTravel      = 0.04f;  // m — minimum X travel to count a sway reversal
        private const int   SwayMinReversals   = 2;      // at least 2 direction changes = one sway
        private const float LiftWindow         = 1.5f;   // s — window scanned for lift gesture
        private const float LiftMinRise        = 0.08f;  // m — minimum net upward travel
        private const float LiftMinUpRatio     = 0.60f;  // fraction of frames that must be rising
        private const float FadeWindow         = 1.5f;   // s — window scanned for fade gesture
        private const float FadeMinDrop        = 0.06f;  // m — minimum net downward travel
        private const float FadeMinDownRatio   = 0.60f;  // fraction of frames that must be falling
        private const float FadePressureEnd    = 0.12f;  // pressure must be below this at end
        private const float CutoffMinSpeed     = 0.9f;   // m/s lateral speed threshold
        private const float GestureCooldown    = 1.2f;   // s — minimum time between any two firings

        // ── State ────────────────────────────────────────────────────────────────

        private readonly List<ConductorPoseFrame> _history = new List<ConductorPoseFrame>(200);

        private ConductorGesture? _pendingGesture;
        private float _swayPeriodSeconds;

        // Per-gesture cooldowns
        private float _swayCooldown;
        private float _liftCooldown;
        private float _fadeCooldown;
        private float _cutoffCooldown;

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Add a new stylus world-position sample. Call once per frame when
        /// conducting mode is active and <c>DrawPressure &lt; 0.05</c>.
        /// </summary>
        public void AddSample(Vector3 worldPosition, float pressure, float timestamp)
        {
            _history.Add(new ConductorPoseFrame
            {
                position  = worldPosition,
                pressure  = pressure,
                timestamp = timestamp
            });

            // Prune frames older than HistoryWindow
            float cutoff = timestamp - HistoryWindow;
            while (_history.Count > 0 && _history[0].timestamp < cutoff)
                _history.RemoveAt(0);

            if (_history.Count < 3)
                return;

            int last = _history.Count - 1;
            float dt = _history[last].timestamp - _history[last - 1].timestamp;
            if (dt <= 0f) return;

            // Tick cooldowns
            _swayCooldown   = Mathf.Max(0f, _swayCooldown   - dt);
            _liftCooldown   = Mathf.Max(0f, _liftCooldown   - dt);
            _fadeCooldown   = Mathf.Max(0f, _fadeCooldown   - dt);
            _cutoffCooldown = Mathf.Max(0f, _cutoffCooldown - dt);

            // Priority: Cutoff > Lift > Fade > Sway
            if (_cutoffCooldown <= 0f)
            {
                float lateralDelta = Mathf.Abs(_history[last].position.x - _history[last - 1].position.x)
                                   + Mathf.Abs(_history[last].position.z - _history[last - 1].position.z);
                if (lateralDelta / dt > CutoffMinSpeed)
                {
                    EmitGesture(ConductorGesture.Cutoff);
                    _cutoffCooldown = GestureCooldown;
                    return;
                }
            }

            if (_liftCooldown <= 0f && TryLift())
            {
                EmitGesture(ConductorGesture.Lift);
                _liftCooldown = GestureCooldown;
                return;
            }

            if (_fadeCooldown <= 0f && TryFade())
            {
                EmitGesture(ConductorGesture.Fade);
                _fadeCooldown = GestureCooldown;
                return;
            }

            if (_swayCooldown <= 0f && TrySway())
            {
                EmitGesture(ConductorGesture.Sway);
                _swayCooldown = GestureCooldown;
            }
        }

        /// <summary>
        /// Returns the most recently detected gesture and clears it.
        /// Returns <c>null</c> if no gesture is pending.
        /// </summary>
        public ConductorGesture? ConsumePendingGesture()
        {
            var result = _pendingGesture;
            _pendingGesture = null;
            return result;
        }

        /// <summary>
        /// The estimated period (in seconds) of the last detected sway.
        /// Use to derive BPM nudge: BPM ≈ 60 / (SwayPeriodSeconds * 0.5).
        /// </summary>
        public float SwayPeriodSeconds => _swayPeriodSeconds;

        /// <summary>Clears all history and resets cooldowns.</summary>
        public void Clear()
        {
            _history.Clear();
            _pendingGesture = null;
            _swayCooldown = _liftCooldown = _fadeCooldown = _cutoffCooldown = 0f;
        }

        // ── Gesture detectors ────────────────────────────────────────────────────

        private bool TryLift()
        {
            if (_history.Count < 8) return false;

            float now         = _history[_history.Count - 1].timestamp;
            float windowStart = now - LiftWindow;

            float startY       = float.NaN;
            float startPressure = 0f;
            int   upFrames     = 0;
            int   totalFrames  = 0;

            for (int i = 0; i < _history.Count; i++)
            {
                if (_history[i].timestamp < windowStart) continue;

                if (float.IsNaN(startY))
                {
                    startY        = _history[i].position.y;
                    startPressure = _history[i].pressure;
                    continue;
                }

                totalFrames++;
                if (_history[i].position.y > _history[i - 1].position.y)
                    upFrames++;
            }

            if (float.IsNaN(startY) || totalFrames < 4) return false;

            float endY       = _history[_history.Count - 1].position.y;
            float endPressure = _history[_history.Count - 1].pressure;
            float netRise    = endY - startY;
            float upRatio    = (float)upFrames / totalFrames;

            return netRise >= LiftMinRise
                && upRatio >= LiftMinUpRatio
                && endPressure >= startPressure - 0.15f; // pressure not dropping hard
        }

        private bool TryFade()
        {
            if (_history.Count < 8) return false;

            float now         = _history[_history.Count - 1].timestamp;
            float windowStart = now - FadeWindow;

            float startY      = float.NaN;
            int   downFrames  = 0;
            int   totalFrames = 0;

            for (int i = 0; i < _history.Count; i++)
            {
                if (_history[i].timestamp < windowStart) continue;

                if (float.IsNaN(startY))
                {
                    startY = _history[i].position.y;
                    continue;
                }

                totalFrames++;
                if (_history[i].position.y < _history[i - 1].position.y)
                    downFrames++;
            }

            if (float.IsNaN(startY) || totalFrames < 4) return false;

            float endY       = _history[_history.Count - 1].position.y;
            float endPressure = _history[_history.Count - 1].pressure;
            float netDrop    = startY - endY;
            float downRatio  = (float)downFrames / totalFrames;

            return netDrop >= FadeMinDrop
                && downRatio >= FadeMinDownRatio
                && endPressure < FadePressureEnd;
        }

        private bool TrySway()
        {
            if (_history.Count < 12) return false;

            float now         = _history[_history.Count - 1].timestamp;
            float windowStart = now - SwayWindow;

            // State-machine: accumulate travel; count direction reversals.
            float refX       = float.NaN;
            int   direction  = 0;  // -1 left, +1 right, 0 = not set
            int   reversals  = 0;
            float firstRevTimestamp = float.NaN;
            float lastRevTimestamp  = float.NaN;

            for (int i = 0; i < _history.Count; i++)
            {
                if (_history[i].timestamp < windowStart) continue;

                float x = _history[i].position.x;
                if (float.IsNaN(refX)) { refX = x; continue; }

                float displacement = x - refX;
                if (Mathf.Abs(displacement) < SwayMinTravel) continue;

                int newDir = displacement > 0f ? 1 : -1;

                if (direction == 0)
                {
                    direction = newDir;
                }
                else if (newDir != direction)
                {
                    reversals++;
                    direction = newDir;
                    if (float.IsNaN(firstRevTimestamp))
                        firstRevTimestamp = _history[i].timestamp;
                    lastRevTimestamp = _history[i].timestamp;
                }

                // Reset refX to current position after counting
                refX = x;
            }

            if (reversals < SwayMinReversals) return false;

            // Estimate period: time between first and last reversal spans (reversals-1) half-cycles
            if (!float.IsNaN(firstRevTimestamp) && !float.IsNaN(lastRevTimestamp) && reversals >= 2)
            {
                float span         = lastRevTimestamp - firstRevTimestamp;
                float halfCycles   = reversals - 1f;
                float halfPeriod   = span / halfCycles;
                _swayPeriodSeconds = halfPeriod * 2f; // full period
            }
            else
            {
                _swayPeriodSeconds = SwayWindow / (reversals / 2f);
            }

            return true;
        }

        private void EmitGesture(ConductorGesture gesture)
        {
            _pendingGesture = gesture;
        }
    }
}
