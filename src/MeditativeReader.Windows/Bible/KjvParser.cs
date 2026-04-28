using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MeditativeReader.Bible
{
    public sealed class KjvPassage
    {
        public string DisplayText { get; init; } = "";
        public string SpeechText { get; init; } = "";
        public int VerseCount { get; init; }
    }

    public sealed class KjvQuery
    {
        public string Book { get; set; } = "";
        public int StartChapter { get; set; }
        public int EndChapter { get; set; }
        public int? StartVerse { get; set; }
        public int? EndVerse { get; set; }
    }

    public static class KjvParser
    {
        public static KjvQuery ParseQuery(string query)
        {
            query = query.Trim();

            var match = Regex.Match(
                query,
                @"^(?<book>.+?)\s+(?<startChapter>\d+)(?::(?<startVerse>\d+))?(?:-(?:(?<endChapter>\d+):)?(?<endVerseOrChapter>\d+))?$");

            if (!match.Success)
            {
                throw new ArgumentException("Could not parse KJV reference. Try: John 3, John 3:16, or John 3:16-18");
            }

            var result = new KjvQuery
            {
                Book = match.Groups["book"].Value.Trim(),
                StartChapter = int.Parse(match.Groups["startChapter"].Value)
            };

            result.EndChapter = result.StartChapter;

            if (match.Groups["startVerse"].Success)
            {
                result.StartVerse = int.Parse(match.Groups["startVerse"].Value);
                result.EndVerse = result.StartVerse;
            }

            if (match.Groups["endVerseOrChapter"].Success)
            {
                int endValue = int.Parse(match.Groups["endVerseOrChapter"].Value);

                if (match.Groups["endChapter"].Success)
                {
                    result.EndChapter = int.Parse(match.Groups["endChapter"].Value);
                    result.EndVerse = endValue;
                }
                else if (result.StartVerse.HasValue)
                {
                    result.EndVerse = endValue;
                }
                else
                {
                    result.EndChapter = endValue;
                }
            }

            return result;
        }
    }

    public sealed class KjvLoader
    {
        private readonly string _csvPath;

        public KjvLoader(string csvPath)
        {
            _csvPath = csvPath;
        }

        public KjvPassage LoadPassage(string reference)
        {
            if (!File.Exists(_csvPath))
            {
                return new KjvPassage
                {
                    DisplayText = "Meta-V CSV not found. Run scripts/windows/download-metav.ps1 with ExecutionPolicy Bypass.",
                    SpeechText = "",
                    VerseCount = 0
                };
            }

            var query = KjvParser.ParseQuery(reference);
            var verses = ReadVerses()
                .Where(v => BookMatches(v.BookId, query.Book))
                .Where(v => v.Chapter >= query.StartChapter && v.Chapter <= query.EndChapter)
                .Where(v => VerseInRange(v, query))
                .OrderBy(v => v.Chapter)
                .ThenBy(v => v.Verse)
                .ToList();

            if (verses.Count == 0)
            {
                return new KjvPassage
                {
                    DisplayText = $"No verses found for {reference}.",
                    SpeechText = "",
                    VerseCount = 0
                };
            }

            return new KjvPassage
            {
                DisplayText = string.Join(Environment.NewLine, verses.Select(v => $"{BookName(v.BookId)} {v.Chapter}:{v.Verse} {v.Text}")),
                SpeechText = string.Join(" ", verses.Select(v => v.Text)),
                VerseCount = verses.Count
            };
        }

        private static bool VerseInRange(VerseRecord v, KjvQuery q)
        {
            if (q.StartVerse.HasValue && v.Chapter == q.StartChapter && v.Verse < q.StartVerse.Value)
                return false;

            if (q.EndVerse.HasValue && v.Chapter == q.EndChapter && v.Verse > q.EndVerse.Value)
                return false;

            return true;
        }

        private IEnumerable<VerseRecord> ReadVerses()
        {
            using var reader = new StreamReader(_csvPath, Encoding.UTF8);

            string? headerLine = reader.ReadLine();
            if (headerLine is null)
                yield break;

            var header = SplitCsv(headerLine).Select(NormalizeHeader).ToList();

            while (!reader.EndOfStream)
            {
                string? line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var fields = SplitCsv(line);

                int? bookId = GetInt(fields, header, "bookid");
                int? chapter = GetInt(fields, header, "chapter");
                int? verse = GetInt(fields, header, "versenum");
                string? text = Get(fields, header, "versetext");

                if (bookId is null || chapter is null || verse is null || string.IsNullOrWhiteSpace(text))
                    continue;

                yield return new VerseRecord(bookId.Value, chapter.Value, verse.Value, text.Trim());
            }
        }

        private static string? Get(List<string> fields, List<string> header, string name)
        {
            int index = header.IndexOf(NormalizeHeader(name));
            return index >= 0 && index < fields.Count ? fields[index] : null;
        }

        private static int? GetInt(List<string> fields, List<string> header, string name)
        {
            return int.TryParse(Get(fields, header, name), out int value) ? value : null;
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
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result;
        }

        private static string NormalizeHeader(string input)
        {
            return new string(input.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        private static string NormalizeBook(string input)
        {
            return NormalizeHeader(input).Replace("songofsolomon", "songofsongs");
        }

        private static bool BookMatches(int bookId, string requestedBook)
        {
            return bookId >= 1 &&
                   bookId <= BookNames.Length &&
                   NormalizeBook(BookNames[bookId - 1]) == NormalizeBook(requestedBook);
        }

        private static string BookName(int bookId)
        {
            return bookId >= 1 && bookId <= BookNames.Length ? BookNames[bookId - 1] : $"Book {bookId}";
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

        private sealed record VerseRecord(int BookId, int Chapter, int Verse, string Text);
    }
}
