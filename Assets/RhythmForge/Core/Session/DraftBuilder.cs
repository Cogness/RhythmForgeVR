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
        public Quaternion renderRotation;
        public bool hasRenderRotation;
        public Vector3 spawnPosition;
        public DerivedSequence derivedSequence;
        public List<string> tags;
        public Color color;
        public ShapeProfile shapeProfile;
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
            var behavior = PatternBehaviorRegistry.Get(type);
            var metrics = StrokeAnalyzer.Analyze(rawPoints);

            if (behavior.PrefersClosedStroke && !metrics.closed)
            {
                // Soft warning only — on-device VR drawing rarely closes perfectly.
                // Proceed with the stroke; the rhythm deriver will handle it gracefully.
                Debug.Log("[RhythmForge] RhythmLoop stroke not fully closed — proceeding anyway.");
            }

            var normalizedPoints = StrokeAnalyzer.NormalizePoints(rawPoints, metrics);
            string name = store.NextDraftName(type);
            string keyName = state.key;
            string groupId = state.activeGroupId;

            // Build shape DNA
            var shapeProfile = ShapeProfileCalculator.Derive(normalizedPoints, metrics, type);
            var soundProfile = behavior.DeriveSoundProfile(shapeProfile);
            string shapeSummary = PresetBiasResolver.SummarizeShapeDNA(type, shapeProfile, soundProfile);

            // Derive sequence
            var derivation = behavior.Derive(rawPoints, metrics, keyName, groupId, shapeProfile, soundProfile);

            return new DraftResult
            {
                success = true,
                type = type,
                name = name,
                bars = derivation.bars,
                tempoBase = state.tempo,
                key = keyName,
                groupId = groupId,
                presetId = derivation.presetId,
                points = normalizedPoints,
                renderRotation = renderRotation,
                hasRenderRotation = true,
                spawnPosition = strokeCenter,
                derivedSequence = derivation.derivedSequence,
                tags = derivation.tags,
                color = TypeColors.GetColor(type),
                shapeProfile = shapeProfile,
                soundProfile = soundProfile,
                shapeSummary = shapeSummary,
                summary = derivation.summary,
                details = ComposeDetails(derivation.details, shapeSummary)
            };
        }
    }
}
