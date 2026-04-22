using System.Collections.Generic;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    public struct RhythmDerivationResult
    {
        public int bars;
        public string presetId;
        public List<string> tags;
        public DerivedSequence derivedSequence;
        public string summary;
        public string details;
    }
}
