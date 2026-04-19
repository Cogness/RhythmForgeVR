using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Session
{
    public class PatternRepository
    {
        private readonly Func<AppState> _getState;
        private readonly Func<string, SceneData> _getScene;
        private readonly Action _notifyStateChanged;
        private readonly Func<PatternType, string> _reserveName;
        private Func<PatternType, Vector3, Vector3?> _resolveSpawnPosition;

        public PatternRepository(
            Func<AppState> getState,
            Func<string, SceneData> getScene,
            Action notifyStateChanged,
            Func<PatternType, string> reserveName,
            Func<PatternType, Vector3, Vector3?> resolveSpawnPosition = null)
        {
            _getState = getState;
            _getScene = getScene;
            _notifyStateChanged = notifyStateChanged;
            _reserveName = reserveName;
            _resolveSpawnPosition = resolveSpawnPosition;
        }

        public void SetSpawnPlacementResolver(Func<PatternType, Vector3, Vector3?> resolver)
        {
            _resolveSpawnPosition = resolver;
        }

        public PatternDefinition GetPattern(string patternId)
        {
            foreach (var pattern in _getState().patterns)
            {
                if (pattern.id == patternId)
                    return pattern;
            }

            return null;
        }

        public PatternInstance GetInstance(string instanceId)
        {
            foreach (var instance in _getState().instances)
            {
                if (instance.id == instanceId)
                    return instance;
            }

            return null;
        }

        public List<PatternInstance> GetSceneInstances(string sceneId)
        {
            var result = new List<PatternInstance>();
            var scene = _getScene(sceneId);
            if (scene?.instanceIds != null)
            {
                foreach (var instanceId in scene.instanceIds)
                {
                    var instance = GetInstance(instanceId);
                    if (instance != null && instance.sceneId == sceneId)
                        result.Add(instance);
                }
            }

            foreach (var instance in _getState().instances)
            {
                if (instance.sceneId == sceneId && !result.Contains(instance))
                    result.Add(instance);
            }

            return result;
        }

        public PatternInstance CommitDraft(DraftResult draft, bool duplicate)
        {
            var state = _getState();
            var pattern = new PatternDefinition
            {
                id = RhythmForge.Core.MathUtils.CreateId("pattern"),
                type = draft.type,
                name = draft.name ?? _reserveName(draft.type),
                bars = draft.bars,
                tempoBase = draft.tempoBase,
                key = draft.key,
                groupId = draft.groupId,
                presetId = draft.presetId,
                points = draft.points,
                worldPoints = draft.worldPoints,
                renderRotation = draft.renderRotation,
                hasRenderRotation = draft.hasRenderRotation,
                derivedSequence = draft.derivedSequence,
                tags = draft.tags,
                color = draft.color,
                shapeProfile = draft.shapeProfile,
                shapeProfile3D = draft.shapeProfile3D,
                musicalShape = draft.musicalShape,
                soundProfile = draft.soundProfile,
                shapeSummary = draft.shapeSummary,
                summary = draft.summary,
                details = draft.details
            };

            state.patterns.Insert(0, pattern);

            Vector3 spawnPosition = ResolveSpawnPosition(pattern.type, draft.spawnPosition) ?? draft.spawnPosition;
            var instance = SpawnPattern(pattern.id, state.activeSceneId, spawnPosition, false);
            if (duplicate)
            {
                Vector3 dupPos = draft.spawnPosition + new Vector3(0.15f, 0.1f, 0f);
                SpawnPattern(pattern.id, state.activeSceneId, dupPos, false);
            }

            state.selectedPatternId = pattern.id;
            state.selectedInstanceId = instance?.id;
            _notifyStateChanged?.Invoke();
            return instance;
        }

        public PatternInstance SpawnPattern(string patternId, string sceneId = null, Vector3? coords = null, bool notify = true)
        {
            var state = _getState();
            sceneId = sceneId ?? state.activeSceneId;
            var pattern = GetPattern(patternId);
            var scene = _getScene(sceneId);
            if (pattern == null || scene == null)
                return null;

            Vector3 fallbackPosition = coords ?? GetNextSpawnPosition(sceneId);
            Vector3 position = coords ?? ResolveSpawnPosition(pattern.type, fallbackPosition) ?? fallbackPosition;
            float depth = position.z;

            var instance = new PatternInstance(patternId, sceneId, position, depth);
            state.instances.Add(instance);
            scene.instanceIds.Add(instance.id);
            state.selectedInstanceId = instance.id;
            state.selectedPatternId = patternId;

            if (notify)
                _notifyStateChanged?.Invoke();

            return instance;
        }

        public void ClonePattern(string patternId)
        {
            var state = _getState();
            var source = GetPattern(patternId);
            if (source == null)
                return;

            var duplicate = source.Clone();
            state.patterns.Insert(0, duplicate);
            state.selectedPatternId = duplicate.id;
            state.selectedInstanceId = null;
            _notifyStateChanged?.Invoke();
        }

        public void RemoveInstance(string instanceId)
        {
            var state = _getState();
            var instance = GetInstance(instanceId);
            if (instance == null)
                return;

            var scene = _getScene(instance.sceneId);
            scene?.instanceIds.Remove(instanceId);
            state.instances.RemoveAll(i => i.id == instanceId);

            if (state.selectedInstanceId == instanceId)
                state.selectedInstanceId = null;

            _notifyStateChanged?.Invoke();
        }

        public void DuplicateInstance(string instanceId)
        {
            var state = _getState();
            var instance = GetInstance(instanceId);
            if (instance == null)
                return;

            var duplicate = instance.Clone();
            duplicate.position += new Vector3(0.1f, 0.1f, 0f);
            duplicate.RecalculateMixFromPosition();

            state.instances.Add(duplicate);
            var scene = _getScene(instance.sceneId);
            scene?.instanceIds.Add(duplicate.id);
            state.selectedInstanceId = duplicate.id;
            _notifyStateChanged?.Invoke();
        }

        public void UpdateInstance(string instanceId, Vector3? position = null, float? depth = null, bool? muted = null)
        {
            var instance = GetInstance(instanceId);
            if (instance == null)
                return;

            if (position.HasValue)
                instance.position = position.Value;
            if (depth.HasValue)
                instance.depth = depth.Value;
            if (muted.HasValue)
                instance.muted = muted.Value;

            instance.RecalculateMixFromPosition();
            _notifyStateChanged?.Invoke();
        }

        public void SetPresetOverride(string instanceId, string presetId)
        {
            var instance = GetInstance(instanceId);
            if (instance == null)
                return;

            instance.presetOverrideId = string.IsNullOrEmpty(presetId) ? null : presetId;
            _notifyStateChanged?.Invoke();
        }

        private Vector3 GetNextSpawnPosition(string sceneId)
        {
            var existing = GetSceneInstances(sceneId);
            Vector3[] positions =
            {
                new Vector3(0.35f, 0.34f, 0.3f),
                new Vector3(0.66f, 0.36f, 0.28f),
                new Vector3(0.28f, 0.70f, 0.44f),
                new Vector3(0.55f, 0.58f, 0.68f),
                new Vector3(0.74f, 0.68f, 0.34f)
            };
            return positions[existing.Count % positions.Length];
        }

        private Vector3? ResolveSpawnPosition(PatternType type, Vector3 fallbackPosition)
        {
            return _resolveSpawnPosition?.Invoke(type, fallbackPosition);
        }
    }
}
