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

        [Test]
        public void CommitDraft_InGuidedMode_ReplacesPreviousHarmonyPattern()
        {
            var state = AppStateFactory.CreateEmpty();
            int notifyCount = 0;
            var repository = new PatternRepository(
                () => state,
                sceneId => GetScene(state, sceneId),
                () => notifyCount++,
                type => $"Reserved-{type}");

            var firstDraft = CreateHarmonyDraft("First Harmony");
            repository.CommitDraft(firstDraft, duplicate: false);

            string firstPatternId = state.patterns[0].id;
            Assert.That(state.patterns, Has.Count.EqualTo(1));
            Assert.That(state.instances, Has.Count.EqualTo(1));

            var secondDraft = CreateHarmonyDraft("Second Harmony");
            repository.CommitDraft(secondDraft, duplicate: false);

            Assert.That(state.patterns, Has.Count.EqualTo(1));
            Assert.That(state.instances, Has.Count.EqualTo(1));
            Assert.That(state.patterns[0].id, Is.Not.EqualTo(firstPatternId));
            Assert.That(state.composition.GetPatternId(CompositionPhase.Harmony), Is.EqualTo(state.patterns[0].id));
            Assert.That(state.scenes[0].instanceIds, Has.Count.EqualTo(1));
        }

        [Test]
        public void CommitDraft_InGuidedMode_ReplacesPreviousMelodyPattern()
        {
            var state = AppStateFactory.CreateEmpty();
            int notifyCount = 0;
            var repository = new PatternRepository(
                () => state,
                sceneId => GetScene(state, sceneId),
                () => notifyCount++,
                type => $"Reserved-{type}");

            var firstDraft = CreateMelodyDraft("First Melody");
            repository.CommitDraft(firstDraft, duplicate: false);

            string firstPatternId = state.patterns[0].id;
            Assert.That(state.patterns, Has.Count.EqualTo(1));
            Assert.That(state.instances, Has.Count.EqualTo(1));

            var secondDraft = CreateMelodyDraft("Second Melody");
            repository.CommitDraft(secondDraft, duplicate: false);

            Assert.That(state.patterns, Has.Count.EqualTo(1));
            Assert.That(state.instances, Has.Count.EqualTo(1));
            Assert.That(state.patterns[0].id, Is.Not.EqualTo(firstPatternId));
            Assert.That(state.composition.GetPatternId(CompositionPhase.Melody), Is.EqualTo(state.patterns[0].id));
            Assert.That(state.scenes[0].instanceIds, Has.Count.EqualTo(1));
            Assert.That(notifyCount, Is.EqualTo(2));
        }

        [Test]
        public void CommitDraft_InGuidedMode_ReplacesPreviousGroovePattern()
        {
            var state = AppStateFactory.CreateEmpty();
            int notifyCount = 0;
            var repository = new PatternRepository(
                () => state,
                sceneId => GetScene(state, sceneId),
                () => notifyCount++,
                type => $"Reserved-{type}");

            var firstDraft = CreateGrooveDraft("First Groove", 0.7f);
            repository.CommitDraft(firstDraft, duplicate: false);

            string firstPatternId = state.patterns[0].id;
            Assert.That(state.patterns, Has.Count.EqualTo(1));
            Assert.That(state.instances, Has.Count.EqualTo(1));

            var secondDraft = CreateGrooveDraft("Second Groove", 1.3f);
            repository.CommitDraft(secondDraft, duplicate: false);

            Assert.That(state.patterns, Has.Count.EqualTo(1));
            Assert.That(state.instances, Has.Count.EqualTo(1));
            Assert.That(state.patterns[0].id, Is.Not.EqualTo(firstPatternId));
            Assert.That(state.composition.GetPatternId(CompositionPhase.Groove), Is.EqualTo(state.patterns[0].id));
            Assert.That(state.scenes[0].instanceIds, Has.Count.EqualTo(1));
            Assert.That(notifyCount, Is.EqualTo(2));
        }

        [Test]
        public void CommitDraft_InGuidedMode_ReplacesPreviousBassPattern()
        {
            var state = AppStateFactory.CreateEmpty();
            int notifyCount = 0;
            var repository = new PatternRepository(
                () => state,
                sceneId => GetScene(state, sceneId),
                () => notifyCount++,
                type => $"Reserved-{type}");

            var firstDraft = CreateBassDraft("First Bass", 43);
            repository.CommitDraft(firstDraft, duplicate: false);

            string firstPatternId = state.patterns[0].id;
            Assert.That(state.patterns, Has.Count.EqualTo(1));
            Assert.That(state.instances, Has.Count.EqualTo(1));

            var secondDraft = CreateBassDraft("Second Bass", 40);
            repository.CommitDraft(secondDraft, duplicate: false);

            Assert.That(state.patterns, Has.Count.EqualTo(1));
            Assert.That(state.instances, Has.Count.EqualTo(1));
            Assert.That(state.patterns[0].id, Is.Not.EqualTo(firstPatternId));
            Assert.That(state.composition.GetPatternId(CompositionPhase.Bass), Is.EqualTo(state.patterns[0].id));
            Assert.That(state.scenes[0].instanceIds, Has.Count.EqualTo(1));
            Assert.That(notifyCount, Is.EqualTo(2));
        }

        private static DraftResult CreateHarmonyDraft(string name)
        {
            var progression = GuidedDefaults.CreateDefaultProgression();
            return new DraftResult
            {
                success = true,
                type = PatternType.Harmony,
                name = name,
                bars = GuidedDefaults.Bars,
                tempoBase = GuidedDefaults.Tempo,
                key = GuidedDefaults.Key,
                groupId = "lofi",
                presetId = "lofi-pad",
                points = new List<Vector2> { Vector2.zero, Vector2.right, Vector2.up },
                renderRotation = Quaternion.identity,
                hasRenderRotation = true,
                spawnPosition = new Vector3(0.4f, 0.3f, 0.25f),
                derivedSequence = new DerivedSequence
                {
                    kind = "harmony",
                    totalSteps = GuidedDefaults.Bars * AppStateFactory.BarSteps,
                    rootMidi = progression.chords[0].rootMidi,
                    flavor = progression.chords[0].flavor,
                    chord = new List<int>(progression.chords[0].voicing),
                    chordEvents = progression.chords
                },
                tags = new List<string> { "guided", "harmony" },
                color = Color.green,
                shapeProfile = new ShapeProfile(),
                soundProfile = new SoundProfile(),
                shapeSummary = "broad balanced arc",
                summary = "harmony bed",
                details = "details"
            };
        }

        private static DraftResult CreateMelodyDraft(string name)
        {
            return new DraftResult
            {
                success = true,
                type = PatternType.Melody,
                name = name,
                bars = GuidedDefaults.Bars,
                tempoBase = GuidedDefaults.Tempo,
                key = GuidedDefaults.Key,
                groupId = "lofi",
                presetId = "lofi-piano",
                points = new List<Vector2> { Vector2.zero, Vector2.right * 0.4f, new Vector2(0.8f, 0.2f) },
                renderRotation = Quaternion.identity,
                hasRenderRotation = true,
                spawnPosition = new Vector3(0.25f, 0.3f, 0.15f),
                derivedSequence = new DerivedSequence
                {
                    kind = "melody",
                    totalSteps = GuidedDefaults.Bars * AppStateFactory.BarSteps,
                    notes = new List<MelodyNote>
                    {
                        new MelodyNote { step = 0, midi = 67, durationSteps = 4, velocity = 0.52f, glide = 0f },
                        new MelodyNote { step = 16, midi = 64, durationSteps = 4, velocity = 0.52f, glide = 0f }
                    }
                },
                tags = new List<string> { "lead", "guided" },
                color = Color.yellow,
                shapeProfile = new ShapeProfile(),
                soundProfile = new SoundProfile(),
                shapeSummary = "arching lead line",
                summary = "melody phrase",
                details = "details"
            };
        }

        private static DraftResult CreateGrooveDraft(string name, float density)
        {
            return new DraftResult
            {
                success = true,
                type = PatternType.Groove,
                name = name,
                bars = GuidedDefaults.Bars,
                tempoBase = GuidedDefaults.Tempo,
                key = GuidedDefaults.Key,
                groupId = "lofi",
                presetId = "lofi-piano",
                points = new List<Vector2> { Vector2.zero, new Vector2(0.3f, 0.2f), new Vector2(0.8f, 0.4f) },
                renderRotation = Quaternion.identity,
                hasRenderRotation = true,
                spawnPosition = new Vector3(0.25f, 0.3f, 0.15f),
                derivedSequence = new DerivedSequence
                {
                    kind = "groove",
                    totalSteps = 0,
                    grooveProfile = new GrooveProfile
                    {
                        density = density,
                        syncopation = 0.24f,
                        swing = 0.16f,
                        quantizeGrid = 16,
                        accentCurve = new[] { 1f, 0.7f, 0.85f, 0.7f }
                    }
                },
                tags = new List<string> { "guided", "groove" },
                color = Color.yellow,
                shapeProfile = new ShapeProfile(),
                soundProfile = new SoundProfile(),
                shapeSummary = "groove contour",
                summary = "groove profile",
                details = "details"
            };
        }

        private static DraftResult CreateBassDraft(string name, int rootMidi)
        {
            return new DraftResult
            {
                success = true,
                type = PatternType.Bass,
                name = name,
                bars = GuidedDefaults.Bars,
                tempoBase = GuidedDefaults.Tempo,
                key = GuidedDefaults.Key,
                groupId = "lofi",
                presetId = "trap-bass",
                points = new List<Vector2> { Vector2.zero, new Vector2(0.35f, 0.18f), new Vector2(0.82f, 0.2f) },
                renderRotation = Quaternion.identity,
                hasRenderRotation = true,
                spawnPosition = new Vector3(0.25f, 0.3f, 0.15f),
                derivedSequence = new DerivedSequence
                {
                    kind = "bass",
                    totalSteps = GuidedDefaults.Bars * AppStateFactory.BarSteps,
                    notes = new List<MelodyNote>
                    {
                        new MelodyNote { step = 0, midi = rootMidi, durationSteps = 8, velocity = 0.56f, glide = 0f },
                        new MelodyNote { step = 8, midi = rootMidi + 7, durationSteps = 6, velocity = 0.48f, glide = 0f }
                    }
                },
                tags = new List<string> { "bass", "guided" },
                color = Color.red,
                shapeProfile = new ShapeProfile(),
                soundProfile = new SoundProfile(),
                shapeSummary = "grounded bass line",
                summary = "bass phrase",
                details = "details"
            };
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
