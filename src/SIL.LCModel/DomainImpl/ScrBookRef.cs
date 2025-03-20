// Copyright (c) 2002-2013 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//
// File: ScrBookRef.cs
// Responsibility: TomB
// Last reviewed:
//
// <remarks>
//
// </remarks>

using System;
using SIL.LCModel.Core.Scripture;
using SIL.LCModel.DomainServices;

namespace SIL.LCModel.DomainImpl
{
	internal partial class ScrRefSystem
	{
		internal void Initialize()
		{
			IScrBookRefFactory scrBookRefFactory = Cache.ServiceLocator.GetInstance<IScrBookRefFactory>();
			for (int i = 0; i < 66; i++)
				BooksOS.Add(scrBookRefFactory.Create());
		}
	}

	/// <summary>
	/// Summary description for ScrBookRef.
	/// </summary>
	internal partial class ScrBookRef
	{
		#region Custom Tags and Properties
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the standard abbreviation of the book in the UI writing system. If no abbrev is
		/// available in the UI writing system, try the current analysis languages and English.
		/// If still no abbrev is available, return the UBS 3-letter book code.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public string UIBookAbbrev
		{
			get
			{
				int wsHvo = WritingSystemServices.FallbackUserWs(m_cache);
				string sBookAbbrev = BookAbbrev.get_String(wsHvo).Text;

				// Try for the current analysis languages and English.
				if (string.IsNullOrEmpty(sBookAbbrev))
					sBookAbbrev = BookAbbrev.BestAnalysisAlternative.Text;

				// UBS book code
				if (string.IsNullOrEmpty(sBookAbbrev) || sBookAbbrev == BookAbbrev.NotFoundTss.Text)
					sBookAbbrev = ScrReference.NumberToBookCode(IndexInOwner + 1);

				return sBookAbbrev.Trim();
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the standard name of the book in the UI writing system. If no name is available
		/// in the UI writing system, try the current analysis languages and English. If still
		/// no name is available, return the UBS 3-letter book code.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public string UIBookName
		{
			get
			{
				int wsHvo = WritingSystemServices.FallbackUserWs(m_cache);
				string sBookName = BookName.get_String(wsHvo).Text;

				// Try for the current analysis languages and English.
				if (string.IsNullOrEmpty(sBookName))
					sBookName = BookName.BestAnalysisAlternative.Text;

				// UBS code, if all else fails.
				if (string.IsNullOrEmpty(sBookName) || sBookName == BookName.NotFoundTss.Text)
					sBookName = ScrReference.NumberToBookCode(IndexInOwner + 1);

				return sBookName.Trim();
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns a <see cref="T:System.String"/> that represents this instance.
		/// </summary>
		/// <returns>
		/// A <see cref="T:System.String"/> that represents this instance.
		/// </returns>
		/// ------------------------------------------------------------------------------------
		public override string ToString()
		{
			return UIBookName;
		}

		#endregion
	}
}
