using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    public abstract class UnifiedShapeDeriverBase : IUnifiedShapeDeriver
    {
        protected readonly IRhythmDeriver _rhythm;
        protected readonly IMelodyDeriver _melody;
        protected readonly IHarmonyDeriver _harmony;

        protected UnifiedShapeDeriverBase(
            IRhythmDeriver rhythm,
            IMelodyDeriver melody,
            IHarmonyDeriver harmony)
        {
            _rhythm = rhythm;
            _melody = melody;
            _harmony = harmony;
        }

        public UnifiedDerivationResult Derive(UnifiedDerivationRequest req)
        {
            if (req.freeMode || req.facetMode == ShapeFacetMode.Free)
            {
                req.facetMode = ShapeFacetMode.Free;
                req.bondStrength = BondStrengthResolver.Resolve(req.shapeProfile, req.shapeProfile3D);
            }

            var sharedRole = new ShapeRole
            {
                index = Mathf.Max(0, req.ensembleRoleIndex),
                count = Mathf.Max(1, req.ensembleRoleCount)
            };

            int bars = req.bars > 0 ? req.bars : ResolveSharedBars(req);
            int totalSteps = req.totalSteps > 0
                ? req.totalSteps
                : Mathf.Max(1, bars) * AppStateFactory.BarSteps;
            bars = Mathf.Max(1, Mathf.CeilToInt(totalSteps / (float)AppStateFactory.BarSteps));
            totalSteps = bars * AppStateFactory.BarSteps;

            var rhythmSound = req.rhythmSoundProfile ?? req.soundProfile;
            var melodySound = req.melodySoundProfile ?? req.soundProfile;
            var harmonySound = req.harmonySoundProfile ?? req.soundProfile;

            HarmonyDerivationResult harmonyResult;
            MelodyDerivationResult melodyResult;
            RhythmDerivationResult rhythmResult;

            using (PatternContextScope.Push(sharedRole, BuildSeedContext(req, 0)))
                harmonyResult = _harmony.Derive(req.curve, req.metrics, req.keyName, req.shapeProfile, harmonySound, req.genre);

            var harmony = BuildHarmonySequence(req, harmonyResult, bars, totalSteps, sharedRole);
            var primaryHarmonyContext = BuildContextFromHarmonyEvent(GetHarmonyEventAtStep(harmony, 0));

            using (PatternContextScope.Push(sharedRole, primaryHarmonyContext))
                melodyResult = _melody.Derive(req.curve, req.metrics, req.keyName, req.shapeProfile, melodySound, req.genre);

            var melody = BuildMelodySequence(req, melodyResult, harmony, totalSteps, bars, sharedRole);

            using (PatternContextScope.Push(sharedRole, primaryHarmonyContext))
                rhythmResult = _rhythm.Derive(req.curve, req.metrics, req.shapeProfile, rhythmSound, req.genre);

            var rhythm = BuildRhythmSequence(req, rhythmResult, melody, totalSteps, bars, sharedRole);

            if (req.shapeProfile3D != null)
                ApplyProfile3DMutations(req.shapeProfile3D, rhythm, melody, harmony);

            var facets = new DerivedShapeSequence
            {
                rhythm = rhythm,
                melody = melody,
                harmony = harmony
            };

            var dominantSequence = BuildDominantSequence(req.dominantType, facets, totalSteps);
            var shape = new MusicalShape
            {
                id = MathUtils.CreateId("shape"),
                profile3D = req.shapeProfile3D,
                soundProfile = req.soundProfile,
                bondStrength = req.bondStrength,
                facetMode = req.facetMode,
                totalSteps = totalSteps,
                roleIndex = sharedRole.index,
                facets = facets,
                keyName = req.keyName,
                bars = bars,
                rhythmPresetId = rhythmResult.presetId,
                melodyPresetId = melodyResult.presetId,
                harmonyPresetId = harmonyResult.presetId,
                rhythmSoundProfile = rhythmSound,
                melodySoundProfile = melodySound,
                harmonySoundProfile = harmonySound
            };

            bool wroteFabric = WriteHarmonyToFabric(req, harmony, sharedRole.index);
            var newContext = BuildContextFromHarmonyEvent(GetHarmonyEventAtStep(harmony, 0));

            return new UnifiedDerivationResult
            {
                shape = shape,
                facets = facets,
                dominantSequence = dominantSequence,
                bars = bars,
                presetId = PickPresetId(req.dominantType, rhythmResult, melodyResult, harmonyResult),
                tags = BuildTags(req, PickTags(req.dominantType, rhythmResult, melodyResult, harmonyResult)),
                summary = BuildSummary(req, PickSummary(req.dominantType, rhythmResult, melodyResult, harmonyResult), bars),
                details = BuildDetails(req, PickDetails(req.dominantType, rhythmResult, melodyResult, harmonyResult)),
                newHarmonicContext = newContext,
                wroteFabricChord = wroteFabric
            };
        }

        private static HarmonicContext BuildSeedContext(UnifiedDerivationRequest req, int localBar)
        {
            var placement = req.barChordProvider?.Invoke(localBar);
            if (placement == null || placement.tones == null || placement.tones.Count == 0)
                return new HarmonicContext();

            return new HarmonicContext
            {
                rootMidi = placement.rootMidi,
                chordTones = new List<int>(placement.tones),
                flavor = placement.flavor ?? "minor"
            };
        }

        private static int ResolveSharedBars(UnifiedDerivationRequest req)
        {
            int rhythmBars = req.metrics.averageSize > 0.30f ? 4 : 2;
            int melodyBars = req.metrics.length > 0.80f ? 4 : 2;
            int harmonyBars;

            if (string.Equals(req.genre?.Id, "newage", System.StringComparison.OrdinalIgnoreCase))
                harmonyBars = req.metrics.length > 0.70f ? 8 : 4;
            else
                harmonyBars = req.metrics.length > 1.20f ? 8 : req.metrics.length > 0.70f ? 4 : 2;

            return Mathf.Max(rhythmBars, melodyBars, harmonyBars);
        }

        private static HarmonySequence BuildHarmonySequence(
            UnifiedDerivationRequest req,
            HarmonyDerivationResult result,
            int bars,
            int totalSteps,
            ShapeRole role)
        {
            var legacy = result.derivedSequence;
            var sequence = new HarmonySequence
            {
                kind = "harmony",
                totalSteps = totalSteps,
                events = new List<HarmonyEvent>()
            };

            for (int bar = 0; bar < bars; bar++)
            {
                var scenePlacement = req.barChordProvider?.Invoke(bar);
                bool useScenePlacement = role.index > 0 &&
                    scenePlacement != null &&
                    scenePlacement.tones != null &&
                    scenePlacement.tones.Count > 0;

                var chord = useScenePlacement
                    ? new List<int>(scenePlacement.tones)
                    : legacy?.chord != null ? new List<int>(legacy.chord) : new List<int>();
                int rootMidi = useScenePlacement ? scenePlacement.rootMidi : legacy?.rootMidi ?? 0;
                string flavor = useScenePlacement ? scenePlacement.flavor : legacy?.flavor ?? "minor";

                if (chord.Count == 0)
                    continue;

                sequence.events.Add(new HarmonyEvent
                {
                    step = bar * AppStateFactory.BarSteps,
                    durationSteps = AppStateFactory.BarSteps,
                    rootMidi = rootMidi,
                    chord = chord,
                    flavor = flavor ?? "minor"
                });
            }

            if (sequence.events.Count == 0 && legacy?.chord != null && legacy.chord.Count > 0)
            {
                sequence.events.Add(new HarmonyEvent
                {
                    step = 0,
                    durationSteps = totalSteps,
                    rootMidi = legacy.rootMidi,
                    chord = new List<int>(legacy.chord),
                    flavor = legacy.flavor ?? "minor"
                });
            }

            SyncHarmonyAliases(sequence);
            return sequence;
        }

        private static MelodySequence BuildMelodySequence(
            UnifiedDerivationRequest req,
            MelodyDerivationResult result,
            HarmonySequence harmony,
            int totalSteps,
            int bars,
            ShapeRole role)
        {
            var legacy = result.derivedSequence;
            var sequence = new MelodySequence
            {
                kind = "melody",
                totalSteps = totalSteps,
                notes = new List<MelodyNote>()
            };

            if (legacy?.notes == null)
                return sequence;

            int sourceTotalSteps = legacy.totalSteps > 0 ? legacy.totalSteps : totalSteps;
            foreach (var note in legacy.notes)
            {
                int step = ScaleStep(note.step, sourceTotalSteps, totalSteps);
                int duration = ScaleDuration(note.durationSteps, sourceTotalSteps, totalSteps);
                var harmonyEvent = GetHarmonyEventAtStep(harmony, step);
                int midi = QuantizeMelodyToHarmony(note.midi, req.keyName, harmonyEvent, role.index > 0 || step % 4 == 0);

                sequence.notes.Add(new MelodyNote
                {
                    step = step,
                    midi = midi,
                    durationSteps = duration,
                    velocity = note.velocity,
                    glide = note.glide
                });
            }

            return sequence;
        }

        private static RhythmSequence BuildRhythmSequence(
            UnifiedDerivationRequest req,
            RhythmDerivationResult result,
            MelodySequence melody,
            int totalSteps,
            int bars,
            ShapeRole role)
        {
            var legacy = result.derivedSequence;
            var sequence = new RhythmSequence
            {
                kind = "rhythm",
                totalSteps = totalSteps,
                swing = legacy?.swing ?? 0f,
                events = new List<RhythmEvent>()
            };

            if (legacy?.events != null)
            {
                int sourceTotalSteps = legacy.totalSteps > 0 ? legacy.totalSteps : totalSteps;
                foreach (var evt in legacy.events)
                {
                    sequence.events.Add(new RhythmEvent
                    {
                        step = ScaleStep(evt.step, sourceTotalSteps, totalSteps),
                        lane = evt.lane,
                        velocity = evt.velocity,
                        microShift = evt.microShift
                    });
                }
            }

            AddMelodyAnchors(sequence, melody, role.index);
            return sequence;
        }

        private static void AddMelodyAnchors(RhythmSequence rhythm, MelodySequence melody, int roleIndex)
        {
            if (rhythm?.events == null || melody?.notes == null)
                return;

            int inserted = 0;
            for (int i = 0; i < melody.notes.Count; i++)
            {
                var note = melody.notes[i];
                if (note == null)
                    continue;
                if (note.step % 4 != 0)
                    continue;
                if (HasRhythmEventAtStep(rhythm.events, note.step))
                    continue;

                rhythm.events.Add(new RhythmEvent
                {
                    step = note.step,
                    lane = roleIndex == 0 && note.step % 8 == 0 ? "kick" : "perc",
                    velocity = roleIndex == 0 ? 0.24f : 0.16f,
                    microShift = 0f
                });

                inserted++;
                if (inserted >= 2)
                    break;
            }
        }

        private static bool HasRhythmEventAtStep(List<RhythmEvent> events, int step)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] != null && events[i].step == step)
                    return true;
            }

            return false;
        }

        private static int QuantizeMelodyToHarmony(int midi, string keyName, HarmonyEvent harmonyEvent, bool snapToChord)
        {
            int quantized = MusicalKeys.QuantizeToKey(midi, keyName);
            if (!snapToChord || harmonyEvent == null || harmonyEvent.chord == null || harmonyEvent.chord.Count == 0)
                return quantized;

            var ctx = new HarmonicContext
            {
                rootMidi = harmonyEvent.rootMidi,
                chordTones = new List<int>(harmonyEvent.chord),
                flavor = harmonyEvent.flavor ?? "minor"
            };
            return ctx.NearestChordTone(quantized);
        }

        private static int ScaleStep(int step, int sourceTotalSteps, int targetTotalSteps)
        {
            if (targetTotalSteps <= 0)
                return 0;
            if (sourceTotalSteps <= 0 || sourceTotalSteps == targetTotalSteps)
                return Mathf.Clamp(step, 0, targetTotalSteps - 1);

            float normalized = Mathf.Clamp01(step / (float)sourceTotalSteps);
            return Mathf.Clamp(Mathf.RoundToInt(normalized * targetTotalSteps), 0, targetTotalSteps - 1);
        }

        private static int ScaleDuration(int duration, int sourceTotalSteps, int targetTotalSteps)
        {
            if (duration <= 0)
                return 1;
            if (sourceTotalSteps <= 0 || sourceTotalSteps == targetTotalSteps)
                return Mathf.Max(1, duration);

            float scaled = duration * (targetTotalSteps / (float)sourceTotalSteps);
            return Mathf.Max(1, Mathf.RoundToInt(scaled));
        }

        private static bool WriteHarmonyToFabric(UnifiedDerivationRequest req, HarmonySequence harmony, int sourceRole)
        {
            if (req.fabric == null || harmony?.events == null || harmony.events.Count == 0)
                return false;

            foreach (var evt in harmony.events)
            {
                if (evt == null || evt.chord == null || evt.chord.Count == 0)
                    continue;

                int localBar = evt.step / AppStateFactory.BarSteps;
                int globalBar = req.progressionBarCount > 0
                    ? (req.progressionBarIndex + localBar) % req.progressionBarCount
                    : req.progressionBarIndex + localBar;
                req.fabric.Write(globalBar, evt.rootMidi, evt.chord, evt.flavor ?? "minor", sourceRole);
            }

            return true;
        }

        private static HarmonyEvent GetHarmonyEventAtStep(HarmonySequence harmony, int step)
        {
            if (harmony?.events == null || harmony.events.Count == 0)
                return null;

            for (int i = 0; i < harmony.events.Count; i++)
            {
                var evt = harmony.events[i];
                if (evt == null)
                    continue;

                int start = evt.step;
                int end = evt.step + Mathf.Max(1, evt.durationSteps);
                if (step >= start && step < end)
                    return evt;
            }

            return harmony.events[0];
        }

        private static HarmonicContext BuildContextFromHarmonyEvent(HarmonyEvent evt)
        {
            if (evt == null || evt.chord == null || evt.chord.Count == 0)
                return new HarmonicContext();

            return new HarmonicContext
            {
                rootMidi = evt.rootMidi,
                chordTones = new List<int>(evt.chord),
                flavor = evt.flavor ?? "minor"
            };
        }

        private static void SyncHarmonyAliases(HarmonySequence harmony)
        {
            if (harmony == null)
                return;

            if (harmony.events != null && harmony.events.Count > 0)
            {
                var primary = harmony.events[0];
                harmony.rootMidi = primary.rootMidi;
                harmony.flavor = primary.flavor ?? "minor";
                harmony.chord = primary.chord != null ? new List<int>(primary.chord) : new List<int>();
            }
            else
            {
                harmony.flavor = harmony.flavor ?? "minor";
                harmony.chord = harmony.chord ?? new List<int>();
            }
        }

        private static DerivedSequence BuildDominantSequence(PatternType type, DerivedShapeSequence facets, int totalSteps)
        {
            switch (type)
            {
                case PatternType.RhythmLoop:
                    return new DerivedSequence
                    {
                        kind = "rhythm",
                        totalSteps = totalSteps,
                        swing = facets.rhythm?.swing ?? 0f,
                        events = facets.rhythm?.events ?? new List<RhythmEvent>()
                    };
                case PatternType.MelodyLine:
                    return new DerivedSequence
                    {
                        kind = "melody",
                        totalSteps = totalSteps,
                        notes = facets.melody?.notes ?? new List<MelodyNote>()
                    };
                default:
                    return new DerivedSequence
                    {
                        kind = "harmony",
                        totalSteps = totalSteps,
                        flavor = facets.harmony?.flavor,
                        rootMidi = facets.harmony?.rootMidi ?? 0,
                        chord = facets.harmony?.chord ?? new List<int>()
                    };
            }
        }

        private static string BuildSummary(UnifiedDerivationRequest req, string dominantSummary, int bars)
        {
            string modeLabel = req.facetMode == ShapeFacetMode.Free
                ? "bonded shape"
                : req.facetMode == ShapeFacetMode.SoloRhythm ? "solo rhythm shape"
                : req.facetMode == ShapeFacetMode.SoloMelody ? "solo melody shape"
                : "solo harmony shape";

            if (string.IsNullOrWhiteSpace(dominantSummary))
                return $"{modeLabel}, {bars} bars.";

            return $"{modeLabel}, {dominantSummary}";
        }

        private static string BuildDetails(UnifiedDerivationRequest req, string dominantDetails)
        {
            if (string.IsNullOrWhiteSpace(dominantDetails))
                return $"Facet mode: {req.facetMode}. Shared loop: {Mathf.Max(1, req.totalSteps / AppStateFactory.BarSteps)} bars.";

            return $"{dominantDetails} Facet mode: {req.facetMode}.";
        }

        private static List<string> BuildTags(UnifiedDerivationRequest req, List<string> dominantTags)
        {
            var tags = dominantTags != null ? new List<string>(dominantTags) : new List<string>();
            tags.Add(req.facetMode == ShapeFacetMode.Free ? "bonded" : req.facetMode.ToString().ToLowerInvariant());
            return tags;
        }

        private static string PickPresetId(
            PatternType t,
            RhythmDerivationResult r, MelodyDerivationResult m, HarmonyDerivationResult h)
        {
            switch (t)
            {
                case PatternType.RhythmLoop: return r.presetId;
                case PatternType.MelodyLine: return m.presetId;
                default:                     return h.presetId;
            }
        }

        private static List<string> PickTags(
            PatternType t,
            RhythmDerivationResult r, MelodyDerivationResult m, HarmonyDerivationResult h)
        {
            switch (t)
            {
                case PatternType.RhythmLoop: return r.tags;
                case PatternType.MelodyLine: return m.tags;
                default:                     return h.tags;
            }
        }

        private static string PickSummary(
            PatternType t,
            RhythmDerivationResult r, MelodyDerivationResult m, HarmonyDerivationResult h)
        {
            switch (t)
            {
                case PatternType.RhythmLoop: return r.summary;
                case PatternType.MelodyLine: return m.summary;
                default:                     return h.summary;
            }
        }

        private static string PickDetails(
            PatternType t,
            RhythmDerivationResult r, MelodyDerivationResult m, HarmonyDerivationResult h)
        {
            switch (t)
            {
                case PatternType.RhythmLoop: return r.details;
                case PatternType.MelodyLine: return m.details;
                default:                     return h.details;
            }
        }

        private static void ApplyProfile3DMutations(
            ShapeProfile3D p3d,
            RhythmSequence rhythm,
            MelodySequence melody,
            HarmonySequence harmony)
        {
            float rhythmAccentGain = 1f + 0.25f * (p3d.tiltMean - 0.5f)
                                        + 0.15f * (p3d.thicknessVariance - 0.3f);
            rhythmAccentGain = Mathf.Clamp(rhythmAccentGain, 0.75f, 1.25f);

            if (rhythm != null)
            {
                rhythm.swing = Mathf.Clamp01(
                    rhythm.swing + 0.1f * (p3d.thicknessVariance - 0.5f));

                float microScale = Mathf.Lerp(1.2f, 0.6f, Mathf.Clamp01(p3d.temporalEvenness));
                if (rhythm.events != null)
                {
                    for (int i = 0; i < rhythm.events.Count; i++)
                    {
                        var e = rhythm.events[i];
                        if (e == null) continue;
                        e.velocity = Mathf.Clamp01(e.velocity * rhythmAccentGain);
                        e.microShift *= microScale;
                    }
                }
            }

            if (melody?.notes != null && melody.notes.Count > 0)
            {
                float melodyGain = Mathf.Clamp(1f + 0.25f * (p3d.elongation3D - 0.3f), 0.8f, 1.25f);
                float helixBias = Mathf.Clamp(p3d.helicity, -1f, 1f);
                float durationScale = Mathf.Lerp(0.9f, 1.2f, Mathf.Clamp01(p3d.elongation3D));

                for (int i = 0; i < melody.notes.Count; i++)
                {
                    var n = melody.notes[i];
                    if (n == null) continue;
                    n.velocity = Mathf.Clamp01(n.velocity * melodyGain);
                    n.glide = Mathf.Clamp(n.glide + helixBias * 0.12f, -1f, 1f);
                    n.durationSteps = Mathf.Max(1, Mathf.RoundToInt(n.durationSteps * durationScale));
                }
            }

            if (harmony?.events != null)
            {
                for (int i = 0; i < harmony.events.Count; i++)
                {
                    var evt = harmony.events[i];
                    if (evt?.chord == null || evt.chord.Count == 0)
                        continue;

                    if (p3d.depthSpan > 0.5f)
                    {
                        int top = evt.chord[evt.chord.Count - 1];
                        int widened = Mathf.Clamp(top + 12, 24, 108);
                        if (widened != top)
                            evt.chord.Add(widened);
                    }
                    if (p3d.centroidDepth < 0.3f)
                    {
                        int rootLow = Mathf.Clamp(evt.rootMidi - 12, 12, 108);
                        if (rootLow != evt.rootMidi && !evt.chord.Contains(rootLow))
                            evt.chord.Insert(0, rootLow);
                    }
                }
            }

            SyncHarmonyAliases(harmony);
        }
    }
}
