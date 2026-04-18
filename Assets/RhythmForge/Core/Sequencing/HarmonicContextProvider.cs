using System.Collections.Generic;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    /// <summary>
    /// Lightweight static bridge that makes the current HarmonicContext available to all
    /// melody derivers without changing the IMelodyDeriver interface.
    /// Unity's main-thread single-threaded dispatch makes this safe.
    ///
    /// Phase B: the bridge also supports a <see cref="HarmonicFabric"/> view. When
    /// <see cref="SetFabricView"/> has been called on a thread AND no explicit
    /// <see cref="Set"/> has been invoked in the current scope on that thread,
    /// <see cref="Current"/> returns a <see cref="HarmonicContext"/> built from
    /// <c>fabric.ChordAtBar(0)</c>. Inside a <see cref="PatternContextScope.Push"/>,
    /// the explicitly-pushed context always wins — which is what keeps Phase B
    /// audibly bit-identical to today.
    /// </summary>
    public static class HarmonicContextProvider
    {
        [System.ThreadStatic] private static HarmonicContext _current;
        [System.ThreadStatic] private static bool _explicitlySet;
        [System.ThreadStatic] private static HarmonicFabric _fabricView;

        /// <summary>
        /// Set by <see cref="PatternContextScope.Push"/> before every derivation
        /// call. While set, it takes precedence over the fabric view.
        /// </summary>
        public static void Set(HarmonicContext ctx)
        {
            _current = ctx?.Clone() ?? new HarmonicContext();
            _explicitlySet = true;
        }

        /// <summary>
        /// Installs a per-thread <see cref="HarmonicFabric"/> view so that
        /// <see cref="Current"/> — when no scope-pushed context is active — falls
        /// back to <c>fabric.ChordAtBar(0)</c>. Main-thread only in Phase B.
        /// </summary>
        public static void SetFabricView(HarmonicFabric fabric)
        {
            _fabricView = fabric;
        }

        /// <summary>Returns the current shared context (never null).</summary>
        public static HarmonicContext Current
        {
            get
            {
                if (_explicitlySet)
                    return _current ?? (_current = new HarmonicContext());

                if (_fabricView != null)
                {
                    var placement = _fabricView.ChordAtBar(0);
                    if (placement != null && placement.tones != null && placement.tones.Count > 0)
                    {
                        return new HarmonicContext
                        {
                            rootMidi = placement.rootMidi,
                            chordTones = new List<int>(placement.tones),
                            flavor = placement.flavor ?? "minor"
                        };
                    }
                }

                return _current ?? (_current = new HarmonicContext());
            }
        }

        public static void Clear()
        {
            _current = new HarmonicContext();
            _explicitlySet = false;
        }
    }
}
