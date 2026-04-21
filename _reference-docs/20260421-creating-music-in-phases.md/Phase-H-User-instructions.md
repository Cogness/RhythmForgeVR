# Phase H User Instructions

## Purpose

This guide explains how to manually test the Phase H Percussion feature inside the VR app.

It is written for a novice user. You do not need drumming knowledge to follow it. When this guide says `fill`, it means a short burst of extra drum hits near the end of a section to make the transition feel more exciting.

## What Phase H Adds

The Percussion phase now lets you draw one shape that becomes the drum part for the full guided 8-bar piece.

What the app should now do:

- create one full percussion pattern for the whole 8-bar loop
- always include a strong drum hit at the start of the loop
- always include a snare hit on step `8`
- add short fill activity near the end of bar 4 and bar 8
- redraw-replace the previous Percussion result instead of layering another drum pattern on top
- if Groove already exists, make some off-beat percussion hits feel slightly delayed or more swung

What the app should not do:

- it should not keep two committed Percussion patterns active at the same time in guided mode
- it should not fall back to the old small 2-bar or 4-bar rhythm loop behavior
- it should not remove the required kick at step `0`
- it should not remove the required snare at step `8`

## Concepts In Plain Language

`Kick`:
The low drum hit that feels like the main pulse.

`Snare`:
The brighter drum hit that helps the groove feel structured.

`Hi-hat`:
The fast ticking sound that gives motion and texture.

`Fill`:
A quick extra burst of hits near the end of a bar, used to lead into the next section.

`Swing`:
A timing feel where some off-beat notes arrive a little later instead of perfectly straight.

## Before You Start

Recommended setup:

1. Launch the app into the guided flow.
2. Start from a fresh guided session if possible.
3. Draw Harmony first.
4. Draw Melody second.
5. Draw Groove third if you want to test swing transfer.
6. Draw Bass fourth if you want to hear the full arrangement.
7. Go to the `Percussion` phase before drawing the Percussion stroke.

Why this order helps:

- Harmony, Melody, and Bass make it easier to hear whether the percussion supports the whole piece
- Groove is especially useful if you want to test the new swing behavior
- Percussion is easiest to judge when the rest of the loop already exists

## Manual Test 1: Basic Percussion Commit

Goal:

- prove that Percussion is now a real guided phase with a full 8-bar output

Steps:

1. Enter the `Percussion` phase in the phase panel.
2. Draw a medium-sized closed shape.
3. Commit the draft.
4. Press play and let the loop run.

Expected result:

- you hear a full drum part under the rest of the piece
- the pattern feels like an 8-bar loop, not a tiny short loop
- the percussion repeats coherently when playback loops

Not expected:

- silence after commit
- a pattern that sounds like only one or two quick hits
- two different percussion layers appearing from one commit

## Manual Test 2: Required Kick And Snare Anchors

Goal:

- confirm the safety-net hits are always present

Steps:

1. Stay in the `Percussion` phase.
2. Draw an extreme shape that feels unusual, such as very tall, very flat, or very jagged.
3. Commit it.
4. Press play and listen to the early part of the loop.

Expected result:

- the loop still starts with a clear kick at the beginning
- you still hear the required snare at step `8`
- even extreme shapes still feel structurally grounded

Not expected:

- the first kick disappearing
- the required snare disappearing
- the loop becoming chaotic just because the shape is extreme

## Manual Test 3: Fill Behavior On Bars 4 And 8

Goal:

- confirm the new transition fills were added

Steps:

1. Commit any Percussion stroke.
2. Press play and listen all the way through bar 4.
3. Keep listening through bar 8 and into the loop restart.

What to listen for:

- near the end of bar 4, you should hear a short snare-style burst that helps push into bar 5
- near the end of bar 8, you should hear another short fill before the loop cycles

Expected result:

- the middle and end of the 8-bar form feel more guided and intentional
- the transitions sound more musical than a completely flat repeating drum loop

Not expected:

- bars 4 and 8 sounding exactly the same as every other bar ending
- the fills overwhelming the whole groove

## Manual Test 4: Redraw Replacement

Goal:

- confirm that Percussion follows the one-shape-per-phase rule

Steps:

1. Commit a Percussion stroke.
2. Press play and listen for a few seconds.
3. Return to `Percussion`.
4. Draw a clearly different Percussion shape and commit it.
5. Press play again.

Expected result:

- the second Percussion result replaces the first one
- you hear one drum pattern, not two layered patterns

Not expected:

- the old and new percussion both playing together
- duplicate committed percussion results staying active in guided mode

## Manual Test 5: Shape Changes The Groove Character

Goal:

- confirm that the old useful drum-shape logic still exists inside the new guided phase

Steps:

1. Draw a flatter, more stretched shape and commit it.
2. Listen to the kick pattern.
3. Redraw with a more circular shape and commit it.
4. Listen again.
5. Redraw with a more jagged or angular shape and commit it.
6. Listen again.

Expected result:

- the kick pattern should change depending on the shape
- more angular shapes should feel busier in the high ticking layer
- the loop should still remain musically stable even when the texture changes

Not expected:

- every different shape sounding identical
- the drum part losing the required anchor hits while changing texture

## Manual Test 6: Groove Swing Reaches Percussion

Goal:

- confirm that Groove can now influence Percussion timing

Steps:

1. Draw and commit a Melody shape.
2. Draw and commit a Groove shape with a more curved or expressive feel.
3. Draw and commit a Percussion shape.
4. Press play and listen to the off-beat hat or percussion hits.
5. Go back to `Groove`.
6. Draw a different Groove shape and commit it.
7. Play again without changing Percussion.

Expected result:

- some off-beat percussion hits should feel slightly more delayed or more relaxed when Groove has stronger swing
- changing Groove should slightly change the feel of the percussion timing even if the Percussion shape itself stays the same

Important note:

- this should feel subtle
- you are listening for feel and timing, not a completely different drum pattern

## Manual Test 7: Full Piece Coherence

Goal:

- confirm the full guided piece sounds coherent end-to-end after Phase H

Steps:

1. Commit Harmony.
2. Commit Melody.
3. Commit Groove.
4. Commit Bass.
5. Commit Percussion.
6. Press play and listen through the whole 8-bar loop.

Expected result:

- the chords, melody, bass, and percussion feel like one musical idea
- the percussion supports the piece rather than fighting it
- the fills help the loop feel structured

Not expected:

- percussion feeling disconnected from the rest of the composition
- obvious layering bugs where old percussion keeps playing after redraw

## Troubleshooting

If you do not hear Percussion clearly:

- make sure you committed the Percussion draft
- make sure playback is actually running
- try a fresh guided session and commit the phases again in order

If you think the fills are missing:

- let the loop play long enough to reach the end of bar 4 and bar 8
- listen for short snare-like bursts, not huge dramatic fills

If you think two percussion layers are playing:

- redraw Percussion one more time and replay
- if the old layer still seems present, that is a bug and should be reported as a phase replacement failure

## Summary

Phase H is working correctly when one Percussion shape gives you one coherent 8-bar drum pattern, anchor hits always remain present, fills appear on bars 4 and 8, Groove can subtly affect percussion swing, and redrawing Percussion replaces the old result instead of stacking it.
