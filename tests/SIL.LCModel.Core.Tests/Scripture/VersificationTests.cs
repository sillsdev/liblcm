// Copyright (c) 2008-2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using NUnit.Framework;

namespace SIL.LCModel.Core.Scripture
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Tests for the VersificationTable class
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public class VersificationTableTests
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Set up to initialize VersificationTable
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[OneTimeSetUp]
		public void FixtureSetup()
		{
			BCVRefTests.InitializeVersificationTable();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests VersificationTable.LastChapter
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetLastChapterForBook()
		{
			Assert.AreEqual(2, VersificationTable.Get(ScrVers.English).LastChapter(
				BCVRef.BookToNumber("HAG")));
			Assert.AreEqual(150, VersificationTable.Get(ScrVers.English).LastChapter(
				BCVRef.BookToNumber("PSA")));
			Assert.AreEqual(0, VersificationTable.Get(ScrVers.English).LastChapter(-1));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests getting the last verse for chapter.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetLastVerseForChapter()
		{
			Assert.AreEqual(20, VersificationTable.Get(ScrVers.English).LastVerse(
				BCVRef.BookToNumber("PSA"), 9));
			Assert.AreEqual(39, VersificationTable.Get(ScrVers.Septuagint).LastVerse(
				BCVRef.BookToNumber("PSA"), 9));
			Assert.AreEqual(1, VersificationTable.Get(ScrVers.English).LastVerse(
				BCVRef.BookToNumber("PSA"), 0), "Intro chapter (0) should be treated as having 1 verse.");
			Assert.AreEqual(0, VersificationTable.Get(ScrVers.Septuagint).LastVerse(0, 0));
		}
	}
}
