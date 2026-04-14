#if UNITY_EDITOR
using NUnit.Framework;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;
using RhythmForge.Sequencer;

namespace RhythmForge.Editor
{
    public class TransportControllerTests
    {
        [Test]
        public void Play_WithArrangement_StartsFromFirstPopulatedSlot_AndSyncsScene()
        {
            var store = new SessionStore();
            store.UpdateArrangement("slot-2", sceneId: "scene-c");
            var navigator = new ArrangementNavigator(store);
            var controller = new TransportController(store, navigator, () => 42d);

            controller.Play();

            Assert.That(controller.IsPlaying, Is.True);
            Assert.That(controller.CurrentTransport.mode, Is.EqualTo("arrangement"));
            Assert.That(controller.CurrentTransport.slotIndex, Is.EqualTo(1));
            Assert.That(controller.CurrentTransport.playbackSceneId, Is.EqualTo("scene-c"));
            Assert.That(controller.CurrentTransport.nextNoteTime, Is.EqualTo(42.05d).Within(0.0001d));
            Assert.That(store.State.activeSceneId, Is.EqualTo("scene-c"));
        }

        [Test]
        public void SceneMode_QueuedSceneSwitchesOnBarBoundary_AndRaisesPlaybackSceneChanged()
        {
            var store = new SessionStore();
            store.SetActiveScene("scene-a");
            var navigator = new ArrangementNavigator(store);
            var controller = new TransportController(store, navigator, () => 10d);
            string previousSceneId = null;
            string currentSceneId = null;
            controller.OnPlaybackSceneChanged += (previous, current) =>
            {
                previousSceneId = previous;
                currentSceneId = current;
            };

            controller.Play();
            store.QueueScene("scene-d");

            for (int i = 0; i < AppStateFactory.BarSteps; i++)
                controller.AdvanceTransport();

            Assert.That(controller.CurrentTransport.mode, Is.EqualTo("scene"));
            Assert.That(controller.CurrentTransport.sceneStep, Is.EqualTo(0));
            Assert.That(controller.CurrentTransport.absoluteBar, Is.EqualTo(2));
            Assert.That(controller.CurrentTransport.playbackSceneId, Is.EqualTo("scene-d"));
            Assert.That(store.State.activeSceneId, Is.EqualTo("scene-d"));
            Assert.That(store.State.queuedSceneId, Is.Null);
            Assert.That(previousSceneId, Is.EqualTo("scene-a"));
            Assert.That(currentSceneId, Is.EqualTo("scene-d"));
        }
    }
}
#endif
