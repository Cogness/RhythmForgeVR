using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    public struct RhythmDerivationResult
    {
        public int bars;
        public string presetId;
        public List<string> tags;
        public DerivedSequence derivedSequence;
        public string summary;
        public string details;
    }

    public static class RhythmDeriver
    {
        public static RhythmDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string groupId,
            ShapeProfile sp,
            SoundProfile sound)
        {
            return PercussionDeriver.Derive(points, metrics, groupId, sp, sound);
        }
    }
}
