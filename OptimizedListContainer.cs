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
		/// The name of the archive this container has lists for.
		/// </summary>
		public readonly string ArchiveName;

		/// <summary>
		/// The optimized lists contained in this container.
		/// </summary>
		public readonly List<OptimizedList> OptimizedLists = new List<OptimizedList>();

		/// <summary>
		/// Initializes a new instance of the <see cref="liblistfile.OptimizedListContainer"/> class.
		/// This constructor creates a new, empty list container.
		/// </summary>
		/// <param name="InArchiveName">In archive name.</param>
		public OptimizedListContainer(string InArchiveName)
		{
			this.ArchiveName = InArchiveName;
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

					this.ArchiveName = br.ReadNullTerminatedString();

					uint entryCount = br.ReadUInt32();
					for (int i = 0; i < entryCount; ++i)
					{
						OptimizedList optimizedList = new OptimizedList(br.ReadBytes((int)(PeekListBlockSize(br) + 4)));
						this.OptimizedLists.Add(optimizedList);
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
		/// Adds the optimized list to the container. If the list is already in the container, 
		/// it is not added.
		/// </summary>
		/// <param name="List">List.</param>
		public void AddOptimizedList(OptimizedList List)
		{
			if (!this.OptimizedLists.Contains(List))
			{
				this.OptimizedLists.Add(List);
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

					bw.WriteNullTerminatedString(this.ArchiveName);
					bw.Write(this.OptimizedLists.Count);

					foreach (OptimizedList List in this.OptimizedLists)
					{
						bw.Write(List.GetBytes());
					}
				}

				return ms.ToArray();
			}
		}
	}

	/// <summary>
	/// An optimized list of file paths.
	/// Each OptimizedList entry (when serialized) is structured as follows:
	/// 
	/// char[4]						: Signature (always LIST)
	/// uint64_t					: BlockSize (byte count, always CompressedData + 64)
	/// int512_t					: RSASignature (Weak signature of the archive that
	/// 							  this list is valid for). 64 bytes.
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
		/// A 512-bit RSA signature of the archive this list is made for.
		/// </summary>
		public readonly byte[] ArchiveSignature = new byte[64];

		/// <summary>
		/// The optimized paths contained in this list.
		/// </summary>
		public readonly List<string> OptimizedPaths = new List<string>();

		/// <summary>
		/// Initializes a new instance of the <see cref="liblistfile.OptimizedList"/> class.
		/// This constructor creates a new, empty OptimizedList.
		/// </summary>
		/// <param name="InArchiveSignature">In archive signature.</param>
		/// <param name="InOptimizedPaths">In optimized paths.</param>
		public OptimizedList(byte[] InArchiveSignature, List<string> InOptimizedPaths)
		{
			this.ArchiveSignature = InArchiveSignature;
			this.OptimizedPaths = InOptimizedPaths;
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

					// Read the RSA archive signature.
					this.ArchiveSignature = br.ReadBytes(this.ArchiveSignature.Length);

					using (MemoryStream compressedData = new MemoryStream(br.ReadBytes((int)(BlockSize - (ulong)this.ArchiveSignature.LongLength))))
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
									while (decompressedData.Position > decompressedData.Length)
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

					byte[] compressedList;
					if (this.OptimizedPaths.Count > 0)
					{
						// Compress the list so we can calculate the final block size
						using (MemoryStream om = new MemoryStream())
						{
							using (BZip2OutputStream bo = new BZip2OutputStream(om))
							{
								byte[] serializedList = this.OptimizedPaths.Serialize();
								bo.Write(serializedList, 0, serializedList.Length);
							}
							compressedList = om.ToArray();
						}
					}
					else
					{
						compressedList = new byte[0];
					}


					ulong listSize = (ulong)compressedList.LongLength;
					ulong blockSize = (ulong)this.ArchiveSignature.LongLength + listSize;
					bw.Write(blockSize);

					bw.Write(this.ArchiveSignature);
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
			return this.ArchiveSignature.Equals(other.ArchiveSignature) &&
			new HashSet<string>(this.OptimizedPaths).SetEquals(new HashSet<string>(other.OptimizedPaths));
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="liblistfile.OptimizedList"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return (this.ArchiveSignature.GetHashCode() + this.OptimizedPaths.GetHashCode()).GetHashCode();
		}

		#endregion
	}
}

