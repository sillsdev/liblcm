﻿// Copyright (c) 2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using System.Linq;

namespace SIL.LCModel.Core.WritingSystems
{
	/// <summary>
	/// Writing System utilities
	/// </summary>
	public static class WritingSystemUtils
	{
		/// <summary>
		/// Gets all writing systems that are related to the specified writing system.
		/// </summary>
		/// <param name="ws">The writing system.</param>
		/// <param name="wss">The writing systems.</param>
		/// <returns></returns>
		public static IEnumerable<CoreWritingSystemDefinition> Related(this IEnumerable<CoreWritingSystemDefinition> wss, CoreWritingSystemDefinition ws)
		{
			return wss.Where(curWs => AreRelated(ws, curWs));
		}

		private static bool AreRelated(CoreWritingSystemDefinition ws1, CoreWritingSystemDefinition ws2)
		{
			return ws1.Language == ws2.Language && ws2 != ws1;
		}

		/// <summary>
		/// Returns the handles for the specified writing systems.
		/// </summary>
		/// <param name="wss">The writing systems.</param>
		/// <returns></returns>
		public static IEnumerable<int> Handles(this IEnumerable<CoreWritingSystemDefinition> wss)
		{
			return wss.Select(ws => ws.Handle);
		}

		/// <summary>
		/// Determines whether the writing systems contains a writing system with the specified handle.
		/// </summary>
		/// <param name="wss">The writing systems.</param>
		/// <param name="wsHandle">The writing system handle.</param>
		/// <returns>
		/// 	<c>true</c> if the writing systems contains a writing system with the handle, otherwise <c>false</c>.
		/// </returns>
		public static bool Contains(this IEnumerable<CoreWritingSystemDefinition> wss, int wsHandle)
		{
			return wss.Any(ws => ws.Handle == wsHandle);
		}
	}

	/// <summary>
	/// An equality comparer for writing systems that determines equality based on the IETF language tag.
	/// </summary>
	public class WritingSystemLanguageTagEqualityComparer : IEqualityComparer<CoreWritingSystemDefinition>
	{
		/// <summary>
		/// Determines whether the specified writing systems are equal.
		/// </summary>
		/// <returns>
		/// true if the specified writing systems are equal; otherwise, false.
		/// </returns>
		/// <param name="x">The first writing system to compare.</param>
		/// <param name="y">The second writing system to compare.</param>
		public bool Equals(CoreWritingSystemDefinition x, CoreWritingSystemDefinition y)
		{
			if (ReferenceEquals(x, y))
				return true;

			return x.LanguageTag == y.LanguageTag;
		}

		/// <summary>
		/// Returns a hash code for the specified writing system.
		/// </summary>
		/// <returns>
		/// A hash code for the specified writing system.
		/// </returns>
		/// <param name="ws">The writing system for which a hash code is to be returned.</param>
		/// <exception cref="T:System.ArgumentNullException">The <paramref name="ws"/> is null.</exception>
		public int GetHashCode(CoreWritingSystemDefinition ws)
		{
			return ws.LanguageTag.GetHashCode();
		}
	}
}
