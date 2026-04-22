# Section 3 Handover Plan

## Purpose

This document is the single source of truth for the Section 3 implementation pass from [`Gap-Analysis.md`](../20260421-creating-music-in-phases.md/Gap-Analysis.md), built on top of the Section 2 architecture baseline in [`Section-2-Handover-plan.md`](./Section-2-Handover-plan.md).

Section 3 was treated as a musical-correctness and expressiveness pass, not a UX rewrite. The main goal was to make the guided composition output better match the beginner-safe music rules promised by the phased-plan and base-knowledge docs.

## Locked Decisions

The original plan and gap analysis had a few internal inconsistencies. This pass locked the following interpretations and implemented code to match them:

- Percussion backbeat is now interpreted canonically as:
  - kick on beats 1 and 3
  - snare on beats 2 and 4
- The old “snare on step 8” beginner safety note was treated as a documentation mistake, not the product target.
- Melody “answer phrase” is now interpreted as bars 5–8, not only bars 5–6.
- Section 3.11 energy-shape work was implemented using the current architecture’s existing layers:
  - melody answer-lift
  - stronger cadence voicings on bars 4 and 8
  - existing percussion fills
  - stronger Groove accent contrast
- No new instrument lanes were added in this pass.

## What Was Implemented

### 1. Percussion beginner floor restored (`Gap` §3.1, §3.2)

File:

- `Assets/RhythmForge/Core/Sequencing/PercussionDeriver.cs`

Changes:

- The guided percussion base pattern now starts from:
  - kick at steps `0` and `8`
  - snare at steps `4` and `12`
- Shape traits now enrich that base instead of replacing it:
  - narrow aspect ratio adds pickup kicks
  - high circularity adds the extra late-bar kick
  - low symmetry adds ghost snares
- Bar 1 safety anchors now reinforce the full backbeat floor:
  - `0 = kick`
  - `4 = snare`
  - `8 = kick`
  - `12 = snare`

Outcome:

- neutral guided percussion now sounds like the beginner-safe rhythm described in the source docs
- shape variation still changes feel, but no longer deletes the core pulse

### 2. Melody phrase anchors guaranteed at derivation time (`Gap` §3.3)

File:

- `Assets/RhythmForge/Core/Sequencing/MelodyDeriver.cs`

Changes:

- Guided melody derivation now guarantees an anchor note at:
  - bar 1 beat 1 (`step 0`)
  - bar 5 beat 1 (`step 64` in the default 8-bar form)
- If the stroke did not naturally land on those steps, the deriver now synthesizes chord-safe notes there.
- Anchor notes are forced to:
  - use the current bar’s chord tones
  - keep at least a quarter-note length (`>= 4` steps)

Outcome:

- the “question / answer” phrase restart is now guaranteed by derivation, not only protected later by Groove scheduling

### 3. Groove no longer thins away strong-beat chord tones (`Gap` §3.4, §3.7)

Files:

- `Assets/RhythmForge/Core/Sequencing/MelodyGrooveApplier.cs`
- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/MelodyLineBehavior.cs`

Changes:

- `MelodyGrooveApplier.Apply(...)` now accepts the current `ChordProgression`.
- Groove scheduling now preserves timing for:
  - phrase anchors
  - strong-beat melody notes (`step % 4 == 0`) that are chord tones of the current bar
- Those protected notes are no longer removed by sparse density thinning.
- Syncopation no longer re-times those protected strong-beat notes.
- Accent lookup for protected notes now stays tied to their original beat role.

Outcome:

- Groove still shapes rhythm, but it no longer breaks the guided melody’s chord-lock guarantee on the structural beats

### 4. Melody answer phrase now lifts across bars 5–8 (`Gap` §3.5)

File:

- `Assets/RhythmForge/Core/Sequencing/MelodyDeriver.cs`

Changes:

- Positive-tilt guided melody strokes now apply the +2 scale-degree lift across the full answer phrase (`bars 5–8`).
- The result summary/details text was updated to match that behavior.

Outcome:

- the second half of the 8-bar loop now sounds more clearly like an answer / variation instead of only lifting bars 5–6

### 5. Groove accent amplitude now scales more clearly with shape height (`Gap` §3.6)

File:

- `Assets/RhythmForge/Core/Sequencing/GrooveShapeMapper.cs`

Changes:

- Accent scaling now works by multiplying deviation from the beat-1 downbeat (`1.0`) instead of scaling around an arbitrary `0.8` center.
- Flat Groove shapes now produce gentler accent contrast.
- Tall Groove shapes now produce stronger accent contrast.

Outcome:

- Groove vertical span now has a more audible effect on rhythmic emphasis without changing melody pitch content

### 6. Cadence lift now always appears on bars 4 and 8 (`Gap` §3.11)

File:

- `Assets/RhythmForge/Core/Sequencing/HarmonyShapeModulator.cs`

Changes:

- Harmony cadence lift is now always applied on bars 4 and 8.
- Shape symmetry now controls how rich the cadence lift becomes:
  - higher symmetry: add the 7th
  - lower symmetry: add the 7th plus the 9th where musically meaningful
- The bar-4 / bar-8 lift is no longer dependent on low symmetry just to exist at all.

Outcome:

- the 8-bar loop now has a more reliable cadence contour even when the Harmony stroke is visually simple

### 7. Percussion swing now honors a pushed pickup feel in the current scheduler seam (`Gap` §3.10)

Files:

- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/PercussionBehavior.cs`
- `Assets/RhythmForge/Editor/BehaviorSeamTests.cs`

Changes:

- Percussion scheduling now preserves signed micro-timing instead of discarding negative offsets.
- Groove swing can now pull late pickup-style hits earlier by scheduling them from the previous step when needed.
- Delayed off-beat swing remains supported.

Important limit:

- this is still constrained by the current step-based scheduler
- it is not a full arbitrary “schedule earlier than any prior step” timing engine
- it improves the audible feel for pickup events and turnaround hits, which is the safest seam available without rewriting the scheduler

### 8. Rhythm-section coupling is now covered by the walkthrough test (`Gap` §3.13)

File:

- `Assets/RhythmForge/Editor/WalkthroughTests.cs`

Changes:

- The guided 5-step walkthrough now asserts kick/bass alignment on:
  - bar 1 beat 1
  - bar 5 beat 1

Outcome:

- future drift in the foundation of the rhythm section is more likely to be caught automatically

## What Was Already Correct And Was Left As-Is

### `Gap` §3.8 — `sus2` truth vs naming

No code change was needed.

The current `HarmonyShapeModulator` already uses a musically correct `sus2` interpretation. The gap analysis itself called this out as an improvement over the original plan text.

## What Was Only Partially Addressed

### `Gap` §3.9 — Electronic-only scope baked into logic

Partial improvement:

- guided melody now reads `GuidedDefaults.ActiveGenreId` instead of a local hardcoded guided-genre literal

Still not implemented:

- there is still no dedicated `GuidedPolicy` / `GuidedGenreResolver` object that centralizes:
  - genre id
  - key
  - bar count
  - tempo
  - register targets

Why deferred:

- Section 3 was focused on musical behavior correctness first
- introducing a new policy object would be a cross-cutting cleanup best done in a separate technical pass

### `Gap` §3.11 — arrangement energy curve

Implemented in the minimal-safe current-architecture form:

- melody answer lift now spans bars 5–8
- cadence voicing lift now always happens on bars 4 and 8
- percussion fills were already present on bars 4 and 8
- Groove accent contrast is now more expressive

Still not implemented:

- no new shaker / tambourine / open-hat / crash lanes
- no dedicated per-bar layer mute/unmute system
- no explicit Bass second-half density policy independent of the stroke

## Explicitly Deferred

### `Gap` §3.12 — shaker / tambourine / open-hat / crash support

Deferred.

Reason:

- requires new percussion-lane semantics and/or new arrangement-layer modeling
- out of scope for a correctness-first Section 3 pass

## Files Touched

Core implementation files:

- `Assets/RhythmForge/Core/Sequencing/PercussionDeriver.cs`
- `Assets/RhythmForge/Core/Sequencing/MelodyDeriver.cs`
- `Assets/RhythmForge/Core/Sequencing/MelodyGrooveApplier.cs`
- `Assets/RhythmForge/Core/Sequencing/GrooveShapeMapper.cs`
- `Assets/RhythmForge/Core/Sequencing/HarmonyShapeModulator.cs`
- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/MelodyLineBehavior.cs`
- `Assets/RhythmForge/Core/PatternBehavior/Behaviors/PercussionBehavior.cs`

Tests updated:

- `Assets/RhythmForge/Editor/PercussionDeriverTests.cs`
- `Assets/RhythmForge/Editor/MelodyDeriverTests.cs`
- `Assets/RhythmForge/Editor/MelodyGrooveApplierTests.cs`
- `Assets/RhythmForge/Editor/GrooveShapeMapperTests.cs`
- `Assets/RhythmForge/Editor/HarmonyShapeModulatorTests.cs`
- `Assets/RhythmForge/Editor/WalkthroughTests.cs`
- `Assets/RhythmForge/Editor/BehaviorSeamTests.cs`

## Test Coverage Added / Updated

### Percussion

- `DefaultKick_IsOnBeats1And3`
- `DefaultSnare_IsOnBeats2And4`
- `AspectRatioShape_AddsPickupHits_WithoutRemovingBaseKicks`

### Melody

- `AnchorNote_AtStep0_AlwaysPresent`
- `AnchorNote_AtStep64_AlwaysPresent_WhenBarsGreaterThan4`
- `AnchorNotes_AreChordTonesOfBar0AndBar4`
- `PositiveTilt_LiftsAnswerPhraseAcrossBars5To8`

### Groove / melody scheduling

- `StrongBeatChordTones_Survive_SparseDensity`
- `PassingBeats_StillThinnedAsExpected`
- `StrongBeatNotes_AreNotShiftedBySyncopation`
- `AccentAmplitude_ScalesWithVerticalSpan`

### Harmony / walkthrough / behavior seams

- `Bars4And8_AlwaysIncludeCadenceLift`
- walkthrough kick/bass beat-1 assertions
- `PercussionBehavior_Schedule_CanPullPickupHitFromPreviousStep`

## Verification Status

### Unity batch run attempted

Command attempted:

```bash
"/Applications/Unity/Hub/Editor/6000.4.3f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -quit \
  -projectPath "/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR" \
  -runTests -testPlatform editmode \
  -testResults "/Users/bogdandiaconescu/Projects/Cogness/RhythmForgeVR/Logs/Section3-EditModeResults.xml"
```

Result:

- blocked because another Unity instance already had the project open
- no terminal-side compile/test pass completed in this session

### Local fallback limits

- `dotnet`, `csc`, `mcs`, and `mono` are not installed in this shell environment
- verification here is limited to source review plus test updates

## Required Next Verification Step

Before the next section starts, the next engineer/agent should:

1. Close the currently open Unity editor instance or run from that editor directly.
2. Run the full EditMode suite.
3. Pay special attention to:
   - `PercussionDeriverTests`
   - `MelodyDeriverTests`
   - `MelodyGrooveApplierTests`
   - `HarmonyShapeModulatorTests`
   - `BehaviorSeamTests`
   - `WalkthroughTests`

## Known Residual Risks

- Unity compile status is not confirmed from this terminal session.
- The new cadence flavor labels (`triad-cadence-rich`, `sus2-cadence-rich`, `maj9-lift`) are internal descriptive values; if any future UI begins depending on exact flavor-name enumerations, those call sites must be reviewed.
- Percussion early-pull scheduling is intentionally conservative and limited by the current step scheduler.
- The broader guided-policy cleanup from `Gap` §3.9 is still open.
- Section 3.12’s additional percussion-layer vision remains deferred.

## Guidance For The Next Agent / Section

The next agent should treat this file as the authoritative record for Section 3.

Important follow-on seams:

- If the next section touches guided scheduling, keep using the Section 2 invalidation model instead of adding new ad-hoc pending state.
- If guided-mode configurability expands, introduce a dedicated guided policy object instead of reintroducing scattered literals.
- If new percussion lanes are added later, preserve the beginner-safe base backbeat as the floor and layer detail on top.
- If the next section adds UX work, keep the locked musical decisions from this document:
  - backbeat = beats 2 and 4
  - answer phrase = bars 5–8
  - cadence lift = bars 4 and 8 always, symmetry changes richness

## Recommended Manual Sanity Pass

After Unity compiles, manually confirm:

1. Neutral Percussion gives kick on 1+3 and snare on 2+4.
2. Positive-tilt Melody sounds more lifted in bars 5–8 than bars 1–4.
3. Sparse Groove still keeps the strong downbeat melody notes.
4. Bars 4 and 8 feel harmonically more cadential than bars 1 and 5.
5. Bass and kick still line up solidly on the first beat of bars 1 and 5.
