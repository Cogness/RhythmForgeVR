# Section 2 User Instructions

## Purpose

This guide explains how to manually test the Section 2 refactor inside the VR app, step by step, as a beginner.

You do not need music theory knowledge to run these checks. The goal is to confirm:

- each guided phase still works
- the app shows the correct phase status after upstream changes
- guided mode still hides old free-mode panels
- Bass and Groove still feel like their own phases

## Before You Start

- Open the project in Unity.
- Enter Play Mode with the VR rig or simulator you normally use.
- Start from a fresh guided session if possible.

What “guided session” means:

- You build one music piece in phases.
- Each phase keeps one committed drawing.
- Redrawing a phase replaces its previous drawing.

## What the Status Words Mean

You may see these labels on a phase button:

- `Current`
  - this is the phase you are working in now
- `Filled`
  - that phase already has a committed drawing
- `Pending`
  - the phase is being re-derived because Harmony changed
- `Stale`
  - the phase still exists, but Groove changed how it will sound at playback time
- `Pending/Stale`
  - both conditions are true at once

Important beginner concept:

- `Pending` means “the app is recalculating this phase”
- `Stale` means “the drawing is still there, but playback will sound different because Groove changed”

## Step-by-Step Test Flow

### 1. Confirm guided-mode layout

What to do:

1. Launch or reset into a fresh guided session.
2. Look at the main UI panels.

What you should expect:

- the `Phase` panel is visible
- the old `Scene Strip` panel is hidden
- the old `Arrangement` panel is hidden

What you should not expect:

- you should not see guided mode exposing the scene/arrangement workflow

### 2. Commit Harmony

What to do:

1. Go to the Harmony phase.
2. Draw a simple smooth line or shape.
3. Commit it.

What you should expect:

- Harmony becomes `Filled`
- the committed Harmony replaces any previous Harmony drawing
- playback now has harmonic content for the piece

What you should not expect:

- multiple Harmony shapes staying active in guided mode

### 3. Commit Melody

What to do:

1. Switch to Melody.
2. Draw a melodic stroke.
3. Commit it.

What you should expect:

- Melody becomes `Filled`
- the piece now has audible melodic notes

What you should not expect:

- Melody should not require multiple stacked melody drawings to work

### 4. Commit Groove

What to do:

1. Switch to Groove.
2. Draw a rhythm/groove stroke.
3. Commit it.

What you should expect:

- Groove becomes `Filled`
- Melody should now show `Stale`
- if Percussion already existed, Percussion should also show `Stale`
- the app should still play, but Melody timing/accent feel may change

What you should not expect:

- Groove creating its own pitched notes
- Groove deleting Melody

Beginner explanation:

- Groove changes *how* Melody is scheduled, not *which notes* Melody owns

### 5. Commit Bass

What to do:

1. Switch to Bass.
2. Draw a bass stroke.
3. Commit it.

What you should expect:

- Bass becomes `Filled`
- Bass should sound present and grounded
- Bass should still respond normally in playback after the refactor

What you should not expect:

- Bass behaving exactly like Melody visually
- Bass needing Melody to exist before it can be committed

### 6. Commit Percussion

What to do:

1. Switch to Percussion.
2. Draw a closed rhythmic shape.
3. Commit it.

What you should expect:

- Percussion becomes `Filled`
- the piece now has drum/percussion events

What you should not expect:

- Percussion depending on Harmony commit order to exist

## Important Change Checks

### A. Groove should mark downstream scheduling as stale

What to do:

1. Commit Melody.
2. Commit Groove after Melody.

What you should expect:

- Melody button shows `Stale`

If Percussion already exists:

- Percussion button also shows `Stale`

### B. Harmony redraw should trigger true pending re-derive

What to do:

1. Commit Harmony.
2. Commit Melody and Bass.
3. Redraw Harmony and commit the new Harmony.

What you should expect:

- Melody shows `Pending`
- Bass shows `Pending`
- after the background work finishes, those `Pending` labels clear
- playback should still work after that

What you should not expect:

- Melody or Bass disappearing
- the phase buttons staying permanently `Pending`

Beginner explanation:

- Harmony changes the chord foundation
- Melody and Bass must be recalculated to stay musically aligned with the new Harmony

### C. Groove and Bass should still feel distinct

What to do:

1. Compare the in-scene feel of Groove and Bass after both are committed.
2. Play the piece a few times.

What you should expect:

- Groove feels like a timing/energy phase
- Bass feels like a low-register note phase

What you should not expect:

- Groove feeling exactly like Melody
- Bass sounding routed as if it were just another Melody line

## If Something Looks Wrong

Report these as likely regressions:

- Harmony, Melody, or Bass never clear `Pending`
- Groove commit does not mark Melody as `Stale`
- guided mode shows Scene Strip or Arrangement panels
- Bass goes silent after commit
- committing a new phase creates multiple guided shapes instead of replacing the old one
- Harmony no longer plays after commit
- a Unity import error appears around the new Groove/Bass visual profile fields

## Short Retest Checklist

If you want the fastest sanity pass, run this exact sequence:

1. Fresh guided session
2. Confirm `Phase` visible, `Scene Strip` hidden, `Arrangement` hidden
3. Commit Harmony
4. Commit Melody
5. Commit Groove and confirm Melody shows `Stale`
6. Commit Bass
7. Redraw Harmony and confirm Melody + Bass show `Pending`
8. Wait for `Pending` to clear
9. Press Play and confirm the full piece still plays

## What Stayed Intentionally Unchanged

- guided mode is still the main UX
- free-mode support was not removed
- Scene Strip and Arrangement still exist in code, but stay hidden in guided mode
- the app still uses one committed drawing per guided phase

## What This Refactor Mostly Changed

- internal architecture
- harmony data model cleanup
- explicit phase invalidation events
- dedicated Groove/Bass runtime seams

This means many improvements are structural rather than dramatic UI changes. That is expected for Section 2.
