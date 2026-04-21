using RhythmForge.Core.Data;

namespace RhythmForge.Core.Session
{
    /// <summary>
    /// Compatibility wrapper kept while call sites migrate to GuidedDemoComposition.
    /// </summary>
    public static class DemoSession
    {
        public static AppState CreateDemoState(SessionStore store)
        {
            return GuidedDemoComposition.CreateDemoState(store);
        }
    }
}
