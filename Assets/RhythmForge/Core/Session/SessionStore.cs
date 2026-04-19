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

        private readonly Dictionary<string, HarmonicFabric> _sceneFabrics = new Dictionary<string, HarmonicFabric>();

        private readonly StateMigrator _stateMigrator;
        private const int DefaultSceneProgressionBars = 8;

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
            InitializeHarmonicFabrics();
        }

        public void SetSpawnPlacementResolver(Func<PatternType, Vector3, Vector3?> resolver)
        {
            Patterns.SetSpawnPlacementResolver(resolver);
        }

        public void LoadState(AppState state)
        {
            State = state ?? AppStateFactory.CreateEmpty();
            _stateMigrator.NormalizeState(State);
            // Sync registry to the loaded genre
            GenreRegistry.SetActive(State.activeGenreId ?? "electronic");
            InitializeHarmonicFabrics();
            NotifyStateChanged();
        }

        public void Reset()
        {
            State = AppStateFactory.CreateEmpty();
            InitializeHarmonicFabrics();
            NotifyStateChanged();
        }

        private void InitializeHarmonicFabrics()
        {
            _sceneFabrics.Clear();
            if (State?.scenes != null)
            {
                foreach (var scene in State.scenes)
                {
                    if (scene == null || string.IsNullOrEmpty(scene.id))
                        continue;

                    _sceneFabrics[scene.id] = new HarmonicFabric
                    {
                        key = ResolveSceneKey(scene)
                    };
                }
            }

            RebuildAllSceneFabrics();
            UpdateActiveSceneHarmonicView();
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
                return mode;

            return PatternType.RhythmLoop;
        }

        public ShapeFacetMode GetDrawShapeMode()
        {
            if (Enum.TryParse(State.drawShapeMode, true, out ShapeFacetMode mode))
                return mode;

            return ShapeFacetMode.Free;
        }

        public void SetDrawMode(PatternType mode)
        {
            string serialized = mode.ToString();
            if (State.drawMode == serialized)
                return;

            State.drawMode = serialized;
            NotifyStateChanged();
        }

        public void SetDrawShapeMode(ShapeFacetMode mode)
        {
            string serialized = mode.ToString();
            if (State.drawShapeMode == serialized)
                return;

            State.drawShapeMode = serialized;
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
            ReindexSceneEnsemble(State.activeSceneId);
            return instance;
        }

        public PatternInstance SpawnPattern(string patternId, string sceneId = null, Vector3? coords = null, bool notify = true)
        {
            var instance = Patterns.SpawnPattern(patternId, sceneId, coords, notify);
            ReindexSceneEnsemble(sceneId ?? State.activeSceneId);
            return instance;
        }

        public void ClonePattern(string patternId) => Patterns.ClonePattern(patternId);

        public void RemoveInstance(string instanceId)
        {
            var instance = GetInstance(instanceId);
            string sceneId = instance?.sceneId;
            Patterns.RemoveInstance(instanceId);
            if (!string.IsNullOrEmpty(sceneId))
                ReindexSceneEnsemble(sceneId);
        }

        public void DuplicateInstance(string instanceId)
        {
            var instance = GetInstance(instanceId);
            string sceneId = instance?.sceneId;
            Patterns.DuplicateInstance(instanceId);
            if (!string.IsNullOrEmpty(sceneId))
                ReindexSceneEnsemble(sceneId);
        }

        public void UpdateInstance(string instanceId, Vector3? position = null, float? depth = null, bool? muted = null)
            => Patterns.UpdateInstance(instanceId, position, depth, muted);

        public void SetPresetOverride(string instanceId, string presetId) => Patterns.SetPresetOverride(instanceId, presetId);

        public void SetActiveScene(string sceneId, bool queueIfPlaying = false)
        {
            Scenes.SetActiveScene(sceneId, queueIfPlaying);
            UpdateActiveSceneHarmonicView();
        }

        public void QueueScene(string sceneId) => Scenes.QueueScene(sceneId);

        public void CopyScene(string sourceId, string targetId)
        {
            Scenes.CopyScene(sourceId, targetId);
            ReindexSceneEnsemble(targetId);
            UpdateActiveSceneHarmonicView();
        }

        public void UpdateArrangement(string slotId, string sceneId = null, int? bars = null)
            => Scenes.UpdateArrangement(slotId, sceneId, bars);

        public void ClearArrangementScene(string slotId) => Scenes.ClearArrangementScene(slotId);

        public string GetActiveGenreId() => State.activeGenreId ?? "electronic";

        public HarmonicContext GetHarmonicContext()
        {
            var fabric = GetHarmonicFabric(State?.activeSceneId);
            var placement = fabric?.ChordAtBar(0);
            if (placement != null && placement.tones != null && placement.tones.Count > 0)
            {
                var ctx = State.harmonicContext ?? (State.harmonicContext = new HarmonicContext());
                ctx.rootMidi = placement.rootMidi;
                ctx.chordTones = new List<int>(placement.tones);
                ctx.flavor = placement.flavor ?? "minor";
                return ctx;
            }

            return State.harmonicContext ?? (State.harmonicContext = new HarmonicContext());
        }

        public void SetHarmonicContext(int rootMidi, List<int> chordTones, string flavor)
        {
            var ctx = State.harmonicContext ?? (State.harmonicContext = new HarmonicContext());
            ctx.rootMidi   = rootMidi;
            ctx.chordTones = chordTones ?? new List<int>();
            ctx.flavor     = flavor ?? "minor";
        }

        public HarmonicFabric GetHarmonicFabric(string sceneId = null)
        {
            sceneId = sceneId ?? State?.activeSceneId;
            if (string.IsNullOrEmpty(sceneId))
                sceneId = "scene-a";

            if (!_sceneFabrics.TryGetValue(sceneId, out var fabric))
            {
                fabric = new HarmonicFabric { key = ResolveSceneKey(GetScene(sceneId)) };
                _sceneFabrics[sceneId] = fabric;
            }

            return fabric;
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

            var harmonicContext = PatternContextScope.CloneHarmonicContext(GetHarmonicContext());

            if (!Application.isPlaying)
            {
                var bg = RederivePatternsBackground(snapshot, genreId, harmonicContext);
                ApplyRederivationResults(bg.results, bg.finalContext, genreId);
                return;
            }

            Task.Run(() =>
            {
                var bg = RederivePatternsBackground(snapshot, genreId, harmonicContext);
                queue.Enqueue(() => ApplyRederivationResults(bg.results, bg.finalContext, genreId));
            });
        }

        private struct PatternSnapshot
        {
            public string patternId;
            public PatternType type;
            public ShapeProfile shapeProfile;
            public ShapeProfile3D shapeProfile3D;
            public List<Vector2> points;
            public string key;
            public string shapeSummary;
            public Vector3 bondStrength;
            public ShapeFacetMode facetMode;
            public int ensembleRoleIndex;
            public int ensembleRoleCount;
            public int progressionBarIndex;
            public int progressionBarCount;
            public int bars;
            public int totalSteps;
            public string sceneId;
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
            public MusicalShape musicalShape;
        }

        private List<PatternSnapshot> BuildRederivationSnapshot(string genreId)
        {
            var snapshots = new List<PatternSnapshot>(State.patterns.Count);
            var primaryInstances = BuildPrimaryInstanceLookup();
            var sceneProjectedCounts = new Dictionary<string, int>();
            var nextFallbackRoleIndex = new Dictionary<string, int>();

            if (State?.scenes != null)
            {
                foreach (var scene in State.scenes)
                {
                    if (scene == null || string.IsNullOrEmpty(scene.id))
                        continue;

                    int actualCount = scene.instanceIds?.Count ?? 0;
                    sceneProjectedCounts[scene.id] = actualCount;
                    nextFallbackRoleIndex[scene.id] = actualCount;
                }
            }

            foreach (var pattern in State.patterns)
            {
                if (pattern?.shapeProfile == null || pattern.points == null || pattern.points.Count < 3)
                    continue;

                primaryInstances.TryGetValue(pattern.id, out var primaryInstance);
                string sceneId = primaryInstance?.sceneId ?? State.activeSceneId;
                if (!sceneProjectedCounts.ContainsKey(sceneId))
                {
                    sceneProjectedCounts[sceneId] = 0;
                    nextFallbackRoleIndex[sceneId] = 0;
                }

                if (primaryInstance == null)
                    sceneProjectedCounts[sceneId]++;
            }

            foreach (var pattern in State.patterns)
            {
                if (pattern?.shapeProfile == null || pattern.points == null || pattern.points.Count < 3)
                    continue;

                primaryInstances.TryGetValue(pattern.id, out var primaryInstance);
                string sceneId = primaryInstance?.sceneId ?? State.activeSceneId;
                var scene = GetScene(sceneId);
                var shape = pattern.musicalShape;
                int fallbackRoleIndex = nextFallbackRoleIndex.TryGetValue(sceneId, out var nextRole)
                    ? nextRole
                    : 0;
                if (primaryInstance == null)
                    nextFallbackRoleIndex[sceneId] = fallbackRoleIndex + 1;
                int roleIndex = primaryInstance?.ensembleRoleIndex ?? fallbackRoleIndex;
                int roleCount = sceneProjectedCounts.TryGetValue(sceneId, out var projectedCount)
                    ? Mathf.Max(1, projectedCount)
                    : Mathf.Max(1, scene?.instanceIds?.Count ?? State.patterns.Count);

                snapshots.Add(new PatternSnapshot
                {
                    patternId    = pattern.id,
                    type         = pattern.type,
                    shapeProfile = pattern.shapeProfile,
                    shapeProfile3D = pattern.shapeProfile3D,
                    points       = new List<Vector2>(pattern.points),
                    key          = pattern.key ?? State.key,
                    shapeSummary = pattern.shapeSummary,
                    bondStrength = shape?.bondStrength ?? OneHotBondStrength(pattern.type),
                    facetMode = shape?.facetMode ?? ShapeFacetMode.Free,
                    ensembleRoleIndex = roleIndex,
                    ensembleRoleCount = roleCount,
                    progressionBarIndex = primaryInstance?.progressionBarIndex ?? (roleIndex % DefaultSceneProgressionBars),
                    progressionBarCount = DefaultSceneProgressionBars,
                    bars = Mathf.Max(1, shape?.bars ?? pattern.bars),
                    totalSteps = GetPatternLoopSteps(pattern),
                    sceneId = sceneId
                });
            }
            return snapshots;
        }

        private struct RederivationBatch
        {
            public List<PatternRederivation> results;
            public HarmonicContext finalContext;
        }

        private static RederivationBatch RederivePatternsBackground(
            List<PatternSnapshot> snapshots,
            string genreId,
            HarmonicContext harmonicContext)
        {
            var genre   = GenreRegistry.Get(genreId);
            var results = new List<PatternRederivation>(snapshots.Count);

            foreach (var snap in snapshots)
            {
                var metrics      = StrokeAnalyzer.Analyze(snap.points);
                var soundProfile = genre.GetSoundMapping(snap.type).Evaluate(snap.type, snap.shapeProfile);
                // Phase C: per-facet sound profiles for silent non-dominant facets.
                var rhythmSound  = snap.type == PatternType.RhythmLoop
                    ? soundProfile
                    : genre.GetSoundMapping(PatternType.RhythmLoop).Evaluate(PatternType.RhythmLoop, snap.shapeProfile);
                var melodySound  = snap.type == PatternType.MelodyLine
                    ? soundProfile
                    : genre.GetSoundMapping(PatternType.MelodyLine).Evaluate(PatternType.MelodyLine, snap.shapeProfile);
                var harmonySound = snap.type == PatternType.HarmonyPad
                    ? soundProfile
                    : genre.GetSoundMapping(PatternType.HarmonyPad).Evaluate(PatternType.HarmonyPad, snap.shapeProfile);

                // Phase G: rederivation wraps the saved 2D points in a StrokeCurve
                // so sub-derivers receive the same carrier as the fresh-draft path.
                // Legacy saves (no 3D samples persisted) produce bit-identical
                // audio to pre-Phase-G rederivation because curve.projected ==
                // snap.points and derivers only read curve.projected today.
                var curve = StrokeCurve.FromLegacy2D(snap.points);

                var request = new UnifiedDerivationRequest
                {
                    dominantType   = snap.type,
                    bondStrength   = snap.bondStrength,
                    facetMode      = snap.facetMode,
                    ensembleRoleIndex = snap.ensembleRoleIndex,
                    ensembleRoleCount = snap.ensembleRoleCount,
                    progressionBarIndex = snap.progressionBarIndex,
                    progressionBarCount = snap.progressionBarCount,
                    bars          = snap.bars,
                    totalSteps    = snap.totalSteps,
                    sceneId       = snap.sceneId,
                    barChordProvider = localBar =>
                    {
                        if (localBar != 0 || harmonicContext == null || harmonicContext.chordTones == null || harmonicContext.chordTones.Count == 0)
                            return null;

                        return new ChordPlacement
                        {
                            rootMidi = harmonicContext.rootMidi,
                            tones = new List<int>(harmonicContext.chordTones),
                            flavor = harmonicContext.flavor ?? "minor",
                            sourceShapeRole = snap.ensembleRoleIndex
                        };
                    },
                    freeMode      = snap.facetMode == ShapeFacetMode.Free,
                    curve          = curve,
                    metrics        = metrics,
                    keyName        = snap.key,
                    groupId        = null,
                    shapeProfile   = snap.shapeProfile,
                    shapeProfile3D = snap.shapeProfile3D,
                    soundProfile   = soundProfile,
                    rhythmSoundProfile  = rhythmSound,
                    melodySoundProfile  = melodySound,
                    harmonySoundProfile = harmonySound,
                    genre          = genre,
                    fabric         = null, // background thread: no fabric writes
                    appState       = null // no main-thread state access
                };

                var unified = genre.UnifiedDeriver.Derive(request);

                if (unified.newHarmonicContext != null)
                    harmonicContext = unified.newHarmonicContext;

                results.Add(new PatternRederivation
                {
                    patternId       = snap.patternId,
                    genreId         = genre.Id,
                    presetId        = unified.presetId,
                    soundProfile    = soundProfile,
                    derivedSequence = unified.dominantSequence,
                    bars            = unified.bars,
                    tags            = unified.tags,
                    summary         = unified.summary,
                    details         = DraftBuilder.ComposeDetails(unified.details, snap.shapeSummary),
                    color           = unified.shape != null
                        ? TypeColors.Blend(unified.shape.bondStrength)
                        : TypeColors.GetColor(snap.type),
                    musicalShape    = unified.shape
                });
            }
            return new RederivationBatch { results = results, finalContext = harmonicContext };
        }

        private static Vector3 OneHotBondStrength(PatternType type)
        {
            switch (type)
            {
                case PatternType.RhythmLoop: return new Vector3(1f, 0f, 0f);
                case PatternType.MelodyLine: return new Vector3(0f, 1f, 0f);
                default:                     return new Vector3(0f, 0f, 1f);
            }
        }

        private void ApplyRederivationResults(
            List<PatternRederivation> results,
            HarmonicContext finalContext,
            string genreId)
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
                pattern.musicalShape    = r.musicalShape;
            }

            RebuildAllSceneFabrics();
            if (finalContext != null && finalContext.chordTones != null && finalContext.chordTones.Count > 0)
                SetHarmonicContext(finalContext.rootMidi, finalContext.chordTones, finalContext.flavor);
            UpdateActiveSceneHarmonicView();

            NotifyStateChanged();
            OnGenreRederived?.Invoke(genreId);
        }

        public void ReindexSceneEnsemble(string sceneId)
        {
            var scene = GetScene(sceneId);
            if (scene == null)
                return;

            for (int i = 0; i < scene.instanceIds.Count; i++)
            {
                var instance = GetInstance(scene.instanceIds[i]);
                if (instance == null)
                    continue;

                instance.ensembleRoleIndex = i;
                instance.progressionBarIndex = i % DefaultSceneProgressionBars;

                var pattern = GetPattern(instance.patternId);
                if (pattern?.musicalShape != null)
                    pattern.musicalShape.roleIndex = i;
            }

            RebuildSceneFabric(sceneId);
            if (sceneId == State?.activeSceneId)
                UpdateActiveSceneHarmonicView();
        }

        private void RebuildAllSceneFabrics()
        {
            if (State?.scenes == null)
                return;

            foreach (var scene in State.scenes)
            {
                if (scene == null)
                    continue;

                RebuildSceneFabric(scene.id);
            }
        }

        private void RebuildSceneFabric(string sceneId)
        {
            var scene = GetScene(sceneId);
            if (scene == null)
                return;

            var fabric = GetHarmonicFabric(sceneId);
            fabric.Clear();
            fabric.key = ResolveSceneKey(scene);

            int maxBar = 0;
            foreach (var instance in GetSceneInstances(sceneId))
            {
                var pattern = GetPattern(instance.patternId);
                var harmony = pattern?.musicalShape?.facets?.harmony;
                if (harmony?.events == null || harmony.events.Count == 0)
                    continue;

                foreach (var evt in harmony.events)
                {
                    if (evt == null || evt.chord == null || evt.chord.Count == 0)
                        continue;

                    int localBar = evt.step / AppStateFactory.BarSteps;
                    int barIndex = (instance.progressionBarIndex + localBar) % DefaultSceneProgressionBars;
                    fabric.Write(
                        barIndex,
                        evt.rootMidi,
                        evt.chord,
                        evt.flavor ?? "minor",
                        instance.ensembleRoleIndex);
                    maxBar = Mathf.Max(maxBar, barIndex + 1);
                }
            }

            while (fabric.progression.Count < Mathf.Max(1, maxBar))
                fabric.progression.Add(null);
            fabric.nextFreeBar = Mathf.Clamp(maxBar, 0, DefaultSceneProgressionBars);
        }

        private void UpdateActiveSceneHarmonicView()
        {
            var fabric = GetHarmonicFabric(State?.activeSceneId);
            HarmonicContextProvider.SetFabricView(fabric);

            var placement = fabric?.ChordAtBar(0);
            if (placement != null && placement.tones != null && placement.tones.Count > 0)
                SetHarmonicContext(placement.rootMidi, placement.tones, placement.flavor);
        }

        private string ResolveSceneKey(SceneData scene)
        {
            if (scene != null && scene.hasKeyOverride && !string.IsNullOrEmpty(scene.keyOverride))
                return scene.keyOverride;

            return State?.key ?? "A minor";
        }

        private static int GetPatternLoopSteps(PatternDefinition pattern)
        {
            if (pattern?.musicalShape?.totalSteps > 0)
                return pattern.musicalShape.totalSteps;
            if (pattern?.derivedSequence?.totalSteps > 0)
                return pattern.derivedSequence.totalSteps;

            return Mathf.Max(1, pattern?.bars ?? 1) * AppStateFactory.BarSteps;
        }

        private Dictionary<string, PatternInstance> BuildPrimaryInstanceLookup()
        {
            var lookup = new Dictionary<string, PatternInstance>();
            if (State?.scenes == null)
                return lookup;

            foreach (var scene in State.scenes)
            {
                if (scene?.instanceIds == null)
                    continue;

                foreach (var instanceId in scene.instanceIds)
                {
                    var instance = GetInstance(instanceId);
                    if (instance == null || string.IsNullOrEmpty(instance.patternId))
                        continue;

                    if (!lookup.ContainsKey(instance.patternId))
                        lookup[instance.patternId] = instance;
                }
            }

            return lookup;
        }

        private void NotifyStateChanged()
        {
            OnStateChanged?.Invoke();
            EventBus.Publish(new SessionStateChangedEvent(this));
        }
    }
}
