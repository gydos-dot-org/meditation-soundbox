// ============================================================================
// Program.cs - Windows console host for Meditate / Meditation Soundbox
// ============================================================================
//
// This file contains the Windows console application layer.
//
// It connects the platform-neutral Core project to Windows audio playback:
//   - MeditativeReader.Core.AmbientSynthesizer generates stereo music.
//   - PiperTextToSpeech renders spoken text to WAV files.
//   - NAudio plays generated music and Piper-rendered speech.
//   - The command loop accepts interactive commands like music, read, text,
//     kjv, rhythm, flavor, export, and quit.
//
// Project structure relationship:
//   src/MeditativeReader.Core
//     Core synthesis, KJV lookup, Piper wrapper, shared enums/models.
//   src/MeditativeReader.Windows
//     Windows console front-end, playback, and music-only export.
//
// NOTE:
// This project intentionally remains prototype-friendly. Several classes live
// in this one file for easier study and patching. Later they can be split into
// separate files without changing behavior.
//
// ============================================================================

using MeditativeReader.Core;
using MeditativeReader.Config;
using MeditativeReader.Bible;
using NAudio.Wave;

internal static class Program
{
    static async Task<int> Main(string[] args)
    {
        var options = Options.Parse(args);
        var app = new MeditationBox(options);
        return await app.RunAsync();
    }
}

internal sealed class LiveAmbientSampleProvider : ISampleProvider
{
    private readonly AmbientSynthesizer _synth;
    public LiveAmbientSampleProvider(AmbientSynthesizer synth)
    {
        _synth = synth;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
    }
    public WaveFormat WaveFormat { get; }
    public int Read(float[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i += 2)
        {
            var f = _synth.NextStereoFrame();
            buffer[offset + i] = f.Left;
            if (i + 1 < count) buffer[offset + i + 1] = f.Right;
        }
        return count;
    }
}

internal sealed class SpeechPlayer : IDisposable
{
    // AudioFileReader decodes the Piper-generated WAV file.
    private AudioFileReader? _reader;

    // WaveOutEvent sends decoded audio to the Windows output device.
    private WaveOutEvent? _out;

    public void Load(string wavPath)
    {
        // Always fully unload previous speech first. This prevents the old WAV
        // from staying locked and prevents old speech from replaying after new
        // text or a new KJV passage is loaded.
        Unload();

        _reader = new AudioFileReader(wavPath);
        _out = new WaveOutEvent();
        _out.Init(_reader);
    }

    public void PlayFromBeginning()
    {
        if (_reader is null || _out is null)
        {
            return;
        }

        // If the WAV already reached the end, Play() alone will not rewind it.
        // This explicit rewind fixes the "second read does nothing" bug.
        _out.Stop();
        _reader.Position = 0;
        _out.Play();
    }

    public void Play() => _out?.Play();

    public void Toggle()
    {
        if (_out?.PlaybackState == PlaybackState.Playing)
        {
            _out.Pause();
        }
        else
        {
            _out?.Play();
        }
    }

    public void Restart() => PlayFromBeginning();

    public void StopReset()
    {
        _out?.Stop();

        if (_reader is not null)
        {
            _reader.Position = 0;
        }
    }

    public void Unload()
    {
        _out?.Stop();
        _out?.Dispose();
        _reader?.Dispose();

        _out = null;
        _reader = null;
    }

    public void Dispose() => Unload();
}

internal sealed class RecordingMixer
{
    public Task RenderMusicOnlyAsync(AmbientProfile profile, string outputPath, TimeSpan duration, IProgress<double> progress, CancellationToken token)
    {
        return Task.Run(() =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            var fmt = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
            using var writer = new WaveFileWriter(outputPath, fmt);
            var synth = new AmbientSynthesizer(profile, 48000);
            float[] frame = new float[2];
            long total = Math.Max(1, (long)(duration.TotalSeconds * 48000));
            for (long i = 0; i < total; i++)
            {
                token.ThrowIfCancellationRequested();
                var f = synth.NextStereoFrame();
                frame[0] = f.Left; frame[1] = f.Right;
                writer.WriteSamples(frame, 0, 2);
                if (i % 48000 == 0) progress.Report(i / (double)total);
            }
            progress.Report(1);
        }, token);
    }
}

internal sealed class MeditationBox
{
    private readonly Options _o;
    private AmbientProfile _profile;
    private readonly AmbientSynthesizer _synth;
    private readonly WaveOutEvent _music = new();
    private readonly SpeechPlayer _speech = new();
    private readonly RecordingMixer _rec = new();
    private PiperTextToSpeech? _tts;
private string _text = "";

// ------------------------------------------------------------
// Display buffer for labeled scripture output.
// This is separate from _text, which is used for TTS.
// ------------------------------------------------------------
private string _displayText = ""; // labeled display buffer
    private string _speechPath = "";
    // Incremented for every fresh speech render so each new reading uses a new WAV path.
    private int _speechVersion;
    private bool _speechRendered;
    private CancellationTokenSource? _exportCts;
    private Task? _exportTask;

    public MeditationBox(Options o)
    {
        _o = o;
        _profile = new AmbientProfile().With(o.Freqs, o.Strength, o.Mode, o.Rhythm, o.Flavor, o.Bpm);
        _synth = new AmbientSynthesizer(_profile);
        _music.Init(new LiveAmbientSampleProvider(_synth));
    }

    public async Task<int> RunAsync()
    {
        _text = await TextSource.TryReadFileAsync(_o.TextFile) ?? "Create in me a clean heart, O God; and renew a right spirit within me.";
        Directory.CreateDirectory("speech-cache");
        _speechPath = Path.Combine("speech-cache", "current-reading.wav");
        if (_o.Piper is not null && _o.Voice is not null)
            _tts = new PiperTextToSpeech(_o.Piper, _o.Voice, _o.LengthScale, _o.SentenceSilence);

        Header();
        while (true)
        {
            PollExport();
            Console.Write("MBOX> ");
            var line = Console.ReadLine();
            if (line is null) continue;
            if (await Cmd(line.Trim())) break;
        }
        await StopExport();
        _music.Dispose(); _speech.Dispose();
        return 0;
    }

    private async Task<bool> Cmd(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;
        var cmd = parts[0].ToLowerInvariant();
        try
        {
            switch (cmd)
            {
                case "m": case "music": ToggleMusic(); break;
                case "r": case "read": await Read(); break;
                case "p": case "pause": _speech.Toggle(); break;
                case "b": case "restart": _speech.Restart(); break;
                case "s": case "stop": _speech.StopReset(); break;
                case "text":
                    // text can be used two ways:
                    //   text This is inline text
                    //   text   -> paste block mode, ended by .end
                    string inlineText = line.Length > 4 ? line[4..].Trim() : "";
                    string newText = string.IsNullOrWhiteSpace(inlineText)
                        ? TextSource.ReadConsoleBlock()
                        : inlineText;

                    if (string.IsNullOrWhiteSpace(newText))
                    {
                        Console.WriteLine("Text was empty; keeping previous reading text.");
                    }
                    else
                    {
                        _text = newText;
                        ResetSpeechForNewText();
                        Console.WriteLine($"New reading text loaded: {_text.Length} characters.");
                    }
                    break;
                case "freq": SetFreq(parts.Skip(1)); break;
                case "mode": SetEnum(parts.Skip(1), (MusicMode v) => _profile = _profile.With(musicMode:v)); break;
                case "rhythm": SetEnum(parts.Skip(1), (RhythmMode v) => _profile = _profile.With(rhythm:v, musicMode:v == RhythmMode.None ? MusicMode.Ambient : MusicMode.Hybrid)); break;
                case "flavor": case "synth": SetEnum(parts.Skip(1), (SynthFlavor v) => _profile = _profile.With(flavor:v)); break;
                case "strength": SetEnum(parts.Skip(1), (FrequencyStrength v) => _profile = _profile.With(strength:v)); break;
                case "bpm": if (double.TryParse(parts.ElementAtOrDefault(1), out var bpm)) { _profile = _profile.With(bpm:bpm); Apply(); } break;
                case "kjv":
{
    // ------------------------------------------------------------
    // KJV LOADING PIPELINE (v0.3.3-dev)
    //
    // This command now loads scripture into TWO buffers:
    //
    // 1. _displayText → contains full labeled verses
    //    Example:
    //      John 3:16 For God so loved the world...
    //
    // 2. _text → contains CLEAN verse text ONLY
    //    Example:
    //      For God so loved the world...
    //
    // Why this matters:
    // - Piper should NOT read verse numbers (it sounds unnatural)
    // - The user SHOULD still SEE verse labels in the console
    //
    // This implements the ROADMAP concept:
    //   DisplayBuffer vs ReadingBuffer
    // ------------------------------------------------------------

    string reference = line.Length > 3 ? line[3..].Trim() : "";

    var passage = new KjvLoader(Path.Combine("data", "metav", "CSV", "Verses.csv"))
        .LoadPassage(reference);

    // If nothing loaded (file missing or no match), show message only
    if (string.IsNullOrWhiteSpace(passage.SpeechText))
    {
        Console.WriteLine(passage.DisplayText);
        break;
    }

    // Store both buffers
    _displayText = passage.DisplayText;
    _text = passage.SpeechText;

    // Force Piper to re-render audio for new content
    ResetSpeechForNewText();

    // Show labeled verses to the user
    Console.WriteLine(_displayText);
    Console.WriteLine();
    Console.WriteLine($"Loaded {passage.VerseCount} verse(s).");

    break;
}
                case "e": case "export": StartExport(parts.ElementAtOrDefault(1)); break;
                case "cancel": _exportCts?.Cancel(); Console.WriteLine("Cancel requested."); break;
                case "status": Status(); break;
                case "h": case "help": Help(); break;
                case "q": case "quit": return true;
                default: Console.WriteLine("Unknown command. Type help."); break;
            }
        }
        catch (Exception ex) { Console.WriteLine("ERROR: " + ex.Message); }
        return false;
    }

    private void ToggleMusic()
    {
        if (_music.PlaybackState == PlaybackState.Playing) { _music.Pause(); Console.WriteLine("Music paused."); }
        else { _music.Play(); Console.WriteLine("Music playing."); }
    }

    private async Task Read()
    {
        if (!_speechRendered)
        {
            if (_tts is null || !_tts.IsConfigured())
            {
                Console.WriteLine("Speech is not configured.");
                return;
            }

            // Use a fresh file name on every render. This avoids stale cache
            // problems when a previous AudioFileReader still has the old WAV
            // open or when Windows has not released it yet.
            _speechPath = Path.Combine(
                "speech-cache",
                $"current-reading-{++_speechVersion}.wav");

            Console.WriteLine("Rendering speech...");
            await _tts.RenderToWavAsync(_text, _speechPath);

            _speech.Load(_speechPath);
            _speechRendered = true;
        }

        // Always start from the beginning. This makes repeated read commands
        // predictable after text, KJV, or manual restart operations.
        _speech.PlayFromBeginning();
        Console.WriteLine("Reading playing from beginning.");
    }

    private void SetFreq(IEnumerable<string> vals)
    {
        var f = vals.Select(v => double.TryParse(v, out var d) ? d : -1).Where(d => d > 0).Take(3).ToArray();
        if (f.Length == 0) { Console.WriteLine("Usage: freq 528 40 8"); return; }
        _profile = _profile.With(frequencies:f); Apply();
    }

    private void SetEnum<T>(IEnumerable<string> vals, Action<T> setter) where T: struct
    {
        var s = string.Join("", vals);
        if (Enum.TryParse<T>(s, true, out var v)) { setter(v); Apply(); }
        else Console.WriteLine("Invalid value. Type help.");
    }

    private void Apply() { _synth.UpdateProfile(_profile); Status(); }

    private void ResetSpeechForNewText()
    {
        // New text means the previous rendered WAV is no longer authoritative.
        // Unload the old speech player and mark speech as needing a fresh Piper
        // render on the next read command.
        _speech.Unload();
        _speechRendered = false;
    }


    private void StartExport(string? min)
    {
        if (_exportTask is { IsCompleted:false }) { Console.WriteLine("Export already running."); return; }
        double minutes = double.TryParse(min, out var m) && m > 0 ? m : 1;
        Directory.CreateDirectory("exports");
        string path = Path.Combine("exports", $"meditation-box-{DateTime.Now:yyyyMMdd-HHmmss}.wav");
        _exportCts = new CancellationTokenSource();
        var progress = new Progress<double>(p => { if (Math.Abs((p*100)%20) < 1) Console.WriteLine($"Export {p:P0}"); });
        _exportTask = _rec.RenderMusicOnlyAsync(_profile, path, TimeSpan.FromMinutes(minutes), progress, _exportCts.Token);
        Console.WriteLine($"Async music-only export started: {path}");
        Console.WriteLine("You can type cancel or quit.");
    }

    private void PollExport()
    {
        if (_exportTask is null) return;
        if (_exportTask.IsCompletedSuccessfully) { Console.WriteLine("Export complete. Check the exports folder for the newest meditation-box-*.wav file."); _exportTask = null; }
        else if (_exportTask.IsCanceled) { Console.WriteLine("Export canceled."); _exportTask = null; }
        else if (_exportTask.IsFaulted) { Console.WriteLine("Export failed: " + _exportTask.Exception?.GetBaseException().Message); _exportTask = null; }
    }

    private async Task StopExport()
    {
        if (_exportTask is { IsCompleted:false })
        {
            _exportCts?.Cancel();
            try { await _exportTask; } catch { }
        }
    }

    private void Status() => Console.WriteLine($"Mode={_profile.MusicMode}; Rhythm={_profile.RhythmMode}; Flavor={_profile.SynthFlavor}; BPM={_profile.Bpm}; Strength={_profile.FrequencyStrength}; Freq={string.Join(",", _profile.FrequenciesHz)}");

private static void Header()
{
    // ============================================================
    // ORIGINAL ASCII BANNER (PRESERVED EXACTLY)
    //
    // Key principle:
    //   - DO NOT reconstruct this with B(n)
    //   - DO NOT alter spacing
    //   - This is pixel-art made of text
    //
    // We ONLY add color — we do NOT modify geometry.
    // ============================================================

    var old = Console.ForegroundColor;

    void L(ConsoleColor c, string t)
    {
        Console.ForegroundColor = c;
        Console.WriteLine(t);
    }

    Console.WriteLine();

    // TOP HALF
    L(ConsoleColor.White,      "███    ███ ███████ ██████  ██ ████████  █████  ████████ ██  ██████  ███    ██");
    L(ConsoleColor.Yellow,     "████  ████ ██      ██   ██ ██    ██    ██   ██    ██    ██ ██    ██ ████   ██");
    L(ConsoleColor.DarkYellow, "██ ████ ██ █████   ██   ██ ██    ██    ███████    ██    ██ ██    ██ ██ ██  ██");
    L(ConsoleColor.Red,        "██  ██  ██ ██      ██   ██ ██    ██    ██   ██    ██    ██ ██    ██ ██  ██ ██");
    L(ConsoleColor.White,      "██      ██ ███████ ██████  ██    ██    ██   ██    ██    ██  ██████  ██   ████");

    Console.WriteLine();

    // BOTTOM HALF (THIS IS WHERE YOUR "BOX" WAS BREAKING)
    L(ConsoleColor.Yellow,     "███████  ██████  ██    ██ ███    ██ ██████  ██████   ██████  ██   ██");
    L(ConsoleColor.DarkYellow, "██      ██    ██ ██    ██ ████   ██ ██   ██ ██   ██ ██    ██  ██ ██");
    L(ConsoleColor.Red,        "███████ ██    ██ ██    ██ ██ ██  ██ ██   ██ ██████  ██    ██   ███");
    L(ConsoleColor.DarkYellow, "     ██ ██    ██ ██    ██ ██  ██ ██ ██   ██ ██   ██ ██    ██  ██ ██");
    L(ConsoleColor.White,      "███████  ██████   ██████  ██   ████ ██████  ██████   ██████  ██   ██");

    Console.ForegroundColor = old;

    Console.WriteLine();
    Console.WriteLine("                    MEDITATION SOUNDBOX v0.3.3-dev");
    Console.WriteLine();

    Help();
}
    private static void Help() => Console.WriteLine(@"Commands:
  music/m, read/r, pause/p, restart/b, stop/s
  text [words...]            set inline text, or paste block ending with .end
  freq 432 40 8             change frequencies live
  mode Ambient|Hybrid|Rhythmic
  rhythm FourOnTheFloor|Breakbeat|Dub|Jungle|Garage|HipHop|Trap|Electro|Trance|Acid|None
  flavor Analog|Pipe|PurePad|EightOhEight|NineOhNine|Moogish|Mpcish|YamahaFm|CasioToy|ThreeOhThree|AkaiSampler
  strength Subtle|Noticeable|Dominant
  bpm 92
  kjv John 3:16           load single verse
  kjv John 3              load full chapter
  kjv Proverbs 24:3-4     load verse range
  export 1 | e 1             async music-only export
  cancel, status, help, quit");
}

internal sealed class Options
{
    public List<double> Freqs { get; } = new();
    public string? Piper { get; private set; }
    public string? Voice { get; private set; }
    public string? TextFile { get; private set; }
    public string? ProfileName { get; private set; }

    public double LengthScale { get; private set; } = 1.25;
    public double SentenceSilence { get; private set; } = 0.45;
    public FrequencyStrength Strength { get; private set; } = FrequencyStrength.Noticeable;
    public MusicMode Mode { get; private set; } = MusicMode.Ambient;
    public RhythmMode Rhythm { get; private set; } = RhythmMode.None;
    public SynthFlavor Flavor { get; private set; } = SynthFlavor.Analog;
    public double Bpm { get; private set; } = 92;

    public static Options Parse(string[] args)
    {
        var o = new Options();

        // ------------------------------------------------------------
        // v0.3.3-dev profile bootstrap
        // ------------------------------------------------------------
        // The older app required long command lines such as:
        //
        //   --piper ".\tools\piper\piper\piper.exe"
        //   --voice ".\voices\en_US-lessac-medium.onnx"
        //
        // The new profile system lets the user say:
        //
        //   --profile default
        //
        // This keeps the old command-line arguments working while allowing
        // profile values to become the starting/default configuration.
        // Explicit command-line arguments below still override the profile.
        // ------------------------------------------------------------

        string? requestedProfile = FindOptionValue(args, "--profile");

        if (!string.IsNullOrWhiteSpace(requestedProfile))
        {
            o.ProfileName = requestedProfile;
            ApplyProfileDefaults(o, requestedProfile);
        }

        for (int i = 0; i < args.Length; i++)
        {
            string? next() => i + 1 < args.Length ? args[++i] : null;

            switch (args[i])
            {
                case "--profile":
                    // Already handled above. Consume its value here so the parser
                    // does not accidentally treat the profile name as another option.
                    _ = next();
                    break;

                case "--freq":
                    if (double.TryParse(next(), out var f))
                    {
                        o.Freqs.Add(f);
                    }
                    break;

                case "--piper":
                    o.Piper = next();
                    break;

                case "--voice":
                    o.Voice = next();
                    break;

                case "--text-file":
                    o.TextFile = next();
                    break;

                case "--length-scale":
                    if (double.TryParse(next(), out var l))
                    {
                        o.LengthScale = l;
                    }
                    break;

                case "--sentence-silence":
                    if (double.TryParse(next(), out var s))
                    {
                        o.SentenceSilence = s;
                    }
                    break;

                case "--frequency-strength":
                    if (Enum.TryParse<FrequencyStrength>(next(), true, out var fs))
                    {
                        o.Strength = fs;
                    }
                    break;

                case "--music-mode":
                    if (Enum.TryParse<MusicMode>(next(), true, out var mm))
                    {
                        o.Mode = mm;
                    }
                    break;

                case "--rhythm":
                    if (Enum.TryParse<RhythmMode>(next(), true, out var rm))
                    {
                        o.Rhythm = rm;

                        // Preserve the legacy convenience behavior:
                        // choosing a rhythm automatically moves the soundbox
                        // out of purely ambient mode unless the rhythm is None.
                        if (rm != RhythmMode.None)
                        {
                            o.Mode = MusicMode.Hybrid;
                        }
                    }
                    break;

                case "--synth-flavor":
                    if (Enum.TryParse<SynthFlavor>(next(), true, out var sf))
                    {
                        o.Flavor = sf;
                    }
                    break;

                case "--bpm":
                    if (double.TryParse(next(), out var bpm))
                    {
                        o.Bpm = bpm;
                    }
                    break;
            }
        }

        // If neither profile nor command line supplied frequencies, use the
        // historical defaults so old launch behavior remains familiar.
        if (o.Freqs.Count == 0)
        {
            o.Freqs.AddRange([528.0, 40.0, 8.0]);
        }

        return o;
    }

    private static string? FindOptionValue(string[] args, string optionName)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static void ApplyProfileDefaults(Options o, string profileName)
    {
        var profile = SndBxConfig.Load(profileName);

        o.Piper = profile.PiperPath;
        o.Voice = profile.VoicePath;
        o.TextFile = profile.DefaultTextFile;
        o.Bpm = profile.Bpm;

        o.Freqs.Clear();

        foreach (var frequency in profile.Frequencies.Where(v => v > 0))
        {
            o.Freqs.Add(frequency);
        }

        if (Enum.TryParse<MusicMode>(profile.Mode, true, out var mode))
        {
            o.Mode = mode;
        }

        if (Enum.TryParse<FrequencyStrength>(profile.Strength, true, out var strength))
        {
            o.Strength = strength;
        }

        if (Enum.TryParse<RhythmMode>(profile.Rhythm, true, out var rhythm))
        {
            o.Rhythm = rhythm;
        }

        if (Enum.TryParse<SynthFlavor>(profile.Flavor, true, out var flavor))
        {
            o.Flavor = flavor;
        }
    }
}





