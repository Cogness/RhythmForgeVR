#if UNITY_EDITOR
using NUnit.Framework;
using RhythmForge.Core.Session;
using RhythmForge.Sequencer;

namespace RhythmForge.Editor
{
    public class ArrangementNavigatorTests
    {
        [Test]
        public void SparseArrangement_FindsFirstPopulatedSlot_AndWrapsToNext()
        {
            var store = new SessionStore();
            store.UpdateArrangement("slot-2", sceneId: "scene-b");
            store.UpdateArrangement("slot-5", sceneId: "scene-d");
            var navigator = new ArrangementNavigator(store);

            Assert.That(navigator.HasArrangement(), Is.True);
            Assert.That(navigator.FindFirstPopulatedSlot(), Is.EqualTo(1));
            Assert.That(navigator.FindNextPopulatedSlot(1), Is.EqualTo(4));
            Assert.That(navigator.FindNextPopulatedSlot(4), Is.EqualTo(1));
            Assert.That(navigator.FindNextPopulatedSlot(0), Is.EqualTo(1));
        }

        [Test]
        public void EmptyArrangement_HasNoArrangement_AndDefaultsToSlotZero()
        {
            var store = new SessionStore();
            var navigator = new ArrangementNavigator(store);

            Assert.That(navigator.HasArrangement(), Is.False);
            Assert.That(navigator.FindFirstPopulatedSlot(), Is.EqualTo(0));
            Assert.That(navigator.FindNextPopulatedSlot(0), Is.EqualTo(0));
        }
    }
}
#endif
