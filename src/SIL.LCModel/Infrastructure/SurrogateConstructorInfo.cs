// // Copyright (c) $year$ SIL International
// // This software is licensed under the LGPL, version 2.1 or later
// // (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Reflection;

namespace SIL.LCModel.Infrastructure
{
	internal static class SurrogateConstructorInfo
	{
		internal static Dictionary<string, ConstructorInfo> ClassToConstructorInfo;
		internal static void InitializeConstructors(List<Type> cmObjectTypes)
		{
			if (ClassToConstructorInfo != null) return;

			ClassToConstructorInfo = new Dictionary<string, ConstructorInfo>();
			// Get default constructor.
			// Only do this once, since they are stored in a static data member.
			foreach (var lcmType in cmObjectTypes)
			{
				if (lcmType.IsAbstract) continue;

				ClassToConstructorInfo.Add(lcmType.Name, lcmType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null));
			}
		}
	}
}