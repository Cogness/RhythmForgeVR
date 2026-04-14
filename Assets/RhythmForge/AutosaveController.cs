using RhythmForge.Core.Data;
using RhythmForge.Core.Session;

namespace RhythmForge
{
    internal sealed class AutosaveController
    {
        private readonly float _intervalSeconds;
        private float _timer;

        public AutosaveController(float intervalSeconds = 30f)
        {
            _intervalSeconds = intervalSeconds;
        }

        public void Tick(float deltaTime, AppState state)
        {
            if (state == null)
                return;

            _timer += deltaTime;
            if (_timer <= _intervalSeconds)
                return;

            _timer = 0f;
            SessionPersistence.Save(state);
        }

        public void SaveNow(AppState state)
        {
            if (state == null)
                return;

            _timer = 0f;
            SessionPersistence.Save(state);
        }

        public void ResetTimer()
        {
            _timer = 0f;
        }
    }
}
