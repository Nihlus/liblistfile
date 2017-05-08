using System;
using System.Collections.Generic;
using System.Linq;

namespace liblistfile
{
	/// <summary>
	/// A set of helper utilities for path handling.
	/// </summary>
	public static class PathUtilities
	{
		/// <summary>
		/// Gets a list of the parent directories of a given path.
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static IEnumerable<string> GetDirectoryChain(string path)
		{
			if (path == "")
			{
				yield break;
			}

			string[] pathParts = path.Split('\\');
			for (int i = 0; i < pathParts.Length; ++i)
			{
				yield return pathParts.Take(i + 1).Aggregate((a, b) => a + '\\' + b) + '\\';
			}
		}

		/// <summary>
		/// Gets the directory path of the given path.
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static string GetDirectoryName(string path)
		{
			try
			{
				return path.Substring(0, path.LastIndexOf('\\'));
			}
			catch
			{
				return String.Empty;
			}
		}

		/// <summary>
		/// Gets the name of the file or directory the path is targeting.
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static string GetPathTargetName(string path)
		{
			string trimmedPath = path.TrimEnd('\\');
			int lastSlashIndex = trimmedPath.LastIndexOf('\\');

			if (lastSlashIndex != -1)
			{
				return trimmedPath.Substring(lastSlashIndex + 1);
			}

			return trimmedPath;
		}
	}
}