//
//  OptimizedList.cs
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
using Ionic.BZip2;
using Warcraft.Core;
using Warcraft.Core.Interfaces;

namespace liblistfile
{
    /// <summary>
    /// An optimized list of file paths.
    /// Each OptimizedList entry (when serialized) is structured as follows:
    ///
    /// char[4]						: Signature (always LIST)
    /// uint64_t					: BlockSize (byte count, always CompressedData + 64)
    /// int128_t					: PackageHash (MD5 hash of the package the list works for.) 16 bytes.
    /// int128_t					: ListHash (MD5 hash of the compressed optimized list.) 16 bytes.
    /// byte[]						: BZip2 compressed cleartext list of optimized
    ///							 	  list entries.
    /// </summary>
    public class OptimizedList : IEquatable<OptimizedList>, IBinarySerializable, IRIFFChunk
    {
        /// <summary>
        /// The binary file signature. Used when serializing this object into an RIFF-style
        /// format.
        /// </summary>
        public const string Signature = "LIST";

        /// <summary>
        /// A 128-bit MD5 hash of the archive this list is made for.
        /// </summary>
        public byte[] PackageHash = new byte[16];

        /// <summary>
        /// A 128-bit MD5 hash of the compressed list data.
        /// </summary>
        public byte[] ListHash = new byte[16];

        /// <summary>
        /// The optimized paths contained in this list.
        /// </summary>
        public readonly List<string> OptimizedPaths = new List<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="liblistfile.OptimizedList"/> class.
        /// This constructor creates a new, empty OptimizedList.
        /// </summary>
        /// <param name="inPackageHash">In archive signature.</param>
        /// <param name="inOptimizedPaths">In optimized paths.</param>
        public OptimizedList(byte[] inPackageHash, List<string> inOptimizedPaths)
        {
            this.PackageHash = inPackageHash;
            this.OptimizedPaths = inOptimizedPaths;

            this.ListHash = this.OptimizedPaths.Compress().ComputeHash();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="liblistfile.OptimizedList"/> class.
        /// This constructor loads an existing OptimizedList from a byte array.
        /// </summary>
        /// <param name="inData">Data.</param>
        public OptimizedList(byte[] inData)
        {
            LoadBinaryData(inData);
        }


	    /// <summary>
	    /// Deserialzes the provided binary data of the object. This is the full data block which follows the data
	    /// signature and data block length.
	    /// </summary>
	    /// <param name="inData">The binary data containing the object.</param>
	    public void LoadBinaryData(byte[] inData)
        {
            using (MemoryStream ms = new MemoryStream(inData))
            {
                using (BinaryReader br = new BinaryReader(ms))
                {
                    // Read the MD5 archive signature.
                    this.PackageHash = br.ReadBytes(this.PackageHash.Length);
                    this.ListHash = br.ReadBytes(this.ListHash.Length);

                    long hashesSize = (this.PackageHash.LongLength + this.ListHash.LongLength);
                    int compressedDataSize = (int)(inData.LongLength - hashesSize);
                    using (MemoryStream compressedData = new MemoryStream(br.ReadBytes(compressedDataSize)))
                    {
                        using (BZip2InputStream bz = new BZip2InputStream(compressedData))
                        {
                            using (MemoryStream decompressedData = new MemoryStream())
                            {
                                // Decompress the data into the stream
                                bz.CopyTo(decompressedData);

                                // Read the decompressed strings
                                decompressedData.Position = 0;
                                using (BinaryReader listReader = new BinaryReader(decompressedData))
                                {
                                    while (decompressedData.Position < decompressedData.Length)
                                    {
                                        this.OptimizedPaths.Add(listReader.ReadNullTerminatedString());
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
	    public string GetSignature()
        {
            return Signature;
        }

        /// <summary>
        /// Serializes the object into a byte array.
        /// </summary>
        /// <returns>The bytes.</returns>
        public byte[] Serialize()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    foreach (char c in Signature)
                    {
                        bw.Write(c);
                    }

                    byte[] compressedList = this.OptimizedPaths.Compress();

                    ulong blockSize = (ulong)this.PackageHash.LongLength + (ulong)this.ListHash.LongLength + (ulong)compressedList.LongLength;
                    bw.Write(blockSize);

                    bw.Write(this.PackageHash);
                    // Calculate and write the hash of the compressed data
                    bw.Write(compressedList.ComputeHash());

                    bw.Write(compressedList);
                }

                return ms.ToArray();
            }
        }

        #region IEquatable implementation

        /// <summary>
        /// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="liblistfile.OptimizedList"/>.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object"/> to compare with the current <see cref="liblistfile.OptimizedList"/>.</param>
        /// <returns><c>true</c> if the specified <see cref="System.Object"/> is equal to the current
        /// <see cref="liblistfile.OptimizedList"/>; otherwise, <c>false</c>.</returns>
        public override bool Equals(object obj)
        {
            OptimizedList other = obj as OptimizedList;
            if (other != null)
            {
                return Equals(other);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Determines whether the specified <see cref="liblistfile.OptimizedList"/> is equal to the current <see cref="liblistfile.OptimizedList"/>.
        /// </summary>
        /// <param name="other">The <see cref="liblistfile.OptimizedList"/> to compare with the current <see cref="liblistfile.OptimizedList"/>.</param>
        /// <returns><c>true</c> if the specified <see cref="liblistfile.OptimizedList"/> is equal to the current
        /// <see cref="liblistfile.OptimizedList"/>; otherwise, <c>false</c>.</returns>
        public bool Equals(OptimizedList other)
        {
            if (other == null)
            {
                return false;
            }

            return this.PackageHash.Equals(other.PackageHash) &&
                   new HashSet<string>(this.OptimizedPaths).SetEquals(new HashSet<string>(other.OptimizedPaths));
        }

        /// <summary>
        /// Serves as a hash function for a <see cref="liblistfile.OptimizedList"/> object.
        /// </summary>
        /// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
        public override int GetHashCode()
        {
            return (this.PackageHash.GetHashCode() + this.OptimizedPaths.GetHashCode()).GetHashCode();
        }

        #endregion
    }
}