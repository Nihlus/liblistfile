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
using System;
using System.Collections.Generic;
using System.IO;
using Warcraft.Core;
using System.Text.RegularExpressions;
using Ionic.BZip2;
using System.Security.Cryptography;

namespace liblistfile
{
	/// <summary>
	/// A set of extension methods used for serialization.
	/// </summary>
	public static class ExtensionMethods
	{
		public static byte[] ComputeHash(this byte[] byteArray)
		{
			using (MD5 md5 = MD5.Create())
			{
				return md5.ComputeHash(byteArray);
			}
		}

		public static byte[] Compress(this byte[] uncompressedBytes)
		{
			byte[] compressedBytes;
			if (uncompressedBytes.Length > 0)
			{
				using (MemoryStream om = new MemoryStream())
				{
					using (BZip2OutputStream bo = new BZip2OutputStream(om))
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

		public static byte[] Compress(this List<string> inputList)
		{
			byte[] compressedList;
			if (inputList.Count > 0)
			{
				using (MemoryStream om = new MemoryStream())
				{
					using (BZip2OutputStream bo = new BZip2OutputStream(om))
					{
						byte[] serializedList = inputList.Serialize();
						bo.Write(serializedList, 0, serializedList.Length);
					}
					compressedList = om.ToArray();
				}
			}
			else
			{
				compressedList = new byte[0];
			}

			return compressedList;
		}

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
		public static int GetSerializedSize(this List<string> stringList)
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

