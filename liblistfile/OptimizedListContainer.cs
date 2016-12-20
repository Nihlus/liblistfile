//
//  MyClass.cs
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
using System.Linq;
using Ionic.BZip2;
using Warcraft.Core;

namespace liblistfile
{
	/// <summary>
	/// A container for OptimizedList objects.
	/// 	Optimized listfile container for MPQ archives.
	/// Contains one or more listfiles that have been optimized using a scored
	/// dictionary.
	///
	/// The file is structured as follows:
	///
	/// char[4]						: Signature (always OLIC)
	/// uint32_t					: Version
	/// char[]						: ArchiveName (the name of the archive which
	/// 							  uses one of the lists)
	/// uint32_t					: ListCount (number of stored optimized lists)
	/// OptimizedList[ListCount]	: The lists contained in the file.
	///
	/// </summary>
	public class OptimizedListContainer
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
			using (MemoryStream ms = new MemoryStream(data))
			{
				using (BinaryReader br = new BinaryReader(ms))
				{
					string dataSignature = new string(br.ReadChars(4));
					if (dataSignature != Signature)
					{
						throw new InvalidDataException("The input data did not begin with a container signature.");
					}

					uint storedVersion = br.ReadUInt32();

					this.PackageName = br.ReadNullTerminatedString();

					uint entryCount = br.ReadUInt32();
					for (int i = 0; i < entryCount; ++i)
					{
						string listSignature = new string(br.ReadChars(4));
						if (listSignature != OptimizedList.Signature)
						{
							throw new InvalidDataException("The input data did not begin with a list signature.");
						}
						ulong blockSize = br.ReadUInt64();

						OptimizedList optimizedList = new OptimizedList(br.ReadBytes((int)(blockSize)));
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
				OptimizedList optimizedList = this.OptimizedLists[inOptimizedList.PackageHash];
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
		public byte[] GetBytes()
		{
			using (MemoryStream ms = new MemoryStream())
			{
				using (BinaryWriter bw = new BinaryWriter(ms))
				{
					foreach (char c in Signature)
					{
						bw.Write(c);
					}
					bw.Write(Version);
					bw.WriteNullTerminatedString(this.PackageName);
					bw.Write((uint)this.OptimizedLists.Count);

					foreach (KeyValuePair<byte[], OptimizedList> listPair in this.OptimizedLists)
					{
						bw.Write(listPair.Value.GetBytes());
					}
				}

				return ms.ToArray();
			}
		}
	}

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
	public class OptimizedList : IEquatable<OptimizedList>
	{
		/// <summary>
		/// The binary file signature. Used when serializing this object into an RIFF-style
		/// format.
		/// </summary>
		public const string Signature = "LIST";

		/// <summary>
		/// A 126-bit MD5 hash of the archive this list is made for.
		/// </summary>
		public readonly byte[] PackageHash = new byte[16];

		/// <summary>
		/// A 126-bit MD5 hash of the compressed list data.
		/// </summary>
		public readonly byte[] ListHash = new byte[16];

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
		/// <param name="data">Data.</param>
		public OptimizedList(byte[] data)
		{
			using (MemoryStream ms = new MemoryStream(data))
			{
				using (BinaryReader br = new BinaryReader(ms))
				{
					// Read the MD5 archive signature.
					this.PackageHash = br.ReadBytes(this.PackageHash.Length);
					this.ListHash = br.ReadBytes(this.ListHash.Length);

					long hashesSize = (this.PackageHash.LongLength + this.ListHash.LongLength);
					int compressedDataSize = (int)(data.LongLength - hashesSize);
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
		/// Serializes the object into a byte array.
		/// </summary>
		/// <returns>The bytes.</returns>
		public byte[] GetBytes()
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

