using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.PatternBehavior;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Core.Session
{
    public class DraftResult
    {
        public bool success;
        public string error;
        public PatternType type;
        public string name;
        public int bars;
        public float tempoBase;
        public string key;
        public string groupId;
        public string presetId;
        public List<Vector2> points;
        public List<Vector3> worldPoints;
        public Quaternion renderRotation;
        public bool hasRenderRotation;
        public Vector3 spawnPosition;
        public DerivedSequence derivedSequence;
        public List<string> tags;
        public Color color;
        public ShapeProfile shapeProfile;
        public ShapeProfile3D shapeProfile3D;
        public MusicalShape musicalShape;
        public SoundProfile soundProfile;
        public string shapeSummary;
        public string summary;
        public string details;
    }

    public static class DraftBuilder
    {
        public static string ComposeDetails(string details, string shapeSummary)
        {
            if (string.IsNullOrEmpty(shapeSummary))
                return details ?? string.Empty;

            string baseDetails = details ?? string.Empty;
            int shapeIndex = baseDetails.IndexOf(" Shape DNA:");
            if (shapeIndex >= 0)
                baseDetails = baseDetails.Substring(0, shapeIndex);

            if (string.IsNullOrWhiteSpace(baseDetails))
                return $"Shape DNA: {shapeSummary}.";

            return $"{baseDetails} Shape DNA: {shapeSummary}.";
        }

        public static DraftResult BuildFromStroke(PatternType type, List<Vector2> rawPoints,
            Vector3 strokeCenter, Quaternion renderRotation, AppState state, SessionStore store)
        {
            return BuildFromStroke(type, rawPoints, strokeCenter, renderRotation, state, store,
                richSamples: null, referenceUp: Vector3.up, bondStrength: null);
        }

        /// <summary>
        /// Extended overload that also accepts the raw 3D stroke stream captured by
        /// <c>StrokeCapture</c> (world position + pressure + stylus rotation + per-sample
        /// timestamp). When <paramref name="richSamples"/> is non-null, a
        /// <see cref="ShapeProfile3D"/> is computed and attached to the draft.
        /// <paramref name="bondStrength"/> (Phase C) overrides the default one-hot
        /// bond vector (so <see cref="ShapeFacetMode.Free"/> can produce
        /// all three audible facets). When null, falls back to the one-hot default
        /// keyed on <paramref name="type"/>, reproducing today's Solo-equivalent
        /// semantics for legacy callers.
        /// <paramref name="freeMode"/> (Phase D) tells the unified deriver to
        /// replace <paramref name="bondStrength"/> with a 3D-derived vector; Solo
        /// modes pass <c>false</c> to keep their explicit one-hot.
        /// </summary>
        public static DraftResult BuildFromStroke(PatternType type, List<Vector2> rawPoints,
            Vector3 strokeCenter, Quaternion renderRotation, AppState state, SessionStore store,
            IReadOnlyList<StrokeSample> richSamples, Vector3 referenceUp,
            Vector3? bondStrength = null, bool freeMode = false,
            Vector3? strokeRight = null, Vector3? strokeUp = null)
        {
            var behavior = PatternBehaviorRegistry.Get(type);

            // Phase G: build the unified StrokeCurve carrier. When the caller
            // supplies richSamples + a plane basis (fresh draft path from
            // StrokeCapture) we compute the 2D projection from the 3D samples
            // and DROP the legacy rawPoints — they are identical by construction.
            // Pre-Phase-G callers (AlgorithmTest, rederivation without rich
            // samples) still pass rawPoints; we wrap them via FromLegacy2D.
            StrokeCurve curve;
            if (richSamples != null && strokeRight.HasValue && strokeUp.HasValue)
            {
                curve = StrokeCurve.FromSamples(
                    richSamples, strokeCenter, strokeRight.Value, strokeUp.Value);
            }
            else
            {
                curve = StrokeCurve.FromLegacy2D(rawPoints);
            }

            // Metrics + shape DNA are computed from the 2D footprint — same
            // semantics as before Phase G (curve.projected == legacy rawPoints
            // on the fresh-draft path).
            var metrics = StrokeAnalyzer.Analyze(curve.projected);

            if (behavior.PrefersClosedStroke && !metrics.closed)
            {
                // Soft warning only — on-device VR drawing rarely closes perfectly.
                // Proceed with the stroke; the rhythm deriver will handle it gracefully.
                Debug.Log("[RhythmForge] RhythmLoop stroke not fully closed — proceeding anyway.");
            }

            var normalizedPoints = StrokeAnalyzer.NormalizePoints(curve.projected, metrics);
            string name = store.NextDraftName(type);
            string keyName = state.key;
            string groupId = state.activeGroupId;

            // Build shape DNA
            var shapeProfile = ShapeProfileCalculator.Derive(normalizedPoints, metrics, type);
            var soundProfile = behavior.DeriveSoundProfile(shapeProfile);
            string shapeSummary = PresetBiasResolver.SummarizeShapeDNA(type, shapeProfile, soundProfile);

            // Phase A: 3D shape profile from the rich stroke stream. Stored on the
            // draft but not consumed by derivers until Phase E.
            ShapeProfile3D shapeProfile3D = richSamples != null
                ? ShapeProfile3DCalculator.Derive(richSamples, shapeProfile, referenceUp)
                : null;

            var activeScene = store.GetScene(state.activeSceneId);
            int sceneShapeCount = activeScene?.instanceIds?.Count ?? 0;
            int nextRoleIndex = sceneShapeCount;
            int nextProgressionBarIndex = sceneShapeCount % 8;
            ShapeFacetMode resolvedFacetMode = ResolveFacetMode(type, bondStrength, freeMode);

            var genre = GenreRegistry.GetActive();

            // Phase C: each facet gets its own SoundProfile via its genre-mapped
            // Evaluate(). For the dominant facet this reproduces today's single
            // soundProfile. Non-dominant facets get a sensible profile so
            // MusicalShapeBehavior can dispatch them through the correct voice.
            var rhythmSound  = type == PatternType.RhythmLoop
                ? soundProfile
                : genre.GetSoundMapping(PatternType.RhythmLoop).Evaluate(PatternType.RhythmLoop, shapeProfile);
            var melodySound  = type == PatternType.MelodyLine
                ? soundProfile
                : genre.GetSoundMapping(PatternType.MelodyLine).Evaluate(PatternType.MelodyLine, shapeProfile);
            var harmonySound = type == PatternType.HarmonyPad
                ? soundProfile
                : genre.GetSoundMapping(PatternType.HarmonyPad).Evaluate(PatternType.HarmonyPad, shapeProfile);

            var request = new UnifiedDerivationRequest
            {
                dominantType   = type,
                bondStrength   = bondStrength ?? OneHotBondStrength(type),
                facetMode      = resolvedFacetMode,
                freeMode       = resolvedFacetMode == ShapeFacetMode.Free,
                ensembleRoleIndex = nextRoleIndex,
                ensembleRoleCount = sceneShapeCount + 1,
                progressionBarIndex = nextProgressionBarIndex,
                progressionBarCount = 8,
                sceneId        = state.activeSceneId,
                barChordProvider = localBar =>
                {
                    var fabric = store.GetHarmonicFabric(state.activeSceneId);
                    return fabric?.ChordAtBar(nextProgressionBarIndex + localBar);
                },
                curve          = curve,
                metrics        = metrics,
                keyName        = keyName,
                groupId        = groupId,
                shapeProfile   = shapeProfile,
                shapeProfile3D = shapeProfile3D,
                soundProfile   = soundProfile,
                rhythmSoundProfile  = rhythmSound,
                melodySoundProfile  = melodySound,
                harmonySoundProfile = harmonySound,
                genre          = genre,
                fabric         = store.GetHarmonicFabric(state.activeSceneId),
                appState       = state
            };

            var unified = genre.UnifiedDeriver.Derive(request);

            // Mirror the newly-derived chord into AppState + fabric so subsequent
            // strokes see the same context. Fabric was already written inside the
            // unified deriver (the re-write below is idempotent and also updates
            // state.harmonicContext for save compatibility).
            if (unified.newHarmonicContext != null)
            {
                store.SetHarmonicContext(
                    unified.newHarmonicContext.rootMidi,
                    unified.newHarmonicContext.chordTones,
                    unified.newHarmonicContext.flavor);
            }

            // Phase G: center-relative 3D point list for the visualizer. Only
            // populated when the caller supplied rich samples (fresh-draft
            // path). Legacy AlgorithmTest / rederivation paths leave it null,
            // and the visualizer falls back to the flat 2D points list.
            List<Vector3> worldPoints = null;
            if (richSamples != null)
            {
                worldPoints = new List<Vector3>(richSamples.Count);
                for (int i = 0; i < richSamples.Count; i++)
                    worldPoints.Add(richSamples[i].worldPos - strokeCenter);
            }

            return new DraftResult
            {
                success = true,
                type = type,
                name = name,
                bars = unified.bars,
                tempoBase = state.tempo,
                key = keyName,
                groupId = groupId,
                presetId = unified.presetId,
                points = normalizedPoints,
                worldPoints = worldPoints,
                renderRotation = renderRotation,
                hasRenderRotation = true,
                spawnPosition = strokeCenter,
                derivedSequence = unified.dominantSequence,
                tags = unified.tags,
                // Phase E: shape-bearing drafts preview their audible mix in their
                // stroke color via TypeColors.Blend(bondStrength). Legacy one-hot
                // bondStrength blends to the dominant facet's single color, so
                // Solo-mode visuals are identical to pre-Phase-E.
                color = unified.shape != null
                    ? TypeColors.Blend(unified.shape.bondStrength)
                    : TypeColors.GetColor(type),
                shapeProfile = shapeProfile,
                shapeProfile3D = shapeProfile3D,
                musicalShape = unified.shape,
                soundProfile = soundProfile,
                shapeSummary = shapeSummary,
                summary = unified.summary,
                details = ComposeDetails(unified.details, shapeSummary)
            };
        }

        private static Vector3 OneHotBondStrength(PatternType type)
        {
            switch (type)
            {
                case PatternType.RhythmLoop: return new Vector3(1f, 0f, 0f);
                case PatternType.MelodyLine: return new Vector3(0f, 1f, 0f);
                default:                     return new Vector3(0f, 0f, 1f);
            }
        }

        private static ShapeFacetMode ResolveFacetMode(PatternType type, Vector3? bondStrength, bool freeMode)
        {
            if (freeMode)
                return ShapeFacetMode.Free;

            var resolved = bondStrength ?? OneHotBondStrength(type);
            if (resolved.x > 0.001f && resolved.y <= 0.001f && resolved.z <= 0.001f)
                return ShapeFacetMode.SoloRhythm;
            if (resolved.y > 0.001f && resolved.x <= 0.001f && resolved.z <= 0.001f)
                return ShapeFacetMode.SoloMelody;
            if (resolved.z > 0.001f && resolved.x <= 0.001f && resolved.y <= 0.001f)
                return ShapeFacetMode.SoloHarmony;

            return ShapeFacetMode.Free;
        }
    }
}
