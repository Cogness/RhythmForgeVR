using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing.Electronic
{
    public sealed class ElectronicRhythmDeriver : IRhythmDeriver
    {
        public RhythmDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile,
            GenreProfile genre)
        {
            var result = RhythmDeriver.Derive(points, metrics, genre.Id, shapeProfile, soundProfile);
            return result;
        }
    }
}
