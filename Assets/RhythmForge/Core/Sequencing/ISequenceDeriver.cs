using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    public interface IRhythmDeriver
    {
        RhythmDerivationResult Derive(
            StrokeCurve curve,
            StrokeMetrics metrics,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile,
            GenreProfile genre);
    }

    public interface IMelodyDeriver
    {
        MelodyDerivationResult Derive(
            StrokeCurve curve,
            StrokeMetrics metrics,
            string keyName,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile,
            GenreProfile genre);
    }

    public interface IHarmonyDeriver
    {
        HarmonyDerivationResult Derive(
            StrokeCurve curve,
            StrokeMetrics metrics,
            string keyName,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile,
            GenreProfile genre);
    }
}
