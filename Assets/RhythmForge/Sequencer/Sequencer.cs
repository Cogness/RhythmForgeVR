using System;
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
        private IAudioDispatcher _audioDispatcher;
        private ArrangementNavigator _arrangementNavigator;
        private TransportController _transportController;
        private PlaybackVisualTracker _playbackVisualTracker;
        private readonly Transport _fallbackTransport = new Transport();

        private const float LookaheadSeconds = 0.12f;

        public bool IsPlaying => _transportController != null && _transportController.IsPlaying;
        public Transport CurrentTransport => _transportController?.CurrentTransport ?? _fallbackTransport;

        public event Action OnTransportChanged;

        public void Initialize(SessionStore store)
        {
            if (_transportController != null)
                _transportController.OnPlaybackSceneChanged -= HandlePlaybackSceneChanged;

            _store = store;
            _arrangementNavigator = new ArrangementNavigator(store);
            _transportController = new TransportController(store, _arrangementNavigator, GetDspTime);
            _transportController.OnPlaybackSceneChanged += HandlePlaybackSceneChanged;
            _playbackVisualTracker = new PlaybackVisualTracker();

            if (_audioEngine == null)
                _audioEngine = GetComponent<AudioEngine>();
            _audioDispatcher = _audioEngine;
        }

        protected virtual double GetDspTime() => AudioSettings.dspTime;
        protected virtual double GetVisualTimeSeconds() => Time.realtimeSinceStartup;

        public void Play()
        {
            if (_store == null || IsPlaying)
                return;

            _playbackVisualTracker?.Clear();
            _transportController.Play();
            OnTransportChanged?.Invoke();
        }

        public void Stop()
        {
            if (!IsPlaying)
                return;

            _transportController.Stop();
            _playbackVisualTracker?.Clear();
            OnTransportChanged?.Invoke();
        }

        public void TogglePlayback()
        {
            if (IsPlaying)
                Stop();
            else
                Play();
        }

        private void Update()
        {
            var transport = CurrentTransport;
            if (!transport.playing || _store == null)
                return;

            double currentTime = GetDspTime();
            float stepDur = SequencerClock.StepDuration(_store.State.tempo);

            while (transport.nextNoteTime < currentTime + LookaheadSeconds)
            {
                ScheduleCurrentStep(transport.nextNoteTime);
                if (_transportController.AdvanceTransport())
                    OnTransportChanged?.Invoke();
                transport.nextNoteTime += stepDur;
            }

            _playbackVisualTracker?.Prune(GetVisualTimeSeconds(), currentTime, stepDur);
        }

        private void ScheduleCurrentStep(double time)
        {
            string sceneId = GetPlaybackSceneId();
            _playbackVisualTracker?.RecordScheduledTransportStep(CurrentTransport, time, sceneId);

            var instances = _store.GetSceneInstances(sceneId);
            int currentStep = CurrentTransport.mode == "arrangement"
                ? CurrentTransport.slotStep
                : CurrentTransport.sceneStep;
            float stepDur = SequencerClock.StepDuration(_store.State.tempo);

            foreach (var instance in instances)
            {
                if (instance.muted)
                    continue;

                var pattern = _store.GetPattern(instance.patternId);
                if (pattern == null || pattern.derivedSequence == null)
                    continue;

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

        private void ScheduleRhythm(
            PatternDefinition pattern,
            PatternInstance instance,
            int localStep,
            double scheduledTime,
            SoundProfile sound,
            InstrumentPreset preset,
            InstrumentGroup group)
        {
            if (pattern.derivedSequence.events == null)
                return;

            foreach (var evt in pattern.derivedSequence.events)
            {
                if (evt.step != localStep)
                    continue;

                _audioDispatcher?.PlayDrum(
                    preset,
                    evt.lane,
                    evt.velocity,
                    instance.pan,
                    instance.brightness,
                    instance.depth,
                    preset.fxSend + group.busFx.reverb * 0.2f,
                    sound);

                _playbackVisualTracker?.RecordTrigger(
                    instance.id,
                    scheduledTime,
                    GetRhythmVisualDuration(evt.lane, sound),
                    GetDspTime(),
                    GetVisualTimeSeconds());
            }
        }

        private void ScheduleMelody(
            PatternDefinition pattern,
            PatternInstance instance,
            int localStep,
            float stepDur,
            double scheduledTime,
            SoundProfile sound,
            InstrumentPreset preset,
            InstrumentGroup group)
        {
            if (pattern.derivedSequence.notes == null)
                return;

            foreach (var note in pattern.derivedSequence.notes)
            {
                if (note.step != localStep)
                    continue;

                float duration = note.durationSteps * stepDur;

                _audioDispatcher?.PlayMelody(
                    preset,
                    note.midi,
                    note.velocity,
                    duration,
                    instance.pan,
                    instance.brightness,
                    instance.depth,
                    preset.fxSend + group.busFx.delay * 0.1f,
                    sound,
                    note.glide);

                _playbackVisualTracker?.RecordTrigger(
                    instance.id,
                    scheduledTime,
                    GetMelodyVisualDuration(duration, sound),
                    GetDspTime(),
                    GetVisualTimeSeconds());
            }
        }

        private void ScheduleHarmony(
            PatternDefinition pattern,
            PatternInstance instance,
            int localStep,
            float stepDur,
            double scheduledTime,
            SoundProfile sound,
            InstrumentPreset preset,
            InstrumentGroup group,
            int totalSteps)
        {
            if (localStep != 0 || pattern.derivedSequence.chord == null)
                return;

            int effectiveSteps = totalSteps;
            if (CurrentTransport.mode == "arrangement" && CurrentTransport.slotIndex >= 0)
            {
                var slot = _store.State.arrangement[CurrentTransport.slotIndex];
                if (slot != null)
                    effectiveSteps = Mathf.Min(totalSteps, slot.bars * AppStateFactory.BarSteps);
            }

            float duration = effectiveSteps * stepDur * 0.96f;

                _audioDispatcher?.PlayChord(
                    preset,
                    pattern.derivedSequence.chord,
                    0.38f,
                duration,
                instance.pan,
                instance.brightness,
                instance.depth,
                preset.fxSend + group.busFx.reverb * 0.18f,
                sound);

            _playbackVisualTracker?.RecordTrigger(
                instance.id,
                scheduledTime,
                GetHarmonyVisualDuration(duration, sound),
                GetDspTime(),
                GetVisualTimeSeconds());
        }

        public float GetPulse(string instanceId)
        {
            return _playbackVisualTracker != null
                ? _playbackVisualTracker.GetPulse(instanceId, GetVisualTimeSeconds())
                : 0f;
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
            if (_playbackVisualTracker == null)
            {
                state = PatternPlaybackVisualState.CreateInactive(pattern?.type ?? PatternType.RhythmLoop, null, playbackSceneId);
                return false;
            }

            return _playbackVisualTracker.TryGetPlaybackVisualState(
                pattern,
                instanceId,
                _store,
                CurrentTransport,
                playbackSceneId,
                GetDspTime(),
                GetVisualTimeSeconds(),
                _store != null ? SequencerClock.StepDuration(_store.State.tempo) : SequencerClock.StepDuration(120f),
                out state);
        }

        public bool HasArrangement()
        {
            return _arrangementNavigator != null && _arrangementNavigator.HasArrangement();
        }

        public string GetPlaybackSceneId()
        {
            return _transportController != null
                ? _transportController.GetPlaybackSceneId()
                : _store?.State?.activeSceneId;
        }

        private void HandlePlaybackSceneChanged(string previousSceneId, string currentSceneId)
        {
            if (!string.IsNullOrEmpty(previousSceneId))
                _playbackVisualTracker?.Clear(previousSceneId, _store);
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
