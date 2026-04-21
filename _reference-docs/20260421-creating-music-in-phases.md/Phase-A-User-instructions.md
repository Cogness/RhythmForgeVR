# Phase A User Instructions

## Purpose

This guide explains how to manually test the Phase A music-mode expansion in the VR app. It is written for someone who is new to the project and needs to understand what to do, what they should see, and what is intentionally not finished yet.

## What Phase A Changes

Before Phase A, the app exposed three music pattern types:

- RhythmLoop
- MelodyLine
- HarmonyPad

After Phase A, the app shows five runtime-facing types:

- Percussion
- Melody
- Harmony
- Bass
- Groove

Important: only the naming and runtime domain expansion are complete in this phase. `Bass` and `Groove` are currently temporary melody-backed modes. They are expected to work, but they are not yet musically unique systems.

## What To Expect In Plain Language

### Percussion

Use this when you want rhythmic or drum-like pattern behavior.

### Melody

Use this when you want a melodic line.

### Harmony

Use this when you want a harmonic or chord-support pattern.

### Bass

This is available in the UI and should work end to end, but in Phase A it still behaves like a melody-family placeholder behind the scenes.

### Groove

This is also available in the UI and should work end to end, but in Phase A it also behaves like a melody-family placeholder behind the scenes.

## What Not To Expect Yet

Do not expect the following in Phase A:

- a guided composition workflow
- a new phase-driven tutorial or creation sequence
- a truly distinct bass generation engine
- a truly distinct groove generation engine
- major visual UX changes beyond the new type names and type support

If `Bass` and `Groove` sound similar to melody behavior, that is expected for this phase.

## Before You Start

Make sure:

- the project opens successfully in Unity
- the VR headset and controllers are connected and recognized by your usual workflow
- the scene or entry point you normally use for RhythmForgeVR testing is available

If your team already has a standard way to launch the app in headset, use that existing process. This Phase A work does not add a new launcher flow.

## How To Launch The App

1. Open the project in Unity.
2. Load the normal RhythmForgeVR scene or startup scene your team uses for VR testing.
3. Start Play Mode in Unity.
4. Put on the headset and confirm the app responds as expected.
5. Wait for the main interaction UI to become available.

If the project has a team-specific bootstrap scene, use that same scene. Phase A did not change the basic app boot process.

## Manual Test Flow

Use the steps below in order.

### 1. Confirm The App Starts Normally

What to do:

1. Launch into the VR app.
2. Look for the normal drawing and transport interface.
3. Confirm there are no immediate startup errors, broken labels, or missing interaction controls.

What you should expect:

- the app loads normally
- the existing free-form flow still feels familiar
- no special Phase B style workflow appears

What you should not expect:

- a brand new guided onboarding flow

### 2. Cycle Through The Available Modes

What to do:

1. Find the mode switch control in the VR interface.
2. Press it repeatedly to cycle through the available pattern types.
3. Read each label carefully as it changes.

What you should expect:

- the visible names should now be:
  - `Percussion`
  - `Melody`
  - `Harmony`
  - `Bass`
  - `Groove`
- the old labels `RhythmLoop`, `MelodyLine`, and `HarmonyPad` should no longer be the main user-facing names

What you should not expect:

- extra phase-based prompts or composition steps

### 3. Draw And Commit A Percussion Pattern

What to do:

1. Switch to `Percussion`.
2. Draw a simple stroke or shape using the normal drawing interaction.
3. Commit or save the draft using the normal in-app flow.
4. Trigger playback if it does not start automatically.

What you should expect:

- the draft should be accepted normally
- a pattern should be created without errors
- playback should behave like the existing rhythm/percussion behavior
- labels and inspector text should refer to the mode as `Percussion`

What you should not expect:

- a completely different percussion engine from what the app already had

### 4. Draw And Commit A Melody Pattern

What to do:

1. Switch to `Melody`.
2. Draw a simple stroke or shape.
3. Commit the draft.
4. Replay the result.

What you should expect:

- normal melody-like pattern creation
- normal playback
- no errors in the pattern list, inspector, or playback flow

### 5. Draw And Commit A Harmony Pattern

What to do:

1. Switch to `Harmony`.
2. Draw a simple stroke or shape.
3. Commit the draft.
4. Replay the result.

What you should expect:

- normal harmony-like pattern creation
- no regression in existing harmony behavior

### 6. Draw And Commit A Bass Pattern

What to do:

1. Switch to `Bass`.
2. Draw a simple stroke or shape.
3. Commit the draft.
4. Replay it.
5. Open or inspect the pattern if the UI supports it.

What you should expect:

- the app accepts the mode without crashing
- the pattern is created successfully
- the pattern can be replayed
- the inspector and labels should identify it as `Bass`
- it may still sound or behave similarly to a melody-type pattern

What you should not expect:

- deep, fully distinct bass generation logic
- special bass-only UI or workflow

### 7. Draw And Commit A Groove Pattern

What to do:

1. Switch to `Groove`.
2. Draw a simple stroke or shape.
3. Commit the draft.
4. Replay it.
5. Inspect it if that is part of your normal workflow.

What you should expect:

- the mode is selectable
- the pattern is created successfully
- playback works
- the inspector and labels should identify it as `Groove`
- it may still behave similarly to melody-family output in this phase

What you should not expect:

- a fully distinct groove engine
- percussion-style uniqueness just because the word is `Groove`

### 8. Reload Or Continue A Session

What to do:

1. If your normal workflow includes save, reload, or reopening a session, do that now.
2. Confirm the app restores without error.
3. Confirm mode selection still works after reload.

What you should expect:

- old and current sessions should still load
- the app should continue using the new visible names
- there should be no obvious breakage caused by the mode-name transition

## Concepts Explained For A Novice Tester

### What “Compatibility Bridge” Means

It means the app was updated carefully so old data and older internal naming can still work while the visible runtime system moves to the new five-type model.

You may notice that the app looks mostly familiar. That is intentional.

### What “Melody-Backed Placeholder” Means

For `Bass` and `Groove`, the app already supports selecting the mode, creating a pattern, and replaying it. However, the musical logic underneath currently reuses melody-family behavior.

This means:

- the feature is present
- the workflow should be stable
- the final musical identity is not finished yet

### Why This Is Still Useful

Phase A proves that the five-type domain can exist safely across:

- mode selection
- drawing
- committing drafts
- playback
- inspection
- state persistence

That foundation is what later phases will build on.

## Quick Pass Criteria

Phase A manual testing is considered successful if:

- the app launches normally
- you can cycle through all five visible modes
- `Percussion`, `Melody`, and `Harmony` still work as before
- `Bass` and `Groove` can be selected, drawn, committed, and replayed
- no exceptions, broken labels, or obvious state-loading problems appear

## Quick Failure Signs

Flag the build if you see any of these:

- mode cycling skips `Bass` or `Groove`
- mode labels still show old runtime names in the primary UI
- choosing `Bass` or `Groove` causes errors or missing pattern creation
- old sessions fail to load after the Phase A changes
- existing `Percussion`, `Melody`, or `Harmony` behavior is clearly broken

## Final Reminder

The most important thing to remember while testing is this:

- `Bass` and `Groove` should be operational
- `Bass` and `Groove` are not yet musically distinct

If they work but still feel melody-like, that is the expected Phase A result.
