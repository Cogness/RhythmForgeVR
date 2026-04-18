using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    /// <summary>
    /// Phase B: single deriver entry point per genre. Composes the existing
    /// <see cref="IRhythmDeriver"/> / <see cref="IMelodyDeriver"/> / <see cref="IHarmonyDeriver"/>
    /// and produces a full <see cref="MusicalShape"/> (rhythm + melody + harmony facets)
    /// for every stroke. The dominant facet (driven by <see cref="UnifiedDerivationRequest.dominantType"/>)
    /// becomes the pattern's <c>derivedSequence</c> for playback; the other two are
    /// stored on <see cref="MusicalShape.facets"/> but silent until Phase C flips UX.
    /// </summary>
    public interface IUnifiedShapeDeriver
    {
        UnifiedDerivationResult Derive(UnifiedDerivationRequest request);
    }

    /// <summary>
    /// Everything a <see cref="IUnifiedShapeDeriver"/> needs to run all three
    /// sub-derivers against the same stroke + scene context. Built by
    /// <c>DraftBuilder.BuildFromStroke</c> and <c>SessionStore.RederivePatternsBackground</c>.
    /// </summary>
    public struct UnifiedDerivationRequest
    {
        public PatternType dominantType;
        public Vector3 bondStrength;
        public ShapeFacetMode facetMode;
        public int ensembleRoleIndex;
        public int ensembleRoleCount;
        public int progressionBarIndex;
        public int progressionBarCount;
        public int bars;
        public int totalSteps;
        public string sceneId;
        public System.Func<int, ChordPlacement> barChordProvider;

        /// <summary>
        /// Phase D: when true, the unified deriver replaces <see cref="bondStrength"/>
        /// with a value computed from <see cref="shapeProfile3D"/> + <see cref="shapeProfile"/>
        /// (thick+angular → rhythm, tall+smooth → melody, deep+closed+flat → harmony).
        /// DrawModeController's Free mode sets this; Solo modes leave it false and
        /// pass an explicit one-hot <c>bondStrength</c>.
        /// </summary>
        public bool freeMode;

        /// <summary>
        /// Phase G: the full stroke carrier (raw 3D samples + projected 2D +
        /// plane basis). Replaces the pre-Phase-G <c>List&lt;Vector2&gt; points</c>
        /// field; derivers that only want the 2D footprint read
        /// <c>curve.projected</c>, which reproduces the pre-refactor list.
        /// </summary>
        public StrokeCurve curve;
        public StrokeMetrics metrics;
        public string keyName;
        public string groupId;
        public ShapeProfile shapeProfile;
        public ShapeProfile3D shapeProfile3D;
        public SoundProfile soundProfile;          // dominant facet sound profile (legacy/alias)
        // Phase C: per-facet sound profiles. Each facet's sub-deriver runs
        // with its own mapping (GenreProfile.GetSoundMapping(facet).Evaluate(...))
        // so MusicalShapeBehavior can dispatch each facet through the correct
        // voice. When null, the base class falls back to <c>soundProfile</c>.
        public SoundProfile rhythmSoundProfile;
        public SoundProfile melodySoundProfile;
        public SoundProfile harmonySoundProfile;
        public GenreProfile genre;
        public HarmonicFabric fabric;
        public AppState appState;
    }

    /// <summary>
    /// Output of <see cref="IUnifiedShapeDeriver.Derive"/>. Carries both the
    /// full 3-facet <see cref="MusicalShape"/> AND the dominant-facet summary
    /// fields that <c>DraftBuilder</c> needs to populate a legacy
    /// <c>DraftResult</c>/<c>PatternDefinition</c>.
    /// </summary>
    public struct UnifiedDerivationResult
    {
        public MusicalShape shape;
        public DerivedShapeSequence facets;

        // Mirror of shape.facets[dominantType] — what goes into
        // PatternDefinition.derivedSequence for the legacy scheduler.
        public DerivedSequence dominantSequence;

        public int bars;
        public string presetId;
        public List<string> tags;
        public string summary;
        public string details;

        // Set iff the dominant facet is HarmonyPad AND the harmony result
        // contained a chord. Callers should use this to mirror the chord into
        // any secondary stores (e.g. <c>AppState.harmonicContext</c>) since the
        // unified deriver only writes the provided <c>HarmonicFabric</c>.
        public HarmonicContext newHarmonicContext;

        // True iff the unified deriver actually wrote the chord into the
        // request's fabric (i.e. <c>newHarmonicContext != null</c> AND
        // <c>request.fabric != null</c>).
        public bool wroteFabricChord;
    }
}
