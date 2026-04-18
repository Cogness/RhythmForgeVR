using System;
using System.Collections.Generic;

namespace RhythmForge.Core.Data
{
    /// <summary>
    /// Holder bundling the three facet sequences produced by a single MusicalShape.
    /// Unused by the runtime in Phase A (type definition only). Phase B onwards, the
    /// unified deriver populates all three facets for every shape.
    /// </summary>
    [Serializable]
    public class DerivedShapeSequence
    {
        public RhythmSequence rhythm;
        public MelodySequence melody;
        public HarmonySequence harmony;

        public DerivedShapeSequence Clone()
        {
            return new DerivedShapeSequence
            {
                rhythm = CloneRhythm(rhythm),
                melody = CloneMelody(melody),
                harmony = CloneHarmony(harmony)
            };
        }

        private static RhythmSequence CloneRhythm(RhythmSequence src)
        {
            if (src == null) return null;
            var copy = new RhythmSequence
            {
                kind = src.kind,
                totalSteps = src.totalSteps,
                swing = src.swing,
                events = new List<RhythmEvent>()
            };
            if (src.events != null)
            {
                foreach (var ev in src.events)
                {
                    copy.events.Add(new RhythmEvent
                    {
                        step = ev.step,
                        lane = ev.lane,
                        velocity = ev.velocity,
                        microShift = ev.microShift
                    });
                }
            }
            return copy;
        }

        private static MelodySequence CloneMelody(MelodySequence src)
        {
            if (src == null) return null;
            var copy = new MelodySequence
            {
                kind = src.kind,
                totalSteps = src.totalSteps,
                notes = new List<MelodyNote>()
            };
            if (src.notes != null)
            {
                foreach (var n in src.notes)
                {
                    copy.notes.Add(new MelodyNote
                    {
                        step = n.step,
                        midi = n.midi,
                        durationSteps = n.durationSteps,
                        velocity = n.velocity,
                        glide = n.glide
                    });
                }
            }
            return copy;
        }

        private static HarmonySequence CloneHarmony(HarmonySequence src)
        {
            if (src == null) return null;
            var events = new List<HarmonyEvent>();
            if (src.events != null)
            {
                foreach (var evt in src.events)
                {
                    events.Add(new HarmonyEvent
                    {
                        step = evt.step,
                        durationSteps = evt.durationSteps,
                        rootMidi = evt.rootMidi,
                        chord = evt.chord != null ? new List<int>(evt.chord) : new List<int>(),
                        flavor = evt.flavor
                    });
                }
            }

            return new HarmonySequence
            {
                kind = src.kind,
                totalSteps = src.totalSteps,
                events = events,
                flavor = src.flavor,
                rootMidi = src.rootMidi,
                chord = src.chord != null ? new List<int>(src.chord) : null
            };
        }
    }
}
