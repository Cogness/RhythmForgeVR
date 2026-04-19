#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;

namespace RhythmForge.Editor
{
    public class SpatialZoneControllerTests
    {
        [Test]
        public void ResolveZoneForPosition_ReturnsZone_WhenPointInsideSphere()
        {
            var go = new GameObject("zones");
            try
            {
                var controller = go.AddComponent<SpatialZoneController>();
                controller.Initialize(null, SpatialZoneLayout.CreateDefault(), null, null);

                var zone = controller.ResolveZoneForPosition("instance-1", new Vector3(0f, 0f, 1f));

                Assert.That(zone?.id, Is.EqualTo("MelodyFront"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResolveZoneForPosition_ReturnsNull_WhenPointOutsideAllZones()
        {
            var go = new GameObject("zones");
            try
            {
                var controller = go.AddComponent<SpatialZoneController>();
                controller.Initialize(null, SpatialZoneLayout.CreateDefault(), null, null);

                var zone = controller.ResolveZoneForPosition("instance-1", new Vector3(6f, 3f, 6f));

                Assert.That(zone, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TryGetDefaultPlacementFor_ReturnsTypedZoneCenter()
        {
            var go = new GameObject("zones");
            try
            {
                var controller = go.AddComponent<SpatialZoneController>();
                controller.Initialize(null, SpatialZoneLayout.CreateDefault(), null, null);

                Assert.That(controller.TryGetDefaultPlacementFor(PatternType.RhythmLoop, out var rhythmPos), Is.True);
                Assert.That(controller.TryGetDefaultPlacementFor(PatternType.MelodyLine, out var melodyPos), Is.True);
                Assert.That(controller.TryGetDefaultPlacementFor(PatternType.HarmonyPad, out var harmonyPos), Is.True);

                Assert.That(Vector3.Distance(rhythmPos, new Vector3(0f, -0.6f, 0.9f)), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(melodyPos, new Vector3(0f, 0f, 1f)), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(harmonyPos, new Vector3(0f, 0.1f, -0.9f)), Is.LessThan(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResolveZoneForPosition_RetainsPreviousZoneWithinDeadband()
        {
            var go = new GameObject("zones");
            try
            {
                var controller = go.AddComponent<SpatialZoneController>();
                controller.Initialize(null, SpatialZoneLayout.CreateDefault(), null, null);

                var first = controller.ResolveZoneForPosition("instance-1", new Vector3(0f, 0f, 1f));
                var retained = controller.ResolveZoneForPosition("instance-1", new Vector3(0.76f, 0f, 1f));
                var exited = controller.ResolveZoneForPosition("instance-1", new Vector3(0.9f, 0f, 1f));

                Assert.That(first?.id, Is.EqualTo("MelodyFront"));
                Assert.That(retained?.id, Is.EqualTo("MelodyFront"));
                Assert.That(exited, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResolveZoneForPosition_HandsOffToNewZone_WhenOutsideDeadband()
        {
            var go = new GameObject("zones");
            try
            {
                var controller = go.AddComponent<SpatialZoneController>();
                controller.Initialize(null, SpatialZoneLayout.CreateDefault(), null, null);

                var first = controller.ResolveZoneForPosition("instance-1", new Vector3(0f, 0f, 1f));
                var handoff = controller.ResolveZoneForPosition("instance-1", new Vector3(0f, 1f, 0.6f));

                Assert.That(first?.id, Is.EqualTo("MelodyFront"));
                Assert.That(handoff?.id, Is.EqualTo("AccentsOverhead"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TryGetDefaultPlacementFor_UsesSourcePositionForSecondaryZones()
        {
            var go = new GameObject("zones");
            try
            {
                var controller = go.AddComponent<SpatialZoneController>();
                controller.Initialize(null, SpatialZoneLayout.CreateDefault(), null, null);

                Assert.That(controller.TryGetDefaultPlacementFor(PatternType.HarmonyPad, new Vector3(0f, 0.2f, 2.2f), out var farPad), Is.True);
                Assert.That(controller.TryGetDefaultPlacementFor(PatternType.MelodyLine, new Vector3(0f, 1f, 0.6f), out var overheadMelody), Is.True);

                Assert.That(Vector3.Distance(farPad, new Vector3(0f, 0.3f, 2.4f)), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(overheadMelody, new Vector3(0f, 1f, 0.6f)), Is.LessThan(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void MissingLayoutFallback_ReturnsNoPlacement_WithoutThrowing()
        {
            var go = new GameObject("zones");
            var layout = ScriptableObject.CreateInstance<SpatialZoneLayout>();
            try
            {
                var controller = go.AddComponent<SpatialZoneController>();
                controller.Initialize(null, layout, null, null);

                Assert.That(controller.TryGetDefaultPlacementFor(PatternType.RhythmLoop, out _), Is.False);
                Assert.That(controller.ResolveZoneForPosition("instance-1", Vector3.zero), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(layout);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Recentre_ReappliesAssignmentsForShiftedOrigin()
        {
            var go = new GameObject("zones");
            var store = new SessionStore();
            try
            {
                store.State.instances.Add(new PatternInstance("pattern-1", "scene-a", new Vector3(1f, 0f, 3f), 3f)
                {
                    id = "instance-1"
                });

                var controller = go.AddComponent<SpatialZoneController>();
                controller.Initialize(store, SpatialZoneLayout.CreateDefault(), null, null);

                controller.Recentre(new Pose(new Vector3(1f, 0f, 2f), Quaternion.identity));

                Assert.That(controller.GetZoneFor("instance-1")?.id, Is.EqualTo("MelodyFront"));
                Assert.That(store.State.instances[0].currentZoneId, Is.EqualTo("MelodyFront"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
