using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;

namespace RhythmForge.Sequencer
{
    internal sealed class PlaybackVisualTracker
    {
        private readonly Dictionary<string, double> _lastTriggerAt = new Dictionary<string, double>();
        private readonly Dictionary<string, PlaybackActivity> _playbackActivity = new Dictionary<string, PlaybackActivity>();
        private readonly List<ScheduledTransportStep> _scheduledTransportSteps = new List<ScheduledTransportStep>();
        private readonly List<string> _expiredScratch = new List<string>();

        private const float PulseWindowSeconds = 0.48f;

        private struct PlaybackActivity
        {
            public double triggerAt;
            public double activeUntil;
        }

        private struct ScheduledTransportStep
        {
            public double dspTime;
            public string mode;
            public string sceneId;
            public int sceneStep;
            public int slotStep;
            public int slotIndex;
        }

        public float GetPulse(string instanceId, double visualTime)
        {
            if (!_lastTriggerAt.TryGetValue(instanceId, out double lastTime))
                return 0f;

            double elapsed = visualTime - lastTime;
            if (elapsed < 0d)
                return 0f;

            return Mathf.Clamp01(1f - (float)(elapsed / PulseWindowSeconds));
        }

        public bool TryGetPlaybackVisualState(
            PatternDefinition pattern,
            string instanceId,
            SessionStore store,
            Transport transport,
            string playbackSceneId,
            double dspTime,
            double visualTime,
            float stepDuration,
            out PatternPlaybackVisualState state)
        {
            state = PatternPlaybackVisualState.CreateInactive(pattern?.type ?? PatternType.RhythmLoop, null, playbackSceneId);

            if (transport == null || !transport.playing || store == null || pattern == null)
                return false;

            var instance = store.GetInstance(instanceId);
            if (instance == null || instance.muted || instance.sceneId != playbackSceneId)
                return false;

            var sound = store.GetEffectiveSoundProfile(instance, pattern);
            state = PatternPlaybackVisualState.CreateInactive(pattern.type, sound, playbackSceneId);

            float sustainAmount = GetSustainAmount(instanceId, visualTime);
            state.pulse = GetPulse(instanceId, visualTime);
            state.sustainAmount = sustainAmount;
            state.isActive = sustainAmount > 0.001f || state.pulse > 0.001f;
            state.isShapeNative = pattern.musicalShape != null;

            if (TryGetCurrentScheduledStep(dspTime, stepDuration, out var scheduledStep, out float stepProgress) &&
                scheduledStep.sceneId == playbackSceneId)
            {
                int totalSteps = GetPatternLoopSteps(pattern);
                int localStep = scheduledStep.mode == "arrangement"
                    ? scheduledStep.slotStep
                    : scheduledStep.sceneStep;
                localStep = totalSteps > 0 ? localStep % totalSteps : 0;

                state.phase = Mathf.Repeat(
                    (localStep + stepProgress) / Mathf.Max(1f, totalSteps),
                    1f);

                if (pattern.musicalShape != null)
                {
                    state.rhythmPulse = ComputeRhythmPulse(pattern.musicalShape, localStep, stepProgress);
                    state.melodyMotion = ComputeMelodyMotion(pattern.musicalShape, localStep, stepProgress);
                    state.harmonySustain = ComputeHarmonySustain(pattern.musicalShape, localStep, stepProgress);
                    state.isActive = state.isActive ||
                        state.rhythmPulse > 0.001f ||
                        state.melodyMotion > 0.001f ||
                        state.harmonySustain > 0.001f;
                }
            }
            else if (pattern.musicalShape != null)
            {
                state.rhythmPulse = state.pulse;
                state.melodyMotion = state.pulse;
                state.harmonySustain = state.sustainAmount;
            }

            return true;
        }

        public void RecordScheduledTransportStep(Transport transport, double dspTime, string sceneId)
        {
            if (transport == null || string.IsNullOrEmpty(sceneId))
                return;

            var step = new ScheduledTransportStep
            {
                dspTime = dspTime,
                mode = transport.mode,
                sceneId = sceneId,
                sceneStep = transport.sceneStep,
                slotStep = transport.slotStep,
                slotIndex = transport.slotIndex
            };

            if (_scheduledTransportSteps.Count > 0)
            {
                var last = _scheduledTransportSteps[_scheduledTransportSteps.Count - 1];
                if (Math.Abs(last.dspTime - step.dspTime) < 0.0001d &&
                    last.sceneId == step.sceneId &&
                    last.sceneStep == step.sceneStep &&
                    last.slotStep == step.slotStep &&
                    last.slotIndex == step.slotIndex &&
                    last.mode == step.mode)
                {
                    return;
                }
            }

            _scheduledTransportSteps.Add(step);
        }

        public void RecordTrigger(string instanceId, double scheduledTime, float activeDuration, double dspTime, double visualTime)
        {
            double leadSeconds = Math.Max(0d, scheduledTime - dspTime);
            double triggerAt = visualTime + leadSeconds;
            double activeUntil = triggerAt + Math.Max(0.08f, activeDuration);

            if (_playbackActivity.TryGetValue(instanceId, out var existing))
            {
                triggerAt = Math.Max(existing.triggerAt, triggerAt);
                activeUntil = Math.Max(existing.activeUntil, activeUntil);
            }

            _lastTriggerAt[instanceId] = triggerAt;
            _playbackActivity[instanceId] = new PlaybackActivity
            {
                triggerAt = triggerAt,
                activeUntil = activeUntil
            };
        }

        public void Clear(string sceneId = null, SessionStore store = null)
        {
            if (store == null || string.IsNullOrEmpty(sceneId))
            {
                _lastTriggerAt.Clear();
                _playbackActivity.Clear();
                _scheduledTransportSteps.Clear();
                return;
            }

            foreach (var instance in store.GetSceneInstances(sceneId))
            {
                _lastTriggerAt.Remove(instance.id);
                _playbackActivity.Remove(instance.id);
            }

            _scheduledTransportSteps.RemoveAll(step => step.sceneId == sceneId);
        }

        public void Prune(double visualTime, double dspTime, float stepDuration)
        {
            _expiredScratch.Clear();
            foreach (var kvp in _lastTriggerAt)
            {
                if (kvp.Value < visualTime - PulseWindowSeconds - 0.05d)
                    _expiredScratch.Add(kvp.Key);
            }
            for (int i = 0; i < _expiredScratch.Count; i++)
                _lastTriggerAt.Remove(_expiredScratch[i]);

            _expiredScratch.Clear();
            foreach (var kvp in _playbackActivity)
            {
                if (kvp.Value.activeUntil < visualTime - 0.05d)
                    _expiredScratch.Add(kvp.Key);
            }
            for (int i = 0; i < _expiredScratch.Count; i++)
                _playbackActivity.Remove(_expiredScratch[i]);

            double pruneThreshold = dspTime - stepDuration * 2f;
            for (int i = _scheduledTransportSteps.Count - 1; i >= 0; i--)
            {
                if (_scheduledTransportSteps[i].dspTime < pruneThreshold)
                    _scheduledTransportSteps.RemoveAt(i);
            }
        }

        private bool TryGetCurrentScheduledStep(double dspTime, float stepDuration, out ScheduledTransportStep step, out float stepProgress)
        {
            step = default;
            stepProgress = 0f;

            if (_scheduledTransportSteps.Count == 0)
                return false;

            int activeIndex = -1;
            for (int i = 0; i < _scheduledTransportSteps.Count; i++)
            {
                if (_scheduledTransportSteps[i].dspTime <= dspTime + 0.0001d)
                    activeIndex = i;
                else
                    break;
            }

            if (activeIndex < 0)
            {
                step = _scheduledTransportSteps[0];
                return true;
            }

            step = _scheduledTransportSteps[activeIndex];
            if (stepDuration > 0.0001f)
                stepProgress = Mathf.Clamp01((float)((dspTime - step.dspTime) / stepDuration));

            return true;
        }

        private float GetSustainAmount(string instanceId, double visualTime)
        {
            if (!_playbackActivity.TryGetValue(instanceId, out var activity))
                return 0f;

            if (visualTime < activity.triggerAt || activity.activeUntil <= activity.triggerAt)
                return 0f;

            if (visualTime >= activity.activeUntil)
                return 0f;

            return Mathf.Clamp01((float)((activity.activeUntil - visualTime) / (activity.activeUntil - activity.triggerAt)));
        }

        private static int GetPatternLoopSteps(PatternDefinition pattern)
        {
            if (pattern?.musicalShape?.totalSteps > 0)
                return pattern.musicalShape.totalSteps;
            if (pattern?.derivedSequence?.totalSteps > 0)
                return pattern.derivedSequence.totalSteps;
            return AppStateFactory.BarSteps;
        }

        private static float ComputeRhythmPulse(MusicalShape shape, int localStep, float stepProgress)
        {
            var rhythm = shape?.facets?.rhythm;
            if (rhythm?.events == null)
                return 0f;

            for (int i = 0; i < rhythm.events.Count; i++)
            {
                var evt = rhythm.events[i];
                if (evt == null)
                    continue;

                if (evt.step == localStep)
                    return Mathf.Clamp01(1f - stepProgress * 0.8f);
            }

            return 0f;
        }

        private static float ComputeMelodyMotion(MusicalShape shape, int localStep, float stepProgress)
        {
            var melody = shape?.facets?.melody;
            if (melody?.notes == null)
                return 0f;

            for (int i = 0; i < melody.notes.Count; i++)
            {
                var note = melody.notes[i];
                if (note == null)
                    continue;

                int duration = Mathf.Max(1, note.durationSteps);
                if (localStep >= note.step && localStep < note.step + duration)
                {
                    float noteProgress = (localStep - note.step + stepProgress) / duration;
                    return Mathf.Clamp01(1f - noteProgress * 0.4f);
                }
            }

            return 0f;
        }

        private static float ComputeHarmonySustain(MusicalShape shape, int localStep, float stepProgress)
        {
            var harmony = shape?.facets?.harmony;
            if (harmony?.events == null)
                return 0f;

            for (int i = 0; i < harmony.events.Count; i++)
            {
                var evt = harmony.events[i];
                if (evt == null)
                    continue;

                int duration = Mathf.Max(1, evt.durationSteps);
                if (localStep >= evt.step && localStep < evt.step + duration)
                {
                    float chordProgress = (localStep - evt.step + stepProgress) / duration;
                    return Mathf.Clamp01(1f - chordProgress * 0.35f);
                }
            }

            return 0f;
        }
    }
}
