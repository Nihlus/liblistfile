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

using System.Collections.Generic;
using System.IO;

namespace liblistfile.NodeTree
{
	/// <summary>
	/// Represents a node in a file tree. A node can be a directory or a file, and can have any number of child nodes.
	///
	/// Typically, file nodes do not have any children, although it is not explicitly disallowed.
	/// </summary>
	public class Node
	{
		/// <summary>
		/// The type of the node.
		/// </summary>
		public NodeType Type;

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
		/// Reads a new node from the specified <see cref="BinaryReader"/> at the specified position.
		/// </summary>
		/// <param name="br"></param>
		/// <param name="position"></param>
		/// <returns></returns>
		public static Node ReadNode(BinaryReader br, long position)
		{
			br.BaseStream.Position = position;

			Node outNode = new Node
			{
				Type = (NodeType) br.ReadUInt32(),
				NameOffset = br.ReadInt64(),
				ParentOffset = br.ReadInt64(),
				ChildCount = br.ReadUInt64(),
				ChildOffsets = new List<ulong>()
			};

			for (ulong i = 0; i < outNode.ChildCount; ++i)
			{
				outNode.ChildOffsets.Add(br.ReadUInt64());
			}

			return outNode;
		}
	}
}