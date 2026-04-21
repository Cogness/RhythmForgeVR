#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;

namespace RhythmForge.Editor
{
    public class PatternRepositoryTests
    {
        [Test]
        public void CommitDraft_CreatesPattern_Instances_AndSelection()
        {
            var state = AppStateFactory.CreateEmpty();
            int notifyCount = 0;
            var repository = new PatternRepository(
                () => state,
                sceneId => GetScene(state, sceneId),
                () => notifyCount++,
                type => $"Reserved-{type}");

            var draft = new DraftResult
            {
                success = true,
                type = PatternType.MelodyLine,
                name = "Test Melody",
                bars = 2,
                tempoBase = 92f,
                key = "A minor",
                groupId = "lofi",
                presetId = "lofi-piano",
                points = new List<Vector2> { Vector2.zero, Vector2.right, Vector2.up },
                renderRotation = Quaternion.identity,
                hasRenderRotation = true,
                spawnPosition = new Vector3(0.4f, 0.3f, 0.25f),
                derivedSequence = new DerivedSequence { kind = "melody", totalSteps = AppStateFactory.BarSteps },
                tags = new List<string> { "airy" },
                color = Color.yellow,
                shapeProfile = new ShapeProfile(),
                soundProfile = new SoundProfile(),
                shapeSummary = "medium smooth line",
                summary = "medium melody",
                details = "details"
            };

            var instance = repository.CommitDraft(draft, duplicate: true);

            Assert.That(state.patterns.Count, Is.EqualTo(1));
            Assert.That(state.instances.Count, Is.EqualTo(2));
            Assert.That(state.scenes[0].instanceIds.Count, Is.EqualTo(2));
            Assert.That(state.selectedPatternId, Is.EqualTo(state.patterns[0].id));
            Assert.That(state.selectedInstanceId, Is.EqualTo(instance.id));
            Assert.That(notifyCount, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateInstance_OffsetsPosition_RecalculatesMix_AndSelectsClone()
        {
            var state = AppStateFactory.CreateEmpty();
            int notifyCount = 0;
            var repository = new PatternRepository(
                () => state,
                sceneId => GetScene(state, sceneId),
                () => notifyCount++,
                type => $"Reserved-{type}");

            state.patterns.Add(new PatternDefinition
            {
                id = "pattern-1",
                type = PatternType.RhythmLoop,
                name = "Beat",
                bars = 1,
                groupId = "lofi",
                presetId = "lofi-drums",
                color = Color.cyan,
                shapeProfile = new ShapeProfile(),
                soundProfile = new SoundProfile(),
                derivedSequence = new DerivedSequence { kind = "rhythm", totalSteps = AppStateFactory.BarSteps }
            });

            var instance = repository.SpawnPattern("pattern-1", "scene-a", new Vector3(0.25f, 0.35f, 0.45f));
            notifyCount = 0;

            repository.DuplicateInstance(instance.id);

            Assert.That(state.instances.Count, Is.EqualTo(2));
            var duplicate = state.instances[1];
            Assert.That(duplicate.id, Is.Not.EqualTo(instance.id));
            Assert.That(duplicate.position, Is.EqualTo(instance.position + new Vector3(0.1f, 0.1f, 0f)));
            Assert.That(duplicate.pan, Is.Not.EqualTo(instance.pan));
            Assert.That(state.scenes[0].instanceIds, Does.Contain(duplicate.id));
            Assert.That(state.selectedInstanceId, Is.EqualTo(duplicate.id));
            Assert.That(notifyCount, Is.EqualTo(1));
        }

        [Test]
        public void CommitDraft_InGuidedMode_TracksLatestPatternForPhase()
        {
            var state = AppStateFactory.CreateEmpty();
            int notifyCount = 0;
            var repository = new PatternRepository(
                () => state,
                sceneId => GetScene(state, sceneId),
                () => notifyCount++,
                type => $"Reserved-{type}");

            var draft = new DraftResult
            {
                success = true,
                type = PatternType.Bass,
                name = "Test Bass",
                bars = 2,
                tempoBase = 100f,
                key = GuidedDefaults.Key,
                groupId = "lofi",
                presetId = "trap-bass",
                points = new List<Vector2> { Vector2.zero, Vector2.right, Vector2.up },
                renderRotation = Quaternion.identity,
                hasRenderRotation = true,
                spawnPosition = new Vector3(0.4f, 0.3f, 0.25f),
                derivedSequence = new DerivedSequence { kind = "melody", totalSteps = AppStateFactory.BarSteps },
                tags = new List<string> { "grounded" },
                color = Color.red,
                shapeProfile = new ShapeProfile(),
                soundProfile = new SoundProfile(),
                shapeSummary = "focused bass motion",
                summary = "bass phrase",
                details = "details"
            };

            repository.CommitDraft(draft, duplicate: false);

            Assert.That(state.patterns.Count, Is.EqualTo(1));
            Assert.That(state.composition.GetPatternId(CompositionPhase.Bass), Is.EqualTo(state.patterns[0].id));
            Assert.That(notifyCount, Is.EqualTo(1));
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
#endif
