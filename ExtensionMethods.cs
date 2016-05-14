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

namespace liblistfile
{
	/// <summary>
	/// A set of extension methods used for serialization.
	/// </summary>
	public static class ExtensionMethods
	{
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

