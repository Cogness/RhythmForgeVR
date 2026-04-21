# Phase I User Instructions

## Purpose

This guide explains how to manually test the Phase I guided-flow improvements inside the VR app.

It is written for a novice user. You do not need to know the codebase to follow it.

## What Phase I Adds

Phase I does not add a new musical layer. Instead, it makes the full phased workflow easier to use.

What the app should now do:

- start in an empty guided composition
- begin on the `Harmony` phase
- let you play the whole piece from the Phase panel
- let you clear any individual phase from the Phase panel
- let you redraw or clear a committed phase from the Inspector panel
- optionally auto-advance to the next phase after saving a shape
- show `Pending` on downstream phases while Harmony-driven background re-derivation is still being applied

What the app should not do:

- it should not start with old demo shapes already committed
- it should not force you to use the transport panel just to hear the piece
- it should not leave an old committed phase active after you redraw that same phase
- it should not show a misleading “Groove saved” message when Groove was cleared

## Concepts In Plain Language

`Phase`:
One creation step in the guided composition flow: Harmony, Melody, Groove, Bass, or Percussion.

`Redraw`:
Throw away the current committed shape for that phase and go back into drawing that same phase again.

`Clear`:
Remove the committed result for a phase without drawing a replacement immediately.

`Adjust`:
Keep the selected pattern and continue tweaking its placement or mix-related controls instead of redrawing it.

`Auto Next`:
A guided save option that automatically moves you to the next phase after you commit a draft.

`Pending`:
A temporary badge meaning the app is still applying background updates after an upstream change.

## Before You Start

Recommended setup:

1. Launch the app.
2. Load a fresh guided session if the app is not already in one.
3. Confirm that you are in the guided flow, not the older scene/arrangement flow.

What you should see at startup:

- the `PhasePanel` is visible
- `Harmony` is the current phase
- there are no committed shapes yet
- scene strip and arrangement controls are hidden

## Manual Test 1: Guided Starter Session

Goal:

- confirm the app now starts from an empty guided composition

Steps:

1. Start a fresh guided session.
2. Look at the Phase panel.
3. Do not draw anything yet.

Expected result:

- `Harmony` is highlighted as the current phase
- the other phases are empty
- there are no already-placed demo patterns in the scene

Not expected:

- preloaded Harmony, Melody, or Percussion demo shapes
- the app starting in a non-guided mode

## Manual Test 2: Phase Panel Play Button

Goal:

- confirm the Phase panel can control full-piece playback

Steps:

1. Draw and save at least one phase, such as Harmony.
2. On the Phase panel, press `Play Piece`.
3. Listen for playback.
4. Press the same button again.

Expected result:

- playback starts when you press `Play Piece`
- the button label changes to `Stop Piece`
- pressing it again stops playback

Not expected:

- needing to change the current phase just to hear playback
- the button staying stuck on the wrong label

## Manual Test 3: Phase Panel Clear Buttons

Goal:

- confirm each phase can be cleared directly from the Phase panel

Steps:

1. Commit a shape for one or more phases.
2. On the Phase panel, press `Clear` under one committed phase.
3. Look at the phase state and listen again if playback is running.

Expected result:

- that phase returns to an empty state
- its old committed result is removed
- other committed phases remain intact

Example:

- if you clear `Percussion`, the drum part should disappear but Harmony or Melody should stay if they were already committed

## Manual Test 4: Auto Next Toggle On Save

Goal:

- confirm guided save can auto-advance between phases

Steps:

1. Go to the current drawing phase.
2. Draw a stroke so the Commit card appears.
3. Look at the center button on the Commit card.
4. If it says `Auto Next OFF`, press it once so it becomes `Auto Next ON`.
5. Press `Save`.

Expected result:

- the draft saves normally
- the app moves to the next phase automatically

Repeat:

1. Draw another draft.
2. Switch the center button to `Auto Next OFF`.
3. Press `Save`.

Expected result:

- the draft saves
- the app stays on the same phase instead of auto-advancing

Not expected:

- guided save creating a duplicate instance
- the center button still behaving like `Save+Dup` in guided mode

## Manual Test 5: Inspector Redraw For A Committed Phase

Goal:

- confirm a committed guided phase can be redrawn from the Inspector

Steps:

1. Commit Harmony.
2. Commit Melody.
3. Select the committed Harmony pattern in the scene so the Inspector opens.
4. Look at the Inspector action buttons.
5. Press `Redraw`.

Expected result:

- the committed Harmony result is cleared
- the current phase becomes `Harmony`
- you are back in the flow for drawing Harmony again

Important expectation:

- if Melody and Bass already exist, Harmony redraw may cause those dependent phases to show `Pending` while the background update finishes

## Manual Test 6: Pending Badge Behavior

Goal:

- confirm Harmony-driven downstream re-derivation is visible in the Phase panel

Steps:

1. Commit Harmony.
2. Commit Melody.
3. Commit Bass.
4. Select Harmony in the scene.
5. Press `Redraw`, then draw and save a new Harmony shape.
6. Watch the Phase panel right after saving.

Expected result:

- Melody and Bass may briefly show `Pending`
- once the background re-derive completes, the `Pending` state disappears

What not to expect:

- every later phase always showing `Pending`
- Groove or Percussion necessarily showing `Pending` just because Harmony changed

Why:

- the current app only badges phases that actually use async background re-derivation today

## Manual Test 7: Inspector Adjust

Goal:

- confirm the guided Inspector still supports non-redraw tweaking

Steps:

1. Commit any phase and select it in the scene.
2. Press `Adjust` in the Inspector.
3. Without redrawing, tweak the pattern using the available controls:
   - move it in space
   - change depth
   - observe pan / gain / brightness readouts

Expected result:

- the pattern stays selected
- you remain in the adjustment workflow
- no phase is cleared just because you pressed `Adjust`

## Manual Test 8: Groove Clear Should Be Quiet

Goal:

- confirm clearing Groove refreshes the session without a misleading success message

Steps:

1. Commit Melody.
2. Commit Groove.
3. Clear Groove from the Phase panel or Inspector.

Expected result:

- Groove becomes empty again
- Melody remains committed
- you should not see a misleading `Groove saved` style message caused by the clear action

## Manual Test 9: End-To-End Guided Flow

Goal:

- confirm the Phase I UX supports the full guided composition flow from scratch

Steps:

1. Start a fresh guided session.
2. Draw and save Harmony.
3. Draw and save Melody.
4. Draw and save Groove.
5. Draw and save Bass.
6. Draw and save Percussion.
7. Press `Play Piece`.
8. Select one committed phase and try `Redraw`.
9. Select another committed phase and try `Clear`.

Expected result:

- the flow feels coherent from start to finish
- phase controls are understandable without knowing the internals
- redraw and clear work without breaking the rest of the composition

## Troubleshooting

If the app still shows old pre-made demo content:

- start a fresh guided session again
- if it still happens, report it as a guided starter-state bug

If `Auto Next` seems wrong:

- check the center button text on the Commit card before pressing `Save`
- `Auto Next ON` means the phase should advance after save
- `Auto Next OFF` means it should stay on the same phase

If you do not see `Pending` after Harmony changes:

- make sure Melody or Bass were already committed
- the badge only appears for actual downstream async re-derives that exist today

If `Adjust` feels like “nothing happened”:

- that is mostly expected
- `Adjust` means keep the current selection and use the existing movement, depth, and mix controls instead of redrawing
