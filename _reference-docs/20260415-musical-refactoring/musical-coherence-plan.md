# Musical Coherence Across Genres — Sound Quality Overhaul

## Context

Current synthesis primitives are individually high-quality (Schroeder reverb, additive kalimba/pad, band-limited waves, FM drums, soft saturation), but the **musical result** still sounds broken — especially in NewAge — because each shape renders as an *isolated* voice with no awareness of:

1. **Other shapes of the same mode** playing simultaneously (voice-stacking with no coordination).
2. **Other modes** occupying the same frequency band (rhythm/melody/harmony collide).
3. **Global mix balance** (every voice fights for headroom; reverb tails pile on top of each other).
4. **Harmonic/temporal fit** — random human shapes produce random onsets, random pitches, random durations. Pentatonic + chord-snap is a start, but doesn't prevent four kalimbas plucking on the same 16th with slightly different pitches.

Evidence in code:
- `TonalSynthesizer.cs:298-300` — every melody voice adds its own transient noise; N shapes = N simultaneous transient clacks.
- `AudioEffectsChain.cs:92` — NewAge reverb scale 1.8×; each voice re-renders its own tail → tails compound.
- `NewAgeRhythmDeriver.cs:43-50` — every rhythm shape fires bowl on step 0 of every bar. Two rhythm shapes = double bowl hit on downbeat, every bar.
- `DrumSynthesizer.cs:280` — singing bowl fundamental 220 Hz + body modulation → boomy low-mid pitch, not bell-like (real Tibetan bowls are 400–1200 Hz with strong inharmonic partials).
- `TonalSynthesizer.cs:119,169` — kalimba partial decay 0.06–0.22 s, but preset release is 0.7 s → voice is silent long before its envelope "officially" ends; reverb tail becomes most of what you hear.
- `GenreRegistry.cs:130` — drone pad attack 0.8 s, release 3.2 s; three harmony shapes = three pads swelling out of phase.

## Goal

Preserve the existing architecture (ShapeProfile → SoundProfile → Deriver → VoiceSpec → Synthesizer → EffectsChain). Introduce **coordination layers** and **refine a handful of timbres** so that any combination of random human shapes sounds like *composed* music. Focus on NewAge first (worst offender), then port the mechanisms to Electronic and Jazz.

## Design Principles

1. **Shapes are instruments, not clips.** Each shape of the same mode should play a *role* in an ensemble (lead vs. counter vs. sub) rather than all playing the same role.
2. **Frequency-band separation.** Rhythm lives in transients + lows, melody in mids, harmony in low-mid pad, bass under. Modes shouldn't compete for the same band.
3. **Temporal offset.** Multiple shapes of the same mode should phase-offset — not trigger on the exact same grid point.
4. **One reverb, one mix.** The global reverb should live on a shared send, not be re-generated per voice.
5. **Envelope coherence.** A voice's amplitude envelope, partial decays, and reverb tail must be on the same time scale.

---

## Proposal

### 1. Shape Role Assignment (new layer, non-breaking)

**Problem.** Two melody shapes both play the same pentatonic line at the same time. Two harmony shapes both pick sus2 on the same root.

**Solution.** When the draft is built, each shape of the same mode is assigned a *role index* (0, 1, 2…) based on its creation order or size rank. Derivers read this role from a new static provider (same pattern as `HarmonicContextProvider`) and adapt:

- **Melody roles**:
  - Role 0 (primary): as today — pentatonic, chord-snapped, mid-range.
  - Role 1 (counter): octave higher, sparser (every 3rd slice), stays on chord tones only.
  - Role 2 (pad-ish fill): longer durations, plays only when role 0/1 rest.
  - Role 3+: thinned further (1 note per bar, chord root only).
- **Harmony roles**:
  - Role 0: full voicing as today (sus2/sus4/drone5).
  - Role 1: drops to **root + 5th drone** one octave below — never duplicates role 0's top voice.
  - Role 2+: pure bass pedal (root −24) that doesn't re-trigger when role 0 changes.
- **Rhythm roles**:
  - Role 0: bowl on downbeats + shaker.
  - Role 1: **no bowl** — only shaker fills on off-beats; more angular logic.
  - Role 2+: soft perc ghosts only (no bowl, no main shaker).

**Files**:
- New `Assets/RhythmForge/Core/Sequencing/ShapeRoleProvider.cs` (mirrors `HarmonicContextProvider.cs:11-23`).
- Set by `DraftBuilder` before each per-shape derivation (same place `HarmonicContextProvider` is set).
- Read by `NewAgeRhythmDeriver.cs`, `NewAgeMelodyDeriver.cs`, `NewAgeHarmonyDeriver.cs` (and the Electronic/Jazz equivalents later).

Role is **stable per shape** for the lifetime of the session (so the user's shape keeps its character). Role computation: sort shapes of the same mode by creation timestamp; index is position.

### 2. Temporal Offset Per Role

**Problem.** Every melody shape starts on step 0; every rhythm shape fires bowl on step 0. Onsets pile up.

**Solution.** Apply a role-dependent step offset inside the deriver:
- Role 0: offset 0.
- Role 1: offset = barSteps / 3 (≈ 5 steps on 16-step bar).
- Role 2: offset = barSteps * 2/3.
- Offsets are modulo totalSteps so sequence still loops cleanly.

Implementation: single additive `roleOffset` field added to each derived event's `step`. Cheap, local to the derivers.

### 3. Frequency-Band Register Policy

**Problem.** Bass can overlap with harmony root; melody can drop into pad range on a large shape.

**Solution.** Clamp derived MIDI ranges per mode and per genre in the deriver:

| Mode | NewAge range | Electronic | Jazz |
|------|-------------|-----------|------|
| Rhythm (tuned perc) | (untuned) | (untuned) | (untuned) |
| Bass / harmony root | A1–E3 | E1–E3 | A1–E3 |
| Harmony voicing | C3–G4 | C3–A4 | C3–A4 |
| Melody | G4–E6 | A3–C6 | E4–E6 |

Apply at the end of each deriver via a single `ClampToRegister(midi, mode, genre)` helper. For melody, if the raw pentatonic pick is below the floor, transpose up an octave (stays pentatonic).

New helper: `Assets/RhythmForge/Core/Sequencing/RegisterPolicy.cs`.

### 4. NewAge-Specific Timbre Refinements

These are surgical edits to existing synth code, not a rewrite.

#### 4a. Singing bowl (DrumSynthesizer.cs:277-303)
- Raise fundamental from 220 Hz to **440 Hz + body×180 + brightness×140** (real Tibetan bowl range).
- Add **two more inharmonic partials** at ratios 2.74, 5.43, 8.12 with amps 0.22 / 0.11 / 0.05 — gives the shimmering beating characteristic of struck bowls.
- Introduce **slow amplitude beating** at 4–7 Hz between partial 1 and 2 (detune partial 2 by ±1.5 Hz) — this is the sound of two bowl walls vibrating against each other.
- Reduce decay floor from 0.9 s to 0.6 s (currently bowls overlap themselves on step-8 re-trigger).

#### 4b. Kalimba envelope coherence (TonalSynthesizer.cs:161-170, GenreRegistry.cs:129)
- Lower preset release from 0.7 s to 0.35 s (matches the partial decay tail).
- Raise `kalBase` floor from 0.06 s to 0.12 s for the fundamental so it doesn't disappear in a clacky fraction of a second.
- Add a **soft body thump** at note onset (150 Hz sine, 20 ms decay, gain 0.08×velocity) — simulates finger hitting tine base; removes the "pure-sine" artificial feel. Currently the NewAge transient noise (line 297) at 0.3× is too dry.

#### 4c. Drone pad coherence (TonalSynthesizer.cs:151-159, GenreRegistry.cs:130)
- Lower preset attack from 0.8 s to 0.45 s — three pads no longer all swell out of phase over 2.4 s.
- Reduce release from 3.2 s to **2.2 s** — tails still lush but don't stack into mud.
- Scale chorus gain by `1 / (1 + roleIndex)` so role-1 pad voice uses less stereo spread (role 0 is the wide one; role 1 sits center).

#### 4d. Shaker (DrumSynthesizer.cs:305-323)
- Currently identical left/right apart from 3% cutoff offset. Add **per-trigger random pan** (deterministic per seed) in ±0.35 range so multiple shaker hits feel like a hand-held shaker, not a centered noise burst.

### 5. Global Mix & Shared Reverb Bus

**Problem.** Each voice renders its own Schroeder reverb → N voices × N reverb tails → feedback-tail wash.

**Solution.** Split `ApplyAmbience` into:
- **Per-voice pre-send** (dry + short pre-delay only, no tail).
- **Shared reverb bus** that sums send signals from all concurrently-rendering voices, runs one Schroeder reverb, and mixes back.

Since voices are rendered offline into discrete clips (not real-time-mixed), the simplest win is:
- In `ResolvedVoiceSpec`, add a **bus-tail-length hint** so individual clips don't each render full 3-second tails.
- Halve the per-voice `genreReverbScale` for NewAge (currently 1.8 at `AudioEffectsChain.cs:92`) to **1.1**, but boost the *bus* reverb in the Unity `AudioMixerGroup` for the NewAge instrument group.
- Bus-level mixer settings live in `GroupBusFx` on the genre profile (`GenreRegistry.cs:154` already has reverb=0.65, delay=0.30). Route these through a real `AudioMixer` send so reverb is truly shared.

This is architecturally the biggest change and I recommend it be done **last**, after steps 1–4 confirm the musical logic is right. Steps 1–4 give 80% of the improvement.

### 6. Per-Mode Gain Staging

Add a `modeGainDb` table per genre (simple constants, not derived):

| Mode | NewAge | Electronic | Jazz |
|------|--------|-----------|------|
| Rhythm | 0 dB | 0 dB | 0 dB |
| Melody | −2 dB | −1 dB | −1 dB |
| Harmony | −5 dB | −4 dB | −4 dB |

Applied in `VoiceSpecResolver.cs` as a final velocity multiplier. Harmony is loudest-lowest-notes with longest tails — it should sit under the mix, not on top.

### 7. Cross-Genre Benefits

The same role, register, temporal-offset, and gain-staging mechanisms also improve Electronic and Jazz:
- Electronic: role-1 melody becomes an arp octave-up; role-1 pad becomes a sub bass line.
- Jazz: role-1 melody plays walking-tone fills between role-0 Rhodes chords; role-1 comp plays guide-tone voicings (3-7) while role-0 plays full comp.

Derivers for those genres receive parallel edits; the shared `ShapeRoleProvider`, `RegisterPolicy`, and mode-gain table drive it.

---

## Critical Files To Modify

| # | File | Change |
|---|------|--------|
| 1 | `Assets/RhythmForge/Core/Sequencing/ShapeRoleProvider.cs` | **NEW** — static role bridge |
| 2 | `Assets/RhythmForge/Core/Sequencing/RegisterPolicy.cs` | **NEW** — per-mode/genre MIDI clamping helper |
| 3 | `Assets/RhythmForge/Core/Sequencing/NewAge/NewAgeMelodyDeriver.cs` | Read role, apply offset, clamp register, role-differentiated slicing |
| 4 | `Assets/RhythmForge/Core/Sequencing/NewAge/NewAgeHarmonyDeriver.cs` | Role-based voicing (0=sus, 1=root+5, 2=pedal) |
| 5 | `Assets/RhythmForge/Core/Sequencing/NewAge/NewAgeRhythmDeriver.cs` | Role-based lane suppression (only role 0 fires bowl) |
| 6 | `Assets/RhythmForge/Audio/Synthesis/DrumSynthesizer.cs:277-303` | Bowl: raise fundamental, add inharmonic partials, add beating |
| 7 | `Assets/RhythmForge/Audio/Synthesis/DrumSynthesizer.cs:305-323` | Shaker: per-trigger pan |
| 8 | `Assets/RhythmForge/Audio/Synthesis/TonalSynthesizer.cs:161-170` | Kalimba: raise `kalBase`, add body thump |
| 9 | `Assets/RhythmForge/Core/Data/GenreRegistry.cs:128-131` | Preset tuning (kalimba release 0.35s, drone attack 0.45s / release 2.2s) |
| 10 | `Assets/RhythmForge/Audio/VoiceSpec/VoiceSpecResolver.cs` | Apply per-mode gain from new genre table |
| 11 | `Assets/RhythmForge/Audio/Synthesis/AudioEffectsChain.cs:92` | Lower NewAge genreReverbScale from 1.8 → 1.1 (only after mixer bus is wired) |
| 12 | `Assets/RhythmForge/Core/Sequencing/DraftBuilder*.cs` | Set `ShapeRoleProvider.Current` before each derivation (mirror existing `HarmonicContextProvider` set pattern) |

## Reused Existing Utilities

- `HarmonicContextProvider.cs:11-23` — template for `ShapeRoleProvider`.
- `MusicalKeys.QuantizeToKey` — already used in harmony deriver; register clamp calls this to stay in-key after octave shifts.
- `ShapeProfileSizing.GetSizeFactor` — continues to drive bars/density; unchanged.
- `AudioEffectsChain.ApplyAmbience` — keeps its API; only genre scale numbers change.
- `SynthUtilities.EnvelopeDecay` / `AdvancePhase` — reused in refined bowl synthesis.

## Implementation Order (recommended)

1. **Phase A (musical coherence, no timbre changes)** — Steps 1, 2, 3, 6. Validates the role/register architecture in isolation. Small, mergeable.
2. **Phase B (NewAge timbre fixes)** — Step 4 (bowl, kalimba, drone, shaker). Immediately audible improvement.
3. **Phase C (Electronic & Jazz roles)** — Port the role logic to other genre derivers. Mostly parallel edits.
4. **Phase D (shared reverb bus)** — Step 5. Biggest infrastructure change; do once A–C prove the musical logic sound.

## Verification

- **Unit-test the derivers** with synthetic ShapeProfiles at roles 0/1/2/3 and verify: (a) melody stays in its MIDI register; (b) harmony role 0/1/2 never duplicate the same note; (c) rhythm role ≥1 never contains a `kick` event for NewAge.
- **Audible test**: in Unity, draw 4 shapes of each mode (12 total) on NewAge at 68 BPM. Listen for:
  - No downbeat-stacked bowl hits.
  - Kalimba voices distinguishable (primary center, counter octave-up).
  - Harmony doesn't cloud — you can hear the root move.
  - Reverb tail is lush but not washing out transients.
- **A/B tempo test**: same 12-shape scene at 60 / 90 / 120 BPM. Roles should still separate at all tempos.
- **Cross-genre regression**: repeat with Electronic and Jazz to confirm Phase C didn't break them; all three should sound musical with the same shapes.
- **Performance check**: background render queue (recent commit `0e057b1`) should still keep per-shape render under current budget — role assignment is O(1), offset is a single add, register clamp is a couple compares. Bowl refinements add ~4 extra sines per sample — negligible.
