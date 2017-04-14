//
//  NodeType.cs
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

namespace liblistfile.NodeTree
{
	/// <summary>
	/// A set of types a node can have.
	/// </summary>
	public enum NodeType : uint
	{
		/// <summary>
		/// The node is a directory, and usually contains other nodes.
		/// </summary>
		Directory 	= 0,

		/// <summary>
		/// The node is a file, and doesn't normally have any child nodes.
		/// </summary>
		File 		= 1
	}
}