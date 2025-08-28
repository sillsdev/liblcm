// Copyright (c) 2007-2013 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//
// File: TsStringComparer.cs
// Responsibility: TE Team

#nullable enable

using System;
using System.Collections;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.WritingSystems;

namespace SIL.LCModel.Core.Text
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Compare two ITsStrings.
	/// </summary>
	/// <remarks>This class does not check the writing systems of the strings to compare but
	/// uses the writing system passed in to the constructor.</remarks>
	/// ----------------------------------------------------------------------------------------
	public class TsStringComparer : IComparer
	{
		private readonly CoreWritingSystemDefinition m_ws;

		#region Constructors

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="TsStringComparer"/> class.
		/// </summary>
		/// <remarks>This version of the constructor uses .NET to compare two ITsStrings.
		/// </remarks>
		/// ------------------------------------------------------------------------------------
		public TsStringComparer()
		{
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="TsStringComparer"/> class.
		/// </summary>
		/// <param name="ws">The writing system.</param>
		/// ------------------------------------------------------------------------------------
		public TsStringComparer(CoreWritingSystemDefinition ws)
		{
			m_ws = ws;
		}
		#endregion

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the collating engine.
		/// </summary>
		/// <value>The collating engine.</value>
		/// ------------------------------------------------------------------------------------
		public CoreWritingSystemDefinition WritingSystem
		{
			get
			{
				return m_ws;
			}
		}

		#region IComparer Members
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Compares two objects and returns a value indicating whether one is less than, equal
		/// to, or greater than the other.
		/// </summary>
		/// <param name="x">The first object to compare.</param>
		/// <param name="y">The second object to compare.</param>
		/// <returns>
		/// Value Condition
		/// Less than zero = x is less than y.
		/// Zero = x equals y.
		/// Greater than zero = x is greater than y.
		/// </returns>
		/// <exception cref="T:System.ArgumentException">Neither x nor y implements the
		/// <see cref="T:System.IComparable"/> interface.-or- x and y are of different types and
		/// neither one can handle comparisons with the other. </exception>
		/// ------------------------------------------------------------------------------------
		public int Compare(object x, object y)
		{
			if ((!(x is ITsString || x is string) || !(y is ITsString || y is string)) &&
				x != null && y != null)
			{
				throw new ArgumentException();
			}

			string? xString = (x is ITsString) ? ((ITsString)x).Text : x as string;
			string? yString = (y is ITsString) ? ((ITsString)y).Text : y as string;
			if (xString == string.Empty)
				xString = null;
			if (yString == string.Empty)
				yString = null;

			if (xString == null && yString == null)
				return 0;

			if (xString == null)
				return -1;

			if (yString == null)
				return 1;

			if (m_ws != null && m_ws.DefaultCollation != null)
				return m_ws.DefaultCollation.Collator.Compare(xString, yString);

			return string.Compare(xString, yString, StringComparison.Ordinal);
		}

		#endregion
	}
}
