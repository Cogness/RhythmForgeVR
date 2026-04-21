#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;
using RhythmForge.Interaction;

namespace RhythmForge.Editor
{
    public class PhaseControllerTests
    {
        [Test]
        public void GoToPhase_UpdatesDrawMode()
        {
            var store = new SessionStore();
            var root = new GameObject("PhaseControllerTest");
            try
            {
                var drawMode = root.AddComponent<DrawModeController>();
                drawMode.SetEventBus(store.EventBus);
                var phaseController = root.AddComponent<PhaseController>();
                phaseController.Initialize(store, drawMode);

                phaseController.GoToPhase(CompositionPhase.Bass);

                Assert.That(phaseController.CurrentPhase, Is.EqualTo(CompositionPhase.Bass));
                Assert.That(store.GetCurrentPhase(), Is.EqualTo(CompositionPhase.Bass));
                Assert.That(drawMode.CurrentMode, Is.EqualTo(PatternType.Bass));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NextPrev_Wrap()
        {
            var store = new SessionStore();
            var root = new GameObject("PhaseControllerWrapTest");
            try
            {
                var drawMode = root.AddComponent<DrawModeController>();
                drawMode.SetEventBus(store.EventBus);
                var phaseController = root.AddComponent<PhaseController>();
                phaseController.Initialize(store, drawMode);

                phaseController.Next();
                Assert.That(phaseController.CurrentPhase, Is.EqualTo(CompositionPhase.Melody));

                phaseController.Next();
                Assert.That(phaseController.CurrentPhase, Is.EqualTo(CompositionPhase.Groove));

                phaseController.Next();
                Assert.That(phaseController.CurrentPhase, Is.EqualTo(CompositionPhase.Bass));

                phaseController.Next();
                Assert.That(phaseController.CurrentPhase, Is.EqualTo(CompositionPhase.Percussion));

                phaseController.Next();
                Assert.That(phaseController.CurrentPhase, Is.EqualTo(CompositionPhase.Harmony));

                phaseController.Prev();
                Assert.That(phaseController.CurrentPhase, Is.EqualTo(CompositionPhase.Percussion));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
#endif
