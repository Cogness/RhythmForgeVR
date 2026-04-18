using System;
using UnityEngine;

namespace RhythmForge.Core.Data
{
    public enum ShapeFacetMode
    {
        Free,
        SoloRhythm,
        SoloMelody,
        SoloHarmony
    }

    /// <summary>
    /// Unified shape entity that bundles all three facets (rhythm + melody + harmony)
    /// of a single stroke. In Phase A this type is defined but NOT emitted by the
    /// draft pipeline — nothing creates a MusicalShape at runtime yet. Phase B wires
    /// the unified deriver to populate it.
    /// </summary>
    [Serializable]
    public class MusicalShape
    {
        public string id;
        public ShapeProfile3D profile3D;
        public SoundProfile soundProfile;       // dominant facet's sound profile (legacy alias)
        public Vector3 bondStrength;            // (rhythm, melody, harmony) components; zero = silent
        public ShapeFacetMode facetMode = ShapeFacetMode.Free;
        public int totalSteps;
        public int roleIndex;                   // compatibility alias; runtime ownership lives on PatternInstance
        public DerivedShapeSequence facets;
        public string keyName;
        public int bars;

        // Per-facet audio routing. Populated by UnifiedShapeDeriverBase so
        // MusicalShapeBehavior (Phase C) can dispatch each facet through the
        // correct InstrumentPreset + SoundProfile instead of being forced to
        // reuse only the dominant facet's sound.
        public string rhythmPresetId;
        public string melodyPresetId;
        public string harmonyPresetId;
        public SoundProfile rhythmSoundProfile;
        public SoundProfile melodySoundProfile;
        public SoundProfile harmonySoundProfile;

        public MusicalShape Clone()
        {
            return new MusicalShape
            {
                id = id,
                profile3D = profile3D?.Clone(),
                soundProfile = soundProfile?.Clone(),
                bondStrength = bondStrength,
                facetMode = facetMode,
                totalSteps = totalSteps,
                roleIndex = roleIndex,
                facets = facets?.Clone(),
                keyName = keyName,
                bars = bars,
                rhythmPresetId = rhythmPresetId,
                melodyPresetId = melodyPresetId,
                harmonyPresetId = harmonyPresetId,
                rhythmSoundProfile = rhythmSoundProfile?.Clone(),
                melodySoundProfile = melodySoundProfile?.Clone(),
                harmonySoundProfile = harmonySoundProfile?.Clone()
            };
        }
    }
}
