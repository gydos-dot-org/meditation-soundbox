\# Meditation Soundbox — ROADMAP.md

Author: Paul Gydos (paul@gydos.org)  

GitHub: https://github.com/gydos-dot-org/meditation-soundbox  



\---



\## Project Vision



Meditation Soundbox is evolving into a multi-channel, scriptable, AI-assisted audio environment for:



\- Focus

\- Prayer / Scripture meditation

\- Creative work

\- Recovery support

\- Deep mental state tuning



\---



\## Architecture Direction



Current:

Single profile → single audio output



Target:

Session → Mixer → Channels → Transition Engine → Output + Recording



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

Channels:

\- Frequency

\- Music

\- Rhythm

\- Voice



Each channel:

\- volume

\- target volume

\- fade speed



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



\- Independent channels:

&#x20; - frequency

&#x20; - music

&#x20; - rhythm

&#x20; - voice



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



Create branch:



git checkout -b v0.3.3-dev



Commit:



git add ROADMAP.md

git commit -m "Add roadmap"

git push -u origin v0.3.3-dev



\---



End of file

