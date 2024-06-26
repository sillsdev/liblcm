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
				var services = new PhonologyServices(cache);
				XDocument xdoc = services.ExportPhonologyAsXml();
				XDocument xdoc2 = null;
				var xml = xdoc.ToString();
				using (var rdr = new StringReader(xml))
				{
					var vernWs = cache.ServiceLocator.WritingSystemManager.Get(cache.DefaultVernWs);
					SetDefaultVernacularWritingSystem(m_cache, vernWs);
					var services2 = new PhonologyServices(m_cache, vernWs.Id);
					services2.ImportPhonologyFromXml(rdr);
					xdoc2 = services2.ExportPhonologyAsXml();
				}
				var xml2 = xdoc2.ToString();
				TestEqual(cache.LanguageProject.PhonologicalDataOA, m_cache.LanguageProject.PhonologicalDataOA);
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
