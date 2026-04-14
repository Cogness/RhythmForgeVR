using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing.Electronic
{
    public sealed class ElectronicHarmonyDeriver : IHarmonyDeriver
    {
        public HarmonyDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile,
            GenreProfile genre)
        {
            return HarmonyDeriver.Derive(points, metrics, keyName, genre.Id, shapeProfile, soundProfile);
        }
    }
}
