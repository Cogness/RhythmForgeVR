#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Editor
{
    /// <summary>
    /// Phase B verification tests. Asserts that every stroke produces a full
    /// <see cref="MusicalShape"/> with all three facets, that the dominant facet
    /// matches what playback consumes, that harmony-dominant strokes write back
    /// into both <see cref="AppState.harmonicContext"/> and <see cref="HarmonicFabric"/>,
    /// and that legacy v&lt;7 saves still load cleanly with <c>musicalShape == null</c>.
    /// </summary>
    public class DraftBuilderMusicalShapeTests
    {
        [Test]
        public void RhythmStroke_ProducesFullMusicalShape_WithAllThreeFacets()
        {
            var store = NewStore();
            var points = GenerateCircle(24, 0.14f);

            var draft = DraftBuilder.BuildFromStroke(
                PatternType.RhythmLoop, points, Vector3.zero, Quaternion.identity,
                store.State, store);

            Assert.That(draft.success, Is.True);
            Assert.That(draft.musicalShape, Is.Not.Null);
            Assert.That(draft.musicalShape.facets, Is.Not.Null);
            Assert.That(draft.musicalShape.facets.rhythm, Is.Not.Null, "rhythm facet");
            Assert.That(draft.musicalShape.facets.melody, Is.Not.Null, "melody facet");
            Assert.That(draft.musicalShape.facets.harmony, Is.Not.Null, "harmony facet");
            Assert.That(draft.musicalShape.facetMode, Is.EqualTo(ShapeFacetMode.SoloRhythm));

            // All three facets share the dominant total step count (16 per bar * bars).
            int totalSteps = draft.musicalShape.totalSteps;
            Assert.That(draft.musicalShape.facets.melody.totalSteps, Is.EqualTo(totalSteps));
            Assert.That(draft.musicalShape.facets.harmony.totalSteps, Is.EqualTo(totalSteps));

            Assert.That(draft.musicalShape.bondStrength, Is.EqualTo(new Vector3(1f, 0f, 0f)));
            Assert.That(draft.musicalShape.facets.rhythm.events.Count, Is.GreaterThan(0));
        }

        [Test]
        public void DominantFacet_IsWhatPlaybackConsumes()
        {
            var store = NewStore();

            foreach (var type in new[]
                {
                    PatternType.RhythmLoop,
                    PatternType.MelodyLine,
                    PatternType.HarmonyPad
                })
            {
                var pts = type == PatternType.RhythmLoop
                    ? GenerateCircle(24, 0.14f)
                    : type == PatternType.MelodyLine
                        ? GenerateWave(32, 0.20f, 0.08f)
                        : GeneratePadStroke(20, 0.18f, 0.06f);

                var draft = DraftBuilder.BuildFromStroke(
                    type, pts, Vector3.zero, Quaternion.identity, store.State, store);

                Assert.That(draft.success, Is.True, $"{type} draft");
                Assert.That(draft.derivedSequence, Is.Not.Null, $"{type} derivedSequence");
                Assert.That(draft.musicalShape, Is.Not.Null, $"{type} musicalShape");

                // The dominant facet on MusicalShape must carry the SAME payload
                // (totalSteps + collection counts) as the DraftResult.derivedSequence
                // that the legacy scheduler will play.
                int domSteps;
                int domCount;
                switch (type)
                {
                    case PatternType.RhythmLoop:
                        domSteps = draft.musicalShape.facets.rhythm.totalSteps;
                        domCount = draft.musicalShape.facets.rhythm.events?.Count ?? 0;
                        Assert.That(draft.derivedSequence.kind, Is.EqualTo("rhythm"));
                        Assert.That(draft.derivedSequence.events?.Count ?? 0, Is.EqualTo(domCount));
                        break;
                    case PatternType.MelodyLine:
                        domSteps = draft.musicalShape.facets.melody.totalSteps;
                        domCount = draft.musicalShape.facets.melody.notes?.Count ?? 0;
                        Assert.That(draft.derivedSequence.kind, Is.EqualTo("melody"));
                        Assert.That(draft.derivedSequence.notes?.Count ?? 0, Is.EqualTo(domCount));
                        break;
                    default:
                        domSteps = draft.musicalShape.facets.harmony.totalSteps;
                        domCount = draft.musicalShape.facets.harmony.chord?.Count ?? 0;
                        Assert.That(draft.derivedSequence.kind, Is.EqualTo("harmony"));
                        Assert.That(draft.derivedSequence.chord?.Count ?? 0, Is.EqualTo(domCount));
                        break;
                }
                Assert.That(draft.derivedSequence.totalSteps, Is.EqualTo(domSteps));
            }
        }

        [Test]
        public void HarmonyStroke_WritesFabricAndMirrorsAppStateHarmonicContext()
        {
            var store = NewStore();
            var points = GeneratePadStroke(20, 0.18f, 0.06f);

            var draft = DraftBuilder.BuildFromStroke(
                PatternType.HarmonyPad, points, Vector3.zero, Quaternion.identity,
                store.State, store);

            Assert.That(draft.success, Is.True);
            Assert.That(draft.musicalShape.facets.harmony.events, Is.Not.Null);
            Assert.That(draft.musicalShape.facets.harmony.events.Count, Is.GreaterThan(0));

            // Fabric at bar 0 must match the first harmony event.
            var fabric = store.GetHarmonicFabric();
            Assert.That(fabric, Is.Not.Null);
            var placement = fabric.ChordAtBar(0);
            Assert.That(placement, Is.Not.Null, "fabric has a chord at bar 0");
            var firstEvent = draft.musicalShape.facets.harmony.events[0];
            Assert.That(placement.tones, Is.EquivalentTo(firstEvent.chord));
            Assert.That(placement.rootMidi, Is.EqualTo(firstEvent.rootMidi));

            // AppState.harmonicContext is mirrored for save compatibility.
            var ctx = store.GetHarmonicContext();
            Assert.That(ctx.chordTones, Is.EquivalentTo(placement.tones));
            Assert.That(ctx.rootMidi, Is.EqualTo(placement.rootMidi));
        }

        [Test]
        public void LegacyV6Save_LoadsCleanly_AndMaterializesMusicalShape()
        {
            var state = AppStateFactory.CreateEmpty();
            state.version = 6; // pre-Phase-B save
            state.activeSceneId = "scene-a";

            // A legacy pattern has derivedSequence but no musicalShape.
            var pattern = new PatternDefinition
            {
                id = "legacy-pattern",
                type = PatternType.RhythmLoop,
                name = "Legacy",
                bars = 2,
                groupId = "electronic",
                presetId = "lofi-drums",
                points = new List<Vector2>(GenerateCircle(12, 0.1f)),
                derivedSequence = new DerivedSequence
                {
                    kind = "rhythm",
                    totalSteps = AppStateFactory.BarSteps * 2,
                    events = new List<RhythmEvent>()
                },
                shapeProfile = new ShapeProfile
                {
                    worldWidth = 0.2f,
                    worldHeight = 0.2f,
                    worldLength = 0.8f,
                    worldAverageSize = 0.2f,
                    worldMaxDimension = 0.3f
                },
                soundProfile = new SoundProfile(),
                musicalShape = null
            };
            state.patterns.Add(pattern);

            var store = new SessionStore();
            store.LoadState(state);

            Assert.That(store.State.version, Is.EqualTo(9), "migrator bumps to v9");
            var loaded = store.GetPattern("legacy-pattern");
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.musicalShape, Is.Not.Null, "legacy pattern is materialized during migration");
            Assert.That(loaded.musicalShape.facetMode, Is.EqualTo(ShapeFacetMode.SoloRhythm));
            Assert.That(loaded.musicalShape.totalSteps, Is.EqualTo(AppStateFactory.BarSteps * 2));
            Assert.That(loaded.derivedSequence, Is.Not.Null, "legacy derivedSequence intact");
            Assert.That(loaded.derivedSequence.kind, Is.EqualTo("rhythm"));
            Assert.That(loaded.musicalShape.facets.rhythm.events, Is.Not.Null);
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

        private static List<Vector2> GeneratePadStroke(int pointCount, float width, float height)
        {
            var points = new List<Vector2>(pointCount);
            for (int i = 0; i < pointCount; i++)
            {
                float t = (float)i / (pointCount - 1);
                float x = (t - 0.5f) * width;
                float y = (t - 0.5f) * height;
                points.Add(new Vector2(x, y));
            }
            return points;
        }
    }
}
#endif
