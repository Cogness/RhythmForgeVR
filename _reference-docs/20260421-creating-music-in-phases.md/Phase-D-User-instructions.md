# Phase D User Instructions

## Purpose

This guide explains how to manually test Phase D from the VR app.

It is written for a novice tester. You do not need to know the codebase. The goal is to verify that the Harmony phase now creates a real 8-bar chord progression and that redrawing Harmony replaces the previous Harmony result.

## What Phase D Changes In Plain Language

Before Phase D:

- Harmony mostly behaved like one static pad chord
- guided mode showed the Harmony step in the workflow, but the actual Harmony music was not yet rewritten

After Phase D:

- drawing in the `Harmony` phase now creates an 8-bar chord bed
- the chord roots still follow the beginner-safe loop:
  - `G`
  - `E minor`
  - `C`
  - `D`
  - then repeat once
- your Harmony shape changes the flavor and voicing of that loop
- drawing a new Harmony shape replaces the previous Harmony pattern

Important:

- Phase D is only the Harmony rewrite
- Melody, Groove, Bass, and Percussion are still using their earlier implementations

## What You Should Expect

You should expect:

- guided mode still starts in `Harmony`
- committing a Harmony stroke creates audible chord changes across 8 bars
- the piece still feels like the same beginner-safe song structure
- drawing a second Harmony stroke changes the chord bed
- only the latest committed Harmony pattern should remain

You should not expect:

- a full Melody rewrite yet
- Groove-driven melody timing yet
- a true Bass rewrite yet
- a Percussion rewrite yet
- every phase to enforce replacement yet

## Important Tester Advice

For Phase D testing, prefer the normal `Save` button.

Avoid using `Save+Dup` during the Harmony test pass. That duplicate action still exists from the older workflow and can create extra scene instances of the same new Harmony pattern, which is outside the main Phase D intent.

## Test Path 1: Confirm Guided Startup Still Works

### Step 1. Launch The App

1. Open the normal RhythmForgeVR scene your team uses.
2. Enter Play Mode.
3. Put on the headset.
4. Wait for the UI to appear.

What you should expect:

- the app starts in guided mode
- the `PhasePanel` is visible
- `Harmony` is the current phase
- scene and arrangement panels are hidden

This matters because Phase D builds on top of the guided startup from Phase C.

## Test Path 2: Create The First Harmony Progression

### Step 2. Stay On Harmony

1. Look at the `PhasePanel`.
2. Confirm `Harmony` is selected.

What you should expect:

- `Harmony` should be highlighted as the current phase

### Step 3. Draw A Simple Harmony Stroke

1. Use the stylus to draw one smooth medium-size stroke in space.
2. Keep it simple:
   - a gentle upward diagonal line is fine
   - a soft curved arc is also fine
3. Finish the stroke so the commit card appears.

What you should expect:

- the draft card appears
- the draft is labeled as Harmony
- the app remains stable

### Step 4. Commit The Harmony Draft

1. Press the normal `Save` button.
2. Start playback if it is not already running.
3. Listen for at least one full 8-bar loop.

What you should expect:

- you should hear Harmony change across the loop instead of sounding like one single static held chord
- the progression should still feel musically safe and coherent
- the loop should repeat cleanly

What you should not expect:

- random chromatic notes
- a Harmony shape that changes the underlying chord roots away from the guided song loop

## Test Path 3: Hear That The 8 Bars Actually Move

### Step 5. Listen For Bar-to-Bar Motion

1. Keep playback running.
2. Focus only on the Harmony layer.
3. Listen through all 8 bars.

What you should expect conceptually:

- bars 1 and 5 should feel related
- bars 2 and 6 should feel related
- bars 3 and 7 should feel related
- bars 4 and 8 should feel like a return or turnaround into the loop

You do not need perfect musical training for this.

The key thing to notice is:

- the Harmony should feel like a repeating 8-step chord path, not one frozen pad

## Test Path 4: Verify Redraw Replacement

### Step 6. Draw A Second Different Harmony Stroke

1. Stay in the `Harmony` phase.
2. Draw a second stroke that is clearly different from the first one.
3. Good examples:
   - if the first stroke tilted upward, draw the second one tilted downward
   - if the first stroke was smooth and centered, make the second more wide or lower
4. Commit it with `Save`.

What you should expect:

- the Harmony should update after the second commit
- the chord bed should sound different from the first version
- the new Harmony should replace the previous Harmony pattern

### Step 7. Check Scene Cleanup

1. Look at the Harmony visual in the scene.
2. Confirm the previous Harmony pattern is gone.

What you should expect:

- only the latest committed Harmony pattern should remain

What you should not expect:

- two separate old and new Harmony patterns both staying in the scene after a normal redraw

## Test Path 5: Check That Other Guided UI Still Behaves

### Step 8. Confirm The Phase Panel Still Tracks Fill State

1. After saving Harmony, look at the `PhasePanel`.
2. Check the Harmony button state.

What you should expect:

- `Harmony` should look filled/current, depending on the panel state rules

### Step 9. Switch To Another Phase Without Drawing

1. Press `Melody`.
2. Press `Harmony` again.

What you should expect:

- the phase selection should still work normally
- returning to Harmony should not remove the committed Harmony pattern by itself

## Test Path 6: Optional Shape Experiments

These are optional exploratory checks if you want to hear the modulation rules more clearly.

### Step 10. Upward Tilt Test

1. Draw an upward-leaning Harmony line.
2. Save it.
3. Listen to the result.

What you may hear:

- a slightly richer or more open Harmony color

### Step 11. Downward Tilt Test

1. Draw a downward-leaning Harmony line.
2. Save it.
3. Listen again.

What you may hear:

- a different suspended-feeling chord color

### Step 12. High vs Low Placement Test

1. Draw one Harmony stroke higher in your drawing space.
2. Save and listen.
3. Then draw another one lower.

What you may hear:

- the Harmony voicing may sit in a different register

Important:

- the piece should still remain in-key and beginner-safe

## Trouble Signs

Report it as a Phase D issue if you observe any of these:

- Harmony still sounds like one single unchanged static chord through the full loop
- redrawing Harmony does not change the chord bed
- saving a second Harmony stroke leaves the old Harmony pattern behind after a normal `Save`
- the Harmony introduces obviously out-of-key notes
- the app updates Harmony before you save the draft
- discarding a Harmony draft still changes the music

## What Not To Treat As A Phase D Failure

These are not Phase D failures by themselves:

- Melody still sounding old or simplistic
- Groove not obviously changing Melody rhythm yet
- Bass still not behaving like a dedicated bass voice yet
- Percussion still using the older logic

Those belong to later phases.

## Short Summary For A Quick Manual Pass

If you only have a few minutes, do this:

1. Launch the app and confirm guided mode starts on `Harmony`.
2. Draw and save one Harmony stroke.
3. Play the loop and confirm the Harmony changes across 8 bars.
4. Draw and save a second Harmony stroke.
5. Confirm the Harmony sound changes and the old Harmony pattern is replaced.

