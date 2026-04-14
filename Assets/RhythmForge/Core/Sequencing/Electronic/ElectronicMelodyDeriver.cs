using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing.Electronic
{
    public sealed class ElectronicMelodyDeriver : IMelodyDeriver
    {
        public MelodyDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile,
            GenreProfile genre)
        {
            return MelodyDeriver.Derive(points, metrics, keyName, genre.Id, shapeProfile, soundProfile);
        }
    }
}
