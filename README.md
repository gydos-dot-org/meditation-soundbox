# Meditation Soundbox

Current version: v0.3.3-dev

Meditation Soundbox is a Windows-first console instrument for:

- generated meditative / electronic soundscapes,
- live frequency changes,
- rhythm and synth flavor changes,
- local Piper text-to-speech,
- pasted text reading,
- KJV / Meta-V scripture loading,
- asynchronous music-only WAV export (music only was unintentional, this feature is incomplete)

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

- KJV parsing may still need tuning depending on the exact Meta-V CSV layout.
- Rhythm/synth labels are broad generated inspirations, not exact hardware emulations.
- Export is currently music-only. Speech+music export has not been completed yet
- The export function might be deferred entirely on the next version in favor of re-playable logs

## Tool Pre-requisites

### Microsoft DotNet SDK version 10+

Currently this window version of Meditation Soundbox requires Microsoft DotNet SDK version 10+
You can see what you do or do not have by:

```powershell
dotnet --version
```

If you don't have v.10+ of some sort, then you can acquire it using this: 
```powershell
winget install -e --id Microsoft.DotNet.SDK.10
```

### Git is also a pre-requisite

To see if you have git
```powershell
git --version
```

If you don't have git then:
```powershell
winget install -e --id Git.Git
```

## Runtime Assets (Required)

This project depends on external runtime assets that are not included in the repository due to size constraints and must be obtained separately.

Run these sections in order on a fresh install.
---

### Meta-V KJV Data (Required)


```powershell
git clone https://github.com/theonize/KJV-bible-database-with-metadata-MetaV-.git .\data\metav

```

### Piper (TTS Engine) (Required)

Download and extract Piper into the expected location:

```powershell
# Create folder
New-Item -ItemType Directory -Force -Path .\tools\piper | Out-Null

# Download Piper (update version if needed)
$zip = ".\tools\piper\piper.zip"
Invoke-WebRequest -Uri https://github.com/rhasspy/piper/releases/latest/download/piper_windows_x64.zip -OutFile $zip

# Extract
Expand-Archive -Path $zip -DestinationPath .\tools\piper -Force

# Cleanup
Remove-Item $zip
```

### Voice Model (Lessac) (Required)

Download the Lessac voice model into the voices folder:

```powershell
# Create folder
New-Item -ItemType Directory -Force -Path .\voices | Out-Null

# Download voice files
Invoke-WebRequest -Uri https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx -OutFile .\voices\en_US-lessac-medium.onnx

Invoke-WebRequest -Uri https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium/en_US-lessac-medium.onnx.json -OutFile .\voices\en_US-lessac-medium.onnx.json
```

### Verify Setup

```powershell
Test-Path ".\tools\piper\piper\piper.exe"
Test-Path ".\voices\en_US-lessac-medium.onnx"
Test-Path ".\voices\en_US-lessac-medium.onnx.json"
Test-Path ".\data\metav\CSV\Verses.csv"
```

All should return: True

## Build

```powershell
dotnet build .\src\MeditativeReader.Windows
```

## Run

### (Run) Shortcut, if implemented properly

``` powershell
dotnet run --project .\src\MeditativeReader.Windows -- --profile default
```

### (Run) Explicit, should always work

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
quit
```

Expected behavior:

- second `read` repeats from the beginning,
- new text forces a fresh speech render,
- KJV passage forces a fresh speech render,

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
export 1 / e 1 (not working as expected currently)
cancel
status
help
quit
```
