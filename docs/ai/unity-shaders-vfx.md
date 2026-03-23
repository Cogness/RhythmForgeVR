# Unity Shaders And VFX Guide

Adapted from the Unity shader specialist guidance in Claude Code Game Studios.

## Render Pipeline Direction

- For VR and Quest-class hardware, treat URP as the expected baseline unless the project explicitly proves otherwise.
- Do not mix render-pipeline-specific assumptions across materials and shaders.

## Shader Rules

- Prefer Shader Graph for maintainable project shaders.
- Use custom HLSL only when Shader Graph is not enough.
- Keep shader variants under control; avoid enabling keywords casually.
- Profile overdraw, draw calls, and shader complexity on target hardware.

## VFX Rules

- Use lightweight effects with clear budgets.
- Pool frequently reused effects.
- Tie effect complexity to distance and importance.
- Avoid any effect that risks headset discomfort through excessive flicker, blur, or unstable motion.

## RhythmForge Context

- Visuals should support musical clarity first: loop paths, trigger points, melody strokes, and mix objects must stay readable.
- Favor effects that communicate timing, intensity, and interaction state.
- Any trail, glow, or beat pulse effect should be checked against readability in VR before adding more spectacle.
