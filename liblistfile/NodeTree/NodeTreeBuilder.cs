﻿//
//  NodeTreeBuilder.cs
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
using System.Text;
using System.Text.RegularExpressions;
using Warcraft.Core;
using Warcraft.Core.Extensions;

namespace liblistfile.NodeTree
{
	/// <summary>
	/// Builds a new <see cref="OptimizedNodeTree"/> from a list of paths.
	/// </summary>
	public class NodeTreeBuilder
	{
		/*
			Common constants
		*/

		private const long HeaderSize = sizeof(uint) + sizeof(long) * 3;

		/*
			Internal transient register data
		*/

		/// <summary>
		/// The root node of the tree that's being built.
		/// </summary>
		protected readonly Node RootNode;

		/// <summary>
		/// A list of all node names which appear in the tree.
		/// </summary>
		protected readonly List<string> Names = new List<string>();

		/// <summary>
		/// A mapping between folder-unique <see cref="NodeIdentifier"/> objects and nodes.
		/// </summary>
		protected readonly Dictionary<NodeIdentifier, Node> Nodes = new Dictionary<NodeIdentifier, Node>();

		/// <summary>
		/// A mapping between folder-unique <see cref="NodeIdentifier"/> objects and lists of which identifiers are
		/// child nodes to that identifier.
		/// </summary>
		protected readonly Dictionary<NodeIdentifier, List<NodeIdentifier>> NodeChildren = new Dictionary<NodeIdentifier, List<NodeIdentifier>>();

		/// <summary>
		/// A mapping between folder-unique <see cref="NodeIdentifier"/> objects and identifiers which are their parent
		/// identifiers.
		/// </summary>
		protected readonly Dictionary<NodeIdentifier, NodeIdentifier> NodeParents = new Dictionary<NodeIdentifier, NodeIdentifier>();

		/*
			Internal building register data
		*/

		/// <summary>
		/// A mapping of names to their final absolute offsets.
		/// </summary>
		protected readonly Dictionary<string, long> AbsoluteNameOffsets = new Dictionary<string, long>();

		/// <summary>
		/// A mapping of <see cref="NodeIdentifier"/> objects to the final absolute offsets of their respective nodes.
		/// </summary>
		protected readonly Dictionary<NodeIdentifier, long> AbsoluteNodeOffsets = new Dictionary<NodeIdentifier, long>();

		private byte[] NameBlock;

		private long NodeBlockOffset;
		private long NameBlockOffset;
		private long SortingBlockOffset;

		/// <summary>
		/// Initializes a new instance of the <see cref="NodeTreeBuilder"/> class.
		/// </summary>
		public NodeTreeBuilder()
		{
			var rootIdentifier = new NodeIdentifier("", "");

			// Form a root node. -1 is used for the NameOffset to denote a nonexistent name.
			this.RootNode = new Node
			{
				Type = NodeType.Directory | NodeType.Meta,
				NameOffset = -1,
				ParentOffset = -1,
				ChildCount = 0,
				ChildOffsets = new List<ulong>()
			};

			this.Nodes.Add(rootIdentifier, this.RootNode);
			this.NodeChildren[rootIdentifier] = new List<NodeIdentifier>();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NodeTreeBuilder"/> class and consumes
		/// all paths provided.
		/// </summary>
		/// <param name="paths"></param>
		public NodeTreeBuilder(IEnumerable<string> paths) : this()
		{
			// Begin forming nodes for the given paths.
			foreach (var path in paths)
			{
				ConsumePath(path);
			}
		}

		/// <summary>
		/// Adds a path to the tree.
		/// </summary>
		/// <param name="path"></param>
		public void AddPath(string path)
		{
			ConsumePath(path);
		}

		/// <summary>
		/// Computes the identifier of the parent node for a node, given the path it is a part of, and the part
		/// which belongs to the node.
		/// </summary>
		/// <param name="nodeIdentifier">The node identifier to compute the parent for.</param>
		/// <returns>A new object which uniquely identifies a node.</returns>
		protected static NodeIdentifier GetParentIdentifier(NodeIdentifier nodeIdentifier)
		{
			if (nodeIdentifier == null)
			{
				throw new ArgumentNullException(nameof(nodeIdentifier));
			}

			return new NodeIdentifier(nodeIdentifier.Package, PathUtilities.GetDirectoryName(nodeIdentifier.Path.TrimEnd('\\')));
		}

		/// <summary>
		/// Consumes a given path, creating nodes for its components and adding them (and any relevant data) to the
		/// internal registers.
		/// </summary>
		/// <param name="path">
		/// The path to consume. Both \ and / characters are accepted, and will be converted to \
		/// internally. The path is split into components by this character.
		/// </param>
		protected virtual void ConsumePath(string path)
		{
			// Replace any instances of windows path separators with unix path separators to ensure that no matter
			// what platform we're doing this on, the splitting will be the same.
			var cleanPath = path.Replace('/', '\\');

			// Split the path into the composing names
			var pathParts = cleanPath.Split('\\');

			// We'll try to create nodes for each part.
			for (var i = 0; i < pathParts.Length; ++i)
			{
				var isPartLastInPath = i == pathParts.Length - 1;

				// Each node is identified by its own name, and the name of its parent. Since we're mirroring a file
				// system here, duplicate names under one parent are not allowed, but they are allowed globally.
				// We'll acquire the identifiers for the new node and its parent for future use.
				var nodeIdentifier = new NodeIdentifier("", string.Join("\\", pathParts.Take(i + 1)));
				var parentIdentifier = GetParentIdentifier(nodeIdentifier);

				// There's a good chance this node has already been encountered somewhere.
				// If that is the case, we can skip it.
				if (this.Nodes.ContainsKey(nodeIdentifier))
				{
					continue;
				}

				// If the part is the final part, then it is almost guaranteed to be a file - if not, a directory.
				// Since listfiles do not support empty directories, we're not checking for extensions here. If
				// a part is last, then it is by definition a file.
				var nodeType = isPartLastInPath ? NodeType.File : NodeType.Directory;

				// We'll also store the type of file that's referenced for later use.
				var fileType = nodeType == NodeType.Directory ? WarcraftFileType.Directory : FileInfoUtilities.GetFileType(pathParts[i]);

				// -2 is used here to denote a missing but existing name that is to be filled in later.
				var node = new Node
				{
					Type = nodeType,
					FileType = fileType,
					NameOffset = -2,
					ChildCount = 0,
					ChildOffsets = new List<ulong>()
				};

				// Enter the created data into the transient registers
				this.Nodes.Add(nodeIdentifier, node);
				this.NodeChildren.Add(nodeIdentifier, new List<NodeIdentifier>());
				this.NodeParents.Add(nodeIdentifier, parentIdentifier);

				// Update the parent node with the new information
				++this.Nodes[parentIdentifier].ChildCount;
				this.NodeChildren[parentIdentifier].Add(nodeIdentifier);
				AddTypeToParentChain(fileType, parentIdentifier);

				// Append the node name (if it doesn't already exist) to the name list so we can build the name
				// block later
				if (!this.Names.Contains(pathParts[i]))
				{
					this.Names.Add(pathParts[i]);
				}
			}
		}

		/// <summary>
		/// Adds a contained file type to a chain of parent directories (upwards).
		/// </summary>
		/// <param name="fileType">The type to add to the chain.</param>
		/// <param name="initialParentIdentifier">The start of the chain.</param>
		protected void AddTypeToParentChain(WarcraftFileType fileType, NodeIdentifier initialParentIdentifier)
		{
			var currentParentIdentifier = initialParentIdentifier;

			var hasParent = true;
			while (hasParent)
			{
				var currentNode = this.Nodes[currentParentIdentifier];
				if (currentNode.FileType.HasFlag(fileType))
				{
					// Early escape - if a parent is already tagged with the file type, then all
					// parents above it are also tagged.
					return;
				}

				currentNode.FileType |= fileType;

				if (currentNode.ParentOffset != -1)
				{
					currentParentIdentifier = this.NodeParents[currentParentIdentifier];
				}
				else
				{
					// We've reached the top of the chain, so add the type to the root node.
					this.RootNode.FileType |= fileType;

					hasParent = false;
				}
			}
		}

		/// <summary>
		/// Builds the gathered data into data ready to be written into a tree object from the consumed paths.
		/// This method can be called as many times as needed, if more paths are added.
		/// </summary>
		/// <returns></returns>
		public virtual void Build()
		{
			// 1: Buld nodes (done in CreateNode)
			// 2: Build name block

			var relativeNameOffsets = new Dictionary<string, long>();
			using (var ms = new MemoryStream())
			using (var bw = new BinaryWriter(ms))
			{
				var currentNameBlockOffset = 0;
				foreach (var name in this.Names)
				{
					relativeNameOffsets.Add(name, currentNameBlockOffset);

					var storedNameLength = name.Length + 1;
					bw.WriteNullTerminatedString(name);

					currentNameBlockOffset += storedNameLength;
				}

				this.NameBlock = ms.ToArray();
			}

			// 3: Build layout
				// 3.1: Order is header, root, nodes, name, sorting lists
			long currentLayoutOffset = 0;
			currentLayoutOffset += HeaderSize;

			// Save the node block offset
			this.NodeBlockOffset = currentLayoutOffset;

			// Calculate the offsets for all of the nodes
			foreach (var node in this.Nodes)
			{
				this.AbsoluteNodeOffsets.Add(node.Key, currentLayoutOffset);
				currentLayoutOffset += node.Value.GetTotalSize();
			}

			// Calculate absolute offsets for the names in the name block
			this.NameBlockOffset = currentLayoutOffset;
			foreach (var relativeNameOffset in relativeNameOffsets)
			{
				this.AbsoluteNameOffsets.Add(relativeNameOffset.Key, relativeNameOffset.Value + currentLayoutOffset);
			}

			currentLayoutOffset += this.NameBlock.Length;

			// Layout of the sorting list block
			this.SortingBlockOffset = currentLayoutOffset;

			// 4: Set known values
			foreach (var node in this.Nodes)
			{
				// 4.1 Set name offsets
				// Skip the root node, it has no name
				if (node.Value.NameOffset == -2)
				{
					var nodeName = node.Key.GetNodeName();
					var nameOffset = this.AbsoluteNameOffsets[nodeName];

					node.Value.NameOffset = nameOffset;
				}

				if (node.Value.NameOffset == -3)
				{
					// Use the package name as the node name instead
					var nodeName = node.Key.Package;
					var nameOffset = this.AbsoluteNameOffsets[nodeName];

					node.Value.NameOffset = nameOffset;
				}

				// 4.2 Set child offsets
				var childList = this.NodeChildren[node.Key];
				foreach (var childIdentifier in childList)
				{
					node.Value.ChildOffsets.Add((ulong)this.AbsoluteNodeOffsets[childIdentifier]);
				}

				// 4.3 Set parent offsets
				// Skip the root node, it has no parent
				if (node.Value.ParentOffset == -1)
				{
					continue;
				}
				node.Value.ParentOffset = this.AbsoluteNodeOffsets[this.NodeParents[node.Key]];
			}
		}

		/// <summary>
		/// Takes the finalized data in the builder (created with <see cref="Build"/>), writes it to a stream, and
		/// forms a tree from it.
		/// </summary>
		/// <returns></returns>
		public byte[] CreateTree()
		{
			var outputStream = new MemoryStream();
			using (var bw = new BinaryWriter(outputStream, Encoding.Default, true))
			{
				// 5.1 Write header
				bw.Write(OptimizedNodeTree.Version);
				bw.Write(this.NodeBlockOffset);
				bw.Write(this.NameBlockOffset);
				bw.Write(this.SortingBlockOffset);

				// 5.2 Write nodes
				foreach (var node in this.Nodes.Values)
				{
					bw.Write(node.Serialize());
				}

				// 5.3 Write name block
				bw.Write(this.NameBlock);

				// 5.4 Write sorting block (unused at the moment)

				bw.Flush();
				outputStream.Flush();
			}

			return outputStream.ToArray();
		}
	}
}