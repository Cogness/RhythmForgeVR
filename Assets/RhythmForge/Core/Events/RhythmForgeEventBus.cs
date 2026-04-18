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

    public readonly struct SpatialZoneChangedEvent
    {
        public SpatialZoneChangedEvent(string instanceId, string previousZoneId, string newZoneId)
        {
            InstanceId     = instanceId;
            PreviousZoneId = previousZoneId;
            NewZoneId      = newZoneId;
        }

        public string InstanceId     { get; }
        public string PreviousZoneId { get; }
        public string NewZoneId      { get; }
    }

    public readonly struct ConductorGestureEvent
    {
        public ConductorGestureEvent(
            RhythmForge.Interaction.ConductorGesture gesture,
            string focusedZoneId,
            bool   allZones,
            float  swayPeriodSeconds)
        {
            Gesture            = gesture;
            FocusedZoneId      = focusedZoneId;
            AllZones           = allZones;
            SwayPeriodSeconds  = swayPeriodSeconds;
        }

        public RhythmForge.Interaction.ConductorGesture Gesture { get; }
        /// <summary>Zone that was in focus when the gesture fired, or null.</summary>
        public string FocusedZoneId { get; }
        /// <summary>True when left grip was held — gesture applies to all zones.</summary>
        public bool AllZones { get; }
        /// <summary>Estimated sway period in seconds (only meaningful for Sway gesture).</summary>
        public float SwayPeriodSeconds { get; }
    }
}
