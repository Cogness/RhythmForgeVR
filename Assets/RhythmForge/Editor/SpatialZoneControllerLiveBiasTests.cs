#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;

namespace RhythmForge.Editor
{
    public class SpatialZoneControllerLiveBiasTests
    {
        [Test]
        public void ApplyLiftTendu_RaisesGainMult_AndDecays()
        {
            var go = new GameObject("zones");
            try
            {
                var controller = go.AddComponent<SpatialZoneController>();
                controller.Initialize(new SessionStore(), SpatialZoneLayout.CreateDefault(), null, null);

                controller.ApplyConductorGesture("MelodyFront", ConductorGestureKind.LiftTendu, 1f);
                controller.GetLiveBiases("MelodyFront", out var liftedGain, out _, out _);
                controller.OnBarStart(3, 0d);
                controller.GetLiveBiases("MelodyFront", out var decayedGain, out _, out _);
                controller.OnBarStart(5, 0d);
                controller.GetLiveBiases("MelodyFront", out var resetGain, out _, out _);

                Assert.That(liftedGain, Is.GreaterThan(1f));
                Assert.That(decayedGain, Is.LessThan(liftedGain));
                Assert.That(resetGain, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplyFadePlie_LowersGainMult_AndDecays()
        {
            var go = new GameObject("zones");
            try
            {
                var controller = go.AddComponent<SpatialZoneController>();
                controller.Initialize(new SessionStore(), SpatialZoneLayout.CreateDefault(), null, null);

                controller.ApplyConductorGesture("HarmonyBehind", ConductorGestureKind.FadePlie, 1f);
                controller.GetLiveBiases("HarmonyBehind", out var fadedGain, out _, out _);
                controller.OnBarStart(5, 0d);
                controller.GetLiveBiases("HarmonyBehind", out var resetGain, out _, out _);

                Assert.That(fadedGain, Is.LessThan(1f));
                Assert.That(resetGain, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplyCutOff_ArmsZone_OnBarStart_ClearsAfterOneBar()
        {
            var go = new GameObject("zones");
            try
            {
                var controller = go.AddComponent<SpatialZoneController>();
                controller.Initialize(new SessionStore(), SpatialZoneLayout.CreateDefault(), null, null);

                controller.ApplyConductorGesture("DrumsFloor", ConductorGestureKind.CutOff, 1f);
                controller.GetLiveBiases("DrumsFloor", out _, out _, out var beforeBarStart);
                controller.OnBarStart(2, 0d);
                controller.GetLiveBiases("DrumsFloor", out _, out _, out var activeCut);
                controller.OnBarStart(3, 0d);
                controller.GetLiveBiases("DrumsFloor", out _, out _, out var clearedCut);

                Assert.That(beforeBarStart, Is.False);
                Assert.That(activeCut, Is.True);
                Assert.That(clearedCut, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BimanualStar_AppliesToAllZones()
        {
            var go = new GameObject("zones");
            try
            {
                var controller = go.AddComponent<SpatialZoneController>();
                controller.Initialize(new SessionStore(), SpatialZoneLayout.CreateDefault(), null, null);

                controller.ApplyConductorGesture("*", ConductorGestureKind.FadePlie, 1f);
                controller.GetLiveBiases("DrumsFloor", out var drumsGain, out _, out _);
                controller.GetLiveBiases("MelodyFront", out var melodyGain, out _, out _);
                controller.GetLiveBiases("HarmonyBehind", out var harmonyGain, out _, out _);

                Assert.That(drumsGain, Is.LessThan(1f));
                Assert.That(melodyGain, Is.LessThan(1f));
                Assert.That(harmonyGain, Is.LessThan(1f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void LiveBias_StacksOverStaticBias_WithoutMutatingIt()
        {
            var go = new GameObject("zones");
            try
            {
                var controller = go.AddComponent<SpatialZoneController>();
                controller.Initialize(new SessionStore(), SpatialZoneLayout.CreateDefault(), null, null);

                var zone = controller.ResolveZoneForPosition("instance-1", new Vector3(0f, 0f, 1f));
                float staticGainBias = zone.gainBias;

                controller.ApplyConductorGesture("MelodyFront", ConductorGestureKind.LiftTendu, 1f);
                controller.GetLiveBiases("MelodyFront", out var liveGain, out _, out _);

                Assert.That(zone.gainBias, Is.EqualTo(staticGainBias));
                Assert.That(liveGain * zone.gainBias, Is.GreaterThan(staticGainBias));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
