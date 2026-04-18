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
    }
}
#endif
