# Phased Music Creation — Gap Analysis

Source of truth for this analysis:
- Plan: [phased-music-creation-plan.md](phased-music-creation-plan.md)
- Musical base knowledge: [base-lnowledge-creating-in-phases.md](base-lnowledge-creating-in-phases.md)
- Per-phase handovers: `Phase-A-Handover-plan.md` .. `Phase-J-Handover-plan.md`

Scope: this document audits what actually exists in the RhythmForge VR codebase against (a) the implementation plan and (b) the musical principles in the base-knowledge file. It covers three planes:

1. **Technical / development plane** — code structure, data model, plumbing, tests.
2. **Musical-principles plane** — whether the derivers enforce the "always musical / beginner-lock" invariants described in the plan and in the base-knowledge file.
3. **Vision / UX plane** — whether the guided flow delivers the "one shape per phase, step-by-step composer for non-professional creators" experience.

All file paths are repo-relative. All MIDI numbers use `C4 = 60`, `A4 = 69`, `G4 = 67`.

---

## 1. Executive summary

The core backbone landed. All ten phases (A–J) left a compilable, phase-owned guided flow behind:

- five-type `PatternType` domain with alias compatibility ([PatternType.cs](../../Assets/RhythmForge/Core/Data/PatternType.cs))
- `Composition` / `ChordProgression` / `ChordSlot` / `GrooveProfile` / `GuidedDefaults` data model ([Composition.cs](../../Assets/RhythmForge/Core/Data/Composition.cs), [GuidedDefaults.cs](../../Assets/RhythmForge/Core/Data/GuidedDefaults.cs))
- `PhaseController` + `PhasePanel` + guided-mode UI gating ([PhaseController.cs](../../Assets/RhythmForge/Interaction/PhaseController.cs), [PhasePanel.cs](../../Assets/RhythmForge/UI/Panels/PhasePanel.cs))
- progression-driven guided Harmony, Melody, Groove, Bass, Percussion derivation ([HarmonyDeriver.cs](../../Assets/RhythmForge/Core/Sequencing/HarmonyDeriver.cs), [MelodyDeriver.cs](../../Assets/RhythmForge/Core/Sequencing/MelodyDeriver.cs), [GrooveShapeMapper.cs](../../Assets/RhythmForge/Core/Sequencing/GrooveShapeMapper.cs), [BassDeriver.cs](../../Assets/RhythmForge/Core/Sequencing/BassDeriver.cs), [PercussionDeriver.cs](../../Assets/RhythmForge/Core/Sequencing/PercussionDeriver.cs))
- single-slot phase replacement in `PatternRepository` for all five phases
- `GuidedDemoComposition` as the starter session, with `DemoSession` removed in phase J

The remaining gaps fall into three buckets:

| Bucket | Severity | Summary |
|---|---|---|
| **Musical correctness** | HIGH | Several "beginner-lock hard rules" from the plan and base-knowledge aren't actually enforced in code. Phrase anchors, backbeat placement, default kick pattern, bar-5/6 vs 5–8 lift, Groove accent-curve amplitude, Harmony-triggered sample invalidation, and strong-beat chord-tone guarantee under thinning all diverge quietly. |
| **Technical / plumbing** | MEDIUM | Legacy `DerivedSequence.chord/rootMidi/flavor` and `ShapeRoleProvider` still live because Jazz / NewAge derivers still write them. No `WalkthroughTests`. No `PhaseInvalidationChangedEvent`. Pending badges only cover two of the four real schedule-time dependency chains. |
| **Vision / UX** | MEDIUM | Guided mode works, but the energy-curve / question-answer / arrangement dynamics from base-knowledge §2.6 are never expressed. `Adjust` is a label re-skin, not a distinct workflow. No cross-phase nudges or teaching scaffolding. The duplicate button in Inspector is relabelled "Clear" instead of the plan's `Redraw / Adjust / Clear` mapping intent. |

None of these gaps are regressions from what previous phases promised, but together they mean the product does not yet fully deliver the "always musical, always coherent 8-bar piece" claim the plan makes.

---

## 2. Plane 1 — Technical / development gaps

### 2.1 Legacy harmony fields still live in `DerivedSequence`

**Status:** partially deferred in Phase J.

Claimed in Phase J: guided `HarmonyDeriver.Derive` now writes only `chordEvents`, and the legacy `chord`, `rootMidi`, `flavor` single-chord fields survive only as a fallback for Jazz/NewAge genre derivers.

Actual state ([SequenceData.cs:52-71](../../Assets/RhythmForge/Core/Data/SequenceData.cs#L52-L71)):
```csharp
public class DerivedSequence {
    public string kind;
    public int totalSteps;
    public float swing;
    public List<RhythmEvent> events;
    public List<MelodyNote> notes;
    public GrooveProfile grooveProfile;
    public string flavor;     // still here
    public int rootMidi;      // still here
    public List<int> chord;   // still here
    public List<ChordSlot> chordEvents;
}
```

Plus an unreferenced class `HarmonySequence` also lives in the same file (noted in Phase J handover §"Known Remaining Tech Debt").

Why it matters: every consumer of `DerivedSequence` must branch on `chordEvents != null` first and fall back to the single-chord fields, or risk silent data drift between guided and free genres. Any new deriver or behavior that forgets the first branch produces only the bar-0 chord.

Needed work:
- rewrite `JazzHarmonyDeriver` and `NewAgeHarmonyDeriver` to write `chordEvents`
- update `MusicalCoherenceRegressionTests.LegacyGenreHarmonyRoles_*` and the NewAge-specific `rootMidi` read in `SessionStore_SetGenre_RederivesRolesInPatternOrder_AndPropagatesHarmonyContext`
- remove the legacy fields and `HarmonySequence`
- remove the `else if (chord != null)` fallbacks in `HarmonyBehavior` and `SessionStore`

### 2.2 `ShapeRoleProvider` and role-index branching still exist

**Status:** deferred.

The plan (§6 phase J) said delete `ShapeRoleProvider` and `ShapeRole`. They are still in use across 11 files: all Jazz and NewAge derivers (`JazzHarmonyDeriver`, `JazzMelodyDeriver`, `JazzRhythmDeriver`, `NewAgeHarmonyDeriver`, `NewAgeMelodyDeriver`, `NewAgeRhythmDeriver`), `MelodyDeriver.DeriveLegacy`, `VoiceSpecResolver`, `PatternContextScope`, and `MusicalCoherenceRegressionTests`.

The guided path does not read them, but they are load-bearing for the legacy free-mode path. Deletion requires either:
- removing free-mode entirely, or
- migrating Jazz/NewAge derivers off role-index branching (they depend on "how many patterns of this type exist" to decide primary / counter / fill output)

### 2.3 No walkthrough integration test

**Status:** not implemented.

The plan (§7 "Testability Contract") explicitly calls for `WalkthroughTests.Guided5StepFlow_ProducesFullComposition` to run one canonical stroke per phase and assert the output at each step. Not present in `Assets/RhythmForge/Editor/`. Phase I handover flags this gap explicitly.

This is the one test that would catch the musical-correctness gaps in §3 below. Every individual `*DeriverTests.cs` tests one deriver in isolation with a hand-built progression; nothing exercises the real cross-phase invalidation chain end-to-end.

### 2.4 No automated build/test execution across phases D–J

Phases D, E, F, G, H, I, J all note that their author could not run Unity batch mode, `dotnet`, `msbuild`, or `csc` from the terminal. Phase A and B confirm only a partial focused run. The Phase D handover reports 58/73 edit-mode tests passing on a temp copy, with 15 failures concentrated in `ArrangementSequencerTests`, `PlaybackAnimationTests`, `ProceduralAudioRendererTests`, `ShapeSizeBehaviorTests`, and `HarmonicContextProviderTests`. Those failures were out of phase D scope and never re-audited.

Needed work: run the full edit-mode suite on master, triage the 15 known failures, and decide which are pre-existing vs. introduced by the refactor.

### 2.5 Pending-badge coverage is narrower than the plan

**Status:** intentional-but-limited.

Plan §6 phase I wanted a "pending re-derive" badge on downstream phases whenever an upstream changes. The actual implementation ([SessionStore.cs pending tracking](../../Assets/RhythmForge/Core/Session/SessionStore.cs)) marks only Melody and Bass as pending, and only on `ChordProgressionChangedEvent`.

Schedule-time dependencies that are NOT reflected in pending state:
- Groove → Melody (timing/accents change at schedule time)
- Groove → Percussion (swing is applied at schedule time)
- Melody → Groove warm-up (Groove becomes audible only once Melody exists — surfaced as a toast, not a badge)

Consequence: a user who redraws Groove after committing Melody hears a different groove on next playback, but the phase UI gives no hint that the stored Melody sequence is now being scheduled differently.

### 2.6 No dedicated invalidation event

The Phase I handover (§"Not implemented") flags that no `PhaseInvalidationChangedEvent` was introduced. The panel refreshes via `SessionStateChangedEvent`, which is broad and fires on many unrelated mutations. Future UI work that needs finer-grained dependency propagation (e.g. a "changes pending since last play" indicator) would need either the event or polling from each consumer.

### 2.7 Groove has no dedicated visual grammar

**Status:** intentional deferral in Phase F.

`GrooveBehavior.AdjustVisualSpec` and `.ComputeAnimation` both delegate to `MelodyBehavior` ([GrooveBehavior.cs:94-107](../../Assets/RhythmForge/Core/PatternBehavior/Behaviors/GrooveBehavior.cs#L94-L107)). Groove strokes therefore look like melody strokes in-scene, which is confusing because Groove outputs no pitches. The phase is owned; its visual identity is not.

### 2.8 Bass has no dedicated visual grammar or audio-dispatcher path

**Status:** intentional deferral in Phase G.

`BassBehavior.AdjustVisualSpec` uses the Melody visual grammar profile ([BassBehavior.cs:99](../../Assets/RhythmForge/Core/PatternBehavior/Behaviors/BassBehavior.cs#L99)), and Bass playback goes through `audioDispatcher.PlayMelody` with a bass-preset tag rather than a dedicated `PlayBass`. This is fine musically but limits how Bass can diverge later (e.g. sidechain ducking, mono-ing, dedicated reverb bus).

### 2.9 Rhythm compatibility wrappers still exist

`RhythmLoopBehavior` ([RhythmLoopBehavior.cs](../../Assets/RhythmForge/Core/PatternBehavior/Behaviors/RhythmLoopBehavior.cs)) and `RhythmDeriver` ([RhythmDeriver.cs](../../Assets/RhythmForge/Core/Sequencing/RhythmDeriver.cs)) are still present as "thin legacy wrappers forwarding to Percussion*". Also, visual grammar assets were not renamed from `RhythmLoop → Percussion`. These are compatibility shims, not active code, but they add noise.

### 2.10 `ArrangementPanel` and `SceneStripPanel` are hidden, not removed

Phase J confirms these are gated via `ApplyGuidedModeUiState` ([RhythmForgeManager.cs:450-459](../../Assets/RhythmForge/RhythmForgeManager.cs#L450-L459)) but not deleted. That's intentional ("free mode remains addressable") — flagged here only so the next agent does not mistake hidden panels for removed ones.

### 2.11 Minor: `StateMigrator` version number

Current `AppState.version = 7`. Any save older than 7 is auto-upgraded to `guidedMode = true`. That heuristic may be wrong for users who had previously opted out of guided mode on a test build.

---

## 3. Plane 2 — Musical-principles gaps

These are the highest-value gaps: the plan promises that every derivation enforces "hard rules" (chord-tone snaps, phrase anchors, backbeat placement) and the base-knowledge file encodes these as non-negotiable beginner floors. Several are not actually in code.

### 3.1 Default kick pattern no longer matches "beats 1 and 3"

**Base knowledge** §Step 6 and plan §2.5: the beginner-safe percussion default is
- Kick on beats 1 and 3 (steps 0 and 8 in a 16-step bar)
- Snare on beats 2 and 4 (steps 4 and 12)
- Closed hat on 8ths

**Code** [PercussionDeriver.cs:36-44](../../Assets/RhythmForge/Core/Sequencing/PercussionDeriver.cs#L36-L44):
```csharp
if (sp.aspectRatio < 0.52f)
    kickPattern = new[] { 0, 6, 10, 13 };       // plan: pickup-hits variant — OK
else if (sp.circularity > 0.75f)
    kickPattern = new[] { 0, 8, 12 };           // plan: rounded variant — OK
else
    kickPattern = new[] { 0, 7, 10, 13 };       // DEFAULT DIVERGES — plan says { 0, 8 }
```
**The default kick is not the plan's default.** Almost every beginner-shape stroke lands in the `else` branch, which means the typical guided output is already an enriched kick pattern instead of the simplest, safest one. A user drawing a plain circle probably hits the circularity branch and gets `{0, 8, 12}`; a user drawing a rectangle gets the aspect branch; anyone else lands on `{0, 7, 10, 13}`.

### 3.2 Snare backbeat is replaced by a non-backbeat default

**Code** [PercussionDeriver.cs:44](../../Assets/RhythmForge/Core/Sequencing/PercussionDeriver.cs#L44):
```csharp
int[] snarePattern = sp.symmetry > 0.6f ? new[] { 8 } : new[] { 5, 8, 13 };
```

Neither option is the plan's / base-knowledge's backbeat (`{4, 12}`). `{8}` is a snare on beat 3 (which musically competes with a kick also on beat 3). `{5, 8, 13}` is ghost-hit-and-beat-3 style.

The safety net `EnsureAnchorEvents` ([PercussionDeriver.cs:185](../../Assets/RhythmForge/Core/Sequencing/PercussionDeriver.cs#L185)) only ensures `step 0 = kick, step 8 = snare`. That's the plan's "beginner safety net" bullet — but the plan also says the *baseline* should be kick on 1+3 and snare on 2+4. Together, the output is: kick on beats 1 and 3, snare on beat 3 (not 2+4). This is a functional regression from the "always musical" promise for listeners who expect a pop/rock backbeat.

**Note** the plan itself is internally inconsistent here: §2.5 says "snare on 2 and 4" but the safety-net bullet says "snare on step 8" (which is beat 3). The code follows the safety-net bullet. The next agent should decide which interpretation is canonical and fix both plan and code.

### 3.3 Melody phrase anchors are not guaranteed at derivation time

**Plan** §2.4: "Hard rule: at least 1 note must land on bar-1 beat-1 and bar-5 beat-1 regardless of shape (phrase anchors)."

**Code:** `MelodyGrooveApplier.IsPhraseAnchor` ([MelodyGrooveApplier.cs:222-229](../../Assets/RhythmForge/Core/Sequencing/MelodyGrooveApplier.cs#L222-L229)) protects step 0 and step 64 from thinning *at schedule time*. The deriver itself ([MelodyDeriver.cs guided path](../../Assets/RhythmForge/Core/Sequencing/MelodyDeriver.cs)) resamples the stroke and rounds to a grid. It does not explicitly insert a note at step 0 or step 64 if the stroke happens to skip those steps. If derivation produces no note at step 64, the applier has nothing to protect.

The test `MelodyDeriverTests` does not assert this invariant.

### 3.4 Strong-beat chord-tone lock can be lost when Groove re-quantizes

The guided `MelodyDeriver` snaps strong-beat notes (`step % 4 == 0`) to the nearest chord tone. But `MelodyGrooveApplier.Apply` quantizes notes to the groove grid ([MelodyGrooveApplier.cs:60](../../Assets/RhythmForge/Core/Sequencing/MelodyGrooveApplier.cs#L60)), applies syncopation offsets, and recomputes `effectiveStep`. A note originally placed on a strong beat with a chord-tone pitch can end up at a non-strong step via `QuantizeStep`, and the accent curve is keyed to `effectiveStep % 4` rather than the original step. This does not move pitch (Phase F invariant), but it means the "strong beats are chord tones" audible guarantee becomes "strong beats, unless groove moved them" — weaker than the plan claims.

### 3.5 Bar 5–6 lift vs. bars 5–8 lift

**Plan** §2.3 rule 4: "bars 5–8 an 'answer' — the shape modulator can optionally lift the melody an interval higher in bars 5–8".

**Plan** §6 phase E: "lift: if `sp.tiltSigned > 0` apply a +2 scale-step transposition to bars 5–6".

**Code** [MelodyDeriver.cs:85](../../Assets/RhythmForge/Core/Sequencing/MelodyDeriver.cs#L85):
```csharp
if (liftAnswerPhrase && (barIndex == 4 || barIndex == 5))
    midi = TransposeByScaleDegrees(midi, keyName, 2);
```

The code lifts only bars 5 and 6 (0-indexed bars 4 and 5). The Phase E handover documents this deviation as intentional ("keeps bars 7 and 8 more stable for cadence resolution"). Whether this is the right tradeoff is a product call — the side effect is that the "answer" phrase ends on a non-lifted bar, which softens the question-answer contrast the base-knowledge §5.1 is aiming for.

### 3.6 Groove accent-curve amplitude is ignored

**Plan** §2.4: accent curve = `[1.0, 0.7, 0.85, 0.7]`, and `sp.verticalSpan → accent curve amplitude`.

**Code** [GrooveShapeMapper.cs — emit the mapping](../../Assets/RhythmForge/Core/Sequencing/GrooveShapeMapper.cs): the mapper writes the default accent curve into `GrooveProfile.accentCurve` but does not scale it by `verticalSpan`. `MelodyGrooveApplier` multiplies velocities by whatever `accentCurve[step % 4]` is stored, so the amplitude is effectively constant across all Groove strokes.

Consequence: the Groove phase is less expressive than the plan implies. Tall vs. flat strokes should carry obvious accent contrast; today they don't.

### 3.7 Groove density-thinning can remove notes that the Melody deriver treated as strong beats

`MelodyGrooveApplier.Apply` thins notes by `stride = ResolveStride(density)` (drops every Nth note). The anchor protection only guards steps 0 and 64. A strong-beat, chord-tone-snapped note on, say, bar 3 beat 1 can be thinned out at sparse density, violating the base-knowledge Step 3 rule "On strong beats (1 and 3), hit notes that belong to the current chord."

The plan's phrasing was "Skip notes where `noteIndex % roundedStride != 0` based on `density`", which is what the code does — but the plan also said "strong beats favor chord tones" as a hard rule. These two instructions are in tension; the implementation chose thinning over strong-beat preservation.

### 3.8 Harmony modulator flavor — `sus2` truth vs. naming

**Plan** §6 phase D: negative-tilt → `sus2` via `{0, 3, 4, 6}`.

**Code** [HarmonyShapeModulator.cs:12](../../Assets/RhythmForge/Core/Sequencing/HarmonyShapeModulator.cs#L12):
```csharp
private static readonly int[] Sus2Degrees = { 0, 1, 4 };  // root, 2nd, 5th
```

The Phase D handover flags this as intentional, explaining the plan's `{0, 3, 4, 6}` "does not actually form a sus2." The code's `{0, 1, 4}` is the correct sus2 from scale degrees. This is an improvement over the plan, not a gap — mentioned here for completeness.

### 3.9 The Electronic-only scope is baked into the music logic

Every guided deriver passes `"electronic"` explicitly (e.g. `RegisterPolicy.ClampBass(rootMidi, "electronic")`). That matches §9 tradeoff 4 ("Genre stays Electronic in v1"). The gap: there is no `GuidedPolicy` / `GuidedGenreResolver` layer, so if the product ever opens guided mode to Jazz or NewAge, every deriver must be revisited in lockstep. A small policy object (genre id, key, bars, tempo, register ranges) would let the next agent re-thread this in one place.

### 3.10 `Composition.groove.swing` affects Percussion, but not equally everywhere

Phase H wired `PercussionBehavior.Schedule` to consume `groove.swing` as a positive micro-delay on off-beats. It is not applied to `EnsureAnchorEvents` after-the-fact micro-shift, and it is not negatively applied to pulled-early hits (noted in Phase H handover as "only positive delay is currently honored"). A user who expects a "pushed" feel from a Groove stroke with curvature variance will only ever hear the "delayed" side of swing.

### 3.11 Arrangement energy curve (base-knowledge §5, §2.6) is unimplemented

**Base knowledge** Step 5 ("shape energy and texture across 8 bars") defines a curve: bars 1–2 minimal layers, bars 3–4 add rhythm guitar/piano + small drum fill, bars 5–6 reach peak density, bars 7–8 fill into loop.

**Code:** the only two artifacts that implement any bar-level dynamics are:
- Percussion fills on bars 4 and 8 ([PercussionDeriver.cs:143-170](../../Assets/RhythmForge/Core/Sequencing/PercussionDeriver.cs#L143-L170)) — this is partial coverage of the bar-4/8 pickup.
- Melody lift for bars 5–6 when tilt is positive ([MelodyDeriver.cs:85](../../Assets/RhythmForge/Core/Sequencing/MelodyDeriver.cs#L85)).

Everything else — adding or muting layers over the 8 bars, Bass becoming walking eighths in bars 5–8, Groove increasing density into the second half, or any concept of a "question/answer" block beyond melody lift — is absent. The base-knowledge example piece is explicitly an 8-bar loop that evolves; the guided output is an 8-bar loop that repeats.

### 3.12 Base-knowledge §2.6 shaker / tambourine / open-hat / crash / toms

**Plan** §2.6: "Shaker/tambourine tracks are not required and are out of scope until a later genre pass."

This is documented, but worth flagging because it's also the tool the base-knowledge file uses to deliver the "high-energy bars 5–8" section. Without shaker/tambourine/open-hat automation, the arrangement curve (§3.11) cannot be expressed no matter how cleanly the existing phases evolve.

### 3.13 Rhythm-section coupling (base-knowledge Step 6 / §5 "kick lines up with bass on beat 1")

Base knowledge: "Kick on beat 1 often lines up with the bass's root note, making the start of each bar feel solid." The code enforces both kick-on-step-0 and bass-root-on-beat-1 independently, so they do line up — but there is no explicit test or invariant that will catch a future drift. Suggest adding a walkthrough assertion: "on every bar, if bass has a note on step 0 and percussion has a kick on step 0, they are simultaneous."

---

## 4. Plane 3 — Vision, principles, and UX gaps

### 4.1 "One shape per phase per piece" — mostly true, with one leak

Plan principle 1. The `PatternRepository.ShouldReplaceGuidedPhasePattern` path covers all five phases. The only documented leak (Phase D handover §"Known Gaps") is `Save+Dup` in free mode. Phase I replaced `Save+Dup` with an `Auto Next` toggle in guided mode, so the leak is closed in the main guided flow. This is now a non-issue unless the user flips into free mode.

### 4.2 "Order is advisory, navigation is free" — works, but feedback is weak

Plan principle 4. `PhaseController.GoToPhase` lets the user jump freely. The pending-badge limitation (§2.5) means the user gets no signal that a Groove redraw will re-flavor a Melody they drew two minutes ago. That's the exact scenario the pending badge was designed for. Compare the plan's §6 phase I: "Downstream phase invalidation: changing upstream phase marks downstream phase buttons with a 'pending re-derive' badge" — partially delivered.

### 4.3 "Always musical" — partially delivered (see §3)

Several hard rules in §3 are not actually enforced. The user can still make something that sounds OK because the derivers are biased toward safe output, but the product's "always musical" promise is a superset of what the code guarantees.

### 4.4 `Adjust` in the inspector is a label swap, not a workflow

Phase I intent: `Redraw / Adjust / Clear` become distinct guided actions.

Actual behavior ([InspectorPanel.cs:316-324](../../Assets/RhythmForge/UI/Panels/InspectorPanel.cs#L316-L324)): the three bottom-row buttons are relabeled to "Redraw", "Adjust", "Clear". But the Adjust button invokes `OnRemove` which, for guided mode, just re-selects the instance ([InspectorPanel.cs:268-272](../../Assets/RhythmForge/UI/Panels/InspectorPanel.cs#L268-L272)). It opens no new adjustment UI. The Phase I handover documents this as intentional ("Adjust is intentionally lightweight") but the user-facing contract of the plan ("Adjust: stays in selection, tweak pan/brightness/gain") implies at least a visible affordance that mix controls are now live. The current behavior is silent — a user who taps Adjust sees nothing change.

### 4.5 Button mapping divergence

Phase I intent: in guided mode, `mute = Redraw, remove = Adjust, duplicate = Clear`. Code matches labels (`mute → Redraw`, `remove → Adjust`, `duplicate → Clear`). But the duplicate button's guided handler actually calls `ClearPhase` ([InspectorPanel.cs:281-285](../../Assets/RhythmForge/UI/Panels/InspectorPanel.cs#L281-L285)) — so "Clear" works. The mute button calls `OnMute` which then does `Redraw` semantics via `_store.ClearPhase(phase)` ([InspectorPanel.cs:254](../../Assets/RhythmForge/UI/Panels/InspectorPanel.cs#L254)) and then sets the current phase. This works but is fragile: a future refactor that splits mute/remove/duplicate handlers cleanly will break the guided relabeling unless someone remembers the swap.

### 4.6 Missing "teach the beginner" UX scaffolding

Base-knowledge Step 3: "Notice: most moves are by step … important notes mostly belong to the chord, which makes the tune feel 'inside' the harmony." The plan §1 calls the product a "step-by-step composer for non-professional music creators." Neither the PhasePanel nor the CommitCardPanel currently teaches *why* a phase matters or what a good drawing looks like. Compare this to the base-knowledge file itself, which literally walks the user through each step with examples.

Gap: no in-phase hints, no example shapes, no "draw a tall arc to make the melody climb" affordance. This is explicitly out of scope per the plan, but it is a vision-level gap for the "guides non-professionals" claim.

### 4.7 Cross-phase feedback after Harmony redraw is silent

Redrawing Harmony publishes `ChordProgressionChangedEvent`, `SessionStore` kicks off background Melody/Bass re-derivation, marks those phases pending, clears the badge when done. The user sees the badge briefly. But they do not hear the re-derived Melody until the next playback cycle, and the toast system is only used for Groove ("add Melody to hear groove effect"). A Harmony redraw that silently rewrites an already-committed Melody is exactly the cross-phase surprise the plan §1 principle 4 was designed to flag to the user.

### 4.8 `Play Piece` is just a transport toggle

Phase I's "Play Piece" button calls `_sequencer.TogglePlayback()` ([PhasePanel.cs:61](../../Assets/RhythmForge/UI/Panels/PhasePanel.cs#L61)). It does not force the sequencer to start from bar 1 or reset the playhead. A user who wants to hear the full 8 bars can get a partial loop starting mid-phrase. This is a small UX gap but maps directly to the "Play Piece" metaphor.

### 4.9 `TransportPanel` "Phase Locked" is correct, but the key/tempo display never changes

Phase C locked the mode cycle in guided mode. BPM and key read from `Composition`. Good. What is missing: there is no affordance for the user to learn *why* they are locked to G major / 100 bpm / 8 bars / 4/4. The plan §2 is explicit that v1 locks these, but the UI does not communicate it. A guided-mode user who did want to change tempo will find no button at all — no tooltip, no help text.

### 4.10 No `Composition` metadata / labeling

`Composition.id` exists but nothing in the UI names a composition. A user cannot tell "I'm in composition A" vs "I just loaded composition B" — the fresh-session path replaces content silently. For a multi-composition future (§9 tradeoff 1), this is a prerequisite to add.

### 4.11 Guided demo is an empty session, which fights discoverability

`GuidedDemoComposition.CreateDemoState` intentionally leaves all phases empty (Phase I). The Phase I handover documents this as "no pre-drawn patterns … the user draws from scratch". That is faithful to the plan (§6 phase I) and the "one shape per phase" principle. The counter-principle from base-knowledge — "Step 0: choose basic feel" — means a first-time user lands in a silent scene with five unfilled phase buttons and no audible context. Compare with how `DemoSession` *used* to seed three patterns. Suggestion for a future phase: seed a tiny "listen first" loop (chords only) so the user can hear the defaults before drawing.

---

## 5. Severity-ranked backlog

Order by impact on the "always musical, always coherent" promise.

### HIGH — fix before release

| ID | Gap | Plane |
|---|---|---|
| H1 | §3.1 Default kick pattern diverges from plan/base-knowledge | Musical |
| H2 | §3.2 Snare backbeat (beats 2+4) is never played in the default shape | Musical |
| H3 | §3.3 Melody phrase anchors not guaranteed at derivation time | Musical |
| H4 | §3.7 Groove density-thinning can remove strong-beat, chord-tone notes | Musical |
| H5 | §2.3 + §4.2 No walkthrough integration test → these bugs can regress invisibly | Technical |

### MEDIUM — address before product scaling

| ID | Gap | Plane |
|---|---|---|
| M1 | §3.4 Strong-beat chord-tone lock can be lost after Groove re-quantization | Musical |
| M2 | §3.6 Groove accent-curve amplitude ignored | Musical |
| M3 | §3.11 Arrangement energy curve (base-knowledge §2.6) unimplemented | Musical / Vision |
| M4 | §2.4 15 pre-existing edit-mode failures not triaged | Technical |
| M5 | §4.4 `Adjust` is label-only, no workflow | UX |
| M6 | §4.7 Harmony redraw silently rewrites downstream phases | UX |
| M7 | §2.5 Pending-badge coverage narrower than plan | Technical/UX |

### LOW — cleanup / future-proofing

| ID | Gap | Plane |
|---|---|---|
| L1 | §2.1 Legacy `DerivedSequence` fields + `HarmonySequence` | Technical |
| L2 | §2.2 `ShapeRoleProvider` + free-mode legacy derivers | Technical |
| L3 | §2.7 Groove visual grammar duplicates Melody | Technical |
| L4 | §2.8 Bass visual grammar duplicates Melody; no `PlayBass` | Technical |
| L5 | §2.9 `RhythmLoopBehavior` / `RhythmDeriver` compatibility wrappers | Technical |
| L6 | §3.5 Bar 5–6 vs 5–8 lift — decide intent, sync code and plan | Musical |
| L7 | §3.9 Electronic-only assumption baked into every deriver; no `GuidedPolicy` | Technical |
| L8 | §3.10 Groove swing applied only as positive delay on percussion | Musical |
| L9 | §4.6 No beginner-facing teaching scaffolding | Vision |
| L10 | §4.8 Play Piece does not reset to bar 1 | UX |
| L11 | §4.9 No tooltip / help text for guided-mode locks | UX |
| L12 | §4.10 Composition labeling / metadata UI | Vision |
| L13 | §4.11 Empty guided demo lacks "listen first" affordance | Vision |
| L14 | §3.12 Shaker/tambourine/open-hat/crash not supported | Musical (deferred) |

---

## 6. Phased remediation plan

The remediation plan is organized by severity and dependency, then decomposed into sub-phases. Each sub-phase must leave a compilable build, passing tests, and a playable composition (the same contract as the original plan §7).

### Phase K — Musical-correctness floors (HIGH)

Goal: make the plan's "hard rules" actually hold in code. No new UI. No new data model.

Depends on: current master.

#### K.1 Baseline drum pattern

Scope: restore the base-knowledge backbeat as the default and keep the shape-driven enrichments as *alternatives*, not overrides.

Files: [PercussionDeriver.cs](../../Assets/RhythmForge/Core/Sequencing/PercussionDeriver.cs)

Changes:
- Rewrite the default kick pattern to `{0, 8}` (beats 1 and 3).
- Rewrite the default snare pattern to `{4, 12}` (beats 2 and 4).
- Keep the shape-driven *additions* (aspect ratio → pickup hits, circularity → rounded variant, symmetry → ghost hits) as additive enrichments layered on top of the default, not replacements.
- Update `EnsureAnchorEvents` to ensure `step 0 = kick` and `step 4 = snare` (or `step 12`), whichever matches the final decision. This is a doc-level inconsistency in the plan; pick one and update both code and plan §2.5.

Tests:
- `PercussionDeriverTests.DefaultKick_IsOnBeats1And3`
- `PercussionDeriverTests.DefaultSnare_IsOnBeats2And4`
- `PercussionDeriverTests.AspectRatioShape_AddsPickupHits_WithoutRemovingBaseKicks`

Acceptance: draws with neutral aspect/circularity/symmetry produce exactly the base-knowledge pattern.

#### K.2 Melody phrase anchor guarantee

Scope: force a note at step 0 and step 64 in the guided `MelodyDeriver` output, regardless of stroke.

Files: [MelodyDeriver.cs](../../Assets/RhythmForge/Core/Sequencing/MelodyDeriver.cs)

Changes:
- After resampling, if no note exists at step 0, synthesize one using the current bar-0 chord tone.
- If the sequence has ≥ 5 bars and no note exists at step 64, synthesize one using the bar-4 chord tone.
- Keep these anchor notes in the duration range the plan specifies (≥ 4 steps).

Tests:
- `MelodyDeriverTests.AnchorNote_AtStep0_AlwaysPresent`
- `MelodyDeriverTests.AnchorNote_AtStep64_AlwaysPresent_WhenBarsGreaterThan4`
- `MelodyDeriverTests.AnchorNotes_AreChordTonesOfBar0AndBar4`

Acceptance: any stroke, including degenerate input, produces an 8-bar melody that starts on bar 1 beat 1 and re-starts on bar 5 beat 1 with chord-safe pitches.

#### K.3 Groove thinning preserves strong-beat chord tones

Scope: `MelodyGrooveApplier.Apply` must protect all strong-beat, chord-tone notes, not only step 0 and step 64.

Files: [MelodyGrooveApplier.cs](../../Assets/RhythmForge/Core/Sequencing/MelodyGrooveApplier.cs)

Changes:
- Extend `IsPhraseAnchor` (or introduce `IsStrongBeatChordTone`) that returns true when a note's `step % 4 == 0` AND the pitch matches the chord-tones of that bar's chord. Skip thinning for those notes.
- Keep the anchor logic for step 0 and step 64 intact.
- Pass the `ChordProgression` to the applier so the chord-tone lookup is bar-accurate.

Tests:
- `MelodyGrooveApplierTests.StrongBeatChordTones_Survive_SparseDensity`
- `MelodyGrooveApplierTests.PassingBeats_StillThinnedAsExpected`

Acceptance: even at `density = 0.5`, every bar's beat-1 and beat-3 notes survive if the deriver placed chord tones there.

#### K.4 Groove syncopation does not break strong-beat chord-tone lock

Scope: `MelodyGrooveApplier.Apply` syncopation must not shift strong-beat notes off beat 1 or 3. The code already keeps syncopation for off-beats only (`beatOffset == 0` early-exits), which protects step 0, 4, 8, 12 of each bar. Verify this and add a test to lock it down.

Tests:
- `MelodyGrooveApplierTests.StrongBeatNotes_AreNotShiftedBySyncopation`

#### K.5 Rhythm-section coupling invariant

Tests:
- `WalkthroughTests.GuidedComposition_KickAndBassRoot_AlignOnBar1Beat1_AndBar5Beat1`

No code change — just an assertion that catches future drift.

### Phase L — Arrangement expressiveness (MEDIUM)

Goal: express the base-knowledge §2.6 energy curve without adding new instruments. Layer density and accent contrast are the levers.

Depends on: K (to avoid layering on a still-uncorrected backbeat).

#### L.1 Groove accent-curve amplitude

Scope: scale the accent curve amplitude by `ShapeProfile.verticalSpan`, per plan §2.4.

Files: [GrooveShapeMapper.cs](../../Assets/RhythmForge/Core/Sequencing/GrooveShapeMapper.cs)

Changes:
- Given base curve `[1.0, 0.7, 0.85, 0.7]`, compute `amp = 0.6 + verticalSpan * 0.8` (or similar), and scale the *deviation from 1.0* by `amp`. Tall shapes → more contrast (`[1.0, 0.5, 0.77, 0.5]`), flat shapes → less contrast (`[1.0, 0.85, 0.92, 0.85]`).

Tests:
- `GrooveShapeMapperTests.AccentAmplitude_ScalesWithVerticalSpan`

#### L.2 Question / Answer contrast (bars 1–4 vs 5–8)

Scope: deliver the audible "answer phrase" promise.

Options (pick one after a product conversation):

- **L.2a — extend the Melody lift** from bars 5–6 to bars 5–8 when tilt is strongly positive, and add a +1 scale-degree lift in bars 5–8 when tilt is neutral. Trade-off: louder cadence divergence, harder to keep bar 8 landing on the tonic.
- **L.2b — add per-bar layer density in Bass** where bars 5–6 always play root+5 and bars 7–8 walk (independent of shape). Trade-off: Bass no longer feels fully shape-driven.
- **L.2c — add a Percussion mid-point fill** at bar 4 pickup (steps 13–15 already exist) plus a ride-cymbal-like reinforcement pattern on bars 5–8 (uses `perc` lane). Trade-off: requires new `perc` lane semantics.

Suggest L.2a as the minimal-surface-area option that matches the plan's Phase E intent.

#### L.3 Harmony per-bar dynamics

Scope: harmony voicing is currently uniform across all 8 bars (shared inversion, shared flavor, shared register). The base-knowledge example expects a slight cadence lift on bars 4 and 8, which the code does via `symmetry < 0.45` → add the 7th. Extend this so the cadence lift is always applied on bars 4 and 8 regardless of symmetry, and `symmetry < 0.45` makes the lift stronger (not its only trigger).

Files: [HarmonyShapeModulator.cs](../../Assets/RhythmForge/Core/Sequencing/HarmonyShapeModulator.cs)

Changes:
- Make `cadenceLift` always true for bars 3 and 7 (0-indexed).
- Use `shapeProfile.symmetry` to select *how* the lift is expressed (add 7th vs. add 9th vs. voicing spread).

Tests:
- `HarmonyShapeModulatorTests.Bars4And8_AlwaysIncludeCadenceLift`

### Phase M — Cross-phase UX (MEDIUM)

Goal: deliver the plan §6 phase I promise that upstream changes are visible in downstream phase state.

Depends on: K.

#### M.1 `Adjust` workflow

Files: [InspectorPanel.cs](../../Assets/RhythmForge/UI/Panels/InspectorPanel.cs), `RhythmForgeManager`

Changes:
- When `Adjust` is tapped in guided mode, keep the instance selected and either (a) toggle an "adjust mode" badge on the phase panel, or (b) open a reduced mix-only overlay (pan/brightness/gain readouts become editable spinners).
- The current silent behavior is the gap — pick the simplest visible affordance.

#### M.2 Schedule-time pending badges

Files: [SessionStore.cs](../../Assets/RhythmForge/Core/Session/SessionStore.cs), `PhasePanel`

Changes:
- Mark Melody and Percussion as "schedule-pending" when Groove commits (playback will sound different next time).
- Distinguish "async re-derive pending" from "schedule dirty" with two badge colors, or fold into one "stale" badge.

#### M.3 Play Piece reset to bar 1

Files: `PhasePanel`, `Sequencer`

Changes:
- When `Play Piece` starts playback, reset playhead to step 0.
- When it stops, leave the playhead where the user can resume or restart cleanly.

#### M.4 Post-Harmony-redraw toast

Files: `RhythmForgeManager`

Changes:
- After `ChordProgressionChangedEvent`, if Melody and/or Bass exist, show a toast like "Harmony updated. Melody and Bass re-tuned — press Play to listen."

### Phase N — Integration testing (HIGH, once K/L exist)

#### N.1 `WalkthroughTests.Guided5StepFlow_ProducesFullComposition`

Files: new `Assets/RhythmForge/Editor/WalkthroughTests.cs`

Scope: simulate one canonical stroke per phase, call through `SessionStore.CommitDraft`, and assert the post-condition matrix the plan §7 specifies:

- Harmony → 8 `ChordSlot`s, all pitches in G major, roots = I–vi–IV–V loop.
- Melody → ≥ 8 notes, strong beats are chord tones, step 0 and step 64 present, bar 8 final note ≥ 8 steps long, all pitches in G major.
- Groove → `Composition.groove != null`, density in [0.5, 1.5].
- Bass → ≥ 8 events, beat 1 of each bar matches progression root, all pitches in electronic bass register and in G major (except allowed chromatic lead on bar 4 / bar 8 final eighth).
- Percussion → step 0 has a kick, step 4 or step 12 has a snare (per K.1 decision), step 8 has a snare, fills on bars 4 and 8.
- Invariants: kick on step 0 aligns with bass on step 0 in every bar.

#### N.2 Cross-phase invalidation test

Scope: commit all five phases, redraw Harmony, assert progression updates propagate to Melody and Bass without losing chord-tone locks.

#### N.3 Guided-mode UI snapshot test

Scope: assert that with `guidedMode = true`, `PhasePanel.activeInHierarchy == true`, `SceneStripPanel.activeInHierarchy == false`, `ArrangementPanel.activeInHierarchy == false`.

### Phase O — Legacy / free-mode cleanup (LOW)

Depends on: N (the walkthrough test must be green first).

#### O.1 Migrate Jazz / NewAge derivers to `chordEvents`

Files: `JazzHarmonyDeriver.cs`, `NewAgeHarmonyDeriver.cs`

Changes:
- Emit `chordEvents` alongside the single-chord fields (transitional), then remove the single-chord writes once regression tests read `chordEvents`.

#### O.2 Remove `DerivedSequence.chord / rootMidi / flavor` and `HarmonySequence`

Files: `SequenceData.cs`, `HarmonyBehavior.cs`, `SessionStore.cs`, `MusicalCoherenceRegressionTests.cs`

Changes: remove fields, fallback reads, and tests that read them.

#### O.3 Remove `ShapeRoleProvider` / `ShapeRole`

Only after all six free-mode derivers (Jazz + NewAge, three each) stop using role-index branching. Depending on product direction, this may also mean deleting free-mode derivers entirely.

#### O.4 Remove `RhythmLoopBehavior` / `RhythmDeriver` wrappers, rename visual-grammar profile

Safe once the visual grammar asset file can be moved without breaking GUID references — requires Unity editor work.

### Phase P — Vision affordances (LOW, product-gated)

#### P.1 Guided starter session with listen-first chord bed

Files: `GuidedDemoComposition.cs`

Changes:
- Optionally seed a minimal Harmony pattern (block chords, neutral shape) on first launch so the user hears the foundation before drawing. Add a setting or UI toggle to control this.

#### P.2 In-phase tooltip / help strip

Files: `PhasePanel`, `CommitCardPanel`

Changes:
- For each phase, surface a one-sentence hint: "Harmony — draw a smooth curve to feel the progression roll." Rotate hints between first-time-in-phase and subsequent visits.

#### P.3 Genre-policy layer

Files: new `Assets/RhythmForge/Core/Data/GuidedPolicy.cs`

Changes:
- Collect the hardcoded `"electronic"` and `"G major"` literals from all five derivers into a `GuidedPolicy` struct (`genreId`, `keyName`, `bars`, `barSteps`, `bassRange`, etc.). Every deriver reads the policy; guided defaults populate it.

---

## 7. How to read this document for a continuation agent

1. Start with **§5 severity-ranked backlog**. Pick a band (HIGH, MEDIUM, LOW) based on the available engineering budget.
2. Each item in §5 references the deeper analysis in §2 / §3 / §4. Read the deep section before touching code.
3. The **§6 remediation plan** maps items to phases K–P. Execute phases in order (K → L → M → N → O → P), each leaving a playable composition.
4. If scope is tight, the minimum viable fix is Phase K alone — the "always musical" promise only holds after K lands. Everything beyond K is polish.
5. Update both code *and* the plan document when you ship. Several gaps in §3 are internal plan inconsistencies (e.g. snare on steps 4+12 vs step 8, bars 5–6 vs 5–8 lift). Pick an interpretation and propagate it.

## 8. Constraints the remediation agent should preserve

Inherited from the existing phase-by-phase handovers:

- Guided mode stays locked to `G major`, `100 bpm`, `8 bars`, `4/4`, `electronic` genre in v1.
- `Composition` is the guided source of truth. Do not revive direct reads of `AppState.drawMode` / `AppState.harmonicContext` / `AppState.tempo` / `AppState.key` for guided logic.
- `chordEvents` is the Harmony source of truth. Do not reintroduce writes to `DerivedSequence.chord / rootMidi / flavor` from the guided path.
- Groove owns timing and accents only. Never let Groove change Melody pitch derivation.
- One committed pattern per phase per composition. Do not introduce Save+Dup semantics in guided mode.
- `PatternRepository.ShouldReplaceGuidedPhasePattern` is the single place that enforces one-slot ownership — extend it, don't duplicate it.
- Progression updates happen on commit, not on draft creation.
- `ApplyGuidedModeUiState` in `RhythmForgeManager` is the single place that toggles panel visibility. If you add a panel, register it there.
- `GuidedDemoComposition` is the guided starter. `DemoSession` no longer exists.
