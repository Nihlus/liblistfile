//
//  ListfileDictionary.cs
//
//  Copyright (c) 2018 Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ListFile.Score;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using Warcraft.Core.Extensions;
using Warcraft.Core.Interfaces;

namespace ListFile
{
    /// <summary>
    /// Dictionary file for listtool. Contains a dictionary of terms used in
    /// listfiles, and their calculated term scores.
    ///
    /// The file (when serialized) is structured as follows:
    ///
    /// char[4]                            : Signature (always DICT)
    /// uint32                            : Version
    /// uint64_t                        : RecordCount
    /// uint64_t                         : CompressedDictionarySize
    /// byte[CompressedDictionarySize]    : BZip2-compressed block of dictionary entries
    ///
    /// Only one version of the file format is supported at any given time.
    /// </summary>
    public class ListfileDictionary : IBinarySerializable
    {
        /// <summary>
        /// The binary file signature. Used when serializing this object into an RIFF-style
        /// format.
        /// </summary>
        public const string Signature = "DICT";

        /// <summary>
        /// The file extension used for serialized list containers.
        /// </summary>
        public const string Extension = "dic";

        /// <summary>
        /// The file format version.
        /// </summary>
        public const uint Version = 2;

        /// <summary>
        /// Gets or sets the low entry score tolerance. This value is used when determining what values are
        /// considered high-scoring.
        /// </summary>
        /// <value>The entry score tolerance.</value>
        public float EntryLowScoreTolerance
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the high entry score tolerance. This value is used when determining what values are
        /// considered high-scoring.
        /// </summary>
        /// <value>The entry score tolerance.</value>
        public float EntryHighScoreTolerance
        {
            get;
            set;
        }

        /// <summary>
        /// The dictionary entries.
        ///
        /// Contains values in the following format:
        /// Key: An all-uppercase term.
        /// Value: A dictionary entry containing the best found format for the term, along with a score.
        /// </summary>
        private readonly Dictionary<string, ListfileDictionaryEntry> _dictionaryEntries =
            new Dictionary<string, ListfileDictionaryEntry>();

        /// <summary>
        /// Gets the entries which have a low score.
        /// </summary>
        /// <value>The entries with a low score.</value>
        public IEnumerable<KeyValuePair<string, ListfileDictionaryEntry>> LowScoreEntries
        {
            get
            {
                return _dictionaryEntries.Where(pair => pair.Value.Score <= EntryLowScoreTolerance);
            }
        }

        /// <summary>
        /// Gets the entries which have a high score.
        /// </summary>
        /// <value>The entries with a low score.</value>
        public IEnumerable<KeyValuePair<string, ListfileDictionaryEntry>> HighScoreEntries
        {
            get
            {
                return _dictionaryEntries.Where(pair => pair.Value.Score >= EntryHighScoreTolerance);
            }
        }

        /// <summary>
        /// Gets the dictionary words, extracted from the high-scoring entries.
        /// </summary>
        public List<string> DictionaryWords { get; } = new List<string>();

        private static readonly Regex WordsRegex = new Regex("([a-zA-Z]{4,})", RegexOptions.Compiled);
        private static readonly Regex AbbreviationRegex = new Regex("(?<=_|^)[A-Z]{2,3}(?=_)", RegexOptions.Compiled);
        private static readonly Regex TermToWordsRegex = new Regex("([A-Z][a-z]{1}[A-Z](?=\\W|$)|[A-Z][a-z]+)", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="ListfileDictionary"/> class.
        /// This constructor creates a new, empty dictionary.
        /// </summary>
        public ListfileDictionary()
        {
            EntryLowScoreTolerance = 0.0f;
            EntryHighScoreTolerance = 3.0f;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ListfileDictionary"/> class.
        /// This constructor loads an existing dictionary from a byte array.
        /// </summary>
        /// <param name="data">Data.</param>
        public ListfileDictionary(byte[] data)
            : this()
        {
            using (var ms = new MemoryStream(data))
            {
                LoadFromStream(ms);
            }
        }

        /// <summary>
        /// Asynchronously loads dictionary data from the given data stream.
        /// </summary>
        /// <param name="dataStream">The stream to load the dictionary from.</param>
        /// <param name="ct">The cancellation token to use.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task LoadFromStreamAsync(Stream dataStream, CancellationToken ct = default)
        {
            await Task.Run(() => LoadFromStream(dataStream, ct), ct);
        }

        /// <summary>
        /// Loads dictionary data from the given data stream.
        /// </summary>
        /// <param name="dataStream">The stream to load the dictionary from.</param>
        /// <param name="ct">The cancellation token to use.</param>
        public void LoadFromStream(Stream dataStream, CancellationToken ct = default)
        {
            using (var br = new BinaryReader(dataStream))
            {
                var dataSignature = new string(br.ReadChars(4));
                if (dataSignature != Signature)
                {
                    throw new InvalidDataException("The input data did not begin with a dictionary signature.");
                }

                var dataVersion = br.ReadUInt32();

                if (dataVersion < Version)
                {
                    if (dataVersion < 2)
                    {
                        // Version 2 started compressing the dictionary block
                        var recordCount = br.ReadUInt64();
                        for (ulong i = 0; i < recordCount; ++i)
                        {
                            ct.ThrowIfCancellationRequested();

                            var entry = new ListfileDictionaryEntry(br.ReadNullTerminatedString(), br.ReadSingle());
                            _dictionaryEntries.Add(entry.Term.ToUpperInvariant(), entry);
                        }
                    }

                    // Perform any extra actions required
                    if (dataVersion == 0)
                    {
                        // From version 0 and up, the score calculation was altered. Recalculate all scores.
                        foreach (var entry in _dictionaryEntries)
                        {
                            ct.ThrowIfCancellationRequested();

                            entry.Value.RecalculateScore();
                        }
                    }
                }
                else
                {
                    // The most current implementation
                    var recordCount = br.ReadUInt64();
                    var recordBlockSize = br.ReadUInt64();

                    using (var compressedData = new MemoryStream(br.ReadBytes((int)recordBlockSize)))
                    {
                        using (var bz = new BZip2Stream(compressedData, CompressionMode.Decompress, true))
                        {
                            using (var decompressedData = new MemoryStream())
                            {
                                // Decompress the data into the stream
                                bz.CopyTo(decompressedData);

                                // Read the dictionary elements
                                decompressedData.Position = 0;
                                using (var zr = new BinaryReader(decompressedData))
                                {
                                    for (ulong i = 0; i < recordCount; ++i)
                                    {
                                        ct.ThrowIfCancellationRequested();

                                        var entry = new ListfileDictionaryEntry(zr.ReadNullTerminatedString(), zr.ReadSingle());
                                        _dictionaryEntries.Add(entry.Term.ToUpperInvariant(), entry);
                                    }
                                }
                            }
                        }
                    }
                }

                // Extract all good words from high-scoring terms
                foreach (var highScoreEntryPair in HighScoreEntries)
                {
                    ct.ThrowIfCancellationRequested();

                    AddNewTermWords(highScoreEntryPair.Value.Term, false);
                }

                DictionaryWords.Sort((s, s1) => CompareWordsByLength(s.AsSpan(), s1.AsSpan()));
            }
        }

        /// <summary>
        /// Adds new words from a new term.
        /// </summary>
        /// <param name="term">Term.</param>
        /// <param name="bSortDictionary">Whether or not the dictionary should be sorted after words have been added.</param>
        public void AddNewTermWords(string term, bool bSortDictionary = true)
        {
            foreach (var word in GetWordsFromTerm(Path.GetFileNameWithoutExtension(term)))
            {
                if (!DictionaryWords.Contains(word))
                {
                    DictionaryWords.Add(word);
                }
            }

            if (bSortDictionary)
            {
                DictionaryWords.Sort((s, s1) => CompareWordsByLength(s.AsSpan(), s1.AsSpan()));
            }
        }

        /// <summary>
        /// Guess the correct format of the specified term based on the words in the dictionary.
        /// </summary>
        /// <param name="term">Term.</param>
        /// <returns>The guessed term.</returns>
        public string Guess(string term)
        {
            if (string.IsNullOrEmpty(term))
            {
                return string.Empty;
            }

            var transientTerm = Path.GetFileNameWithoutExtension(term);
            var extension = Path.GetExtension(term);

            // Get everything in the term that isn't an abbreviation or a non-word character
            var matches = WordsRegex.Matches(transientTerm);

            foreach (Match match in matches)
            {
                var transientMatch = match.Value;
                foreach (var word in DictionaryWords)
                {
                    transientMatch = transientMatch.FastReplaceCaseInsensitive(word.ToUpperInvariant(), word);
                }

                transientTerm = transientTerm.FastReplaceCaseInsensitive(transientMatch.ToUpperInvariant(), transientMatch);
            }

            // Get all abbreviations between underscores
            var abbreviationMatches = AbbreviationRegex.Matches(transientTerm);

            foreach (Match match in abbreviationMatches)
            {
                var transientMatch = match.Value;

                // We'll only look at words which have the same length as the abbreviation
                foreach (var word in DictionaryWords.Where(str => str.Length == match.Value.Length))
                {
                    transientMatch = transientMatch.FastReplaceCaseInsensitive(word.ToUpperInvariant(), word);
                }

                transientTerm = transientTerm.FastReplaceCaseInsensitive(transientMatch.ToUpperInvariant(), transientMatch);
            }

            return transientTerm + extension.ToLowerInvariant();
        }

        /// <summary>
        /// Guesses the correct casing of the term using assisted scoring. First, the casing is guessed using
        /// the dictionary, then TermScore is used to corrent mangled casing in unknown words. Then, it's reguessed to
        /// correct any issues generated by the TermScore guess.
        /// </summary>
        /// <returns>The scored.</returns>
        /// <param name="term">Term.</param>
        public string GuessScored(string term)
        {
            var transientTerm = Guess(term);
            var scoredTransientTerm = TermScore.Guess(transientTerm.AsSpan());
            var correctedScoredTransientTerm = Guess(scoredTransientTerm.ToString());

            return correctedScoredTransientTerm;
        }

        /// <summary>
        /// Optimizes the provided list using the loaded dictionary.
        /// </summary>
        /// <returns>The optimized list.</returns>
        /// <param name="unoptimizedList">Unoptimized list.</param>
        public IEnumerable<string> OptimizeList(IEnumerable<string> unoptimizedList)
        {
            foreach (var path in unoptimizedList)
            {
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                yield return OptimizePath(path);
            }
        }

        /// <summary>
        /// Optimizes the provided path using the loaded dictionary.
        /// </summary>
        /// <param name="path">The path to optimize.</param>
        /// <returns>The optimized path.</returns>
        public string OptimizePath(string path)
        {
            var sb = new StringBuilder();

            var parts = path.Split('\\');
            for (var i = 0; i < parts.Length; ++i)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }

                var extension = Path.GetExtension(parts[i]).ToLowerInvariant();
                var potentialTerm = Path.GetFileNameWithoutExtension(parts[i]);

                if (!ContainsTerm(potentialTerm))
                {
                    UpdateTermEntry(potentialTerm);
                }

                sb.Append(GetTermEntry(potentialTerm).Term);

                if (!string.IsNullOrEmpty(extension))
                {
                    sb.Append(extension);
                }

                if (i < parts.Length - 1)
                {
                    sb.Append("\\");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets the words from term.
        /// </summary>
        /// <returns>The words from term.</returns>
        /// <param name="term">Term.</param>
        public static IEnumerable<string> GetWordsFromTerm(string term)
        {
            var words = new List<string>();

            if (string.IsNullOrEmpty(term))
            {
                yield break;
            }

            var matches = TermToWordsRegex.Matches(Path.GetFileNameWithoutExtension(term));

            foreach (Match match in matches)
            {
                if (words.Contains(match.Value))
                {
                    continue;
                }

                words.Add(match.Value);
                yield return match.Value;
            }
        }

        /// <summary>
        /// Checks if the dictionary contains the specified term.
        /// </summary>
        /// <returns><c>true</c>, if the dictionary contains the term, <c>false</c> otherwise.</returns>
        /// <param name="cleanTerm">term.</param>
        public bool ContainsTerm(string cleanTerm)
        {
            return _dictionaryEntries.ContainsKey(cleanTerm.ToUpperInvariant());
        }

        /// <summary>
        /// Deletes the specified term from the dictionary.
        /// </summary>
        /// <param name="term">The term to delete.</param>
        public void DeleteTerm(string term)
        {
            var termKey = term.ToUpperInvariant();
            if (_dictionaryEntries.ContainsKey(termKey))
            {
                _dictionaryEntries.Remove(termKey);
            }
        }

        /// <summary>
        /// Gets the entry for the specified term, containing the current best value and the score of that value.
        /// </summary>
        /// <returns>The term entry.</returns>
        /// <param name="term">term.</param>
        public ListfileDictionaryEntry GetTermEntry(string term)
        {
            if (term == null)
            {
                return null;
            }

            if (_dictionaryEntries.ContainsKey(Path.GetFileNameWithoutExtension(term).ToUpperInvariant()))
            {
                return _dictionaryEntries[Path.GetFileNameWithoutExtension(term).ToUpperInvariant()];
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Adds an entry for the provided term if it's not already in the dictionary.
        /// </summary>
        /// <param name="term">term.</param>
        /// <returns>true if the term was added; otherwise, false.</returns>
        public bool AddTermEntry(string term)
        {
            if (term == null)
            {
                return false;
            }

            var cleanTerm = Path.GetFileNameWithoutExtension(term);
            if (!ContainsTerm(cleanTerm))
            {
                var newEntry = new ListfileDictionaryEntry(cleanTerm, TermScore.Calculate(cleanTerm.AsSpan()));

                _dictionaryEntries.Add(cleanTerm.ToUpperInvariant(), newEntry);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Updates the dictionary entry for the provided term. If the list doesn't contain the term, it is added.
        /// If not, the score of the term is compared with the existing one. If it has a larger score, it replaces
        /// the one in the dictionary.
        /// </summary>
        /// <param name="term">term.</param>
        /// <returns>true if the term was updated; otherwise, false.</returns>
        public bool UpdateTermEntry(string term)
        {
            if (term == null)
            {
                return false;
            }

            var cleanTerm = Path.GetFileNameWithoutExtension(term);
            if (!ContainsTerm(cleanTerm))
            {
                return AddTermEntry(cleanTerm);
            }
            else
            {
                return _dictionaryEntries[cleanTerm.ToUpperInvariant()].UpdateTerm(cleanTerm);
            }
        }

        /// <summary>
        /// Forcibly sets the score of the provided term.
        /// </summary>
        /// <param name="term">term.</param>
        /// <param name="score">Score.</param>
        /// <returns>true if the score was set; otherwise, false.</returns>
        public bool SetTermScore(string term, float score)
        {
            if (term == null)
            {
                return false;
            }

            var cleanTerm = Path.GetFileNameWithoutExtension(term);
            if (!ContainsTerm(cleanTerm))
            {
                var success = AddTermEntry(cleanTerm);
                if (success)
                {
                    _dictionaryEntries[cleanTerm.ToUpperInvariant()].SetScore(score);
                }

                return success;
            }

            if (score != _dictionaryEntries[cleanTerm.ToUpperInvariant()].Score)
            {
                _dictionaryEntries[cleanTerm.ToUpperInvariant()].SetScore(score);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Compares the words by their length.
        /// Code example taken from https://msdn.microsoft.com/en-us/library/w56d4y5z(v=vs.110).aspx.
        /// </summary>
        /// <returns>The words by length.</returns>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        private static int CompareWordsByLength(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
        {
            if (x == null)
            {
                if (y == null)
                {
                    // If x is null and y is null, they're
                    // equal.
                    return 0;
                }

                // If x is null and y is not null, y
                // is greater.
                return -1;
            }

            // If x is not null and y is null, x is greater.
            if (y == null)
            {
                return 1;
            }

            // If x is not null and y is not null, compare the lengths of the two strings.
            var retval = x.Length.CompareTo(y.Length);

            if (retval != 0)
            {
                // If the strings are not of equal length, the longer string is greater.
                return retval;
            }

            // If the strings are of equal length, sort them with ordinary string comparison.
            return x.CompareTo(y, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    foreach (var c in Signature)
                    {
                        bw.Write(c);
                    }

                    bw.Write(Version);

                    bw.Write((ulong)_dictionaryEntries.Count);

                    // Compress the dictionary entries
                    byte[] compressedEntries;
                    using (var uncompressedDictionaryStream = new MemoryStream())
                    {
                        using (var uncompressedWriter = new BinaryWriter(uncompressedDictionaryStream))
                        {
                            foreach (var dictionaryEntryPair in _dictionaryEntries)
                            {
                                uncompressedWriter.Write(dictionaryEntryPair.Value.Serialize());
                            }
                        }

                        compressedEntries = uncompressedDictionaryStream.ToArray().Compress();
                    }

                    // Write the dictionary block with leading size
                    bw.Write((ulong)compressedEntries.LongLength);
                    bw.Write(compressedEntries);
                }

                return ms.ToArray();
            }
        }
    }
}
