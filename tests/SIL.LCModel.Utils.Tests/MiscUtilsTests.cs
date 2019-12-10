// Copyright (c) 2003-2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace SIL.LCModel.Utils
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// MiscUtilsTests class
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public class MiscUtilsTests // can't derive from BaseTest because of dependencies
	{
		private static readonly Guid kGuid = new Guid("3E5CF9BD-BBD6-41d0-B09B-2CB4CA5F5479");
		private static readonly string kStrGuid = new string(new char[] { (char)0xF9BD,
			(char)0x3E5C, (char)0xBBD6, (char)0x41d0, (char)0x9BB0, (char)0xB42C, (char)0x5FCA,
			(char)0x7954 } );

		/// <summary>
		/// Tests MiscUtils.IndexOfSubArray
		/// </summary>
		[TestCase("a", "a", ExpectedResult = 0)]
		[TestCase("a", "b", ExpectedResult = -1)]
		[TestCase("a", "ab", ExpectedResult = -1)]
		[TestCase("abcde", "a", ExpectedResult = 0)]
		[TestCase("abcde", "ab", ExpectedResult = 0)]
		[TestCase("abcde", "b", ExpectedResult = 1)]
		[TestCase("abcde", "abcde", ExpectedResult = 0)]
		[TestCase("abcde", "bc", ExpectedResult = 1)]
		[TestCase("abcde", "de", ExpectedResult = 3)]
		public int ByteIndexOf(string input, string target)
		{
			return Encoding.UTF8.GetBytes(input).IndexOfSubArray(Encoding.UTF8.GetBytes(target));
		}

		/// <summary>
		/// Tests MiscUtils.SubArray
		/// </summary>
		[TestCase("", 0, 0, ExpectedResult = "")]
		[TestCase("a", 0, 0, ExpectedResult = "")]
		[TestCase("a", 0, 1, ExpectedResult = "a")]
		[TestCase("ab", 1, 0, ExpectedResult = "")]
		[TestCase("ab", 2, 0, ExpectedResult = "")]
		[TestCase("abc", 0, 1, ExpectedResult = "a")]
		[TestCase("abc", 1, 1, ExpectedResult = "b")]
		[TestCase("abc", 2, 1, ExpectedResult = "c")]
		[TestCase("abc", 0, 2, ExpectedResult = "ab")]
		[TestCase("abc", 1, 2, ExpectedResult = "bc")]
		[TestCase("abc", 0, 3, ExpectedResult = "abc")]
		[TestCase("abc", 2, 3, ExpectedResult = "c")] // truncate if length too long
		[TestCase("abc", 3, 3, ExpectedResult = "")]  // truncate if length too long
		public string SubArray(string input, int start, int length)
		{
			return Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(input).SubArray(start, length));
		}

		/// <summary>
		/// Tests MiscUtils.ReplaceSubArray
		/// </summary>
		[TestCase("", 0, 0, "", ExpectedResult = "")]
		[TestCase("a", 0, 0, "", ExpectedResult = "a")]
		[TestCase("a", 0, 1, "", ExpectedResult = "")]
		[TestCase("abc", 0, 1, "x", ExpectedResult = "xbc")]
		[TestCase("abc", 1, 1, "x", ExpectedResult = "axc")]
		[TestCase("abc", 2, 1, "x", ExpectedResult = "abx")]
		[TestCase("abc", 0, 3, "x", ExpectedResult = "x")]
		[TestCase("abcde", 1, 3, "x", ExpectedResult = "axe")]
		[TestCase("abc", 0, 0, "qed", ExpectedResult = "qedabc")]
		[TestCase("abc", 1, 1, "xy", ExpectedResult = "axyc")]
		public string ReplaceSubArray(string input, int start, int length, string replacement)
		{
			return Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(input)
				.ReplaceSubArray(start, length, Encoding.UTF8.GetBytes(replacement)));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that the guid stored in a string is extracted properly as a guid.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetGuidFromObjDataCorrectly()
		{
			Assert.That(MiscUtils.GetGuidFromObjData(kStrGuid), Is.EqualTo(kGuid));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that we get the expected string from a guid
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetObjDataFromGuidCorrectly()
		{
			Assert.That(MiscUtils.GetObjDataFromGuid(kGuid), Is.EqualTo(kStrGuid));
		}

		/// <summary />
		[TestCase("MyFile \u2200", MiscUtils.FilenameFilterStrength.kFilterMSDE, ExpectedResult = "MyFile \u2200")]
		[TestCase("My?|File<>Dude\\?*:/.'[];funñy()\n\t\"\u0344\u0361\u0513\u0307", MiscUtils.FilenameFilterStrength.kFilterBackup, ExpectedResult = "My__File__Dude_____.'[];funñy()___\u0344\u0361\u0513\u0307")]
		[TestCase("My?|File<>Dude\\?*:/.'[];funñy()\n\t\"\u0344\u0361\u0513\u0307", MiscUtils.FilenameFilterStrength.kFilterMSDE, ExpectedResult = "My__File__Dude_____.'___funñy()___\u0344\u0361\u0513\u0307")]
		[TestCase("My?|File<>Dude\\?*:/.'[];funñy()\n\t\"\u0344\u0361\u0513\u0307", MiscUtils.FilenameFilterStrength.kFilterProjName, ExpectedResult = "My__File__Dude_____.'___fun_y_________")]
		public string FilterForFileName(string filter, MiscUtils.FilenameFilterStrength strength)
		{
			return MiscUtils.FilterForFileName(filter, strength);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that GetFolderName handles invalid folder strings correctly. It should return
		/// string.Empty
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[TestCase("", ExpectedResult = "")]
		[TestCase("<&^$%#@>", ExpectedResult = "")]
		public string GetFolderName_InvalidFolderString(string folderName)
		{
			return MiscUtils.GetFolderName(folderName);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets a directory name for testing purposes. We use the directory where the
		/// executable is located.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private string DirectoryName
		{
			get
			{
				return Path.GetDirectoryName(Assembly.GetCallingAssembly().CodeBase
					.Substring(MiscUtils.IsUnix ? 7 : 8));
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that GetFolderName gets valid directory names from strings.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetFolderName()
		{
			string directory = DirectoryName;
			Assert.That(MiscUtils.GetFolderName(directory), Is.EqualTo(directory));

		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that GetFolderName gets valid directory names from strings that contains
		/// directory and existing filename.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetFolderName_FromFilename()
		{
			string directory = DirectoryName;
			Assert.That(MiscUtils.GetFolderName(Path.Combine(directory, "iso-8859-1.tec")), Is.EqualTo(directory));
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that GetFolderName returns string.Empty if passed in directory doesn't exist
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void GetFolderName_InvalidDirectory()
		{
			string directory = DirectoryName;
			Assert.That(MiscUtils.GetFolderName(directory.Insert(3, "junk")), Is.Empty);
		}

		/// <summary/>
		[TestCase("45A", "45A", ExpectedResult = 0)]
		[TestCase("0x45A", "0x45A", ExpectedResult = 0)]
		[TestCase("45A", "0X45A", ExpectedResult = 0)]
		[TestCase("45A", "45B", ExpectedResult = -1)]
		[TestCase("ABCDEF", "1", ExpectedResult = 1)]
		[TestCase("0000", "0x0", ExpectedResult = 0, Description = "both parameters are (different) hexadecimal representations of the number 0.")]
		[TestCase("0", "", ExpectedResult = 0)]
		public int CompareHex(string hex1, string hex2)
		{
			return MiscUtils.CompareHex(hex1, hex2);
		}

		/// <summary/>
		[TestCase(null, null, typeof(ArgumentNullException))]
		[TestCase("XYZ", "@!#", typeof(FormatException))]
		[TestCase("34", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", typeof(OverflowException))]
		public void CompareHex_Exception(string hex1, string hex2, Type exceptionType)
		{
			Assert.That(() => MiscUtils.CompareHex(hex1, hex2),
				Throws.TypeOf(exceptionType));
		}

		/// <summary></summary>
		[TestCase(null, ExpectedResult = false)]
		[TestCase("", ExpectedResult = false)]
		[TestCase("a", ExpectedResult = true)]
		[TestCase("C", ExpectedResult = true)]
		[TestCase("bb", ExpectedResult = true)]
		[TestCase("aBcDeFg", ExpectedResult = true)]
		[TestCase("1", ExpectedResult = false)]
		[TestCase("22", ExpectedResult = false)]
		[TestCase("a1", ExpectedResult = false)]
		[TestCase("$", ExpectedResult = false)]
		[TestCase("a#", ExpectedResult = false)]
		[TestCase("3%", ExpectedResult = false)]
		[TestCase("a1&", ExpectedResult = false)]
		[TestCase(" ", ExpectedResult = false)]
		[TestCase("\n", ExpectedResult = false)]
		[TestCase("a\n", ExpectedResult = false)]
		[TestCase("a b c", ExpectedResult = false)]
		public bool IsAlpha(string input)
		{
			return MiscUtils.IsAlpha(input);
		}

		/// <summary></summary>
		[TestCase("Wow & Cool!", ExpectedResult = "Wow & Cool!", Description = "SimpleAmpersand")]
		[TestCase("Wow &amp;&&amp;&;&#x721; Cool!", ExpectedResult = "Wow &amp;&&amp;&;&#x721; Cool!", Description = "AmpersandMixedWithEntitiesAndCodepoints")]
		[TestCase("Wow &#x Cool!", ExpectedResult = "Wow &#x Cool!", Description = "AmpersandWithCodepointStart")]
		[TestCase("Wow &amp; Cool!", ExpectedResult = "Wow &amp; Cool!", Description = "ValidEntities")]
		[TestCase("Wow &#x3456;&#x31;&#x721; Cool!", ExpectedResult = "Wow &#x3456;&#x31;&#x721; Cool!", Description = "ValidHexCodepoints")]
		[TestCase("Wow &#x34564; Cool!", ExpectedResult = "Wow &#x34564; Cool!", Description = "InvalidHexCodepointsWithTooManyDigits")]
		[TestCase("Wow \uFFFE Cool!", ExpectedResult = "Wow  Cool!", Description = "BogusFFFE")]
		[TestCase("Wow \uFFFF Cool!", ExpectedResult = "Wow  Cool!", Description = "BogusFFFF")]
		[TestCase("Wow \uFFFF\uFFFE Cool!", ExpectedResult = "Wow  Cool!", Description = "BogusFFFEandFFFF")]
		[TestCase("Wow &#xFFFE; Cool!", ExpectedResult = "Wow  Cool!", Description = "BogusFFFE encoded as hex")]
		[TestCase("Wow &#xFFFF; Cool!", ExpectedResult = "Wow  Cool!", Description = "BogusFFFF encoded as hex")]
		[TestCase("Wow &#xFFFE;&#xFFFF;&#xFFFE;&#xFFFF; Cool!", ExpectedResult = "Wow  Cool!", Description = "BogusFFFEandFFFF encoded as hex")]
		[TestCase("Wow \u000A\u000D&#xA;&#xD; Cool!", ExpectedResult = "Wow  Cool!", Description = "CarriageReturnAndLinefeed")]
		[TestCase("Wow&#x20;&#x9; \tCool!", ExpectedResult = "Wow&#x20;&#x9; \tCool!", Description = "KeepSpaceAndTab")]
		public string CleanupXmlString(string input)
		{
			return MiscUtils.CleanupXmlString(input);
		}

		#region RunProcess tests

		/// <summary></summary>
		[Test]
		public void RunProcess_existingCommand_noError()
		{
			bool errorTriggered = false;
			using (MiscUtils.RunProcess("find", "blah",
				(exception) => { errorTriggered = true; }))
			{
				Assert.That(errorTriggered, Is.False);
			}
		}

		/// <summary></summary>
		[Test]
		public void RunProcess_nonexistentCommand_givesError()
		{
			bool errorTriggered = false;
			using (MiscUtils.RunProcess("nonexistentCommand", "",
				(exception) => { errorTriggered = true; }))
			{
				Assert.That(errorTriggered, Is.True);
			}
		}

		/// <summary></summary>
		[Test]
		public void RunProcess_allowsNullErrorHandler()
		{
			Assert.That(() => {
				using (MiscUtils.RunProcess("nonexistentCommand", "", null))
				{
				}
			}, Throws.Nothing);
		}
		#endregion // RunProcess tests

		/// <summary/>
		[Test]
		public void RunningTests_IsTrue()
		{
			Assert.That(MiscUtils.RunningTests, Is.True);
		}

		/// <summary>
		/// Test the indicated routine
		/// </summary>
		[TestCase("", ExpectedResult = "")]
		[TestCase("abc", ExpectedResult = "abc")]
		[TestCase("1", ExpectedResult = "0000000001")]
		[TestCase("a1", ExpectedResult = "a0000000001")]
		[TestCase("a1b", ExpectedResult = "a0000000001b")]
		[TestCase("1b", ExpectedResult = "0000000001b")]
		[TestCase("12", ExpectedResult = "0000000012")]
		[TestCase("1.2.3", ExpectedResult = "0000000001.0000000002.0000000003")]
		public string NumbersAlphabeticKey_Key(string key)
		{
			return MiscUtils.NumbersAlphabeticKey(key);
		}

		/// <summary>
		/// Test the indicated routine
		/// </summary>
		[TestCase("12", "2")]
		[TestCase("12.12abcd", "12.2abcd")]
		public void NumbersAlphabeticKey_CompareKeys(string key1, string key2)
		{
			// This is really the point!
			Assert.That(MiscUtils.NumbersAlphabeticKey(key1), Is.GreaterThan(MiscUtils.NumbersAlphabeticKey(key2)));
		}
	}
}
