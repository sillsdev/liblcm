// Copyright (c) 2026 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Infrastructure;
using SIL.TestUtilities;

namespace SIL.LCModel.DomainServices
{
	/// <summary>
	/// On master a lone (unpaired) surrogate could never enter the model: every TsString setter threw while
	/// normalizing. Now the setters replace it with U+FFFD, so stored text equals in-memory text and nothing
	/// that indexes into a paragraph (segment offsets, analyses) can drift after a save and reload.
	/// NOTE: the strings are built in code with \u escapes; NUnit attributes would turn lone surrogates into U+FFFD.
	/// </summary>
	[TestFixture]
	public class LoneSurrogateModelTests : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		private const string High = "\uD800";
		private const string Low = "\uDC00";
		private const string Fffd = "�";

		// The test base already has a unit of work open for each test.
		private static void InUow(Action task)
		{
			task();
		}

		private IStTxtPara AddPara(ITsString contents)
		{
			IStTxtPara para = null;
			InUow(() =>
			{
				var text = Cache.ServiceLocator.GetInstance<ITextFactory>().Create();
				var stText = Cache.ServiceLocator.GetInstance<IStTextFactory>().Create();
				text.ContentsOA = stText;
				para = stText.AddNewTextPara(null);
				para.Contents = contents;
			});
			return para;
		}

		// Built in code and named in ASCII: NUnit attribute arguments would turn lone surrogates into U+FFFD.
		private static TestCaseData[] SetterCases()
		{
			return new[]
			{
				new TestCaseData("a" + High + "b", "a" + Fffd + "b").SetName("Setter_LoneHighMiddle"),
				new TestCaseData("a" + Low + "b", "a" + Fffd + "b").SetName("Setter_LoneLowMiddle"),
				new TestCaseData(High + "ab", Fffd + "ab").SetName("Setter_LoneHighAtStart"),
				new TestCaseData(Low + "ab", Fffd + "ab").SetName("Setter_LoneLowAtStart"),
				new TestCaseData("ab" + High, "ab" + Fffd).SetName("Setter_LoneHighAtEnd"),
				new TestCaseData("ab" + Low, "ab" + Fffd).SetName("Setter_LoneLowAtEnd"),
			};
		}

		private static TestCaseData[] ParserCases()
		{
			return new[]
			{
				new TestCaseData(Low + "ab cd. Ef gh.").SetName("Parse_LoneLowAtStart"),
				new TestCaseData("b" + Low + "c de.").SetName("Parse_LoneLowAfterLetter"),
				new TestCaseData("b" + High + "c de.").SetName("Parse_LoneHighAfterLetter"),
				new TestCaseData(High + "ab cd. Ef gh.").SetName("Parse_LoneHighAtStart"),
				new TestCaseData("ab cd. Ef gh." + Low).SetName("Parse_LoneLowAtEnd"),
				new TestCaseData("ab cd. Ef gh." + High).SetName("Parse_LoneHighAtEnd"),
				new TestCaseData("ab " + Low + " cd" + High + ". " + Low + High).SetName("Parse_Mixed"),
			};
		}

		[TestCaseSource(nameof(SetterCases))]
		public void ParagraphContents_Setter_ReplacesLoneSurrogate(string text, string expected)
		{
			var para = AddPara(TsStringUtils.MakeString(text, Cache.DefaultVernWs));
			Assert.That(para.Contents.Text, Is.EqualTo(expected));
		}

		[Test]
		public void MultiStringAndMultiUnicode_Setters_ReplaceLoneSurrogate()
		{
			int ws = Cache.DefaultAnalWs;
			InUow(() =>
			{
				Cache.LangProject.PartsOfSpeechOA.Name.set_String(ws, TsStringUtils.MakeString("n" + High + "m", ws));
				Cache.LangProject.PartsOfSpeechOA.Description.set_String(ws, TsStringUtils.MakeString("d" + Low + "e", ws));
			});
			Assert.That(Cache.LangProject.PartsOfSpeechOA.Name.get_String(ws).Text, Is.EqualTo("n" + Fffd + "m"));
			Assert.That(Cache.LangProject.PartsOfSpeechOA.Description.get_String(ws).Text, Is.EqualTo("d" + Fffd + "e"));
		}

		[Test]
		public void DomainDataByFlid_SetString_ReplacesLoneSurrogate()
		{
			var para = AddPara(TsStringUtils.MakeString("x", Cache.DefaultVernWs));
			InUow(() =>
				Cache.DomainDataByFlid.SetString(para.Hvo, StTxtParaTags.kflidContents,
					TsStringUtils.MakeString("p" + High + "q", Cache.DefaultVernWs)));
			Assert.That(para.Contents.Text, Is.EqualTo("p" + Fffd + "q"));
		}

		/// <summary>
		/// Even text that got into the model some other way (here by bypassing the setter, as undo and load do)
		/// must not make the parser throw. Lone highs and lows at the start, middle and end.
		/// </summary>
		[TestCaseSource(nameof(ParserCases))]
		public void ParagraphParser_TextWithLoneSurrogateThatBypassedTheSetter_DoesNotThrow(string text)
		{
			var para = AddPara(TsStringUtils.MakeString("placeholder", Cache.DefaultVernWs));
			InUow(() =>
				((ICmObjectInternal)para).SetProperty(StTxtParaTags.kflidContents,
					TsStringUtils.MakeString(text, Cache.DefaultVernWs), false));
			Assert.That(para.Contents.Text, Is.EqualTo(text), "the setter was bypassed");
			InUow(() =>
				Assert.DoesNotThrow(() => ParagraphParser.ParseParagraph(para)));
		}

		[Test]
		public void ParagraphParser_Fuzz_NeverThrows()
		{
			var rng = new Random(414);
			string[] alphabet = { "a", "b", "c", " ", ".", "é", "́", "\U0001F98A", High, Low, "\uDBFF", "\uDFFF" };
			var para = AddPara(TsStringUtils.MakeString("placeholder", Cache.DefaultVernWs));
			for (int n = 0; n < 150; n++)
			{
				var sb = new StringBuilder();
				int len = rng.Next(1, 10);
				for (int i = 0; i < len; i++)
					sb.Append(alphabet[rng.Next(alphabet.Length)]);
				string text = sb.ToString();
				string description = string.Join(" ", text.Select(c => ((int)c).ToString("X4")));
				InUow(() =>
				{
					((ICmObjectInternal)para).SetProperty(StTxtParaTags.kflidContents,
						TsStringUtils.MakeString(text, Cache.DefaultVernWs), false);
					Assert.DoesNotThrow(() => ParagraphParser.ParseParagraph(para), description);
				});
			}
		}
	}

	/// <summary>
	/// The scenario that matters: a paragraph with a lone surrogate, parsed, saved with the XML backend,
	/// and reloaded must still have segments that line up with its text.
	/// </summary>
	[TestFixture]
	public class LoneSurrogateSaveReloadTests
	{
		private TemporaryFolder m_projectsFolder;
		private ILcmDirectories m_lcmDirectories;

		[SetUp]
		public void TestSetup()
		{
			m_projectsFolder = new TemporaryFolder("LoneSurrogate" + Guid.NewGuid().ToString("N"));
			m_lcmDirectories = new TestLcmDirectories(m_projectsFolder.Path);
		}

		[TearDown]
		public void TestTeardown()
		{
			m_projectsFolder.Dispose();
		}

		[Test]
		public void ParsedParagraphWithLoneSurrogate_SegmentOffsetsSurviveSaveAndReload()
		{
			var projectName = "LoneSurrogate" + new Random().Next(1000000);
			var path = Path.Combine(m_projectsFolder.Path, LcmFileHelper.GetXmlDataFileName(projectName));
			var projectId = new TestProjectId(BackendProviderType.kXMLWithMemoryOnlyWsMgr, path);

			Guid paraGuid;
			int[] offsetsBefore;
			string[] baselinesBefore;
			string textBefore;
			using (var cache = LcmCache.CreateCacheWithNewBlankLangProj(projectId, "en", "fr", "en",
				new DummyLcmUI(), m_lcmDirectories, new LcmSettings()))
			{
				IStTxtPara para = null;
				UndoableUnitOfWorkHelper.Do("doit", "undoit", cache.ActionHandlerAccessor, () =>
				{
					var text = cache.ServiceLocator.GetInstance<ITextFactory>().Create();
					var stText = cache.ServiceLocator.GetInstance<IStTextFactory>().Create();
					text.ContentsOA = stText;
					para = stText.AddNewTextPara(null);
					para.Contents = TsStringUtils.MakeString("One a\uD800b two. Three four. Five six.", cache.DefaultVernWs);
					ParagraphParser.ParseParagraph(para);
				});
				paraGuid = para.Guid;
				textBefore = para.Contents.Text;
				offsetsBefore = para.SegmentsOS.Select(s => s.BeginOffset).ToArray();
				baselinesBefore = para.SegmentsOS.Select(s => s.BaselineText.Text).ToArray();
				Assert.That(textBefore, Is.EqualTo("One a�b two. Three four. Five six."));
				Assert.That(offsetsBefore, Is.EqualTo(new[] { 0, 13, 25 }), "sanity: three segments");

				cache.ServiceLocator.GetInstance<IUndoStackManager>().Save();
			}

			using (var reloaded = LcmCache.CreateCacheFromExistingData(projectId, "en", new DummyLcmUI(),
				m_lcmDirectories, new LcmSettings(), new DummyProgressDlg()))
			{
				var para = (IStTxtPara)reloaded.ServiceLocator.GetObject(paraGuid);
				Assert.That(para.Contents.Text, Is.EqualTo(textBefore), "text length must not change on save");
				Assert.That(para.SegmentsOS.Select(s => s.BeginOffset).ToArray(), Is.EqualTo(offsetsBefore));
				Assert.That(para.SegmentsOS.Select(s => s.BaselineText.Text).ToArray(), Is.EqualTo(baselinesBefore));
				Assert.That(para.ParseIsCurrent, Is.True);
			}
		}
	}
}
