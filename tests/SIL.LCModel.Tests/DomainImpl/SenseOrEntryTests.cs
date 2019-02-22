// Copyright (c) 2017 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Linq;
using NUnit.Framework;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;

// ReSharper disable InconsistentNaming

namespace SIL.LCModel.DomainImpl
{
	/// <summary/>
	public class SenseOrEntryTests : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		/// <summary/>
		[Test]
		public void ISenseOrEntryHeadwordRef_IncludesSenseNumber()
		{
			var mainEntry = CreateInterestingLexEntry(Cache, "MainEntry", "MainSense");
			AddSenseToEntry(mainEntry, "SecondSense", EnsureWritingSystemSetup(Cache, "en", false), Cache);
			var secondSense = new SenseOrEntry(mainEntry.SensesOS[1]);
			CreateInterestingLexEntry(Cache, "MainEntry", "Nonsense"); // create a homograph

			// Set default sense number style
			var settings = Cache.ServiceLocator.GetInstance<HomographConfiguration>();
			settings.ksSenseNumberStyle = "%d";

			// SUT
			Assert.AreEqual("MainEntry1 2", secondSense.HeadWordRef.BestVernacularAlternative.Text);
		}

		[Test]
		public void ISenseOrEntryHeadwordRef_SenseNumberNotShownWhenHCSenseIsNotShown()
		{
			var mainEntry = CreateInterestingLexEntry(Cache, "MainEntry", "MainSense");
			AddSenseToEntry(mainEntry, "SecondSense", EnsureWritingSystemSetup(Cache, "en", false), Cache);
			var secondSense = new SenseOrEntry(mainEntry.SensesOS[1]);
			var referencedEntry = CreateInterestingLexEntry(Cache);

			CreateLexicalReference(mainEntry, referencedEntry, "");
			CreateInterestingLexEntry(Cache, "MainEntry", "Nonsense");

			// Set empty sense number style
			var settings = Cache.ServiceLocator.GetInstance<HomographConfiguration>();
			settings.ksSenseNumberStyle = "";

			// SUT
			Assert.AreEqual("MainEntry1", secondSense.HeadWordRef.BestVernacularAlternative.Text);
		}

		private void CreateLexicalReference(ICmObject mainEntry, ICmObject referencedForm, string refTypeName, string refTypeReverseName = null)
		{
			CreateLexicalReference(mainEntry, referencedForm, null, refTypeName, refTypeReverseName);
		}

		private void CreateLexicalReference(ICmObject firstEntry, ICmObject secondEntry, ICmObject thirdEntry, string refTypeName, string refTypeReverseName = null)
		{
			var lrt = CreateLexRefType(LexRefTypeTags.MappingTypes.kmtEntryOrSenseSequence, refTypeName, "", refTypeReverseName, "");
			if (!string.IsNullOrEmpty(refTypeReverseName))
			{
				lrt.ReverseName.set_String(Cache.DefaultAnalWs, refTypeReverseName);
				lrt.MappingType = (int)MappingTypes.kmtEntryOrSenseTree;
			}
			var lexRef = Cache.ServiceLocator.GetInstance<ILexReferenceFactory>().Create();
			lrt.MembersOC.Add(lexRef);
			lexRef.TargetsRS.Add(firstEntry);
			lexRef.TargetsRS.Add(secondEntry);
			if (thirdEntry != null)
				lexRef.TargetsRS.Add(thirdEntry);
		}

		private ILexRefType CreateLexRefType(LexRefTypeTags.MappingTypes type, string name, string abbr, string revName, string revAbbr)
		{
			int wsEn = Cache.WritingSystemFactory.GetWsFromStr("en");
			if (Cache.LangProject.LexDbOA.ReferencesOA == null)
			{
				Cache.LangProject.LexDbOA.ReferencesOA = Cache.ServiceLocator.GetInstance<ICmPossibilityListFactory>().Create();
			}
			var referencePossibilities = Cache.LangProject.LexDbOA.ReferencesOA.PossibilitiesOS;
			if (referencePossibilities.Any(r => r.Name.BestAnalysisAlternative.Text == name))
			{
				return referencePossibilities.First(r => r.Name.BestAnalysisAlternative.Text == name) as ILexRefType;
			}
			var lrt = Cache.ServiceLocator.GetInstance<ILexRefTypeFactory>().Create();
			referencePossibilities.Add(lrt);
			lrt.MappingType = (int)type;
			lrt.Name.set_String(wsEn, name);
			lrt.Abbreviation.set_String(wsEn, abbr);
			if (!string.IsNullOrEmpty(revName))
				lrt.ReverseName.set_String(wsEn, revName);
			if (!string.IsNullOrEmpty(revAbbr))
				lrt.ReverseAbbreviation.set_String(wsEn, revAbbr);
			return lrt;
		}

		/// <summary>
		/// Creates an ILexEntry object, optionally with specified headword and gloss
		/// </summary>
		/// <param name="cache"></param>
		/// <param name="headword">Optional: defaults to 'Citation'</param>
		/// <param name="gloss">Optional: defaults to 'gloss'</param>
		/// <returns></returns>
		internal static ILexEntry CreateInterestingLexEntry(LcmCache cache, string headword = "Citation", string gloss = "gloss")
		{
			var entryFactory = cache.ServiceLocator.GetInstance<ILexEntryFactory>();
			var entry = entryFactory.Create();
			var wsEn = EnsureWritingSystemSetup(cache, "en", false);
			var wsFr = EnsureWritingSystemSetup(cache, "fr", true);
			AddHeadwordToEntry(entry, headword, wsFr, cache);
			entry.Comment.set_String(wsEn, TsStringUtils.MakeString("Comment", wsEn));
			AddSenseToEntry(entry, gloss, wsEn, cache);
			return entry;
		}

		private static void AddHeadwordToEntry(ILexEntry entry, string headword, int wsId, LcmCache cache)
		{
			// The headword field is special: it uses Citation if available, or LexemeForm if Citation isn't filled in
			entry.CitationForm.set_String(wsId, TsStringUtils.MakeString(headword, wsId));
		}

		private static void AddSenseToEntry(ILexEntry entry, string gloss, int wsId, LcmCache cache)
		{
			var senseFactory = cache.ServiceLocator.GetInstance<ILexSenseFactory>();
			var sense = senseFactory.Create();
			entry.SensesOS.Add(sense);
			if (!string.IsNullOrEmpty(gloss))
				sense.Gloss.set_String(wsId, TsStringUtils.MakeString(gloss, wsId));
		}

		private static int EnsureWritingSystemSetup(LcmCache cache, string wsStr, bool isVernacular)
		{
			var wsFact = cache.WritingSystemFactory;
			var result = wsFact.GetWsFromStr(wsStr);
			if (result < 1)
			{
				if (isVernacular)
				{
					cache.LangProject.AddToCurrentVernacularWritingSystems(cache.WritingSystemFactory.get_Engine(wsStr) as CoreWritingSystemDefinition);
				}
				else
				{
					cache.LangProject.AddToCurrentAnalysisWritingSystems(cache.WritingSystemFactory.get_Engine(wsStr) as CoreWritingSystemDefinition);
				}
			}
			return wsFact.GetWsFromStr(wsStr);
		}
	}
}
