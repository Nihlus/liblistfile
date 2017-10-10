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
using liblistfile.Exceptions;
using liblistfile.NodeTree;
using Warcraft.Core.Extensions;

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
	public class OptimizedNodeTree : IDisposable
	{
		/// <summary>
		/// The current version of the node tree format.
		/// </summary>
		public const uint Version = 2;

		private readonly long NodesOffset;
		private readonly long NamesOffset;
		private readonly long SortListsOffset;

		private readonly string TreeLocation;

		private readonly object ReaderLock = new object();
		private readonly BinaryReader TreeReader;

		/// <summary>
		/// The absolute root node of the tree.
		/// </summary>
		public Node Root
		{
			get
			{
				if (this.InternalRoot != null)
				{
					return this.InternalRoot;
				}

				this.InternalRoot = GetNode((ulong)this.NodesOffset);
				return this.InternalRoot;
			}
		}
		private Node InternalRoot;

		private readonly Dictionary<ulong, Node> CachedNodes = new Dictionary<ulong, Node>();
		private readonly Dictionary<Node, ulong> CachedOffsets = new Dictionary<Node, ulong>();

		/// <summary>
		/// Creates a new <see cref="OptimizedNodeTree"/> from a data stream.
		/// </summary>
		/// <param name="treeLocation"></param>
		/// <exception cref="ArgumentException"></exception>
		public OptimizedNodeTree(string treeLocation)
		{
			if (!File.Exists(treeLocation))
			{
				throw new NodeTreeNotFoundException();
			}

			this.TreeLocation = treeLocation;

			var treeStream = File.Open(this.TreeLocation, FileMode.Open, FileAccess.Read, FileShare.Read);
			this.TreeReader = new BinaryReader(treeStream);

			uint storedVersion = this.TreeReader.ReadUInt32();
			if (storedVersion != Version)
			{
				// Do whatever functionality switching is needed
				throw new UnsupportedNodeTreeVersionException();
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

			// Nodes may be read from multiple threads at any time due to async/await patterns, so we
			// lock the reader

			lock (this.ReaderLock)
			{
				Node newNode = Node.ReadNode(this.TreeReader, offset);

				if (newNode == null)
				{
					return null;
				}

				this.CachedNodes.Add(offset, newNode);
				this.CachedOffsets.Add(newNode, offset);
				return newNode;
			}
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

		/// <summary>
		/// Gets the name of a given node.
		/// </summary>
		/// <param name="node"></param>
		/// <returns></returns>
		public string GetNodeName(Node node)
		{
			if (node.NameOffset < 0)
			{
				return string.Empty;
			}

			lock (this.ReaderLock)
			{
				this.TreeReader.BaseStream.Position = node.NameOffset;
				return this.TreeReader.ReadNullTerminatedString();
			}
		}

		/// <inheritdoc />
		public void Dispose()
		{
			this.TreeReader?.Dispose();
		}
	}
}