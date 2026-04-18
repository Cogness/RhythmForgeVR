using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing.Electronic
{
    public sealed class ElectronicHarmonyDeriver : IHarmonyDeriver
    {
        public HarmonyDerivationResult Derive(
            StrokeCurve curve,
            StrokeMetrics metrics,
            string keyName,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile,
            GenreProfile genre)
        {
            return HarmonyDeriver.Derive(curve, metrics, keyName, genre.Id, shapeProfile, soundProfile);
        }
    }
}
