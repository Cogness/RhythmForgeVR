# Phase G User Instructions

## Purpose

This guide explains how to manually test the Phase G Bass feature inside the VR app.

It is written for a novice user. You do not need music theory knowledge to follow it. When this guide says "root note", you can read that as "the main low note that makes the chord feel grounded."

## What Phase G Adds

The Bass phase lets you draw one shape that becomes the low-end line of the 8-bar guided piece.

What the app should now do:

- each bar starts with the correct low root note for the current chord
- a taller Bass stroke tends to add another bass note around beat 3
- a wider Bass stroke tends to hold notes longer
- a longer Bass stroke tends to make the bass line busier
- a strong left-to-right motion in the Bass stroke tends to create upward end-of-bar movement
- redrawing Bass replaces the previous Bass pattern instead of layering another bass part on top

What the app should not do:

- it should not create two different committed Bass patterns at the same time in guided mode
- it should not move the beat-1 bass note away from the current chord root
- it should not place normal bass notes outside G major
- it should not require Groove to exist before Bass can work

## Concepts In Plain Language

`Beat 1`:
The first pulse of each bar. In this feature, Bass must always hit the chord root here.

`Root note`:
The note that names the chord. For the guided loop, the bass roots you should hear are mainly `G`, `E`, `C`, and `D` across the 8 bars.

`Fifth`:
Another stable note from the same chord. Phase G may add it around beat 3 when the Bass stroke is tall enough.

`Walk`:
A short stepping motion near the end of a bar that leads toward the next bar.

## Before You Start

Recommended setup:

1. Launch the app into the guided flow.
2. Start from a fresh guided session if possible.
3. Draw Harmony first.
4. Draw Melody second.
5. Draw Groove if you want, but Groove is optional for testing Bass.
6. Go to the `Bass` phase before drawing the Bass stroke.

Why this order helps:

- Harmony gives Bass the chord path it must follow
- Melody makes it easier to hear whether the new Bass part supports the piece
- Groove is optional because Bass does not depend on Groove for note generation

## Manual Test 1: Basic Bass Commit

Goal:

- prove that Bass is now a real phase and produces audible low notes

Steps:

1. Enter the `Bass` phase in the phase panel.
2. Draw a simple medium-length stroke.
3. Commit the draft.
4. Press play for the full piece.

Expected result:

- you hear a low bass part under the chords and melody
- the bass repeats across the 8-bar loop
- the low notes feel matched to the harmony instead of sounding random

Not expected:

- silence after commit
- a bright lead-like piano timbre instead of a bass timbre
- multiple stacked bass patterns from one commit

## Manual Test 2: Beat-1 Root Lock

Goal:

- confirm that every bar starts on the chord root

Steps:

1. Keep the Bass phase active.
2. Draw and commit any Bass shape.
3. Press play.
4. Listen to the start of each bar.

What to listen for:

- the bass should strongly "land" at the start of every bar
- across the loop, the low-note motion should feel like `G -> E -> C -> D -> G -> E -> C -> D`

Expected result:

- the bass feels grounded and predictable at each bar start
- even if the middle of the bar becomes busier, the start of the bar remains stable

Not expected:

- a bar beginning with a note that obviously clashes with the chord
- the first bar or fifth bar starting on anything other than a strong root note

## Manual Test 3: Redraw Replacement

Goal:

- confirm that Bass follows the one-shape-per-phase rule

Steps:

1. Commit a Bass stroke.
2. Press play and listen for a few seconds.
3. Return to `Bass`.
4. Draw a very different Bass stroke and commit it.
5. Press play again.

Expected result:

- the second Bass stroke replaces the first one
- you hear one bass line, not two bass lines layered together

Not expected:

- the old bass still playing together with the new one
- duplicate Bass shapes visible as separate committed phase results

## Manual Test 4: Tall Shape Adds More Harmonic Weight

Goal:

- verify the beat-3 fifth behavior

Steps:

1. In the `Bass` phase, draw a noticeably tall stroke.
2. Commit it and play the piece.
3. Redraw with a much flatter stroke.
4. Commit again and replay.

Expected result:

- the taller stroke should feel more active or supportive in the middle of each bar
- the flatter stroke should feel simpler and more root-focused

What this means musically:

- the tall stroke is more likely to add the chord fifth on beat 3

## Manual Test 5: Width Changes Sustain Length

Goal:

- verify that horizontal spread affects note length

Steps:

1. Draw a narrow Bass stroke and commit it.
2. Play the piece and notice whether notes feel shorter and more separated.
3. Draw a wider Bass stroke and commit it.
4. Play again.

Expected result:

- the wider stroke should hold notes longer
- the narrow stroke should feel shorter or more pulsed

Not expected:

- both strokes sounding identical in sustain

## Manual Test 6: Long Stroke Creates More Motion

Goal:

- verify the density mapping

Steps:

1. Draw a short, simple Bass stroke and commit it.
2. Play the piece.
3. Draw a longer Bass stroke and commit it.
4. Play again.

Expected result:

- the shorter stroke should sound more like root holds
- the longer stroke should sound busier, with extra movement inside the bar

## Manual Test 7: Direction Bias Changes Walk Direction

Goal:

- verify end-of-bar walk behavior

Steps:

1. Draw a stroke with a clear left-to-right travel and commit it.
2. Play the piece and listen near the end of bars.
3. Redraw with a stroke that clearly travels the opposite way and commit it.
4. Play again.

Expected result:

- the stronger left-to-right stroke should feel like it climbs more near bar endings
- the opposite-direction stroke should feel less upward, or more downward, near bar endings

Important note:

- this is a subtle musical cue, not a dramatic melody rewrite
- the strongest invariant is still the root note on beat 1 of every bar

## Manual Test 8: Harmony Redraw Updates Bass

Goal:

- confirm that Bass follows Harmony changes

Steps:

1. Commit Harmony, Melody, and Bass.
2. Play the piece once so you get familiar with the bass movement.
3. Go back to `Harmony`.
4. Redraw Harmony with a clearly different shape and commit it.
5. Return to playback.

Expected result:

- the bass should still land correctly with the updated harmony
- the bar starts should continue to feel musically locked to the chords

Not expected:

- old bass roots surviving after a Harmony redraw
- the bass sounding disconnected from the new chord bed

## Troubleshooting

If you do not hear Bass clearly:

- make sure you actually committed the Bass draft
- make sure playback is running
- make sure Harmony exists, because Bass is easiest to judge when chords are present
- try a fresh guided session and repeat the test in order: Harmony -> Melody -> Bass

If the Bass feels too subtle:

- draw a longer or taller stroke
- listen on speakers or headphones with enough low-end

If you think two bass lines are playing:

- redraw the Bass phase again with a very different shape
- the old bass should disappear completely after the new commit

## Success Criteria

Phase G is working correctly if all of the following are true:

- Bass commit creates an audible low-end part
- there is at least one bass hit at the start of every bar
- Harmony redraw keeps Bass musically aligned with the progression
- Bass redraw replaces the previous Bass pattern
- taller, wider, and longer strokes produce meaningfully different bass behavior without breaking musical safety
