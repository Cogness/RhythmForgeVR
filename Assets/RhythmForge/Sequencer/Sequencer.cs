using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;
using RhythmForge.Audio;

namespace RhythmForge.Sequencer
{
    /// <summary>
    /// Lookahead scheduler that drives pattern playback in scene or arrangement mode.
    /// Ported from the pilot's sequencer logic.
    /// </summary>
    public class Sequencer : MonoBehaviour
    {
        [SerializeField] private AudioEngine _audioEngine;

        private SessionStore _store;
        private Transport _transport = new Transport();

        // Pulse tracking for visual feedback
        private readonly Dictionary<string, double> _lastTriggerAt = new Dictionary<string, double>();
        private readonly Dictionary<string, PlaybackActivity> _playbackActivity = new Dictionary<string, PlaybackActivity>();
        private readonly List<ScheduledTransportStep> _scheduledTransportSteps = new List<ScheduledTransportStep>();

        private const float LookaheadSeconds = 0.12f;
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

        public bool IsPlaying => _transport.playing;
        public Transport CurrentTransport => _transport;

        public event Action OnTransportChanged;

        public void Initialize(SessionStore store)
        {
            _store = store;
            if (_audioEngine == null)
                _audioEngine = GetComponent<AudioEngine>();
        }

        protected virtual double GetDspTime() => AudioSettings.dspTime;
        protected virtual double GetVisualTimeSeconds() => Time.realtimeSinceStartup;

        public void Play()
        {
            if (_store == null || _transport.playing) return;

            ClearPlaybackVisualState();

            bool hasArrangement = HasArrangement();
            _transport.playing = true;
            _transport.mode = hasArrangement ? "arrangement" : "scene";
            _transport.nextNoteTime = GetDspTime() + 0.05;
            _transport.sceneStep = 0;
            _transport.slotStep = 0;
            _transport.absoluteBar = 1;

            if (hasArrangement)
            {
                _transport.slotIndex = FindFirstPopulatedSlot();
                SetPlaybackScene(_store.State.arrangement[_transport.slotIndex]?.sceneId);
            }
            else
            {
                _transport.slotIndex = -1;
                _transport.playbackSceneId = _store.State.activeSceneId;
            }

            OnTransportChanged?.Invoke();
        }

        public void Stop()
        {
            if (!_transport.playing) return;
            _transport.playing = false;
            ClearPlaybackVisualState();
            OnTransportChanged?.Invoke();
        }

        public void TogglePlayback()
        {
            if (_transport.playing) Stop();
            else Play();
        }

        private void Update()
        {
            if (!_transport.playing || _store == null) return;

            double currentTime = GetDspTime();
            float stepDur = SequencerClock.StepDuration(_store.State.tempo);

            while (_transport.nextNoteTime < currentTime + LookaheadSeconds)
            {
                ScheduleCurrentStep(_transport.nextNoteTime);
                AdvanceTransport();
                _transport.nextNoteTime += stepDur;
            }

            PrunePlaybackVisualState();
        }

        private void ScheduleCurrentStep(double time)
        {
            string sceneId = _transport.playbackSceneId;
            RecordScheduledTransportStep(time, sceneId);

            var instances = _store.GetSceneInstances(sceneId);
            int currentStep = _transport.mode == "arrangement" ? _transport.slotStep : _transport.sceneStep;
            float stepDur = SequencerClock.StepDuration(_store.State.tempo);

            foreach (var instance in instances)
            {
                if (instance.muted) continue;

                var pattern = _store.GetPattern(instance.patternId);
                if (pattern == null || pattern.derivedSequence == null) continue;

                var effectiveSound = _store.GetEffectiveSoundProfile(instance, pattern);
                var preset = _store.GetPreset(_store.GetEffectivePresetId(instance, pattern));
                var group = _store.GetGroup(pattern.groupId);

                int totalSteps = pattern.derivedSequence.totalSteps > 0
                    ? pattern.derivedSequence.totalSteps
                    : AppStateFactory.BarSteps;
                int localStep = currentStep % totalSteps;

                switch (pattern.type)
                {
                    case PatternType.RhythmLoop:
                        ScheduleRhythm(pattern, instance, localStep, time, effectiveSound, preset, group);
                        break;
                    case PatternType.MelodyLine:
                        ScheduleMelody(pattern, instance, localStep, stepDur, time, effectiveSound, preset, group);
                        break;
                    case PatternType.HarmonyPad:
                        ScheduleHarmony(pattern, instance, localStep, stepDur, time, effectiveSound, preset, group, totalSteps);
                        break;
                }
            }
        }

        private void ScheduleRhythm(PatternDefinition pattern, PatternInstance instance,
            int localStep, double scheduledTime, SoundProfile sound,
            InstrumentPreset preset, InstrumentGroup group)
        {
            if (pattern.derivedSequence.events == null) return;

            foreach (var evt in pattern.derivedSequence.events)
            {
                if (evt.step != localStep) continue;

                _audioEngine?.PlayDrumEvent(
                    preset,
                    evt.lane,
                    evt.velocity,
                    instance.pan,
                    instance.brightness,
                    instance.depth,
                    preset.fxSend + group.busFx.reverb * 0.2f,
                    sound
                );

                RecordTrigger(instance.id, scheduledTime, GetRhythmVisualDuration(evt.lane, sound));
            }
        }

        private void ScheduleMelody(PatternDefinition pattern, PatternInstance instance,
            int localStep, float stepDur, double scheduledTime, SoundProfile sound,
            InstrumentPreset preset, InstrumentGroup group)
        {
            if (pattern.derivedSequence.notes == null) return;

            foreach (var note in pattern.derivedSequence.notes)
            {
                if (note.step != localStep) continue;

                float duration = note.durationSteps * stepDur;

                _audioEngine?.PlayMelodyNote(
                    preset,
                    note.midi,
                    note.velocity,
                    duration,
                    instance.pan,
                    instance.brightness,
                    instance.depth,
                    preset.fxSend + group.busFx.delay * 0.1f,
                    sound,
                    note.glide
                );

                RecordTrigger(instance.id, scheduledTime, GetMelodyVisualDuration(duration, sound));
            }
        }

        private void ScheduleHarmony(PatternDefinition pattern, PatternInstance instance,
            int localStep, float stepDur, double scheduledTime, SoundProfile sound,
            InstrumentPreset preset, InstrumentGroup group, int totalSteps)
        {
            if (localStep != 0 || pattern.derivedSequence.chord == null) return;

            int effectiveSteps = totalSteps;
            if (_transport.mode == "arrangement" && _transport.slotIndex >= 0)
            {
                var slot = _store.State.arrangement[_transport.slotIndex];
                if (slot != null)
                    effectiveSteps = Mathf.Min(totalSteps, slot.bars * AppStateFactory.BarSteps);
            }

            float duration = effectiveSteps * stepDur * 0.96f;

            _audioEngine?.PlayHarmonyChord(
                preset,
                pattern.derivedSequence.chord,
                0.38f,
                duration,
                instance.pan,
                instance.brightness,
                instance.depth,
                preset.fxSend + group.busFx.reverb * 0.18f,
                sound
            );

            RecordTrigger(instance.id, scheduledTime, GetHarmonyVisualDuration(duration, sound));
        }

        private void AdvanceTransport()
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
                    int nextIndex = FindNextPopulatedSlot(_transport.slotIndex);
                    _transport.slotIndex = nextIndex;
                    _transport.slotStep = 0;
                    SetPlaybackScene(_store.State.arrangement[nextIndex]?.sceneId);
                    transportChanged = true;
                }

                if (transportChanged)
                    OnTransportChanged?.Invoke();
                return;
            }

            // Scene mode
            _transport.sceneStep++;
            if (_transport.sceneStep % AppStateFactory.BarSteps == 0)
            {
                bool transportChanged = true;
                _transport.absoluteBar++;

                if (!string.IsNullOrEmpty(_store.State.queuedSceneId))
                {
                    string queuedSceneId = _store.State.queuedSceneId;
                    SetPlaybackScene(queuedSceneId);
                    _transport.sceneStep = 0;
                }

                if (transportChanged)
                    OnTransportChanged?.Invoke();
            }
        }

        // --- Pulse for visual feedback ---

        public float GetPulse(string instanceId)
        {
            if (!_lastTriggerAt.TryGetValue(instanceId, out double lastTime))
                return 0f;

            double elapsed = GetVisualTimeSeconds() - lastTime;
            if (elapsed < 0d)
                return 0f;

            return Mathf.Clamp01(1f - (float)(elapsed / PulseWindowSeconds));
        }

        public float GetPhaseForPattern(PatternDefinition pattern, string instanceId)
        {
            if (!TryGetPlaybackVisualState(pattern, instanceId, out var state))
                return -1f;

            return state.phase;
        }

        public bool TryGetPlaybackVisualState(PatternDefinition pattern, string instanceId, out PatternPlaybackVisualState state)
        {
            string playbackSceneId = GetPlaybackSceneId();
            state = PatternPlaybackVisualState.CreateInactive(pattern?.type ?? PatternType.RhythmLoop, null, playbackSceneId);

            if (!_transport.playing || _store == null || pattern == null || pattern.derivedSequence == null)
                return false;

            var instance = _store.GetInstance(instanceId);
            if (instance == null || instance.muted || instance.sceneId != playbackSceneId)
                return false;

            var sound = _store.GetEffectiveSoundProfile(instance, pattern);
            state = PatternPlaybackVisualState.CreateInactive(pattern.type, sound, playbackSceneId);

            float sustainAmount = GetSustainAmount(instanceId);
            state.pulse = GetPulse(instanceId);
            state.sustainAmount = sustainAmount;
            state.isActive = sustainAmount > 0.001f || state.pulse > 0.001f;

            if (TryGetCurrentScheduledStep(out var scheduledStep, out float stepProgress) &&
                scheduledStep.sceneId == playbackSceneId)
            {
                int totalSteps = pattern.derivedSequence.totalSteps > 0
                    ? pattern.derivedSequence.totalSteps
                    : AppStateFactory.BarSteps;
                int localStep = scheduledStep.mode == "arrangement"
                    ? scheduledStep.slotStep
                    : scheduledStep.sceneStep;

                state.phase = Mathf.Repeat(
                    (localStep + stepProgress) / Mathf.Max(1f, totalSteps),
                    1f);
            }

            return true;
        }

        // --- Arrangement helpers ---

        public bool HasArrangement()
        {
            if (_store?.State?.arrangement == null) return false;
            foreach (var slot in _store.State.arrangement)
                if (slot.IsPopulated) return true;
            return false;
        }

        public string GetPlaybackSceneId()
        {
            if (!_transport.playing) return _store?.State?.activeSceneId;
            return _transport.playbackSceneId ?? _store?.State?.activeSceneId;
        }

        private int FindFirstPopulatedSlot()
        {
            for (int i = 0; i < _store.State.arrangement.Count; i++)
                if (_store.State.arrangement[i].IsPopulated) return i;
            return 0;
        }

        private int FindNextPopulatedSlot(int currentIndex)
        {
            var populated = new List<int>();
            for (int i = 0; i < _store.State.arrangement.Count; i++)
                if (_store.State.arrangement[i].IsPopulated) populated.Add(i);

            if (populated.Count == 0) return 0;
            int currentPos = populated.IndexOf(currentIndex);
            return populated[(currentPos + 1) % populated.Count];
        }

        private void SetPlaybackScene(string sceneId)
        {
            string resolvedSceneId = !string.IsNullOrEmpty(sceneId)
                ? sceneId
                : _store.State.activeSceneId;

            string previousSceneId = _transport.playbackSceneId;
            _transport.playbackSceneId = resolvedSceneId;

            if (!string.IsNullOrEmpty(previousSceneId) && previousSceneId != resolvedSceneId)
                ClearPlaybackVisualState(previousSceneId);

            if (_store == null || string.IsNullOrEmpty(resolvedSceneId)) return;
            if (_store.State.activeSceneId == resolvedSceneId &&
                string.IsNullOrEmpty(_store.State.queuedSceneId)) return;

            _store.SetActiveScene(resolvedSceneId);
        }

        private void RecordScheduledTransportStep(double dspTime, string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId))
                return;

            var step = new ScheduledTransportStep
            {
                dspTime = dspTime,
                mode = _transport.mode,
                sceneId = sceneId,
                sceneStep = _transport.sceneStep,
                slotStep = _transport.slotStep,
                slotIndex = _transport.slotIndex
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

        private bool TryGetCurrentScheduledStep(out ScheduledTransportStep step, out float stepProgress)
        {
            step = default;
            stepProgress = 0f;

            if (_scheduledTransportSteps.Count == 0 || _store == null)
                return false;

            PrunePlaybackVisualState();

            double now = GetDspTime();
            int activeIndex = -1;
            for (int i = 0; i < _scheduledTransportSteps.Count; i++)
            {
                if (_scheduledTransportSteps[i].dspTime <= now + 0.0001d)
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
            float stepDuration = SequencerClock.StepDuration(_store.State.tempo);
            if (stepDuration > 0.0001f)
                stepProgress = Mathf.Clamp01((float)((now - step.dspTime) / stepDuration));

            return true;
        }

        private void RecordTrigger(string instanceId, double scheduledTime, float activeDuration)
        {
            double leadSeconds = Math.Max(0d, scheduledTime - GetDspTime());
            double triggerAt = GetVisualTimeSeconds() + leadSeconds;
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

        private float GetSustainAmount(string instanceId)
        {
            if (!_playbackActivity.TryGetValue(instanceId, out var activity))
                return 0f;

            double now = GetVisualTimeSeconds();
            if (now < activity.triggerAt || activity.activeUntil <= activity.triggerAt)
                return 0f;

            if (now >= activity.activeUntil)
                return 0f;

            return Mathf.Clamp01((float)((activity.activeUntil - now) / (activity.activeUntil - activity.triggerAt)));
        }

        private void ClearPlaybackVisualState(string sceneId = null)
        {
            if (_store == null || string.IsNullOrEmpty(sceneId))
            {
                _lastTriggerAt.Clear();
                _playbackActivity.Clear();
                _scheduledTransportSteps.Clear();
                return;
            }

            foreach (var instance in _store.GetSceneInstances(sceneId))
            {
                _lastTriggerAt.Remove(instance.id);
                _playbackActivity.Remove(instance.id);
            }

            _scheduledTransportSteps.RemoveAll(step => step.sceneId == sceneId);
        }

        private void PrunePlaybackVisualState()
        {
            double nowVisual = GetVisualTimeSeconds();
            double nowDsp = GetDspTime();
            float stepDuration = _store != null
                ? SequencerClock.StepDuration(_store.State.tempo)
                : SequencerClock.StepDuration(120f);

            var expiredPulseIds = new List<string>();
            foreach (var kvp in _lastTriggerAt)
            {
                if (kvp.Value < nowVisual - PulseWindowSeconds - 0.05d)
                    expiredPulseIds.Add(kvp.Key);
            }

            foreach (var id in expiredPulseIds)
                _lastTriggerAt.Remove(id);

            var expiredActivityIds = new List<string>();
            foreach (var kvp in _playbackActivity)
            {
                if (kvp.Value.activeUntil < nowVisual - 0.05d)
                    expiredActivityIds.Add(kvp.Key);
            }

            foreach (var id in expiredActivityIds)
                _playbackActivity.Remove(id);

            _scheduledTransportSteps.RemoveAll(step => step.dspTime < nowDsp - stepDuration * 2f);
        }

        private static float GetRhythmVisualDuration(string lane, SoundProfile sound)
        {
            float baseDuration = lane == "kick"
                ? 0.22f
                : lane == "snare" ? 0.18f
                : lane == "perc" ? 0.14f : 0.1f;

            sound = sound ?? new SoundProfile();
            return baseDuration + sound.body * 0.14f + sound.releaseBias * 0.24f;
        }

        private static float GetMelodyVisualDuration(float noteDuration, SoundProfile sound)
        {
            sound = sound ?? new SoundProfile();
            return noteDuration + 0.06f + sound.releaseBias * 0.42f + sound.body * 0.08f;
        }

        private static float GetHarmonyVisualDuration(float chordDuration, SoundProfile sound)
        {
            sound = sound ?? new SoundProfile();
            return chordDuration + 0.14f + sound.releaseBias * 0.78f + sound.reverbBias * 0.22f;
        }
    }
}
