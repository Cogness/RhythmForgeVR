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
        public string genreId;
        public string presetId;
        public List<Vector2> points = new List<Vector2>(); // normalized 0-1
        /// <summary>
        /// Phase G (save v8+): 3D stroke points in the pattern's local space
        /// (center-relative, same convention as <see cref="points"/> but
        /// preserving the out-of-plane dimension). Null on v7 and older saves;
        /// the visualizer falls back to <see cref="points"/> when absent so
        /// legacy patterns render exactly as before.
        /// </summary>
        public List<Vector3> worldPoints = null;
        public Quaternion renderRotation = Quaternion.identity;
        public bool hasRenderRotation;
        public DerivedSequence derivedSequence;
        public List<string> tags = new List<string>();
        public Color color;
        public ShapeProfile shapeProfile;
        public ShapeProfile3D shapeProfile3D;
        public MusicalShape musicalShape;
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
                genreId = genreId,
                presetId = presetId,
                points = new List<Vector2>(points),
                worldPoints = worldPoints != null ? new List<Vector3>(worldPoints) : null,
                renderRotation = renderRotation,
                hasRenderRotation = hasRenderRotation,
                derivedSequence = derivedSequence,
                tags = new List<string>(tags),
                color = color,
                shapeProfile = shapeProfile?.Clone(),
                shapeProfile3D = shapeProfile3D?.Clone(),
                musicalShape = musicalShape?.Clone(),
                soundProfile = soundProfile?.Clone(),
                shapeSummary = shapeSummary,
                summary = summary,
                details = details
            };
            return clone;
        }
    }
}
