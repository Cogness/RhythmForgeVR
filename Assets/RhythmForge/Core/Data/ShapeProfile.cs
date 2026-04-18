using System;

namespace RhythmForge.Core.Data
{
    [Serializable]
    public class ShapeProfile
    {
        public float closedness;
        public float circularity;
        public float aspectRatio;
        public float angularity;
        public float symmetry;
        public float verticalSpan;
        public float horizontalSpan;
        public float pathLength;
        public float speedVariance;
        public float curvatureMean;
        public float curvatureVariance;
        public float centroidHeight;
        public float directionBias;
        public float tilt;
        public float tiltSigned;
        public float wobble;
        public float worldWidth;
        public float worldHeight;
        public float worldLength;
        public float worldAverageSize;
        public float worldMaxDimension;

        // Kinematic features (Phase 1 — Pen-as-Instrument)
        public float pressureMean;
        public float pressureVariance;
        public float pressurePeak;
        public float pressureSlopeEnd;
        public float tiltMean;
        public float tiltVariance;
        public float speedMean;
        public float speedPeak;
        public float speedTailOff;
        public float strokeSeconds;

        // 3D stroke features (Phase 3 — Use the third dimension)
        public float planarity;
        public float thrustAxis;
        public float verticalityWorld;

        public ShapeProfile Clone()
        {
            return new ShapeProfile
            {
                closedness = closedness,
                circularity = circularity,
                aspectRatio = aspectRatio,
                angularity = angularity,
                symmetry = symmetry,
                verticalSpan = verticalSpan,
                horizontalSpan = horizontalSpan,
                pathLength = pathLength,
                speedVariance = speedVariance,
                curvatureMean = curvatureMean,
                curvatureVariance = curvatureVariance,
                centroidHeight = centroidHeight,
                directionBias = directionBias,
                tilt = tilt,
                tiltSigned = tiltSigned,
                wobble = wobble,
                worldWidth = worldWidth,
                worldHeight = worldHeight,
                worldLength = worldLength,
                worldAverageSize = worldAverageSize,
                worldMaxDimension = worldMaxDimension,
                // Kinematic features (Phase 1 — Pen-as-Instrument)
                pressureMean = pressureMean,
                pressureVariance = pressureVariance,
                pressurePeak = pressurePeak,
                pressureSlopeEnd = pressureSlopeEnd,
                tiltMean = tiltMean,
                tiltVariance = tiltVariance,
                speedMean = speedMean,
                speedPeak = speedPeak,
                speedTailOff = speedTailOff,
                strokeSeconds = strokeSeconds,
                // 3D stroke features (Phase 3 — Use the third dimension)
                planarity = planarity,
                thrustAxis = thrustAxis,
                verticalityWorld = verticalityWorld
            };
        }
    }
}
