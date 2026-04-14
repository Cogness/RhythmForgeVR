using System;
using System.Collections.Generic;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Session
{
    public class SceneController
    {
        private readonly Func<AppState> _getState;
        private readonly Func<string, SceneData> _getScene;
        private readonly Func<string, PatternInstance> _getInstance;
        private readonly Action _notifyStateChanged;

        public SceneController(
            Func<AppState> getState,
            Func<string, SceneData> getScene,
            Func<string, PatternInstance> getInstance,
            Action notifyStateChanged)
        {
            _getState = getState;
            _getScene = getScene;
            _getInstance = getInstance;
            _notifyStateChanged = notifyStateChanged;
        }

        public void SetActiveScene(string sceneId, bool queueIfPlaying = false)
        {
            var state = _getState();
            if (_getScene(sceneId) == null)
                return;

            state.activeSceneId = sceneId;
            state.queuedSceneId = null;

            if (state.selectedInstanceId != null)
            {
                var selected = _getInstance(state.selectedInstanceId);
                if (selected == null || selected.sceneId != sceneId)
                    state.selectedInstanceId = null;
            }

            _notifyStateChanged?.Invoke();
        }

        public void QueueScene(string sceneId)
        {
            _getState().queuedSceneId = sceneId;
            _notifyStateChanged?.Invoke();
        }

        public void CopyScene(string sourceId, string targetId)
        {
            var state = _getState();
            var source = _getScene(sourceId);
            var target = _getScene(targetId);
            if (source == null || target == null || sourceId == targetId)
                return;

            var toRemove = new HashSet<string>(target.instanceIds);
            state.instances.RemoveAll(instance => toRemove.Contains(instance.id));
            target.instanceIds.Clear();

            foreach (var instanceId in source.instanceIds)
            {
                var sourceInstance = _getInstance(instanceId);
                if (sourceInstance == null)
                    continue;

                var copied = sourceInstance.Clone();
                copied.sceneId = targetId;
                target.instanceIds.Add(copied.id);
                state.instances.Add(copied);
            }

            state.activeSceneId = targetId;
            state.selectedInstanceId = null;
            _notifyStateChanged?.Invoke();
        }

        public void UpdateArrangement(string slotId, string sceneId = null, int? bars = null)
        {
            foreach (var slot in _getState().arrangement)
            {
                if (slot.id != slotId)
                    continue;

                if (sceneId != null)
                    slot.sceneId = sceneId;
                if (bars.HasValue)
                    slot.bars = bars.Value;
                break;
            }

            _notifyStateChanged?.Invoke();
        }

        public void ClearArrangementScene(string slotId)
        {
            foreach (var slot in _getState().arrangement)
            {
                if (slot.id != slotId)
                    continue;

                slot.sceneId = null;
                break;
            }

            _notifyStateChanged?.Invoke();
        }
    }
}
