# Phase F User Instructions

## Purpose

This guide explains how to manually test Phase F from the VR app.

It is written for a novice tester. You do not need to know the codebase. The goal is to verify that the Groove phase now changes Melody rhythm and feel without changing Melody pitch content, and that redrawing Groove replaces the previous Groove result.

## What Phase F Changes In Plain Language

Before Phase F:

- Groove still behaved like a temporary Melody-like placeholder
- Groove did not yet act as the real rhythm-shaping step for the guided piece
- Melody pitch behavior came from Phase E, but Groove was not yet formally shaping Melody timing

After Phase F:

- drawing in the `Groove` phase now creates a stored Groove profile
- the Groove profile changes how the committed Melody is played
- Groove can make the Melody feel sparser, more syncopated, or more accented
- Groove should not change which notes Melody chose in Phase E
- drawing a new Groove shape replaces the previous Groove pattern

Important:

- Phase F is only the Groove rewrite
- Bass and Percussion are still not rewritten yet

## What You Should Expect

You should expect:

- guided mode still starts normally
- Groove can now affect Melody timing and emphasis
- Melody should still sound like the same tune, but with a different rhythmic feel
- very sparse Groove shapes should make Melody feel more stripped back
- Groove redraw should replace the previous Groove pattern

You should not expect:

- Groove to create a brand-new Melody pitch contour
- Groove to rewrite Harmony
- Groove to replace the future Bass or Percussion work
- a dramatic "extra-note generator" effect every time

## Important Tester Advice

For the main Phase F pass, use the normal `Save` button.

Avoid using `Save+Dup` during the Groove pass. The older duplicate workflow still exists and can create extra scene instances that are outside the main Phase F intent.

## Concepts Explained For A Novice Tester

### What is Groove in this phase?

Groove is the step that changes how the Melody moves through time.

It does not decide the Melody's notes. Melody already decided those in Phase E.

Groove changes things like:

- how busy or sparse the Melody feels
- whether off-beat notes feel pushed or pulled
- which beats feel stronger or softer

### What does "without changing pitch" mean?

It means the Melody should still sound like the same tune.

You may hear different timing, spacing, and emphasis, but it should not suddenly feel like Groove composed a different set of notes.

### What are the protected anchor notes?

For beginner safety, two important Melody anchors should survive Groove thinning:

- bar 1 beat 1
- bar 5 beat 1

You do not need to count precisely if that feels hard.

In plain language:

- the Melody should still feel anchored at the start of the first phrase
- and again at the start of the answer phrase

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

## Test Path 2: Make Sure Harmony And Melody Exist First

Groove changes Melody playback, so it is important to have a committed Melody pattern before you judge the audible Groove result.

### Step 2. If Harmony Is Empty, Draw One

1. Stay on the `Harmony` phase.
2. Draw one simple Harmony stroke.
3. Press `Save`.
4. Let it commit.

What you should expect:

- you get an 8-bar Harmony bed

### Step 3. If Melody Is Empty, Draw One

1. Switch to the `Melody` phase.
2. Draw one clear Melody stroke.
3. Press `Save`.
4. Play the loop and listen for one full cycle.

What you should expect:

- you hear a Melody above the Harmony
- the Melody feels musically safe and connected to the chords

If both Harmony and Melody already exist from earlier testing, you can move on.

## Test Path 3: Check Groove Commit With No Melody

This is a useful edge case and should be tested once.

### Step 4. Optional Empty-Melody Check

1. Start from a fresh guided session if convenient.
2. Do not save a Melody yet.
3. Go directly to the `Groove` phase.
4. Draw a Groove stroke.
5. Press `Save`.

What you should expect:

- the app stays stable
- the Groove draft commits successfully
- you should not hear a new Groove sound by itself

Why this is okay:

- Groove stores a profile even if Melody is not there yet
- Groove is not its own audio layer

## Test Path 4: Create The First Groove

### Step 5. Switch To Groove

1. Press the `Groove` button on the `PhasePanel`.
2. Confirm `Groove` is now the active phase.

What you should expect:

- the phase selection changes cleanly
- the app remains stable

### Step 6. Draw A Groove Stroke

1. Use the stylus to draw one open Groove stroke.
2. For a first pass, make it simple but clear:
   - medium length
   - not too tiny
   - not too wild
3. Finish the stroke so the draft card appears.

What you should expect:

- the draft card appears
- the draft is labeled as Groove
- the app remains stable

### Step 7. Commit The Groove Draft

1. Press the normal `Save` button.
2. Let playback run for at least one full 8-bar loop.

What you should expect:

- the Melody rhythm should feel different from before
- the Melody should still sound like the same tune
- the change may be subtle on a moderate Groove shape

What you should not expect:

- Harmony changing with the Groove commit
- Melody turning into obviously different notes

## Test Path 5: Confirm Pitch Stays The Same

### Step 8. Listen For Same-Tune Different-Feel Behavior

1. Keep playback running.
2. Focus on the Melody line.
3. Compare what you hear after Groove against what you heard before Groove.

What you should expect conceptually:

- the Melody should still feel recognizably the same
- some note starts may feel more delayed, more pushed, or less dense
- the overall tune should not feel replaced

You do not need to identify exact MIDI notes.

The key question is:

- does Groove feel like a rhythm change, not a pitch rewrite?

## Test Path 6: Check Sparse Groove Behavior

### Step 9. Draw A Shorter Simpler Groove Shape

1. Stay on the `Groove` phase.
2. Draw a shorter or more compact Groove stroke than before.
3. Save it.
4. Listen through the loop again.

What you should expect:

- the Melody can feel more stripped back or less busy
- the main phrase should still remain anchored
- the loop should still feel musically coherent

What you should not expect:

- the Melody disappearing completely
- the phrase losing all sense of arrival at the beginning of the loop

## Test Path 7: Check Syncopation Feel

### Step 10. Draw A Sharper More Angular Groove Shape

1. Stay on `Groove`.
2. Draw a more jagged or angular stroke.
3. Save it.
4. Listen closely to the off-beat feel.

What you may hear:

- some Melody notes feel a little more pushed or pulled
- the rhythm can feel less straight and more lively

What you should not expect:

- total rhythmic chaos
- Melody notes jumping into obviously wrong places

This is a timing-feel change, not a broken quantization effect.

## Test Path 8: Verify Groove Replacement

### Step 11. Draw A Second Clearly Different Groove

1. Stay on `Groove`.
2. Draw a second Groove stroke that is clearly different from the first.
3. Save it.

What you should expect:

- the Groove feel updates
- the new Groove should replace the previous Groove pattern
- only the latest Groove pattern should remain after a normal redraw

### Step 12. Check Scene Cleanup

1. Look at the Groove visual in the scene.
2. Confirm the previous Groove pattern is gone.

What you should expect:

- only the newest Groove pattern remains

## Test Path 9: Verify Groove Still Applies After Melody Changes

### Step 13. Redraw Melody After Groove Is Already Saved

1. Keep your committed Groove.
2. Switch back to `Melody`.
3. Draw and save a clearly different Melody stroke.
4. Listen again.

What you should expect:

- the new Melody should be heard
- Groove should still shape that new Melody rhythmically
- the app should remain stable after the Melody commit

Why this matters:

- Groove is now a stored composition setting
- it should keep applying even when Melody is redrawn later

## Trouble Signs

Report it as a Phase F issue if you observe any of these:

- saving Groove changes Melody pitch content in an obvious way
- Groove commit crashes the app or leaves the UI unstable
- Groove with an existing Melody causes no audible timing or feel change at all
- a second Groove save leaves the old Groove pattern behind after a normal `Save`
- Groove without Melody creates a separate audible layer by itself
- after Groove is active, redrawing Melody makes the app stop reflecting Groove entirely

## What Not To Treat As A Phase F Failure

These are not Phase F failures by themselves:

- Bass still not behaving like a dedicated bass voice yet
- Percussion still using older logic
- Groove not creating an extreme wall of extra notes
- Harmony and Melody still doing the musical jobs introduced in earlier phases

Those belong to later phases or later refinement passes.

## Short Summary For A Quick Manual Pass

If you only have a few minutes, do this:

1. Launch the app and confirm guided mode still starts normally.
2. Make sure Harmony and Melody are committed.
3. Draw and save one Groove stroke.
4. Listen for a rhythm/feel change in Melody without a pitch rewrite.
5. Draw and save a second Groove stroke.
6. Confirm the Groove feel changes and the old Groove pattern is replaced.
