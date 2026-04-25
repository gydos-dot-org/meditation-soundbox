# DEVLOG

## v0.3.2.5 documentation/commenting pass

No version bump. This pass keeps the v0.3.2.5 version number.

### Corrections included

- Replaced SpeechPlayer with a safer version:
  - Unload()
  - PlayFromBeginning()
  - Restart() delegates to PlayFromBeginning()
- Added `_speechVersion`.
- Read() now writes each render to a fresh WAV path:
  - speech-cache/current-reading-1.wav
  - speech-cache/current-reading-2.wav
  - etc.
- Read() now always starts from the beginning.
- ResetSpeechForNewText() now unloads the old speech player and marks speech as not rendered.

### Why this matters

This fixes the observed behavior where:

- `read` a second time did nothing after the WAV had already ended.
- Loading a new KJV verse could still cause the previous verse to play.
- Reusing the same speech-cache/current-reading.wav path could leave stale file state.

### Documentation changes

- Rebuilt README.md.
- Added source-level comments to Program.cs.
- Added source-level comments to Core.cs.
- Preserved the current Meditation Soundbox ASCII/logo block from Paul's working paste.

### Still deferred

- Speech+music mixed export.
- Full studio-grade voice effects.
- Final KJV parser verification.
