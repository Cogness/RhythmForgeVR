using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Core.Session
{
    /// <summary>
    /// Builds a demo AppState containing a SINGLE unified 3D MusicalShape that reproduces
    /// the exact combined audio of <see cref="DemoSession.CreateDemoState"/> (Beat + Melody + Pad).
    ///
    /// Strategy: run the three legacy drafts in the same order as <see cref="DemoSession"/> —
    /// committing each to state before the next so role resolution / harmonic context threads
    /// identically — then extract each draft's DOMINANT facet (rhythm draft ⇒ rhythm facet,
    /// melody draft ⇒ melody facet, harmony draft ⇒ harmony facet). Combine into one
    /// <see cref="MusicalShape"/> with <c>bondStrength = (1,1,1)</c> (unnormalized, so
    /// <see cref="BondStrengthVelocity"/> returns 1.0 pass-through for every facet).
    ///
    /// Result:
    ///   - Musical content (step grid, events, velocities, pitches, chord tones) is bit-identical
    ///     to the three-pattern demo.
    ///   - Single pattern + single instance routed through <see cref="MusicalShapeBehavior"/>
    ///     (selected by <see cref="PatternBehaviorRegistry"/> when <c>musicalShape != null</c>).
    ///   - All three facets share one <c>PatternInstance.pan/brightness/depth</c>, so spatial
    ///     positioning is averaged across the legacy spawn points (trade-off: audio mix is
    ///     mono-centered rather than spread across three positions in the scene).
    /// </summary>
    public static class UnifiedDemoSession
    {
        public static AppState CreateUnifiedDemoState(SessionStore store)
        {
            // Step 1: Build the three legacy drafts in the exact same order/conditions as
            // DemoSession so each deriver sees the same sceneShapeCount / harmonic context.
            var state = AppStateFactory.CreateEmpty();
            state.tempo = 85f;
            state.key = "A minor";
            state.activeGroupId = "lofi";
            state.activeGenreId = "electronic";
            state.activeSceneId = "scene-a";
            GenreRegistry.SetActive("electronic");

            var circlePoints = GenerateCircle(24, 0.14f);
            var rhythmDraft = DraftBuilder.BuildFromStroke(
                PatternType.RhythmLoop, circlePoints, new Vector3(0.35f, 0.34f, 0.3f),
                Quaternion.identity, state, store);
            if (rhythmDraft.success)
            {
                CommitDraftToState(state, rhythmDraft, "Beat-Demo");
            }

            var wavePoints = GenerateWave(32, 0.2f, 0.08f);
            var melodyDraft = DraftBuilder.BuildFromStroke(
                PatternType.MelodyLine, wavePoints, new Vector3(0.66f, 0.36f, 0.28f),
                Quaternion.identity, state, store);
            if (melodyDraft.success)
            {
                CommitDraftToState(state, melodyDraft, "Melody-Demo");
            }

            var padPoints = GeneratePadStroke(20, 0.18f, 0.06f);
            var harmonyDraft = DraftBuilder.BuildFromStroke(
                PatternType.HarmonyPad, padPoints, new Vector3(0.5f, 0.58f, 0.5f),
                Quaternion.identity, state, store);
            if (harmonyDraft.success)
            {
                CommitDraftToState(state, harmonyDraft, "Pad-Demo");
            }

            if (!rhythmDraft.success || !melodyDraft.success || !harmonyDraft.success)
            {
                Debug.LogWarning("[UnifiedDemoSession] One or more seed drafts failed; falling back to legacy 3-pattern demo.");
                return state;
            }

            // Step 2: Extract each draft's dominant facet.
            var rhythmFacet  = rhythmDraft.musicalShape?.facets?.rhythm;
            var melodyFacet  = melodyDraft.musicalShape?.facets?.melody;
            var harmonyFacet = harmonyDraft.musicalShape?.facets?.harmony;

            if (rhythmFacet == null || melodyFacet == null || harmonyFacet == null)
            {
                Debug.LogWarning("[UnifiedDemoSession] Missing facets on seed drafts; falling back.");
                return state;
            }

            // Step 3: Clear the three legacy patterns/instances — we want ONE unified shape.
            state.patterns.Clear();
            state.instances.Clear();
            foreach (var scene in state.scenes)
            {
                scene.instanceIds.Clear();
            }

            // Step 4: Assemble one MusicalShape with unit bondStrength (pass-through velocity).
            // Use the rhythm draft as the visual/stroke carrier (its points are the circle).
            var unifiedShape = new MusicalShape
            {
                id = MathUtils.CreateId("shape"),
                profile3D = rhythmDraft.shapeProfile3D?.Clone(),
                soundProfile = rhythmDraft.soundProfile?.Clone(),
                bondStrength = new Vector3(1f, 1f, 1f),   // NOT normalized → Scale* == 1.0
                roleIndex = 0,
                facets = new DerivedShapeSequence
                {
                    rhythm = rhythmFacet,
                    melody = melodyFacet,
                    harmony = harmonyFacet
                },
                keyName = "A minor",
                bars = rhythmDraft.bars,
                rhythmPresetId  = rhythmDraft.musicalShape?.rhythmPresetId  ?? rhythmDraft.presetId,
                melodyPresetId  = melodyDraft.musicalShape?.melodyPresetId  ?? melodyDraft.presetId,
                harmonyPresetId = harmonyDraft.musicalShape?.harmonyPresetId ?? harmonyDraft.presetId,
                rhythmSoundProfile  = rhythmDraft.musicalShape?.rhythmSoundProfile?.Clone()  ?? rhythmDraft.soundProfile?.Clone(),
                melodySoundProfile  = melodyDraft.musicalShape?.melodySoundProfile?.Clone()  ?? melodyDraft.soundProfile?.Clone(),
                harmonySoundProfile = harmonyDraft.musicalShape?.harmonySoundProfile?.Clone() ?? harmonyDraft.soundProfile?.Clone()
            };

            var unifiedPattern = new PatternDefinition
            {
                id = MathUtils.CreateId("pattern"),
                type = PatternType.RhythmLoop,  // arbitrary — MusicalShapeBehavior ignores type
                name = "Unified-Demo",
                bars = rhythmDraft.bars,
                tempoBase = rhythmDraft.tempoBase,
                key = "A minor",
                groupId = rhythmDraft.groupId,
                genreId = GenreRegistry.GetActive().Id,
                presetId = rhythmDraft.presetId,
                points = rhythmDraft.points,
                worldPoints = rhythmDraft.worldPoints,
                renderRotation = rhythmDraft.renderRotation,
                hasRenderRotation = rhythmDraft.hasRenderRotation,
                derivedSequence = rhythmDraft.derivedSequence,
                tags = rhythmDraft.tags,
                color = rhythmDraft.color,
                shapeProfile = rhythmDraft.shapeProfile,
                shapeProfile3D = rhythmDraft.shapeProfile3D,
                musicalShape = unifiedShape,
                soundProfile = rhythmDraft.soundProfile,
                shapeSummary = rhythmDraft.shapeSummary,
                summary = "Unified 3D shape — rhythm + melody + harmony from one stroke.",
                details = rhythmDraft.details
            };
            state.patterns.Add(unifiedPattern);

            // Spawn at midpoint of the three legacy positions.
            var spawnPos = new Vector3(0.5f, 0.4f, 0.3f);
            SpawnInstanceInScene(state, unifiedPattern.id, "scene-a", spawnPos, 0.3f);

            return state;
        }

        private static PatternDefinition CommitDraftToState(AppState state, DraftResult draft, string name)
        {
            var pattern = new PatternDefinition
            {
                id = MathUtils.CreateId("pattern"),
                type = draft.type,
                name = name,
                bars = draft.bars,
                tempoBase = draft.tempoBase,
                key = draft.key,
                groupId = draft.groupId,
                genreId = GenreRegistry.GetActive().Id,
                presetId = draft.presetId,
                points = draft.points,
                worldPoints = draft.worldPoints,
                renderRotation = draft.renderRotation,
                hasRenderRotation = draft.hasRenderRotation,
                derivedSequence = draft.derivedSequence,
                tags = draft.tags,
                color = draft.color,
                shapeProfile = draft.shapeProfile,
                shapeProfile3D = draft.shapeProfile3D,
                musicalShape = draft.musicalShape,
                soundProfile = draft.soundProfile,
                shapeSummary = draft.shapeSummary,
                summary = draft.summary,
                details = draft.details
            };
            state.patterns.Insert(0, pattern);
            return pattern;
        }

        private static void SpawnInstanceInScene(AppState state, string patternId, string sceneId,
            Vector3 position, float depth)
        {
            var instance = new PatternInstance(patternId, sceneId, position, depth);
            state.instances.Add(instance);

            foreach (var scene in state.scenes)
            {
                if (scene.id == sceneId)
                {
                    scene.instanceIds.Add(instance.id);
                    break;
                }
            }
        }

        // --- Shape generators (copied verbatim from DemoSession since its helpers are private) ---

        private static List<Vector2> GenerateCircle(int pointCount, float radius)
        {
            var points = new List<Vector2>(pointCount + 1);
            for (int i = 0; i <= pointCount; i++)
            {
                float angle = (float)i / pointCount * Mathf.PI * 2f;
                float wobble = 1f + Mathf.Sin(angle * 3f) * 0.08f;
                points.Add(new Vector2(
                    Mathf.Cos(angle) * radius * wobble,
                    Mathf.Sin(angle) * radius * wobble
                ));
            }
            return points;
        }

        private static List<Vector2> GenerateWave(int pointCount, float width, float amplitude)
        {
            var points = new List<Vector2>(pointCount);
            for (int i = 0; i < pointCount; i++)
            {
                float t = (float)i / (pointCount - 1);
                float x = (t - 0.5f) * width;
                float y = Mathf.Sin(t * Mathf.PI * 3f) * amplitude * (0.6f + t * 0.8f);
                points.Add(new Vector2(x, y));
            }
            return points;
        }

        private static List<Vector2> GeneratePadStroke(int pointCount, float width, float height)
        {
            var points = new List<Vector2>(pointCount);
            for (int i = 0; i < pointCount; i++)
            {
                float t = (float)i / (pointCount - 1);
                float x = (t - 0.5f) * width;
                float y = (t - 0.5f) * height + Mathf.Sin(t * Mathf.PI * 2f) * height * 0.3f;
                points.Add(new Vector2(x, y));
            }
            return points;
        }
    }
}
