#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.PatternBehavior;
using RhythmForge.Core.PatternBehavior.Behaviors;
using RhythmForge.Core.Sequencing;
using RhythmForge.Core.Session;

namespace RhythmForge.Editor
{
    /// <summary>
    /// Phase C verification: Free mode makes all three facets audible from a
    /// single stroke (bondStrength = (1,1,1)) and Solo modes zero two of the
    /// three. Also verifies the registry routes shape-bearing patterns to
    /// <see cref="MusicalShapeBehavior"/> rather than the legacy per-type
    /// behavior.
    /// </summary>
    public class MusicalShapeBehaviorTests
    {
        [Test]
        public void FreeMode_Override_ProducesDeterministicBondStrength()
        {
            var store = NewStore();
            var pts = GenerateCircle(24, 0.14f);

            var draft = DraftBuilder.BuildFromStroke(
                PatternType.RhythmLoop, pts, Vector3.zero, Quaternion.identity,
                store.State, store,
                richSamples: null, referenceUp: Vector3.up,
                bondStrength: new Vector3(1f, 1f, 1f));

            Assert.That(draft.success, Is.True);
            Assert.That(draft.musicalShape, Is.Not.Null);
            Assert.That(draft.musicalShape.facetMode, Is.EqualTo(ShapeFacetMode.Free));
            Assert.That(draft.musicalShape.bondStrength,
                Is.EqualTo(BondStrengthResolver.Resolve(draft.shapeProfile, draft.shapeProfile3D)),
                "Free mode should recompute a deterministic bonded mix from the shape profile");
            Assert.That(draft.musicalShape.bondStrength.x, Is.GreaterThan(0.05f));
            Assert.That(draft.musicalShape.bondStrength.y, Is.GreaterThan(0.05f));
            Assert.That(draft.musicalShape.bondStrength.z, Is.GreaterThan(0.05f));

            // All three facets populated.
            Assert.That(draft.musicalShape.facets.rhythm.events, Is.Not.Null);
            Assert.That(draft.musicalShape.facets.rhythm.events.Count, Is.GreaterThan(0));
            Assert.That(draft.musicalShape.facets.melody.notes, Is.Not.Null);
            Assert.That(draft.musicalShape.facets.harmony.chord, Is.Not.Null);
            Assert.That(draft.musicalShape.facets.harmony.chord.Count, Is.GreaterThan(0));
        }

        [Test]
        public void SoloMelody_Override_ProducesOneHotY()
        {
            var store = NewStore();
            var pts = GenerateWave(32, 0.20f, 0.08f);

            var draft = DraftBuilder.BuildFromStroke(
                PatternType.MelodyLine, pts, Vector3.zero, Quaternion.identity,
                store.State, store,
                richSamples: null, referenceUp: Vector3.up,
                bondStrength: new Vector3(0f, 1f, 0f));

            Assert.That(draft.success, Is.True);
            Assert.That(draft.musicalShape.bondStrength,
                Is.EqualTo(new Vector3(0f, 1f, 0f)));
        }

        [Test]
        public void NullOverride_FallsBackToOneHotDominant()
        {
            // Omitting bondStrength reproduces pre-Phase-C semantics: one-hot
            // on the dominant facet.
            var store = NewStore();
            var pts = GenerateCircle(24, 0.14f);

            var draft = DraftBuilder.BuildFromStroke(
                PatternType.RhythmLoop, pts, Vector3.zero, Quaternion.identity,
                store.State, store,
                richSamples: null, referenceUp: Vector3.up,
                bondStrength: null);

            Assert.That(draft.musicalShape.bondStrength,
                Is.EqualTo(new Vector3(1f, 0f, 0f)));
        }

        [Test]
        public void Registry_RoutesShapeBearingPatternToMusicalShapeBehavior()
        {
            var rhythmShape = new PatternDefinition
            {
                id = "p-shape",
                type = PatternType.RhythmLoop,
                musicalShape = new MusicalShape
                {
                    facets = new DerivedShapeSequence
                    {
                        rhythm  = new RhythmSequence(),
                        melody  = new MelodySequence(),
                        harmony = new HarmonySequence()
                    },
                    bondStrength = new Vector3(1f, 1f, 1f)
                }
            };

            var legacyRhythm = new PatternDefinition
            {
                id = "p-legacy",
                type = PatternType.RhythmLoop,
                musicalShape = null
            };

            var shapeBehavior  = PatternBehaviorRegistry.GetForPattern(rhythmShape);
            var legacyBehavior = PatternBehaviorRegistry.GetForPattern(legacyRhythm);

            Assert.That(shapeBehavior, Is.InstanceOf<MusicalShapeBehavior>(),
                "Patterns carrying a MusicalShape must route to MusicalShapeBehavior");
            Assert.That(legacyBehavior, Is.Not.InstanceOf<MusicalShapeBehavior>(),
                "Legacy v<7 patterns (musicalShape == null) stay on their per-type behavior");
        }

        // --- helpers ---

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

        private static List<Vector2> GenerateWave(int pointCount, float width, float amplitude)
        {
            var points = new List<Vector2>(pointCount);
            for (int i = 0; i < pointCount; i++)
            {
                float t = (float)i / (pointCount - 1);
                float x = (t - 0.5f) * width;
                float y = Mathf.Sin(t * Mathf.PI * 3f) * amplitude;
                points.Add(new Vector2(x, y));
            }
            return points;
        }
    }
}
#endif
