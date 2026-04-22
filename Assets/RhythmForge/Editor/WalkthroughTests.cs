#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using RhythmForge;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.Session;
using RhythmForge.UI.Panels;

namespace RhythmForge.Editor
{
    public class WalkthroughTests
    {
        private static readonly FieldInfo StoreField =
            typeof(RhythmForgeManager).GetField("_store", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo ApplyGuidedModeUiStateMethod =
            typeof(RhythmForgeManager).GetMethod("ApplyGuidedModeUiState", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void Guided5StepFlow_ProducesFullComposition()
        {
            var store = new SessionStore();

            CommitStroke(store, PatternType.Harmony, CreateHarmonyPoints());
            CommitStroke(store, PatternType.Melody, CreateMelodyPoints());
            CommitStroke(store, PatternType.Groove, CreateGroovePoints());
            CommitStroke(store, PatternType.Bass, CreateBassPoints());
            CommitStroke(store, PatternType.Percussion, CreatePercussionPoints());

            var composition = store.GetComposition();
            Assert.That(composition.GetPatternId(CompositionPhase.Harmony), Is.Not.Null);
            Assert.That(composition.GetPatternId(CompositionPhase.Melody), Is.Not.Null);
            Assert.That(composition.GetPatternId(CompositionPhase.Groove), Is.Not.Null);
            Assert.That(composition.GetPatternId(CompositionPhase.Bass), Is.Not.Null);
            Assert.That(composition.GetPatternId(CompositionPhase.Percussion), Is.Not.Null);
            Assert.That(composition.progression.chords, Has.Count.EqualTo(GuidedDefaults.Bars));
            Assert.That(composition.groove, Is.Not.Null);

            var harmony = store.GetPattern(composition.GetPatternId(CompositionPhase.Harmony));
            var melody = store.GetPattern(composition.GetPatternId(CompositionPhase.Melody));
            var bass = store.GetPattern(composition.GetPatternId(CompositionPhase.Bass));
            var percussion = store.GetPattern(composition.GetPatternId(CompositionPhase.Percussion));

            Assert.That(harmony.derivedSequence.chordEvents, Has.Count.EqualTo(GuidedDefaults.Bars));
            Assert.That(melody.derivedSequence.notes, Is.Not.Empty);
            Assert.That(bass.derivedSequence.notes, Is.Not.Empty);
            Assert.That(percussion.derivedSequence.events, Is.Not.Empty);

            AssertKickAndBassAlignOnBarBeatOne(bass.derivedSequence.notes, percussion.derivedSequence.events, 0);
            AssertKickAndBassAlignOnBarBeatOne(bass.derivedSequence.notes, percussion.derivedSequence.events, 4);
        }

        [Test]
        public void HarmonyRedraw_InvalidatesAndRederivesDependentPhases()
        {
            var store = new SessionStore();

            CommitStroke(store, PatternType.Harmony, CreateHarmonyPoints());
            CommitStroke(store, PatternType.Melody, CreateMelodyPoints());
            CommitStroke(store, PatternType.Bass, CreateBassPoints());

            var invalidations = new List<PhaseInvalidationChangedEvent>();
            store.EventBus.Subscribe<PhaseInvalidationChangedEvent>(evt => invalidations.Add(evt));

            CommitStroke(store, PatternType.Harmony, CreateAlternateHarmonyPoints());

            Assert.That(store.GetPhaseInvalidation(CompositionPhase.Melody), Is.EqualTo(PhaseInvalidationKind.AsyncRederive));
            Assert.That(store.GetPhaseInvalidation(CompositionPhase.Bass), Is.EqualTo(PhaseInvalidationKind.AsyncRederive));

            DateTime deadline = DateTime.UtcNow.AddSeconds(3);
            while ((store.GetPhaseInvalidation(CompositionPhase.Melody) != PhaseInvalidationKind.None ||
                    store.GetPhaseInvalidation(CompositionPhase.Bass) != PhaseInvalidationKind.None) &&
                   DateTime.UtcNow < deadline)
            {
                store.Tick();
            }

            store.Tick();

            Assert.That(store.GetPhaseInvalidation(CompositionPhase.Melody), Is.EqualTo(PhaseInvalidationKind.None));
            Assert.That(store.GetPhaseInvalidation(CompositionPhase.Bass), Is.EqualTo(PhaseInvalidationKind.None));
            Assert.That(invalidations.Exists(evt => evt.Phase == CompositionPhase.Melody && evt.Kind == PhaseInvalidationKind.AsyncRederive), Is.True);
            Assert.That(invalidations.Exists(evt => evt.Phase == CompositionPhase.Bass && evt.Kind == PhaseInvalidationKind.AsyncRederive), Is.True);
            Assert.That(store.GetPattern(store.GetComposition().GetPatternId(CompositionPhase.Melody)).derivedSequence.notes, Is.Not.Empty);
            Assert.That(store.GetPattern(store.GetComposition().GetPatternId(CompositionPhase.Bass)).derivedSequence.notes, Is.Not.Empty);
        }

        [Test]
        public void GuidedMode_HidesSceneAndArrangementPanels()
        {
            var managerGo = new GameObject("WalkthroughManager");
            var phaseGo = new GameObject("PhasePanel");
            var sceneGo = new GameObject("SceneStripPanel");
            var arrangementGo = new GameObject("ArrangementPanel");

            var manager = managerGo.AddComponent<RhythmForgeManager>();
            var phasePanel = phaseGo.AddComponent<PhasePanel>();
            var scenePanel = sceneGo.AddComponent<SceneStripPanel>();
            var arrangementPanel = arrangementGo.AddComponent<ArrangementPanel>();

            try
            {
                manager.Configure(
                    new ManagerSubsystems(),
                    new ManagerPanels
                    {
                        phase = phasePanel,
                        sceneStrip = scenePanel,
                        arrangement = arrangementPanel
                    },
                    null,
                    null);

                StoreField.SetValue(manager, new SessionStore());
                ApplyGuidedModeUiStateMethod.Invoke(manager, null);

                Assert.That(phaseGo.activeSelf, Is.True);
                Assert.That(sceneGo.activeSelf, Is.False);
                Assert.That(arrangementGo.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(arrangementGo);
                UnityEngine.Object.DestroyImmediate(sceneGo);
                UnityEngine.Object.DestroyImmediate(phaseGo);
                UnityEngine.Object.DestroyImmediate(managerGo);
            }
        }

        private static void CommitStroke(SessionStore store, PatternType type, List<Vector2> points)
        {
            var draft = DraftBuilder.BuildFromStroke(
                type,
                points,
                new Vector3(0.5f, 0.5f, 0.4f),
                Quaternion.identity,
                store.State,
                store);

            Assert.That(draft.success, Is.True);
            store.CommitDraft(draft, duplicate: false);
        }

        private static List<Vector2> CreateHarmonyPoints()
        {
            return new List<Vector2>
            {
                new Vector2(-0.4f, -0.15f),
                new Vector2(-0.1f, -0.05f),
                new Vector2(0.15f, 0.08f),
                new Vector2(0.4f, 0.2f)
            };
        }

        private static List<Vector2> CreateAlternateHarmonyPoints()
        {
            return new List<Vector2>
            {
                new Vector2(-0.45f, 0.18f),
                new Vector2(-0.12f, 0.32f),
                new Vector2(0.18f, 0.28f),
                new Vector2(0.42f, 0.12f)
            };
        }

        private static List<Vector2> CreateMelodyPoints()
        {
            return new List<Vector2>
            {
                new Vector2(0.00f, 0.45f),
                new Vector2(0.12f, 0.58f),
                new Vector2(0.28f, 0.34f),
                new Vector2(0.46f, 0.72f),
                new Vector2(0.70f, 0.40f),
                new Vector2(0.92f, 0.56f)
            };
        }

        private static List<Vector2> CreateGroovePoints()
        {
            return new List<Vector2>
            {
                new Vector2(0.00f, 0.40f),
                new Vector2(0.18f, 0.76f),
                new Vector2(0.34f, 0.38f),
                new Vector2(0.56f, 0.74f),
                new Vector2(0.80f, 0.36f),
                new Vector2(1.00f, 0.60f)
            };
        }

        private static List<Vector2> CreateBassPoints()
        {
            return new List<Vector2>
            {
                new Vector2(0.00f, 0.20f),
                new Vector2(0.18f, 0.26f),
                new Vector2(0.38f, 0.18f),
                new Vector2(0.62f, 0.30f),
                new Vector2(0.86f, 0.22f)
            };
        }

        private static List<Vector2> CreatePercussionPoints()
        {
            return new List<Vector2>
            {
                new Vector2(-0.2f, -0.2f),
                new Vector2(0.2f, -0.2f),
                new Vector2(0.24f, 0.18f),
                new Vector2(-0.18f, 0.22f),
                new Vector2(-0.2f, -0.2f)
            };
        }

        private static void AssertKickAndBassAlignOnBarBeatOne(List<MelodyNote> bassNotes, List<RhythmEvent> events, int barIndex)
        {
            int step = barIndex * AppStateFactory.BarSteps;
            Assert.That(bassNotes.Exists(note => note.step == step), Is.True, $"Expected bass note on bar {barIndex + 1} beat 1.");
            Assert.That(events.Exists(evt => evt.step == step && evt.lane == "kick"), Is.True, $"Expected kick on bar {barIndex + 1} beat 1.");
        }
    }
}
#endif
