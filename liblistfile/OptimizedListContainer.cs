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
using Ionic.BZip2;
using Warcraft.Core;
using System.Security.Cryptography;
using System.Linq;

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
		/// <param name="InArchiveName">In archive name.</param>
		public OptimizedListContainer(string InArchiveName)
		{
			this.PackageName = InArchiveName;
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
					if (dataSignature != OptimizedListContainer.Signature)
					{
						throw new InvalidDataException("The input data did not begin with a container signature.");
					}

					uint Version = br.ReadUInt32();

					this.PackageName = br.ReadNullTerminatedString();

					uint entryCount = br.ReadUInt32();
					for (int i = 0; i < entryCount; ++i)
					{
						OptimizedList optimizedList = new OptimizedList(br.ReadBytes((int)(PeekListBlockSize(br) + 12)));
						this.OptimizedLists.Add(optimizedList.PackageHash, optimizedList);
					}

					if (Version < OptimizedListContainer.Version)
					{
						// Do whatever updating needs to be done
					}
				}
			}
		}

		/// <summary>
		/// Peeks the size of the list block.
		/// </summary>
		/// <returns>The list block size.</returns>
		/// <param name="br">Br.</param>
		private ulong PeekListBlockSize(BinaryReader br)
		{
			long currentPosition = br.BaseStream.Position;

			string dataSignature = new string(br.ReadChars(4));
			if (dataSignature != OptimizedList.Signature)
			{
				throw new InvalidDataException("The input data did not begin with a list signature.");
			}

			ulong BlockSize = br.ReadUInt64();

			// Return to the previous position
			br.BaseStream.Position = currentPosition;

			return BlockSize;
		}

		/// <summary>
		/// Determines whether the specifed package hash has any lists stored in the container.
		/// </summary>
		/// <returns><c>true</c>, if the hash has any lists, <c>false</c> otherwise.</returns>
		/// <param name="PackageHash">Package hash.</param>
		public bool ContainsPackageList(byte[] PackageHash)
		{
			return this.OptimizedLists.ContainsKey(PackageHash);
		}

		/// <summary>
		/// Determines whether the specifed list is the same as the one which is stored in the container.
		/// </summary>
		/// <returns><c>true</c> if the specifed list is the same as the one which is stored in the container; otherwise, <c>false</c>.</returns>
		/// <param name="List">Optimized list.</param>
		public bool IsListSameAsStored(OptimizedList List)
		{
			if (ContainsPackageList(List.PackageHash))
			{
				OptimizedList optimizedList = this.OptimizedLists[List.PackageHash];
				return optimizedList.ListHash.Equals(List.ListHash);
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
		/// <param name="List">List.</param>
		public void AddOptimizedList(OptimizedList List)
		{
			if (!this.OptimizedLists.ContainsKey(List.PackageHash))
			{
				this.OptimizedLists.Add(List.PackageHash, List);
			}
		}

		/// <summary>
		/// Replaces the optimized list stored in the container (under the same package hash) with the provided list.
		/// </summary>
		/// <param name="List">List.</param>
		public void ReplaceOptimizedList(OptimizedList List)
		{
			if (this.OptimizedLists.ContainsKey(List.PackageHash))
			{
				this.OptimizedLists[List.PackageHash] = List;
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
					foreach (char c in OptimizedListContainer.Signature)
					{
						bw.Write(c);
					}
					bw.Write(OptimizedListContainer.Version);
					bw.WriteNullTerminatedString(this.PackageName);
					bw.Write(this.OptimizedLists.Count);

					foreach (KeyValuePair<byte[], OptimizedList> ListPair in this.OptimizedLists)
					{
						bw.Write(ListPair.Value.GetBytes());
					}
				}

				return ms.ToArray();
			}
		}
	}

	public class ByteArrayComparer : IEqualityComparer<byte[]>
	{
		public bool Equals(byte[] left, byte[] right)
		{
			if (left == null || right == null)
			{
				return left == right;
			}

			return left.SequenceEqual(right);
		}

		public int GetHashCode(byte[] key)
		{
			if (key == null)
			{
				throw new ArgumentNullException("key");
			
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
		/// <param name="InPackageHash">In archive signature.</param>
		/// <param name="InOptimizedPaths">In optimized paths.</param>
		public OptimizedList(byte[] InPackageHash, List<string> InOptimizedPaths)
		{
			this.PackageHash = InPackageHash;
			this.OptimizedPaths = InOptimizedPaths;

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
					string dataSignature = new string(br.ReadChars(4));
					if (dataSignature != OptimizedList.Signature)
					{
						throw new InvalidDataException("The input data did not begin with a list signature.");
					}

					ulong BlockSize = br.ReadUInt64();

					// Read the MD5 archive signature.
					this.PackageHash = br.ReadBytes(this.PackageHash.Length);
					this.ListHash = br.ReadBytes(this.ListHash.Length);

					int compressedDataSize = (int)((long)(BlockSize - sizeof(ulong)) - (this.PackageHash.LongLength + this.ListHash.LongLength));
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
					foreach (char c in OptimizedList.Signature)
					{
						bw.Write(c);
					}

					byte[] compressedList = this.OptimizedPaths.Compress();

					ulong blockSize = sizeof(ulong) + (ulong)this.PackageHash.LongLength + (ulong)this.ListHash.LongLength + (ulong)compressedList.LongLength;
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

