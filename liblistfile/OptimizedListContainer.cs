//
//  OptimizedListContainer.cs
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
using Warcraft.Core.Extensions;
using Warcraft.Core.Interfaces;

namespace liblistfile
{
    /// <summary>
    /// A container for OptimizedList objects.
    ///     Optimized listfile container for MPQ archives.
    /// Contains one or more listfiles that have been optimized using a scored
    /// dictionary.
    ///
    /// The file is structured as follows:
    ///
    /// char[4]                        : Signature (always OLIC)
    /// uint32_t                    : Version
    /// char[]                        : ArchiveName (the name of the archive which
    ///                               uses one of the lists)
    /// uint32_t                    : ListCount (number of stored optimized lists)
    /// OptimizedList[ListCount]    : The lists contained in the file.
    ///
    /// </summary>
    public class OptimizedListContainer : IBinarySerializable
    {
        /// <summary>
        /// The binary file signature. Used when serializing this object into an RIFF-style
        /// format.
        /// </summary>
        public const string Signature = "OLIC";

        /// <summary>
        /// The file extension used for serialized list containers.
        /// </summary>
        public const string Extension = "olc";

        /// <summary>
        /// The file format version.
        /// </summary>
        public const uint Version = 1;

        /// <summary>
        /// The name of the archive this container has lists for.
        /// </summary>
        public readonly string PackageName;

        /// <summary>
        /// The optimized lists contained in this container.
        /// </summary>
        public readonly Dictionary<byte[], OptimizedList> OptimizedLists = new Dictionary<byte[], OptimizedList>(new ByteArrayComparer());

        /// <summary>
        /// Initializes a new instance of the <see cref="liblistfile.OptimizedListContainer"/> class.
        /// This constructor creates a new, empty list container.
        /// </summary>
        /// <param name="inArchiveName">In archive name.</param>
        public OptimizedListContainer(string inArchiveName)
        {
            this.PackageName = inArchiveName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="liblistfile.OptimizedListContainer"/> class.
        /// This constructor loads an existing list container from a block of bytes.
        /// </summary>
        /// <param name="data">File stream.</param>
        public OptimizedListContainer(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var br = new BinaryReader(ms))
                {
                    var dataSignature = new string(br.ReadChars(4));
                    if (dataSignature != Signature)
                    {
                        throw new InvalidDataException("The input data did not begin with a container signature.");
                    }

                    var storedVersion = br.ReadUInt32();

                    this.PackageName = br.ReadNullTerminatedString();

                    var entryCount = br.ReadUInt32();
                    for (var i = 0; i < entryCount; ++i)
                    {
                        var listSignature = new string(br.ReadChars(4));
                        if (listSignature != OptimizedList.Signature)
                        {
                            throw new InvalidDataException("The input data did not begin with a list signature.");
                        }
                        var blockSize = br.ReadUInt64();

                        var optimizedList = new OptimizedList(br.ReadBytes((int)(blockSize)));
                        this.OptimizedLists.Add(optimizedList.PackageHash, optimizedList);
                    }

                    if (storedVersion < Version)
                    {
                        // Do whatever updating needs to be done
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether the specifed package hash has any lists stored in the container.
        /// </summary>
        /// <returns><c>true</c>, if the hash has any lists, <c>false</c> otherwise.</returns>
        /// <param name="packageHash">Package hash.</param>
        public bool ContainsPackageListfile(byte[] packageHash)
        {
            return this.OptimizedLists.ContainsKey(packageHash);
        }

        /// <summary>
        /// Determines whether the specifed list is the same as the one which is stored in the container.
        /// </summary>
        /// <returns><c>true</c> if the specifed list is the same as the one which is stored in the container; otherwise, <c>false</c>.</returns>
        /// <param name="inOptimizedList">Optimized list.</param>
        public bool IsListSameAsStored(OptimizedList inOptimizedList)
        {
            if (ContainsPackageListfile(inOptimizedList.PackageHash))
            {
                var optimizedList = this.OptimizedLists[inOptimizedList.PackageHash];
                return optimizedList.ListHash.Equals(inOptimizedList.ListHash);
            }
            else
            {
                throw new KeyNotFoundException("The specified package did not have an optimized list stored.");
            }
        }

        /// <summary>
        /// Adds the optimized list to the container. If the list is already in the container,
        /// it is not added.
        /// </summary>
        /// <param name="inOptimizedList">List.</param>
        public void AddOptimizedList(OptimizedList inOptimizedList)
        {
            if (!this.OptimizedLists.ContainsKey(inOptimizedList.PackageHash))
            {
                this.OptimizedLists.Add(inOptimizedList.PackageHash, inOptimizedList);
            }
        }

        /// <summary>
        /// Replaces the optimized list stored in the container (under the same package hash) with the provided list.
        /// </summary>
        /// <param name="inOptimizedList">List.</param>
        public void ReplaceOptimizedList(OptimizedList inOptimizedList)
        {
            if (this.OptimizedLists.ContainsKey(inOptimizedList.PackageHash))
            {
                this.OptimizedLists[inOptimizedList.PackageHash] = inOptimizedList;
            }
        }

        /// <summary>
        /// Serializes the object into a byte array.
        /// </summary>
        /// <returns>The bytes.</returns>
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
                    bw.Write(Version);
                    bw.WriteNullTerminatedString(this.PackageName);
                    bw.Write((uint)this.OptimizedLists.Count);

                    foreach (var listPair in this.OptimizedLists)
                    {
                        bw.Write(listPair.Value.Serialize());
                    }
                }

                return ms.ToArray();
            }
        }
    }
}

