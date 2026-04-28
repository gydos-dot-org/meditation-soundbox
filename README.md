# Meditation Soundbox v0.3.2.5

Meditation Soundbox is a Windows-first console instrument for:

- generated meditative / electronic soundscapes,
- live frequency changes,
- rhythm and synth flavor changes,
- local Piper text-to-speech,
- pasted text reading,
- KJV / Meta-V scripture loading,
- asynchronous music-only WAV export.

Project owner: Paul Gydos  
Email: paul@gydos.org  
GitHub: gydos-dot-org

Assistant collaborator: OpenAI ChatGPT, GPT-5.5 Thinking, Kimi Thinking 2.6, Kimi Instant 2.6 

## Current status

This is still a prototype, but it is now useful:

- Live music works.
- Live command control works.
- Pasted text renders through Piper.
- Repeated `read` now restarts from the beginning.
- New text/KJV passages now force fresh Piper rendering.
- [Async music-only export works and prints the output path.] <-- Does it?

## Known limitations

- Export is currently music-only. Speech+music export is intentionally deferred.
- KJV parsing may still need tuning depending on the exact Meta-V CSV layout.
- Rhythm/synth labels are broad generated inspirations, not exact hardware emulations.

## Required local assets

These are intentionally not committed:

```text
tools\piper\piper\piper.exe
voices\en_US-lessac-medium.onnx
voices\en_US-lessac-medium.onnx.json
data\metav\CSV\Verses.csv
```

## After extracting this zip

From the new project folder, copy working assets from v0.3.2.5 or another known-good folder.

Example:

```powershell
cd C:\meditate-soundbox-v0.3.2.5\meditative-reader-starter

Copy-Item -Recurse -Force C:\meditate-soundbox-v0.3.2.5\meditative-reader-starter\tools .\
Copy-Item -Recurse -Force C:\meditate-soundbox-v0.3.2.5\meditative-reader-starter\voices .\
Copy-Item -Recurse -Force C:\meditate-soundbox-v0.3.2.5\meditative-reader-starter\data .\
```

## Build

```powershell
dotnet build .\src\MeditativeReader.Windows
```

## Run

```powershell
dotnet run --project .\src\MeditativeReader.Windows -- `
  --piper ".\tools\piper\piper\piper.exe" `
  --voice ".\voices\en_US-lessac-medium.onnx" `
  --text-file ".\sample.txt"
```

## Test script

Inside the app:

```text
music
text This is Paul. I am a brother in Christ Jesus.
read
read
kjv John 3:16
read
read
e 1
quit
```

Expected behavior:

- second `read` repeats from the beginning,
- new text forces a fresh speech render,
- KJV passage forces a fresh speech render,
- export prints its file path.

## Commands

```text
music / m
read / r
pause / p
restart / b
stop / s
text This is inline text
text              # paste mode; end with .end
freq 432 40 8
mode Ambient|Hybrid|Rhythmic
rhythm FourOnTheFloor|Breakbeat|Dub|Jungle|Garage|HipHop|Trap|Electro|Trance|Acid|None
flavor Analog|Pipe|PurePad|EightOhEight|NineOhNine|Moogish|Mpcish|YamahaFm|CasioToy|ThreeOhThree|AkaiSampler
strength Subtle|Noticeable|Dominant
bpm 92
kjv John 3:16
export 1 / e 1
cancel
status
help
quit
```
