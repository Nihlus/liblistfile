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
using System;
using System.IO;
using Warcraft.Core;
using System.Collections.Generic;
using liblistfile.Score;

namespace liblistfile
{
	/// <summary>
	/// Dictionary file for listtool. Contains a dictionary of words used in 
	/// listfiles, and their calculated word scores.
	/// 
	/// The file (when serialized) is structured as follows:
	/// 
	/// char[4]					: Signature (always DICT)
	/// uint64_t				: RecordCount
	/// DictRec[RecordCount]	: Dictionary entries
	/// </summary>
	public class ListfileDictionary
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
		/// The dictionary entries.
		///
		/// Contains values in the following format:
		/// Key: An all-uppercase word.
		/// Value: A dictionary entry containing the best found format for the word, along with a score.
		/// </summary>
		private readonly Dictionary<string, ListfileDictionaryEntry> DictionaryEntries = 
			new Dictionary<string, ListfileDictionaryEntry>();

		/// <summary>
		/// Initializes a new instance of the <see cref="liblistfile.ListfileDictionary"/> class.
		/// This constructor creates a new, empty dictionary.
		/// </summary>
		public ListfileDictionary()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="liblistfile.ListfileDictionary"/> class.
		/// This constructor loads an existing dictionary from a byte array.
		/// </summary>
		/// <param name="data">Data.</param>
		public ListfileDictionary(byte[] data)
		{
			using (MemoryStream ms = new MemoryStream(data))
			{
				using (BinaryReader br = new BinaryReader(ms))
				{
					string dataSignature = new string(br.ReadChars(4));
					if (dataSignature != ListfileDictionary.Signature)
					{
						throw new InvalidDataException("The input data did not begin with a dictionary signature.");
					}

					ulong RecordCount = br.ReadUInt64();
					for (ulong i = 0; i < RecordCount; ++i)
					{
						ListfileDictionaryEntry entry = new ListfileDictionaryEntry(br.ReadNullTerminatedString(), br.ReadSingle());
						this.DictionaryEntries.Add(entry.Word.ToUpperInvariant(), entry);
					}
				}
			}
		}

		/// <summary>
		/// Checks if the dictionary contains the specified word.
		/// </summary>
		/// <returns><c>true</c>, if the dictionary contains the word, <c>false</c> otherwise.</returns>
		/// <param name="word">Word.</param>
		public bool ContainsWord(string word)
		{
			return this.DictionaryEntries.ContainsKey(word.ToUpperInvariant());
		}

		/// <summary>
		/// Gets the entry for the specified word, containing the current best value and the score of that value.
		/// </summary>
		/// <returns>The word entry.</returns>
		/// <param name="word">Word.</param>
		public ListfileDictionaryEntry GetWordEntry(string word)
		{
			if (this.DictionaryEntries.ContainsKey(word.ToUpperInvariant()))
			{
				return DictionaryEntries[word.ToUpperInvariant()];
			}
			else
			{
				return null;
			}
		}

		/// <summary>
		/// Adds an entry for the provided word if it's not already in the dictionary.
		/// </summary>
		/// <param name="word">Word.</param>
		public void AddWordEntry(string word)
		{
			if (!ContainsWord(word))
			{
				ListfileDictionaryEntry newEntry = new ListfileDictionaryEntry(word, WordScore.Calculate(word));

				this.DictionaryEntries.Add(word.ToUpperInvariant(), newEntry);
			}
		}

		/// <summary>
		/// Updates the dictionary entry for the provided word. If the list doesn't contain the word, it is added.
		/// If not, the score of the word is compared with the existing one. If it has a larger score, it replaced
		/// the one in the dictionary.
		/// </summary>
		/// <param name="word">Word.</param>
		public void UpdateWordEntry(string word)
		{
			if (!ContainsWord(word))
			{
				AddWordEntry(word);
			}
			else
			{
				this.DictionaryEntries[word.ToUpperInvariant()].UpdateWord(word);
			}
		}

		/// <summary>
		/// Serializes the object into a byte array.
		/// </summary>
		/// <returns>The bytes.</returns>
		public byte[] GetBytes()
		{
			using (MemoryStream ms = new MemoryStream())
			{
				using (BinaryWriter bw = new BinaryWriter(ms))
				{
					foreach (char c in ListfileDictionary.Signature)
					{
						bw.Write(c);
					}

					bw.Write((ulong)this.DictionaryEntries.Count);

					foreach (KeyValuePair<string, ListfileDictionaryEntry> DictionaryEntry in DictionaryEntries)
					{
						bw.Write(DictionaryEntry.Value.GetBytes());
					}
				}

				return ms.ToArray();
			}
		}
	}

	/// <summary>
	/// Listfile dictionary entry.
	///
	///	Each dictionary entry (when serialized) is structured as follows:
	///
	/// char[]					: Value (a null-terminated string of a word)
	/// float					: Score (the score of the word)
	/// </summary>
	public class ListfileDictionaryEntry
	{
		/// <summary>
		/// Gets the current best word.
		/// </summary>
		/// <value>The word.</value>
		public string Word
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the score of the current word.
		/// </summary>
		/// <value>The score.</value>
		public float Score
		{
			get; 
			private set;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="liblistfile.ListfileDictionaryEntry"/> class.
		/// </summary>
		/// <param name="InWord">In value.</param>
		/// <param name="InScore">In score.</param>
		public ListfileDictionaryEntry(string InWord, float InScore)
		{
			this.Word = InWord;
			this.Score = InScore;
		}

		/// <summary>
		/// Updates the word contained in this entry if the new word has a better score than the old one.
		/// </summary>
		/// <returns><c>true</c>, if the word was updated, <c>false</c> otherwise.</returns>
		/// <param name="word">Word.</param>
		public bool UpdateWord(string word)
		{
			float newWordScore = WordScore.Calculate(word);
			if (this.Score < newWordScore)
			{
				this.Word = word;
				this.Score = newWordScore;

				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Serializes the object into a byte array.
		/// </summary>
		/// <returns>The bytes.</returns>
		public byte[] GetBytes()
		{
			using (MemoryStream ms = new MemoryStream(Word.Length + 1 + 4))
			{
				using (BinaryWriter bw = new BinaryWriter(ms))
				{
					bw.WriteNullTerminatedString(this.Word);
					bw.Write(this.Score);
				}

				return ms.ToArray();
			}
		}
	}
}

