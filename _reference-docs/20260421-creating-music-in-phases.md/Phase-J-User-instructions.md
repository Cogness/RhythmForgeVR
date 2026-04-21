# RhythmForge VR — Manual Test Guide (Phase J)

## Who this is for

This guide is for someone who is new to RhythmForge VR and wants to verify that the Phase J cleanup did not break anything in the app. You do not need to understand music theory deeply. Everything you need to do is described step by step.

Phase J was a code cleanup — no new musical features were added. The goal of this test is to confirm that the app still works exactly as it did after Phase I.

---

## What Phase J Changed (plain language)

Phase J removed some old scaffolding code that was no longer needed:

1. A small compatibility file called `DemoSession.cs` was deleted. It was just forwarding calls to the real code (`GuidedDemoComposition`). You will not notice any difference.
2. The harmony system was cleaned up so it only stores chord data in one place instead of two. Again, you will not hear or see any difference — this was internal bookkeeping.
3. A test was updated to check the right data structure after the cleanup.
4. Two panels (Scene Strip and Arrangement) were confirmed to be correctly hidden when you are in guided composition mode.

---

## Before You Start

Make sure you have:
- A Meta Quest headset with RhythmForge VR installed, OR the Unity editor open in Play Mode
- The MX Ink stylus (or the simulated stylus in the editor)
- A fresh session ready to load

---

## Part 1: App Starts in Guided Composition Mode

**What you are checking:** The app starts correctly and shows the guided composition panels.

Steps:
1. Put on the headset (or press Play in the Unity editor).
2. Wait for the scene to load — this usually takes 3–5 seconds.
3. Look around. You should see a set of floating panels in front of you.

What you should see:
- A **Phase Panel** — a panel with 5 buttons labeled **Harmony**, **Melody**, **Groove**, **Bass**, and **Percussion**. The Harmony button should be highlighted in yellow (it is the starting phase).
- A **Transport Panel** — shows the key (G major) and tempo (100 bpm) as labels. They should not be clickable buttons — just text.
- A **Dock Panel** — a small panel for draw mode switching. In guided mode it should not show a "cycle mode" button.

What you should NOT see:
- A "Scene A / B / C / D" strip panel — this is hidden in guided mode.
- An "Arrangement" panel with 8 slots — also hidden in guided mode.

If you do see the Scene or Arrangement panels, something is wrong and needs investigation.

---

## Part 2: Drawing Each of the Five Phases

This is the core test. You will draw one shape per phase and confirm that the app responds correctly.

### Concept: What drawing means

In RhythmForge, you draw shapes by pressing the stylus tip against the air and moving it. The shape you draw controls the sound that gets generated. You do not need to draw anything specific — any shape will work for this test.

### Phase 1 — Harmony

1. The Harmony phase button should already be highlighted (yellow).
2. Draw any shape in the air. A glowing line should appear while you draw.
3. Release the stylus tip. You should see a **commit card** appear — it shows you a summary of the pattern you just created.
4. Tap the **Save** button on the commit card.

What to verify:
- The Harmony phase button turns **green** (this means the phase has a committed shape).
- A floating glowing shape (the pattern instance) appears in the scene.
- The commit card disappears after saving.

### Phase 2 — Melody

1. Tap the **Melody** phase button on the Phase Panel.
2. The button should turn yellow (active).
3. Draw any shape. Commit it with **Save**.

What to verify:
- The Melody button turns green.
- A new shape appears in the scene, different from the Harmony shape.

### Phase 3 — Groove

1. Tap the **Groove** phase button.
2. Draw any shape. Commit it with **Save**.

What to verify:
- The Groove button turns green.
- The melody's rhythm may change slightly when you play the piece (the groove modulates the melody's timing — this is expected).

### Phase 4 — Bass

1. Tap the **Bass** phase button.
2. Draw any shape. Commit it with **Save**.

What to verify:
- The Bass button turns green.
- A new shape appears in the scene.

### Phase 5 — Percussion

1. Tap the **Percussion** phase button.
2. Draw any shape. Commit it with **Save**.

What to verify:
- The Percussion button turns green.
- All 5 phase buttons are now green.

---

## Part 3: Playing the Full Composition

**What you are checking:** All 5 layers play together as a coherent piece.

1. On the Phase Panel, tap the **Play Piece** button (it may say "Play Piece" or have a play symbol).
2. Listen for a few seconds.

What you should hear:
- Chords ringing out and changing every 1–2 seconds as the 8-bar loop cycles through G – Em – C – D.
- A melody line playing on top of the chords.
- A bass line (lower-pitched notes following the chord roots: G, E, C, D).
- A drum pattern: kick on beats 1 and 3, snare on beats 2 and 4, hi-hat on every 8th note.
- The rhythm of the melody may be shifted or thinned depending on the Groove shape you drew.

What you should NOT hear:
- Silence after committing all 5 phases.
- Any audio glitches when the 8-bar loop restarts.

3. Tap the **Stop Piece** button (same button, label changes when playing) to stop.

---

## Part 4: Clearing and Redrawing a Phase

**What you are checking:** Phase clearing and redrawing work correctly.

1. Look at the Harmony phase instance floating in the scene.
2. Reach out and grab it (use the left controller trigger or the grab input for your setup).
3. With the instance selected, look at the **Inspector Panel** — it should show three buttons: **Redraw**, **Adjust**, and **Clear**.
4. Tap **Clear**. The Harmony shape should disappear from the scene. The Harmony button on the Phase Panel should turn gray (empty).
5. Now draw a new Harmony shape and commit it. The Harmony button should turn green again.

What to verify after clearing Harmony:
- The Melody and Bass buttons may briefly show the word **Pending** appended to their labels. This is expected — it means the app is re-calculating those phases against the new harmony. The label goes back to normal once the re-calculation finishes (usually within 1–2 seconds).

---

## Part 5: Auto-Advance on Save

**What you are checking:** The auto-advance feature moves you to the next phase after saving.

1. Make sure all phases are cleared (you can tap **Clear** on each committed instance, or use the top-level reset if one is available).
2. Return to the Harmony phase.
3. Look at the **Commit Card** panel — there should be a button that says **Auto Next ON** or **Auto Next OFF**. Toggle it to **ON**.
4. Draw a Harmony shape and tap **Save** on the commit card.

What to verify:
- The app automatically advances to the Melody phase (Melody button turns yellow) without you tapping the Melody button yourself.
- This should also happen when you save Melody (advances to Groove), save Groove (advances to Bass), and save Bass (advances to Percussion).
- Percussion has no "next" phase — it wraps back to Harmony.

---

## Part 6: Checking That Free-Mode Panels Are Hidden

**What you are checking:** The Scene Strip and Arrangement panels are correctly hidden in guided mode.

This is a Phase J-specific verification. After all the steps above:
1. Scan all the floating panels visible in the scene.
2. Confirm you do not see:
   - A panel with "Scene A", "Scene B", "Scene C", "Scene D" buttons.
   - A panel with 8 "Slot 1" through "Slot 8" arrangement rows.
3. The only panels you should see are: Phase, Transport, Dock, Toast (briefly, when messages appear), Inspector (when something is selected), Commit Card (when a stroke is pending commit), Genre Selector.

---

## What to Do If Something Is Wrong

### The app does not start / crashes on load
- Check the Unity console (if in editor) for red error messages.
- A common cause is a missing script reference — look for "NullReferenceException" and note the file name it points to.

### A phase button stays gray after saving
- This means the commit did not register. Check if the Commit Card appeared after drawing. If not, the stroke may have been too short — try drawing a larger shape.

### The harmony sounds out of tune or plays wrong notes
- This is a possible regression. Note the phase you were in, the shape you drew (size, direction), and the audio you heard. This helps the developer reproduce the bug.

### The Scene Strip or Arrangement panel IS visible
- This is a regression introduced by Phase J or an earlier phase. The `ApplyGuidedModeUiState()` method in `RhythmForgeManager` is responsible for hiding these panels. Check that `_store.State.guidedMode` is `true` when the session loads.

### "Pending" badge never goes away on Melody or Bass after clearing Harmony
- This means the background re-derivation task never completed. Check the console for errors in `SessionStore.OnRederivationComplete` or similar re-derive callbacks.

---

## Concepts Explained for Novice Testers

**Phase**: One of the five musical layers in the composition (Harmony, Melody, Groove, Bass, Percussion). Each phase is controlled by one drawn shape.

**Chord progression**: The sequence of chords the piece moves through. In RhythmForge guided mode it is always G major – E minor – C major – D major, repeating twice over 8 bars.

**Bar**: One unit of music with 4 beats. At 100 bpm there are 100 bars per minute, so each bar is about 2.4 seconds. The guided piece is 8 bars long, so the full loop is about 19 seconds.

**Chord tones**: The specific notes that belong to the current chord. On "strong beats" (beats 1 and 3 of each bar) the melody always lands on a chord tone, which is why it never sounds wrong regardless of how you draw it.

**Groove**: In this app, Groove does not add new notes — it changes the *timing and density* of the melody notes. A rounder, smoother shape produces a more even groove; an angular shape produces a more syncopated (off-beat) feel.

**Commit**: After drawing a shape, you confirm it by pressing "Save" on the commit card. Until you commit, the shape is a draft and has no audio.

**Redraw**: Clearing a committed phase and immediately entering it again so you can draw a new shape. The old audio disappears; new audio is generated when you commit the new shape.

**Pending**: Shown briefly on a phase button after an upstream phase changes. It means the app is recalculating that phase's sound in the background. You do not need to do anything — it resolves automatically.
