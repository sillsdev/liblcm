using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.Infrastructure;
using System.IO;
using SIL.LCModel.Core.KernelInterfaces;
using StructureMap.Diagnostics.TreeView;
using SIL.LCModel.Core.Text;
using System.Xml.Linq;
using System.Security.Cryptography;
using SIL.Xml;
using System.Xml;
using static Icu.Normalization.Normalizer2;

namespace SIL.LCModel.DomainServices
{
	[TestFixture]
	public class PhonologyServicesTest
	{
		private LcmCache m_cache;
		private DateTime m_now;

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Create the cache before each test
		/// </summary>
		///--------------------------------------------------------------------------------------
		[SetUp]
		public void CreateTestCache()
		{
			m_now = DateTime.Now;
			m_cache = LcmCache.CreateCacheWithNewBlankLangProj(new TestProjectId(BackendProviderType.kMemoryOnly, "MemoryOnly.mem"),
				"en", "fr", "en", new DummyLcmUI(), TestDirectoryFinder.LcmDirectories, new LcmSettings());
			IDataSetup dataSetup = m_cache.ServiceLocator.GetInstance<IDataSetup>();
			dataSetup.LoadDomain(BackendBulkLoadDomain.All);
			if (m_cache.LangProject != null)
			{
				if (m_cache.LangProject.DefaultVernacularWritingSystem == null)
				{
					List<CoreWritingSystemDefinition> rglgws = m_cache.ServiceLocator.WritingSystemManager.WritingSystems.ToList();
					if (rglgws.Count > 0)
					{
						m_cache.DomainDataByFlid.BeginNonUndoableTask();
						m_cache.LangProject.DefaultVernacularWritingSystem = rglgws[rglgws.Count - 1];
						m_cache.DomainDataByFlid.EndNonUndoableTask();
					}
				}
				if (m_cache.LangProject.DefaultAnalysisWritingSystem == null)
				{
					List<CoreWritingSystemDefinition> rglgws = m_cache.ServiceLocator.WritingSystemManager.WritingSystems.ToList();
					if (rglgws.Count > 0)
					{
						m_cache.DomainDataByFlid.BeginNonUndoableTask();
						m_cache.LangProject.DefaultAnalysisWritingSystem = rglgws[rglgws.Count - 1];
						m_cache.DomainDataByFlid.EndNonUndoableTask();
					}
				}
			}
		}

		///--------------------------------------------------------------------------------------
		/// <summary>
		/// Destroy the cache after each test
		/// </summary>
		///--------------------------------------------------------------------------------------
		[TearDown]
		public void DestroyTestCache()
		{
			if (m_cache != null)
			{
				m_cache.Dispose();
				m_cache = null;
			}
		}

		private void SetDefaultVernacularWritingSystem(LcmCache cache, CoreWritingSystemDefinition vernWritingSystem)
		{
			var vernWsName = vernWritingSystem.Id;
			var wsManager = cache.ServiceLocator.WritingSystemManager;
			if (wsManager.Exists(vernWsName))
				vernWritingSystem = wsManager.Get(vernWsName);
			else
			{
				vernWritingSystem = wsManager.Set(vernWsName);
			}
			NonUndoableUnitOfWorkHelper.Do(m_cache.ActionHandlerAccessor, () =>
				m_cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem = vernWritingSystem);
		}

		private void TestProject(string projectsDirectory, string dbFileName)
		{
			var projectId = new TestProjectId(BackendProviderType.kXML, dbFileName);
			var m_ui = new DummyLcmUI();
			var m_lcmDirectories = new TestLcmDirectories(projectsDirectory);
			using (var cache = LcmCache.CreateCacheFromExistingData(projectId, "en", m_ui, m_lcmDirectories, new LcmSettings(),
					new DummyProgressDlg()))
			{
				// Export project as XML.
				var services = new PhonologyServices(cache);
				XDocument xdoc = services.ExportPhonologyAsXml();
				var xml = xdoc.ToString();
				// Import XML to a new cache and export it a second time.
				XDocument xdoc2 = null;
				using (var rdr = new StringReader(xml))
				{
					var vernWs = cache.ServiceLocator.WritingSystemManager.Get(cache.DefaultVernWs);
					SetDefaultVernacularWritingSystem(m_cache, vernWs);
					var services2 = new PhonologyServices(m_cache, vernWs.Id);
					services2.ImportPhonologyFromXml(rdr);
					xdoc2 = services2.ExportPhonologyAsXml();
				}
				var xml2 = xdoc2.ToString();
				// Compare original cache data to new cache data.
				TestEqual(cache.LanguageProject.PhonologicalDataOA, m_cache.LanguageProject.PhonologicalDataOA);
				// Compare original XML to new XML.
				TestEqual(xdoc, xdoc2);
			}
		}

		private void TestEqual(XDocument xdoc, XDocument xdoc2)
		{
			TestEqual(xdoc.Elements(), xdoc2.Elements());
		}

		private void TestEqual(IEnumerable<XElement> elements,  IEnumerable<XElement> elements2)
		{
			foreach (var pair in elements.Zip(elements2, Tuple.Create))
			{
				TestEqual(pair.Item1, pair.Item2);
			}
		}

		private void TestEqual(XElement element, XElement element2)
		{
			Assert.AreEqual(element.Attributes().Count(), element2.Attributes().Count());
			foreach (var attr in element.Attributes())
			{
				XName name = attr.Name;
				if (name == "Id" || name == "dst" || name == "Guid")
					continue;
				bool found = false;
				foreach (var attr2 in element2.Attributes())
				{
					if (attr2.Name == name)
					{
						Assert.AreEqual(attr.Value, attr2.Value);
						found = true;
					}
				}
				Assert.IsTrue(found);
			}
			TestEqual(element.Elements(), element2.Elements());
		}

		private void TestEqual(IPhPhonData phonologicalData, IPhPhonData phonologicalData2)
		{
			TestEqual(phonologicalData.PhonemeSetsOS, phonologicalData2.PhonemeSetsOS);
			TestEqual(phonologicalData.NaturalClassesOS, phonologicalData2.NaturalClassesOS);
			TestEqual(
				phonologicalData.Services.GetInstance<IPhEnvironmentRepository>().AllValidInstances(),
				phonologicalData2.Services.GetInstance<IPhEnvironmentRepository>().AllValidInstances());
			TestEqual(phonologicalData.PhonRulesOS, phonologicalData2.PhonRulesOS);
		}

		private void TestEqual(IEnumerable<IPhPhonemeSet> phonemeSets, IEnumerable<IPhPhonemeSet> phonemeSets2)
		{
			Assert.AreEqual(phonemeSets.Count(),phonemeSets2.Count());
			foreach (var pair in phonemeSets.Zip(phonemeSets2, Tuple.Create))
			{
				TestEqual(pair.Item1, pair.Item2);
			}
		}

		private void TestEqual(IPhPhonemeSet phonemeSet, IPhPhonemeSet phonemeSet2)
		{
			TestEqual(phonemeSet.Name, phonemeSet2.Name);
			TestEqual(phonemeSet.PhonemesOC, phonemeSet2.PhonemesOC);
		}

		private void TestEqual(IEnumerable<IPhNaturalClass> naturalClasses, IEnumerable<IPhNaturalClass> naturalClasses2)
		{
			Assert.AreEqual(naturalClasses.Count(), naturalClasses2.Count());
			foreach (var pair in naturalClasses.Zip(naturalClasses2, Tuple.Create))
			{
				TestEqual(pair.Item1, pair.Item2);
			}
		}

		private void TestEqual(IPhNaturalClass naturalClass, IPhNaturalClass naturalClass2)
		{
			TestEqual(naturalClass.Name, naturalClass2.Name);
			TestEqual(naturalClass.Description, naturalClass2.Description);
			TestEqual(naturalClass.Abbreviation, naturalClass2.Abbreviation);
			if (naturalClass is IPhNCFeatures)
				TestEqual(naturalClass as IPhNCFeatures, naturalClass2 as IPhNCFeatures);
			else
				TestEqual(naturalClass as IPhNCSegments, naturalClass2 as IPhNCSegments);
		}

		private void TestEqual(IPhNCFeatures naturalClass, IPhNCFeatures naturalClass2)
		{
			TestEqual(naturalClass.FeaturesOA, naturalClass2.FeaturesOA);
		}

		private void TestEqual(IFsAbstractStructure absFeatStruc, IFsAbstractStructure absFeatStruc2)
		{
			if (absFeatStruc == absFeatStruc2)
				return;
			switch (absFeatStruc.ClassName)
			{
				case "FsFeatStruc":
					var featStruc = (IFsFeatStruc)absFeatStruc;
					var featStruc2 = (IFsFeatStruc)absFeatStruc;
					TestEqual(featStruc.TypeRA, featStruc2.TypeRA);
					TestEqual(featStruc.FeatureSpecsOC, featStruc2.FeatureSpecsOC);
					break;
				default:
					// As of 14 November 2009, FsFeatStrucDisj is not supported.
					throw new ArgumentException("Unrecognized subclass.");
			}
		}

		private void TestEqual(IFsFeatStrucType type, IFsFeatStrucType type2)
		{
			if (type != type2)
				Assert.True(type.Equals(type2));
		}

		private void TestEqual(IEnumerable<IFsFeatureSpecification> featureSpecs, IEnumerable<IFsFeatureSpecification> featureSpecs2)
		{
			Assert.AreEqual(featureSpecs.Count(), featureSpecs2.Count());
			foreach (var pair in featureSpecs.Zip(featureSpecs2, Tuple.Create))
			{
				TestEqual(pair.Item1, pair.Item2);
			}

		}

		private void TestEqual(IFsFeatureSpecification featureSpec, IFsFeatureSpecification featureSpec2)
		{
			switch (featureSpec.ClassName)
			{
				default:
					// These are not supported as of 14 November 2009.
					// FsOpenValue
					// FsDisjunctiveValue
					// FsSharedValue
					throw new ArgumentException("Unrecognized feature specification");
				case "FsClosedValue":
					var closedValue = (IFsClosedValue)featureSpec;
					var closedValue2 = (IFsClosedValue)featureSpec2;
					TestEqual(closedValue.FeatureRA, closedValue2.FeatureRA);
					TestEqual(closedValue.ValueRA, closedValue2.ValueRA);
					break;
				case "FsComplexValue":
					var complexValue = (IFsComplexValue)featureSpec;
					var complexValue2 = (IFsComplexValue)featureSpec2;
					TestEqual(complexValue.FeatureRA, complexValue2.FeatureRA);
					TestEqual(complexValue.ValueOA, complexValue2.ValueOA);
					break;
				case "FsNegatedValue":
					var negatedValue = (IFsNegatedValue)featureSpec;
					var negatedValue2 = (IFsNegatedValue)featureSpec2;
					TestEqual(negatedValue.FeatureRA, negatedValue2.FeatureRA);
					TestEqual(negatedValue.ValueRA, negatedValue2.ValueRA);
					break;
			}

		}

		private void TestEqual(IFsFeatDefn def, IFsFeatDefn def2)
		{
			Assert.AreEqual(def, def2);
		}

		private void TestEqual(IFsSymFeatVal value, IFsSymFeatVal value2)
		{
			Assert.AreEqual(value, value2);
		}

		private void TestEqual(IPhNCSegments naturalClass, IPhNCSegments naturalClass2)
		{
			TestEqual(naturalClass.SegmentsRC, naturalClass2.SegmentsRC);
		}

		private void TestEqual(IEnumerable<IPhPhoneme> phonemes, IEnumerable<IPhPhoneme> phonemes2)
		{
			Assert.AreEqual(phonemes.Count(), phonemes2.Count());
			foreach (var pair in phonemes.Zip(phonemes2, Tuple.Create))
			{
				TestEqual(pair.Item1, pair.Item2);
			}
		}

		private void TestEqual(IPhPhoneme phoneme,  IPhPhoneme phoneme2)
		{
			TestEqual(phoneme.Name, phoneme2.Name);
			TestEqual(phoneme.Description, phoneme2.Description);
			TestEqual(phoneme.CodesOS, phoneme2.CodesOS);
			TestEqual(phoneme.BasicIPASymbol, phoneme2.BasicIPASymbol);
			TestEqual(phoneme.FeaturesOA, phoneme2.FeaturesOA);
		}

		private void TestEqual(IEnumerable<IPhCode> codes, IEnumerable<IPhCode> codes2)
		{
			Assert.AreEqual(codes.Count(), codes2.Count());
			foreach (var pair in codes.Zip(codes2, Tuple.Create))
			{
				TestEqual(pair.Item1, pair.Item2);
			}
		}

		private void TestEqual(IPhCode code, IPhCode code2)
		{
			TestEqual(code.Representation, code2.Representation);
		}

		private void TestEqual(IEnumerable<IPhEnvironment> environments, IEnumerable<IPhEnvironment> environments2)
		{
			Assert.AreEqual(environments.Count(), environments2.Count());
			foreach (var pair in environments.Zip(environments2, Tuple.Create))
			{
				TestEqual(pair.Item1, pair.Item2);
			}
		}

		private void TestEqual(IPhEnvironment environment, IPhEnvironment environment2)
		{
			TestEqual(environment.Name, environment2.Name);
		}

		private void TestEqual(IEnumerable<IPhSegmentRule> rules, IEnumerable<IPhSegmentRule> rules2)
		{
			Assert.AreEqual(rules.Count(), rules2.Count());
			foreach (var pair in rules.Zip(rules2, Tuple.Create))
			{
				TestEqual(pair.Item1, pair.Item2);
			}
		}

		private void TestEqual(IPhSegmentRule rule, IPhSegmentRule rule2)
		{
			Assert.AreEqual(rule.ClassName, rule2.ClassName);
			// Too complicated to test the rest.
			// We will depend on the XML test for it.
		}

		private void TestEqual(IMultiAccessorBase multiString, IMultiAccessorBase multiString2)
		{
			Assert.AreEqual(multiString.AvailableWritingSystemIds.Length, multiString2.AvailableWritingSystemIds.Length);
			for (int i = 0; i < multiString.AvailableWritingSystemIds.Length; i++)
			{
				int ws = multiString.AvailableWritingSystemIds[i];
				int ws2 = multiString2.AvailableWritingSystemIds[i];
				TestEqual(multiString.get_String(ws), multiString2.get_String(ws2));
			}
		}

		private void TestEqual(ITsString tsString, ITsString tsString2)
		{
			// We can't use tsString.Equals(tsString2) because
			// the writing system ids are incompatible.
			Assert.AreEqual(tsString.ToString(), tsString2.ToString());
		}

		[Test]
		public void TestQuechua()
		{
			TestProject(
				"C:\\Users\\PC\\source\\repos\\FieldWorks\\DistFiles\\Projects\\QuechuaMark",
				"C:\\Users\\PC\\source\\repos\\FieldWorks\\DistFiles\\Projects\\QuechuaMark\\QuechuaMark.fwdata");
		}

		[Test]
		public void TestSpanish2()
		{
			TestProject(
				"C:\\Users\\PC\\source\\repos\\FieldWorks\\DistFiles\\Projects\\Spanish-GenerateWords-Experiment",
				"C:\\Users\\PC\\source\\repos\\FieldWorks\\DistFiles\\Projects\\Spanish-GenerateWords-Experiment\\Spanish-GenerateWords-Experiment.fwdata");
		}

		[Test]
		public void TestBlxFlex()
		{
			TestProject(
				"C:\\Users\\PC\\source\\repos\\FieldWorks\\DistFiles\\Projects\\blx-flex",
				"C:\\Users\\PC\\source\\repos\\FieldWorks\\DistFiles\\Projects\\blx-flex\\blx-flex.fwdata");
		}

		[Test]
		public void TestIkuzu()
		{
			TestProject(
				"C:\\Users\\PC\\source\\repos\\FieldWorks\\DistFiles\\Projects\\Ikizu",
				"C:\\Users\\PC\\source\\repos\\FieldWorks\\DistFiles\\Projects\\Ikizu\\Ikizu.fwdata");
		}

		[Test]
		public void TestEmpty()
		{
			var services = new PhonologyServices(m_cache);
			var xdoc = services.ExportPhonologyAsXml();
			var xml = xdoc.ToString();
			using (var rdr = new StringReader(xml))
			{
				services.ImportPhonologyFromXml(rdr);
				var xdoc2 = services.ExportPhonologyAsXml();
				var xml2 = xdoc2.ToString();
				Assert.AreEqual(xml, xml2);
			}
		}

		public static readonly string ksPhFS1 =
			string.Format("<item id=\"gPAMajorClassFeature\" posid=\"Adjective\" guid=\"f673a43d-ba35-44f1-a4d0-308a292c4b97\" status=\"visible\" type=\"group\"><abbrev ws=\"en\">mcf</abbrev><term ws=\"en\">major class features</term><def ws=\"en\">The features that represent the major classes of sounds.</def><citation>[http://en.wikipedia.org/wiki/Distinctive_feature] Date accessed: 12-Feb-2009</citation>" +
				"<item id=\"fPAConsonantal\" guid=\"b4ddf8e5-1ff8-43fc-9723-04f1ee0471fc\" type=\"feature\"><abbrev ws=\"en\">cons</abbrev><term ws=\"en\">consonantal</term><def ws=\"en\">Consonantal segments are produced with an audible constriction in the vocal tract, like plosives, affricates, fricatives, nasals, laterals and [r]. Vowels, glides and laryngeal segments are not consonantal.</def><citation>[http://en.wikipedia.org/wiki/Distinctive_feature] Date accessed: 12-Feb-2009</citation>" +
				"<item id='vPAConsonantalPositive' guid=\"ec5800b4-52a8-4859-a976-f3005c53bd5f\" type='value'><abbrev ws='en'>+</abbrev><term ws='en'>positive</term><fs id='vPAConsonantalPositiveFS' type='Phon' typeguid=\"0ea53dd6-79f5-4fac-a672-f2f7026d8d15\"><f name='fPAConsonantal'><sym value='+'/></f></fs></item>" +
				"<item id='vPAConsonantalNegative' guid=\"81c50b82-83ff-4f73-8e27-6ff9217b810a\" type='value'><abbrev ws='en'>-</abbrev><term ws='en'>negative</term><fs id='vPAConsonantalNegativeFS' type='Phon' typeguid=\"0ea53dd6-79f5-4fac-a672-f2f7026d8d15\"><f name='fPAConsonantal'><sym value='-'/></f></fs></item></item>" +
				"<item id=\"fPASonorant\" guid=\"7df7b583-dd42-424d-9730-ab7bcda314e7\" type=\"feature\"><abbrev ws=\"en\">son</abbrev><term ws=\"en\">sonorant</term><def ws=\"en\">This feature describes the type of oral constriction that can occur in the vocal tract. [+son] designates the vowels and sonorant consonants, which are produced without the imbalance of air pressure in the vocal tract that might cause turbulence. [-son] alternatively describes the obstruents, articulated with a noticeable turbulence caused by an imbalance of air pressure in the vocal tract.</def><citation>[http://en.wikipedia.org/wiki/Distinctive_feature] Date accessed: 12-Feb-2009</citation>" +
				"<item id='vPASonorantPositive' guid=\"d190d8a1-f058-4a9c-b16e-f16b525b041c\" type='value'><abbrev ws='en'>+</abbrev><term ws='en'>positive</term><fs id='vPASonorantPositiveFS' type='Phon' typeguid=\"0ea53dd6-79f5-4fac-a672-f2f7026d8d15\"><f name='fPASonorant'><sym value='+'/></f></fs></item>" +
				"<item id='vPASonorantNegative' guid=\"ff4a2434-54e9-4e3d-bf11-cadfedef1765\" type='value'><abbrev ws='en'>-</abbrev><term ws='en'>negative</term><fs id='vPASonorantNegativeFS' type='Phon' typeguid=\"0ea53dd6-79f5-4fac-a672-f2f7026d8d15\"><f name='fPASonorant'><sym value='-'/></f></fs></item></item>" +
				"<item id=\"fPASyllabic\" guid=\"0acbdb9b-28bc-41c2-9706-5873bb3b12e5\" type=\"feature\"><abbrev ws=\"en\">syl</abbrev><term ws=\"en\">syllabic</term><def ws=\"en\">Syllabic segments may function as the nucleus of a syllable, while their counterparts, the [-syl] segments, may not.</def><citation>[http://en.wikipedia.org/wiki/Distinctive_feature] Date accessed: 12-Feb-2009</citation>" +
				"<item id='vPASyllabicPositive' guid=\"31929bd3-e2f8-4ea7-beed-527404d34e74\" type='value'><abbrev ws='en'>+</abbrev><term ws='en'>positive</term><fs id='vPASyllabicPositiveFS' type='Phon' typeguid=\"0ea53dd6-79f5-4fac-a672-f2f7026d8d15\"><f name='fPASyllabic'><sym value='+'/></f></fs></item>" +
				"<item id='vPASyllabicNegative' guid=\"73a064b8-21f0-479a-b5d2-142f30297ffa\" type='value'><abbrev ws='en'>-</abbrev><term ws='en'>negative</term><fs id='vPASyllabicNegativeFS' type='Phon' typeguid=\"0ea53dd6-79f5-4fac-a672-f2f7026d8d15\"><f name='fPASyllabic'><sym value='-'/></f></fs></item></item></item>",
				Environment.NewLine);

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests adding closed features to feature system and to a feature structure
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void TestPhonologicalFeatures()
		{
			ILangProject lp = m_cache.LangProject;
			var actionHandler = m_cache.ServiceLocator.GetInstance<IActionHandler>();
			actionHandler.BeginUndoTask("Undo doing stuff", "Redo doing stuff");

			// ==================================
			// set up phonological feature system
			// ==================================
			// Set up the xml fs description
			XmlDocument doc = new XmlDocument();
			doc.LoadXml(ksPhFS1);
			// get [consonantal:positive]
			XmlNode itemValue = doc.SelectSingleNode("/item/item[1]/item[1]");

			// Add the feature for first time
			IFsFeatureSystem phfs = lp.PhFeatureSystemOA;
			phfs.AddFeatureFromXml(itemValue);
			// get [consonantal:negative]
			itemValue = doc.SelectSingleNode("/item/item[1]/item[2]");
			phfs.AddFeatureFromXml(itemValue);
			// add sonorant feature
			itemValue = doc.SelectSingleNode("/item/item[2]/item[1]");
			phfs.AddFeatureFromXml(itemValue);
			itemValue = doc.SelectSingleNode("/item/item[2]/item[2]");
			phfs.AddFeatureFromXml(itemValue);
			// add syllabic feature
			itemValue = doc.SelectSingleNode("/item/item[3]/item[1]");
			phfs.AddFeatureFromXml(itemValue);
			itemValue = doc.SelectSingleNode("/item/item[3]/item[2]");
			phfs.AddFeatureFromXml(itemValue);

			// ===============
			// set up phonemes
			// ===============
			var phonData = lp.PhonologicalDataOA;

			var phonemeset = m_cache.ServiceLocator.GetInstance<IPhPhonemeSetFactory>().Create();
			phonData.PhonemeSetsOS.Add(phonemeset);
			var phonemeM = m_cache.ServiceLocator.GetInstance<IPhPhonemeFactory>().Create();
			phonemeset.PhonemesOC.Add(phonemeM);
			phonemeM.Name.set_String(m_cache.DefaultUserWs, "m");
			phonemeM.FeaturesOA = m_cache.ServiceLocator.GetInstance<IFsFeatStrucFactory>().Create();
			var fsM = phonemeM.FeaturesOA;
			var closedValue = m_cache.ServiceLocator.GetInstance<IFsClosedValueFactory>().Create();
			fsM.FeatureSpecsOC.Add(closedValue);
			var feat = phfs.FeaturesOC.First() as IFsClosedFeature;
			closedValue.FeatureRA = feat;
			closedValue.ValueRA = feat.ValuesOC.First();
			closedValue = m_cache.ServiceLocator.GetInstance<IFsClosedValueFactory>().Create();
			fsM.FeatureSpecsOC.Add(closedValue);
			feat = phfs.FeaturesOC.ElementAt(1) as IFsClosedFeature;
			closedValue.FeatureRA = feat;
			closedValue.ValueRA = feat.ValuesOC.First();
			var phonemeP = m_cache.ServiceLocator.GetInstance<IPhPhonemeFactory>().Create();
			phonemeset.PhonemesOC.Add(phonemeP);
			phonemeP.Name.set_String(m_cache.DefaultUserWs, "p");
			phonemeP.FeaturesOA = m_cache.ServiceLocator.GetInstance<IFsFeatStrucFactory>().Create();
			var fsP = phonemeP.FeaturesOA;
			closedValue = m_cache.ServiceLocator.GetInstance<IFsClosedValueFactory>().Create();
			fsP.FeatureSpecsOC.Add(closedValue);
			feat = phfs.FeaturesOC.First() as IFsClosedFeature;
			closedValue.FeatureRA = feat;
			closedValue.ValueRA = feat.ValuesOC.First();
			closedValue = m_cache.ServiceLocator.GetInstance<IFsClosedValueFactory>().Create();
			fsP.FeatureSpecsOC.Add(closedValue);
			feat = phfs.FeaturesOC.ElementAt(1) as IFsClosedFeature;
			closedValue.FeatureRA = feat;
			closedValue.ValueRA = feat.ValuesOC.Last();

			var phonemeB = m_cache.ServiceLocator.GetInstance<IPhPhonemeFactory>().Create();
			phonemeset.PhonemesOC.Add(phonemeB);
			phonemeB.Name.set_String(m_cache.DefaultUserWs, "b");
			phonemeB.FeaturesOA = m_cache.ServiceLocator.GetInstance<IFsFeatStrucFactory>().Create();
			var fsB = phonemeB.FeaturesOA;
			closedValue = m_cache.ServiceLocator.GetInstance<IFsClosedValueFactory>().Create();
			fsB.FeatureSpecsOC.Add(closedValue);
			feat = phfs.FeaturesOC.First() as IFsClosedFeature;
			closedValue.FeatureRA = feat;
			closedValue.ValueRA = feat.ValuesOC.First();
			closedValue = m_cache.ServiceLocator.GetInstance<IFsClosedValueFactory>().Create();
			fsB.FeatureSpecsOC.Add(closedValue);
			feat = phfs.FeaturesOC.ElementAt(1) as IFsClosedFeature;
			closedValue.FeatureRA = feat;
			closedValue.ValueRA = feat.ValuesOC.Last();

			// ====================
			// set up natural class
			// ====================
			var natClass = m_cache.ServiceLocator.GetInstance<IPhNCSegmentsFactory>().Create();
			phonData.NaturalClassesOS.Add(natClass);
			natClass.SegmentsRC.Add(phonemeM);
			natClass.SegmentsRC.Add(phonemeP);
			natClass.SegmentsRC.Add(phonemeB);

			using (var cache = m_cache = LcmCache.CreateCacheWithNewBlankLangProj(
					new TestProjectId(BackendProviderType.kMemoryOnly, "MemoryOnly.mem"),
					"en", "fr", "en", new DummyLcmUI(), TestDirectoryFinder.LcmDirectories, new LcmSettings()))
			{
				var services = new PhonologyServices(m_cache);
				XDocument xdoc = services.ExportPhonologyAsXml();
				XDocument xdoc2 = null;
				var xml = xdoc.ToString();
				using (var rdr = new StringReader(xml))
				{
					var services2 = new PhonologyServices(cache);
					services2.ImportPhonologyFromXml(rdr);
					xdoc2 = services2.ExportPhonologyAsXml();
				}
				var xml2 = xdoc2.ToString();
				TestEqual(m_cache.LanguageProject.PhonologicalDataOA, cache.LanguageProject.PhonologicalDataOA);
				TestEqual(xdoc, xdoc2);
			}
		}

		[Test]
		public void TestSpanish()
		{
			var services = new PhonologyServices(m_cache);
			var xml = @"<?xml version=""1.0"" encoding=""utf-8"" standalone=""yes""?>
<M3Dump>
  <PhPhonData Id=""11886"">
    <Environments>
      <PhEnvironment Id=""381"" StringRepresentation=""/n_"" LeftContext=""0"" RightContext=""0"">
        <Name>***</Name>
        <Description>***</Description>
      </PhEnvironment>
      <PhEnvironment Id=""7882"" StringRepresentation=""/[C]_"" LeftContext=""0"" RightContext=""0"">
        <Name>***</Name>
        <Description>***</Description>
      </PhEnvironment>
    </Environments>
    <NaturalClasses>
      <PhNCSegments Id=""12082"">
        <Name>Consonants</Name>
        <Description>Consonants</Description>
        <Abbreviation>C</Abbreviation>
        <Segments dst=""137"" />
        <Segments dst=""4599"" />
        <Segments dst=""5872"" />
        <Segments dst=""9651"" />
        <Segments dst=""12095"" />
        <Segments dst=""12096"" />
        <Segments dst=""12098"" />
        <Segments dst=""12099"" />
        <Segments dst=""12101"" />
        <Segments dst=""12102"" />
        <Segments dst=""12103"" />
        <Segments dst=""12104"" />
        <Segments dst=""12105"" />
        <Segments dst=""12107"" />
        <Segments dst=""12108"" />
        <Segments dst=""12109"" />
        <Segments dst=""12126"" />
        <Segments dst=""12128"" />
        <Segments dst=""12129"" />
        <Segments dst=""12130"" />
        <Segments dst=""12131"" />
        <Segments dst=""12132"" />
        <Segments dst=""11429"" />
      </PhNCSegments>
      <PhNCSegments Id=""12083"">
        <Name>Vowels</Name>
        <Description>Vowels</Description>
        <Abbreviation>V</Abbreviation>
        <Segments dst=""12094"" />
        <Segments dst=""12097"" />
        <Segments dst=""12100"" />
        <Segments dst=""12106"" />
        <Segments dst=""12127"" />
      </PhNCSegments>
    </NaturalClasses>
    <Contexts />
    <PhonemeSets>
      <PhPhonemeSet Id=""11887"">
        <Name>Main phoneme set</Name>
        <Description>Main phoneme set</Description>
        <Phonemes>
          <PhPhoneme Id=""137"">
            <Name>ñ</Name>
            <Description>Voiced palatal nasal</Description>
            <Codes>
              <PhCode Id=""1406"">
                <Representation>ñ</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>ɲ</BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""4599"">
            <Name>rr</Name>
            <Description>Voiced alveolar trill</Description>
            <Codes>
              <PhCode Id=""8579"">
                <Representation>rr</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>r</BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""5872"">
            <Name>ch</Name>
            <Description>Voiceless alveolar affricate</Description>
            <Codes>
              <PhCode Id=""7539"">
                <Representation>ch</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>tʃ</BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""9651"">
            <Name>y</Name>
            <Description>Voiced palatal approximant</Description>
            <Codes>
              <PhCode Id=""6368"">
                <Representation>y</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>j</BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12094"">
            <Name>a</Name>
            <Description>low central unrounded vowel</Description>
            <Codes>
              <PhCode Id=""11892"">
                <Representation>a</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12095"">
            <Name>b</Name>
            <Description>voiced bilabial stop</Description>
            <Codes>
              <PhCode Id=""11896"">
                <Representation>b</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12096"">
            <Name>d</Name>
            <Description>voiced alveolar stop</Description>
            <Codes>
              <PhCode Id=""11898"">
                <Representation>d</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12097"">
            <Name>e</Name>
            <Description>mid front unrounded vowel</Description>
            <Codes>
              <PhCode Id=""11891"">
                <Representation>e</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12098"">
            <Name>f</Name>
            <Description>voiceless labiodental fricative</Description>
            <Codes>
              <PhCode Id=""11901"">
                <Representation>f</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12099"">
            <Name>g</Name>
            <Description>voiced velar stop</Description>
            <Codes>
              <PhCode Id=""11900"">
                <Representation>g</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12100"">
            <Name>i</Name>
            <Description>high front unrounded vowel</Description>
            <Codes>
              <PhCode Id=""11890"">
                <Representation>i</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12101"">
            <Name>j</Name>
            <Description>palatal approximant</Description>
            <Codes>
              <PhCode Id=""11912"">
                <Representation>j</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12102"">
            <Name>k</Name>
            <Description>voiceless velar stop</Description>
            <Codes>
              <PhCode Id=""11899"">
                <Representation>k</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12103"">
            <Name>l</Name>
            <Description>alveolar lateral</Description>
            <Codes>
              <PhCode Id=""11909"">
                <Representation>l</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12104"">
            <Name>m</Name>
            <Description>bilabial nasal</Description>
            <Codes>
              <PhCode Id=""11906"">
                <Representation>m</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12105"">
            <Name>n</Name>
            <Description>alveolar nasal</Description>
            <Codes>
              <PhCode Id=""11907"">
                <Representation>n</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12106"">
            <Name>o</Name>
            <Description>mid back rounded vowel</Description>
            <Codes>
              <PhCode Id=""11893"">
                <Representation>o</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12107"">
            <Name>p</Name>
            <Description>voiceless bilabial stop</Description>
            <Codes>
              <PhCode Id=""11895"">
                <Representation>p</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12108"">
            <Name>r</Name>
            <Description>alveolar flap</Description>
            <Codes>
              <PhCode Id=""11910"">
                <Representation>r</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12109"">
            <Name>s</Name>
            <Description>voiceless alveolar fricative</Description>
            <Codes>
              <PhCode Id=""11903"">
                <Representation>s</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12126"">
            <Name>t</Name>
            <Description>voiceless alveolar stop</Description>
            <Codes>
              <PhCode Id=""11897"">
                <Representation>t</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12127"">
            <Name>u</Name>
            <Description>high back rounded vowel</Description>
            <Codes>
              <PhCode Id=""11894"">
                <Representation>u</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12128"">
            <Name>v</Name>
            <Description>voiced labiodental fricative</Description>
            <Codes>
              <PhCode Id=""11902"">
                <Representation>v</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12129"">
            <Name>w</Name>
            <Description>labiovelar approximant</Description>
            <Codes>
              <PhCode Id=""11911"">
                <Representation>w</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12130"">
            <Name>x</Name>
            <Description>voiceless velar fricative</Description>
            <Codes>
              <PhCode Id=""11905"">
                <Representation>x</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12131"">
            <Name>z</Name>
            <Description>voiced alveolar fricative</Description>
            <Codes>
              <PhCode Id=""11904"">
                <Representation>z</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol />
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12132"">
            <Name>ŋ</Name>
            <Description>velar nasal</Description>
            <Codes>
              <PhCode Id=""11908"">
                <Representation>ng</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>ŋ</BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""11429"">
            <Name>q</Name>
            <Description>Voiceless velar plosive</Description>
            <Codes>
              <PhCode Id=""2393"">
                <Representation>q</Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>k</BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
        </Phonemes>
        <BoundaryMarkers>
          <PhBdryMarker Id=""2636"" Guid=""3bde17ce-e39a-4bae-8a5c-a8d96fd4cb56"">
            <Name>+</Name>
            <Codes>
              <PhCode Id=""11889"">
                <Representation>+</Representation>
              </PhCode>
            </Codes>
          </PhBdryMarker>
          <PhBdryMarker Id=""5757"" Guid=""7db635e0-9ef3-4167-a594-12551ed89aaa"">
            <Name>#</Name>
            <Codes>
              <PhCode Id=""11888"">
                <Representation>#</Representation>
              </PhCode>
            </Codes>
          </PhBdryMarker>
        </BoundaryMarkers>
      </PhPhonemeSet>
    </PhonemeSets>
    <FeatureConstraints />
    <PhonRules />
    <PhonRuleFeats />
    <PhIters />
    <PhIters />
    <PhIters />
    <PhIters />
    <PhIters />
    <PhIters />
  </PhPhonData>
  <PhFeatureSystem Id=""12088"">
    <Types />
    <Features />
  </PhFeatureSystem>
</M3Dump>";
			using (var rdr = new StringReader(xml))
			{
				services.ImportPhonologyFromXml(rdr);
				var xdoc2 = services.ExportPhonologyAsXml();
				var xml2 = xdoc2.ToString();
				// Assert.AreEqual(xml, xml2);
				using (var rdr2 = new StringReader(xml2))
				{
					services.ImportPhonologyFromXml(rdr2);
				}
			}
		}

	}
}
