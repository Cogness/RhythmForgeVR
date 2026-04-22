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
            state.version = 8;

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
            if (state.harmonicContext == null)
                state.harmonicContext = new HarmonicContext();
            if (state.composition == null)
                state.composition = GuidedDefaults.Create();
            if (state.composition.progression == null)
                state.composition.progression = GuidedDefaults.CreateDefaultProgression();
            if (state.composition.phasePatternIds == null)
                state.composition.phasePatternIds = new List<CompositionPhasePatternRef>();
            if (loadedVersion < 8)
                state.guidedMode = true;

            if (string.IsNullOrEmpty(state.activeGroupId))
                state.activeGroupId = "lofi";
            // v4→v5: migrate activeGroupId to activeGenreId
            if (string.IsNullOrEmpty(state.activeGenreId))
                state.activeGenreId = "electronic";
            if (string.IsNullOrEmpty(state.drawMode))
                state.drawMode = PatternType.Percussion.ToString();
            if (string.IsNullOrEmpty(state.activeSceneId))
                state.activeSceneId = "scene-a";

            state.drawMode = NormalizeDrawMode(state.drawMode);
            if (!Enum.TryParse(state.drawMode, true, out PatternType mode))
                mode = PatternType.Percussion;
            state.drawMode = PatternTypeCompatibility.Canonicalize(mode).ToString();

            NormalizeHarmonyPayloads(state);
            NormalizePatternShapeData(state, loadedVersion);
            NormalizePatternOrientations(state, loadedVersion);
            CleanupSceneMembership(state);
        }

        private static string NormalizeDrawMode(string drawMode)
        {
            if (string.IsNullOrWhiteSpace(drawMode))
                return PatternType.Percussion.ToString();

            switch (drawMode.Trim())
            {
                case "RhythmLoop":
                case "Rhythm":
                    return PatternType.Percussion.ToString();
                case "MelodyLine":
                    return PatternType.Melody.ToString();
                case "HarmonyPad":
                    return PatternType.Harmony.ToString();
                default:
                    return drawMode;
            }
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

        private void NormalizeHarmonyPayloads(AppState state)
        {
            if (state?.patterns == null)
                return;

            var composition = state.composition;
            bool progressionHasChords = composition?.progression?.chords != null && composition.progression.chords.Count > 0;

            for (int i = 0; i < state.patterns.Count; i++)
            {
                var pattern = state.patterns[i];
                if (pattern == null || !PatternTypeCompatibility.IsHarmony(pattern.type))
                    continue;

                if (pattern.derivedSequence == null)
                    pattern.derivedSequence = new DerivedSequence();

                int bars = ResolveHarmonyBars(pattern, composition);
                pattern.derivedSequence.kind = string.IsNullOrEmpty(pattern.derivedSequence.kind)
                    ? "harmony"
                    : pattern.derivedSequence.kind;

                if (pattern.derivedSequence.totalSteps <= 0)
                    pattern.derivedSequence.totalSteps = bars * AppStateFactory.BarSteps;

                if (pattern.derivedSequence.chordEvents == null || pattern.derivedSequence.chordEvents.Count == 0)
                {
                    pattern.derivedSequence.chordEvents = BuildHarmonyFallbackSlots(state, bars);
                }

                if (!progressionHasChords &&
                    composition != null &&
                    pattern.derivedSequence.chordEvents != null &&
                    pattern.derivedSequence.chordEvents.Count > 0)
                {
                    composition.progression = new ChordProgression
                    {
                        bars = bars,
                        chords = CloneChordSlots(pattern.derivedSequence.chordEvents, bars)
                    };
                    progressionHasChords = true;
                }
            }

            if ((state.harmonicContext == null || !state.harmonicContext.HasChord) &&
                composition?.progression?.chords != null &&
                composition.progression.chords.Count > 0)
            {
                state.harmonicContext = composition.progression.ToHarmonicContext(0);
            }
        }

        private static int ResolveHarmonyBars(PatternDefinition pattern, Composition composition)
        {
            if (pattern != null && pattern.bars > 0)
                return pattern.bars;
            if (composition != null && composition.bars > 0)
                return composition.bars;
            if (composition?.progression != null && composition.progression.bars > 0)
                return composition.progression.bars;
            return GuidedDefaults.Bars;
        }

        private static List<ChordSlot> BuildHarmonyFallbackSlots(AppState state, int bars)
        {
            var progression = state?.composition?.progression;
            if (progression?.chords != null && progression.chords.Count > 0)
                return CloneChordSlots(progression.chords, bars);

            var harmonicContext = state?.harmonicContext ?? new HarmonicContext();
            var slots = new List<ChordSlot>(bars);
            for (int barIndex = 0; barIndex < bars; barIndex++)
            {
                slots.Add(new ChordSlot
                {
                    barIndex = barIndex,
                    rootMidi = harmonicContext.rootMidi,
                    flavor = string.IsNullOrEmpty(harmonicContext.flavor) ? "major" : harmonicContext.flavor,
                    voicing = harmonicContext.chordTones != null
                        ? new List<int>(harmonicContext.chordTones)
                        : new List<int>()
                });
            }

            return slots;
        }

        private static List<ChordSlot> CloneChordSlots(List<ChordSlot> source, int bars)
        {
            var slots = new List<ChordSlot>(bars);
            if (source == null || source.Count == 0)
                return slots;

            for (int barIndex = 0; barIndex < bars; barIndex++)
            {
                var slot = source[barIndex % source.Count];
                if (slot == null)
                    continue;

                var clone = slot.Clone();
                clone.barIndex = barIndex;
                slots.Add(clone);
            }

            return slots;
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
