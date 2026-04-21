using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.PatternBehavior;
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
        private SamplePlayer _samplePlayer;
        private ArrangementNavigator _arrangementNavigator;
        private TransportController _transportController;
        private PlaybackVisualTracker _playbackVisualTracker;
        private RhythmForgeEventBus _eventBus;
        private readonly Transport _fallbackTransport = new Transport();
        private int _lastWarmBar = -1;
        private readonly List<ResolvedVoiceSpec> _warmSpecScratch = new List<ResolvedVoiceSpec>();

        private const float LookaheadSeconds = 0.12f;

        public bool IsPlaying => _transportController != null && _transportController.IsPlaying;
        public Transport CurrentTransport => _transportController?.CurrentTransport ?? _fallbackTransport;

        public event Action OnTransportChanged;

        public void Initialize(SessionStore store)
        {
            if (_transportController != null)
                _transportController.OnPlaybackSceneChanged -= HandlePlaybackSceneChanged;

            _store = store;
            _eventBus = store != null ? store.EventBus : null;
            _arrangementNavigator = new ArrangementNavigator(store);
            _transportController = new TransportController(store, _arrangementNavigator, GetDspTime);
            _transportController.OnPlaybackSceneChanged += HandlePlaybackSceneChanged;
            _playbackVisualTracker = new PlaybackVisualTracker();
            _lastWarmBar = -1;

            if (_audioEngine == null)
                _audioEngine = GetComponent<AudioEngine>();
            _audioDispatcher = _audioEngine;
        }

        public void SetSamplePlayer(SamplePlayer samplePlayer)
        {
            _samplePlayer = samplePlayer;
        }

        public void ResetWarmBar()
        {
            _lastWarmBar = -1;
        }

        protected virtual double GetDspTime() => AudioSettings.dspTime;
        protected virtual double GetVisualTimeSeconds() => Time.realtimeSinceStartup;

        public void Play()
        {
            if (_store == null || IsPlaying)
                return;

            _playbackVisualTracker?.Clear();
            _transportController.Play();
            NotifyTransportChanged();
        }

        public void Stop()
        {
            if (!IsPlaying)
                return;

            _transportController.Stop();
            _playbackVisualTracker?.Clear();
            NotifyTransportChanged();
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
                    NotifyTransportChanged();
                transport.nextNoteTime += stepDur;
            }

            TryWarmNextBar(transport);
            _playbackVisualTracker?.Prune(GetVisualTimeSeconds(), currentTime, stepDur);
        }

        private void TryWarmNextBar(Transport transport)
        {
            if (_samplePlayer == null || _store == null)
                return;

            int currentBar = transport.absoluteBar;
            if (currentBar == _lastWarmBar)
                return;

            _lastWarmBar = currentBar;
            string sceneId = GetPlaybackSceneId();
            var instances = _store.GetSceneInstances(sceneId);
            float stepDur = SequencerClock.StepDuration(_store.State.tempo);

            _warmSpecScratch.Clear();
            foreach (var instance in instances)
            {
                if (instance.muted) continue;
                var pattern = _store.GetPattern(instance.patternId);
                if (pattern == null || pattern.derivedSequence == null) continue;

                var sound  = _store.GetEffectiveSoundProfile(instance, pattern);
                var preset = _store.GetPreset(_store.GetEffectivePresetId(instance, pattern));

                int totalSteps = pattern.derivedSequence.totalSteps > 0
                    ? pattern.derivedSequence.totalSteps
                    : AppStateFactory.BarSteps;

                PatternBehaviorRegistry.Get(pattern.type).CollectVoiceSpecs(
                    new PatternSchedulingContext
                    {
                        pattern       = pattern,
                        instance      = instance,
                        stepDuration  = stepDur,
                        sound         = sound,
                        preset        = preset,
                        appState      = _store.State,
                        audioDispatcher = _audioDispatcher
                    },
                    totalSteps,
                    _warmSpecScratch);
            }

            if (_warmSpecScratch.Count > 0)
                _samplePlayer.WarmClips(_warmSpecScratch);
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

                PatternBehaviorRegistry.Get(pattern.type).Schedule(new PatternSchedulingContext
                {
                    pattern = pattern,
                    instance = instance,
                    localStep = localStep,
                    stepDuration = stepDur,
                    scheduledTime = time,
                    sound = effectiveSound,
                    preset = preset,
                    group = group,
                    transport = CurrentTransport,
                    appState = _store.State,
                    audioDispatcher = _audioDispatcher,
                    recordTrigger = RecordTrigger
                });
            }
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
                state = PatternPlaybackVisualState.CreateInactive(pattern?.type ?? PatternType.Percussion, null, playbackSceneId);
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

            _eventBus?.Publish(new PlaybackSceneChangedEvent(previousSceneId, currentSceneId));
        }

        private void RecordTrigger(string instanceId, double scheduledTime, float activeDuration)
        {
            _playbackVisualTracker?.RecordTrigger(
                instanceId,
                scheduledTime,
                activeDuration,
                GetDspTime(),
                GetVisualTimeSeconds());
        }

        private void NotifyTransportChanged()
        {
            OnTransportChanged?.Invoke();
            _eventBus?.Publish(new TransportChangedEvent(CurrentTransport, GetPlaybackSceneId(), IsPlaying));
        }
    }
}
