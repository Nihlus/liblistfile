//
//  OptimizedNodeTree.cs
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
using System.Linq;
using liblistfile.NodeTree;
using Warcraft.Core.Extensions;
using Warcraft.Core.Interfaces;

namespace liblistfile
{
	/// <summary>
	/// An optimized tree of object nodes.
	/// Each <see cref="OptimizedNodeTree"/> (when serialized) is structured as follows:
	///
	/// Node RootNode
	/// Node[] Nodes
	/// char[] Names
	///
	/// </summary>
	public class OptimizedNodeTree : IDisposable, IBinarySerializable
	{
		public const uint Version = 1;
		private long NodesOffset;
		private long NamesOffset;
		private long SortListsOffset;

		/// <summary>
		/// A stream containing the data of the tree.
		/// </summary>
		private readonly Stream TreeStream;

		/// <summary>
		/// A <see cref="BinaryReader"/> bound to the data contained in the tree.
		/// </summary>
		public readonly BinaryReader TreeReader;

		private Node InternalRoot;
		public Node Root
		{
			get
			{
				if (this.InternalRoot != null)
				{
					return this.InternalRoot;
				}

				this.InternalRoot = Node.ReadNode(this.TreeReader, (ulong)this.NodesOffset);
				return this.InternalRoot;
			}
		}

		private readonly Dictionary<ulong, Node> CachedNodes = new Dictionary<ulong, Node>();
		private readonly Dictionary<Node, ulong> CachedOffsets = new Dictionary<Node, ulong>();

		/// <summary>
		/// Creates a new <see cref="OptimizedNodeTree"/> from a data stream.
		/// </summary>
		/// <param name="dataStream"></param>
		/// <exception cref="ArgumentException"></exception>
		public OptimizedNodeTree(Stream dataStream)
		{
			if (!dataStream.CanSeek)
			{
				throw new ArgumentException("Unseekable streams are not supported.", nameof(dataStream));
			}

			this.TreeStream = dataStream;
			this.TreeReader = new BinaryReader(this.TreeStream);

			// Reset the position, in case the stream was recently created.
			this.TreeReader.BaseStream.Position = 0;

			uint storedVersion = this.TreeReader.ReadUInt32();
			if (storedVersion != Version)
			{
				// Do whatever functionality switching is needed
			}

			// Latest implementation
			this.NodesOffset = this.TreeReader.ReadInt64();
			this.NamesOffset = this.TreeReader.ReadInt64();
			this.SortListsOffset = this.TreeReader.ReadInt64();
		}

		/// <summary>
		/// Gets a node from the specified offset in the tree.
		/// </summary>
		/// <param name="offset"></param>
		/// <returns></returns>
		public Node GetNode(ulong offset)
		{
			if (offset < (ulong)this.NodesOffset)
			{
				return null;
			}

			if (this.CachedNodes.ContainsKey(offset))
			{
				return this.CachedNodes[offset];
			}

			Node newNode = Node.ReadNode(this.TreeReader, offset);
			if (newNode == null)
			{
				return null;
			}

			this.CachedNodes.Add(offset, newNode);
			this.CachedOffsets.Add(newNode, offset);
			return newNode;
		}

		/// <summary>
		/// Gets the absolute offset of a given node.
		/// </summary>
		/// <param name="node"></param>
		/// <returns></returns>
		public ulong GetNodeOffset(Node node)
		{
			if (this.CachedOffsets.ContainsKey(node))
			{
				return this.CachedOffsets[node];
			}

			return 0;
		}

		public string GetNodeName(Node node)
		{
			if (node.NameOffset < 0)
			{
				return string.Empty;
			}

			this.TreeReader.BaseStream.Position = node.NameOffset;
			return this.TreeReader.ReadNullTerminatedString();
		}

		/// <summary>
		/// Disposes the node tree, releasing the underlying data.
		/// </summary>
		public void Dispose()
		{
			this.TreeStream?.Dispose();
			this.TreeReader?.Dispose();
		}

		/// <summary>
		/// Serializes the current object into a byte array.
		/// </summary>
		public byte[] Serialize()
		{
			using (MemoryStream ms = new MemoryStream())
			{
				this.TreeStream.Position = 0;
				this.TreeStream.CopyTo(ms);

				return ms.ToArray();
			}
		}
	}
}