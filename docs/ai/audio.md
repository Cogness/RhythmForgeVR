# Audio Guide

Adapted from the audio-director, sound-designer, and team-audio material in Claude Code Game Studios, then narrowed to RhythmForgeVR.

## Audio Role In RhythmForge

RhythmForge is not using audio as polish layered on top of gameplay. Audio is the product. The player is building loops, melodies, and mixes directly through spatial interaction.

That means audio decisions must optimize for:
- musical coherence
- low-latency response
- expressive control through gesture
- clear sample and preset workflow
- export-ready structure instead of one-off toy effects

## Sonic Direction

Use audio choices that make the app feel like a creative instrument, not an 8-bit game or random sound toy.

Core palette goals:
- real instrument and drum samples where possible
- genre preset banks with clear identities
- variations that reduce repetition
- effects that support performance and mixing rather than masking weak source sounds

## Audio Categories

Define work against these categories:
- rhythm one-shots and loop slots
- melody and tonal instruments
- spatial mix objects and environmental feedback
- UI and interaction feedback
- transition and export feedback

## Event Architecture

Every sound should have a clear trigger source and ownership.

Recommended event groups:
- direct audition triggers
- loop transport triggers
- gesture or drawing commit events
- instrument assignment events
- mix manipulation feedback
- UI navigation feedback

Keep concurrency, retrigger behavior, and priority explicit. Avoid unclear overlapping playback rules.

## Sample And Preset Standards

- Prefer sample-first presets with procedural fallback only where needed.
- Keep slot identity consistent within a preset family.
- Use multiple sample variations for repetitive sounds.
- Document required source folders and naming whenever the sample workflow changes.
- Aim for presets that sound production-oriented: `Orchestra`, `Rock`, `Classical`, `DrumAndBass`, `Electronic`, and future expansions.

## Mix Guidance

- Rhythm-critical sounds must stay intelligible.
- Reverb and spatial treatment should help depth, not blur timing.
- Loudness relationships between slots should be intentional, not accidental.
- If new UI sounds are added, they must not compete with active musical content.

## Implementation Guidance

- Keep `RhythmSoundEngine` as the main engine API.
- Keep input adapters thin.
- Do not bury sample loading policy in drawing code.
- If audio architecture changes, update `Assets/Features/Audio/README.md` and this file.

## Next-Level Docs To Add Later

If the project grows, split this into:
- sound bible
- preset slot maps per genre
- audio event registry
- bus and routing plan
- export and recording requirements
