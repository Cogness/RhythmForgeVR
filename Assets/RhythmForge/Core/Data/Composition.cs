using System;
using System.Collections.Generic;

namespace RhythmForge.Core.Data
{
    [Serializable]
    public class CompositionPhasePatternRef
    {
        public CompositionPhase phase;
        public string patternId;
    }

    [Serializable]
    public class Composition
    {
        public string id;
        public float tempo = GuidedDefaults.Tempo;
        public string key = GuidedDefaults.Key;
        public int bars = GuidedDefaults.Bars;
        public ChordProgression progression;
        public GrooveProfile groove;
        public List<CompositionPhasePatternRef> phasePatternIds = new List<CompositionPhasePatternRef>();
        public CompositionPhase currentPhase = CompositionPhase.Harmony;

        public string GetPatternId(CompositionPhase phase)
        {
            if (phasePatternIds == null)
                return null;

            for (int i = 0; i < phasePatternIds.Count; i++)
            {
                var entry = phasePatternIds[i];
                if (entry != null && entry.phase == phase)
                    return entry.patternId;
            }

            return null;
        }

        public void SetPatternId(CompositionPhase phase, string patternId)
        {
            if (phasePatternIds == null)
                phasePatternIds = new List<CompositionPhasePatternRef>();

            for (int i = 0; i < phasePatternIds.Count; i++)
            {
                var entry = phasePatternIds[i];
                if (entry == null || entry.phase != phase)
                    continue;

                entry.patternId = patternId;
                return;
            }

            phasePatternIds.Add(new CompositionPhasePatternRef
            {
                phase = phase,
                patternId = patternId
            });
        }

        public void RemovePatternId(CompositionPhase phase)
        {
            if (phasePatternIds == null)
                return;

            for (int i = phasePatternIds.Count - 1; i >= 0; i--)
            {
                var entry = phasePatternIds[i];
                if (entry != null && entry.phase == phase)
                    phasePatternIds.RemoveAt(i);
            }
        }

        public Composition Clone()
        {
            var clone = new Composition
            {
                id = id,
                tempo = tempo,
                key = key,
                bars = bars,
                progression = progression?.Clone(),
                groove = groove?.Clone(),
                currentPhase = currentPhase,
                phasePatternIds = new List<CompositionPhasePatternRef>()
            };

            if (phasePatternIds == null)
                return clone;

            for (int i = 0; i < phasePatternIds.Count; i++)
            {
                var entry = phasePatternIds[i];
                if (entry == null)
                    continue;

                clone.phasePatternIds.Add(new CompositionPhasePatternRef
                {
                    phase = entry.phase,
                    patternId = entry.patternId
                });
            }

            return clone;
        }
    }
}
