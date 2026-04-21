using System;

namespace RhythmForge.Core.Data
{
    [Serializable]
    public class GrooveProfile
    {
        public float density;
        public float syncopation;
        public float swing;
        public int quantizeGrid;
        public float[] accentCurve;

        public GrooveProfile Clone()
        {
            return new GrooveProfile
            {
                density = density,
                syncopation = syncopation,
                swing = swing,
                quantizeGrid = quantizeGrid,
                accentCurve = accentCurve != null ? (float[])accentCurve.Clone() : null
            };
        }
    }
}
