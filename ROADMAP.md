\# Meditation Soundbox — ROADMAP.md



Author: Paul Gydos  

Email: paul@gydos.org  

GitHub: https://github.com/gydos-dot-org/meditation-soundbox



\---



\## Project Vision



Meditation Soundbox is evolving into a Windows-first, local-first, scriptable sound environment for meditation, prayer, Scripture reading, focus, recovery support, and creative work.



\---



\## Core Architecture Direction



Target system:



Session Engine

&#x20;   ↓

Config/Profile System

&#x20;   ↓

Music Bus / Rhythm Bus / Voice Bus

&#x20;   ↓

Transition Engine

&#x20;   ↓

Master Output + Recording + Logs



\---



\### Three-Bus Mixer Model



Frequency / Tones

&#x20;     ↓

Flavor / Music Processing

&#x20;     ↓

Finished Music Output  ─┐

&#x20;                        ├── Master Mix → Output + Recording

Rhythm Output ───────────┤

Voice / Reading Output ──┘



\---



\## Core Components



\### SessionState

\- mode (ambient / hybrid / rhythmic)

\- bpm

\- strength

\- timers

\- active channels



\---



\### MixerState



Three buses:



\- Music (Frequency/Tones processed through Flavor)

\- Rhythm

\- Voice



Music bus internally contains:



\- frequency set (double precision)

\- oscillator slots

\- waveform

\- flavor / synthesis type

\- musical variation

\- strength

\- glide / transitions



Each bus has:

\- volume (current)

\- target volume (for smooth transitions)

\- fade speed / interpolation



\---



\### TransitionEngine

Handles:

\- volume fades

\- BPM transitions

\- frequency glide

\- smooth mode switching



No hard transitions unless forced.



\---



\### KjvReadingBuffer



DisplayBuffer:

\- includes verse labels



ReadingBuffer:

\- words only

\- no verse numbers



\---



\### SessionLogger

Logs:

\- commands

\- timestamps

\- changes



\---



\### ScriptRunner

Supports:

\- timed commands

\- sequences



Example:

freq 528.5

bpm 60

wait 120

read start



\---



\### RecordingManager

\- records all channels

\- runs by default

\- prompts on quit to save/delete



\---



\## Version Roadmap



\---



\## v0.3.3 — Control + Fixes



\- Decimal frequency support (528.5 etc)

\- KJV full chapter parsing

\- Clean reading buffer (no numbers)

\- Default launch command

\- Quit command improvements

\- Timestamped filenames



Filename format:



yYYYYmMMdDD\_aHHMM\_SndBx\_filename.wav



Example:



y2026m04d27\_p0423\_SndBx\_focus.wav



\---



\## v0.4.0 — Mixer Engine



\- Three-bus mixer:

&#x20; - music

&#x20; - rhythm

&#x20; - voice



\- Music bus includes internal oscillator + flavor processing



\- Independent volume controls



\- Smooth transitions:

&#x20; - volume

&#x20; - BPM

&#x20; - frequency



\- Mode blending (ambient/hybrid/rhythmic)



\- Roll-off ending (no hard stop)



\- Session timers



\---



\## v0.5.0 — Scripts + Logs



\- Script files

\- Replay sessions

\- Log viewer

\- Automatic recording logs



\---



\## v0.6.0 — AI Integration



\- Prompt → frequency + BPM

\- Prompt → text buffer

\- Continue text loading



\---



\## v0.7.0 — Voices + GUI



\- Voice selection system

\- Multiple English voices

\- GUI (future)



\---



\## Development Standards



\- Do NOT remove comments without approval

\- Add detailed explanations

\- Maintain:

&#x20; - CHANGELOG.md

&#x20; - DEVLOG.md

\- Clean git commits

\- Production-quality code



\---



\## Next Step



1\. Wire `SndBxConfig` into `Program.cs`

&#x20;  - Implement `--profile default`

&#x20;  - Load voice, frequencies, BPM, mode, and strength from config



2\. Integrate `KjvParser` into the `kjv` command

&#x20;  - Support full chapter loading

&#x20;  - Use clean reading buffer for Piper



3\. Replace export naming with `RecordingNamer`



4\. Update README to reflect:

&#x20;  - scaffolded vs fully wired features

&#x20;  - profile-based launch



\---



End of file

