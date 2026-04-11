using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;

namespace RhythmForge.Core.Session
{
    public class SessionStore
    {
        public AppState State { get; private set; }

        public event Action OnStateChanged;

        public SessionStore()
        {
            State = AppStateFactory.CreateEmpty();
        }

        public void LoadState(AppState state)
        {
            State = state ?? AppStateFactory.CreateEmpty();
            NormalizeState();
            OnStateChanged?.Invoke();
        }

        public void Reset()
        {
            State = AppStateFactory.CreateEmpty();
            OnStateChanged?.Invoke();
        }

        // --- Naming ---

        public string NextDraftName(PatternType type)
        {
            switch (type)
            {
                case PatternType.RhythmLoop:
                    return $"Beat-{State.counters.rhythm:D2}";
                case PatternType.MelodyLine:
                    return $"Melody-{State.counters.melody:D2}";
                default:
                    return $"Pad-{State.counters.harmony:D2}";
            }
        }

        public string ReserveName(PatternType type)
        {
            string name = NextDraftName(type);
            switch (type)
            {
                case PatternType.RhythmLoop: State.counters.rhythm++; break;
                case PatternType.MelodyLine: State.counters.melody++; break;
                default: State.counters.harmony++; break;
            }
            return name;
        }

        // --- Lookups ---

        public InstrumentGroup GetGroup(string groupId) => InstrumentGroups.Get(groupId);
        public InstrumentPreset GetPreset(string presetId) => InstrumentPresets.Get(presetId);

        public PatternDefinition GetPattern(string patternId)
        {
            foreach (var p in State.patterns)
                if (p.id == patternId) return p;
            return null;
        }

        public SceneData GetScene(string sceneId)
        {
            foreach (var s in State.scenes)
                if (s.id == sceneId) return s;
            return null;
        }

        public PatternInstance GetInstance(string instanceId)
        {
            foreach (var i in State.instances)
                if (i.id == instanceId) return i;
            return null;
        }

        public List<PatternInstance> GetSceneInstances(string sceneId)
        {
            var result = new List<PatternInstance>();
            foreach (var inst in State.instances)
                if (inst.sceneId == sceneId) result.Add(inst);
            return result;
        }

        public SceneData GetActiveScene()
        {
            return GetScene(State.activeSceneId) ?? State.scenes[0];
        }

        public string GetEffectivePresetId(PatternInstance instance, PatternDefinition pattern)
        {
            return !string.IsNullOrEmpty(instance.presetOverrideId) ? instance.presetOverrideId : pattern.presetId;
        }

        public SoundProfile GetEffectiveSoundProfile(PatternInstance instance, PatternDefinition pattern)
        {
            var preset = GetPreset(GetEffectivePresetId(instance, pattern));
            var geometry = pattern.soundProfile ?? SoundProfileMapper.Derive(pattern.type, pattern.shapeProfile);
            var presetBias = PresetBiasResolver.GetPresetBias(preset, pattern.type);
            return PresetBiasResolver.ResolveEffective(geometry, presetBias);
        }

        // --- Mutations ---

        public PatternType GetDrawMode()
        {
            if (Enum.TryParse(State.drawMode, true, out PatternType mode))
                return mode;

            return PatternType.RhythmLoop;
        }

        public void SetDrawMode(PatternType mode)
        {
            string serialized = mode.ToString();
            if (State.drawMode == serialized) return;

            State.drawMode = serialized;
            OnStateChanged?.Invoke();
        }

        public void SetActiveGroup(string groupId)
        {
            State.activeGroupId = groupId;
            OnStateChanged?.Invoke();
        }

        public void SetTempo(float tempo)
        {
            State.tempo = Mathf.Clamp(tempo, 60f, 160f);
            OnStateChanged?.Invoke();
        }

        public void SetKey(string keyName)
        {
            State.key = MusicalKeys.All.ContainsKey(keyName) ? keyName : "A minor";
            OnStateChanged?.Invoke();
        }

        public void SetSelectedInstance(string instanceId)
        {
            State.selectedInstanceId = instanceId;
            State.selectedPatternId = instanceId != null ? GetInstance(instanceId)?.patternId : null;
            OnStateChanged?.Invoke();
        }

        public PatternInstance CommitDraft(DraftResult draft, bool duplicate)
        {
            var pattern = new PatternDefinition
            {
                id = MathUtils.CreateId("pattern"),
                type = draft.type,
                name = draft.name ?? ReserveName(draft.type),
                bars = draft.bars,
                tempoBase = draft.tempoBase,
                key = draft.key,
                groupId = draft.groupId,
                presetId = draft.presetId,
                points = draft.points,
                derivedSequence = draft.derivedSequence,
                tags = draft.tags,
                color = draft.color,
                shapeProfile = draft.shapeProfile,
                soundProfile = draft.soundProfile,
                shapeSummary = draft.shapeSummary,
                summary = draft.summary,
                details = draft.details
            };

            State.patterns.Insert(0, pattern);

            var instance = SpawnPattern(pattern.id, State.activeSceneId, draft.spawnPosition, false);
            if (duplicate)
            {
                Vector3 dupPos = draft.spawnPosition + new Vector3(0.15f, 0.1f, 0f);
                SpawnPattern(pattern.id, State.activeSceneId, dupPos, false);
            }

            State.selectedPatternId = pattern.id;
            State.selectedInstanceId = instance?.id;
            OnStateChanged?.Invoke();
            return instance;
        }

        public PatternInstance SpawnPattern(string patternId, string sceneId = null, Vector3? coords = null, bool notify = true)
        {
            sceneId = sceneId ?? State.activeSceneId;
            var pattern = GetPattern(patternId);
            var scene = GetScene(sceneId);
            if (pattern == null || scene == null) return null;

            Vector3 position = coords ?? GetNextSpawnPosition(sceneId);
            float depth = position.z;

            var instance = new PatternInstance(patternId, sceneId, position, depth);
            State.instances.Add(instance);
            scene.instanceIds.Add(instance.id);
            State.selectedInstanceId = instance.id;
            State.selectedPatternId = patternId;

            if (notify) OnStateChanged?.Invoke();
            return instance;
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

        public void ClonePattern(string patternId)
        {
            var source = GetPattern(patternId);
            if (source == null) return;

            var dup = source.Clone();
            State.patterns.Insert(0, dup);
            State.selectedPatternId = dup.id;
            State.selectedInstanceId = null;
            OnStateChanged?.Invoke();
        }

        public void RemoveInstance(string instanceId)
        {
            var instance = GetInstance(instanceId);
            if (instance == null) return;

            var scene = GetScene(instance.sceneId);
            scene?.instanceIds.Remove(instanceId);
            State.instances.RemoveAll(i => i.id == instanceId);

            if (State.selectedInstanceId == instanceId)
                State.selectedInstanceId = null;

            OnStateChanged?.Invoke();
        }

        public void DuplicateInstance(string instanceId)
        {
            var instance = GetInstance(instanceId);
            if (instance == null) return;

            var dup = instance.Clone();
            dup.position += new Vector3(0.1f, 0.1f, 0f);
            dup.RecalculateMixFromPosition();

            State.instances.Add(dup);
            var scene = GetScene(instance.sceneId);
            scene?.instanceIds.Add(dup.id);
            State.selectedInstanceId = dup.id;
            OnStateChanged?.Invoke();
        }

        public void UpdateInstance(string instanceId, Vector3? position = null, float? depth = null, bool? muted = null)
        {
            var instance = GetInstance(instanceId);
            if (instance == null) return;

            if (position.HasValue) instance.position = position.Value;
            if (depth.HasValue) instance.depth = depth.Value;
            if (muted.HasValue) instance.muted = muted.Value;

            instance.RecalculateMixFromPosition();
            OnStateChanged?.Invoke();
        }

        public void SetPresetOverride(string instanceId, string presetId)
        {
            var instance = GetInstance(instanceId);
            if (instance == null) return;
            instance.presetOverrideId = string.IsNullOrEmpty(presetId) ? null : presetId;
            OnStateChanged?.Invoke();
        }

        public void SetActiveScene(string sceneId, bool queueIfPlaying = false)
        {
            if (GetScene(sceneId) == null) return;
            State.activeSceneId = sceneId;
            State.queuedSceneId = null;

            if (State.selectedInstanceId != null)
            {
                var selected = GetInstance(State.selectedInstanceId);
                if (selected == null || selected.sceneId != sceneId)
                    State.selectedInstanceId = null;
            }
            OnStateChanged?.Invoke();
        }

        public void QueueScene(string sceneId)
        {
            State.queuedSceneId = sceneId;
            OnStateChanged?.Invoke();
        }

        public void CopyScene(string sourceId, string targetId)
        {
            var source = GetScene(sourceId);
            var target = GetScene(targetId);
            if (source == null || target == null || sourceId == targetId) return;

            // Remove existing instances in target
            var toRemove = new HashSet<string>(target.instanceIds);
            State.instances.RemoveAll(i => toRemove.Contains(i.id));
            target.instanceIds.Clear();

            // Copy instances from source
            foreach (var instanceId in source.instanceIds)
            {
                var srcInst = GetInstance(instanceId);
                if (srcInst == null) continue;

                var copied = srcInst.Clone();
                copied.sceneId = targetId;
                target.instanceIds.Add(copied.id);
                State.instances.Add(copied);
            }

            State.activeSceneId = targetId;
            State.selectedInstanceId = null;
            OnStateChanged?.Invoke();
        }

        public void UpdateArrangement(string slotId, string sceneId = null, int? bars = null)
        {
            foreach (var slot in State.arrangement)
            {
                if (slot.id == slotId)
                {
                    if (sceneId != null) slot.sceneId = sceneId;
                    if (bars.HasValue) slot.bars = bars.Value;
                    break;
                }
            }
            OnStateChanged?.Invoke();
        }

        // --- State normalization ---

        private void NormalizeState()
        {
            var fallback = AppStateFactory.CreateEmpty();
            State.version = 2;

            if (State.scenes == null || State.scenes.Count != 4)
                State.scenes = fallback.scenes;

            if (State.arrangement == null || State.arrangement.Count != AppStateFactory.MaxArrangementSlots)
                State.arrangement = fallback.arrangement;

            if (State.patterns == null) State.patterns = new List<PatternDefinition>();
            if (State.instances == null) State.instances = new List<PatternInstance>();
            if (State.counters == null) State.counters = new DraftCounters();

            if (string.IsNullOrEmpty(State.activeGroupId)) State.activeGroupId = "lofi";
            if (string.IsNullOrEmpty(State.drawMode)) State.drawMode = PatternType.RhythmLoop.ToString();
            State.drawMode = GetDrawMode().ToString();
            if (string.IsNullOrEmpty(State.activeSceneId)) State.activeSceneId = "scene-a";

            CleanupSceneMembership();
        }

        private void CleanupSceneMembership()
        {
            var instanceIds = new HashSet<string>();
            foreach (var inst in State.instances)
                instanceIds.Add(inst.id);

            foreach (var scene in State.scenes)
            {
                scene.instanceIds.RemoveAll(id => !instanceIds.Contains(id));
            }

            foreach (var inst in State.instances)
            {
                var scene = GetScene(inst.sceneId);
                if (scene != null && !scene.instanceIds.Contains(inst.id))
                    scene.instanceIds.Add(inst.id);
            }
        }
    }
}
