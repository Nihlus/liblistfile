//
//  MultiPackageNodeTreeBuilder.cs
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
using System.Linq;
using System.Threading.Tasks;
using Warcraft.Core;
using Warcraft.MPQ;
using Warcraft.MPQ.FileInfo;

namespace liblistfile.NodeTree
{
	/// <summary>
	/// The <see cref="MultiPackageNodeTreeBuilder"/> is similar to the <see cref="NodeTreeBuilder"/>, with the major
	/// difference that it is made to handle multiple packages in one tree. It creates subtrees for each package, and
	/// maps them to a common virtual node tree at the top level.
	/// </summary>
	public class MultiPackageNodeTreeBuilder : NodeTreeBuilder
	{
		private readonly Dictionary<NodeIdentifier, List<NodeIdentifier>> VirtualNodeHardNodes = new Dictionary<NodeIdentifier, List<NodeIdentifier>>();
		private readonly ListfileDictionary ListDictionary;
		private readonly bool OptimizeCasing;

		/// <summary>
		/// Creates a builder for a multi-package node tree.
		/// </summary>
		public MultiPackageNodeTreeBuilder() : base()
		{
			Node packagesFolderNode = new Node
			{
				Type = NodeType.Meta,
				NameOffset = -2,
				ChildCount = 0,
				ChildOffsets = new List<ulong>()
			};

			NodeIdentifier packagesFolderNodeIdentifier = new NodeIdentifier("", "Packages");
			NodeIdentifier rootNodeIdentifier = GetParentIdentifier(packagesFolderNodeIdentifier);

			// Register metanode information
			this.Nodes.Add(packagesFolderNodeIdentifier, packagesFolderNode);
			this.NodeParents.Add(packagesFolderNodeIdentifier, rootNodeIdentifier);
			this.NodeChildren.Add(packagesFolderNodeIdentifier, new List<NodeIdentifier>());

			// Update parent with information
			++this.Nodes[rootNodeIdentifier].ChildCount;
			this.NodeChildren[rootNodeIdentifier].Add(packagesFolderNodeIdentifier);

			if (!this.Names.Contains("Packages"))
			{
				this.Names.Add("Packages");
			}
		}

		public MultiPackageNodeTreeBuilder(ListfileDictionary dictionary) : this()
		{
			this.ListDictionary = dictionary;
			this.OptimizeCasing = true;
		}

		/// <summary>
		/// Consumes a package asynchronously, creating hard nodes for its contents, and mapping them to virtual nodes.
		/// </summary>
		/// <param name="packageName">The name of the package.</param>
		/// <param name="package">The package.</param>
		public async Task ConsumePackageAsync(string packageName, IPackage package)
		{
			await Task.Run(() => ConsumePackage(packageName, package));
		}

		/// <summary>
		/// Consumes a package, creating hard nodes for its contents, and mapping them to virtual nodes.
		/// </summary>
		/// <param name="packageName">The name of the package.</param>
		/// <param name="package">The package.</param>
		public void ConsumePackage(string packageName, IPackage package)
		{
			CreateMetaPackageNode(packageName);
			List<string> packagePaths = package.GetFileList();

			// We'll be progressively removing entries we're done with. Once they've all been consumed, we'll be done.
			while (packagePaths.Count > 0)
			{
				// Read the next path block.
				string pathBlockDirectory = PathUtilities.GetDirectoryName(packagePaths.First());

				// Create nodes for all directories up the chain
				IEnumerable<string> pathBlockChain = PathUtilities.GetDirectoryChain(pathBlockDirectory);
				foreach (string parentDirectory in pathBlockChain)
				{
					string path = parentDirectory;

					if (this.OptimizeCasing)
					{
						path = this.ListDictionary.OptimizePath(parentDirectory);
					}

					ConsumePath(packageName, package, path);
				}

				// Create file nodes for all files in that directory
				List<string> completedPaths = new List<string>();
				IEnumerable<string> pathBlockFiles = packagePaths.Where(p => PathUtilities.GetDirectoryName(p) == pathBlockDirectory);
				foreach (string blockFile in pathBlockFiles)
				{
					string path = blockFile;

					if (this.OptimizeCasing)
					{
						path = this.ListDictionary.OptimizePath(blockFile);
					}

					ConsumePath(packageName, package, path);
					completedPaths.Add(blockFile);
				}

				foreach (string completedPath in completedPaths)
				{
					packagePaths.Remove(completedPath);
				}
			}
		}

		/// <summary>
		/// Consumes a single path, creating a single node for it and updating the paired virtual node.
		/// </summary>
		/// <param name="packageName"></param>
		/// <param name="package"></param>
		/// <param name="path"></param>
		protected virtual void ConsumePath(string packageName, IPackage package, string path)
		{
			bool isDirectory = path.EndsWith("\\");
			string nodeName = PathUtilities.GetPathTargetName(path);

			// Each node is identified by its own name, and the name of its parent. Since we're mirroring a file
			// system here, duplicate names under one parent are not allowed, but they are allowed globally.
			// We'll acquire the identifiers for the new node and its parent for future use.
			NodeIdentifier nodeIdentifier = new NodeIdentifier(packageName, path);
			NodeIdentifier parentIdentifier = GetParentIdentifier(nodeIdentifier);

			// There's a good chance this node has already been encountered somewhere.
			// If that is the case, we can skip it.
			if (this.Nodes.ContainsKey(nodeIdentifier))
			{
				return;
			}

			// If the part is the final part, then it is almost guaranteed to be a file - if not, a directory.
			// Since listfiles do not support empty directories, we're not checking for extensions here. If
			// a part is last, then it is by definition a file.
			NodeType nodeType = isDirectory ? NodeType.Directory : NodeType.File;

			if (nodeType == NodeType.File)
			{
				MPQFileInfo fileInfo = package.GetFileInfo(path);
				if (fileInfo == null)
				{
					nodeType |= NodeType.Nonexistent;
				}
				else if (fileInfo.IsDeleted)
				{
					nodeType |= NodeType.Deleted;
				}
			}

			// We'll also store the type of file that's referenced for later use.
			WarcraftFileType fileType = nodeType == NodeType.Directory ? WarcraftFileType.Directory : FileInfoUtilities.GetFileType(nodeName);

			// -2 is used here to denote a missing but existing name that is to be filled in later.
			Node node = new Node
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
			if (!this.Names.Contains(nodeName))
			{
				this.Names.Add(nodeName);
			}

			// Now, we'll begin virtual node creation.
			CreateOrUpdateVirtualNode(nodeIdentifier);
		}

		/// <summary>
		/// Updates (and creates, if neccesary) the virtual node for the given node. This adds it as a hard node for
		/// that virtual node.
		/// </summary>
		/// <param name="nodeIdentifier"></param>
		protected void CreateOrUpdateVirtualNode(NodeIdentifier nodeIdentifier)
		{
			NodeIdentifier virtualNodeIdentifier = GetVirtualNodeIdentifier(nodeIdentifier);
			NodeIdentifier virtualParentIdentifier = GetParentIdentifier(virtualNodeIdentifier);

			NodeType hardType = this.Nodes[nodeIdentifier].Type;
			WarcraftFileType hardFileType = this.Nodes[nodeIdentifier].FileType;

			// Create a new virtual node if one does not exist
			if (!this.Nodes.ContainsKey(virtualNodeIdentifier))
			{
				Node virtualNode = new Node
				{
					Type = hardType | NodeType.Virtual,
					FileType = hardFileType,
					NameOffset = -2,
					ChildCount = 0,
					ChildOffsets = new List<ulong>(),
					HardNodeCount = 0,
					HardNodeOffsets = new List<ulong>()
				};

				this.Nodes.Add(virtualNodeIdentifier, virtualNode);
				this.NodeChildren.Add(virtualNodeIdentifier, new List<NodeIdentifier>());
				this.NodeParents.Add(virtualNodeIdentifier, virtualParentIdentifier);

				++this.Nodes[virtualParentIdentifier].ChildCount;
				this.NodeChildren[virtualParentIdentifier].Add(virtualNodeIdentifier);

				this.VirtualNodeHardNodes.Add(virtualNodeIdentifier, new List<NodeIdentifier>());
			}

			// Update the existing virtual node with new information
			this.Nodes[virtualNodeIdentifier].Type |= hardType;
			AddTypeToParentChain(hardFileType, virtualParentIdentifier);

			++this.Nodes[virtualNodeIdentifier].HardNodeCount;
			this.VirtualNodeHardNodes[virtualNodeIdentifier].Add(nodeIdentifier);
		}

		/// <summary>
		/// Creates a metanode for a package.
		/// </summary>
		/// <param name="packageName"></param>
		/// <returns></returns>
		protected void CreateMetaPackageNode(string packageName)
		{
			Node metaPackageNode = new Node
			{
				Type = NodeType.Meta | NodeType.Package,
				NameOffset = -3,
				ChildCount = 0,
				ChildOffsets = new List<ulong>()
			};

			NodeIdentifier metaPackageIdentifier = new NodeIdentifier(packageName, $"");
			NodeIdentifier packagesFolderIdentifier = new NodeIdentifier("", "Packages");

			this.Nodes.Add(metaPackageIdentifier, metaPackageNode);
			this.NodeParents.Add(metaPackageIdentifier, packagesFolderIdentifier);
			this.NodeChildren.Add(metaPackageIdentifier, new List<NodeIdentifier>());

			// Update parent with information
			++this.Nodes[packagesFolderIdentifier].ChildCount;
			this.NodeChildren[packagesFolderIdentifier].Add(metaPackageIdentifier);

			if (!this.Names.Contains(packageName))
			{
				this.Names.Add(packageName);
			}
		}

		public override void Build()
		{
			base.Build();

			// 4.4: Set virtual node information
			foreach (var node in this.Nodes)
			{
				// 4.2 Set hard node offsets
				if (node.Value.Type.HasFlag(NodeType.Virtual))
				{
					var hardNodeList = this.VirtualNodeHardNodes[node.Key];
					foreach (var hardIdentifier in hardNodeList)
					{
						node.Value.HardNodeOffsets.Add((ulong)this.AbsoluteNodeOffsets[hardIdentifier]);
					}
				}
			}
		}

		/// <summary>
		/// Gets the symmetrical virtual identifier for a given node identifier.
		/// </summary>
		/// <param name="nodeIdentifier"></param>
		/// <returns></returns>
		protected static NodeIdentifier GetVirtualNodeIdentifier(NodeIdentifier nodeIdentifier)
		{
			return new NodeIdentifier("", nodeIdentifier.Path);
		}

		protected override void ConsumePath(string path)
		{
			throw new InvalidOperationException("Individual paths may not be consumed when generating a multipackage tree. Use ConsumePackage(string, IPackage) instead.");
		}
	}
}