using System;
using System.Collections.Generic;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;
using RhythmForge.Sequencer;

namespace RhythmForge.Core.Events
{
    public sealed class RhythmForgeEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
                return;

            Type type = typeof(T);
            if (!_handlers.TryGetValue(type, out var handlers))
            {
                handlers = new List<Delegate>();
                _handlers[type] = handlers;
            }

            if (!handlers.Contains(handler))
                handlers.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
                return;

            Type type = typeof(T);
            if (!_handlers.TryGetValue(type, out var handlers))
                return;

            handlers.Remove(handler);
            if (handlers.Count == 0)
                _handlers.Remove(type);
        }

        public void Publish<T>(T evt) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var handlers) || handlers.Count == 0)
                return;

            var snapshot = handlers.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] is Action<T> typedHandler)
                    typedHandler(evt);
            }
        }
    }

    public readonly struct SessionStateChangedEvent
    {
        public SessionStateChangedEvent(SessionStore store)
        {
            Store = store;
        }

        public SessionStore Store { get; }
    }

    public readonly struct DraftCreatedEvent
    {
        public DraftCreatedEvent(DraftResult draft)
        {
            Draft = draft;
        }

        public DraftResult Draft { get; }
    }

    public readonly struct DraftCommittedEvent
    {
        public DraftCommittedEvent(DraftResult draft, PatternInstance instance, bool duplicate)
        {
            Draft = draft;
            Instance = instance;
            Duplicate = duplicate;
        }

        public DraftResult Draft { get; }
        public PatternInstance Instance { get; }
        public bool Duplicate { get; }
    }

    public readonly struct DraftDiscardedEvent
    {
    }

    public readonly struct StrokeStartedEvent
    {
    }

    public readonly struct DrawModeChangedEvent
    {
        public DrawModeChangedEvent(PatternType mode)
        {
            Mode = mode;
        }

        public PatternType Mode { get; }
    }

    public readonly struct PhaseChangedEvent
    {
        public PhaseChangedEvent(CompositionPhase phase)
        {
            Phase = phase;
        }

        public CompositionPhase Phase { get; }
    }

    public readonly struct TransportChangedEvent
    {
        public TransportChangedEvent(Transport transport, string playbackSceneId, bool isPlaying)
        {
            Transport = transport;
            PlaybackSceneId = playbackSceneId;
            IsPlaying = isPlaying;
        }

        public Transport Transport { get; }
        public string PlaybackSceneId { get; }
        public bool IsPlaying { get; }
    }

    public readonly struct PlaybackSceneChangedEvent
    {
        public PlaybackSceneChangedEvent(string previousSceneId, string currentSceneId)
        {
            PreviousSceneId = previousSceneId;
            CurrentSceneId = currentSceneId;
        }

        public string PreviousSceneId { get; }
        public string CurrentSceneId { get; }
    }

    public readonly struct ParameterLabelsVisibilityChangedEvent
    {
        public ParameterLabelsVisibilityChangedEvent(bool visible)
        {
            Visible = visible;
        }

        public bool Visible { get; }
    }

    public readonly struct GenreChangedEvent
    {
        public GenreChangedEvent(string previousGenreId, string newGenreId)
        {
            PreviousGenreId = previousGenreId;
            NewGenreId = newGenreId;
        }

        public string PreviousGenreId { get; }
        public string NewGenreId { get; }
    }

    public readonly struct ChordProgressionChangedEvent
    {
        public ChordProgressionChangedEvent(ChordProgression progression)
        {
            Progression = progression;
        }

        public ChordProgression Progression { get; }
    }

    public readonly struct MelodyCommittedEvent
    {
        public MelodyCommittedEvent(string patternId)
        {
            PatternId = patternId;
        }

        public string PatternId { get; }
    }

    public readonly struct GrooveCommittedEvent
    {
        public GrooveCommittedEvent(string patternId)
        {
            PatternId = patternId;
        }

        public string PatternId { get; }
    }

    [Flags]
    public enum PhaseInvalidationKind
    {
        None = 0,
        AsyncRederive = 1 << 0,
        ScheduleDirty = 1 << 1
    }

    public readonly struct PhaseInvalidationChangedEvent
    {
        public PhaseInvalidationChangedEvent(CompositionPhase phase, PhaseInvalidationKind kind)
        {
            Phase = phase;
            Kind = kind;
        }

        public CompositionPhase Phase { get; }
        public PhaseInvalidationKind Kind { get; }
        public bool IsPending => Kind != PhaseInvalidationKind.None;
    }
}
