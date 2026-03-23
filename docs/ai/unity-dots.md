# Unity DOTS Guide

Adapted from the Unity DOTS/ECS specialist guidance in Claude Code Game Studios.

## Default Position For This Repo

Do not introduce DOTS or ECS by default.

RhythmForge's current needs are input handling, audio control, spatial interaction, and VR feedback. Those are better served by straightforward MonoBehaviour-based architecture until profiling proves a genuine entity-count or CPU bottleneck.

## If DOTS Is Considered Later

- Components must remain pure data.
- Systems should be isolated, stateless, and Burst-friendly where possible.
- Keep the GameObject and ECS boundary explicit.
- Do not move a system to DOTS just because it sounds advanced.
- Only migrate when there is a measurable performance case and a contained subsystem to move.

## Good Candidates

Potential future candidates if scale demands it:
- Large numbers of visual rhythm markers
- Dense background reactive particles
- Massive lightweight spatial indicators

## Poor Candidates Right Now

- MX Ink interaction logic
- Scene wiring and VR UX flows
- Audio preset and sample management
- Small gameplay controllers with low entity counts
