using System.Collections.Generic;
using System.Text;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;

namespace RhythmForge.UI
{
    /// <summary>
    /// Returns the mode-relevant geometric inputs and key sound outputs,
    /// and formats them into a compact multi-line label for world-space display.
    /// </summary>
    public static class PatternParameterHelper
    {
        // ── Geometric input descriptors per mode ──

        public struct ParamEntry
        {
            public string label;
            public float value;
        }

        public static List<ParamEntry> GetRelevantInputs(PatternType type, ShapeProfile sp)
        {
            var list = new List<ParamEntry>();
            if (sp == null) return list;

            switch (PatternTypeCompatibility.Canonicalize(type))
            {
                case PatternType.Percussion:
                    list.Add(new ParamEntry { label = "size", value = ShapeProfileSizing.GetSizeFactor(type, sp) });
                    list.Add(new ParamEntry { label = "ang", value = sp.angularity });
                    list.Add(new ParamEntry { label = "circ", value = sp.circularity });
                    list.Add(new ParamEntry { label = "sym", value = sp.symmetry });
                    list.Add(new ParamEntry { label = "wob", value = sp.wobble });
                    list.Add(new ParamEntry { label = "curvVar", value = sp.curvatureVariance });
                    break;

                case PatternType.Melody:
                case PatternType.Bass:
                case PatternType.Groove:
                    list.Add(new ParamEntry { label = "size", value = ShapeProfileSizing.GetSizeFactor(type, sp) });
                    list.Add(new ParamEntry { label = "ang", value = sp.angularity });
                    list.Add(new ParamEntry { label = "centH", value = sp.centroidHeight });
                    list.Add(new ParamEntry { label = "vSpan", value = sp.verticalSpan });
                    list.Add(new ParamEntry { label = "hSpan", value = sp.horizontalSpan });
                    list.Add(new ParamEntry { label = "curvM", value = sp.curvatureMean });
                    list.Add(new ParamEntry { label = "curvVar", value = sp.curvatureVariance });
                    list.Add(new ParamEntry { label = "spdVar", value = sp.speedVariance });
                    list.Add(new ParamEntry { label = "dirBias", value = sp.directionBias });
                    break;

                default:
                    list.Add(new ParamEntry { label = "size", value = ShapeProfileSizing.GetSizeFactor(type, sp) });
                    list.Add(new ParamEntry { label = "centH", value = sp.centroidHeight });
                    list.Add(new ParamEntry { label = "ang", value = sp.angularity });
                    list.Add(new ParamEntry { label = "tilt", value = sp.tiltSigned });
                    list.Add(new ParamEntry { label = "sym", value = sp.symmetry });
                    list.Add(new ParamEntry { label = "hSpan", value = sp.horizontalSpan });
                    list.Add(new ParamEntry { label = "path", value = sp.pathLength });
                    list.Add(new ParamEntry { label = "curvVar", value = sp.curvatureVariance });
                    list.Add(new ParamEntry { label = "vSpan", value = sp.verticalSpan });
                    break;
            }

            return list;
        }

        public static List<ParamEntry> GetRelevantOutputs(PatternType type, SoundProfile snd)
        {
            var list = new List<ParamEntry>();
            if (snd == null) return list;

            switch (PatternTypeCompatibility.Canonicalize(type))
            {
                case PatternType.Percussion:
                    list.Add(new ParamEntry { label = "bright", value = snd.brightness });
                    list.Add(new ParamEntry { label = "drive", value = snd.drive });
                    list.Add(new ParamEntry { label = "trans", value = snd.transientSharpness });
                    list.Add(new ParamEntry { label = "attack", value = snd.attackBias });
                    list.Add(new ParamEntry { label = "release", value = snd.releaseBias });
                    list.Add(new ParamEntry { label = "body", value = snd.body });
                    list.Add(new ParamEntry { label = "groove", value = snd.grooveInstability });
                    list.Add(new ParamEntry { label = "stereo", value = snd.stereoSpread });
                    break;

                case PatternType.Melody:
                case PatternType.Bass:
                case PatternType.Groove:
                    list.Add(new ParamEntry { label = "bright", value = snd.brightness });
                    list.Add(new ParamEntry { label = "reson", value = snd.resonance });
                    list.Add(new ParamEntry { label = "drive", value = snd.drive });
                    list.Add(new ParamEntry { label = "attack", value = snd.attackBias });
                    list.Add(new ParamEntry { label = "release", value = snd.releaseBias });
                    list.Add(new ParamEntry { label = "modDep", value = snd.modDepth });
                    list.Add(new ParamEntry { label = "stereo", value = snd.stereoSpread });
                    list.Add(new ParamEntry { label = "filter", value = snd.filterMotion });
                    break;

                default:
                    list.Add(new ParamEntry { label = "bright", value = snd.brightness });
                    list.Add(new ParamEntry { label = "reson", value = snd.resonance });
                    list.Add(new ParamEntry { label = "detune", value = snd.detune });
                    list.Add(new ParamEntry { label = "stereo", value = snd.stereoSpread });
                    list.Add(new ParamEntry { label = "modDep", value = snd.modDepth });
                    list.Add(new ParamEntry { label = "reverb", value = snd.reverbBias });
                    list.Add(new ParamEntry { label = "filter", value = snd.filterMotion });
                    break;
            }

            return list;
        }

        /// <summary>
        /// Builds a compact multi-line string showing relevant inputs and outputs.
        /// Two entries per line, separated by spaces.
        /// </summary>
        public static string FormatLabel(PatternType type, ShapeProfile sp, SoundProfile snd)
        {
            var sb = new StringBuilder();

            var inputs = GetRelevantInputs(type, sp);
            if (inputs.Count > 0)
            {
                sb.AppendLine("-- Shape --");
                AppendPairs(sb, inputs);
            }

            var outputs = GetRelevantOutputs(type, snd);
            if (outputs.Count > 0)
            {
                sb.AppendLine("-- Sound --");
                AppendPairs(sb, outputs);
            }

            return sb.ToString().TrimEnd();
        }

        private static void AppendPairs(StringBuilder sb, List<ParamEntry> entries)
        {
            for (int i = 0; i < entries.Count; i += 2)
            {
                string a = $"{entries[i].label} {entries[i].value:F2}";
                if (i + 1 < entries.Count)
                {
                    string b = $"{entries[i + 1].label} {entries[i + 1].value:F2}";
                    sb.AppendLine($"{a}  {b}");
                }
                else
                {
                    sb.AppendLine(a);
                }
            }
        }
    }
}
