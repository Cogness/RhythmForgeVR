#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;
using SequencerComponent = RhythmForge.Sequencer.Sequencer;

namespace RhythmForge.Editor
{
    public class ArrangementSequencerTests
    {
        private static readonly MethodInfo AdvanceTransportMethod =
            typeof(SequencerComponent).GetMethod("AdvanceTransport", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void Play_WithoutArrangement_UsesSceneMode()
        {
            var store = new SessionStore();
            store.SetActiveScene("scene-c");

            var sequencer = CreateSequencer(store);
            try
            {
                sequencer.Play();

                Assert.That(sequencer.IsPlaying, Is.True);
                Assert.That(sequencer.CurrentTransport.mode, Is.EqualTo("scene"));
                Assert.That(sequencer.CurrentTransport.slotIndex, Is.EqualTo(-1));
                Assert.That(sequencer.GetPlaybackSceneId(), Is.EqualTo("scene-c"));
                Assert.That(store.State.activeSceneId, Is.EqualTo("scene-c"));
            }
            finally
            {
                DestroySequencer(sequencer);
            }
        }

        [Test]
        public void Play_WithArrangement_StartsAtFirstPopulatedSlot_AndSyncsActiveScene()
        {
            var store = new SessionStore();
            store.SetActiveScene("scene-a");
            store.UpdateArrangement("slot-1", sceneId: "scene-b");
            store.UpdateArrangement("slot-3", sceneId: "scene-d");

            var sequencer = CreateSequencer(store);
            try
            {
                sequencer.Play();

                Assert.That(sequencer.CurrentTransport.mode, Is.EqualTo("arrangement"));
                Assert.That(sequencer.CurrentTransport.slotIndex, Is.EqualTo(0));
                Assert.That(sequencer.GetPlaybackSceneId(), Is.EqualTo("scene-b"));
                Assert.That(store.State.activeSceneId, Is.EqualTo("scene-b"));
            }
            finally
            {
                DestroySequencer(sequencer);
            }
        }

        [Test]
        public void ClearArrangementScene_UnpopulatesSlot()
        {
            var store = new SessionStore();
            store.UpdateArrangement("slot-1", sceneId: "scene-d");

            store.ClearArrangementScene("slot-1");

            Assert.That(store.State.arrangement[0].sceneId, Is.Null);
            Assert.That(store.State.arrangement[0].IsPopulated, Is.False);
        }

        [Test]
        public void Arrangement_SkipsEmptySlots_WhenAdvancing()
        {
            var store = new SessionStore();
            store.UpdateArrangement("slot-1", sceneId: "scene-b");
            store.UpdateArrangement("slot-3", sceneId: "scene-d");

            var sequencer = CreateSequencer(store);
            try
            {
                sequencer.Play();
                AdvanceSteps(sequencer, AppStateFactory.BarSteps * 4);

                Assert.That(sequencer.CurrentTransport.slotIndex, Is.EqualTo(2));
                Assert.That(sequencer.GetPlaybackSceneId(), Is.EqualTo("scene-d"));
                Assert.That(store.State.activeSceneId, Is.EqualTo("scene-d"));
            }
            finally
            {
                DestroySequencer(sequencer);
            }
        }

        [Test]
        public void Arrangement_BarsValueControlsSlotDuration()
        {
            var store = new SessionStore();
            store.UpdateArrangement("slot-1", sceneId: "scene-b", bars: 8);
            store.UpdateArrangement("slot-2", sceneId: "scene-c", bars: 4);

            var sequencer = CreateSequencer(store);
            try
            {
                sequencer.Play();

                AdvanceSteps(sequencer, AppStateFactory.BarSteps * 4);
                Assert.That(sequencer.CurrentTransport.slotIndex, Is.EqualTo(0));
                Assert.That(sequencer.GetPlaybackSceneId(), Is.EqualTo("scene-b"));

                AdvanceSteps(sequencer, AppStateFactory.BarSteps * 4);
                Assert.That(sequencer.CurrentTransport.slotIndex, Is.EqualTo(1));
                Assert.That(sequencer.GetPlaybackSceneId(), Is.EqualTo("scene-c"));
                Assert.That(store.State.activeSceneId, Is.EqualTo("scene-c"));
            }
            finally
            {
                DestroySequencer(sequencer);
            }
        }

        [Test]
        public void Arrangement_LoopsAcrossPopulatedSlots()
        {
            var store = new SessionStore();
            store.UpdateArrangement("slot-1", sceneId: "scene-b");
            store.UpdateArrangement("slot-3", sceneId: "scene-d");

            var sequencer = CreateSequencer(store);
            try
            {
                sequencer.Play();

                AdvanceSteps(sequencer, AppStateFactory.BarSteps * 4);
                Assert.That(sequencer.CurrentTransport.slotIndex, Is.EqualTo(2));

                AdvanceSteps(sequencer, AppStateFactory.BarSteps * 4);
                Assert.That(sequencer.CurrentTransport.slotIndex, Is.EqualTo(0));
                Assert.That(sequencer.GetPlaybackSceneId(), Is.EqualTo("scene-b"));
                Assert.That(store.State.activeSceneId, Is.EqualTo("scene-b"));
            }
            finally
            {
                DestroySequencer(sequencer);
            }
        }

        [Test]
        public void TransportChanged_FiresOnPlay_AndDisplayedBarChanges()
        {
            var store = new SessionStore();
            store.UpdateArrangement("slot-1", sceneId: "scene-b");

            var sequencer = CreateSequencer(store);
            try
            {
                int transportChangedCount = 0;
                sequencer.OnTransportChanged += () => transportChangedCount++;

                sequencer.Play();
                Assert.That(transportChangedCount, Is.EqualTo(1));

                AdvanceSteps(sequencer, AppStateFactory.BarSteps);
                Assert.That(transportChangedCount, Is.EqualTo(2));

                AdvanceSteps(sequencer, AppStateFactory.BarSteps * 3);
                Assert.That(transportChangedCount, Is.EqualTo(5));
            }
            finally
            {
                DestroySequencer(sequencer);
            }
        }

        [Test]
        public void SceneMode_QueuedSceneSwitch_StillUpdatesPlaybackScene()
        {
            var store = new SessionStore();
            store.SetActiveScene("scene-a");

            var sequencer = CreateSequencer(store);
            try
            {
                sequencer.Play();
                store.QueueScene("scene-c");

                AdvanceSteps(sequencer, AppStateFactory.BarSteps);

                Assert.That(sequencer.CurrentTransport.mode, Is.EqualTo("scene"));
                Assert.That(sequencer.CurrentTransport.sceneStep, Is.EqualTo(0));
                Assert.That(sequencer.GetPlaybackSceneId(), Is.EqualTo("scene-c"));
                Assert.That(store.State.activeSceneId, Is.EqualTo("scene-c"));
                Assert.That(store.State.queuedSceneId, Is.Null);
            }
            finally
            {
                DestroySequencer(sequencer);
            }
        }

        private static SequencerComponent CreateSequencer(SessionStore store)
        {
            var go = new GameObject("ArrangementSequencerTests");
            var sequencer = go.AddComponent<SequencerComponent>();
            sequencer.Initialize(store);
            return sequencer;
        }

        private static void AdvanceSteps(SequencerComponent sequencer, int steps)
        {
            Assert.That(AdvanceTransportMethod, Is.Not.Null);

            for (int i = 0; i < steps; i++)
                AdvanceTransportMethod.Invoke(sequencer, null);
        }

        private static void DestroySequencer(SequencerComponent sequencer)
        {
            if (sequencer != null)
                Object.DestroyImmediate(sequencer.gameObject);
        }
    }
}
#endif
