# Phase C User Instructions

## Purpose

This guide explains how to manually test Phase C from the VR app. It is written for a novice tester who may not know the codebase or the phased-music rollout.

Phase C is the first release where guided mode becomes visible in the UI. You can now move between musical phases using a dedicated phase panel. The music generation itself is not fully rewritten yet, so this phase is mostly about workflow, navigation, and UI behavior.

## What Phase C Changes In Plain Language

Phase C introduces a guided composition workflow with five visible phases:

1. `Harmony`
2. `Melody`
3. `Groove`
4. `Bass`
5. `Percussion`

When the app is in guided mode:

- a `PhasePanel` should appear
- the current phase should be shown clearly
- scene and arrangement panels should be hidden
- the transport mode button should no longer cycle modes
- tempo and key should act like fixed guided values

Important:

- the musical generation logic is still mostly the old implementation under the hood
- this phase is about guided navigation, not the full musical rewrite yet

## What To Expect

You should expect:

- a visible `PhasePanel` on normal app launch
- `Harmony` selected first in a fresh guided session
- clicking phase buttons to change the active drawing mode
- the transport mode button to look locked in guided mode
- scene-related panels to disappear in guided mode
- committed patterns to mark their phase as filled

You should not expect:

- one-shape-per-phase replacement rules yet
- automatic phase advance after commit
- fully new Harmony, Melody, Groove, Bass, or Percussion musical behavior yet
- the legacy demo session to automatically become guided if you load it manually

## Very Important Startup Note

This repo now starts in a fresh guided session by default on normal app launch.

That means:

- if you simply press Play and enter the app, you should now see the Phase C guided UI immediately
- `Harmony` should be the starting phase
- the scene and arrangement panels should already be hidden

The legacy demo still exists, but it is no longer the default startup path.

Because of that, this guide now has two paths:

1. verify the new default guided startup
2. optionally verify that the legacy demo still stays free-form if you load it manually

## Test Path 1: Confirm The New Default Guided Startup

Use this path first.

### Step 1. Launch The App Normally

1. Open the normal RhythmForgeVR scene your team uses.
2. Enter Play Mode.
3. Put on the headset.
4. Wait for the UI to appear.

What you should expect:

- the app boots normally
- the guided Phase C UI should be visible immediately
- you should see the phase panel instead of the old scene strip

What you should not expect:

- the old demo-first free-form layout on normal launch

### Step 2. Confirm This Is Guided Mode

1. Look for the phase navigation panel.
2. Look for the scene strip and arrangement panel.
3. Check whether the transport mode button behaves like a locked guided control.

What you should expect:

- phase panel visible
- scene strip hidden
- arrangement panel hidden
- transport mode button locked
- the app feels like a guided creation flow

Why this matters:

- this is the startup behavior change that makes Phase C visible immediately

## Test Path 2: Verify The Guided Session State

Use this path right after startup.

### Step 3. Start From A Fresh Empty Session

1. Look around the scene after normal launch.
2. Confirm there are no pre-seeded demo patterns already present.
3. Confirm you are starting from an empty guided session.

What you should expect:

- a fresh empty session
- guided-mode UI instead of the full free-form scene workflow

### Step 4. Verify The Guided Panels

1. Look directly at the main control area.
2. Find the transport panel.
3. Find the new phase panel.
4. Check whether the scene strip is missing.
5. Check whether the arrangement panel is missing.

What you should expect:

- a `PhasePanel` is visible
- the scene strip is hidden
- the arrangement panel is hidden

What you should not expect:

- visible scene switching controls in guided mode

### Step 5. Confirm The Starting Phase

1. Read the current-phase banner.
2. Look at the five phase buttons.

What you should expect in a fresh guided session:

- current phase should be `Harmony`
- `Harmony` button should look like the current phase
- the other phase buttons should start empty

What this means:

- the guided composition starts at the beginning of the intended creation flow

### Step 6. Check The Transport Panel Lock

1. Look at the transport mode button.
2. Try pressing it.

What you should expect:

- it should look locked or non-interactable
- it should not cycle through free-form modes while guided mode is active

What you should not expect:

- `Percussion -> Melody -> Harmony -> Bass -> Groove` cycling from the transport panel

Important concept:

- in guided mode, phase selection should happen from the `PhasePanel`, not the old mode-cycle control

### Step 7. Check Tempo And Key Labels

1. Look at the BPM label on the transport panel.
2. Look at the key label.

What you should expect in a fresh guided session:

- BPM should show `100 BPM`
- key should show `G major`

What you should not expect:

- editable guided tempo or key controls

## Test Path 3: Verify Phase Navigation

### Step 8. Click Through All Five Phases

1. Press `Harmony`.
2. Press `Melody`.
3. Press `Groove`.
4. Press `Bass`.
5. Press `Percussion`.

After each press, look at:

- the current-phase banner
- the button highlight
- the draw mode shown elsewhere in the UI, if visible

What you should expect:

- the banner updates to the selected phase
- the selected phase becomes the current one
- the app’s active drawing mode follows that phase

What you should not expect:

- audio to change immediately just because you changed phase

Important concept:

- Phase C changes what the next drawing action means
- it does not yet rewrite all audio behavior for each phase

## Test Path 4: Verify Guided Drawing Still Works

### Step 9. Draw In Harmony

1. Select `Harmony` on the phase panel.
2. Draw a simple stroke.
3. Commit the draft.

What you should expect:

- the draft is accepted
- the phase panel updates so `Harmony` no longer looks empty
- the app stays stable

What you should not expect:

- the full Phase D harmony progression rewrite yet

### Step 10. Draw In Melody

1. Select `Melody`.
2. Draw a simple stroke.
3. Commit it.

What you should expect:

- the draft commits successfully
- `Melody` becomes filled on the phase panel

### Step 11. Draw In Groove

1. Select `Groove`.
2. Draw a simple stroke.
3. Commit it.

What you should expect:

- the app accepts the pattern
- `Groove` becomes filled on the phase panel

What you should not expect:

- the true Groove-profile scheduling behavior yet

### Step 12. Draw In Bass

1. Select `Bass`.
2. Draw a simple stroke.
3. Commit it.

What you should expect:

- the app accepts the pattern
- `Bass` becomes filled on the phase panel

What you should not expect:

- a fully distinct bass derivation engine yet

### Step 13. Draw In Percussion

1. Select `Percussion`.
2. Draw a simple stroke.
3. Commit it.

What you should expect:

- the app accepts the pattern
- `Percussion` becomes filled on the phase panel

## Test Path 5: Check “Filled” State Behavior

### Step 14. Confirm Filled Buttons Stay Filled

1. After committing a pattern in a phase, move to another phase.
2. Look back at the earlier phase button.

What you should expect:

- the earlier phase should stay marked as filled

Important limitation:

- Phase C tracks the latest committed pattern for that phase
- it does not yet enforce that older phase patterns are deleted or replaced automatically

So if you redraw in the same phase later, you should think of the panel as tracking the latest known phase owner, not as proof that the app has already cleaned up all old content.

## Test Path 6: Check Playback Still Works

### Step 15. Press Play

1. After committing a few phase patterns, press Play on the transport panel.
2. Listen for playback.

What you should expect:

- playback still works
- the app should remain stable
- patterns from the selected phases should still be playable through the current runtime behavior

What you should not expect:

- the fully coherent six-step guided musical result described in later phases of the plan

Phase C is still using the current pre-rewrite derivation logic.

## Optional Test Path 7: Manually Verify The Legacy Demo Still Exists

Use this only if your build still exposes a manual way to load the demo session.

### Step 16. Load The Legacy Demo Manually

1. Use the project’s manual demo-load path, if available.
2. Wait for the session to switch.

What you should expect:

- the guided phase panel disappears
- scene strip and arrangement UI return
- the demo behaves like the older free-form path

Why this matters:

- the startup default changed, but the legacy demo behavior itself was intentionally preserved

## Concepts Explained For A Novice Tester

### What “Guided Mode” Means

Guided mode means the app is helping the user create music step by step instead of asking them to freely cycle through all pattern types.

In Phase C, that means:

- there is a visible ordered list of phases
- the user chooses the phase directly
- the old scene-heavy free-form workflow is reduced in the UI

### What “Current Phase” Means

It means:

- the next stroke you draw will be interpreted as that musical role
- for example, if `Bass` is current, your next committed stroke becomes the latest Bass-phase pattern

### What “Filled Phase” Means

It means the session has a remembered committed pattern for that phase.

In Phase C, this is a UI ownership marker.

It does not yet mean:

- the old phase content was removed
- the final phase-specific derivation rewrite is complete

### Why The Demo Still Looks Old

The demo was intentionally left in free mode so the team can keep a stable regression sample while guided mode is being built out.

That is why:

- the manual demo path and the normal startup guided path are different in Phase C

## Pass / Fail Summary

Pass if:

- the app boots normally
- the app immediately shows the guided phase panel on normal launch
- a fresh guided session shows the new Phase panel
- guided mode hides scene and arrangement panels
- the transport mode button is locked in guided mode
- `Harmony` starts as the current phase in a fresh guided session
- clicking phase buttons changes the active phase
- committing a pattern marks that phase as filled
- playback still works

Fail if:

- guided sessions do not show the phase panel
- scene or arrangement panels are still visible in guided mode
- the transport mode button still cycles free-form modes in guided mode
- clicking phase buttons does nothing
- committing in a guided phase does not mark it as filled
- the app crashes or becomes unstable during phase switching or commit

## What To Report Back

When reporting results, include:

- whether you tested the legacy demo path
- whether guided mode appeared immediately on normal launch
- whether the phase panel appeared
- whether `Harmony` started as the current phase
- whether scene and arrangement panels were hidden
- whether the transport mode button stayed locked
- whether phase buttons changed the active phase
- whether committed phases became filled
- whether playback still worked after guided drawing
