using System;
using System.Collections.Generic;

namespace RhythmForge.Core.Data
{
    [Serializable]
    public class MusicalKey
    {
        public string name;
        public int rootMidi;
        public int[] scale;

        public MusicalKey(string name, int rootMidi, int[] scale)
        {
            this.name = name;
            this.rootMidi = rootMidi;
            this.scale = scale;
        }
    }

    public static class MusicalKeys
    {
        private static readonly int[] MinorScale = { 0, 2, 3, 5, 7, 8, 10 };
        private static readonly int[] MajorScale = { 0, 2, 4, 5, 7, 9, 11 };

        private static Dictionary<string, MusicalKey> _keys;

        public static Dictionary<string, MusicalKey> All
        {
            get
            {
                if (_keys == null)
                    Initialize();
                return _keys;
            }
        }

        private static void Initialize()
        {
            _keys = new Dictionary<string, MusicalKey>
            {
                { "A minor", new MusicalKey("A minor", 57, MinorScale) },
                { "C major", new MusicalKey("C major", 60, MajorScale) },
                { "D minor", new MusicalKey("D minor", 62, MinorScale) },
                { "E minor", new MusicalKey("E minor", 64, MinorScale) },
                { "F major", new MusicalKey("F major", 65, MajorScale) },
                { "G major", new MusicalKey("G major", 67, MajorScale) },
                { "B minor", new MusicalKey("B minor", 59, MinorScale) },
                { "C minor", new MusicalKey("C minor", 60, MinorScale) },
                { "D major", new MusicalKey("D major", 62, MajorScale) },
                { "E major", new MusicalKey("E major", 64, MajorScale) },
                { "F minor", new MusicalKey("F minor", 65, MinorScale) },
                { "G minor", new MusicalKey("G minor", 67, MinorScale) },
            };
        }

        public static MusicalKey Get(string keyName)
        {
            if (All.TryGetValue(keyName, out var key))
                return key;
            return All["A minor"];
        }
    }
}
