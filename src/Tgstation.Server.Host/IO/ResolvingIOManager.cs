﻿using System;
using System.IO;

namespace Tgstation.Server.Host.IO
{
	/// <summary>
	/// An <see cref="IIOManager"/> that resolve relative paths from another <see cref="IIOManager"/> to a subdirectory of that.
	/// </summary>
	sealed class ResolvingIOManager : DefaultIOManager
	{
		/// <summary>
		/// Path to the subdirectory attached to path resolutions.
		/// </summary>
		readonly string subdirectory;

		/// <summary>
		/// Initializes a new instance of the <see cref="ResolvingIOManager"/> class.
		/// </summary>
		/// <param name="parent">The <see cref="IIOManager"/> that resolves to the directory to work out of.</param>
		/// <param name="subdirectory">The value of <see cref="subdirectory"/>.</param>
		public ResolvingIOManager(IIOManager parent, string subdirectory)
		{
			if (parent == null)
				throw new ArgumentNullException(nameof(parent));
			if (subdirectory == null)
				throw new ArgumentNullException(nameof(subdirectory));

			this.subdirectory = ConcatPath(parent.ResolvePath(), subdirectory);
		}

		/// <inheritdoc />
		public override string ResolvePath(string path)
		{
			if (!Path.IsPathRooted(path))
				return base.ResolvePath(ConcatPath(subdirectory, path));
			return path;
		}
	}
}
