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
                worldMaxDimension = worldMaxDimension
            };
        }
    }
}
