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
    }
}
