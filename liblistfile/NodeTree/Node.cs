//
//  Node.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2017 Jarl Gullberg
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
using Warcraft.Core.Interfaces;

namespace liblistfile.NodeTree
{
	/// <summary>
	/// Represents a node in a file tree. A node can be a directory or a file, and can have any number of child nodes.
	/// Furthermore, a node can be virtual, and be a "supernode" for other nodes. While not strictly enforced, it is
	/// expected that these subnodes have the same paths and names as the virtual node, and only differ in that they
	/// reside inside packages, while the virtual node is in a top-level tree.
	///
	/// Whether or not a node is virtual depends on whether or not the flag <see cref="NodeType.Virtual"/> is set in
	/// <see cref="Type"/>.
	///
	/// Typically, file nodes do not have any children, although it is not explicitly disallowed.
	/// </summary>
	public class Node : IBinarySerializable
	{
		/// <summary>
		/// The type of the node.
		/// </summary>
		public NodeType Type;

		/// <summary>
		/// The type of the file or directory pointed to by the node.
		/// </summary>
		public WarcraftFileType FileType;

		/// <summary>
		/// The absolute offset where the name of this node is found. A negative value denotes no name.
		/// </summary>
		public long NameOffset;

		/// <summary>
		/// The absolute offset to the parent node of this node. A negative value denotes no parent, and is reserved for
		/// the root node.
		/// </summary>
		public long ParentOffset;

		/// <summary>
		/// The number of child nodes this node has.
		/// </summary>
		public ulong ChildCount;

		/// <summary>
		/// A list of absolute offsets to where the children of this node can be found. This is comes in no particular
		/// enforced order - it is up to the consuming software to order them as neccesary.
		/// </summary>
		public List<ulong> ChildOffsets = new List<ulong>();

		/// <summary>
		/// The number of hard nodes that this node has. This is only > 0 if <see cref="Type"/> is flagged
		/// as <see cref="NodeType.Virtual"/>.
		/// </summary>
		public ulong HardNodeCount;

		/// <summary>
		/// A list of absolute offsets to where the hard nodes of this virtual node can be found. This only contains
		/// data if <see cref="Type"/> is flagged as <see cref="NodeType.Virtual"/>.
		/// </summary>
		public List<ulong> HardNodeOffsets = new List<ulong>();

		/// <summary>
		/// Reads a new node from the specified <see cref="BinaryReader"/> at the specified position.
		/// </summary>
		/// <param name="br"></param>
		/// <param name="position"></param>
		/// <returns></returns>
		public static Node ReadNode(BinaryReader br, ulong position)
		{
			br.BaseStream.Seek((long) position, SeekOrigin.Begin);

			var outNode = new Node
			{
				Type = (NodeType) br.ReadUInt32(),
				FileType = (WarcraftFileType) br.ReadUInt64(),
				NameOffset = br.ReadInt64(),
				ParentOffset = br.ReadInt64(),
				ChildCount = br.ReadUInt64(),
				ChildOffsets = new List<ulong>()
			};

			for (ulong i = 0; i < outNode.ChildCount; ++i)
			{
				outNode.ChildOffsets.Add(br.ReadUInt64());
			}

			if (outNode.Type.HasFlag(NodeType.Virtual))
			{
				outNode.HardNodeCount = br.ReadUInt64();
				for (ulong i = 0; i < outNode.HardNodeCount; ++i)
				{
					outNode.HardNodeOffsets.Add(br.ReadUInt64());
				}
			}

			return outNode;
		}

		/// <summary>
		/// Determines whether or not this node has children.
		/// </summary>
		/// <returns></returns>
		public bool HasChildren()
		{
			return this.ChildCount > 0;
		}

		/// <summary>
		/// Gets the total size of the node in serialized form.
		/// </summary>
		/// <returns>The number of bytes this node would occupy.</returns>
		public long GetTotalSize()
		{
			// Type, FileType, NameOffset, ParentOffset, ChildCount
			ulong size = sizeof(uint) + sizeof(ulong) + (sizeof(long) * 2) + sizeof(ulong);

			// ChildOffsets
			size += (this.ChildCount * sizeof(ulong));

			if (this.Type.HasFlag(NodeType.Virtual))
			{
				// HardNodeCount
				size += sizeof(ulong);

				// HardNodeOffsets
				size += this.HardNodeCount * sizeof(ulong);
			}

			return (long) size;
		}

		/// <summary>
		/// Serializes the current object into a byte array.
		/// </summary>
		public byte[] Serialize()
		{
			using (var ms = new MemoryStream())
			using (var bw = new BinaryWriter(ms))
			{
				bw.Write((uint)this.Type);
				bw.Write((ulong)this.FileType);
				bw.Write(this.NameOffset);
				bw.Write(this.ParentOffset);

				bw.Write(this.ChildCount);
				foreach (var childOffset in this.ChildOffsets)
				{
					bw.Write(childOffset);
				}

				if (this.Type.HasFlag(NodeType.Virtual))
				{
					bw.Write(this.HardNodeCount);
					foreach (var hardNodeOffset in this.HardNodeOffsets)
					{
						bw.Write(hardNodeOffset);
					}
				}

				return ms.ToArray();
			}
		}

		/// <summary>
		/// Determines whether or not a node is equal to another.
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
		{
			if (!(obj is Node))
			{
				return false;
			}

			var other = (Node) obj;

			return
				this.Type == other.Type &&
				this.FileType == other.FileType &&
				this.NameOffset == other.NameOffset &&
				this.ParentOffset == other.ParentOffset &&
				this.ChildCount == other.ChildCount &&
				this.HardNodeCount == other.HardNodeCount;
		}

		/// <summary>
		/// Gets the hash code of this node.
		/// </summary>
		/// <returns></returns>
		public override int GetHashCode()
		{
			unchecked
			{
				var hash = 17;

				hash *= 23 + (int)this.Type;
				hash *= 23 + (int) this.FileType;
				hash *= 23 + (int)this.NameOffset;
				hash *= 23 + (int)this.ChildCount;
				hash *= 23 + (int)this.HardNodeCount;

				return hash;
			}
		}
	}
}