# Phase J Handover Plan

## Purpose

This document is the single source of truth for what was implemented in Phase J of the phased music creation rollout in RhythmForgeVR, and what was deliberately deferred.

Phase J is the cleanup phase. Its goal is to remove transitional scaffolding that previous phases left in place while they were being built. It does not add new musical features.

---

## Locked Scope

### What was implemented in Phase J

- `DemoSession.cs` deleted — the compatibility shim is gone; all call sites already used `GuidedDemoComposition` directly
- `HarmonyDeriver.Derive()` (guided path) no longer writes the transitional `chord`, `rootMidi`, and `flavor` fields to `DerivedSequence`; it writes only `chordEvents`
- `AlgorithmTest.TestHarmonyPipeline()` updated to assert on `chordEvents` (8 bars, ≥ 3 voicing notes per bar, flavor set on first slot) instead of the removed legacy fields
- Confirmed that `ArrangementPanel` and `SceneStripPanel` are already gated by `ApplyGuidedModeUiState()` in `RhythmForgeManager` — no additional code was needed
- Confirmed that `ShapeRoleProvider` is still legitimately used by free-mode derivers (Jazz, NewAge) — not deleted

### What was explicitly deferred from the Phase J plan

The Phase J spec assumed phases D, E, and H would have cleaned up all deriver references to `ShapeRoleProvider` and to the legacy `DerivedSequence` fields. They cleaned up the **guided** derivation path but left the **free-mode** genre derivers (Jazz, NewAge, and their associated tests) intact. The deferred items are:

| Deferred item | Why it was not completed | What is needed to complete it |
|---|---|---|
| Remove `chord`, `rootMidi`, `flavor` from `DerivedSequence` entirely | `JazzHarmonyDeriver` and `NewAgeHarmonyDeriver` still write these fields; `HarmonyBehavior` uses them as the fallback playback path for those patterns; the regression test `LegacyGenreHarmonyRoles_StayWithinExpectedRegisters` reads them | Update Jazz + NewAge derivers to also write `chordEvents`; remove fallback reads in `HarmonyBehavior`; update or remove the regression test |
| Delete `ShapeRoleProvider` and `ShapeRole` | Still actively used by `JazzHarmonyDeriver`, `NewAgeHarmonyDeriver`, `NewAgeMelodyDeriver`, `JazzMelodyDeriver`, `VoiceSpecResolver`, `DraftBuilder`, and the regression tests | Remove role-index branches from all free-mode derivers, or remove free-mode entirely |
| Remove `else if (derivedSequence?.chord != null)` fallback from `SessionStore` re-derivation (lines 538–545) | Still needed to update `harmonicContext` after Jazz/NewAge re-derivation | Remove after Jazz/NewAge derivers write `chordEvents` |
| `StateMigrator` v5→v6 enum rewrite branch cleanup | The branch did not exist yet — the enum rewriting is already handled by `NormalizeDrawMode()` which covers all legacy string variants | No action needed now; if a formal version bump is desired, add a version-gated cleanup in a future patch |

---

## Files Changed in Phase J

| File | Action | Reason |
|---|---|---|
| `Assets/RhythmForge/Core/Session/DemoSession.cs` | **Deleted** | Pure wrapper; all call sites already used `GuidedDemoComposition` |
| `Assets/RhythmForge/Core/Session/DemoSession.cs.meta` | **Deleted** | Unity meta for deleted file |
| `Assets/RhythmForge/Core/Sequencing/HarmonyDeriver.cs` | **Modified** | Removed `chord`, `rootMidi`, `flavor` writes; removed local `chord` variable; renamed local `flavor` to `flavorLabel` to avoid name collision with the removed field |
| `Assets/RhythmForge/Editor/AlgorithmTest.cs` | **Modified** | `TestHarmonyPipeline()` now asserts on `chordEvents` (8 chord slots, first slot has ≥ 3 voicing notes and a non-empty `flavor`) |

---

## Architecture State After Phase J

### What is now canonical for the guided harmony path

`HarmonyDeriver.Derive()` (the Electronic / guided path) produces:

```
DerivedSequence {
    kind = "harmony",
    totalSteps = 128,    // 8 bars × 16 steps
    chordEvents = List<ChordSlot>   // 8 entries, one per bar
    // chord, rootMidi, flavor NOT written — use chordEvents
}
```

`HarmonyBehavior.CollectVoiceSpecs()` and `HarmonyBehavior.Schedule()` always prefer `chordEvents` when present (the primary if-branch). The fallback `else if (chord != null)` paths remain for Jazz/NewAge compatibility.

### What is still legacy for free-mode genre derivers

`JazzHarmonyDeriver` and `NewAgeHarmonyDeriver` still write only the legacy fields:

```
DerivedSequence {
    kind = "harmony",
    totalSteps = N,
    flavor = "...",     // e.g. "maj7", "sus2"
    rootMidi = N,
    chord = List<int>   // flat voicing list
    // chordEvents is null for Jazz/NewAge patterns
}
```

The fallback read paths in `HarmonyBehavior` and `SessionStore` remain intentionally in place for these patterns.

### Panel visibility (free mode vs. guided mode)

| Panel | Guided mode | Free mode |
|---|---|---|
| `PhasePanel` | Visible | Hidden |
| `SceneStripPanel` | Hidden | Visible |
| `ArrangementPanel` | Hidden | Visible |
| `TransportPanel` | Read-only (key/tempo locked) | Full controls |
| `DockPanel` | Guided draw cycle | Full pattern type cycle |

All of the above is controlled through `RhythmForgeManager.ApplyGuidedModeUiState()`, called whenever the session loads or resets. No per-panel gating code is required.

---

## Constraints the Next Agent Should Preserve

- `DemoSession` is gone. Any new code that needs a guided starter state must call `GuidedDemoComposition.CreateDemoState(store)` directly.
- The guided `HarmonyDeriver.Derive()` writes **only** `chordEvents`. Do not reintroduce writes to the legacy `DerivedSequence` fields from the guided path.
- `JazzHarmonyDeriver` and `NewAgeHarmonyDeriver` still use legacy `DerivedSequence` fields. Do not remove the fallback read paths in `HarmonyBehavior` or `SessionStore` until those derivers are updated.
- `ShapeRoleProvider` is still needed. The regression test `LegacyGenreHarmonyRoles_StayWithinExpectedRegisters` (parametrized for `"jazz"` and `"newage"`) exercises multi-voice free-mode harmony. Do not delete `ShapeRoleProvider` without also removing those tests and the role-branch code in Jazz/NewAge derivers.
- `ApplyGuidedModeUiState()` is the single control point for guided-vs-free-mode panel visibility. If new panels are added, register them there — do not add per-panel `guidedMode` checks inside the panel's own `Initialize()`.

---

## Known Remaining Tech Debt

These items are known but were determined to be out of scope for the current release:

1. **Legacy `DerivedSequence` fields** (`chord`, `rootMidi`, `flavor`): Still present in the data model because Jazz/NewAge derivers write them. The fields can be removed after those derivers are updated to write `chordEvents`.

2. **`ShapeRoleProvider` and `ShapeRole`**: Still in use for free-mode multi-voice ensemble behavior. The Phase J plan says "confirm unreferenced before deleting" — they are still referenced in 8+ places. Removal requires a free-mode refactor pass.

3. **`HarmonySequence` class** in `SequenceData.cs` (lines 42–50): Appears to be unused dead code (separate from `DerivedSequence`). No Phase J action was taken. Investigate before the next major cleanup.

4. **`MusicalCoherenceRegressionTests.SessionStore_SetGenre_RederivesRolesInPatternOrder_AndPropagatesHarmonyContext`** (line 137): Still reads `harmony.derivedSequence.rootMidi` (line 176) for NewAge genre. This test and that read will need updating when NewAge moves to `chordEvents`.

5. **`StateMigrator` version number**: Currently `version = 7`. Any save file with `version < 7` gets `guidedMode = true` set automatically. This version-gate logic can be removed once the team is confident no v6 or earlier saves will be encountered.

---

## Verification Status

Code review completed:
- Guided harmony path cleanup reviewed: `HarmonyDeriver.Derive()` no longer writes legacy fields
- `AlgorithmTest` harmony assertions updated to `chordEvents`
- Panel visibility via `ApplyGuidedModeUiState()` confirmed correct
- No remaining `DemoSession` class references in `.cs` files

Local compilation status:
- Unity CLI not installed; no local compile or edit-mode batch run was possible from the terminal

---

## Recommended Next Steps

If the product decides to fully remove free-mode support:

1. Delete `JazzHarmonyDeriver`, `NewAgeHarmonyDeriver`, `JazzMelodyDeriver`, `NewAgeMelodyDeriver`, `JazzRhythmDeriver`, `NewAgeRhythmDeriver`
2. Remove `ShapeRoleProvider`, `ShapeRole`, `PatternContextScope.Push()` overload that takes `ShapeRole`
3. Remove the legacy field fallback paths in `HarmonyBehavior.CollectVoiceSpecs()` and `Schedule()`
4. Remove `DerivedSequence.chord`, `DerivedSequence.rootMidi`, `DerivedSequence.flavor`
5. Remove `SessionStore` re-derivation fallback at lines 538–545
6. Delete `ArrangementPanel` and `SceneStripPanel` (currently hidden but not deleted)
7. Delete `MusicalCoherenceRegressionTests.LegacyGenreHarmonyRoles_*`

If the product decides to keep free-mode but move Jazz/NewAge to the `chordEvents` format:

1. Update `JazzHarmonyDeriver` and `NewAgeHarmonyDeriver` to also write `chordEvents`
2. Update `MusicalCoherenceRegressionTests.LegacyGenreHarmonyRoles_*` to read `chordEvents`
3. Remove the fallback read paths in `HarmonyBehavior`
4. Remove the `else if (chord != null)` branch in `SessionStore` re-derivation
5. Remove `DerivedSequence.chord`, `DerivedSequence.rootMidi`, `DerivedSequence.flavor`
6. At that point `ShapeRoleProvider` would only serve `MelodyDeriver.DeriveLegacy()` — decide whether to keep or remove legacy melody roles too
