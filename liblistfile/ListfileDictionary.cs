//
//  ListfileDictionary.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2016 Jarl Gullberg
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using liblistfile.Score;
using System;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using Warcraft.Core.Extensions;
using Warcraft.Core.Interfaces;

namespace liblistfile
{
	/// <summary>
	/// Dictionary file for listtool. Contains a dictionary of terms used in
	/// listfiles, and their calculated term scores.
	///
	/// The file (when serialized) is structured as follows:
	///
	/// char[4]							: Signature (always DICT)
	/// uint32							: Version
	/// uint64_t						: RecordCount
	/// uint64_t 						: CompressedDictionarySize
	/// byte[CompressedDictionarySize]	: BZip2-compressed block of dictionary entries
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
		private readonly Dictionary<string, ListfileDictionaryEntry> DictionaryEntries =
			new Dictionary<string, ListfileDictionaryEntry>();


		/// <summary>
		/// Gets the entries which have a low score.
		/// </summary>
		/// <value>The entries with a low score.</value>
		public IEnumerable<KeyValuePair<string, ListfileDictionaryEntry>> LowScoreEntries
		{
			get
			{
				return this.DictionaryEntries.Where(pair => pair.Value.Score <= EntryLowScoreTolerance);
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
				return this.DictionaryEntries.Where(pair => pair.Value.Score >= EntryHighScoreTolerance);
			}
		}

		/// <summary>
		/// The dictionary words, extracted from the high-scoring entries.
		/// </summary>
		public readonly List<string> DictionaryWords = new List<string>();

		/// <summary>
		/// Initializes a new instance of the <see cref="liblistfile.ListfileDictionary"/> class.
		/// This constructor creates a new, empty dictionary.
		/// </summary>
		public ListfileDictionary()
		{
			this.EntryLowScoreTolerance = 0.0f;
			this.EntryHighScoreTolerance = 3.0f;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="liblistfile.ListfileDictionary"/> class.
		/// This constructor loads an existing dictionary from a byte array.
		/// </summary>
		/// <param name="data">Data.</param>
		public ListfileDictionary(byte[] data)
			: this()
		{
			using (MemoryStream ms = new MemoryStream(data))
			{
				LoadFromStream(ms);
			}
		}

		/// <summary>
		/// Asynchronously loads dictionary data from the given data stream.
		/// </summary>
		/// <param name="dataStream"></param>
		/// <param name="ct"></param>
		/// <exception cref="InvalidDataException"></exception>
		public async Task LoadFromStreamAsync(Stream dataStream, CancellationToken ct = new CancellationToken())
		{
			await Task.Run(() => LoadFromStream(dataStream, ct), ct);
		}

		/// <summary>
		/// Loads dictionary data from the given data stream.
		/// </summary>
		/// <param name="dataStream"></param>
		/// <param name="ct"></param>
		/// <exception cref="InvalidDataException"></exception>
		public void LoadFromStream(Stream dataStream, CancellationToken ct = new CancellationToken())
		{
			using (BinaryReader br = new BinaryReader(dataStream))
			{
				string dataSignature = new string(br.ReadChars(4));
				if (dataSignature != Signature)
				{
					throw new InvalidDataException("The input data did not begin with a dictionary signature.");
				}
				uint dataVersion = br.ReadUInt32();

				if (dataVersion < Version)
				{
					if (dataVersion < 2)
					{
						// Version 2 started compressing the dictionary block
						ulong recordCount = br.ReadUInt64();
						for (ulong i = 0; i < recordCount; ++i)
						{
							ct.ThrowIfCancellationRequested();

							ListfileDictionaryEntry entry = new ListfileDictionaryEntry(br.ReadNullTerminatedString(), br.ReadSingle());
							this.DictionaryEntries.Add(entry.Term.ToUpperInvariant(), entry);
						}
					}

					// Perform any extra actions required
					if (dataVersion == 0)
					{
						// From version 0 and up, the score calculation was altered. Recalculate all scores.
						foreach (KeyValuePair<string, ListfileDictionaryEntry> entry in this.DictionaryEntries)
						{
							ct.ThrowIfCancellationRequested();

							entry.Value.RecalculateScore();
						}
					}
				}
				else
				{
					// The most current implementation
					ulong recordCount = br.ReadUInt64();
					ulong recordBlockSize = br.ReadUInt64();

					using (MemoryStream compressedData = new MemoryStream(br.ReadBytes((int)recordBlockSize)))
					{
						using (BZip2Stream bz = new BZip2Stream(compressedData, CompressionMode.Decompress, true))
						{
							using (MemoryStream decompressedData = new MemoryStream())
							{
								// Decompress the data into the stream
								bz.CopyTo(decompressedData);

								// Read the dictionary elements
								decompressedData.Position = 0;
								using (BinaryReader zr = new BinaryReader(decompressedData))
								{
									for (ulong i = 0; i < recordCount; ++i)
									{
										ct.ThrowIfCancellationRequested();

										ListfileDictionaryEntry entry = new ListfileDictionaryEntry(zr.ReadNullTerminatedString(), zr.ReadSingle());
										this.DictionaryEntries.Add(entry.Term.ToUpperInvariant(), entry);
									}
								}
							}
						}
					}
				}

				// Extract all good words from high-scoring terms
				foreach (KeyValuePair<string, ListfileDictionaryEntry> highScoreEntryPair in this.HighScoreEntries)
				{
					ct.ThrowIfCancellationRequested();

					AddNewTermWords(highScoreEntryPair.Value.Term, false);
				}

				this.DictionaryWords.Sort(CompareWordsByLength);
			}
		}

		/// <summary>
		/// Adds new words from a new term.
		/// </summary>
		/// <param name="term">Term.</param>
		/// <param name="bSortDictionary">Whether or not the dictionary should be sorted after words have been added.</param>
		public void AddNewTermWords(string term, bool bSortDictionary = true)
		{
			foreach (string word in GetWordsFromTerm(Path.GetFileNameWithoutExtension(term)))
			{
				if (!this.DictionaryWords.Contains(word))
				{
					this.DictionaryWords.Add(word);
				}
			}

			if (bSortDictionary)
			{
				this.DictionaryWords.Sort(CompareWordsByLength);
			}
		}

		/// <summary>
		/// Guess the correct format of the specified term based on the words in the dictionary.
		/// </summary>
		/// <param name="term">Term.</param>
		public string Guess(string term)
		{
			if (string.IsNullOrEmpty(term))
			{
				return string.Empty;
			}

			string transientTerm = Path.GetFileNameWithoutExtension(term);
			string extension = Path.GetExtension(term);

			// Get everything in the term that isn't an abbreviation or a non-word character
			MatchCollection matches = Regex.Matches(transientTerm, "([a-zA-Z]{4,})");

			foreach (Match match in matches)
			{
				string transientMatch = match.Value;
				foreach (string word in this.DictionaryWords)
				{
					transientMatch = transientMatch.ReplaceCaseInsensitive(word.ToUpperInvariant(), word);
				}

				transientTerm = transientTerm.ReplaceCaseInsensitive(transientMatch.ToUpperInvariant(), transientMatch);
			}

			// Get all abbreviations between underscores
			MatchCollection abbreviationMatches = Regex.Matches(transientTerm, "(?<=_|^)[A-Z]{2,3}(?=_)");

			foreach (Match match in abbreviationMatches)
			{
				string transientMatch = match.Value;

				// We'll only look at words which have the same length as the abbreviation
				foreach (string word in this.DictionaryWords.Where(str => str.Length == match.Value.Length))
				{
					transientMatch = transientMatch.ReplaceCaseInsensitive(word.ToUpperInvariant(), word);
				}

				transientTerm = transientTerm.ReplaceCaseInsensitive(transientMatch.ToUpperInvariant(), transientMatch);
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
			string transientTerm = Guess(term);
			string scoredTransientTerm = TermScore.Guess(transientTerm);
			string correctedScoredTransientTerm = Guess(scoredTransientTerm);

			return correctedScoredTransientTerm;
		}

		/// <summary>
		/// Optimizes the provided list using the loaded dictionary.
		/// </summary>
		/// <returns>The optimized list.</returns>
		/// <param name="unoptimizedList">Unoptimized list.</param>
		public IEnumerable<string> OptimizeList(IEnumerable<string> unoptimizedList)
		{
			foreach (string path in unoptimizedList)
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
		/// <param name="path"></param>
		/// <returns></returns>
		public string OptimizePath(string path)
		{
			StringBuilder sb = new StringBuilder();

			string[] parts = path.Split('\\');
			for (int i = 0; i < parts.Length; ++i)
			{
				if (string.IsNullOrEmpty(parts[i]))
				{
					continue;
				}

				string extension = Path.GetExtension(parts[i]).ToLowerInvariant();
				string potentialTerm = Path.GetFileNameWithoutExtension(parts[i]);

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
			List<string> words = new List<string>();

			if (string.IsNullOrEmpty(term))
			{
				yield break;
			}

			MatchCollection matches = Regex.Matches(Path.GetFileNameWithoutExtension(term), "([A-Z][a-z]{1}[A-Z](?=\\W|$)|[A-Z][a-z]+)");

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
			return this.DictionaryEntries.ContainsKey(cleanTerm.ToUpperInvariant());
		}

		/// <summary>
		/// Deletes the specified term from the dictionary.
		/// </summary>
		/// <param name="term"></param>
		public void DeleteTerm(string term)
		{
			string termKey = term.ToUpperInvariant();
			if (this.DictionaryEntries.ContainsKey(termKey))
			{
				this.DictionaryEntries.Remove(termKey);
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

			if (this.DictionaryEntries.ContainsKey(Path.GetFileNameWithoutExtension(term).ToUpperInvariant()))
			{
				return this.DictionaryEntries[Path.GetFileNameWithoutExtension(term).ToUpperInvariant()];
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
		public bool AddTermEntry(string term)
		{
			if (term == null)
			{
				return false;
			}

			string cleanTerm = Path.GetFileNameWithoutExtension(term);
			if (!ContainsTerm(cleanTerm))
			{
				ListfileDictionaryEntry newEntry = new ListfileDictionaryEntry(cleanTerm, TermScore.Calculate(cleanTerm));

				this.DictionaryEntries.Add(cleanTerm.ToUpperInvariant(), newEntry);
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
		public bool UpdateTermEntry(string term)
		{
			if (term == null)
			{
				return false;
			}

			string cleanTerm = Path.GetFileNameWithoutExtension(term);
			if (!ContainsTerm(cleanTerm))
			{
				return AddTermEntry(cleanTerm);
			}
			else
			{
				return this.DictionaryEntries[cleanTerm.ToUpperInvariant()].UpdateTerm(cleanTerm);
			}
		}

		/// <summary>
		/// Forcibly sets the score of the provided term.
		/// </summary>
		/// <param name="term">term.</param>
		/// <param name="score">Score.</param>
		public bool SetTermScore(string term, float score)
		{
			if (term == null)
			{
				return false;
			}

			string cleanTerm = Path.GetFileNameWithoutExtension(term);
			if (!ContainsTerm(cleanTerm))
			{
				bool success = AddTermEntry(cleanTerm);
				if (success)
				{
					this.DictionaryEntries[cleanTerm.ToUpperInvariant()].SetScore(score);
				}
				return success;
			}

			if (score != this.DictionaryEntries[cleanTerm.ToUpperInvariant()].Score)
			{
				this.DictionaryEntries[cleanTerm.ToUpperInvariant()].SetScore(score);
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
		private static int CompareWordsByLength(string x, string y)
		{
			if (x == null)
			{
				if (y == null)
				{
					// If x is null and y is null, they're
					// equal.
					return 0;
				}
				else
				{
					// If x is null and y is not null, y
					// is greater.
					return -1;
				}
			}
			else
			{
				// If x is not null...
				//
				if (y == null)
                // ...and y is null, x is greater.
				{
					return 1;
				}
				else
				{
					// ...and y is not null, compare the
					// lengths of the two strings.
					//
					int retval = x.Length.CompareTo(y.Length);

					if (retval != 0)
					{
						// If the strings are not of equal length,
						// the longer string is greater.
						//
						return retval;
					}
					else
					{
						// If the strings are of equal length,
						// sort them with ordinary string comparison.
						//
						return string.Compare(x, y, StringComparison.Ordinal);
					}
				}
			}
		}

		/// <summary>
		/// Serializes the object into a byte array.
		/// </summary>
		/// <returns>The bytes.</returns>
		public byte[] Serialize()
		{
			using (MemoryStream ms = new MemoryStream())
			{
				using (BinaryWriter bw = new BinaryWriter(ms))
				{
					foreach (char c in Signature)
					{
						bw.Write(c);
					}
					bw.Write(Version);

					bw.Write((ulong)this.DictionaryEntries.Count);

					// Compress the dictionary entries
					byte[] compressedEntries;
					using (MemoryStream uncompressedDictionaryStream = new MemoryStream())
					{
						using (BinaryWriter uncompressedWriter = new BinaryWriter(uncompressedDictionaryStream))
						{
							foreach (KeyValuePair<string, ListfileDictionaryEntry> dictionaryEntryPair in this.DictionaryEntries)
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

