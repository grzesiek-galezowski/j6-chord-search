# Chord Fun — Technical Documentation

Chord Fun is a browser-based polyphonic synthesiser and chord sequencer. It accepts input from a MIDI keyboard, mouse/touch on on-screen piano keyboards, or a QWERTY keyboard. The lowest octave of keys selects chord modifiers; the upper two octaves select root notes that are expanded into full chords before being sent to the synthesis engine.

---

## Table of Contents

1. [Synthesis Engine](#synthesis-engine)
   - [Signal Chain](#signal-chain)
   - [Oscillator and Waveforms](#oscillator-and-waveforms)
   - [Unison](#unison)
   - [Amplitude Envelope](#amplitude-envelope)
   - [Ladder Filter](#ladder-filter)
   - [Filter Envelope](#filter-envelope)
   - [Drive / Wave Shaper](#drive--wave-shaper)
   - [Gain Scaling](#gain-scaling)
   - [Presets](#presets)
2. [Chord Engine](#chord-engine)
   - [Keyboard Layout](#keyboard-layout)
   - [Chord Modes](#chord-modes)
   - [Extensions](#extensions)
   - [Inversions](#inversions)
   - [Root Doubling](#root-doubling)
   - [Strumming](#strumming)
   - [Hold Mode](#hold-mode)
   - [Octave Shift](#octave-shift)
   - [Input Methods](#input-methods)
3. [Chord Sequencer](#chord-sequencer)
   - [Steps and Data Model](#steps-and-data-model)
   - [Recording — Select Mode](#recording--select-mode)
   - [Recording — SH-101 Mode](#recording--sh-101-mode)
   - [Sustain Logic](#sustain-logic)
   - [Playback](#playback)
   - [BPM Control](#bpm-control)
   - [Controls Reference](#controls-reference)

---

## Synthesis Engine

The engine is built entirely on the [Web Audio API](https://developer.mozilla.org/en-US/docs/Web/API/Web_Audio_API). A single shared `AudioContext` is created on the first user interaction (required by browser autoplay policies) and reused for the lifetime of the page.

All active voices are tracked in `activeOscillators`, a `Map` keyed by the MIDI note number (or the special string key `'seq'` for sequencer voices). Each map entry holds an array of *voice objects* — one per sounding frequency in the chord.

### Signal Chain

Each sounding frequency in a chord gets its own independent chain:

```
Oscillator(s) ──► WaveShaper (drive) ──► BiquadFilter LP (1) ──► BiquadFilter LP (2) ──► GainNode (amp) ──► AudioDestination
```

Multiple oscillators (unison voices) share the same downstream chain from the `WaveShaper` onward.

### Oscillator and Waveforms

Each voice starts with one or more `OscillatorNode` instances. The waveform is set at the time `playFreqs` is called and comes from the **Waveform** dropdown.

| Value | Type | Description |
|---|---|---|
| `sine` | Native | Pure tone, no harmonics |
| `triangle` | Native | Odd harmonics, soft and mellow |
| `sawtooth` | Native | All harmonics, bright and buzzy |
| `square` | Native | Odd harmonics, hollow and reedy |
| `organ` | Custom | Partials: 1, 0.8, 0.6, 0.1, 0.2, 0.1 — simulates drawbar organ |
| `brass` | Custom | Partials descending from 1 to 0.1 across 10 harmonics |
| `bell` | Custom | Sparse partials at 1st, 5th, 8th, 10th — inharmonic bell character |
| `voice` | Custom | Formant-like partial layout for a vocal quality |
| `pluck` | Custom | Rapidly decaying harmonic series (1, 0.5, 0.25, 0.125, 0.06) |
| `soft-saw` | Custom | Sawtooth with amplitude taper, smoother than the native sawtooth |
| `hollow-square` | Custom | Odd harmonics only (square-like) but with gaps for a hollow sound |
| `metallic` | Custom | Non-uniform partial weighting for a metallic, inharmonic feel |
| `sub-bass` | Custom | Heavy fundamental with weak upper harmonics |
| `harmonic-noise` | Custom | Randomised partial amplitudes scaled by `1/i` — each page load is unique |

Custom waveforms are created once via `AudioContext.createPeriodicWave()` and cached in the `customWaves` object at audio context initialisation time.

### Unison

**Unison Voices** (1–8) controls how many oscillators are stacked per frequency. When more than one voice is used, each oscillator is detuned by a different amount within a symmetric spread centred on 0 cents:

```
detune[i] = (i / (voices - 1)) * spread * 2 − spread    (for voices > 1)
```

where `spread` is the **Unison Detune** value in cents (0–100). With 1 voice, no detune is applied. All voices for a single frequency connect to the same `WaveShaper` node, so they share the filter and gain chain.

### Amplitude Envelope

The `GainNode` amplitude is shaped by a four-stage ADSR envelope applied at note-on and note-off time using Web Audio parameter automation:

| Stage | Parameter | Range | Unit |
|---|---|---|---|
| Attack | `amp-a` | 0–2000 | ms |
| Decay | `amp-d` | 0–2000 | ms |
| Sustain | `amp-s` | 0–100 | % of peak gain |
| Release | `amp-r` | 0–5000 | ms |

**Note-on** (`playFreqs`):
1. Gain set to 0 at note start time.
2. `linearRampToValueAtTime` to peak gain over Attack duration.
3. `exponentialRampToValueAtTime` to `peak × sustain` over Decay duration.

The note then stays at sustain level indefinitely (held by the oscillator running).

**Note-off** (`stopFreqs`):
1. All scheduled automation is cancelled and the current instantaneous value is re-pinned.
2. `exponentialRampToValueAtTime` to near-zero (`0.0001`) over Release duration.
3. Oscillators are scheduled to stop `0.1 s` after the release end; nodes are disconnected `0.2 s` after release end via `setTimeout`.

### Ladder Filter

Two `BiquadFilterNode` instances of type `lowpass` are cascaded in series to approximate a steeper roll-off (similar to a 4-pole Moog-style ladder filter). Both filters receive the same frequency and Q automation.

| Parameter | Control | Range |
|---|---|---|
| Cutoff frequency | `filt-cutoff` | 20–20 000 Hz |
| Resonance (Q) | `filt-res` | 0.1–20 |

Higher Q values produce a resonant peak at the cutoff frequency. With two filters in series the combined resonance effect is more pronounced than a single filter at the same Q setting.

### Filter Envelope

The cutoff frequency of both filters is modulated by its own ADSR envelope:

| Stage | Parameter | Range | Unit |
|---|---|---|---|
| Amount | `filt-env-amt` | −10 000–+10 000 | Hz offset |
| Attack | `filt-a` | 0–2000 | ms |
| Decay | `filt-d` | 0–2000 | ms |
| Sustain | `filt-s` | 0–100 | % of amount |
| Release | `filt-r` | 0–5000 | ms |

The three key frequency values are:

```
baseCutoff   = filt-cutoff
peakCutoff   = clamp(baseCutoff + filtEnvAmt, 20, 20000)
sustainCutoff = clamp(baseCutoff + filtEnvAmt × filtS, 20, 20000)
```

**Note-on**: filter sweeps from `baseCutoff` → `peakCutoff` (Attack) → `sustainCutoff` (Decay).  
**Note-off**: filter returns from its current value → `baseCutoff` over the filter Release time.

Negative `filt-env-amt` values produce a downward sweep (filter closes on attack).

### Drive / Wave Shaper

A `WaveShaperNode` sits immediately after the oscillators and before the filters. Its transfer curve is a soft-clip function:

```
y = (3 + k) × x × 20° / (π + k × |x|)
```

where `k` is the **Drive** value (0–50). At `k = 0` the curve is nearly linear (unity gain through the soft-clip). Higher values introduce increasingly aggressive saturation. The node runs with `oversample = '4x'` to reduce aliasing artefacts.

### Gain Scaling

Peak gain is automatically scaled down with polyphony to avoid clipping:

```
maxGain = 0.5 / max(1, sqrt(numFreqs × unisonVoices))
```

This keeps the output level roughly constant regardless of how many simultaneous frequencies and unison voices are active.

### Presets

Presets are plain objects stored in the `presets` map. Selecting a preset from the dropdown calls `applyPreset`, which writes all synth parameter values to their respective DOM controls and fires the corresponding `input` events to update display spans.

| Preset | Character |
|---|---|
| **Lush Pad** | Soft-saw, 4 unison voices, slow attack/release, filter envelope sweeps up |
| **Plucky Synth** | Pluck wave, 2 voices, instant decay to zero sustain, punchy filter sweep |
| **Dirty Bass** | Sawtooth, 3 voices, high drive, low cutoff, octave shift −2 |
| **Soft Keys** | Sine, 1 voice, no drive, subtle filter envelope |
| **Basic Triangle** | Triangle, 1 voice, minimal processing — useful as a clean reference |

---

## Chord Engine

The chord engine transforms a single played MIDI note (the *root*) into a set of frequencies by applying an interval pattern, optional extensions, optional inversions, and an octave shift. The resulting frequency array is passed directly to `playFreqs`.

### Keyboard Layout

The keyboard is divided into two functional regions, each mapped to one of the two on-screen canvases:

**Control octave** — MIDI notes 48–59 (C2–B2), drawn on the upper canvas:

| Note | MIDI | QWERTY | Function |
|---|---|---|---|
| C2 | 48 | `z` | Major chord mode |
| C#2 | 49 | `s` | Diminished chord mode |
| D2 | 50 | `x` | Minor chord mode |
| D#2 | 51 | `d` | Suspended chord mode |
| E2 | 52 | `c` | Inversion 1 |
| F2 | 53 | `v` | Inversion 2 |
| F#2 | 54 | `g` | Extension: 6th |
| G2 | 55 | `b` | Extension: minor 7th |
| G#2 | 56 | `h` | Extension: major 7th |
| A2 | 57 | `n` | Extension: 9th |

**Play octave** — MIDI notes 60–83 (C3–B4), drawn on the lower canvas:

| Range | QWERTY | Description |
|---|---|---|
| C3–B3 | `q 2 w 3 e r 5 t 6 y 7 u` | Lower play octave |
| C4–G#4 | `i 9 o 0 p [ = ]` | Upper play octave |

### Chord Modes

A chord mode defines the interval pattern applied to the root note. Only one mode can be active at a time (the first matching pressed note wins).

| Mode | Intervals (semitones) | Guitar Voicing (strumming) |
|---|---|---|
| **Major** | 0, 4, 7 | 0, 7, 12, 16, 19, 24 |
| **Diminished** | 0, 3, 6 | 0, 6, 12, 15, 18, 24 |
| **Minor** | 0, 3, 7 | 0, 7, 12, 15, 19, 24 |
| **Suspended** | 0, 5, 7 | 0, 7, 12, 17, 19, 24 |

If no chord mode key is held, the engine returns `[0]` — a single unison note with no chord.

### Extensions

Extension keys are additive and independent of one another; any combination can be held simultaneously. Extensions are appended to the interval list before inversions are applied.

| Extension | Interval | Semitones above root |
|---|---|---|
| 6 | Major 6th | 9 |
| m7 | Minor 7th | 10 |
| M7 | Major 7th | 11 |
| 9 | Major 9th | 14 |

In strumming mode, all extension intervals are shifted up by one octave (+12 semitones) so they sit above the guitar voicing span.

### Inversions

Inversions raise one note in the already-sorted interval list by one octave (+12). They are applied after extensions are added and the full list is sorted ascending.

| Inversion | Raises | Effect |
|---|---|---|
| **Inv1** | Index 0 (lowest note) | First inversion — root moves up an octave |
| **Inv2** | Index 1 (second note) | Second inversion — third/fourth moves up |

Both inversions can be active at once.

### Root Doubling

When **Root Doubling** is enabled (default: on) and strumming is off, an extra interval of `−12` (one octave below the root) is appended to the interval list *after* inversions are applied, so the doubled root is never raised by an inversion.

### Strumming

When **Strumming** is enabled:
- Guitar voicings replace the compact interval lists for all chord modes.
- Extensions are shifted up one octave.
- Root doubling is suppressed.
- Each frequency in the resulting array is started `30 ms` later than the previous one (`noteStartTime = now + index × 0.03`), producing an arpeggiated strum effect.

### Hold Mode

**Hold Mode** latches the chord state (active mode + active extensions + active inversions) at the moment the checkbox is checked. While hold is active:
- The keyboard no longer changes the chord by holding/releasing control keys.
- Each control key press *toggles* the corresponding latched item on or off (chord mode cycles between the pressed mode and null; each extension/inversion toggles independently).
- Releasing control keys has no effect.

Unchecking Hold Mode immediately returns to real-time key-tracking.

### Octave Shift

The **Synth Octave Shift** stepper (range −3 to +3) offsets all computed chord frequencies by whole octaves before synthesis. It does not affect which MIDI note is used as a map key in `activeOscillators` — only the frequencies passed to `playFreqs`. The shift is also applied when the sequencer records a step, so recorded frequencies reflect the shift at record time.

### Input Methods

| Method | Notes |
|---|---|
| **QWERTY keyboard** | `keydown`/`keyup` on `window`; `e.repeat` events are ignored; ignored when focus is on `INPUT` or `SELECT` elements |
| **Mouse** | `mousedown` on the canvas triggers note-on; `mouseup` or `mouseleave` triggers note-off; one active note tracked per canvas via `dataset.activeNote` |
| **Touch** | Multi-touch via `touchstart`/`touchmove`/`touchend`/`touchcancel`; each touch point tracked independently by `touch.identifier`; `touchmove` handles gliding between keys |
| **MIDI** | Web MIDI API; status bytes 0x80–0x8F (note-off) and 0x90–0x9F (note-on, velocity > 0) are handled; note-on with velocity 0 is treated as note-off |

---

## Chord Sequencer

The sequencer records a series of up to 8 chords and loops them as a background progression. Each step holds exactly one bar (4 beats) of material.

### Steps and Data Model

The `sequencerSteps` array holds 8 entries. Each entry is either `null` (empty step) or an object:

```js
{
  rootNote: Number,   // MIDI note number of the root (60–83)
  label:    String,   // Human-readable label, e.g. "C Maj m7"
  freqs:    Number[]  // Pre-computed frequency array, one entry per chord tone
}
```

Frequencies are computed at **record time** using the octave shift and chord state active at that moment. Changing the octave shift or chord parameters after recording does not retroactively update stored steps.

### Recording — Select Mode

1. Click any of the 8 numbered step cells to select it (highlighted with a blue border). The selection persists until you click a different cell.
2. Hold any combination of control-octave keys (chord mode, extensions, inversions) on the control piano.
3. Press a root note on the play piano.

The step is immediately overwritten with the new chord. Recording only occurs when the sequencer is **not** playing back — pressing play notes while the sequencer runs is for live improvisation and does not affect stored steps.

### Recording — SH-101 Mode

Works identically to Select Mode except the selected step advances to the next step automatically after every root note press. This allows filling steps in order without clicking each cell:

```
press root → records to step N → selection moves to step N+1 → press root → …
```

After step 8 the selection wraps back to step 1.

### Sustain Logic

When `seqPlayStep` is called for a new step it compares the incoming chord's frequency array against the last array that was actually sent to the synthesis engine (`seqCurrentFreqs`). The comparison is element-wise with a 0.01 Hz tolerance:

```
same chord  →  do nothing (oscillators continue running, no envelope retrigger)
new chord   →  stopFreqs(SEQ_KEY) on the old voice, then playFreqs(SEQ_KEY, newFreqs)
empty step  →  stopFreqs(SEQ_KEY) if anything is playing, then silence
```

The `SEQ_KEY = 'seq'` string is used as the `activeOscillators` map key for all sequencer voices. It is distinct from numeric MIDI note keys used by live playing, so sequencer and live voices coexist independently.

Sequencer voices are routed through a dedicated `seqOutputGain` node (a `GainNode` inserted between the voice amp nodes and `AudioDestination`) rather than connecting directly to the destination. This allows the sequencer volume to be adjusted in real time — including while a chord is sustaining — without affecting live-played voices.

### Playback

Playback is driven by `setInterval`. On **Play**:

1. `seqCurrentStep` is reset to 0 and `seqCurrentFreqs` is cleared.
2. Step 0 is played immediately via `seqPlayStep(0)`.
3. An interval is set for the bar duration; each tick increments `seqCurrentStep` modulo 8 and calls `seqPlayStep`.

On **Stop**:

1. The interval is cleared.
2. `stopFreqs(SEQ_KEY)` is called to release the current chord through its normal Release envelope.
3. `seqCurrentStep` returns to −1 (no highlighted step).

While playing, the current step cell is highlighted yellow; if it is also the selected step the border turns orange.

### BPM Control

Bar duration in milliseconds:

```
barMs = (4 × 60 × 1000) / BPM
```

| BPM | Bar duration |
|---|---|
| 60 | 4000 ms |
| 120 | 2000 ms |
| 180 | 1333 ms |
| 240 | 1000 ms |

The BPM stepper adjusts in steps of 5, clamped to the range 20–300. Changing the BPM while playing calls `seqRestartInterval`, which clears the old interval and creates a new one at the updated rate. The current bar is cut short by the change.

### Controls Reference

| Control | Behaviour |
|---|---|
| **Vol slider** | Sets the `seqOutputGain` level (0–100%, default 70%); updates in real time via `setTargetAtTime` with a 15 ms smoothing constant — adjusting while a chord sustains takes effect immediately without retriggering the envelope |
| **− / + (BPM)** | Decrease / increase BPM by 5; restarts interval if playing |
| **▶ Play** | Starts playback from step 1; no-op if already playing; highlights blue when active |
| **■ Stop** | Stops playback and releases the current chord through its Release envelope |
| **↺ Reset** | Stops playback, clears all 8 steps, resets selected step to 1 |
| **Select radio** | Recording mode: selected step stays fixed after each note press |
| **SH-101 radio** | Recording mode: selected step advances after each note press |
| **Step cell click** | Sets the selected step (blue border) |
| **× button on step** | Clears that individual step without affecting others or stopping playback |
