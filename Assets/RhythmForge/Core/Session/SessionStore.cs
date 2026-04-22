using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.PatternBehavior;
using RhythmForge.Core.Sequencing;

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

        // Main-thread dispatch queue: background tasks post completions here; Tick() drains it.
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
        private readonly Dictionary<CompositionPhase, PhaseInvalidationKind> _phaseInvalidations = new Dictionary<CompositionPhase, PhaseInvalidationKind>();

        /// <summary>Fired on the main thread after a background genre re-derivation completes.</summary>
        public event Action<string> OnGenreRederived;

        public event Action OnStateChanged;

        public SessionStore(RhythmForgeEventBus eventBus = null)
        {
            EventBus = eventBus ?? new RhythmForgeEventBus();
            State = AppStateFactory.CreateEmpty();
            Patterns = new PatternRepository(() => State, GetScene, NotifyStateChanged, ReserveName);
            Scenes = new SceneController(() => State, GetScene, GetInstance, NotifyStateChanged);
            SoundResolver = new SoundProfileResolver(GetPreset);
            _stateMigrator = new StateMigrator();
            EventBus.Subscribe<ChordProgressionChangedEvent>(HandleChordProgressionChanged);
        }

        public void LoadState(AppState state)
        {
            State = state ?? AppStateFactory.CreateEmpty();
            _stateMigrator.NormalizeState(State);
            _phaseInvalidations.Clear();
            // Sync registry to the loaded genre
            GenreRegistry.SetActive(State.activeGenreId ?? "electronic");
            NotifyStateChanged();
        }

        public void Reset()
        {
            State = AppStateFactory.CreateEmpty();
            _phaseInvalidations.Clear();
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
                return PatternTypeCompatibility.Canonicalize(mode);

            return PatternType.Percussion;
        }

        public void SetDrawMode(PatternType mode)
        {
            string serialized = PatternTypeCompatibility.Canonicalize(mode).ToString();
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

        public PatternInstance CommitDraft(DraftResult draft, bool duplicate)
        {
            var instance = Patterns.CommitDraft(draft, duplicate);
            PatternType canonicalType = draft != null
                ? PatternTypeCompatibility.Canonicalize(draft.type)
                : PatternType.Percussion;

            if (draft != null &&
                draft.success &&
                canonicalType == PatternType.Harmony &&
                draft.derivedSequence?.chordEvents != null &&
                draft.derivedSequence.chordEvents.Count > 0)
            {
                var progression = new ChordProgression
                {
                    bars = draft.bars > 0 ? draft.bars : GuidedDefaults.Bars,
                    chords = CloneChordSlots(draft.derivedSequence.chordEvents)
                };

                ClearPhaseInvalidation(CompositionPhase.Harmony, PhaseInvalidationKind.AsyncRederive | PhaseInvalidationKind.ScheduleDirty, notifyState: false);
                if (State.guidedMode)
                    UpdateProgression(progression);
                else
                {
                    State.harmonicContext = progression.ToHarmonicContext(0);
                    NotifyStateChanged();
                }
            }

            if (draft != null && draft.success && canonicalType == PatternType.Melody)
            {
                ClearPhaseInvalidation(CompositionPhase.Melody, PhaseInvalidationKind.AsyncRederive | PhaseInvalidationKind.ScheduleDirty, notifyState: true);
                EventBus.Publish(new MelodyCommittedEvent(instance?.patternId));
            }

            if (draft != null && draft.success && canonicalType == PatternType.Groove)
            {
                ClearPhaseInvalidation(CompositionPhase.Groove, PhaseInvalidationKind.AsyncRederive | PhaseInvalidationKind.ScheduleDirty, notifyState: false);
                UpdateGroove(draft.derivedSequence?.grooveProfile);
                EventBus.Publish(new GrooveCommittedEvent(instance?.patternId));
            }

            if (draft != null && draft.success && canonicalType == PatternType.Bass)
                ClearPhaseInvalidation(CompositionPhase.Bass, PhaseInvalidationKind.AsyncRederive | PhaseInvalidationKind.ScheduleDirty, notifyState: true);

            if (draft != null && draft.success && canonicalType == PatternType.Percussion)
                ClearPhaseInvalidation(CompositionPhase.Percussion, PhaseInvalidationKind.AsyncRederive | PhaseInvalidationKind.ScheduleDirty, notifyState: true);

            return instance;
        }

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

        public string GetActiveGenreId() => State.activeGenreId ?? "electronic";

        public HarmonicContext GetHarmonicContext() => State.harmonicContext ?? (State.harmonicContext = new HarmonicContext());

        public void SetHarmonicContext(int rootMidi, List<int> chordTones, string flavor)
        {
            var ctx = State.harmonicContext ?? (State.harmonicContext = new HarmonicContext());
            ctx.rootMidi   = rootMidi;
            ctx.chordTones = chordTones ?? new List<int>();
            ctx.flavor     = flavor ?? "minor";
        }

        /// <summary>
        /// Drains the main-thread dispatch queue. Call from a MonoBehaviour.Update().
        /// </summary>
        public void Tick()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
                action();
        }

        /// <summary>
        /// Switches the active genre and re-derives all existing patterns on a background thread.
        /// GenreChangedEvent fires immediately; patterns update asynchronously. Listen to
        /// OnGenreRederived to know when re-derivation and cache invalidation are complete.
        /// </summary>
        public void SetGenre(string genreId)
        {
            string previousId = State.activeGenreId ?? "electronic";
            if (previousId == genreId)
                return;

            GenreRegistry.SetActive(genreId);
            State.activeGenreId = genreId;

            // In guided mode: re-seed the composition key, tempo and chord progression
            // from the incoming genre's policy so derivers immediately work in the new key.
            if (State.guidedMode)
            {
                var policy = GuidedPolicy.Get(genreId);
                var composition = GetComposition();
                composition.key    = policy.keyName;
                composition.tempo  = policy.tempo;
                composition.progression = policy.CreateDefaultProgression();
                State.key   = policy.keyName;
                State.tempo = policy.tempo;
                State.harmonicContext = composition.progression.ToHarmonicContext(0);
                EventBus.Publish(new ChordProgressionChangedEvent(composition.progression.Clone()));
            }

            // Fire the UI-visible event immediately so buttons highlight without waiting.
            EventBus.Publish(new GenreChangedEvent(previousId, genreId));

            // Take a snapshot of pattern data safe to read off main thread.
            var snapshot = BuildRederivationSnapshot(genreId);
            var queue    = _mainThreadQueue;

            var progression = State.guidedMode
                ? PatternContextScope.CloneProgression(State.composition?.progression)
                : null;

            Task.Run(() =>
            {
                var results = RederivePatternsBackground(snapshot, genreId, progression);
                queue.Enqueue(() => ApplyRederivationResults(results, genreId));
            });
        }

        public Composition GetComposition()
        {
            if (State.composition == null)
                State.composition = GuidedDefaults.Create();
            if (State.composition.progression == null)
                State.composition.progression = GuidedDefaults.CreateDefaultProgression();
            if (State.composition.phasePatternIds == null)
                State.composition.phasePatternIds = new List<CompositionPhasePatternRef>();

            return State.composition;
        }

        public void SetComposition(Composition composition)
        {
            var next = composition?.Clone() ?? GuidedDefaults.Create();
            if (next.progression == null)
                next.progression = GuidedDefaults.CreateDefaultProgression();
            if (next.phasePatternIds == null)
                next.phasePatternIds = new List<CompositionPhasePatternRef>();

            State.composition = next;
            State.guidedMode = true;
            State.tempo = next.tempo;
            State.key = string.IsNullOrEmpty(next.key) ? GuidedDefaults.Key : next.key;
            State.harmonicContext = GetHarmonicContextForBar(0);
            _phaseInvalidations.Clear();
            NotifyStateChanged();
        }

        public CompositionPhase GetCurrentPhase()
        {
            return GetComposition().currentPhase;
        }

        public bool IsPhasePending(CompositionPhase phase)
        {
            return GetPhaseInvalidation(phase) != PhaseInvalidationKind.None;
        }

        public PhaseInvalidationKind GetPhaseInvalidation(CompositionPhase phase)
        {
            return _phaseInvalidations.TryGetValue(phase, out var kind)
                ? kind
                : PhaseInvalidationKind.None;
        }

        public bool HasCommittedPhase(CompositionPhase phase)
        {
            var composition = GetComposition();
            if (phase == CompositionPhase.Groove)
                return composition.groove != null;

            string patternId = composition.GetPatternId(phase);
            return !string.IsNullOrEmpty(patternId) && GetPattern(patternId) != null;
        }

        public void SetCurrentPhase(CompositionPhase phase)
        {
            var composition = GetComposition();
            bool guidedModeChanged = !State.guidedMode;
            if (!guidedModeChanged && composition.currentPhase == phase)
                return;

            State.guidedMode = true;
            composition.currentPhase = phase;
            NotifyStateChanged();
        }

        public void ClearPhase(CompositionPhase phase)
        {
            var composition = GetComposition();
            string patternId = composition.GetPatternId(phase);

            Patterns.RemovePatternAndInstances(patternId, notify: false);
            composition.RemovePatternId(phase);
            ClearPhaseInvalidation(phase, PhaseInvalidationKind.AsyncRederive | PhaseInvalidationKind.ScheduleDirty, notifyState: false);

            switch (phase)
            {
                case CompositionPhase.Harmony:
                    UpdateProgression(GuidedDefaults.CreateDefaultProgression());
                    return;
                case CompositionPhase.Groove:
                    composition.groove = null;
                    MarkPhaseInvalidations(ResolveScheduleDirtyDependentPhases(), PhaseInvalidationKind.ScheduleDirty, notifyState: false);
                    NotifyStateChanged();
                    EventBus.Publish(new GrooveCommittedEvent(null));
                    return;
                case CompositionPhase.Melody:
                    NotifyStateChanged();
                    EventBus.Publish(new MelodyCommittedEvent(null));
                    return;
                default:
                    NotifyStateChanged();
                    return;
            }
        }

        public void UpdateProgression(ChordProgression progression)
        {
            var composition = GetComposition();
            composition.progression = progression?.Clone() ?? GuidedDefaults.CreateDefaultProgression();
            State.harmonicContext = GetHarmonicContextForBar(0);
            NotifyStateChanged();
            EventBus.Publish(new ChordProgressionChangedEvent(composition.progression.Clone()));
        }

        public void UpdateGroove(GrooveProfile groove)
        {
            var composition = GetComposition();
            composition.groove = groove?.Clone();
            MarkPhaseInvalidations(ResolveScheduleDirtyDependentPhases(), PhaseInvalidationKind.ScheduleDirty, notifyState: false);
            NotifyStateChanged();
        }

        public HarmonicContext GetHarmonicContextForBar(int barIndex)
        {
            var progression = GetComposition().progression;
            if (progression == null)
                return GetHarmonicContext().Clone();

            return progression.ToHarmonicContext(barIndex);
        }

        private struct PatternSnapshot
        {
            public string patternId;
            public PatternType type;
            public ShapeProfile shapeProfile;
            public List<Vector2> points;
            public string key;
            public string shapeSummary;
        }

        private struct PatternRederivation
        {
            public string patternId;
            public string genreId;
            public string presetId;
            public SoundProfile soundProfile;
            public DerivedSequence derivedSequence;
            public int bars;
            public List<string> tags;
            public string summary;
            public string details;
            public Color color;
        }

        private List<PatternSnapshot> BuildRederivationSnapshot(string genreId)
        {
            var snapshots = new List<PatternSnapshot>(State.patterns.Count);
            foreach (var pattern in State.patterns)
            {
                if (pattern?.shapeProfile == null || pattern.points == null || pattern.points.Count < 3)
                    continue;
                snapshots.Add(new PatternSnapshot
                {
                    patternId    = pattern.id,
                    type         = pattern.type,
                    shapeProfile = pattern.shapeProfile,
                    points       = new List<Vector2>(pattern.points),
                    key          = pattern.key ?? State.key,
                    shapeSummary = pattern.shapeSummary
                });
            }
            return snapshots;
        }

        private void HandleChordProgressionChanged(ChordProgressionChangedEvent evt)
        {
            if (evt.Progression == null)
                return;

            var snapshot = BuildDependentProgressionRederivationSnapshot();
            if (snapshot.Count == 0)
                return;

            MarkPhaseInvalidations(snapshot, PhaseInvalidationKind.AsyncRederive, notifyState: true);
            var queue = _mainThreadQueue;
            string genreId = GetActiveGenreId();

            Task.Run(() =>
            {
                var results = RederivePatternsBackground(snapshot, genreId, evt.Progression.Clone());
                queue.Enqueue(() => ApplyRederivationResults(results, genreId));
            });
        }

        private List<PatternSnapshot> BuildDependentProgressionRederivationSnapshot()
        {
            var snapshots = new List<PatternSnapshot>();
            foreach (var pattern in State.patterns)
            {
                if (pattern?.shapeProfile == null || pattern.points == null || pattern.points.Count < 3)
                    continue;

                PatternType canonicalType = PatternTypeCompatibility.Canonicalize(pattern.type);
                if (canonicalType != PatternType.Melody && canonicalType != PatternType.Bass)
                    continue;

                snapshots.Add(new PatternSnapshot
                {
                    patternId = pattern.id,
                    type = pattern.type,
                    shapeProfile = pattern.shapeProfile,
                    points = new List<Vector2>(pattern.points),
                    key = pattern.key ?? State.key,
                    shapeSummary = pattern.shapeSummary
                });
            }

            return snapshots;
        }

        private static List<ChordSlot> CloneChordSlots(List<ChordSlot> chords)
        {
            var result = new List<ChordSlot>();
            if (chords == null)
                return result;

            for (int i = 0; i < chords.Count; i++)
            {
                if (chords[i] != null)
                    result.Add(chords[i].Clone());
            }

            return result;
        }

        private static List<PatternRederivation> RederivePatternsBackground(
            List<PatternSnapshot> snapshots,
            string genreId,
            ChordProgression progression)
        {
            var genre   = GenreRegistry.Get(genreId);
            var results = new List<PatternRederivation>(snapshots.Count);
            var harmonicContext = progression?.ToHarmonicContext(0) ?? new HarmonicContext();

            foreach (var snap in snapshots)
            {
                var metrics      = StrokeAnalyzer.Analyze(snap.points);
                var soundProfile = genre.GetSoundMapping(snap.type).Evaluate(snap.type, snap.shapeProfile);

                PatternDerivationResult derivation;
                using (PatternContextScope.Push(
                    harmonicContext,
                    progression))
                {
                    derivation = DerivePatternForGenre(
                        genre,
                        snap.type,
                        snap.points,
                        metrics,
                        snap.key,
                        snap.shapeProfile,
                        soundProfile);
                }

                if (PatternTypeCompatibility.IsHarmony(snap.type) &&
                    derivation.derivedSequence?.chordEvents != null &&
                    derivation.derivedSequence.chordEvents.Count > 0)
                {
                    progression = new ChordProgression
                    {
                        bars = derivation.bars > 0 ? derivation.bars : GuidedDefaults.Bars,
                        chords = CloneChordSlots(derivation.derivedSequence.chordEvents)
                    };
                    harmonicContext = progression.ToHarmonicContext(0);
                }

                results.Add(new PatternRederivation
                {
                    patternId       = snap.patternId,
                    genreId         = genre.Id,
                    presetId        = derivation.presetId,
                    soundProfile    = soundProfile,
                    derivedSequence = derivation.derivedSequence,
                    bars            = derivation.bars,
                    tags            = derivation.tags,
                    summary         = derivation.summary,
                    details         = DraftBuilder.ComposeDetails(derivation.details, snap.shapeSummary),
                    color           = genre.ColorPalette.Get(snap.type)
                });
            }
            return results;
        }

        private static PatternDerivationResult DerivePatternForGenre(
            GenreProfile genre,
            PatternType type,
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile)
        {
            switch (PatternTypeCompatibility.Canonicalize(type))
            {
                case PatternType.Percussion:
                    var rhythm = genre.RhythmDeriver.Derive(points, metrics, shapeProfile, soundProfile, genre);
                    return new PatternDerivationResult
                    {
                        bars = rhythm.bars,
                        presetId = rhythm.presetId,
                        tags = rhythm.tags,
                        derivedSequence = rhythm.derivedSequence,
                        summary = rhythm.summary,
                        details = rhythm.details
                    };
                case PatternType.Melody:
                    var melody = genre.MelodyDeriver.Derive(points, metrics, keyName, shapeProfile, soundProfile, genre);
                    return new PatternDerivationResult
                    {
                        bars = melody.bars,
                        presetId = melody.presetId,
                        tags = melody.tags,
                        derivedSequence = melody.derivedSequence,
                        summary = melody.summary,
                        details = melody.details
                    };
                case PatternType.Bass:
                    var bass = BassDeriver.Derive(points, metrics, keyName, genre.Id, shapeProfile, soundProfile);
                    return new PatternDerivationResult
                    {
                        bars = bass.bars,
                        presetId = bass.presetId,
                        tags = bass.tags,
                        derivedSequence = bass.derivedSequence,
                        summary = bass.summary,
                        details = bass.details
                    };
                case PatternType.Groove:
                    var groove = GrooveShapeMapper.Map(shapeProfile);
                    return new PatternDerivationResult
                    {
                        bars = GuidedDefaults.Bars,
                        presetId = genre.GetDefaultPresetId(PatternType.Groove),
                        tags = new List<string>
                        {
                            groove.density < 0.85f ? "sparse" : groove.density > 1.15f ? "busy" : "balanced",
                            groove.syncopation > 0.22f ? "syncopated" : "steady"
                        },
                        derivedSequence = new DerivedSequence
                        {
                            kind = "groove",
                            totalSteps = GuidedDefaults.Bars * AppStateFactory.BarSteps,
                            grooveProfile = groove
                        },
                        summary = $"Groove profile with {(groove.quantizeGrid >= 16 ? "16th" : "8th")} grid and swing {Mathf.RoundToInt(groove.swing * 100f)}%.",
                        details = "Groove remaps melody timing, density, and accents at schedule time while leaving pitch derivation untouched."
                    };
                default:
                    var harmony = genre.HarmonyDeriver.Derive(points, metrics, keyName, shapeProfile, soundProfile, genre);
                    return new PatternDerivationResult
                    {
                        bars = harmony.bars,
                        presetId = harmony.presetId,
                        tags = harmony.tags,
                        derivedSequence = harmony.derivedSequence,
                        summary = harmony.summary,
                        details = harmony.details
                    };
            }
        }

        private void ApplyRederivationResults(List<PatternRederivation> results, string genreId)
        {
            var completedPhases = new List<CompositionPhase>();
            foreach (var r in results)
            {
                var pattern = GetPattern(r.patternId);
                if (pattern == null) continue;

                CompositionPhase phase = pattern.type.ToCompositionPhase();
                if (!completedPhases.Contains(phase))
                    completedPhases.Add(phase);

                pattern.genreId         = r.genreId;
                pattern.presetId        = r.presetId;
                pattern.soundProfile    = r.soundProfile;
                pattern.derivedSequence = r.derivedSequence;
                pattern.bars            = r.bars;
                pattern.tags            = r.tags;
                pattern.summary         = r.summary;
                pattern.details         = r.details;
                pattern.color           = r.color;

                if (PatternTypeCompatibility.Canonicalize(pattern.type) == PatternType.Groove &&
                    State.composition != null &&
                    State.composition.GetPatternId(CompositionPhase.Groove) == r.patternId)
                {
                    State.composition.groove = r.derivedSequence?.grooveProfile?.Clone();
                }
            }

            ClearPhaseInvalidations(completedPhases, PhaseInvalidationKind.AsyncRederive, notifyState: false);
            NotifyStateChanged();
            OnGenreRederived?.Invoke(genreId);
        }

        private List<CompositionPhase> ResolveScheduleDirtyDependentPhases()
        {
            var phases = new List<CompositionPhase>();
            if (HasCommittedPhase(CompositionPhase.Melody))
                phases.Add(CompositionPhase.Melody);
            if (HasCommittedPhase(CompositionPhase.Percussion))
                phases.Add(CompositionPhase.Percussion);
            return phases;
        }

        private void MarkPhaseInvalidations(List<PatternSnapshot> snapshots, PhaseInvalidationKind kind, bool notifyState)
        {
            var phases = new List<CompositionPhase>();
            for (int i = 0; i < snapshots.Count; i++)
            {
                CompositionPhase phase = snapshots[i].type.ToCompositionPhase();
                if (!phases.Contains(phase))
                    phases.Add(phase);
            }

            MarkPhaseInvalidations(phases, kind, notifyState);
        }

        private void MarkPhaseInvalidations(List<CompositionPhase> phases, PhaseInvalidationKind kind, bool notifyState)
        {
            bool changed = false;
            for (int i = 0; i < phases.Count; i++)
            {
                var phase = phases[i];
                var previous = GetPhaseInvalidation(phase);
                var next = previous | kind;
                if (previous == next)
                    continue;

                _phaseInvalidations[phase] = next;
                EventBus.Publish(new PhaseInvalidationChangedEvent(phase, next));
                changed = true;
            }

            if (changed && notifyState)
                NotifyStateChanged();
        }

        private void ClearPhaseInvalidations(List<CompositionPhase> phases, PhaseInvalidationKind kind, bool notifyState)
        {
            bool changed = false;
            for (int i = 0; i < phases.Count; i++)
            {
                if (ClearPhaseInvalidationInternal(phases[i], kind))
                    changed = true;
            }

            if (changed && notifyState)
                NotifyStateChanged();
        }

        private void ClearPhaseInvalidation(CompositionPhase phase, PhaseInvalidationKind kind, bool notifyState)
        {
            bool changed = ClearPhaseInvalidationInternal(phase, kind);
            if (changed && notifyState)
                NotifyStateChanged();
        }

        private bool ClearPhaseInvalidationInternal(CompositionPhase phase, PhaseInvalidationKind kind)
        {
            var previous = GetPhaseInvalidation(phase);
            var next = previous & ~kind;
            if (previous == next)
                return false;

            if (next == PhaseInvalidationKind.None)
                _phaseInvalidations.Remove(phase);
            else
                _phaseInvalidations[phase] = next;

            EventBus.Publish(new PhaseInvalidationChangedEvent(phase, next));
            return true;
        }

        private void NotifyStateChanged()
        {
            OnStateChanged?.Invoke();
            EventBus.Publish(new SessionStateChangedEvent(this));
        }
    }
}
