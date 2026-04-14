using System;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;

namespace RhythmForge.Sequencer
{
    internal sealed class TransportController
    {
        private readonly SessionStore _store;
        private readonly ArrangementNavigator _arrangementNavigator;
        private readonly Func<double> _getDspTime;
        private readonly Transport _transport = new Transport();

        public event Action<string, string> OnPlaybackSceneChanged;

        public Transport CurrentTransport => _transport;
        public bool IsPlaying => _transport.playing;

        public TransportController(SessionStore store, ArrangementNavigator arrangementNavigator, Func<double> getDspTime)
        {
            _store = store;
            _arrangementNavigator = arrangementNavigator;
            _getDspTime = getDspTime;
        }

        public void Play()
        {
            if (_store == null || _transport.playing)
                return;

            bool hasArrangement = _arrangementNavigator.HasArrangement();
            _transport.playing = true;
            _transport.mode = hasArrangement ? "arrangement" : "scene";
            _transport.nextNoteTime = _getDspTime() + 0.05d;
            _transport.sceneStep = 0;
            _transport.slotStep = 0;
            _transport.absoluteBar = 1;

            if (hasArrangement)
            {
                _transport.slotIndex = _arrangementNavigator.FindFirstPopulatedSlot();
                SetPlaybackScene(_store.State.arrangement[_transport.slotIndex]?.sceneId);
            }
            else
            {
                _transport.slotIndex = -1;
                _transport.playbackSceneId = _store.State.activeSceneId;
            }
        }

        public void Stop()
        {
            if (!_transport.playing)
                return;

            _transport.playing = false;
        }

        public bool AdvanceTransport()
        {
            if (_transport.mode == "arrangement")
            {
                bool transportChanged = false;
                _transport.slotStep++;
                if (_transport.slotStep % AppStateFactory.BarSteps == 0)
                {
                    _transport.absoluteBar++;
                    transportChanged = true;
                }

                var slot = _store.State.arrangement[_transport.slotIndex];
                int slotTotalSteps = (slot?.bars ?? 4) * AppStateFactory.BarSteps;

                if (_transport.slotStep >= slotTotalSteps)
                {
                    int nextIndex = _arrangementNavigator.FindNextPopulatedSlot(_transport.slotIndex);
                    _transport.slotIndex = nextIndex;
                    _transport.slotStep = 0;
                    SetPlaybackScene(_store.State.arrangement[nextIndex]?.sceneId);
                    transportChanged = true;
                }

                return transportChanged;
            }

            _transport.sceneStep++;
            if (_transport.sceneStep % AppStateFactory.BarSteps != 0)
                return false;

            _transport.absoluteBar++;

            if (!string.IsNullOrEmpty(_store.State.queuedSceneId))
            {
                string queuedSceneId = _store.State.queuedSceneId;
                SetPlaybackScene(queuedSceneId);
                _transport.sceneStep = 0;
            }

            return true;
        }

        public string GetPlaybackSceneId()
        {
            if (!_transport.playing)
                return _store?.State?.activeSceneId;

            return _transport.playbackSceneId ?? _store?.State?.activeSceneId;
        }

        private void SetPlaybackScene(string sceneId)
        {
            string resolvedSceneId = !string.IsNullOrEmpty(sceneId)
                ? sceneId
                : _store.State.activeSceneId;

            string previousSceneId = _transport.playbackSceneId;
            _transport.playbackSceneId = resolvedSceneId;

            if (!string.IsNullOrEmpty(previousSceneId) && previousSceneId != resolvedSceneId)
                OnPlaybackSceneChanged?.Invoke(previousSceneId, resolvedSceneId);

            if (_store == null || string.IsNullOrEmpty(resolvedSceneId))
                return;

            if (_store.State.activeSceneId == resolvedSceneId &&
                string.IsNullOrEmpty(_store.State.queuedSceneId))
                return;

            _store.SetActiveScene(resolvedSceneId);
        }
    }
}
