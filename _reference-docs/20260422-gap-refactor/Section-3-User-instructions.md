# Section 3 User Instructions

## Purpose

This guide explains how to manually test the Section 3 music-behavior changes inside the VR app.

You do not need music theory experience. The goal is to confirm the app now behaves in a more beginner-safe and musically clear way.

## Before You Start

- Open the project in Unity.
- Enter Play Mode with your normal VR rig or simulator.
- Start from a fresh guided session if possible.
- Use headphones if you can. Several of these checks are easier to hear than to see.

## A Few Beginner Concepts

### What “8 bars” means here

- The loop has 8 equal chunks.
- You can count each one as: `1 2 3 4`.
- Bars 1–4 are the first half.
- Bars 5–8 are the second half.

### What “backbeat” means

- The snare should feel like it lands on beats `2` and `4`.
- This is the classic pop / rock / electronic beginner-safe pulse.

### What “phrase anchor” means

- The melody should clearly restart at:
  - bar 1 beat 1
  - bar 5 beat 1
- Even if the drawn shape is messy, those restart points should still feel solid.

### What Groove is allowed to change

- Groove should change timing, density, and accents.
- Groove should not break the important melody foundation notes.

## Step-By-Step Test Flow

### 1. Confirm the neutral drum foundation

What to do:

1. Start a fresh guided session.
2. Go directly to `Percussion`.
3. Draw a simple balanced closed shape.
   - Example: a rounded square or simple oval-like loop.
   - Try not to make it very skinny, very jagged, or very uneven.
4. Commit it.
5. Press `Play Piece` and listen to one full 8-bar cycle.

What you should expect:

- the kick feels steady on beats `1` and `3`
- the snare feels steady on beats `2` and `4`
- the hi-hat gives a simple, readable pulse

What you should not expect:

- a strange snare on beat `3`
- a rhythm that feels like the base pulse disappeared

## 2. Confirm shape enrichments add detail without deleting the base pulse

What to do:

1. Stay in `Percussion`.
2. Redraw using a tall narrow shape.
3. Commit and play again.

What you should expect:

- you still hear the same core kick/snare foundation
- extra kick activity may appear before later beats
- the groove feels more animated, not less stable

What you should not expect:

- the base backbeat vanishing
- the new shape replacing the simple foundation with something unrelated

## 3. Confirm the melody has clear bar-1 and bar-5 restarts

What to do:

1. Commit `Harmony` first.
2. Go to `Melody`.
3. Draw a messy line with some ups and downs.
4. Commit it.
5. Play the full 8-bar loop a few times.

What you should expect:

- the melody clearly starts on bar 1
- the melody clearly restarts or re-anchors on bar 5
- the melody sounds connected to the chords instead of random

What you should not expect:

- bar 5 feeling like the melody forgot to re-enter
- the melody sounding “lost” when the second half begins

Beginner listening tip:

- count the loop as `1 2 3 4`, then again for each new bar
- when the second half begins, you should hear a clear melodic landing point, not empty drift

## 4. Confirm positive-tilt melody shapes lift the answer phrase

What to do:

1. Stay in `Melody`.
2. Draw a shape that clearly tilts upward from left to right.
3. Commit it.
4. Play the full loop.
5. Compare bars 1–4 with bars 5–8.

What you should expect:

- bars 5–8 feel a bit more lifted or elevated than bars 1–4
- the second half sounds like a variation or answer, not a perfect copy

What you should not expect:

- only bars 5–6 changing while bars 7–8 drop fully back to the earlier contour

## 5. Confirm Groove can thin the melody without breaking the important notes

What to do:

1. Make sure `Harmony` and `Melody` are already committed.
2. Go to `Groove`.
3. Draw a sparse-feeling Groove shape.
   - Example: shorter path, less busy stroke.
4. Commit it.
5. Play several loops.

What you should expect:

- some melody notes may feel fewer or more spaced out
- the key melodic landing points still feel stable
- bars 1 and 5 still feel anchored

What you should not expect:

- the strong melody foundation disappearing
- the melody losing its obvious restart at bar 5

Beginner explanation:

- Groove is allowed to simplify or reshape the melody rhythm
- Groove is not supposed to delete the notes that make the phrase feel grounded

## 6. Confirm Harmony cadences stand out on bars 4 and 8

What to do:

1. Commit `Harmony`.
2. Play the loop and focus on the ends of bar 4 and bar 8.
3. Redraw Harmony with a simple balanced shape and compare again.

What you should expect:

- the end of bar 4 feels like a lead-in to bar 5
- the end of bar 8 feels like a lead-in back to bar 1
- those two cadence points should feel more intentional than the middle bars

What you should not expect:

- all 8 bars feeling harmonically identical in shape and weight

## 7. Confirm the rhythm section still locks together

What to do:

1. Commit `Bass`.
2. Keep `Percussion` committed.
3. Play the loop and focus on the first beat of bar 1 and bar 5.

What you should expect:

- Bass and kick feel tightly together at the start of the bar
- bar 1 and bar 5 both feel solid and grounded

What you should not expect:

- the bass arriving late or feeling disconnected from the kick on those entry points

## 8. Confirm percussion pickups can feel slightly pushed, not only delayed

What to do:

1. Use a more active `Percussion` shape with some asymmetry or detail.
2. Keep `Groove` committed with a noticeable swing feel.
3. Listen especially near fills and late-bar pickup hits.

What you should expect:

- some pickup hits can feel like they lean forward into the next beat
- the groove should not feel like every timing change is only “late”

What you should not expect:

- every swing effect feeling like a simple delay only

Important note:

- this is a subtle change
- it is easiest to hear on fills and turnaround moments, not on every single hit

## Fast Retest Checklist

If you want the shortest useful pass, do this exact sequence:

1. Fresh guided session
2. Commit neutral `Percussion` and confirm:
   - kick on `1` and `3`
   - snare on `2` and `4`
3. Commit `Harmony`
4. Commit upward-tilting `Melody`
5. Play and confirm bars 5–8 feel more lifted than bars 1–4
6. Commit sparse `Groove`
7. Play and confirm the main melody anchors still survive
8. Commit `Bass`
9. Play and confirm kick + bass feel locked on bar 1 and bar 5

## If Something Looks Or Sounds Wrong

Report these as likely regressions:

- neutral percussion does not give a normal backbeat
- the snare feels centered on beat `3`
- bar 5 no longer feels like a clear melody restart
- Groove makes the melody lose its main anchor notes
- bars 4 and 8 do not feel any more cadential than other bars
- Bass and kick stop feeling aligned on the first beat of major phrase starts
- fills only ever feel later, never slightly pushed

## What This Section Did Not Add

These are intentionally still absent in this pass:

- no shaker lane
- no tambourine lane
- no new crash-cymbal system
- no fully separate arrangement editor for bars 1–8

That means you should expect better musical behavior from the existing guided layers, not a brand-new instrument feature set.
