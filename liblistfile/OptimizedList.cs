//
//  OptimizedList.cs
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
using JetBrains.Annotations;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using Warcraft.Core.Extensions;
using Warcraft.Core.Interfaces;

namespace ListFile
{
    /// <summary>
    /// An optimized list of file paths.
    /// Each OptimizedList entry (when serialized) is structured as follows:
    ///
    /// char[4]                        : Signature (always LIST)
    /// uint64_t                    : BlockSize (byte count, always CompressedData + 64)
    /// int128_t                    : PackageHash (MD5 hash of the package the list works for.) 16 bytes.
    /// int128_t                    : ListHash (MD5 hash of the compressed optimized list.) 16 bytes.
    /// byte[]                        : BZip2 compressed cleartext list of optimized
    ///                                   list entries.
    /// </summary>
    [PublicAPI]
    public class OptimizedList : IEquatable<OptimizedList>, IBinarySerializable, IIFFChunk
    {
        /// <summary>
        /// The binary file signature. Used when serializing this object into an RIFF-style
        /// format.
        /// </summary>
        [PublicAPI, NotNull]
        public const string Signature = "LIST";

        /// <summary>
        /// Gets a 128-bit MD5 hash of the archive this list is made for.
        /// </summary>
        [PublicAPI, NotNull]
        public byte[] PackageHash { get; private set; } = new byte[16];

        /// <summary>
        /// Gets a 128-bit MD5 hash of the compressed list data.
        /// </summary>
        [PublicAPI, NotNull]
        public byte[] ListHash { get; private set; } = new byte[16];

        /// <summary>
        /// Gets the optimized paths contained in this list.
        /// </summary>
        [PublicAPI]
        public List<string> OptimizedPaths { get; } = new List<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="OptimizedList"/> class.
        /// This constructor creates a new, empty OptimizedList.
        /// </summary>
        /// <param name="inPackageHash">In archive signature.</param>
        /// <param name="inOptimizedPaths">In optimized paths.</param>
        [PublicAPI]
        public OptimizedList([NotNull] byte[] inPackageHash, [NotNull, ItemNotNull] List<string> inOptimizedPaths)
        {
            PackageHash = inPackageHash;
            OptimizedPaths = inOptimizedPaths;

            ListHash = OptimizedPaths.Compress().ComputeHash();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptimizedList"/> class.
        /// This constructor loads an existing OptimizedList from a byte array.
        /// </summary>
        /// <param name="inData">Data.</param>
        [PublicAPI]
        public OptimizedList([NotNull] byte[] inData)
        {
            LoadBinaryData(inData);
        }

        /// <summary>
        /// Deserializes the provided binary data of the object. This is the full data block which follows the data
        /// signature and data block length.
        /// </summary>
        /// <param name="inData">The binary data containing the object.</param>
        [PublicAPI]
        public void LoadBinaryData([NotNull] byte[] inData)
        {
            using (var ms = new MemoryStream(inData))
            {
                using (var br = new BinaryReader(ms))
                {
                    // Read the MD5 archive signature.
                    PackageHash = br.ReadBytes(PackageHash.Length);
                    ListHash = br.ReadBytes(ListHash.Length);

                    var hashesSize = PackageHash.LongLength + ListHash.LongLength;
                    var compressedDataSize = (int)(inData.LongLength - hashesSize);
                    using (var compressedData = new MemoryStream(br.ReadBytes(compressedDataSize)))
                    {
                        using (var bz = new BZip2Stream(compressedData, CompressionMode.Decompress, true))
                        {
                            using (var decompressedData = new MemoryStream())
                            {
                                // Decompress the data into the stream
                                bz.CopyTo(decompressedData);

                                // Read the decompressed strings
                                decompressedData.Position = 0;
                                using (var listReader = new BinaryReader(decompressedData))
                                {
                                    while (decompressedData.Position < decompressedData.Length)
                                    {
                                        OptimizedPaths.Add(listReader.ReadNullTerminatedString());
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the static data signature of this data block type.
        /// </summary>
        /// <returns>A string representing the block signature.</returns>
        [NotNull, Pure]
        public string GetSignature()
        {
            return Signature;
        }

        /// <summary>
        /// Serializes the object into a byte array.
        /// </summary>
        /// <returns>The bytes.</returns>
        [NotNull, Pure]
        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    foreach (var c in Signature)
                    {
                        bw.Write(c);
                    }

                    var compressedList = OptimizedPaths.Compress();

                    var blockSize = (ulong)PackageHash.LongLength + (ulong)ListHash.LongLength + (ulong)compressedList.LongLength;
                    bw.Write(blockSize);

                    bw.Write(PackageHash);

                    // Calculate and write the hash of the compressed data
                    bw.Write(compressedList.ComputeHash());

                    bw.Write(compressedList);
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Determines whether the specified <see cref="object"/> is equal to the current <see cref="OptimizedList"/>.
        /// </summary>
        /// <param name="obj">The <see cref="object"/> to compare with the current <see cref="OptimizedList"/>.</param>
        /// <returns><c>true</c> if the specified <see cref="object"/> is equal to the current
        /// <see cref="OptimizedList"/>; otherwise, <c>false</c>.</returns>
        public override bool Equals(object obj)
        {
            if (obj is OptimizedList other)
            {
                return Equals(other);
            }

            return false;
        }

        /// <summary>
        /// Determines whether the specified <see cref="OptimizedList"/> is equal to the current <see cref="OptimizedList"/>.
        /// </summary>
        /// <param name="other">The <see cref="OptimizedList"/> to compare with the current <see cref="OptimizedList"/>.</param>
        /// <returns><c>true</c> if the specified <see cref="OptimizedList"/> is equal to the current
        /// <see cref="OptimizedList"/>; otherwise, <c>false</c>.</returns>
        public bool Equals(OptimizedList other)
        {
            if (other == null)
            {
                return false;
            }

            return PackageHash.Equals(other.PackageHash) &&
                   new HashSet<string>(OptimizedPaths).SetEquals(new HashSet<string>(other.OptimizedPaths));
        }

        /// <summary>
        /// Serves as a hash function for a <see cref="OptimizedList"/> object.
        /// </summary>
        /// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
        public override int GetHashCode()
        {
            return (PackageHash.GetHashCode() + OptimizedPaths.GetHashCode()).GetHashCode();
        }
    }
}
