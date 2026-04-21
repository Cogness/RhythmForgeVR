using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.Session;

namespace RhythmForge.Interaction
{
    public class PhaseController : MonoBehaviour
    {
        private SessionStore _store;
        private DrawModeController _drawMode;
        private RhythmForgeEventBus _eventBus;

        public CompositionPhase CurrentPhase => _store?.GetCurrentPhase() ?? CompositionPhase.Harmony;

        public void Initialize(SessionStore store, DrawModeController drawMode)
        {
            _store = store;
            _drawMode = drawMode;
            _eventBus = store != null ? store.EventBus : null;
            SyncFromStore(true);
        }

        public void SyncFromStore(bool publishEvent = false)
        {
            ApplyPhase(CurrentPhase, persist: false, publishEvent: publishEvent);
        }

        public void GoToPhase(CompositionPhase phase)
        {
            ApplyPhase(phase, persist: true, publishEvent: true);
        }

        public void Next()
        {
            var phases = CompositionPhaseExtensions.All;
            int index = GetPhaseIndex(CurrentPhase);
            GoToPhase(phases[(index + 1) % phases.Length]);
        }

        public void Prev()
        {
            var phases = CompositionPhaseExtensions.All;
            int index = GetPhaseIndex(CurrentPhase);
            GoToPhase(phases[(index - 1 + phases.Length) % phases.Length]);
        }

        private void ApplyPhase(CompositionPhase phase, bool persist, bool publishEvent)
        {
            CompositionPhase previousPhase = CurrentPhase;

            if (persist && _store != null)
                _store.SetCurrentPhase(phase);

            _drawMode?.SetMode(phase.ToPatternType());

            if (publishEvent && (_eventBus != null) && (persist || previousPhase != phase))
                _eventBus.Publish(new PhaseChangedEvent(phase));
        }

        private static int GetPhaseIndex(CompositionPhase phase)
        {
            var phases = CompositionPhaseExtensions.All;
            for (int i = 0; i < phases.Length; i++)
            {
                if (phases[i] == phase)
                    return i;
            }

            return 0;
        }
    }
}
