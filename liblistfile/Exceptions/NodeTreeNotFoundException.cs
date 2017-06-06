//
//  NodeTreeNotFoundException.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2016 Jarl Gullberg
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

namespace liblistfile.Exceptions
{
	/// <summary>
	/// Thrown when the the node tree is attempted to be loaded from a nonexistent file.
	/// </summary>
	public class NodeTreeNotFoundException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="NodeTreeNotFoundException"/> class.
		/// </summary>
		public NodeTreeNotFoundException()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NodeTreeNotFoundException"/> class.
		/// </summary>
		/// <param name="message">A user-defined message.</param>
		public NodeTreeNotFoundException(string message) : base(message)
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NodeTreeNotFoundException"/> class.
		/// </summary>
		/// <param name="message">A user-defined message.</param>
		/// <param name="inner">The exception which caused this exception.</param>
		public NodeTreeNotFoundException(string message, Exception inner) : base(message, inner)
		{

		}
	}
}