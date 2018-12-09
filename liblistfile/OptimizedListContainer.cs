//
//  OptimizedListContainer.cs
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
using Warcraft.Core.Extensions;
using Warcraft.Core.Interfaces;

namespace ListFile
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
    [PublicAPI]
    public class OptimizedListContainer : IBinarySerializable
    {
        /// <summary>
        /// The binary file signature. Used when serializing this object into an RIFF-style
        /// format.
        /// </summary>
        [PublicAPI, NotNull]
        public const string Signature = "OLIC";

        /// <summary>
        /// The file extension used for serialized list containers.
        /// </summary>
        [PublicAPI, NotNull]
        public const string Extension = "olc";

        /// <summary>
        /// The file format version.
        /// </summary>
        [PublicAPI]
        public const uint Version = 1;

        /// <summary>
        /// Gets the name of the archive this container has lists for.
        /// </summary>
        [PublicAPI, NotNull]
        public string PackageName { get; }

        /// <summary>
        /// Gets the optimized lists contained in this container.
        /// </summary>
        [PublicAPI]
        public Dictionary<byte[], OptimizedList> OptimizedLists { get; }
            = new Dictionary<byte[], OptimizedList>(new ByteArrayComparer());

        /// <summary>
        /// Initializes a new instance of the <see cref="OptimizedListContainer"/> class.
        /// This constructor creates a new, empty list container.
        /// </summary>
        /// <param name="inArchiveName">In archive name.</param>
        [PublicAPI]
        public OptimizedListContainer([NotNull] string inArchiveName)
        {
            PackageName = inArchiveName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptimizedListContainer"/> class.
        /// This constructor loads an existing list container from a block of bytes.
        /// </summary>
        /// <param name="data">File stream.</param>
        [PublicAPI]
        public OptimizedListContainer([NotNull] byte[] data)
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
                    if (storedVersion != Version)
                    {
                        throw new NotSupportedException();
                    }

                    PackageName = br.ReadNullTerminatedString();

                    var entryCount = br.ReadUInt32();
                    for (var i = 0; i < entryCount; ++i)
                    {
                        var listSignature = new string(br.ReadChars(4));
                        if (listSignature != OptimizedList.Signature)
                        {
                            throw new InvalidDataException("The input data did not begin with a list signature.");
                        }

                        var blockSize = br.ReadUInt64();

                        var optimizedList = new OptimizedList(br.ReadBytes((int)blockSize));
                        OptimizedLists.Add(optimizedList.PackageHash, optimizedList);
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether the specifed package hash has any lists stored in the container.
        /// </summary>
        /// <returns><c>true</c>, if the hash has any lists, <c>false</c> otherwise.</returns>
        /// <param name="packageHash">Package hash.</param>
        [PublicAPI]
        public bool ContainsPackageListfile([NotNull] byte[] packageHash)
        {
            return OptimizedLists.ContainsKey(packageHash);
        }

        /// <summary>
        /// Determines whether the specifed list is the same as the one which is stored in the container.
        /// </summary>
        /// <returns><c>true</c> if the specifed list is the same as the one which is stored in the container; otherwise, <c>false</c>.</returns>
        /// <param name="inOptimizedList">Optimized list.</param>
        [PublicAPI]
        public bool IsListSameAsStored([NotNull] OptimizedList inOptimizedList)
        {
            if (ContainsPackageListfile(inOptimizedList.PackageHash))
            {
                var optimizedList = OptimizedLists[inOptimizedList.PackageHash];
                return optimizedList.ListHash.Equals(inOptimizedList.ListHash);
            }

            throw new KeyNotFoundException("The specified package did not have an optimized list stored.");
        }

        /// <summary>
        /// Adds the optimized list to the container. If the list is already in the container,
        /// it is not added.
        /// </summary>
        /// <param name="inOptimizedList">List.</param>
        [PublicAPI]
        public void AddOptimizedList([NotNull] OptimizedList inOptimizedList)
        {
            if (!OptimizedLists.ContainsKey(inOptimizedList.PackageHash))
            {
                OptimizedLists.Add(inOptimizedList.PackageHash, inOptimizedList);
            }
        }

        /// <summary>
        /// Replaces the optimized list stored in the container (under the same package hash) with the provided list.
        /// </summary>
        /// <param name="inOptimizedList">List.</param>
        [PublicAPI]
        public void ReplaceOptimizedList([NotNull] OptimizedList inOptimizedList)
        {
            if (OptimizedLists.ContainsKey(inOptimizedList.PackageHash))
            {
                OptimizedLists[inOptimizedList.PackageHash] = inOptimizedList;
            }
        }

        /// <summary>
        /// Serializes the object into a byte array.
        /// </summary>
        /// <returns>The bytes.</returns>
        [PublicAPI, NotNull]
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
                    bw.WriteNullTerminatedString(PackageName);
                    bw.Write((uint)OptimizedLists.Count);

                    foreach (var listPair in OptimizedLists)
                    {
                        bw.Write(listPair.Value.Serialize());
                    }
                }

                return ms.ToArray();
            }
        }
    }
}
