using System;
using System.Collections.Generic;

namespace RhythmForge.Core.Data
{
    [Serializable]
    public class ChordSlot
    {
        public int barIndex;
        public int rootMidi;
        public string flavor;
        public List<int> voicing = new List<int>();

        public ChordSlot Clone()
        {
            return new ChordSlot
            {
                barIndex = barIndex,
                rootMidi = rootMidi,
                flavor = flavor,
                voicing = voicing != null ? new List<int>(voicing) : new List<int>()
            };
        }
    }

    [Serializable]
    public class ChordProgression
    {
        public int bars = 8;
        public List<ChordSlot> chords = new List<ChordSlot>();

        public ChordSlot GetSlotForBar(int barIndex)
        {
            if (chords == null || chords.Count == 0)
                return null;

            int wrappedBar = WrapBarIndex(barIndex, chords.Count);
            for (int i = 0; i < chords.Count; i++)
            {
                var slot = chords[i];
                if (slot != null && slot.barIndex == wrappedBar)
                    return slot;
            }

            return chords[wrappedBar];
        }

        public HarmonicContext ToHarmonicContext(int barIndex)
        {
            var slot = GetSlotForBar(barIndex);
            if (slot == null)
                return new HarmonicContext();

            return new HarmonicContext
            {
                rootMidi = slot.rootMidi,
                chordTones = slot.voicing != null ? new List<int>(slot.voicing) : new List<int>(),
                flavor = string.IsNullOrEmpty(slot.flavor) ? "major" : slot.flavor
            };
        }

        public ChordProgression Clone()
        {
            var clone = new ChordProgression
            {
                bars = bars,
                chords = new List<ChordSlot>()
            };

            if (chords == null)
                return clone;

            for (int i = 0; i < chords.Count; i++)
            {
                if (chords[i] != null)
                    clone.chords.Add(chords[i].Clone());
            }

            return clone;
        }

        private static int WrapBarIndex(int barIndex, int count)
        {
            if (count <= 0)
                return 0;

            int wrapped = barIndex % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }
    }
}
