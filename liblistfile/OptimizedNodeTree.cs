﻿//
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
using System.IO;
using liblistfile.NodeTree;
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

				this.InternalRoot = Node.ReadNode(this.TreeReader, 0);
				return this.InternalRoot;
			}
		}

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
				this.TreeStream.CopyTo(ms);

				return ms.ToArray();
			}
		}
	}
}