using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing.Electronic
{
    public sealed class ElectronicRhythmDeriver : IRhythmDeriver
    {
        public RhythmDerivationResult Derive(
            StrokeCurve curve,
            StrokeMetrics metrics,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile,
            GenreProfile genre)
        {
            var result = RhythmDeriver.Derive(curve, metrics, genre.Id, shapeProfile, soundProfile);
            return result;
        }
    }
}
