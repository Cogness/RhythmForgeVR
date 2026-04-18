#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;

namespace RhythmForge.Editor
{
    /// <summary>
    /// NUnit edit-mode tests for OrchestratorStage zone focus and gain modulation.
    /// </summary>
    public class OrchestratorStageTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a SpatialZoneController with two zones:
        ///   "zone-a" centred at (0, 0, 0), radius 1.0
        ///   "zone-b" centred at (5, 0, 0), radius 1.0
        /// </summary>
        private static (SpatialZoneController ctrl, OrchestratorStage stage) BuildStage()
        {
            var zones = new List<SpatialZone>
            {
                new SpatialZone { id = "zone-a", displayName = "A", center = new Vector3(0, 0, 0), radius = 1.0f, defaultType = PatternType.RhythmLoop },
                new SpatialZone { id = "zone-b", displayName = "B", center = new Vector3(5, 0, 0), radius = 1.0f, defaultType = PatternType.MelodyLine }
            };
            var ctrl  = new SpatialZoneController(zones);
            var stage = new OrchestratorStage(ctrl);
            return (ctrl, stage);
        }

        // ── Focus ─────────────────────────────────────────────────────────────────

        [Test]
        public void StylusTipNearZoneA_FocusIsZoneA()
        {
            var (_, stage) = BuildStage();
            // UpdateFrame with stylus inside zone-a (radius = 1, tip at 0.5 m from center)
            stage.UpdateFrame(Vector3.up * 1.6f, new Vector3(0.5f, 0f, 0f), 0.016f);
            Assert.That(stage.FocusedZoneId, Is.EqualTo("zone-a"),
                "Stylus at (0.5,0,0) should focus zone-a centred at (0,0,0)");
        }

        [Test]
        public void StylusTipNearZoneB_FocusIsZoneB()
        {
            var (_, stage) = BuildStage();
            stage.UpdateFrame(Vector3.up * 1.6f, new Vector3(5.3f, 0f, 0f), 0.016f);
            Assert.That(stage.FocusedZoneId, Is.EqualTo("zone-b"),
                "Stylus at (5.3,0,0) should focus zone-b centred at (5,0,0)");
        }

        [Test]
        public void StylusFarFromAllZones_FocusIsNull()
        {
            var (_, stage) = BuildStage();
            stage.UpdateFrame(Vector3.up * 1.6f, new Vector3(20f, 0f, 0f), 0.016f);
            Assert.That(stage.FocusedZoneId, Is.Null,
                "Stylus far from all zones should give null focus");
        }

        // ── Crescendo ────────────────────────────────────────────────────────────

        [Test]
        public void ApplyCrescendo_RaisesGainTargetAboveOne()
        {
            var (ctrl, stage) = BuildStage();

            // Register a dummy instance in zone-a via the zone controller
            var inst = new PatternInstance { id = "inst-1", position = new Vector3(0.1f, 0f, 0f) };
            ctrl.EvaluateAll(new[] { inst });

            // Default gain should be 1.0 before any gesture
            Assert.That(stage.GetGainMod("inst-1"), Is.EqualTo(1f).Within(0.01f),
                "Default gain mod should be 1.0");

            stage.ApplyCrescendo("zone-a", allZones: false);

            // Tick several frames so gain starts rising
            for (int i = 0; i < 30; i++)
                stage.UpdateFrame(Vector3.zero, Vector3.zero, 0.016f);

            float gainAfterCrescendo = stage.GetGainMod("inst-1");
            Assert.That(gainAfterCrescendo, Is.GreaterThan(1.0f),
                "Crescendo should raise gain above 1.0 after several frames");
        }

        // ── Fade ────────────────────────────────────────────────────────────────

        [Test]
        public void ApplyFade_ReducesGainTowardZero()
        {
            var (ctrl, stage) = BuildStage();

            var inst = new PatternInstance { id = "inst-2", position = new Vector3(0.1f, 0f, 0f) };
            ctrl.EvaluateAll(new[] { inst });

            stage.ApplyFade("zone-a", allZones: false);

            // After many frames the gain should approach 0
            for (int i = 0; i < 200; i++)
                stage.UpdateFrame(Vector3.zero, Vector3.zero, 0.016f);

            Assert.That(stage.GetGainMod("inst-2"), Is.LessThan(0.05f),
                "After ApplyFade and sufficient frames, gain should be near zero");
        }

        // ── Cutoff ───────────────────────────────────────────────────────────────

        [Test]
        public void ApplyCutoff_ImmediatelyZerosGain()
        {
            var (ctrl, stage) = BuildStage();

            var inst = new PatternInstance { id = "inst-3", position = new Vector3(0.1f, 0f, 0f) };
            ctrl.EvaluateAll(new[] { inst });

            stage.ApplyCutoff("zone-a", allZones: false);

            // No frame tick needed — cutoff is immediate
            Assert.That(stage.GetGainMod("inst-3"), Is.EqualTo(0f).Within(0.001f),
                "ApplyCutoff should set gain to 0 immediately (no lerp)");
        }
    }
}
#endif
