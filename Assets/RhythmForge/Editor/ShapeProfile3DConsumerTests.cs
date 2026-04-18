#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;
using RhythmForge.Core.Session;

namespace RhythmForge.Editor
{
    /// <summary>
    /// Phase D verification: identical 2D footprints produce different audible
    /// output once the 3D profile differs. Covers two consumers:
    /// <list type="bullet">
    ///   <item><see cref="BondStrengthResolver"/> (Free-mode facet weights)</item>
    ///   <item><see cref="UnifiedShapeDeriverBase.ApplyProfile3DMutations"/>
    ///         (post-derive rhythm/melody/harmony shaping)</item>
    /// </list>
    /// Every test uses the SAME 2D points; the only variable is the rich 3D
    /// sample stream handed to the draft pipeline — so any delta in output
    /// can only come from the 3D signals we just wired up.
    /// </summary>
    public class ShapeProfile3DConsumerTests
    {
        // ---------- BondStrengthResolver ----------

        [Test]
        public void BondStrength_FlatCircle_FavorsHarmony()
        {
            var profile = BuildPlanarCircleProfile();
            var p3d = new ShapeProfile3D
            {
                planarity = 0.95f,       // very flat
                depthSpan = 0.15f,       // barely any depth
                elongation3D = 0.1f,     // not stretched
                thicknessVariance = 0.1f,
                helicity = 0f,
                temporalEvenness = 0.9f
            };

            var bs = BondStrengthResolver.Resolve(profile, p3d);

            Assert.That(bs.z, Is.GreaterThan(bs.x),
                "Flat closed circle should favor harmony over rhythm");
            Assert.That(bs.z, Is.GreaterThan(bs.y),
                "Flat closed circle should favor harmony over melody");
            AssertSumsToOne(bs);
        }

        [Test]
        public void BondStrength_TallHelix_FavorsMelodyAndRhythmOverFlatHarmony()
        {
            var profile = BuildPlanarCircleProfile();
            var flat = new ShapeProfile3D
            {
                planarity = 0.95f, depthSpan = 0.15f,
                elongation3D = 0.1f, thicknessVariance = 0.1f,
                helicity = 0f, temporalEvenness = 0.9f
            };
            var helix = new ShapeProfile3D
            {
                planarity = 0.2f,        // volumetric
                depthSpan = 0.8f,        // deep
                elongation3D = 0.9f,     // very stretched along one axis
                thicknessVariance = 0.7f,
                helicity = 0.6f,
                temporalEvenness = 0.7f
            };

            var bsFlat  = BondStrengthResolver.Resolve(profile, flat);
            var bsHelix = BondStrengthResolver.Resolve(profile, helix);

            float delta = Mathf.Abs(bsFlat.x - bsHelix.x)
                        + Mathf.Abs(bsFlat.y - bsHelix.y)
                        + Mathf.Abs(bsFlat.z - bsHelix.z);
            Assert.That(delta, Is.GreaterThan(0.15f),
                $"Same 2D footprint, different 3D profile → bondStrength must shift. L1 delta = {delta}");

            // Helix should tilt melody up vs the flat case (elongation3D).
            Assert.That(bsHelix.y, Is.GreaterThan(bsFlat.y),
                "Elongated 3D stroke should foreground melody");
            AssertSumsToOne(bsHelix);
        }

        [Test]
        public void BondStrength_FreeMode_AllFacetsStayAboveFloor()
        {
            var profile = BuildPlanarCircleProfile();
            // Extreme 3D — still every facet must clear the MinWeight floor
            // so Free mode never silences a facet.
            var extreme = new ShapeProfile3D
            {
                planarity = 1f, depthSpan = 0f,
                elongation3D = 0f, thicknessVariance = 0f,
                helicity = 0f, temporalEvenness = 1f
            };

            var bs = BondStrengthResolver.Resolve(profile, extreme);

            Assert.That(bs.x, Is.GreaterThan(0.05f), "Rhythm must stay audible in Free mode");
            Assert.That(bs.y, Is.GreaterThan(0.05f), "Melody must stay audible in Free mode");
            Assert.That(bs.z, Is.GreaterThan(0.05f), "Harmony must stay audible in Free mode");
            AssertSumsToOne(bs);
        }

        [Test]
        public void BondStrength_NullProfile3D_FallsBackGracefully()
        {
            var profile = BuildPlanarCircleProfile();
            var bs = BondStrengthResolver.Resolve(profile, null);
            AssertSumsToOne(bs);
            Assert.That(bs.x, Is.GreaterThan(0f));
            Assert.That(bs.y, Is.GreaterThan(0f));
            Assert.That(bs.z, Is.GreaterThan(0f));
        }

        // ---------- ApplyProfile3DMutations (end-to-end via UnifiedDeriver) ----------

        [Test]
        public void Tilt_ChangesRhythmVelocities_SameFootprint()
        {
            var store = NewStore();
            var pts = GenerateCircle(24, 0.14f);

            var flat = BuildRichSamples3D(pts, tiltAmount: 0f);
            var leaning = BuildRichSamples3D(pts, tiltAmount: 0.9f);

            var draftFlat = DraftBuilder.BuildFromStroke(
                PatternType.RhythmLoop, pts, Vector3.zero, Quaternion.identity,
                store.State, store,
                richSamples: flat, referenceUp: Vector3.up,
                bondStrength: new Vector3(1f, 0f, 0f));

            var store2 = NewStore();
            var draftLean = DraftBuilder.BuildFromStroke(
                PatternType.RhythmLoop, pts, Vector3.zero, Quaternion.identity,
                store2.State, store2,
                richSamples: leaning, referenceUp: Vector3.up,
                bondStrength: new Vector3(1f, 0f, 0f));

            Assert.That(draftFlat.success, Is.True);
            Assert.That(draftLean.success, Is.True);

            float velSumFlat = SumVelocities(draftFlat.musicalShape.facets.rhythm.events);
            float velSumLean = SumVelocities(draftLean.musicalShape.facets.rhythm.events);

            Assert.That(Mathf.Abs(velSumFlat - velSumLean), Is.GreaterThan(0.001f),
                $"Tilt must alter rhythm velocities. flat={velSumFlat} leaning={velSumLean}");
        }

        [Test]
        public void DepthSpan_WidensHarmonyChord()
        {
            var store = NewStore();
            var pts = GenerateCircle(24, 0.14f);

            var shallow = BuildRichSamples3DWithDepth(pts, depthExtent: 0.01f);
            var deep    = BuildRichSamples3DWithDepth(pts, depthExtent: 0.30f);

            var draftShallow = DraftBuilder.BuildFromStroke(
                PatternType.HarmonyPad, pts, Vector3.zero, Quaternion.identity,
                store.State, store,
                richSamples: shallow, referenceUp: Vector3.up,
                bondStrength: new Vector3(0f, 0f, 1f));

            var store2 = NewStore();
            var draftDeep = DraftBuilder.BuildFromStroke(
                PatternType.HarmonyPad, pts, Vector3.zero, Quaternion.identity,
                store2.State, store2,
                richSamples: deep, referenceUp: Vector3.up,
                bondStrength: new Vector3(0f, 0f, 1f));

            Assert.That(draftShallow.success, Is.True);
            Assert.That(draftDeep.success, Is.True);

            int shallowChord = draftShallow.musicalShape.facets.harmony.chord.Count;
            int deepChord    = draftDeep.musicalShape.facets.harmony.chord.Count;

            Assert.That(deepChord, Is.GreaterThanOrEqualTo(shallowChord),
                $"Deep stroke should not produce fewer chord tones. shallow={shallowChord} deep={deepChord}");
        }

        // ---------- helpers ----------

        private static void AssertSumsToOne(Vector3 bs)
        {
            float sum = bs.x + bs.y + bs.z;
            Assert.That(Mathf.Abs(sum - 1f), Is.LessThan(0.001f),
                $"bondStrength must L1-normalise to 1 (got {sum})");
        }

        private static float SumVelocities(List<RhythmEvent> events)
        {
            if (events == null) return 0f;
            float s = 0f;
            for (int i = 0; i < events.Count; i++) s += events[i].velocity;
            return s;
        }

        private static ShapeProfile BuildPlanarCircleProfile()
        {
            return new ShapeProfile
            {
                closedness = 1f,
                circularity = 0.95f,
                aspectRatio = 1f,
                angularity = 0.1f,
                symmetry = 0.9f,
                verticalSpan = 0.3f,
                horizontalSpan = 0.3f,
                pathLength = 1.2f,
                speedVariance = 0.2f,
                curvatureMean = 0.5f,
                curvatureVariance = 0.1f,
                centroidHeight = 0.5f,
                directionBias = 0f,
                tilt = 0f,
                tiltSigned = 0f,
                wobble = 0.1f,
                worldWidth = 0.3f,
                worldHeight = 0.3f,
                worldLength = 0.3f,
                worldAverageSize = 0.3f,
                worldMaxDimension = 0.3f
            };
        }

        private static SessionStore NewStore()
        {
            var store = new SessionStore();
            store.LoadState(AppStateFactory.CreateEmpty());
            return store;
        }

        private static List<Vector2> GenerateCircle(int pointCount, float radius)
        {
            var points = new List<Vector2>(pointCount + 1);
            for (int i = 0; i <= pointCount; i++)
            {
                float angle = (float)i / pointCount * Mathf.PI * 2f;
                points.Add(new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
            }
            return points;
        }

        /// <summary>
        /// Build 3D rich samples by extruding a 2D circle into the XY plane at z=0
        /// with controllable stylus tilt. Keeps the XY footprint identical across
        /// calls; only the stylus rotation changes, which drives
        /// <see cref="ShapeProfile3D.tiltMean"/>.
        /// </summary>
        private static List<StrokeSample> BuildRichSamples3D(List<Vector2> pts, float tiltAmount)
        {
            var samples = new List<StrokeSample>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                // Stylus up vector tilted away from worldUp by tiltAmount (0..1 → 0..90°).
                Quaternion rot = Quaternion.AngleAxis(tiltAmount * 90f, Vector3.right);
                samples.Add(new StrokeSample
                {
                    worldPos = new Vector3(pts[i].x, pts[i].y, 0f),
                    pressure = 0.5f,
                    stylusRot = rot,
                    timestamp = i * 0.02
                });
            }
            return samples;
        }

        /// <summary>
        /// Extrude the 2D circle along Z by <paramref name="depthExtent"/> so the
        /// 3D profile gains depthSpan while the XY footprint stays unchanged.
        /// </summary>
        private static List<StrokeSample> BuildRichSamples3DWithDepth(List<Vector2> pts, float depthExtent)
        {
            var samples = new List<StrokeSample>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                float t = (float)i / Mathf.Max(1, pts.Count - 1);
                samples.Add(new StrokeSample
                {
                    worldPos = new Vector3(pts[i].x, pts[i].y, (t - 0.5f) * depthExtent * 2f),
                    pressure = 0.5f,
                    stylusRot = Quaternion.identity,
                    timestamp = i * 0.02
                });
            }
            return samples;
        }
    }
}
#endif
