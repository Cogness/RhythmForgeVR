using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    public interface IRhythmDeriver
    {
        RhythmDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile,
            GenreProfile genre);
    }

    public interface IMelodyDeriver
    {
        MelodyDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile,
            GenreProfile genre);
    }

    public interface IHarmonyDeriver
    {
        HarmonyDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile,
            GenreProfile genre);
    }
}
