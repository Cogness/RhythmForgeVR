#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Interaction;

namespace RhythmForge.Editor
{
    /// <summary>
    /// NUnit edit-mode tests for ConductorGestureRecognizer gesture detection.
    /// All tests drive the recognizer with synthetic position / pressure sequences
    /// at a stable simulated 60 Hz.
    /// </summary>
    public class ConductorGestureRecognizerTests
    {
        private const float Dt = 1f / 60f;  // simulated frame interval

        // ── Helper: build recognizer pre-loaded with N neutral samples ─────────

        private static ConductorGestureRecognizer Primed(int warmupFrames = 5)
        {
            var r = new ConductorGestureRecognizer();
            float t = 0f;
            for (int i = 0; i < warmupFrames; i++, t += Dt)
                r.AddSample(Vector3.zero, 0f, t);
            return r;
        }

        // ── Sway ──────────────────────────────────────────────────────────────

        [Test]
        public void ThreeDirectionReversals_DetectsSway()
        {
            var r = new ConductorGestureRecognizer();
            float t = 0f;

            // Start with a few neutral samples to prime the history
            for (int i = 0; i < 5; i++, t += Dt)
                r.AddSample(Vector3.zero, 0f, t);

            // Swing right → left → right → left (3 direction reversals, 0.06 m each leg)
            float[] xs = { 0.08f, 0.16f, 0.08f, 0f, -0.08f, 0f, 0.08f, 0.16f };
            foreach (float x in xs)
            {
                r.AddSample(new Vector3(x, 0f, 0f), 0f, t);
                t += Dt * 8; // spread out so each leg covers ~8 frames
            }

            // Pump more frames to trigger detection check
            for (int i = 0; i < 10; i++, t += Dt)
                r.AddSample(new Vector3(0.16f + i * 0.001f, 0f, 0f), 0f, t);

            var gesture = r.ConsumePendingGesture();
            Assert.That(gesture.HasValue && gesture.Value == ConductorGesture.Sway,
                "Three horizontal reversals should produce a Sway gesture");
        }

        [Test]
        public void SteadyForwardMotion_NoSway()
        {
            var r = Primed();
            float t = 5 * Dt; // after warmup

            // Move steadily in Z only — no X oscillation
            for (int i = 0; i < 80; i++, t += Dt)
                r.AddSample(new Vector3(0f, 0f, i * 0.003f), 0f, t);

            var gesture = r.ConsumePendingGesture();
            Assert.That(gesture == null || gesture.Value != ConductorGesture.Sway,
                "Steady forward motion with no lateral reversals should not detect Sway");
        }

        // ── Lift ──────────────────────────────────────────────────────────────

        [Test]
        public void MonotonicRise_DetectsLift()
        {
            var r = new ConductorGestureRecognizer();
            float t = 0f;

            // Warm up with 20 neutral samples (enough history to satisfy LiftWindow check)
            for (int i = 0; i < 20; i++, t += Dt)
                r.AddSample(new Vector3(0f, 0f, 0f), 0.3f, t);

            // Steadily rise 12 cm over 70 frames with consistent pressure
            for (int i = 0; i < 70; i++, t += Dt)
            {
                float y = i * (0.12f / 70f);
                r.AddSample(new Vector3(0f, y, 0f), 0.3f + i * 0.003f, t);
            }

            var gesture = r.ConsumePendingGesture();
            Assert.That(gesture.HasValue && gesture.Value == ConductorGesture.Lift,
                "Monotonic upward rise with stable pressure should detect Lift");
        }

        // ── Fade ──────────────────────────────────────────────────────────────

        [Test]
        public void MonotonicDropWithFadingPressure_DetectsFade()
        {
            var r = new ConductorGestureRecognizer();
            float t = 0f;

            // Warm up with 20 samples at medium height and pressure
            for (int i = 0; i < 20; i++, t += Dt)
                r.AddSample(new Vector3(0f, 0.5f, 0f), 0.4f, t);

            // Steadily drop 10 cm over 70 frames while pressure fades to ~0
            for (int i = 0; i < 70; i++, t += Dt)
            {
                float y        = 0.5f - i * (0.10f / 70f);
                float pressure = Mathf.Lerp(0.4f, 0.0f, i / 70f);
                r.AddSample(new Vector3(0f, y, 0f), pressure, t);
            }

            var gesture = r.ConsumePendingGesture();
            Assert.That(gesture.HasValue && gesture.Value == ConductorGesture.Fade,
                "Downward motion with fading pressure ending near zero should detect Fade");
        }

        // ── Cutoff ────────────────────────────────────────────────────────────

        [Test]
        public void SharpLateralChop_DetectsCutoff()
        {
            var r = Primed();
            float t = 5 * Dt;

            // Hold still for a moment
            for (int i = 0; i < 10; i++, t += Dt)
                r.AddSample(new Vector3(0f, 1f, 0f), 0f, t);

            // Sharp lateral chop: move 0.08 m in a single Dt frame → speed ≈ 4.8 m/s
            r.AddSample(new Vector3(0.08f, 1f, 0f), 0f, t);

            var gesture = r.ConsumePendingGesture();
            Assert.That(gesture.HasValue && gesture.Value == ConductorGesture.Cutoff,
                "A single-frame lateral displacement exceeding speed threshold should fire Cutoff");
        }
    }
}
#endif
