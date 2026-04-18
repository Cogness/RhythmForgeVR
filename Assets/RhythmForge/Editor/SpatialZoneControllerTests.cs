#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.Session;

namespace RhythmForge.Editor
{
    public class SpatialZoneControllerTests
    {
        private static List<SpatialZone> DefaultZones() =>
            SpatialZoneFactory.CreateDefaults(Vector3.zero);

        // ── 1 ────────────────────────────────────────────────────────────────
        [Test]
        public void GetZoneFx_InstanceInsideZone_ReturnsZoneFx()
        {
            var zones = DefaultZones();
            var controller = new SpatialZoneController(zones);

            // Place instance exactly at drums-floor center
            var drumsZone = zones.Find(z => z.id == "drums-floor");
            var instance = new PatternInstance("pattern-1", "scene-a", drumsZone.center, 0.3f);

            controller.EvaluateAll(new[] { instance });

            var fx = controller.GetZoneFx(instance.id);
            Assert.That(fx.reverbAdd, Is.EqualTo(drumsZone.busFx.reverbAdd).Within(0.0001f));
        }

        // ── 2 ────────────────────────────────────────────────────────────────
        [Test]
        public void GetZoneFx_InstanceOutsideAllZones_ReturnsZero()
        {
            var zones = DefaultZones();
            var controller = new SpatialZoneController(zones);

            // Position far away from all zones
            var instance = new PatternInstance("pattern-2", "scene-a", new Vector3(100f, 100f, 100f), 0.3f);

            controller.EvaluateAll(new[] { instance });

            var fx = controller.GetZoneFx(instance.id);
            Assert.That(fx.reverbAdd,          Is.EqualTo(0f).Within(0.0001f));
            Assert.That(fx.delayAdd,           Is.EqualTo(0f).Within(0.0001f));
            Assert.That(fx.filterCutoffOffset, Is.EqualTo(0f).Within(0.0001f));
        }

        // ── 3 ────────────────────────────────────────────────────────────────
        [Test]
        public void EvaluateAll_InstanceEntersZone_FiresChangedEvent()
        {
            var zones = DefaultZones();
            var bus = new RhythmForgeEventBus();
            var controller = new SpatialZoneController(zones, bus);

            string capturedNewZoneId = null;
            bus.Subscribe<SpatialZoneChangedEvent>(evt => capturedNewZoneId = evt.NewZoneId);

            // First call: instance outside all zones — fires event (null → null: no fire actually)
            var instance = new PatternInstance("pattern-3", "scene-a", new Vector3(100f, 100f, 100f), 0.3f);
            controller.EvaluateAll(new[] { instance });
            // (no zone membership yet, so no event for null→null; capturedNewZoneId remains null)
            Assert.That(capturedNewZoneId, Is.Null);

            // Move into drums-floor zone
            var drumsZone = zones.Find(z => z.id == "drums-floor");
            instance.position = drumsZone.center;
            controller.EvaluateAll(new[] { instance });

            Assert.That(capturedNewZoneId, Is.EqualTo("drums-floor"));
        }

        // ── 4 ────────────────────────────────────────────────────────────────
        [Test]
        public void EvaluateAll_InstanceAlreadyInZone_DoesNotFireAgain()
        {
            var zones = DefaultZones();
            var bus = new RhythmForgeEventBus();
            var controller = new SpatialZoneController(zones, bus);

            int eventCount = 0;
            bus.Subscribe<SpatialZoneChangedEvent>(_ => eventCount++);

            var drumsZone = zones.Find(z => z.id == "drums-floor");
            var instance = new PatternInstance("pattern-4", "scene-a", drumsZone.center, 0.3f);

            // First evaluate — enters zone: fires once
            controller.EvaluateAll(new[] { instance });
            Assert.That(eventCount, Is.EqualTo(1));

            // Second evaluate — same position, same zone: should NOT fire again
            controller.EvaluateAll(new[] { instance });
            Assert.That(eventCount, Is.EqualTo(1));
        }

        // ── 5 ────────────────────────────────────────────────────────────────
        [Test]
        public void GetDefaultSpawnPosition_RhythmType_NearDrumsFloor()
        {
            var zones = DefaultZones();
            var controller = new SpatialZoneController(zones);

            // Stroke at origin — well away from all zones
            Vector3 strokeCenter = Vector3.zero;
            Vector3 result = controller.GetDefaultSpawnPosition(PatternType.RhythmLoop, strokeCenter);

            var drumsZone = zones.Find(z => z.id == "drums-floor");

            // Result must be closer to drums-floor center than the original stroke center was
            // (Lerp at 40% moves the spawn 40% of the way from strokeCenter to zone center)
            float resultDistToZone  = Vector3.Distance(result, drumsZone.center);
            float strokeDistToZone  = Vector3.Distance(strokeCenter, drumsZone.center);
            Assert.That(resultDistToZone, Is.LessThan(strokeDistToZone),
                "Spawn should be biased toward the drums-floor zone center relative to the original stroke center");
        }
    }
}
#endif
