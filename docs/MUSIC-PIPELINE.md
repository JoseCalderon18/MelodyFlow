# Music generation pipeline — planner, not generated audio

## Functional objective

Create commercially usable original instrumental arrangements *algorithmically*: LLM outputs **composition instructions**, not PCM/WAV/music audio. A deterministic composer maps the plan into MIDI notes, then FluidSynth renders a SoundFont; FFmpeg encodes WAV to MP3 and mixes with imported visuals to produce an MP4.

## Planner options

- **OpenRouter (remote API key, paid/free depending on chosen model):** HTTP adapter `IArrangementPlanner`. Keep API key in secrets; do not log headers.
- **LM Studio (local OpenAI-compatible server):** another `IArrangementPlanner` adapter. It serves a language model, **not** musical synthesis.
- LMMS is a distinct DAW and optional for manual adjustment; do not substitute it for LM Studio.

## Proposed arrangement JSON contract (schema proposal)

```json
{
  "schemaVersion": 1,
  "genre": "lofi",
  "mood": "calm",
  "tempoBpm": 78,
  "timeSignature": "4/4",
  "key": "A minor",
  "bars": 32,
  "instruments": [
    {"role": "chords", "program": 0, "channel": 0},
    {"role": "bass", "program": 32, "channel": 1}
  ],
  "sections": [
    {"name": "verse", "startBar": 0, "lengthBars": 16,
     "chords": ["Am7", "Dm7", "G7", "Cmaj7"]}
  ],
  "variationSeed": 12345
}
```

**Not executable as-is:** musical generation requires implementing note patterns, voicing, rhythm, validation, instrument patch availability, and safe command execution. No promise that an LLM's free-form JSON produces a musical result automatically.

## Pipeline and checkpoints

1. Validate request (positive duration, tempo bounds chosen in policy, supported instruments, rights).
2. Generate structured plan; parse JSON strictly and reject unknown schema version/inconsistent bars.
3. Composer selects notes/chords/arpeggios/rhythm. Generate MIDI with tempo track, program changes and end-of-track events.
4. Run `fluidsynth` with a licensed `.sf2` and render to WAV. Verify exit code and measured duration with `ffprobe`.
5. Normalize/encode with FFmpeg; do not rely on raw file extension. Track provenance and licensing evidence.
6. Persist `MusicTrack` metadata and StorageKey; checkpoint job state. Optionally render MP4 with a manually imported `VisualAsset`.

## Safety, ownership and repeatability

- Do not pass arbitrary user input as an executable command. Use `ProcessStartInfo.ArgumentList`; restrict file paths to managed storage roots, set timeout and cancellation.
- Avoid user-supplied executable/script paths and downloading random SoundFonts automatically.
- Originality and monetization **not guaranteed** by use of MIDI or SoundFonts. Document sample/patch licenses and YouTube policies.
- Keep deterministic seed and plan snapshot so tests can reproduce an arrangement.

## MVP acceptance test

Given a validated arrangement JSON and a licensed local SF2, worker outputs a playable WAV and MP3; media probe confirms nonzero duration; job transitions to Completed; files live outside PostgreSQL. Failure, cancellation and invalid plans produce sanitized Failed/Cancelled statuses without corrupt outputs.
