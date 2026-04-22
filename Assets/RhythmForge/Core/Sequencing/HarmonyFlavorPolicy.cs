using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    /// <summary>
    /// Per-genre harmony vocabulary: picks the base flavor for each bar from the
    /// shape profile, maps flavors to diatonic scale-degree intervals, and decides
    /// which degrees the bars-4 / bars-8 cadence lift should add.
    /// Keeps the guided Electronic behavior as <see cref="HarmonyFlavorPolicies.Default"/>;
    /// Jazz and NewAge provide their own idiomatic vocabularies.
    /// </summary>
    public interface IHarmonyFlavorPolicy
    {
        string PickBaseFlavor(ShapeProfile shapeProfile);
        int[] GetScaleDegrees(string flavor);
        CadenceLiftSpec PickCadenceLift(string flavor, float symmetry);
    }

    public readonly struct CadenceLiftSpec
    {
        public readonly int[] addDegrees;
        public readonly string newFlavor;

        public CadenceLiftSpec(int[] addDegrees, string newFlavor)
        {
            this.addDegrees = addDegrees;
            this.newFlavor = newFlavor;
        }
    }

    public static class HarmonyFlavorPolicies
    {
        public static readonly IHarmonyFlavorPolicy Default = new DefaultPolicy();
        public static readonly IHarmonyFlavorPolicy Jazz = new JazzPolicy();
        public static readonly IHarmonyFlavorPolicy NewAge = new NewAgePolicy();

        public static IHarmonyFlavorPolicy Get(string genreId)
        {
            switch (genreId)
            {
                case "jazz":   return Jazz;
                case "newage": return NewAge;
                default:       return Default;
            }
        }

        private sealed class DefaultPolicy : IHarmonyFlavorPolicy
        {
            public string PickBaseFlavor(ShapeProfile sp)
            {
                if (sp == null) return "triad";
                if (sp.tiltSigned > 0.28f) return "maj7";
                if (sp.tiltSigned < -0.22f) return "sus2";
                return "triad";
            }

            public int[] GetScaleDegrees(string flavor)
            {
                switch (flavor)
                {
                    case "maj7":
                        return new[] { 0, 2, 4, 6 };
                    case "sus2":
                    case "sus2-lift":
                    case "sus2-cadence-rich":
                        return new[] { 0, 1, 4 };
                    default:
                        return new[] { 0, 2, 4 };
                }
            }

            public CadenceLiftSpec PickCadenceLift(string flavor, float symmetry)
            {
                // Always add the 7 (scale step 6 from root); add the 9 on low-symmetry (busy) shapes.
                int[] add = symmetry < 0.45f ? new[] { 6, 1 } : new[] { 6 };
                string newFlavor = flavor;
                if (flavor == "triad")
                    newFlavor = symmetry < 0.45f ? "triad-cadence-rich" : "triad-lift";
                else if (flavor == "sus2")
                    newFlavor = symmetry < 0.45f ? "sus2-cadence-rich" : "sus2-lift";
                else if (flavor == "maj7" && symmetry < 0.45f)
                    newFlavor = "maj9-lift";
                return new CadenceLiftSpec(add, newFlavor);
            }
        }

        private sealed class JazzPolicy : IHarmonyFlavorPolicy
        {
            public string PickBaseFlavor(ShapeProfile sp)
            {
                if (sp == null) return "min7";
                if (sp.tiltSigned > 0.28f)  return "maj7";
                if (sp.tiltSigned < -0.30f) return "min7b5";
                if (sp.tiltSigned < -0.05f) return "dom7";
                return "min7";
            }

            public int[] GetScaleDegrees(string flavor)
            {
                // Every jazz flavor is a diatonic 7th shape built from the key's scale
                // at the chord's root position; harmonic color comes from where the root
                // sits in the key (ii gives min7b5, V gives dom7, i gives min7 for D minor, etc).
                return new[] { 0, 2, 4, 6 };
            }

            public CadenceLiftSpec PickCadenceLift(string flavor, float symmetry)
            {
                // Tight shapes -> add the 9; open shapes -> add the 13.
                int[] add = symmetry < 0.45f ? new[] { 1 } : new[] { 5 };
                string label = symmetry < 0.45f ? "-9" : "-13";
                return new CadenceLiftSpec(add, flavor + label);
            }
        }

        private sealed class NewAgePolicy : IHarmonyFlavorPolicy
        {
            public string PickBaseFlavor(ShapeProfile sp)
            {
                if (sp == null) return "triad";
                if (sp.tiltSigned > 0.22f)  return "sus2";
                if (sp.tiltSigned < -0.18f) return "sus4";
                if (sp.circularity > 0.7f)  return "drone5";
                return "triad";
            }

            public int[] GetScaleDegrees(string flavor)
            {
                switch (flavor)
                {
                    case "sus2":
                    case "sus2-add9":
                    case "sus2-add11":
                        return new[] { 0, 1, 4 };
                    case "sus4":
                    case "sus4-add9":
                    case "sus4-add11":
                        return new[] { 0, 3, 4 };
                    case "drone5":
                    case "drone5-add9":
                    case "drone5-add11":
                        return new[] { 0, 4 };
                    default:
                        return new[] { 0, 2, 4 };
                }
            }

            public CadenceLiftSpec PickCadenceLift(string flavor, float symmetry)
            {
                // Low symmetry -> add the 9 (breath); high symmetry -> add the 11 (spacious).
                int[] add = symmetry < 0.45f ? new[] { 1 } : new[] { 3 };
                string suffix = symmetry < 0.45f ? "-add9" : "-add11";
                string newFlavor;
                switch (flavor)
                {
                    case "triad":   newFlavor = "add" + (symmetry < 0.45f ? "9" : "11"); break;
                    case "sus2":    newFlavor = "sus2" + suffix; break;
                    case "sus4":    newFlavor = "sus4" + suffix; break;
                    case "drone5":  newFlavor = "drone5" + suffix; break;
                    default:        newFlavor = flavor + suffix; break;
                }
                return new CadenceLiftSpec(add, newFlavor);
            }
        }
    }
}
