# Phase E User Instructions

## Purpose

This guide explains how to manually test Phase E from the VR app.

It is written for a novice tester. You do not need to know the codebase. The goal is to verify that the Melody phase now follows the Harmony progression bar-by-bar, stays in key, and replaces the previous Melody when redrawn.

## What Phase E Changes In Plain Language

Before Phase E:

- Melody in guided mode still used the older electronic melody logic
- Harmony could already create an 8-bar chord progression, but Melody was not yet fully reacting to each bar of that progression
- redrawing Melody did not yet have the same clear replacement hygiene as Harmony

After Phase E:

- drawing in the `Melody` phase now creates an 8-bar lead line for the guided piece
- the Melody listens to the current Harmony progression bar-by-bar
- strong musical moments in the Melody should land on notes that fit the active chord
- the rest of the Melody still stays inside `G major`
- if you draw a new Melody shape, it replaces the previous Melody pattern

Important:

- Phase E is only the Melody rewrite
- Groove, Bass, and Percussion are still using earlier implementations

## What You Should Expect

You should expect:

- the app still starts in guided mode
- Harmony still controls the chord bed underneath the piece
- Melody should sound more "connected" to those chord changes than before
- wild Melody shapes should still sound musically safe
- drawing a second Melody shape should replace the first Melody pattern
- redrawing Harmony should cause the committed Melody to adapt to the new Harmony after the re-derive completes

You should not expect:

- Groove to change Melody rhythm yet
- a rewritten Bass phase yet
- a rewritten Percussion phase yet
- every phase to enforce replacement yet

## Important Tester Advice

For the main Phase E pass, use the normal `Save` button.

Avoid using `Save+Dup` during the Melody pass. The older duplicate workflow still exists and can create extra scene instances that are outside the main Phase E intent.

## Concepts Explained For A Novice Tester

### What is a "strong beat"?

In this project, the strong beats are the more stable anchor points inside the bar.

You do not need to count music formally, but if you tap along to `1 2 3 4`, the Melody should usually feel especially "settled" on beats `1` and `3`.

Phase E is trying to make those moments fit the active chord more clearly.

### What does "stays in key" mean here?

The guided piece is locked to `G major`.

That means the Melody should avoid sounding randomly wrong or sharply clashing with the Harmony. You do not need music theory training to hear this. If it sounds consistently smooth and intentional, that is the main goal.

### What does "answer phrase lift" mean?

If you draw an upward-feeling Melody shape, part of the later phrase can feel slightly more lifted or elevated.

You may hear that most clearly around bars 5 and 6.

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

## Test Path 2: Make Sure Harmony Exists First

Phase E Melody follows Harmony, so it is important to have a committed Harmony pattern before you judge the Melody result.

### Step 2. If Harmony Is Empty, Draw One

1. Stay on the `Harmony` phase.
2. Draw one simple medium-size Harmony stroke.
3. Press `Save`.
4. Start playback and let it run for one full loop.

What you should expect:

- you hear an 8-bar Harmony bed
- the loop still feels stable and beginner-safe

If Harmony was already committed from an earlier test, you can keep it and move on.

## Test Path 3: Create The First Guided Melody

### Step 3. Switch To Melody

1. Press the `Melody` button on the `PhasePanel`.
2. Confirm `Melody` is now the active phase.

What you should expect:

- the phase selection changes cleanly
- the app remains stable

### Step 4. Draw A Clearly Shaped Melody Stroke

1. Use the stylus to draw one open Melody stroke.
2. Make it visually obvious:
   - a wave
   - a zig-zag
   - an up-and-down contour
3. Finish the stroke so the draft card appears.

Good novice advice:

- do not worry about drawing "music"
- just draw a shape with visible rises and falls

What you should expect:

- the draft card appears
- the draft is labeled as Melody
- the app remains stable

### Step 5. Commit The Melody Draft

1. Press the normal `Save` button.
2. Let playback run for at least one full 8-bar loop.

What you should expect:

- you now hear a Melody above the Harmony
- the Melody should feel like it belongs with the chords, not like a random note stream
- the Melody should stay musically safe even if your shape was dramatic

What you should not expect:

- harsh out-of-key clashes
- Melody notes that sound randomly detached from the Harmony all the time

## Test Path 4: Listen For Chord-Aware Melody Behavior

### Step 6. Focus On The Stable Moments

1. Keep playback running.
2. Listen for the more settled moments of the Melody.
3. Pay attention when the Harmony changes from bar to bar.

What you should expect conceptually:

- when the chord changes underneath, the Melody should still sound like it fits
- the more anchored Melody moments should sound especially compatible with the current chord
- the line can still move, but it should not feel like it is fighting the Harmony

You do not need to identify the exact notes.

The key question is:

- does the Melody feel harmonically connected to the 8-bar chord loop?

## Test Path 5: Check That The Melody Stays In Key

### Step 7. Try A More Extreme Shape

1. Stay on the `Melody` phase.
2. Draw a more exaggerated stroke:
   - steeper ups and downs
   - more sudden direction changes
3. Save it.
4. Listen through the full loop again.

What you should expect:

- the Melody contour should change
- the phrasing may become more active or jumpy
- it should still sound inside the same guided musical world

What you should not expect:

- obvious random wrong notes
- a Melody that suddenly sounds like it changed to a different key

## Test Path 6: Check The Bar-5/6 Lift

### Step 8. Draw An Upward-Leaning Melody

1. Stay on `Melody`.
2. Draw a stroke that feels upward overall:
   - start lower
   - finish higher
3. Save it.
4. Listen especially in the second half of the loop.

What you may hear:

- bars 5 and 6 can feel slightly more lifted than the first phrase
- the answer phrase can feel like it rises or responds to the earlier phrase

What you should not expect:

- a massive octave jump every time
- bars 7 and 8 becoming chaotic

This is a subtle musical effect, not a dramatic special effect.

## Test Path 7: Verify Melody Replacement

### Step 9. Draw A Second Clearly Different Melody

1. Stay on `Melody`.
2. Draw a second Melody stroke that is clearly different from the first.
3. Save it.

What you should expect:

- the Melody sound updates
- the new Melody should replace the previous Melody pattern
- only the newest Melody pattern should remain in the scene after a normal redraw

### Step 10. Check Scene Cleanup

1. Look at the Melody visual in the scene.
2. Confirm the previous Melody pattern is gone.

What you should expect:

- only the latest committed Melody pattern remains

What you should not expect:

- both the old Melody and new Melody remaining after a normal redraw

## Test Path 8: Verify Harmony Redraw Re-Derives Melody

This is the most important end-to-end check for Phase E.

### Step 11. Redraw Harmony After Melody Already Exists

1. Keep your committed Melody.
2. Switch back to `Harmony`.
3. Draw a noticeably different Harmony stroke.
4. Save it.
5. Let playback continue.

What you should expect:

- the Harmony bed changes
- after the Harmony change, the Melody should still sound compatible with the new chords
- the Melody should not feel frozen against the old Harmony logic

Practical tester advice:

- give the app a brief moment after the Harmony commit if needed
- the re-derive is background-driven, so listen over the next loop rather than expecting a dramatic instant pop

What you should not expect:

- the Melody sounding permanently attached to the old Harmony version
- obvious harmonic clashes after the Harmony redraw

## Test Path 9: Optional Timing Density Check

### Step 12. Draw A Faster, More Jittery Melody Shape

1. Go back to `Melody`.
2. Draw a shape with more quick movement and small changes.
3. Save it.
4. Listen for whether the Melody feels a little more rhythmically active.

What you may hear:

- slightly denser note placement
- more active melodic movement

What you should not expect:

- Groove-style rhythm mutation
- a completely rewritten rhythmic engine

That fuller rhythm work belongs to the later Groove phase.

## Quick Pass / Fail Summary

Phase E looks good if:

- guided startup still works
- Melody commits successfully in the `Melody` phase
- Melody sounds compatible with Harmony across the whole 8-bar loop
- extreme Melody shapes still stay musically safe
- upward Melody shapes can produce a subtle lifted answer feeling
- redrawing Melody replaces the previous Melody pattern
- redrawing Harmony keeps Melody harmonically compatible after re-derive

Phase E needs follow-up if:

- Melody regularly sounds out of key
- Harmony changes but Melody still sounds tied to the old Harmony
- Melody redraw leaves multiple Melody patterns behind after normal `Save`
- the final phrase loses its sense of cadence or sounds abruptly cut off
