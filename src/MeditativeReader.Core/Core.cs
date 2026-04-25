// ============================================================================
// Core.cs - platform-neutral engine for Meditation Soundbox
// ============================================================================
//
// This file contains the code that should remain reusable outside Windows UI:
//
// - StereoFrame: one left/right audio frame.
// - AmbientProfile: current music settings.
// - AmbientSynthesizer: procedural sound generator.
// - PiperTextToSpeech: wrapper around external Piper executable.
// - TextSource: file/console text helpers.
// - KjvLookup: Meta-V KJV CSV passage lookup.
//
// Windows-specific playback is intentionally not here. NAudio and console
// command handling live in src/MeditativeReader.Windows/Program.cs.
//
// ============================================================================

using System.Diagnostics;
using System.Text;

namespace MeditativeReader.Core;

public readonly record struct StereoFrame(float Left, float Right);
public enum FrequencyStrength { Subtle, Noticeable, Dominant }
public enum MusicMode { Ambient, Hybrid, Rhythmic }
public enum RhythmMode { None, FourOnTheFloor, Breakbeat, Dub, Jungle, Garage, HipHop, Trap, Electro, Trance, Acid }
public enum SynthFlavor { PurePad, Pipe, Analog, EightOhEight, NineOhNine, Moogish, Mpcish, YamahaFm, CasioToy, ThreeOhThree, AkaiSampler }
public enum VoiceEffect { Clean, Compressor, Distortion, Robot }

public sealed class AmbientProfile
{
    public IReadOnlyList<double> FrequenciesHz { get; init; } = new[] { 528.0, 40.0, 8.0 };
    public FrequencyStrength FrequencyStrength { get; init; } = FrequencyStrength.Noticeable;
    public MusicMode MusicMode { get; init; } = MusicMode.Ambient;
    public RhythmMode RhythmMode { get; init; } = RhythmMode.None;
    public SynthFlavor SynthFlavor { get; init; } = SynthFlavor.Analog;
    public double Bpm { get; init; } = 92.0;
    public double Volume { get; init; } = 0.32;
    public double RootHz { get; init; } = 216.0;

    public AmbientProfile With(
        IEnumerable<double>? frequencies = null,
        FrequencyStrength? strength = null,
        MusicMode? musicMode = null,
        RhythmMode? rhythm = null,
        SynthFlavor? flavor = null,
        double? bpm = null)
    {
        return new AmbientProfile
        {
            FrequenciesHz = frequencies?.Where(f => f > 0).Take(3).ToArray() ?? FrequenciesHz,
            FrequencyStrength = strength ?? FrequencyStrength,
            MusicMode = musicMode ?? MusicMode,
            RhythmMode = rhythm ?? RhythmMode,
            SynthFlavor = flavor ?? SynthFlavor,
            Bpm = bpm ?? Bpm,
            Volume = Volume,
            RootHz = RootHz
        };
    }
}

public sealed class AmbientSynthesizer
{
    private const double Tau = Math.PI * 2.0;
    private readonly int _sampleRate;
    private readonly Random _random = new(1337);
    private readonly object _gate = new();
    private AmbientProfile _profile;
    private double _time, _p1, _p2, _p3, _pHi, _noise;
    private int _lastStep = -1;
    private DrumVoice _kick = DrumVoice.Silent(), _snare = DrumVoice.Silent(), _hat = DrumVoice.Silent(), _perc = DrumVoice.Silent();

    public AmbientSynthesizer(AmbientProfile profile, int sampleRate = 48000)
    {
        _profile = profile;
        _sampleRate = sampleRate;
    }

    public AmbientProfile CurrentProfile { get { lock (_gate) return _profile; } }
    public void UpdateProfile(AmbientProfile profile) { lock (_gate) _profile = profile; }

    public StereoFrame NextStereoFrame()
    {
        AmbientProfile p;
        lock (_gate) p = _profile;

        _time += 1.0 / _sampleRate;
        double[] ratios = ChordRatios(_time);
        double padGain = p.MusicMode == MusicMode.Rhythmic ? 0.09 : p.MusicMode == MusicMode.Hybrid ? 0.14 : 0.20;

        double left = 0, right = 0;
        left += Osc(ref _p1, p.RootHz * ratios[0], p.SynthFlavor) * padGain * 0.90;
        right += Osc(ref _p1, p.RootHz * ratios[0] * 1.002, p.SynthFlavor) * padGain * 0.82;
        left += Osc(ref _p2, p.RootHz * ratios[1], p.SynthFlavor) * padGain * 0.58;
        right += Osc(ref _p2, p.RootHz * ratios[1] * 0.998, p.SynthFlavor) * padGain * 0.68;
        left += Osc(ref _p3, p.RootHz * ratios[2], p.SynthFlavor) * padGain * 0.45;
        right += Osc(ref _p3, p.RootHz * ratios[2] * 1.003, p.SynthFlavor) * padGain * 0.50;

        double mod = 1.0;
        foreach (var f in p.FrequenciesHz.Where(f => f < 20))
            mod += 0.018 * Math.Sin(Tau * f * _time);

        foreach (var f in p.FrequenciesHz.Where(f => f >= 20).Take(3))
        {
            double g = FrequencyGain(f, p.FrequencyStrength);
            left += Math.Sin(Tau * f * _time) * g * 0.75;
            right += Math.Sin(Tau * (f * 1.0015) * _time) * g * 0.70;
        }

        _noise = (_noise * 0.995) + (((_random.NextDouble() * 2) - 1) * 0.005);
        left += _noise * 0.045;
        right += _noise * 0.038;

        var drums = NextDrums(p);
        left += drums.Left;
        right += drums.Right;

        double shimmer = Osc(ref _pHi, 1728, SynthFlavor.Pipe) * 0.006 * (0.5 + 0.5 * Math.Sin(Tau * 0.017 * _time));
        left += shimmer * 0.7;
        right += shimmer;

        double breath = 0.72 + 0.28 * Math.Sin(Tau * 0.035 * _time);
        return new StereoFrame((float)Math.Tanh(left * p.Volume * breath * mod), (float)Math.Tanh(right * p.Volume * breath * mod));
    }

    private static double[] ChordRatios(double t)
    {
        return ((int)Math.Floor(t / 24.0) % 4) switch
        {
            0 => new[] { 1.0, 1.25, 1.5 },
            1 => new[] { 1.0, 1.333333333, 1.5 },
            2 => new[] { 0.833333333, 1.0, 1.333333333 },
            _ => new[] { 0.75, 1.0, 1.25 }
        };
    }

    private StereoFrame NextDrums(AmbientProfile p)
    {
        if (p.RhythmMode == RhythmMode.None || p.MusicMode == MusicMode.Ambient) return new StereoFrame(0, 0);
        int step = ((int)Math.Floor(_time * p.Bpm / 60.0 * 4.0)) % 16;
        if (step != _lastStep) { Trigger(step, p); _lastStep = step; }
        double h = _hat.Next(_sampleRate);
        double mono = (_kick.Next(_sampleRate) + _snare.Next(_sampleRate) + h + _perc.Next(_sampleRate)) * (p.MusicMode == MusicMode.Hybrid ? 0.22 : 0.44);
        return new StereoFrame((float)(mono - h * 0.03), (float)(mono + h * 0.03));
    }

    private void Trigger(int step, AmbientProfile p)
    {
        var pat = Pattern(p.RhythmMode);
        if (pat.k.Contains(step)) _kick = DrumVoice.Kick(p.SynthFlavor);
        if (pat.s.Contains(step)) _snare = DrumVoice.Snare(p.SynthFlavor);
        if (pat.h.Contains(step)) _hat = DrumVoice.Hat(p.SynthFlavor);
        if (pat.p.Contains(step)) _perc = DrumVoice.Perc(p.SynthFlavor);
    }

    private static (int[] k, int[] s, int[] h, int[] p) Pattern(RhythmMode m) => m switch
    {
        RhythmMode.FourOnTheFloor => (new[] {0,4,8,12}, new[] {4,12}, new[] {0,2,4,6,8,10,12,14}, Array.Empty<int>()),
        RhythmMode.Breakbeat => (new[] {0,6,10}, new[] {4,12}, new[] {0,2,3,6,8,10,11,14}, new[] {7,15}),
        RhythmMode.Dub => (new[] {0,10}, new[] {12}, new[] {2,6,10,14}, new[] {5,13}),
        RhythmMode.Jungle => (new[] {0,3,10}, new[] {4,7,12}, new[] {0,2,3,5,6,8,10,11,13,14}, new[] {1,9,15}),
        RhythmMode.Garage => (new[] {0,7,10}, new[] {4,12}, new[] {1,3,5,7,9,11,13,15}, new[] {6,14}),
        RhythmMode.HipHop => (new[] {0,6,11}, new[] {4,12}, new[] {0,4,8,12}, new[] {3,10,15}),
        RhythmMode.Trap => (new[] {0,7,11}, new[] {4,12}, new[] {0,1,2,4,6,7,8,10,12,13,14,15}, new[] {5,9}),
        RhythmMode.Electro => (new[] {0,3,8,11}, new[] {4,12}, new[] {0,2,4,6,8,10,12,14}, new[] {6,10,15}),
        RhythmMode.Trance => (new[] {0,4,8,12}, new[] {4,12}, new[] {2,6,10,14}, new[] {1,5,9,13}),
        RhythmMode.Acid => (new[] {0,4,8,12}, new[] {4,12}, new[] {0,2,4,6,8,10,12,14}, new[] {3,7,11,15}),
        _ => (Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>())
    };

    private double Osc(ref double phase, double hz, SynthFlavor flavor)
    {
        phase += Tau * hz / _sampleRate;
        while (phase > Tau) phase -= Tau;
        double sine = Math.Sin(phase), second = Math.Sin(phase * 2.0), saw = (2 * (phase / Tau)) - 1;
        return flavor switch
        {
            SynthFlavor.Pipe => sine + 0.08 * Math.Sin(phase * 3),
            SynthFlavor.PurePad => sine,
            SynthFlavor.Moogish => Math.Tanh((sine + 0.35 * second) * 1.6),
            SynthFlavor.YamahaFm => sine + 0.2 * Math.Sin(phase + 2 * Math.Sin(phase * 2)),
            SynthFlavor.CasioToy => Math.Round(sine * 12) / 12,
            SynthFlavor.ThreeOhThree => Math.Tanh(saw * 2.5),
            SynthFlavor.EightOhEight => sine + 0.18 * second,
            SynthFlavor.NineOhNine => sine + 0.12 * Math.Sin(phase * 3),
            SynthFlavor.Mpcish => Math.Round((sine + 0.08 * second) * 24) / 24,
            SynthFlavor.AkaiSampler => Math.Round((sine + 0.14 * second) * 32) / 32,
            _ => (sine + 0.24 * second + 0.08 * Math.Sin(phase * 3)) / 1.32
        };
    }

    private static double FrequencyGain(double f, FrequencyStrength s)
    {
        double b = f is >= 35 and <= 45 ? 0.012 : 0.025;
        return s switch { FrequencyStrength.Subtle => b * 0.5, FrequencyStrength.Dominant => b * 2.0, _ => b };
    }

    private sealed class DrumVoice
    {
        private double _age, _phase;
        private readonly double _hz, _decay, _tone, _noise;
        private DrumVoice(double hz, double decay, double tone, double noise) { _hz = hz; _decay = decay; _tone = tone; _noise = noise; }
        public static DrumVoice Silent() => new(0, 1, 0, 0);
        public static DrumVoice Kick(SynthFlavor f) => new(f == SynthFlavor.EightOhEight ? 46 : 60, 0.18, 1, 0);
        public static DrumVoice Snare(SynthFlavor f) => new(190, 0.09, 0.25, f == SynthFlavor.NineOhNine ? 0.8 : 0.55);
        public static DrumVoice Hat(SynthFlavor f) => new(7000, 0.035, 0.06, 0.8);
        public static DrumVoice Perc(SynthFlavor f) => new(850, 0.07, 0.35, 0.25);
        public double Next(int sr)
        {
            if (_hz <= 0) return 0;
            _age += 1.0 / sr;
            double env = Math.Exp(-_age / _decay);
            if (env < 0.0001) return 0;
            _phase += Tau * _hz * (1 + Math.Exp(-_age / 0.04)) / sr;
            while (_phase > Tau) _phase -= Tau;
            return ((Math.Sin(_phase) * _tone) + (((Random.Shared.NextDouble()*2)-1) * _noise)) * env;
        }
    }
}

public sealed class PiperTextToSpeech
{
    public string PiperExecutablePath { get; }
    public string VoiceModelPath { get; }
    public double LengthScale { get; }
    public double SentenceSilence { get; }

    public PiperTextToSpeech(string piperExecutablePath, string voiceModelPath, double lengthScale = 1.25, double sentenceSilence = 0.45)
    {
        PiperExecutablePath = piperExecutablePath;
        VoiceModelPath = voiceModelPath;
        LengthScale = lengthScale;
        SentenceSilence = sentenceSilence;
    }

    public bool IsConfigured() => File.Exists(PiperExecutablePath) && File.Exists(VoiceModelPath);

    public async Task RenderToWavAsync(string text, string outputWavPath, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured()) throw new FileNotFoundException("Piper is not configured.");
        Directory.CreateDirectory(Path.GetDirectoryName(outputWavPath)!);
        var psi = new ProcessStartInfo
        {
            FileName = PiperExecutablePath,
            Arguments = $"--model \"{VoiceModelPath}\" --length_scale {LengthScale:0.###} --sentence_silence {SentenceSilence:0.###} --output_file \"{outputWavPath}\"",
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        using var p = new Process { StartInfo = psi };
        p.Start();
        await p.StandardInput.WriteAsync(text.AsMemory(), cancellationToken);
        p.StandardInput.Close();
        string err = await p.StandardError.ReadToEndAsync(cancellationToken);
        await p.WaitForExitAsync(cancellationToken);
        if (p.ExitCode != 0) throw new InvalidOperationException(err);
    }
}

public static class TextSource
{
    public static async Task<string?> TryReadFileAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path, Encoding.UTF8);
    }

    public static string ReadConsoleBlock()
    {
        Console.WriteLine("Paste text. End with a line containing only .end");
        var lines = new List<string>();
        while (true)
        {
            string? line = Console.ReadLine();
            if (line is null || line.Trim() == ".end") break;
            lines.Add(line);
        }
        return string.Join(Environment.NewLine, lines);
    }
}


public sealed class KjvLookup
{
    private readonly string _csvPath;

    public KjvLookup(string csvPath) => _csvPath = csvPath;

    /// <summary>
    /// Read a KJV passage from the Meta-V CSV.
    ///
    /// This version is intentionally schema-aware and forgiving:
    /// - it uses header names when possible,
    /// - it maps numeric book ids to Bible book names when needed,
    /// - it falls back to row heuristics when headers are unfamiliar.
    ///
    /// Supported examples:
    /// - John 3:16
    /// - Proverbs 24:3-4
    /// - 1 John 1:9
    /// </summary>
    public string ReadPassage(string reference)
    {
        if (!File.Exists(_csvPath))
        {
            return "Meta-V CSV not found. Run scripts/windows/download-metav.ps1 with ExecutionPolicy Bypass.";
        }

        var parsed = BibleReference.TryParse(reference);
        if (parsed is null)
        {
            return "Could not parse KJV reference. Try: John 3:16 or Proverbs 24:3-4";
        }

        using var reader = new StreamReader(_csvPath, Encoding.UTF8);

        string? firstLine = reader.ReadLine();
        if (firstLine is null)
        {
            return "Meta-V CSV exists but is empty.";
        }

        var firstFields = SplitCsv(firstLine);
        bool firstLineIsHeader = LooksLikeHeader(firstFields);
        var header = firstLineIsHeader ? firstFields.Select(NormalizeHeader).ToList() : new List<string>();

        var hits = new List<(int Verse, string Text)>();

        if (!firstLineIsHeader)
        {
            TryAddRow(firstFields, header, parsed, hits);
        }

        while (!reader.EndOfStream)
        {
            string? line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            TryAddRow(SplitCsv(line), header, parsed, hits);
        }

        var ordered = hits
            .Where(h => h.Verse >= parsed.StartVerse && h.Verse <= parsed.EndVerse)
            .GroupBy(h => h.Verse)
            .Select(g => g.First())
            .OrderBy(h => h.Verse)
            .ToList();

        if (ordered.Count == 0)
        {
            return $"No verses found for {reference}. Meta-V is present at {_csvPath}, but parser tuning is still needed.";
        }

        return string.Join(Environment.NewLine, ordered.Select(h =>
            $"{parsed.Book} {parsed.Chapter}:{h.Verse} {h.Text}"));
    }

    private static void TryAddRow(
        List<string> fields,
        List<string> header,
        BibleReference parsed,
        List<(int Verse, string Text)> hits)
    {
        if (fields.Count < 4)
        {
            return;
        }

        // Exact Meta-V Verses.csv path:
        // VerseID, BookID, Chapter, VerseNum, OsisRef, VerseText
        int? exactBookId = TryGetIntByHeader(fields, header, "bookid");
        int? exactChapter = TryGetIntByHeader(fields, header, "chapter");
        int? exactVerse = TryGetIntByHeader(fields, header, "versenum");
        string? exactText = TryGetByHeader(fields, header, "versetext");

        if (exactBookId is not null &&
            exactChapter is not null &&
            exactVerse is not null &&
            exactText is not null)
        {
            if (BookNumberFor(parsed.Book) == exactBookId.Value &&
                parsed.Chapter == exactChapter.Value &&
                exactVerse.Value >= parsed.StartVerse &&
                exactVerse.Value <= parsed.EndVerse)
            {
                hits.Add((exactVerse.Value, exactText.Trim()));
            }

            return;
        }

        string wantedBookNorm = NormalizeBook(parsed.Book);

        string? bookName = TryGetByHeader(fields, header, "bookname")
            ?? TryGetByHeader(fields, header, "book")
            ?? TryGetByHeader(fields, header, "book_name")
            ?? TryFindBookNameField(fields);

        int? bookNumber = TryGetIntByHeader(fields, header, "booknumber")
            ?? TryGetIntByHeader(fields, header, "booknum")
            ?? TryGetIntByHeader(fields, header, "bookid")
            ?? TryGetIntByHeader(fields, header, "book")
            ?? TryGuessBookNumber(fields);

        bool bookMatches = false;

        if (!string.IsNullOrWhiteSpace(bookName))
        {
            bookMatches = NormalizeBook(bookName).Equals(wantedBookNorm, StringComparison.OrdinalIgnoreCase);
        }

        if (!bookMatches && bookNumber is not null)
        {
            bookMatches = BookNumberFor(parsed.Book) == bookNumber.Value;
        }

        if (!bookMatches)
        {
            return;
        }

        int? chapter = TryGetIntByHeader(fields, header, "chapter")
            ?? TryGetIntByHeader(fields, header, "chapterid")
            ?? TryGetIntByHeader(fields, header, "c")
            ?? TryGuessChapter(fields, parsed);

        int? verse = TryGetIntByHeader(fields, header, "verse")
            ?? TryGetIntByHeader(fields, header, "versenum")
            ?? TryGetIntByHeader(fields, header, "versenumber")
            ?? TryGetIntByHeader(fields, header, "verseid")
            ?? TryGetIntByHeader(fields, header, "v")
            ?? TryGuessVerse(fields, parsed);

        if (chapter != parsed.Chapter || verse is null)
        {
            return;
        }

        if (verse < parsed.StartVerse || verse > parsed.EndVerse)
        {
            return;
        }

        string? text = TryGetByHeader(fields, header, "text")
            ?? TryGetByHeader(fields, header, "versetext")
            ?? TryGetByHeader(fields, header, "verse_text")
            ?? TryGetByHeader(fields, header, "scripture")
            ?? TryGetByHeader(fields, header, "words")
            ?? TryGuessVerseText(fields, parsed.Book);

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        hits.Add((verse.Value, text.Trim()));
    }

    private static bool LooksLikeHeader(List<string> fields)
    {
        string joined = string.Join("|", fields.Select(NormalizeHeader));
        return joined.Contains("book") ||
               joined.Contains("chapter") ||
               joined.Contains("verse") ||
               joined.Contains("scripture") ||
               joined.Contains("text");
    }

    private static string? TryGetByHeader(List<string> fields, List<string> header, string wanted)
    {
        if (header.Count == 0)
        {
            return null;
        }

        string normWanted = NormalizeHeader(wanted);

        for (int i = 0; i < header.Count && i < fields.Count; i++)
        {
            if (header[i] == normWanted)
            {
                return fields[i];
            }
        }

        return null;
    }

    private static int? TryGetIntByHeader(List<string> fields, List<string> header, string wanted)
    {
        string? value = TryGetByHeader(fields, header, wanted);
        return int.TryParse(value, out int n) ? n : null;
    }

    private static string? TryFindBookNameField(List<string> fields)
    {
        foreach (string f in fields)
        {
            string norm = NormalizeBook(f);
            if (BookNamesNormalized.Contains(norm))
            {
                return f;
            }
        }

        return null;
    }

    private static int? TryGuessBookNumber(List<string> fields)
    {
        // Common Bible CSV layouts put book number among the first few numeric
        // fields. We choose the first plausible 1..66 number.
        foreach (string f in fields.Take(6))
        {
            if (int.TryParse(f, out int n) && n >= 1 && n <= 66)
            {
                return n;
            }
        }

        return null;
    }

    private static int? TryGuessChapter(List<string> fields, BibleReference parsed)
    {
        // If the parsed chapter appears as a numeric field, accept it.
        return fields.Any(f => int.TryParse(f, out int n) && n == parsed.Chapter)
            ? parsed.Chapter
            : null;
    }

    private static int? TryGuessVerse(List<string> fields, BibleReference parsed)
    {
        var numbers = fields
            .Select(f => int.TryParse(f, out int n) ? n : -1)
            .Where(n => n >= parsed.StartVerse && n <= parsed.EndVerse)
            .ToList();

        return numbers.Count > 0 ? numbers[0] : null;
    }

    private static string? TryGuessVerseText(List<string> fields, string book)
    {
        string bookNorm = NormalizeBook(book);

        return fields
            .Where(f => !int.TryParse(f, out _))
            .Where(f => !NormalizeBook(f).Equals(bookNorm, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Length)
            .FirstOrDefault(f => f.Length > 10);
    }

    private static List<string> SplitCsv(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString().Trim());
        return result;
    }

    private static string NormalizeHeader(string input)
    {
        return new string(input
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static string NormalizeBook(string input)
    {
        return NormalizeHeader(input)
            .Replace("songofsolomon", "songofsongs");
    }

    private static int BookNumberFor(string book)
    {
        string norm = NormalizeBook(book);

        for (int i = 0; i < BookNames.Length; i++)
        {
            if (NormalizeBook(BookNames[i]).Equals(norm, StringComparison.OrdinalIgnoreCase))
            {
                return i + 1;
            }
        }

        return -1;
    }

    private static readonly string[] BookNames =
    {
        "Genesis","Exodus","Leviticus","Numbers","Deuteronomy","Joshua","Judges","Ruth",
        "1 Samuel","2 Samuel","1 Kings","2 Kings","1 Chronicles","2 Chronicles","Ezra","Nehemiah","Esther",
        "Job","Psalm","Proverbs","Ecclesiastes","Song of Songs","Isaiah","Jeremiah","Lamentations",
        "Ezekiel","Daniel","Hosea","Joel","Amos","Obadiah","Jonah","Micah","Nahum","Habakkuk","Zephaniah",
        "Haggai","Zechariah","Malachi","Matthew","Mark","Luke","John","Acts","Romans","1 Corinthians",
        "2 Corinthians","Galatians","Ephesians","Philippians","Colossians","1 Thessalonians","2 Thessalonians",
        "1 Timothy","2 Timothy","Titus","Philemon","Hebrews","James","1 Peter","2 Peter","1 John","2 John",
        "3 John","Jude","Revelation"
    };

    private static readonly HashSet<string> BookNamesNormalized = BookNames
        .Select(NormalizeBook)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private sealed record BibleReference(string Book, int Chapter, int StartVerse, int EndVerse)
    {
        public static BibleReference? TryParse(string reference)
        {
            reference = reference.Trim();

            int colon = reference.LastIndexOf(':');
            if (colon < 0)
            {
                return null;
            }

            string left = reference[..colon].Trim();
            string versePart = reference[(colon + 1)..].Trim();

            int lastSpace = left.LastIndexOf(' ');
            if (lastSpace < 0)
            {
                return null;
            }

            string book = left[..lastSpace].Trim();
            if (!int.TryParse(left[(lastSpace + 1)..].Trim(), out int chapter))
            {
                return null;
            }

            int startVerse;
            int endVerse;

            if (versePart.Contains('-'))
            {
                string[] pieces = versePart.Split('-', 2);
                if (!int.TryParse(pieces[0].Trim(), out startVerse) ||
                    !int.TryParse(pieces[1].Trim(), out endVerse))
                {
                    return null;
                }
            }
            else
            {
                if (!int.TryParse(versePart, out startVerse))
                {
                    return null;
                }

                endVerse = startVerse;
            }

            return new BibleReference(book, chapter, startVerse, endVerse);
        }
    }
}
