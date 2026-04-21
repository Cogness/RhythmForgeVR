# Phase B User Instructions

## Purpose

This guide explains how to manually test Phase B from the VR app.

Phase B is not a visible guided-creation release yet. It is a data and state foundation release. That means most of the app still looks and behaves like the current free-form version, but the session now carries a guided composition model behind the scenes.

## What Phase B Changes In Plain Language

Phase B adds a hidden guided composition structure with fixed defaults:

- key: `G major`
- tempo: `100 BPM`
- bars: `8`
- progression: `G, Em, C, D, G, Em, C, D`

Important:

- there is no new phase UI yet
- there is no new phase button flow yet
- there is no automatic one-shape-per-phase workflow yet
- drawing and playback should still feel like the old app

## What To Expect

You should expect:

- the app still boots and plays like the existing free-form build
- old drawing modes still work
- saving and loading should still work
- the legacy demo still behaves like the legacy demo

You should not expect:

- a Harmony → Melody → Groove → Bass → Percussion guided workflow
- visible composition-phase controls
- a new beginner onboarding sequence
- audible Phase B-specific musical changes

## Important Startup Note

By default, the bootstrapper still loads the legacy demo session on start.

That means:

- if you press Play and enter the VR app normally, you will probably land in the demo session first
- that is expected
- the demo is not the same thing as a fresh empty guided-default session

So this guide has two test paths:

1. default boot and regression checks
2. fresh-session guided-default checks

## Test Path 1: Default Boot And Regression Check

Use this path first.

### Step 1. Launch The App

1. Open the usual RhythmForgeVR scene your team uses.
2. Enter Play Mode.
3. Put on the headset.
4. Wait for the normal RhythmForge interface to appear.

What you should expect:

- the app launches normally
- the interaction panels appear as usual
- you are likely placed into the demo session automatically

What you should not expect:

- a new guided startup screen

### Step 2. Confirm The App Still Feels Like The Existing Build

1. Look around the UI.
2. Find the normal drawing and transport controls.
3. Confirm there is no new phase-navigation panel yet.

What this means:

- Phase B changed the session model, not the visible workflow

### Step 3. Draw A Pattern In Any Existing Mode

1. Use the normal mode-selection flow.
2. Draw a stroke.
3. Commit it using the normal commit flow.
4. Play it back.

What you should expect:

- drawing still works
- committing still works
- playback still works
- the app should not feel broken or blocked by the new guided data model

What you should not expect:

- phase locking
- one-shape-per-phase replacement rules
- progression-aware melody behavior yet

### Step 4. Save And Reload

1. Use your normal save flow if your build exposes it.
2. Stop Play Mode or relaunch the scene if that is your standard team workflow.
3. Start the app again.
4. Confirm the session still loads and the app remains stable.

What you should expect:

- save/load should still behave normally
- older behavior should remain intact

## Test Path 2: Fresh Guided-Default Session Check

This path is for checking the new hidden guided defaults.

Because the app still auto-loads the demo by default, a fresh empty session may require one of these:

- a session reset action, if your current build exposes one
- launching a build where demo-on-start is disabled
- clearing the saved session before launch in the team workflow you already use

If you do not have one of those options available, skip this path and note that the fresh guided-default state could not be inspected from the current VR boot path.

### Step 5. Start From A Fresh Empty Session

1. Use the project’s reset or new-session flow if available.
2. Enter the VR app again.
3. Confirm you are not looking at the pre-seeded demo content.

What you should expect:

- an empty session with no committed demo patterns
- the app still looks like the free-form build

### Step 6. Check Tempo And Key If The Current UI Shows Them

1. Look at the transport panel.
2. Read the BPM label.
3. Read the key label.

What you should expect in a fresh empty session:

- tempo should read `100 BPM`
- key should read `G major`

Important:

- this only applies to a fresh empty session created from the new Phase B baseline
- it does not apply to the legacy demo session, which still uses its own older musical setup

### Step 7. Confirm Nothing New Is Visibly Guided Yet

1. Look for phase indicators, current-phase banners, or step buttons.
2. Try drawing normally.

What you should expect:

- no phase panel
- no auto-advance
- no special guided prompts
- the session is guided-ready internally, but not guided visually yet

## Test Path 3: Demo Session Specific Check

This path confirms the demo was intentionally left as a legacy demo.

### Step 8. Load Or Observe The Demo Session

1. If the app booted into the demo automatically, stay there.
2. If not, use the project’s demo-session load path if your build exposes it.
3. Observe the seeded demo patterns.

What you should expect:

- the demo still sounds and behaves like the old demo
- it is not transformed into a guided five-phase composition flow

Why this matters:

- Phase B keeps the demo stable while preparing the real guided flow for later phases

## Concepts Explained For A Novice Tester

### What “Guided Defaults” Means

It means a brand-new empty session now starts with a predefined musical foundation in the data:

- one key
- one tempo
- one fixed 8-bar chord loop

This is groundwork for later guided creation steps.

### What “Foundation Phase” Means

It means the team changed the app’s internal model first before changing the visible workflow.

That is why:

- the code changed
- the saved session format changed
- but the VR experience mostly still looks familiar

### Why The Demo Still Looks Old

That is intentional.

The demo is still being used as a stability and regression sample. Phase B does not try to convert it into the new guided experience yet.

## Pass / Fail Summary

Pass if:

- the app boots
- the existing free-form workflow still works
- save/load does not regress
- the demo still works
- a fresh empty session, when you can access one, shows `100 BPM` and `G major`

Fail if:

- the app crashes or panels break after startup
- drawing or committing stops working
- save/load breaks
- the demo behaves unpredictably
- a fresh empty session shows obviously wrong defaults such as missing key, missing tempo, or missing session state

## What To Report Back

When reporting results, include:

- whether you tested the default demo path
- whether you were able to access a fresh empty session
- whether the transport panel showed `100 BPM` and `G major`
- whether drawing, commit, playback, save, and reload all still worked
- whether you saw any visible guided UI that should not exist yet
