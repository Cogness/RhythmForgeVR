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
        }

        public void LoadState(AppState state)
        {
            State = state ?? AppStateFactory.CreateEmpty();
            _stateMigrator.NormalizeState(State);
            // Sync registry to the loaded genre
            GenreRegistry.SetActive(State.activeGenreId ?? "electronic");
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

            // Fire the UI-visible event immediately so buttons highlight without waiting.
            EventBus.Publish(new GenreChangedEvent(previousId, genreId));

            // Take a snapshot of pattern data safe to read off main thread.
            var snapshot = BuildRederivationSnapshot(genreId);
            var queue    = _mainThreadQueue;

            var harmonicContext = PatternContextScope.CloneHarmonicContext(State.harmonicContext);

            Task.Run(() =>
            {
                var results = RederivePatternsBackground(snapshot, genreId, harmonicContext);
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
            NotifyStateChanged();
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

        private static List<PatternRederivation> RederivePatternsBackground(
            List<PatternSnapshot> snapshots,
            string genreId,
            HarmonicContext harmonicContext)
        {
            var genre   = GenreRegistry.Get(genreId);
            var results = new List<PatternRederivation>(snapshots.Count);
            var roleCounter = new Dictionary<PatternType, int>();
            var roleTotals = new Dictionary<PatternType, int>();

            foreach (var snap in snapshots)
            {
                if (!roleTotals.ContainsKey(snap.type))
                    roleTotals[snap.type] = 0;
                roleTotals[snap.type]++;
            }

            foreach (var snap in snapshots)
            {
                var metrics      = StrokeAnalyzer.Analyze(snap.points);
                var soundProfile = genre.GetSoundMapping(snap.type).Evaluate(snap.type, snap.shapeProfile);

                if (!roleCounter.TryGetValue(snap.type, out int roleIdx))
                    roleIdx = 0;
                roleCounter[snap.type] = roleIdx + 1;

                PatternDerivationResult derivation;
                using (PatternContextScope.Push(
                    new ShapeRole { index = roleIdx, count = roleTotals[snap.type] },
                    harmonicContext))
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

                if (PatternTypeCompatibility.IsHarmony(snap.type) && derivation.derivedSequence?.chord != null)
                {
                    harmonicContext = new HarmonicContext
                    {
                        rootMidi = derivation.derivedSequence.rootMidi,
                        chordTones = new List<int>(derivation.derivedSequence.chord),
                        flavor = derivation.derivedSequence.flavor ?? "minor"
                    };
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
                case PatternType.Bass:
                case PatternType.Groove:
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
            foreach (var r in results)
            {
                var pattern = GetPattern(r.patternId);
                if (pattern == null) continue;

                pattern.genreId         = r.genreId;
                pattern.presetId        = r.presetId;
                pattern.soundProfile    = r.soundProfile;
                pattern.derivedSequence = r.derivedSequence;
                pattern.bars            = r.bars;
                pattern.tags            = r.tags;
                pattern.summary         = r.summary;
                pattern.details         = r.details;
                pattern.color           = r.color;
            }

            NotifyStateChanged();
            OnGenreRederived?.Invoke(genreId);
        }

        private void NotifyStateChanged()
        {
            OnStateChanged?.Invoke();
            EventBus.Publish(new SessionStateChangedEvent(this));
        }
    }
}
