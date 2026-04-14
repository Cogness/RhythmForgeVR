using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.PatternBehavior;

namespace RhythmForge.Core.Session
{
    public class SessionStore
    {
        public AppState State { get; private set; }
        public RhythmForgeEventBus EventBus { get; }

        internal PatternRepository Patterns { get; }
        internal SceneController Scenes { get; }
        internal SoundProfileResolver SoundResolver { get; }

        private readonly StateMigrator _stateMigrator;

        public event Action OnStateChanged;

        public SessionStore(RhythmForgeEventBus eventBus = null)
        {
            EventBus = eventBus ?? new RhythmForgeEventBus();
            State = AppStateFactory.CreateEmpty();
            Patterns = new PatternRepository(() => State, GetScene, NotifyStateChanged, ReserveName);
            Scenes = new SceneController(() => State, GetScene, GetInstance, NotifyStateChanged);
            SoundResolver = new SoundProfileResolver(GetPreset);
            _stateMigrator = new StateMigrator();
        }

        public void LoadState(AppState state)
        {
            State = state ?? AppStateFactory.CreateEmpty();
            _stateMigrator.NormalizeState(State);
            NotifyStateChanged();
        }

        public void Reset()
        {
            State = AppStateFactory.CreateEmpty();
            NotifyStateChanged();
        }

        public string NextDraftName(PatternType type)
        {
            string prefix = PatternBehaviorRegistry.Get(type).DraftNamePrefix;
            int count = State.counters.GetCount(type);
            return $"{prefix}-{count:D2}";
        }

        public string ReserveName(PatternType type)
        {
            string name = NextDraftName(type);
            State.counters.Increment(type);
            return name;
        }

        public InstrumentGroup GetGroup(string groupId) => InstrumentGroups.Get(groupId);
        public InstrumentPreset GetPreset(string presetId) => InstrumentPresets.Get(presetId);

        public PatternDefinition GetPattern(string patternId) => Patterns.GetPattern(patternId);

        public SceneData GetScene(string sceneId)
        {
            foreach (var scene in State.scenes)
            {
                if (scene.id == sceneId)
                    return scene;
            }

            return null;
        }

        public PatternInstance GetInstance(string instanceId) => Patterns.GetInstance(instanceId);

        public List<PatternInstance> GetSceneInstances(string sceneId) => Patterns.GetSceneInstances(sceneId);

        public SceneData GetActiveScene()
        {
            return GetScene(State.activeSceneId) ?? State.scenes[0];
        }

        public string GetEffectivePresetId(PatternInstance instance, PatternDefinition pattern)
        {
            return SoundResolver.GetEffectivePresetId(instance, pattern);
        }

        public SoundProfile GetEffectiveSoundProfile(PatternInstance instance, PatternDefinition pattern)
        {
            return SoundResolver.GetEffectiveSoundProfile(instance, pattern);
        }

        public PatternType GetDrawMode()
        {
            if (Enum.TryParse(State.drawMode, true, out PatternType mode))
                return mode;

            return PatternType.RhythmLoop;
        }

        public void SetDrawMode(PatternType mode)
        {
            string serialized = mode.ToString();
            if (State.drawMode == serialized)
                return;

            State.drawMode = serialized;
            NotifyStateChanged();
        }

        public void SetActiveGroup(string groupId)
        {
            State.activeGroupId = groupId;
            NotifyStateChanged();
        }

        public void SetTempo(float tempo)
        {
            State.tempo = Mathf.Clamp(tempo, 60f, 160f);
            NotifyStateChanged();
        }

        public void SetKey(string keyName)
        {
            State.key = MusicalKeys.All.ContainsKey(keyName) ? keyName : "A minor";
            NotifyStateChanged();
        }

        public void SetSelectedInstance(string instanceId)
        {
            State.selectedInstanceId = instanceId;
            State.selectedPatternId = instanceId != null ? GetInstance(instanceId)?.patternId : null;
            NotifyStateChanged();
        }

        public PatternInstance CommitDraft(DraftResult draft, bool duplicate) => Patterns.CommitDraft(draft, duplicate);

        public PatternInstance SpawnPattern(string patternId, string sceneId = null, Vector3? coords = null, bool notify = true)
            => Patterns.SpawnPattern(patternId, sceneId, coords, notify);

        public void ClonePattern(string patternId) => Patterns.ClonePattern(patternId);

        public void RemoveInstance(string instanceId) => Patterns.RemoveInstance(instanceId);

        public void DuplicateInstance(string instanceId) => Patterns.DuplicateInstance(instanceId);

        public void UpdateInstance(string instanceId, Vector3? position = null, float? depth = null, bool? muted = null)
            => Patterns.UpdateInstance(instanceId, position, depth, muted);

        public void SetPresetOverride(string instanceId, string presetId) => Patterns.SetPresetOverride(instanceId, presetId);

        public void SetActiveScene(string sceneId, bool queueIfPlaying = false) => Scenes.SetActiveScene(sceneId, queueIfPlaying);

        public void QueueScene(string sceneId) => Scenes.QueueScene(sceneId);

        public void CopyScene(string sourceId, string targetId) => Scenes.CopyScene(sourceId, targetId);

        public void UpdateArrangement(string slotId, string sceneId = null, int? bars = null)
            => Scenes.UpdateArrangement(slotId, sceneId, bars);

        public void ClearArrangementScene(string slotId) => Scenes.ClearArrangementScene(slotId);

        private void NotifyStateChanged()
        {
            OnStateChanged?.Invoke();
            EventBus.Publish(new SessionStateChangedEvent(this));
        }
    }
}
