# Spatial Audio User Exercise

Status: user-facing onboarding exercise, April 2026
Scope: quick hands-on walkthrough for the spatial-audio follow-up work, including zones, spatial placement, MX Ink expression flags, and the new feedback loop.

---

## 5-Minute Exercise

This is the fastest way to feel what changed.

### 1. Reset your space

Stand where you want to work and long-press `Y` for about 1 second.

This recenters:
- the panels
- the zone layout

You should now have the room organized around you:
- drums low/front
- melody front-center
- harmony behind/farther out

### 2. Draw a rhythm

Set draw mode to `Rhythm`.
Draw a simple closed or punchy shape and save it.

What to notice:
- it should auto-land near the drum zone
- when it plays, it should feel low/front and fairly direct
- if you grab it, the sound should stay attached to it in space

### 3. Draw a melody

Set draw mode to `Melody`.
Draw a more open, directional shape and save it.

What to notice:
- it should auto-land in front of you
- it should feel more like a lead voice in the room
- turning your head should change how it localizes

### 4. Draw a harmony

Set draw mode to `Harmony`.
Draw a broader shape and save it.

What to notice:
- it should auto-land behind or farther back
- it should feel wetter and less direct than the melody
- now you should hear three separate roles in space, not one flat mix

### 5. Re-orchestrate with your hand

Start playback if it is not already running.
Grab each pattern with the left trigger and move it while it plays.

Try this:
- pull melody closer
- push harmony farther away using left thumbstick `Y` while grabbing
- keep rhythm low/front

Listen for:
- closer = more present
- farther = more ambient
- moving left/right/front/back changes where it lives around your head

### 6. Feel the zones

Now deliberately drag one pattern into a "wrong" place.

For example:
- move harmony into the front lead area
- move melody into the far pad area

You’re listening for two things:
- spatial location changes immediately
- the zone also nudges the mix feel: wetter/drier, more/less present

This teaches the difference between:
- physical position
- zone character

### 7. Learn the new stroke feedback

Draw a throwaway stroke without saving it.

While drawing:
- press harder: the sidetone gets louder
- raise/lower the stylus: pitch shifts
- tilt the pen: the stroke color warms and thickens slightly

This is your input monitor. Use it to learn what the system is reading before you commit.

### 8. Make an ornamented pattern

Draw a new pattern, but while drawing, squeeze `middle pressure` for a moment.
Save it.

Check:
- the commit card should show `Ornamented`
- the inspector should also show the badge after selection
- on playback, it should sound more decorated than the plain version

Use this on:
- rhythm for flams
- melody for passing tones
- harmony for shimmer/motion

### 9. Make an accented pattern

Hold the `back button` and make a short jab-like stroke.
Save it.

Check:
- it should show `Accented`
- it should feel punchier or more emphasized than a normal stroke

Use this when you want:
- more attack
- a stab
- a stronger rhythmic gesture

### 10. Final listening pass

Make a tiny trio:
- one plain rhythm
- one ornamented melody
- one harmony pushed far back

Then walk around them and ask:
- Can I point to where each one is?
- Does melody feel like the front voice?
- Does harmony feel like atmosphere?
- Does rhythm still anchor the room?

If yes, you’re using the new system correctly.

---

## What This Teaches

By the end of this exercise, the user should understand:
- drawing creates musical material
- zones suggest orchestration
- grabbing performs mixing in 3D
- middle-pressure adds decoration
- back-button jab adds emphasis
- the room itself is now part of composition

---

## Teacher Script

Use this when demoing the feature live to someone else.

### Goal

Show that RhythmForge is no longer just generating patterns into a flat stereo field.
Patterns now behave like sound objects in a room, and the pen has expressive modifiers.

### Recommended setup

- user wearing headphones or headset speakers in a quiet room
- teacher standing slightly off-axis so the user can focus on the sound field
- start from a recentered layout

### 1. Open with the one-sentence framing

Say:

"You’re no longer placing loops on a panel. You’re placing musical objects in space."

Then long-press `Y` and say:

"This recenters both the controls and the musical room around you."

### 2. Show the three basic roles

Draw and save:
- one rhythm
- one melody
- one harmony

Say:

"The system auto-places these by role. Rhythm wants to ground the room, melody wants to lead from the front, and harmony wants to sit farther back."

Ask the user to listen before touching anything.

### 3. Demonstrate head-relative audio

Have playback running.
Tell the user:

"Now turn your head slowly left and right."

Explain:

"The sounds stay anchored to the shapes, not to a stereo mix. That’s the first big change."

### 4. Demonstrate manual orchestration

Grab the melody and bring it closer.
Push the harmony farther away with left thumbstick `Y`.

Say:

"You mix with distance now. Closer becomes more present. Farther becomes more atmospheric."

Then move one object across the room and say:

"Left/right/front/back are physical now, not fake pan values."

### 5. Explain zones clearly

Move a pattern into a different zone.

Say:

"Zones are soft suggestions, not rules. They don’t trap the pattern. They gently bias its character."

Then explain:

"So there are really two things happening at once: where the sound literally is, and what kind of mix behavior the current zone encourages."

### 6. Show the pen feedback loop

Draw a stroke without saving it.

While drawing, narrate:
- "Pressure makes the sidetone louder."
- "Height shifts pitch."
- "Tilt changes the line color and width."

Say:

"This is there so you can feel what the instrument is reading before you commit the gesture."

### 7. Demonstrate ornament

Draw a pattern while squeezing middle pressure.
Save it.

Say:

"This stroke is ornamented. It tells the system: decorate this phrase."

Then describe what to listen for:
- rhythm: flams
- melody: passing tones
- harmony: shimmer/motion

### 8. Demonstrate accent

Hold back button and make a short jab stroke.
Save it.

Say:

"This is an accented stroke. It’s the fast way to ask for a punchier, more emphatic musical result."

### 9. Let the user do one full pass

Ask the user to do this sequence:
1. draw one rhythm
2. draw one melody
3. move melody closer
4. make one ornamented pattern
5. make one accented pattern

Do not explain too much during this step.
Only correct them if they miss the input gesture.

### 10. Close with the intended mental model

Say:

"The core idea is: draw for material, place for orchestration, and use pressure/buttons for expression."

If they seem comfortable, add:

"Once that clicks, the room itself becomes part of the composition."

---

## Teacher Notes

- If the user does not immediately hear the difference between zones and position, exaggerate it:
  move harmony very far back and pull melody very close.
- If the user forgets the ornament gesture:
  remind them that ornament is `middle pressure during the stroke`.
- If the user forgets the accent gesture:
  remind them that accent is `back button + short jab stroke`.
- If the user gets lost in the room:
  long-press `Y` again and restart from the three-role demo.
