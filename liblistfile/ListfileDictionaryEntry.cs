//
//  ListfileDictionaryEntry.cs
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

using System.IO;
using liblistfile.Score;
using Warcraft.Core.Extensions;
using Warcraft.Core.Interfaces;

namespace liblistfile
{
	/// <summary>
	/// Listfile dictionary entry.
	///
	///	Each dictionary entry (when serialized) is structured as follows:
	///
	/// char[]					: Value (a null-terminated string of a term)
	/// float					: Score (the score of the term)
	/// </summary>
	public class ListfileDictionaryEntry : IBinarySerializable
	{
		/// <summary>
		/// Gets the current best term.
		/// </summary>
		/// <value>The term.</value>
		public string Term
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the score of the current term.
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
		/// <param name="inTerm">The input term.</param>
		/// <param name="inScore">In score.</param>
		public ListfileDictionaryEntry(string inTerm, float inScore)
		{
			this.Term = inTerm;
			this.Score = inScore;
		}

		/// <summary>
		/// Updates the term contained in this entry if the new term has a better score than the old one.
		/// </summary>
		/// <returns><c>true</c>, if the term was updated, <c>false</c> otherwise.</returns>
		/// <param name="term">Term.</param>
		public bool UpdateTerm(string term)
		{
			var newTermScore = TermScore.Calculate(term);
			if (this.Score < newTermScore)
			{
				this.Term = term;
				this.Score = newTermScore;

				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Forcibly sets the term.
		/// </summary>
		/// <param name="term"></param>
		public void SetTerm(string term)
		{
			this.Term = term;
		}

		/// <summary>
		/// Forcibly sets the score of the term.
		/// </summary>
		/// <param name="score"></param>
		public void SetScore(float score)
		{
			this.Score = score;
		}

		/// <summary>
		/// Recalculates the score of the term.
		/// </summary>
		public void RecalculateScore()
		{
			this.Score = TermScore.Calculate(this.Term);
		}

		/// <summary>
		/// Serializes the object into a byte array.
		/// </summary>
		/// <returns>The bytes.</returns>
		public byte[] Serialize()
		{
			using (var ms = new MemoryStream(this.Term.Length + 1 + 4))
			{
				using (var bw = new BinaryWriter(ms))
				{
					bw.WriteNullTerminatedString(this.Term);
					bw.Write(this.Score);
				}

				return ms.ToArray();
			}
		}
	}
}