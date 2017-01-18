//
//  ByteArrayComparer.cs
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
using System.Linq;

namespace liblistfile
{
    /// <summary>
    /// Equality comparator for byte arrays used in the optimized lists.
    /// This comparator compares the arrays based on their contents, and not their references.
    /// </summary>
    internal class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        /// <summary>
        /// Determines whether the two arrays are equal.
        /// </summary>
        /// <param name="left">Left.</param>
        /// <param name="right">Right.</param>
        public bool Equals(byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return left.SequenceEqual(right);
        }

        /// <Docs>The object for which the hash code is to be returned.</Docs>
        /// <para>Returns a hash code for the specified object.</para>
        /// <returns>A hash code for the specified object.</returns>
        /// <summary>
        /// Gets the hash code.
        /// </summary>
        /// <param name="key">Key.</param>
        public int GetHashCode(byte[] key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key), "The byte may noy be null.");

            }

            return key.Sum(b => b);
        }
    }
}