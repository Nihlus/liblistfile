//
//  WordScore.cs
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
using System.Text;

namespace liblistfile.Score
{
	/// <summary>
	/// PascalCase term score calculator.
	/// This class calculates the PascalCase score of the provided term. A higher score indicates better compliance
	/// with the PascalCase definition.
	///
	/// In general, the more mixed a word is, the better the score. All-caps words get the lowest score (a 0),
	/// and those with mixed case that begin with a single upper-case letter get the best scores.
	///
	/// Overall, the score is merely an indication and doesn't reliably detect propre PascalCase,
	/// since it doesn't know anything about actual words. However, it's good enough for most purposes.
	/// </summary>
	public static class TermScore
	{
		/// <summary>
		/// Calculates the PascalCase score of the provided word.
		/// If the calculation is set to be strict, all-lower case words are given the same score as
		/// all-upper case words. If not, all lower-case words are considered slightly better than all
		/// uppers.
		/// </summary>
		/// <param name="word">Word.</param>
		/// <param name="strict">Whether or not the score should be strict in its calculation.</param>
		public static float Calculate(string word, bool strict = false)
		{
			var score = 0.0f;

			if (word.IsAllUpper())
			{
				score = 0.0f;
			}

			if (word.IsAllLower())
			{
				score = strict ? 0.0f : 0.5f;
			}

			if (word.IsMixedCase())
			{
				if (word.IsFilename())
				{
					// Ignore the casing of the extension
					return Calculate(word.GetFilename());
				}

				score += 1.0f;

				if (word.HasMoreThanOneVersal())
				{
					score += 1.0f;
				}

				if (word.StartsWithSingleUpper())
				{
					score += 1.0f;
				}
			}

			return score;
		}

		/// <summary>
		/// Guesses the casing for the specified word.
		/// </summary>
		/// <param name="word">Word.</param>
		public static string Guess(string word)
		{
			var transientWord = word;

			transientWord = transientWord.ToLowerInvariant();

			// Set the first character to be uppercase
			var wordBuilder = new StringBuilder(transientWord)
			{
				[0] = char.ToUpper(transientWord[0])
			};

			var previousChar = (char)0;
			for (var i = 0; i < transientWord.Length; ++i)
			{
				var currentChar = transientWord[i];

				// Any char following a _ is upper
				if (previousChar == '_')
				{
					if (char.IsLetter(currentChar))
					{
						wordBuilder[i] = char.ToUpper(currentChar);
					}

					if (i < transientWord.Length - 2)
					{
						if (transientWord[i + 2] == '_')
						{
							if (char.IsLetter(transientWord[i + 1]))
							{
								wordBuilder[i + 1] = char.ToUpper(transientWord[i + 1]);
							}
						}
					}
				}

				// Any char following a digit is upper
				if (char.IsDigit(currentChar) && (i + 1) < transientWord.Length)
				{
					wordBuilder[i + 1] = char.ToUpper(transientWord[i + 1]);
				}

				// Any set of three chars or less between a string boundary or a _ is upper
				if (currentChar == '_')
				{
					var isThreeOrLessBetween = false;
					for (var j = 4; j > 0; --j)
					{
						if ((i - j) < 0 || transientWord[i - j] == '_')
						{
							isThreeOrLessBetween = true;
							break;
						}
					}

					if (isThreeOrLessBetween)
					{
						var j = 1;
						while (i - j >= 0 && j <= 3)
						{
							if (transientWord[i - j] == '_')
							{
								break;
							}

							wordBuilder[i - j] = char.ToUpper(transientWord[i - j]);
							++j;
						}
					}
				}

				// Any char following a - is upper
				if (previousChar == '-')
				{
					if (char.IsLetter(currentChar))
					{
						wordBuilder[i] = char.ToUpper(currentChar);
					}
				}


				if (i + 1 < transientWord.Length && char.IsDigit(previousChar))
				{
					var nextChar = transientWord[i + 1];
					if (char.IsDigit(nextChar))
					{
						wordBuilder[i] = char.ToLower(currentChar);
					}
				}

				if (currentChar == '.')
				{
					// Set the rest of the string to be lowercase
					var extension = transientWord.Substring(i).ToLower();
					transientWord = transientWord.Remove(i);
					transientWord += extension;
				}

				previousChar = currentChar;
			}

			return wordBuilder.ToString();
		}

		/// <summary>
		/// Determines if the specified string is all upper-case.
		/// </summary>
		/// <returns><c>true</c> if the specified string is all upper-case; otherwise, <c>false</c>.</returns>
		/// <param name="str">String.</param>
		public static bool IsAllUpper(this string str)
		{
			foreach (var c in str)
			{
				if (char.IsLower(c))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Determines if the specified string is all lower-case.
		/// </summary>
		/// <returns><c>true</c> if the specified string is all lower-case; otherwise, <c>false</c>.</returns>
		/// <param name="str">String.</param>
		public static bool IsAllLower(this string str)
		{
			foreach (var c in str)
			{
				if (char.IsUpper(c))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Determines if the specified string is a filename.
		/// </summary>
		/// <returns><c>true</c> if is file the specified str; otherwise, <c>false</c>.</returns>
		/// <param name="str">String.</param>
		public static bool IsFilename(this string str)
		{
			return str.Contains(".");
		}

		/// <summary>
		/// Determines if the specifed string (as a filename) is all uppercase. This ignores the extension.
		/// </summary>
		/// <returns><c>true</c> if is filename all upper the specified str; otherwise, <c>false</c>.</returns>
		/// <param name="str">String.</param>
		public static string GetFilename(this string str)
		{
			if (str.IsFilename())
			{
				var parts = str.Split('.');
				return parts[0];
			}
			else
			{
				return str;
			}
		}

		/// <summary>
		/// Determines if the specified string is mixed-case.
		/// </summary>
		/// <returns><c>true</c> if the specified string is mixed-case; otherwise, <c>false</c>.</returns>
		/// <param name="str">String.</param>
		public static bool IsMixedCase(this string str)
		{
			return !str.IsAllLower() && !str.IsAllUpper();
		}

		/// <summary>
		/// Determines if the specified string has more than one upper-case letter.
		/// </summary>
		/// <returns><c>true</c> if the specified string has more than one upper-case letter; otherwise, <c>false</c>.</returns>
		/// <param name="str">String.</param>
		public static bool HasMoreThanOneVersal(this string str)
		{
			var versalCount = 0;
			foreach (var c in str)
			{
				if (char.IsUpper(c))
				{
					++versalCount;
				}

				if (versalCount > 1)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Determines if the specified string starts with a single upper-case letter.
		/// </summary>
		/// <returns><c>true</c> if the specified string starts with a single upper-case letter; otherwise, <c>false</c>.</returns>
		/// <param name="str">String.</param>
		public static bool StartsWithSingleUpper(this string str)
		{
			if (str.Length > 0)
			{
				if (str.Length > 1)
				{
					return char.IsUpper(str[0]) && !char.IsUpper(str[1]);
				}
				else
				{
					return char.IsUpper(str[0]);
				}
			}
			return false;
		}
	}
}

