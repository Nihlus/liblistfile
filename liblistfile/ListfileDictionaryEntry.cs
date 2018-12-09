//
//  ListfileDictionaryEntry.cs
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
using System.IO;
using JetBrains.Annotations;
using ListFile.Score;
using Warcraft.Core.Extensions;
using Warcraft.Core.Interfaces;

namespace ListFile
{
    /// <summary>
    /// Listfile dictionary entry.
    ///
    ///    Each dictionary entry (when serialized) is structured as follows:
    ///
    /// char[]                    : Value (a null-terminated string of a term)
    /// float                    : Score (the score of the term)
    ///
    /// One entry represents a term an an associated score.
    /// </summary>
    [PublicAPI]
    public class ListfileDictionaryEntry : IBinarySerializable
    {
        /// <summary>
        /// Gets the current best term.
        /// </summary>
        /// <value>The term.</value>
        [PublicAPI, NotNull]
        public string Term
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the score of the current term.
        /// </summary>
        /// <value>The score.</value>
        [PublicAPI]
        public float Score
        {
            get;
            private set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ListfileDictionaryEntry"/> class.
        /// </summary>
        /// <param name="inTerm">The input term.</param>
        /// <param name="inScore">In score.</param>
        [PublicAPI]
        public ListfileDictionaryEntry([NotNull] string inTerm, float inScore)
        {
            Term = inTerm;
            Score = inScore;
        }

        /// <summary>
        /// Updates the term contained in this entry if the new term has a better score than the old one.
        /// </summary>
        /// <returns><c>true</c>, if the term was updated, <c>false</c> otherwise.</returns>
        /// <param name="term">Term.</param>
        [PublicAPI]
        public bool UpdateTerm([NotNull] string term)
        {
            var newTermScore = TermScore.Calculate(term.AsSpan());
            if (Score < newTermScore)
            {
                Term = term;
                Score = newTermScore;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Forcibly sets the term value.
        /// </summary>
        /// <param name="term">The term.</param>
        [PublicAPI]
        public void SetTerm([NotNull] string term)
        {
            Term = term;
        }

        /// <summary>
        /// Forcibly sets the score of the term.
        /// </summary>
        /// <param name="score">The score.</param>
        [PublicAPI]
        public void SetScore(float score)
        {
            Score = score;
        }

        /// <summary>
        /// Recalculates the score of the term.
        /// </summary>
        [PublicAPI]
        public void RecalculateScore()
        {
            Score = TermScore.Calculate(Term.AsSpan());
        }

        /// <summary>
        /// Serializes the object into a byte array.
        /// </summary>
        /// <returns>The bytes.</returns>
        [PublicAPI, NotNull, Pure]
        public byte[] Serialize()
        {
            using (var ms = new MemoryStream(Term.Length + 1 + 4))
            {
                using (var bw = new BinaryWriter(ms))
                {
                    bw.WriteNullTerminatedString(Term);
                    bw.Write(Score);
                }

                return ms.ToArray();
            }
        }
    }
}
