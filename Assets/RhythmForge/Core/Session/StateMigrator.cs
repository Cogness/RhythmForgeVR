using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;

namespace RhythmForge.Core.Session
{
    public class StateMigrator
    {
        public void NormalizeState(AppState state)
        {
            var fallback = AppStateFactory.CreateEmpty();
            int loadedVersion = state.version;
            state.version = 10;
            // v5→v6: PatternDefinition gains an optional shapeProfile3D field.
            // v6→v7: PatternDefinition gains an optional musicalShape field.
            // v7→v8: PatternDefinition gains an optional 3D worldPoints list
            //        (Phase G). Legacy v7 patterns leave it null; visualizer
            //        falls back to the flat 2D `points` list and audio stays
            //        bit-identical because derivers only read curve.projected.
            // v8→v9: every pattern is normalized into a MusicalShape and every
            // instance gets scene-scoped ensemble/progression ownership.
            // v9→v10: instance mix is spatial-native; pan/gain collapse into
            // brightness + reverbSend + delaySend + gainTrim.

            if (state.scenes == null || state.scenes.Count != 4)
                state.scenes = fallback.scenes;

            if (state.arrangement == null || state.arrangement.Count != AppStateFactory.MaxArrangementSlots)
                state.arrangement = fallback.arrangement;

            if (state.patterns == null)
                state.patterns = new List<PatternDefinition>();
            if (state.instances == null)
                state.instances = new List<PatternInstance>();
            if (state.counters == null)
                state.counters = new DraftCounters();

            if (string.IsNullOrEmpty(state.activeGroupId))
                state.activeGroupId = "lofi";
            // v4→v5: migrate activeGroupId to activeGenreId
            if (string.IsNullOrEmpty(state.activeGenreId))
                state.activeGenreId = "electronic";
            if (string.IsNullOrEmpty(state.drawMode))
                state.drawMode = PatternType.RhythmLoop.ToString();
            if (string.IsNullOrEmpty(state.drawShapeMode))
                state.drawShapeMode = nameof(ShapeFacetMode.Free);
            if (string.IsNullOrEmpty(state.activeSceneId))
                state.activeSceneId = "scene-a";

            if (!Enum.TryParse(state.drawMode, true, out PatternType mode))
                mode = PatternType.RhythmLoop;
            state.drawMode = mode.ToString();
            if (!Enum.TryParse(state.drawShapeMode, true, out ShapeFacetMode shapeMode))
                shapeMode = ShapeFacetMode.Free;
            state.drawShapeMode = shapeMode.ToString();

            NormalizePatternShapeData(state, loadedVersion);
            NormalizePatternOrientations(state, loadedVersion);
            NormalizeInstanceMixes(state, loadedVersion);
            CleanupSceneMembership(state);
            NormalizeMusicalShapes(state);
            ReindexSceneEnsembles(state);
        }

        private void NormalizePatternShapeData(AppState state, int loadedVersion)
        {
            foreach (var pattern in state.patterns)
            {
                if (pattern == null)
                    continue;

                // v4→v5: backfill genreId from legacy groupId
                if (string.IsNullOrEmpty(pattern.genreId))
                    pattern.genreId = "electronic";

                if (pattern.shapeProfile == null)
                    continue;

                bool missingSize = pattern.shapeProfile.worldMaxDimension <= 0.0001f ||
                    pattern.shapeProfile.worldAverageSize <= 0.0001f;
                bool backfilledSize = false;

                if (loadedVersion < 4 || missingSize)
                {
                    ShapeProfileSizing.BackfillLegacyWorldMetrics(pattern.shapeProfile);
                    backfilledSize = true;
                }

                if (loadedVersion < 4 || backfilledSize || pattern.soundProfile == null || string.IsNullOrEmpty(pattern.shapeSummary))
                    RefreshPatternDescriptors(pattern);
            }
        }

        private void NormalizePatternOrientations(AppState state, int loadedVersion)
        {
            foreach (var pattern in state.patterns)
            {
                if (pattern == null)
                    continue;

                bool hasValidRotation = pattern.hasRenderRotation && IsValidRotation(pattern.renderRotation);
                if (loadedVersion < 3 || !hasValidRotation)
                {
                    pattern.renderRotation = Quaternion.identity;
                    pattern.hasRenderRotation = false;
                    continue;
                }

                pattern.renderRotation = NormalizeQuaternion(pattern.renderRotation);
            }
        }

        private void NormalizeInstanceMixes(AppState state, int loadedVersion)
        {
            foreach (var instance in state.instances)
            {
                if (instance == null)
                    continue;

                if (string.IsNullOrEmpty(instance.sceneId))
                    instance.sceneId = state.activeSceneId;

                if (loadedVersion < 10)
                {
#pragma warning disable CS0618
                    instance.pan = Mathf.Clamp(instance.position.x * 2f - 1f, -1f, 1f);
                    instance.gain = Mathf.Clamp01(1.05f - instance.depth * 0.15f);
#pragma warning restore CS0618
                }

                instance.RecalculateMixFromPosition();
            }
        }

        private static bool IsValidRotation(Quaternion rotation)
        {
            if (float.IsNaN(rotation.x) || float.IsNaN(rotation.y) ||
                float.IsNaN(rotation.z) || float.IsNaN(rotation.w))
                return false;

            return rotation.x * rotation.x +
                   rotation.y * rotation.y +
                   rotation.z * rotation.z +
                   rotation.w * rotation.w > 0.0001f;
        }

        private static Quaternion NormalizeQuaternion(Quaternion rotation)
        {
            float magnitude = Mathf.Sqrt(
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w);

            if (magnitude <= 0.0001f)
                return Quaternion.identity;

            return new Quaternion(
                rotation.x / magnitude,
                rotation.y / magnitude,
                rotation.z / magnitude,
                rotation.w / magnitude);
        }

        private void RefreshPatternDescriptors(PatternDefinition pattern)
        {
            if (pattern?.shapeProfile == null)
                return;

            pattern.soundProfile = SoundProfileMapper.Derive(pattern.type, pattern.shapeProfile);
            pattern.shapeSummary = PresetBiasResolver.SummarizeShapeDNA(pattern.type, pattern.shapeProfile, pattern.soundProfile);
            pattern.details = DraftBuilder.ComposeDetails(pattern.details, pattern.shapeSummary);
            pattern.summary = ComposeSummary(pattern.summary, pattern.type, pattern.shapeProfile);
        }

        private static string ComposeSummary(string summary, PatternType type, ShapeProfile shapeProfile)
        {
            string sizeWord = ShapeProfileSizing.DescribeSize(type, shapeProfile);
            if (string.IsNullOrWhiteSpace(summary))
                return $"{sizeWord} {type}";

            string normalized = summary.Trim();
            if (normalized.StartsWith("compact ", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("medium ", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("expanded ", StringComparison.OrdinalIgnoreCase))
                return normalized;

            if (normalized.Length == 1)
                return $"{sizeWord} {normalized.ToLowerInvariant()}";

            return $"{sizeWord} {char.ToLowerInvariant(normalized[0])}{normalized.Substring(1)}";
        }

        private void CleanupSceneMembership(AppState state)
        {
            var instanceIds = new HashSet<string>();
            foreach (var instance in state.instances)
                instanceIds.Add(instance.id);

            foreach (var scene in state.scenes)
                scene.instanceIds.RemoveAll(id => !instanceIds.Contains(id));

            foreach (var instance in state.instances)
            {
                var scene = GetScene(state, instance.sceneId);
                if (scene != null && !scene.instanceIds.Contains(instance.id))
                    scene.instanceIds.Add(instance.id);
            }
        }

        private void NormalizeMusicalShapes(AppState state)
        {
            foreach (var pattern in state.patterns)
            {
                if (pattern == null)
                    continue;

                pattern.musicalShape = NormalizeMusicalShape(pattern);
                if (pattern.musicalShape != null)
                {
                    pattern.derivedSequence = ToDominantSequence(pattern.type, pattern.musicalShape);
                    pattern.bars = pattern.musicalShape.bars;
                    pattern.color = TypeColors.Blend(pattern.musicalShape.bondStrength);
                }
            }
        }

        private static MusicalShape NormalizeMusicalShape(PatternDefinition pattern)
        {
            int totalSteps = ResolveTotalSteps(pattern);
            int bars = Mathf.Max(1, pattern?.musicalShape?.bars ?? pattern?.bars ?? 0);
            if (totalSteps <= 0)
                totalSteps = bars * AppStateFactory.BarSteps;
            bars = Mathf.Max(1, Mathf.CeilToInt(totalSteps / (float)AppStateFactory.BarSteps));

            var shape = pattern.musicalShape ?? new MusicalShape();
            shape.id = string.IsNullOrEmpty(shape.id) ? $"{pattern.id}-shape" : shape.id;
            shape.soundProfile = shape.soundProfile ?? pattern.soundProfile?.Clone();
            shape.profile3D = shape.profile3D ?? pattern.shapeProfile3D?.Clone();
            shape.bondStrength = ResolveBondStrength(pattern, shape.bondStrength);
            shape.facetMode = InferFacetMode(shape.facetMode, shape.bondStrength, pattern.type);
            shape.totalSteps = totalSteps;
            shape.bars = bars;
            shape.keyName = string.IsNullOrEmpty(shape.keyName) ? pattern.key : shape.keyName;
            shape.facets = shape.facets ?? new DerivedShapeSequence();

            shape.facets.rhythm = NormalizeRhythmFacet(pattern, shape.facets.rhythm, totalSteps);
            shape.facets.melody = NormalizeMelodyFacet(pattern, shape.facets.melody, totalSteps);
            shape.facets.harmony = NormalizeHarmonyFacet(pattern, shape.facets.harmony, totalSteps);

            if (shape.rhythmSoundProfile == null && pattern.type == PatternType.RhythmLoop)
                shape.rhythmSoundProfile = pattern.soundProfile?.Clone();
            if (shape.melodySoundProfile == null && pattern.type == PatternType.MelodyLine)
                shape.melodySoundProfile = pattern.soundProfile?.Clone();
            if (shape.harmonySoundProfile == null && pattern.type == PatternType.HarmonyPad)
                shape.harmonySoundProfile = pattern.soundProfile?.Clone();

            return shape;
        }

        private static RhythmSequence NormalizeRhythmFacet(PatternDefinition pattern, RhythmSequence rhythm, int totalSteps)
        {
            rhythm = rhythm ?? new RhythmSequence();
            if ((rhythm.events == null || rhythm.events.Count == 0) &&
                pattern.type == PatternType.RhythmLoop &&
                pattern.derivedSequence?.events != null)
            {
                rhythm.events = new List<RhythmEvent>(pattern.derivedSequence.events.Count);
                foreach (var evt in pattern.derivedSequence.events)
                {
                    rhythm.events.Add(new RhythmEvent
                    {
                        step = evt.step,
                        lane = evt.lane,
                        velocity = evt.velocity,
                        microShift = evt.microShift
                    });
                }
                rhythm.swing = pattern.derivedSequence.swing;
            }

            rhythm.totalSteps = totalSteps;
            if (rhythm.events == null)
                rhythm.events = new List<RhythmEvent>();
            return rhythm;
        }

        private static MelodySequence NormalizeMelodyFacet(PatternDefinition pattern, MelodySequence melody, int totalSteps)
        {
            melody = melody ?? new MelodySequence();
            if ((melody.notes == null || melody.notes.Count == 0) &&
                pattern.type == PatternType.MelodyLine &&
                pattern.derivedSequence?.notes != null)
            {
                melody.notes = new List<MelodyNote>(pattern.derivedSequence.notes.Count);
                foreach (var note in pattern.derivedSequence.notes)
                {
                    melody.notes.Add(new MelodyNote
                    {
                        step = note.step,
                        midi = note.midi,
                        durationSteps = note.durationSteps,
                        velocity = note.velocity,
                        glide = note.glide
                    });
                }
            }

            melody.totalSteps = totalSteps;
            if (melody.notes == null)
                melody.notes = new List<MelodyNote>();
            return melody;
        }

        private static HarmonySequence NormalizeHarmonyFacet(PatternDefinition pattern, HarmonySequence harmony, int totalSteps)
        {
            harmony = harmony ?? new HarmonySequence();
            if ((harmony.events == null || harmony.events.Count == 0) &&
                pattern.type == PatternType.HarmonyPad)
            {
                var chord = harmony.chord;
                if ((chord == null || chord.Count == 0) && pattern.derivedSequence?.chord != null)
                    chord = new List<int>(pattern.derivedSequence.chord);

                int rootMidi = harmony.rootMidi != 0 ? harmony.rootMidi : pattern.derivedSequence?.rootMidi ?? 0;
                string keyName = !string.IsNullOrEmpty(pattern?.musicalShape?.keyName)
                    ? pattern.musicalShape.keyName
                    : !string.IsNullOrEmpty(pattern?.key) ? pattern.key : "A minor";
                string flavor = !string.IsNullOrEmpty(harmony.flavor) ? harmony.flavor : pattern.derivedSequence?.flavor;

                if ((chord == null || chord.Count == 0) && rootMidi == 0)
                {
                    var key = MusicalKeys.Get(keyName);
                    rootMidi = key.rootMidi;
                    chord = MusicalKeys.BuildScaleChord(rootMidi, keyName, new[] { 0, 2, 4 });
                    if (string.IsNullOrEmpty(flavor))
                        flavor = keyName.ToLowerInvariant().Contains("major") ? "major" : "minor";
                }

                if (chord != null && chord.Count > 0)
                {
                    harmony.events = new List<HarmonyEvent>
                    {
                        new HarmonyEvent
                        {
                            step = 0,
                            durationSteps = totalSteps,
                            rootMidi = rootMidi,
                            chord = chord,
                            flavor = flavor ?? "minor"
                        }
                    };
                }
            }

            harmony.totalSteps = totalSteps;
            if (harmony.events == null)
                harmony.events = new List<HarmonyEvent>();

            if (harmony.events.Count > 0)
            {
                var primary = harmony.events[0];
                harmony.rootMidi = primary.rootMidi;
                harmony.flavor = primary.flavor ?? "minor";
                harmony.chord = primary.chord != null ? new List<int>(primary.chord) : new List<int>();
            }
            else
            {
                harmony.flavor = string.IsNullOrEmpty(harmony.flavor) ? pattern.derivedSequence?.flavor ?? "minor" : harmony.flavor;
                harmony.chord = harmony.chord ?? new List<int>();
            }

            return harmony;
        }

        private static int ResolveTotalSteps(PatternDefinition pattern)
        {
            if (pattern?.musicalShape?.totalSteps > 0)
                return pattern.musicalShape.totalSteps;

            int facetSteps = 0;
            if (pattern?.musicalShape?.facets?.rhythm?.totalSteps > 0)
                facetSteps = Mathf.Max(facetSteps, pattern.musicalShape.facets.rhythm.totalSteps);
            if (pattern?.musicalShape?.facets?.melody?.totalSteps > 0)
                facetSteps = Mathf.Max(facetSteps, pattern.musicalShape.facets.melody.totalSteps);
            if (pattern?.musicalShape?.facets?.harmony?.totalSteps > 0)
                facetSteps = Mathf.Max(facetSteps, pattern.musicalShape.facets.harmony.totalSteps);
            if (facetSteps > 0)
                return facetSteps;

            if (pattern?.derivedSequence?.totalSteps > 0)
                return pattern.derivedSequence.totalSteps;

            return Mathf.Max(1, pattern?.bars ?? 1) * AppStateFactory.BarSteps;
        }

        private static Vector3 ResolveBondStrength(PatternDefinition pattern, Vector3 current)
        {
            if (current.x > 0f || current.y > 0f || current.z > 0f)
                return current;

            switch (pattern.type)
            {
                case PatternType.RhythmLoop: return new Vector3(1f, 0f, 0f);
                case PatternType.MelodyLine: return new Vector3(0f, 1f, 0f);
                default:                     return new Vector3(0f, 0f, 1f);
            }
        }

        private static ShapeFacetMode InferFacetMode(ShapeFacetMode current, Vector3 bondStrength, PatternType dominantType)
        {
            if (bondStrength.x > 0.001f && bondStrength.y <= 0.001f && bondStrength.z <= 0.001f)
                return ShapeFacetMode.SoloRhythm;
            if (bondStrength.y > 0.001f && bondStrength.x <= 0.001f && bondStrength.z <= 0.001f)
                return ShapeFacetMode.SoloMelody;
            if (bondStrength.z > 0.001f && bondStrength.x <= 0.001f && bondStrength.y <= 0.001f)
                return ShapeFacetMode.SoloHarmony;

            return current == ShapeFacetMode.Free ? ShapeFacetMode.Free : current;
        }

        private static DerivedSequence ToDominantSequence(PatternType type, MusicalShape shape)
        {
            if (shape?.facets == null)
                return null;

            switch (type)
            {
                case PatternType.RhythmLoop:
                    return new DerivedSequence
                    {
                        kind = "rhythm",
                        totalSteps = shape.totalSteps,
                        swing = shape.facets.rhythm?.swing ?? 0f,
                        events = shape.facets.rhythm?.events ?? new List<RhythmEvent>()
                    };
                case PatternType.MelodyLine:
                    return new DerivedSequence
                    {
                        kind = "melody",
                        totalSteps = shape.totalSteps,
                        notes = shape.facets.melody?.notes ?? new List<MelodyNote>()
                    };
                default:
                    var harmony = shape.facets.harmony;
                    return new DerivedSequence
                    {
                        kind = "harmony",
                        totalSteps = shape.totalSteps,
                        flavor = harmony?.flavor,
                        rootMidi = harmony?.rootMidi ?? 0,
                        chord = harmony?.chord ?? new List<int>()
                    };
            }
        }

        private void ReindexSceneEnsembles(AppState state)
        {
            foreach (var scene in state.scenes)
            {
                if (scene == null)
                    continue;

                for (int i = 0; i < scene.instanceIds.Count; i++)
                {
                    var instance = FindInstance(state.instances, scene.instanceIds[i]);
                    if (instance == null)
                        continue;

                    instance.ensembleRoleIndex = i;
                    instance.progressionBarIndex = i % 8;
                }
            }
        }

        private static PatternInstance FindInstance(List<PatternInstance> instances, string instanceId)
        {
            foreach (var instance in instances)
            {
                if (instance != null && instance.id == instanceId)
                    return instance;
            }

            return null;
        }

        private static SceneData GetScene(AppState state, string sceneId)
        {
            foreach (var scene in state.scenes)
            {
                if (scene.id == sceneId)
                    return scene;
            }

            return null;
        }
    }
}
