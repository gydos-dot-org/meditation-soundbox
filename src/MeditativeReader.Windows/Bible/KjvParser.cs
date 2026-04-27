using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MeditativeReader.Bible
{
    public class KjvQuery
    {
        public string Book { get; set; } = "";
        public int StartChapter { get; set; }
        public int EndChapter { get; set; }
        public int? StartVerse { get; set; }
        public int? EndVerse { get; set; }

        public bool IsWholeChapter => StartVerse == null && StartChapter == EndChapter;
        public bool IsChapterRange => StartChapter != EndChapter && StartVerse == null;
    }

    public static class KjvParser
    {
        // Handles: "John 3", "John 3:16", "John 3:16-18", "John 3-4", "John 3:16-4:18"
        public static KjvQuery ParseQuery(string query)
        {
            query = query.Trim();
            var result = new KjvQuery();

            // Pattern: Book name (handles numbers: 1 John, 2 Samuel, etc.)
            var match = Regex.Match(query, @"^(\d?\s?[A-Za-z]+)\s+(\d+)(?::(\d+))?(?:-(\d+)(?::(\d+))?)?$");

            if (!match.Success)
                throw new ArgumentException($"Invalid KJV query format: {query}");

            result.Book = match.Groups[1].Value.Trim();
            result.StartChapter = int.Parse(match.Groups[2].Value);
            result.EndChapter = result.StartChapter;

            if (match.Groups[3].Success) // Has start verse
            {
                result.StartVerse = int.Parse(match.Groups[3].Value);
                result.EndVerse = result.StartVerse;
            }

            if (match.Groups[4].Success) // Has range end
            {
                if (match.Groups[5].Success) // End has chapter:verse format (John 3:16-4:18)
                {
                    result.EndChapter = int.Parse(match.Groups[4].Value);
                    result.EndVerse = int.Parse(match.Groups[5].Value);
                }
                else // Same chapter verse range (John 3:16-18) or chapter range (John 3-4)
                {
                    var endNum = int.Parse(match.Groups[4].Value);
                    if (result.StartVerse.HasValue && endNum > result.StartVerse.Value)
                    {
                        // Verse range within same chapter
                        result.EndVerse = endNum;
                    }
                    else if (!result.StartVerse.HasValue)
                    {
                        // Chapter range
                        result.EndChapter = endNum;
                    }
                }
            }

            return result;
        }
    }

    public class KjvLoader
    {
        private readonly List<VerseRecord> _verses; // Your CSV data

        public KjvLoader(IEnumerable<VerseRecord> verses)
        {
            _verses = verses.ToList();
        }

        /// <summary>
        /// Returns verses with metadata for DISPLAY (includes book/chapter/verse labels).
        /// </summary>
        public IEnumerable<VerseRecord> GetPassageForDisplay(KjvQuery query)
        {
            return _verses.Where(v =>
                v.Book.Equals(query.Book, StringComparison.OrdinalIgnoreCase) &&
                v.Chapter >= query.StartChapter &&
                v.Chapter <= query.EndChapter &&
                (!query.StartVerse.HasValue ||
                 (v.Chapter == query.StartChapter && v.Verse >= query.StartVerse.Value)) &&
                (!query.EndVerse.HasValue ||
                 (v.Chapter == query.EndChapter && v.Verse <= query.EndVerse.Value))
            );
        }

        /// <summary>
        /// Returns ONLY the words for the TTS buffer — no verse numbers, no labels.
        /// This is what gets sent to Piper.
        /// </summary>
        public string GetCleanTextForSpeech(KjvQuery query)
        {
            var passage = GetPassageForDisplay(query);
            // Concatenate verse text only, with single spaces
            return string.Join(" ", passage.Select(v => v.Text));
        }
    }

    public class VerseRecord
    {
        public string Book { get; set; } = "";
        public int Chapter { get; set; }
        public int Verse { get; set; }
        public string Text { get; set; } = "";
    }
}
