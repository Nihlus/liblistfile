//
//  ExtensionMethods.cs
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
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using Warcraft.Core.Extensions;

namespace liblistfile
{
	/// <summary>
	/// A set of extension methods used for serialization.
	/// </summary>
	public static class ExtensionMethods
	{
		/// <summary>
		/// Computes the MD5 hash for this byte array.
		/// </summary>
		/// <returns>The hash.</returns>
		/// <param name="byteArray">Byte array.</param>
		public static byte[] ComputeHash(this byte[] byteArray)
		{
			using (MD5 md5 = MD5.Create())
			{
				return md5.ComputeHash(byteArray);
			}
		}

		/// <summary>
		/// Compresses this byte array using a BZip2 algorithm.
		/// </summary>
		/// <param name="uncompressedBytes">Uncompressed bytes.</param>
		public static byte[] Compress(this byte[] uncompressedBytes)
		{
			byte[] compressedBytes;
			if (uncompressedBytes.Length > 0)
			{
				using (MemoryStream om = new MemoryStream())
				{
					using (BZip2Stream bo = new BZip2Stream(om, CompressionMode.Compress, true))
					{
						byte[] serializedList = uncompressedBytes;
						bo.Write(serializedList, 0, serializedList.Length);
					}

					compressedBytes = om.ToArray();
				}
			}
			else
			{
				compressedBytes = new byte[0];
			}

			return compressedBytes;
		}

		/// <summary>
		/// Compresses this string list using a BZip2 algorithm.
		/// The strings are first stored in list order as null-terminated strings.
		/// </summary>
		/// <param name="inputList">Input list.</param>
		public static byte[] Compress(this List<string> inputList)
		{
			return inputList.Serialize().Compress();
		}

		/// <summary>
		/// Replaces an instance of a string with another inside this string. This replacement
		/// is case-insensitive.
		/// </summary>
		/// <returns>The string with the instance replaced.</returns>
		/// <param name="input">Input.</param>
		/// <param name="search">Search.</param>
		/// <param name="replacement">Replacement.</param>
		public static string ReplaceCaseInsensitive(this string input, string search, string replacement)
		{
			string result = Regex.Replace(
				                input,
				                Regex.Escape(search),
				                replacement.Replace("$", "$$"),
				                RegexOptions.IgnoreCase
			                );
			return result;
		}

		/// <summary>
		/// Serialize the specified stringList.
		/// </summary>
		/// <param name="stringList">String list.</param>
		public static byte[] Serialize(this List<string> stringList)
		{
			using (MemoryStream ms = new MemoryStream(stringList.GetSerializedSize()))
			{
				using (BinaryWriter bw = new BinaryWriter(ms))
				{
					foreach (string entry in stringList)
					{
						bw.WriteNullTerminatedString(entry);
					}
				}

				return ms.ToArray();
			}
		}

		/// <summary>
		/// Gets the size of the stringlist when it's been serialized.
		/// </summary>
		/// <returns>The serialized size.</returns>
		/// <param name="stringList">String list.</param>
		public static int GetSerializedSize(this IEnumerable<string> stringList)
		{
			int listSize = 0;
			foreach (string entry in stringList)
			{
				listSize += entry.Length + 1;
			}

			return listSize;
		}
	}
}

