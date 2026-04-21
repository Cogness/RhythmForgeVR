using RhythmForge.Core.Data;

namespace RhythmForge.Core.Session
{
    /// <summary>
    /// Creates the guided starter composition for the phased flow.
    /// The musical foundation is seeded, but all user-drawn phases start empty.
    /// </summary>
    public static class GuidedDemoComposition
    {
        public static AppState CreateDemoState(SessionStore store)
        {
            var state = AppStateFactory.CreateEmpty();
            state.guidedMode = true;
            state.composition = GuidedDefaults.Create();
            state.composition.currentPhase = CompositionPhase.Harmony;
            state.tempo = state.composition.tempo;
            state.key = state.composition.key;
            state.activeGroupId = "lofi";
            state.activeGenreId = GuidedDefaults.ActiveGenreId;
            state.activeSceneId = "scene-a";
            state.selectedInstanceId = null;
            state.selectedPatternId = null;
            state.harmonicContext = state.composition.progression.ToHarmonicContext(0);
            GenreRegistry.SetActive(state.activeGenreId);
            return state;
        }
    }
}
