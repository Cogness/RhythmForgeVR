#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.PatternBehavior;
using RhythmForge.Core.PatternBehavior.Behaviors;

namespace RhythmForge.Editor
{
    public class GrooveBehaviorTests
    {
        [Test]
        public void Schedule_RecordsVisualTrigger_OnGroovePulseStep()
        {
            var behavior = new GrooveBehavior();
            var pattern = new PatternDefinition
            {
                id = "groove-1",
                type = PatternType.Groove,
                name = "Groove",
                groupId = "lofi",
                presetId = "lofi-piano",
                color = Color.yellow,
                shapeProfile = new ShapeProfile(),
                soundProfile = new SoundProfile(),
                derivedSequence = new DerivedSequence
                {
                    kind = "groove",
                    totalSteps = GuidedDefaults.Bars * AppStateFactory.BarSteps,
                    grooveProfile = new GrooveProfile
                    {
                        density = 1.2f,
                        syncopation = 0.2f,
                        swing = 0.1f,
                        quantizeGrid = 8,
                        accentCurve = new[] { 1f, 0.7f, 0.85f, 0.7f }
                    }
                }
            };

            var instance = new PatternInstance(pattern.id, "scene-a", Vector3.zero, 0.3f);
            var state = AppStateFactory.CreateEmpty();
            state.composition.groove = pattern.derivedSequence.grooveProfile.Clone();

            bool triggered = false;
            behavior.Schedule(new PatternSchedulingContext
            {
                pattern = pattern,
                instance = instance,
                localStep = 0,
                scheduledTime = 1d,
                stepDuration = 0.15f,
                appState = state,
                recordTrigger = (_, __, ___) => triggered = true
            });

            Assert.That(triggered, Is.True);
        }
    }
}
#endif
