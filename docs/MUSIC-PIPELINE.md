# Music generation pipeline — planner, not generated audio

## Functional objective

Create instrumental arrangements algorithmically for review: an LLM outputs **composition instructions**, not PCM/WAV/music audio. A deterministic composer maps the plan into MIDI notes, then FluidSynth renders a SoundFont; FFmpeg encodes WAV to MP3 and can later mix it with imported visuals to produce an MP4. Music originality and monetization are not guaranteed.

## Selected local implementation

The Worker has a configurable pipeline with two arrangement choices. By default (`useAI: false`), C# deterministically selects a key, tempo, and chord progression. With `useAI: true`, local LM Studio proposes a JSON arrangement which is validated before use. In both cases, `MidiComposer` writes the Standard MIDI File, FluidSynth renders WAV with a local `FluidR3_GM.sf2`, FFmpeg encodes MP3, and FFprobe measures duration. `MusicTrack` metadata and the plan snapshot (including `usedAI`) are persisted; media bytes are written to the configured folder. The API exposes metadata and an MP3 stream. For tracks intended for YouTube monetization, verify the terms of every concrete tool/model/SoundFont/resource before use and publication; preserve required attribution and notices. A license means terms of use, not a version or entry key. Free options are preferred, without assuming they are cost-free in every respect or commercially permitted.

The current plan schema is deliberately smaller than the broader proposed arrangement format below:

```json
{
  "schemaVersion": 1,
  "tempoBpm": 78,
  "key": "A minor",
  "chords": ["Am7", "Dm7", "G7", "Cmaj7"],
  "variationSeed": 12345,
  "usedAI": false
}
```

Tempo must be 50–180 BPM, the key must be major or minor, and there must be 1–16 supported chord symbols. The seed is controlled by job parameters or derived from the generation job ID. Current worker output is instrumental MIDI rendered to MP3; automated video rendering is still planned.

## Planner options

- **LM Studio (optional; local OpenAI-compatible server):** used only when a job explicitly sets `useAI: true`. It serves a language model, **not** musical synthesis. Local inference has no per-call provider charge; the downloaded model has its own license and hardware requirements.
- **Deterministic C# planner (default):** used when `useAI` is omitted or `false`; it picks a reproducible key, tempo and chord progression without calling a language model.
- **OpenRouter:** possible later adapter for a paid/free-tier model; not configured in this implementation.
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

**Future schema:** the larger proposal is not accepted by the current parser. Current implementation uses the smaller schema above and a basic deterministic chord/bass/melody pattern. A richer schema will need matching validation and composition behavior before use.

## Pipeline and checkpoints

1. Validate project duration (> 0), safe job preferences and local generation configuration.
2. Generate structured plan; parse JSON strictly and reject unknown schema version or unsupported chords.
3. Composer selects notes/chords/melody from the plan and writes a MIDI file with tempo, instrument program changes and end-of-track events.
4. Run local `fluidsynth` with `FluidR3_GM.sf2` to render WAV; check process exit status.
5. Encode MP3 with FFmpeg and measure its duration with FFprobe.
6. Save the MP3 under an application-generated relative key, then persist `MusicTrack` metadata and the plan snapshot. Optionally render MP4 later with a manually imported `VisualAsset`.

## Safety, ownership and repeatability

- Do not pass arbitrary user input as an executable command. Use `ProcessStartInfo.ArgumentList`; restrict file paths to managed storage roots, set timeout and cancellation.
- Avoid user-supplied executable/script paths and downloading random SoundFonts automatically.
- Originality and monetization **not guaranteed** by use of MIDI or SoundFonts. Document sample/patch licenses and YouTube policies.
- Before publication, follow YouTube's current AI disclosure guidance (which lists AI-generated music as requiring disclosure) and channel monetization policies. Eligibility depends on the content and channel review; tool or asset licensing and disclosure do not guarantee monetization.
- Keep deterministic seed and plan snapshot so tests can reproduce an arrangement.

## Configuration and readiness

The Worker registers the Music handler when the `.sf2` path, a license-evidence key and FluidSynth/FFmpeg/FFprobe executables are valid. LM Studio URL/model are checked when an AI-enabled job runs; if they are missing or unavailable, that job fails safely instead of silently switching to non-AI composition. API and Worker must share the same `Media:StorageRoot` and run under an account that can read/write it. See [local setup](SETUP-DEPLOYMENT.md).

LM Studio and a model are not prerequisites for a non-AI job. A real rendered audio file still needs to be verified with the local audio tools and SoundFont; an AI-enabled job additionally needs LM Studio running with its selected model loaded.

## MVP acceptance test

Given a validated arrangement JSON and a licensed local SF2, worker outputs a playable WAV and MP3; media probe confirms nonzero duration; job transitions to Completed; files live outside PostgreSQL. Failure, cancellation and invalid plans produce sanitized Failed/Cancelled statuses without corrupt outputs.
