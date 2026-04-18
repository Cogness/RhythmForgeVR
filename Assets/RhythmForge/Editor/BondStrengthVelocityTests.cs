#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;
using RhythmForge.Core.Session;

namespace RhythmForge.Editor
{
    /// <summary>
    /// Phase E verification: <see cref="BondStrengthVelocity"/> math,
    /// <see cref="TypeColors.Blend"/> color blending, and the DraftBuilder
    /// path that assigns a blended color to shape-bearing drafts.
    /// </summary>
    public class BondStrengthVelocityTests
    {
        // ---------- Math ----------

        [Test]
        public void ScaleRhythm_OneHotWeight_PassesThroughUnchanged()
        {
            // Legacy bit-identity: weight=1 → multiplier=1.
            Assert.That(BondStrengthVelocity.ScaleRhythm(0.8f, 1f), Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(BondStrengthVelocity.ScaleMelody(0.5f, 1f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(BondStrengthVelocity.ScaleHarmony(0.38f, 1f), Is.EqualTo(0.38f).Within(0.0001f));
        }

        [Test]
        public void ScaleRhythm_ZeroWeight_AttenuatesToForty()
        {
            // Solo-silent facets are skipped by the > 0 gate in
            // MusicalShapeBehavior, but the math itself must still floor at 0.4.
            Assert.That(BondStrengthVelocity.ScaleRhythm(1f, 0f), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(BondStrengthVelocity.ScaleMelody(1f, 0f), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(BondStrengthVelocity.ScaleHarmony(1f, 0f), Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void Scale_EqualThirdsFreeMode_ProducesSixtyPercent()
        {
            // Free mode equal weighting: 0.4 + 0.6*0.333 ≈ 0.6
            float w = 1f / 3f;
            Assert.That(BondStrengthVelocity.ScaleRhythm(1f, w), Is.EqualTo(0.6f).Within(0.01f));
        }

        [Test]
        public void Scale_FreeModeFloor_StaysAudible()
        {
            // Floor weight 0.15 → 0.4 + 0.6*0.15 = 0.49 — still audible.
            float floor = BondStrengthVelocity.ScaleRhythm(1f, 0.15f);
            Assert.That(floor, Is.EqualTo(0.49f).Within(0.001f));
            Assert.That(floor, Is.GreaterThan(0.1f),
                "Free-mode floor must stay well above the audibility threshold");
        }

        [Test]
        public void Scale_ClampedToOne_NeverExceedsUnity()
        {
            // velocity*multiplier > 1 must clamp. weight=1 is pass-through, so
            // the only way to exceed 1 would be velocity > 1 (never valid),
            // but make sure Clamp01 is in place for defensive inputs.
            Assert.That(BondStrengthVelocity.ScaleRhythm(2f, 1f), Is.EqualTo(1f));
            Assert.That(BondStrengthVelocity.ScaleMelody(5f, 1f), Is.EqualTo(1f));
        }

        // ---------- Color blending ----------

        [Test]
        public void Blend_OneHotRhythm_EqualsRhythmColor()
        {
            var c = TypeColors.Blend(new Vector3(1f, 0f, 0f));
            AssertColorsApproxEqual(c, TypeColors.RhythmLoop);
        }

        [Test]
        public void Blend_OneHotHarmony_EqualsHarmonyColor()
        {
            var c = TypeColors.Blend(new Vector3(0f, 0f, 1f));
            AssertColorsApproxEqual(c, TypeColors.HarmonyPad);
        }

        [Test]
        public void Blend_FreeEqualThirds_DiffersFromAnySingleFacetColor()
        {
            var blended = TypeColors.Blend(new Vector3(1f / 3f, 1f / 3f, 1f / 3f));
            Assert.That(Distance(blended, TypeColors.RhythmLoop), Is.GreaterThan(0.05f));
            Assert.That(Distance(blended, TypeColors.MelodyLine), Is.GreaterThan(0.05f));
            Assert.That(Distance(blended, TypeColors.HarmonyPad), Is.GreaterThan(0.05f));
        }

        [Test]
        public void Blend_ZeroVector_FallsBackToWhite()
        {
            var c = TypeColors.Blend(Vector3.zero);
            AssertColorsApproxEqual(c, Color.white);
        }

        [Test]
        public void Blend_UnnormalisedInput_RenormalisesBeforeMixing()
        {
            // (2, 0, 0) should blend identically to (1, 0, 0).
            var a = TypeColors.Blend(new Vector3(2f, 0f, 0f));
            var b = TypeColors.Blend(new Vector3(1f, 0f, 0f));
            AssertColorsApproxEqual(a, b);
        }

        // ---------- DraftBuilder color wiring ----------

        [Test]
        public void DraftBuilder_FreeMode_DraftColorDiffersFromOneHotColor()
        {
            var store = new SessionStore();
            store.LoadState(AppStateFactory.CreateEmpty());
            var pts = GenerateCircle(24, 0.14f);

            var freeDraft = DraftBuilder.BuildFromStroke(
                PatternType.RhythmLoop, pts, Vector3.zero, Quaternion.identity,
                store.State, store,
                richSamples: null, referenceUp: Vector3.up,
                bondStrength: new Vector3(1f / 3f, 1f / 3f, 1f / 3f));

            var store2 = new SessionStore();
            store2.LoadState(AppStateFactory.CreateEmpty());
            var soloDraft = DraftBuilder.BuildFromStroke(
                PatternType.RhythmLoop, pts, Vector3.zero, Quaternion.identity,
                store2.State, store2,
                richSamples: null, referenceUp: Vector3.up,
                bondStrength: new Vector3(1f, 0f, 0f));

            Assert.That(freeDraft.success, Is.True);
            Assert.That(soloDraft.success, Is.True);
            Assert.That(Distance(freeDraft.color, soloDraft.color), Is.GreaterThan(0.05f),
                "Free-mode draft color must differ from Solo-mode (one-hot) color");

            // Solo-Rhythm draft should visually equal the rhythm facet color.
            AssertColorsApproxEqual(soloDraft.color, TypeColors.RhythmLoop);
        }

        // ---------- helpers ----------

        private static void AssertColorsApproxEqual(Color a, Color b, float tol = 0.01f)
        {
            Assert.That(a.r, Is.EqualTo(b.r).Within(tol), "red channel");
            Assert.That(a.g, Is.EqualTo(b.g).Within(tol), "green channel");
            Assert.That(a.b, Is.EqualTo(b.b).Within(tol), "blue channel");
        }

        private static float Distance(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        private static System.Collections.Generic.List<Vector2> GenerateCircle(int pointCount, float radius)
        {
            var points = new System.Collections.Generic.List<Vector2>(pointCount + 1);
            for (int i = 0; i <= pointCount; i++)
            {
                float angle = (float)i / pointCount * Mathf.PI * 2f;
                points.Add(new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
            }
            return points;
        }
    }
}
#endif
