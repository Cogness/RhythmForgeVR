using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
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
        public static DraftResult BuildFromStroke(PatternType type, List<Vector2> rawPoints,
            Vector3 strokeCenter, Quaternion renderRotation, AppState state, SessionStore store)
        {
            var metrics = StrokeAnalyzer.Analyze(rawPoints);

            if (type == PatternType.RhythmLoop && !metrics.closed)
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
            var soundProfile = SoundProfileMapper.Derive(type, shapeProfile);
            string shapeSummary = PresetBiasResolver.SummarizeShapeDNA(type, shapeProfile, soundProfile);

            // Derive sequence
            int bars;
            string presetId;
            List<string> tags;
            DerivedSequence derivedSequence;
            string summary, details;

            switch (type)
            {
                case PatternType.RhythmLoop:
                {
                    var result = RhythmDeriver.Derive(rawPoints, metrics, groupId, shapeProfile, soundProfile);
                    bars = result.bars;
                    presetId = result.presetId;
                    tags = result.tags;
                    derivedSequence = result.derivedSequence;
                    summary = result.summary;
                    details = result.details;
                    break;
                }
                case PatternType.MelodyLine:
                {
                    var result = MelodyDeriver.Derive(rawPoints, metrics, keyName, groupId, shapeProfile, soundProfile);
                    bars = result.bars;
                    presetId = result.presetId;
                    tags = result.tags;
                    derivedSequence = result.derivedSequence;
                    summary = result.summary;
                    details = result.details;
                    break;
                }
                default:
                {
                    var result = HarmonyDeriver.Derive(rawPoints, metrics, keyName, groupId, shapeProfile, soundProfile);
                    bars = result.bars;
                    presetId = result.presetId;
                    tags = result.tags;
                    derivedSequence = result.derivedSequence;
                    summary = result.summary;
                    details = result.details;
                    break;
                }
            }

            return new DraftResult
            {
                success = true,
                type = type,
                name = name,
                bars = bars,
                tempoBase = state.tempo,
                key = keyName,
                groupId = groupId,
                presetId = presetId,
                points = normalizedPoints,
                renderRotation = renderRotation,
                hasRenderRotation = true,
                spawnPosition = strokeCenter,
                derivedSequence = derivedSequence,
                tags = tags,
                color = TypeColors.GetColor(type),
                shapeProfile = shapeProfile,
                soundProfile = soundProfile,
                shapeSummary = shapeSummary,
                summary = summary,
                details = $"{details} Shape DNA: {shapeSummary}."
            };
        }
    }
}
