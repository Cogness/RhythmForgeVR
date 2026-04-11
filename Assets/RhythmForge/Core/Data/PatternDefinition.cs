using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmForge.Core.Data
{
    [Serializable]
    public class PatternDefinition
    {
        public string id;
        public PatternType type;
        public string name;
        public int bars;
        public float tempoBase;
        public string key;
        public string groupId;
        public string presetId;
        public List<Vector2> points = new List<Vector2>(); // normalized 0-1
        public Quaternion renderRotation = Quaternion.identity;
        public bool hasRenderRotation;
        public DerivedSequence derivedSequence;
        public List<string> tags = new List<string>();
        public Color color;
        public ShapeProfile shapeProfile;
        public SoundProfile soundProfile;
        public string shapeSummary;
        public string summary;
        public string details;

        public PatternDefinition Clone()
        {
            var clone = new PatternDefinition
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 12),
                type = type,
                name = name + " Copy",
                bars = bars,
                tempoBase = tempoBase,
                key = key,
                groupId = groupId,
                presetId = presetId,
                points = new List<Vector2>(points),
                renderRotation = renderRotation,
                hasRenderRotation = hasRenderRotation,
                derivedSequence = derivedSequence,
                tags = new List<string>(tags),
                color = color,
                shapeProfile = shapeProfile,
                soundProfile = soundProfile?.Clone(),
                shapeSummary = shapeSummary,
                summary = summary,
                details = details
            };
            return clone;
        }
    }
}
