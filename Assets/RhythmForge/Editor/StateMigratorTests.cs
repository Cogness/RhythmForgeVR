#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;

namespace RhythmForge.Editor
{
    public class StateMigratorTests
    {
        [Test]
        public void NormalizeState_CleansMembership_NormalizesModes_MaterializesShapes_AndResetsInvalidRotation()
        {
            var migrator = new StateMigrator();
            var state = AppStateFactory.CreateEmpty();
            state.version = 2;
            state.drawMode = "NotARealMode";
            state.drawShapeMode = "NotARealShapeMode";
            state.activeSceneId = "scene-a";
            state.scenes[0].instanceIds.Add("missing-instance");

            var pattern = new PatternDefinition
            {
                id = "pattern-1",
                type = PatternType.HarmonyPad,
                name = "Pad",
                bars = 1,
                groupId = "dream",
                presetId = "dream-pad",
                shapeProfile = new ShapeProfile
                {
                    worldWidth = 0.2f,
                    worldHeight = 0.18f,
                    worldLength = 0.5f,
                    worldAverageSize = 0.19f,
                    worldMaxDimension = 0.2f
                },
                soundProfile = new SoundProfile(),
                derivedSequence = new DerivedSequence { kind = "harmony", totalSteps = AppStateFactory.BarSteps },
                hasRenderRotation = true,
                renderRotation = new Quaternion(0f, 0f, 0f, 0f),
                summary = "",
                details = ""
            };
            state.patterns.Add(pattern);

            var instance = new PatternInstance(pattern.id, "scene-b", Vector3.zero, 0.3f);
            state.instances.Add(instance);
            state.scenes[1].instanceIds.Clear();

            migrator.NormalizeState(state);

            Assert.That(state.version, Is.EqualTo(10));
            Assert.That(state.drawMode, Is.EqualTo(PatternType.RhythmLoop.ToString()));
            Assert.That(state.drawShapeMode, Is.EqualTo(ShapeFacetMode.Free.ToString()));
            Assert.That(state.scenes[0].instanceIds, Does.Not.Contain("missing-instance"));
            Assert.That(state.scenes[1].instanceIds, Does.Contain(instance.id));
            Assert.That(pattern.hasRenderRotation, Is.False);
            Assert.That(pattern.renderRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(pattern.musicalShape, Is.Not.Null);
            Assert.That(pattern.musicalShape.totalSteps, Is.EqualTo(AppStateFactory.BarSteps));
            Assert.That(pattern.musicalShape.facets.harmony.events.Count, Is.EqualTo(1));
            Assert.That(instance.reverbSend, Is.EqualTo(Mathf.Clamp01(instance.depth * 0.55f)).Within(0.0001f));
            Assert.That(instance.delaySend, Is.EqualTo(Mathf.Clamp01(instance.depth * 0.35f)).Within(0.0001f));
            Assert.That(instance.gainTrim, Is.EqualTo(Mathf.Clamp01(1.05f - instance.depth * 0.15f)).Within(0.0001f));
            Assert.That(instance.ensembleRoleIndex, Is.EqualTo(0));
            Assert.That(instance.progressionBarIndex, Is.EqualTo(0));
        }

        [Test]
        public void NormalizeState_AssignsSceneScopedEnsembleAndProgressionIndexes_FromSceneOrder()
        {
            var migrator = new StateMigrator();
            var state = AppStateFactory.CreateEmpty();

            var patternA = new PatternDefinition { id = "pattern-a", type = PatternType.RhythmLoop, bars = 2 };
            var patternB = new PatternDefinition { id = "pattern-b", type = PatternType.MelodyLine, bars = 2 };
            state.patterns.Add(patternA);
            state.patterns.Add(patternB);

            var instanceA = new PatternInstance("pattern-a", "scene-a", Vector3.zero, 0.2f);
            var instanceB = new PatternInstance("pattern-b", "scene-a", Vector3.one, 0.3f);
            state.instances.Add(instanceA);
            state.instances.Add(instanceB);
            state.scenes[0].instanceIds.Clear();
            state.scenes[0].instanceIds.Add(instanceB.id);
            state.scenes[0].instanceIds.Add(instanceA.id);

            migrator.NormalizeState(state);

            Assert.That(instanceB.ensembleRoleIndex, Is.EqualTo(0));
            Assert.That(instanceB.progressionBarIndex, Is.EqualTo(0));
            Assert.That(instanceA.ensembleRoleIndex, Is.EqualTo(1));
            Assert.That(instanceA.progressionBarIndex, Is.EqualTo(1));
        }

        [Test]
        public void NormalizeState_MigratesLegacyInstanceMix_ToSpatialFields()
        {
            var migrator = new StateMigrator();
            var state = AppStateFactory.CreateEmpty();
            state.version = 9;

            var instance = new PatternInstance("pattern-a", "scene-a", new Vector3(0.3f, 0.25f, 0.9f), 0.9f);
#pragma warning disable CS0618
            instance.pan = -0.8f;
            instance.gain = 0.42f;
#pragma warning restore CS0618
            state.instances.Add(instance);
            state.scenes[0].instanceIds.Clear();
            state.scenes[0].instanceIds.Add(instance.id);

            migrator.NormalizeState(state);

            Assert.That(state.version, Is.EqualTo(10));
            Assert.That(instance.brightness, Is.EqualTo(Mathf.Clamp01(1f - instance.position.y)).Within(0.0001f));
            Assert.That(instance.reverbSend, Is.EqualTo(Mathf.Clamp01(instance.depth * 0.55f)).Within(0.0001f));
            Assert.That(instance.delaySend, Is.EqualTo(Mathf.Clamp01(instance.depth * 0.35f)).Within(0.0001f));
            Assert.That(instance.gainTrim, Is.EqualTo(Mathf.Clamp01(1.05f - instance.depth * 0.15f)).Within(0.0001f));
        }
    }
}
#endif
