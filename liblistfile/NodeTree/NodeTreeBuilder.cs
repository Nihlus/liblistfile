//
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

		private readonly Node RootNode;

		private readonly List<string> Names = new List<string>();
		private readonly Dictionary<Tuple<string, string>, Node> Nodes = new Dictionary<Tuple<string, string>, Node>();
		private readonly Dictionary<Node, List<Node>> NodeChildren = new Dictionary<Node, List<Node>>();
		private readonly Dictionary<Node, Node> NodeParents = new Dictionary<Node, Node>();

		/*
			Internal building register data
		*/
		private readonly Dictionary<string, long> AbsoluteNameOffsets = new Dictionary<string, long>();
		private readonly Dictionary<Node, long> AbsoluteNodeOffsets = new Dictionary<Node, long>();
		private long NodesOffset;
		private long NameBlockOffset;
		private long SortingListsOffset;

		public NodeTreeBuilder()
		{
			// Form a root node. -1 is used for the NameOffset to denote a nonexistent name.
			this.RootNode = new Node
			{
				Type = NodeType.Directory,
				NameOffset = -1,
				ChildCount = 0,
				ChildOffsets = new List<ulong>()
			};
			this.Nodes.Add(new Tuple<string, string>("", ""), this.RootNode);
			this.NodeChildren[this.RootNode] = new List<Node>();
		}

		public NodeTreeBuilder(IEnumerable<string> paths) : this()
		{
			// Begin forming nodes for the given paths.
			foreach (string path in paths)
			{
				ConsumePath(path);
			}
		}

		public void AddPath(string path)
		{
			ConsumePath(path);
		}

		/// <summary>
		/// Creates an identifier for a node, given the path it is a part of, and the part which belongs to the node.
		/// </summary>
		/// <param name="pathParts">The path, split into an array of constituent parts.</param>
		/// <param name="part">The part of the path which belongs to the node.</param>
		/// <returns>A new tuple which uniquely identifies a node.</returns>
		private static Tuple<string, string> CreateNodeIdentifier(string[] pathParts, string part)
		{
			int partIndex = Array.IndexOf(pathParts, part);
			if (partIndex > 0)
			{
				return new Tuple<string, string>(pathParts[partIndex - 1], part);
			}

			return new Tuple<string, string>("", part);
		}

		/// <summary>
		/// Computes the identifier of the parent node for a node, given the path it is a part of, and the part
		/// which belongs to the node.
		/// </summary>
		/// <param name="pathParts">The path, split into an array of constituent parts.</param>
		/// <param name="part">The part of the path which belongs to the node.</param>
		/// <returns>A new tuple which uniquely identifies a node.</returns>
		private static Tuple<string, string> GetParentIdentifier(string[] pathParts, string part)
		{
			int partIndex = Array.IndexOf(pathParts, part);
			if (partIndex >= 2)
			{
				return new Tuple<string, string>(pathParts[partIndex - 2], pathParts[partIndex - 1]);
			}

			if (partIndex == 1)
			{
				return new Tuple<string, string>("", pathParts[partIndex - 1]);
			}

			return new Tuple<string, string>("", "");
		}

		/// <summary>
		/// Consumes a given path, creating nodes for its components and adding them (and any relevant data) to the
		/// internal registers.
		/// </summary>
		/// <param name="path">
		/// The path to consume. Both \ and / characters are accepted, and will be converted to /
		/// internally. The path is split into components by this character.
		/// </param>
		private void ConsumePath(string path)
		{
			// Replace any instances of windows path separators with unix path separators to ensure that no matter
			// what platform we're doing this on, the splitting will be the same.
			string cleanPath = path.Replace('\\', '/');

			// Split the path into the composing names
			string[] pathParts = cleanPath.Split('/');

			// We'll try to create nodes for each part.
			for (int i = 0; i < pathParts.Length; ++i)
			{
				bool isPartLastInPath = i == pathParts.Length - 1;

				// Each node is identified by its own name, and the name of its parent. Since we're mirroring a file
				// system here, duplicate names under one parent are not allowed, but they are allowed globally.
				// We'll acquire the identifiers for the new node and its parent for future use.
				Tuple<string, string> parentIdentifier = GetParentIdentifier(pathParts, pathParts[i]);
				Tuple<string, string> nodeIdentifier = CreateNodeIdentifier(pathParts, pathParts[i]);

				// There's a good chance this node has already been encountered somewhere.
				// If that is the case, we can skip it.
				if (this.Nodes.ContainsKey(nodeIdentifier))
				{
					continue;
				}

				// If the part is the final part, then it is almost guaranteed to be a file - if not, a directory.
				// Since listfiles do not support empty directories, we're not checking for extensions here. If
				// a part is last, then it is by definition a file.
				NodeType nodeType = isPartLastInPath ? NodeType.File : NodeType.Directory;

				// -2 is used here to denote a missing but existing name that is to be filled in later.
				Node node = new Node
				{
					Type = nodeType,
					NameOffset = -2,
					ChildCount = 0,
					ChildOffsets = new List<ulong>()
				};

				// Enter the created data into the transient registers
				this.Nodes.Add(nodeIdentifier, node);
				this.NodeChildren[node] = new List<Node>();
				this.NodeParents.Add(node, this.Nodes[parentIdentifier]);

				// Update the parent node with the new information
				++this.Nodes[parentIdentifier].ChildCount;
				this.NodeChildren[this.Nodes[parentIdentifier]].Add(node);

				// Append the node name (if it doesn't already exist) to the name list so we can build the name
				// block later
				if (!this.Names.Contains(pathParts[i]))
				{
					this.Names.Add(pathParts[i]);
				}
			}
		}

		/// <summary>
		/// Build an <see cref="OptimizedNodeTree"/> object from the consumed paths. This method can be called as many
		/// times as needed, if more paths are added.
		/// </summary>
		/// <returns></returns>
		/// <exception cref="NotImplementedException"></exception>
		public OptimizedNodeTree Build()
		{
			// 1: Buld nodes (done in ConsumePath)
			// 2: Build name block

			Dictionary<string, long> relativeNameOffsets = new Dictionary<string, long>();
			byte[] nameBlock;
			using (MemoryStream ms = new MemoryStream())
			using (BinaryWriter bw = new BinaryWriter(ms))
			{
				int currentNameBlockOffset = 0;
				foreach (string name in this.Names)
				{
					relativeNameOffsets.Add(name, currentNameBlockOffset);

					int storedNameLength = name.Length + 1;
					bw.WriteNullTerminatedString(name);

					currentNameBlockOffset += storedNameLength;
				}

				nameBlock = ms.ToArray();
			}

			// 3: Build layout
				// 3.1: Order is header, root, nodes, name, sorting lists
			long currentLayoutOffset = 0;
			currentLayoutOffset += HeaderSize;

			// Save the node block offset
			this.NodesOffset = currentLayoutOffset;

			// Calculate the offsets for all of the nodes
			this.AbsoluteNodeOffsets.Add(this.RootNode, currentLayoutOffset);
			currentLayoutOffset += this.RootNode.GetTotalSize();

			foreach (Node node in this.Nodes.Values)
			{
				this.AbsoluteNodeOffsets.Add(node, currentLayoutOffset);
				currentLayoutOffset += node.GetTotalSize();
			}

			// Calculate absolute offsets for the names in the name block
			this.NameBlockOffset = currentLayoutOffset;
			foreach (KeyValuePair<string, long> relativeNameOffset in relativeNameOffsets)
			{
				this.AbsoluteNameOffsets.Add(relativeNameOffset.Key, relativeNameOffset.Value + currentLayoutOffset);
			}

			currentLayoutOffset += nameBlock.Length;

			// Layout of the sorting list block
			this.SortingListsOffset = currentLayoutOffset;

			// 4: Set known values
			// 5: Write data to stream, create object and return.

			throw new NotImplementedException();
		}
	}
}