using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Core.Session
{
    /// <summary>
    /// Creates a hardcoded demo session with pre-drawn patterns for testing.
    /// Mirrors the pilot's createDemoPatterns / createDemoInstances logic.
    /// </summary>
    public static class DemoSession
    {
        public static AppState CreateDemoState(SessionStore store)
        {
            var state = AppStateFactory.CreateEmpty();
            state.tempo = 85f;
            state.key = "A minor";
            state.activeGroupId = "lofi";
            state.activeSceneId = "scene-a";

            // --- Demo Rhythm: a rough circle ---
            var circlePoints = GenerateCircle(24, 0.14f);
            var rhythmDraft = DraftBuilder.BuildFromStroke(
                PatternType.RhythmLoop, circlePoints, new Vector3(0.35f, 0.34f, 0.3f),
                Quaternion.identity, state, store);

            if (rhythmDraft.success)
            {
                var pattern = CommitDraftToState(state, rhythmDraft, "Beat-Demo");
                SpawnInstanceInScene(state, pattern.id, "scene-a", new Vector3(0.35f, 0.34f, 0.3f), 0.3f);
            }

            // --- Demo Melody: a wavy line ---
            var wavePoints = GenerateWave(32, 0.2f, 0.08f);
            var melodyDraft = DraftBuilder.BuildFromStroke(
                PatternType.MelodyLine, wavePoints, new Vector3(0.66f, 0.36f, 0.28f),
                Quaternion.identity, state, store);

            if (melodyDraft.success)
            {
                var pattern = CommitDraftToState(state, melodyDraft, "Melody-Demo");
                SpawnInstanceInScene(state, pattern.id, "scene-a", new Vector3(0.66f, 0.36f, 0.28f), 0.28f);
            }

            // --- Demo Harmony: a broad diagonal stroke ---
            var padPoints = GeneratePadStroke(20, 0.18f, 0.06f);
            var harmonyDraft = DraftBuilder.BuildFromStroke(
                PatternType.HarmonyPad, padPoints, new Vector3(0.5f, 0.58f, 0.5f),
                Quaternion.identity, state, store);

            if (harmonyDraft.success)
            {
                var pattern = CommitDraftToState(state, harmonyDraft, "Pad-Demo");
                SpawnInstanceInScene(state, pattern.id, "scene-a", new Vector3(0.5f, 0.58f, 0.5f), 0.5f);
            }

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
                presetId = draft.presetId,
                points = draft.points,
                renderRotation = draft.renderRotation,
                hasRenderRotation = draft.hasRenderRotation,
                derivedSequence = draft.derivedSequence,
                tags = draft.tags,
                color = draft.color,
                shapeProfile = draft.shapeProfile,
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

        // --- Shape generators ---

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
