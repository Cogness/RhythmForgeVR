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
        private Dictionary<string, float> _lastTriggerAt = new Dictionary<string, float>();

        private const float LookaheadSeconds = 0.12f;

        public bool IsPlaying => _transport.playing;
        public Transport CurrentTransport => _transport;

        public event Action OnTransportChanged;

        public void Initialize(SessionStore store)
        {
            _store = store;
        }

        public void Play()
        {
            if (_store == null || _transport.playing) return;

            bool hasArrangement = HasArrangement();
            _transport.playing = true;
            _transport.mode = hasArrangement ? "arrangement" : "scene";
            _transport.nextNoteTime = AudioSettings.dspTime + 0.05;
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

            double currentTime = AudioSettings.dspTime;
            float stepDur = SequencerClock.StepDuration(_store.State.tempo);

            while (_transport.nextNoteTime < currentTime + LookaheadSeconds)
            {
                ScheduleCurrentStep(_transport.nextNoteTime);
                AdvanceTransport();
                _transport.nextNoteTime += stepDur;
            }
        }

        private void ScheduleCurrentStep(double time)
        {
            string sceneId = _transport.playbackSceneId;
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
                        ScheduleRhythm(pattern, instance, localStep, stepDur, effectiveSound, preset, group);
                        break;
                    case PatternType.MelodyLine:
                        ScheduleMelody(pattern, instance, localStep, stepDur, effectiveSound, preset, group);
                        break;
                    case PatternType.HarmonyPad:
                        ScheduleHarmony(pattern, instance, localStep, stepDur, effectiveSound, preset, group, totalSteps);
                        break;
                }
            }
        }

        private void ScheduleRhythm(PatternDefinition pattern, PatternInstance instance,
            int localStep, float stepDur, SoundProfile sound,
            InstrumentPreset preset, InstrumentGroup group)
        {
            if (pattern.derivedSequence.events == null) return;

            foreach (var evt in pattern.derivedSequence.events)
            {
                if (evt.step != localStep) continue;

                float swingOffset = evt.step % 2 == 1
                    ? SequencerClock.SwingOffset(pattern.derivedSequence.swing, stepDur) : 0f;
                float microShift = evt.microShift * stepDur;

                _audioEngine.PlayDrumEvent(
                    preset,
                    evt.lane,
                    evt.velocity,
                    instance.pan,
                    instance.brightness,
                    instance.depth,
                    preset.fxSend + group.busFx.reverb * 0.2f,
                    sound
                );

                _lastTriggerAt[instance.id] = Time.time;
            }
        }

        private void ScheduleMelody(PatternDefinition pattern, PatternInstance instance,
            int localStep, float stepDur, SoundProfile sound,
            InstrumentPreset preset, InstrumentGroup group)
        {
            if (pattern.derivedSequence.notes == null) return;

            foreach (var note in pattern.derivedSequence.notes)
            {
                if (note.step != localStep) continue;

                _audioEngine.PlayMelodyNote(
                    preset,
                    note.midi,
                    note.velocity,
                    note.durationSteps * stepDur,
                    instance.pan,
                    instance.brightness,
                    instance.depth,
                    preset.fxSend + group.busFx.delay * 0.1f,
                    sound,
                    note.glide
                );

                _lastTriggerAt[instance.id] = Time.time;
            }
        }

        private void ScheduleHarmony(PatternDefinition pattern, PatternInstance instance,
            int localStep, float stepDur, SoundProfile sound,
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

            _audioEngine.PlayHarmonyChord(
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

            _lastTriggerAt[instance.id] = Time.time;
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
            if (!_lastTriggerAt.TryGetValue(instanceId, out float lastTime))
                return 0f;
            float elapsed = (Time.time - lastTime) * 1000f; // ms
            return Mathf.Clamp01(1f - elapsed / 480f);
        }

        public float GetPhaseForPattern(PatternDefinition pattern, string instanceId)
        {
            if (!_transport.playing) return -1f;

            var instance = _store.GetInstance(instanceId);
            if (instance == null || instance.sceneId != _store.State.activeSceneId) return -1f;

            int localStep = _transport.mode == "arrangement" ? _transport.slotStep : _transport.sceneStep;
            int totalSteps = pattern.derivedSequence?.totalSteps ?? AppStateFactory.BarSteps;
            return (localStep % totalSteps) / Mathf.Max(1f, totalSteps - 1f);
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

            _transport.playbackSceneId = resolvedSceneId;

            if (_store == null || string.IsNullOrEmpty(resolvedSceneId)) return;
            if (_store.State.activeSceneId == resolvedSceneId &&
                string.IsNullOrEmpty(_store.State.queuedSceneId)) return;

            _store.SetActiveScene(resolvedSceneId);
        }
    }
}
