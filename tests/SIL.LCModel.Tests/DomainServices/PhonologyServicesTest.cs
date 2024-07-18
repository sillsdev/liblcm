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
using SIL.LCModel.Infrastructure.Impl;

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
				TestXml(xdoc, xdoc2);
			}
		}

		private void TestXml(XDocument xdoc, XDocument xdoc2)
		{
			IDictionary<string, string> dstMap = new Dictionary<string, string>();
			IDictionary<string, XElement> idMap1 = new Dictionary<string, XElement>();
			IDictionary<string, XElement> idMap2 = new Dictionary<string, XElement>();
			TestXml(xdoc.Elements(), xdoc2.Elements(), dstMap, idMap1, idMap2);
			// Test dst references.
			foreach (var dst1 in dstMap.Keys)
			{
				var dst2 = dstMap[dst1];
				TestXml(idMap1[dst1], idMap2[dst2], dstMap, idMap1, idMap2);
			}
		}

		private void TestXml(IEnumerable<XElement> elements, IEnumerable<XElement> elements2,
			IDictionary<string, string> dstMap,
			IDictionary<string, XElement> idMap1,
			IDictionary<string, XElement> idMap2)
		{
			foreach (var pair in elements.Zip(elements2, Tuple.Create))
			{
				TestXml(pair.Item1, pair.Item2, dstMap, idMap1, idMap2);
			}
		}

		private void TestXml(XElement element, XElement element2,
			IDictionary<string, string> dstMap,
			IDictionary<string, XElement> idMap1,
			IDictionary<string, XElement> idMap2)
		{
			// Check attributes.
			Assert.AreEqual(element.Attributes().Count(), element2.Attributes().Count());
			foreach (var attr in element.Attributes())
			{
				XName name = attr.Name;
				bool found = false;
				if (name == "Guid")
					continue;
				foreach (var attr2 in element2.Attributes())
				{
					if (attr2.Name == name)
					{
						if (name == "Id")
						{
							// Save for later.
							if (idMap1.ContainsKey(attr.Value))
								Assert.AreEqual(idMap1[attr.Value], element);
							else
								idMap1[attr.Value] = element;
							if (idMap2.ContainsKey(attr2.Value))
								Assert.AreEqual(idMap2[attr2.Value], element2);
							else
								idMap2[attr2.Value] = element2;
						}
						else if (name == "dst")
						{
							// Save for later.
							if (dstMap.ContainsKey(attr.Value))
								Assert.AreEqual(dstMap[attr.Value], attr2.Value);
							else
								dstMap[attr.Value] = attr2.Value;
						}
						else
						{
							Assert.AreEqual(attr.Value, attr2.Value);
						}
						found = true;
					}
				}
				Assert.IsTrue(found);
			}
			// Check elements.
			TestXml(element.Elements(), element2.Elements(), dstMap, idMap1, idMap2);
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
			Assert.AreEqual(phonemeSets.Count(), phonemeSets2.Count());
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

		private void TestEqual(IPhPhoneme phoneme, IPhPhoneme phoneme2)
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
		public void TestSpanish()
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
				TestXml(xdoc, xdoc2);
			}
		}
	}
}
