using System.Collections.Generic;
using RhythmForge.Core.Session;

namespace RhythmForge.Sequencer
{
    internal sealed class ArrangementNavigator
    {
        private readonly SessionStore _store;

        public ArrangementNavigator(SessionStore store)
        {
            _store = store;
        }

        public bool HasArrangement()
        {
            if (_store?.State?.arrangement == null)
                return false;

            foreach (var slot in _store.State.arrangement)
            {
                if (slot.IsPopulated)
                    return true;
            }

            return false;
        }

        public int FindFirstPopulatedSlot()
        {
            for (int i = 0; i < _store.State.arrangement.Count; i++)
            {
                if (_store.State.arrangement[i].IsPopulated)
                    return i;
            }

            return 0;
        }

        public int FindNextPopulatedSlot(int currentIndex)
        {
            var populated = new List<int>();
            for (int i = 0; i < _store.State.arrangement.Count; i++)
            {
                if (_store.State.arrangement[i].IsPopulated)
                    populated.Add(i);
            }

            if (populated.Count == 0)
                return 0;

            int currentPos = populated.IndexOf(currentIndex);
            return populated[(currentPos + 1) % populated.Count];
        }
    }
}
