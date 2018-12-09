//
//  ExtensionMethods.cs
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
using System.Security.Cryptography;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using Warcraft.Core.Extensions;

namespace ListFile
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
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(byteArray);
            }
        }

        /// <summary>
        /// Compresses this byte array using a BZip2 algorithm.
        /// </summary>
        /// <param name="uncompressedBytes">Uncompressed bytes.</param>
        /// <returns>The uncompressed list.</returns>
        public static byte[] Compress(this byte[] uncompressedBytes)
        {
            byte[] compressedBytes;
            if (uncompressedBytes.Length > 0)
            {
                using (var om = new MemoryStream())
                {
                    using (var bo = new BZip2Stream(om, CompressionMode.Compress, true))
                    {
                        var serializedList = uncompressedBytes;
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
        /// <returns>The compressed list.</returns>
        public static byte[] Compress(this List<string> inputList)
        {
            return inputList.Serialize().Compress();
        }

        /// <summary>
        /// Replaces an instance of a string with another inside this string. This replacement is case-insensitive.
        /// Algorithm taken from https://www.codeproject.com/Articles/10890/Fastest-C-Case-Insenstive-String-Replace.
        /// </summary>
        /// <param name="original">The original string.</param>
        /// <param name="pattern">The pattern to replace.</param>
        /// <param name="replacement">The replacement for the pattern.</param>
        /// <returns>The string, with the pattern replaced.</returns>
        public static string FastReplaceCaseInsensitive(this string original, string pattern, string replacement)
        {
            int position0;
            int position1;

            var count = position0 = 0;

            var upperString = original.ToUpper();
            var upperPattern = pattern.ToUpper();

            var inc = (original.Length / pattern.Length) * (replacement.Length - pattern.Length);

            var chars = new char[original.Length + Math.Max(0, inc)];

            while ((position1 = upperString.IndexOf(upperPattern, position0, StringComparison.Ordinal)) != -1)
            {
                for (var i = position0; i < position1; ++i)
                {
                    chars[count++] = original[i];
                }

                for (var i = 0; i < replacement.Length; ++i)
                {
                    chars[count++] = replacement[i];
                }

                position0 = position1 + pattern.Length;
            }

            if (position0 == 0)
            {
                return original;
            }

            for (var i = position0; i < original.Length; ++i)
            {
                chars[count++] = original[i];
            }

            return new string(chars, 0, count);
        }

        /// <summary>
        /// Serialize the specified list of strings into a flat array of null-terminated strings.
        /// </summary>
        /// <param name="stringList">String list.</param>
        /// <returns>The serialized list.</returns>
        public static byte[] Serialize(this List<string> stringList)
        {
            using (var ms = new MemoryStream(stringList.GetSerializedSize()))
            {
                using (var bw = new BinaryWriter(ms))
                {
                    foreach (var entry in stringList)
                    {
                        bw.WriteNullTerminatedString(entry);
                    }
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Gets the size of the string list when it's been serialized.
        /// </summary>
        /// <returns>The serialized size.</returns>
        /// <param name="stringList">String list.</param>
        public static int GetSerializedSize(this IEnumerable<string> stringList)
        {
            var listSize = 0;
            foreach (var entry in stringList)
            {
                listSize += entry.Length + 1;
            }

            return listSize;
        }
    }
}
