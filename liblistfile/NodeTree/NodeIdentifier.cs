//
//  NodeIdentifier.cs
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
using System.Linq;

namespace liblistfile.NodeTree
{
	/// <summary>
	/// The <see cref="NodeIdentifier"/> class acts as a uniquely identifiable tag for a node while constructing
	/// a node tree. It keeps track of the package the node belongs to, and which path the node has inside that package.
	/// </summary>
	public class NodeIdentifier : IEquatable<NodeIdentifier>
	{
		/// <summary>
		/// The package the node belongs to.
		/// </summary>
		public readonly string Package;

		/// <summary>
		/// The path of the node inside that package.
		/// </summary>
		public readonly string Path;

		/// <summary>
		/// Creates a new <see cref="NodeIdentifier"/> from a package and a path.
		/// </summary>
		/// <param name="inPackage"></param>
		/// <param name="inPath"></param>
		public NodeIdentifier(string inPackage, string inPath)
		{
			this.Package = inPackage;
			this.Path = inPath;
		}

		/// <summary>
		/// Gets the name of the node.
		/// </summary>
		/// <returns></returns>
		public string GetNodeName() => this.Path.Split('/').Last();

		/// <summary>
		/// Determines if this object is equal to another.
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool Equals(NodeIdentifier other)
		{
			if (other == null)
			{
				return false;
			}

			return this.Package == other.Package && this.Path == other.Path;
		}

		/// <summary>
		/// Determines if this object is equal to another.
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
		{
			if (!(obj is NodeIdentifier))
			{
				return false;
			}

			return Equals((NodeIdentifier) obj);
		}

		/// <summary>
		/// Computes the hash code of the object.
		/// </summary>
		/// <returns></returns>
		public override int GetHashCode()
		{
			unchecked
			{
				int hash = 17;

				hash *= 23 + this.Package.GetHashCode();
				hash *= 23 + this.Path.GetHashCode();

				return hash;
			}
		}
	}
}