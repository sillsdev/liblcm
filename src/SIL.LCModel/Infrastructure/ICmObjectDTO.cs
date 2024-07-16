// Copyright (c) 2023 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

namespace SIL.LCModel.Infrastructure
{
	/// <summary>
	/// This DTO should hold any information needed to realize an object from backend storage
	/// </summary>
	public interface ICmObjectDTO
	{
		/// <summary/>
		/// <returns>The fully restored object contents</returns>
		ICmObject Transfer(LcmCache cache, string className);
	}
}