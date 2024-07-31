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
				// Compare original XML to new XML.
				TestXml(xdoc, xdoc2);
			}
		}

		private void TestXml(string xml, string vernWs)
		{
			var xdoc = XDocument.Parse(xml);
			NonUndoableUnitOfWorkHelper.Do(m_cache.ActionHandlerAccessor, () =>
			{
				m_cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem =
					m_cache.ServiceLocator.WritingSystemManager.Get(vernWs);
			});
			var services = new PhonologyServices(m_cache);
			using (var rdr = new StringReader(xml))
			{
				services.ImportPhonologyFromXml(rdr);
				var xdoc2 = services.ExportPhonologyAsXml();
				var xml2 = xdoc2.ToString();
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
			Assert.AreEqual(elements.Count(), elements2.Count());
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
						else if (name == "dst" || name == "Feature" || name == "Value")
						{
							// Save for later.
							if (dstMap.ContainsKey(attr.Value))
								Assert.AreEqual(dstMap[attr.Value], attr2.Value);
							else
								dstMap[attr.Value] = attr2.Value;
						}
						else
						{
							Assert.AreEqual(attr.Value, attr2.Value, "Attribute " + name + " has different values.");
						}
						found = true;
					}
				}
				Assert.IsTrue(found);
			}
			// Check elements.
			TestXml(element.Elements(), element2.Elements(), dstMap, idMap1, idMap2);
		}

		string SpanishPhonology = @"<Phonology Version=""1"" DefaultVernWs=""es"">
  <PhPhonData Id=""11847"">
    <Environments>
      <PhEnvironment Id=""379"" LeftContext=""0"" RightContext=""0"">
        <StringRepresentation>
          <Str>
            <Run ws=""es"" underline=""none"">/n_</Run>
          </Str>
        </StringRepresentation>
      </PhEnvironment>
      <PhEnvironment Id=""7858"" LeftContext=""0"" RightContext=""0"">
        <StringRepresentation>
          <Str>
            <Run ws=""es"" underline=""none"">/[C]_</Run>
          </Str>
        </StringRepresentation>
      </PhEnvironment>
    </Environments>
    <NaturalClasses>
      <PhNCSegments Id=""12043"">
        <Name>
          <AUni ws=""en"">Consonants</AUni>
        </Name>
        <Description>
          <AStr ws=""en"">
            <Run ws=""en"">Consonants</Run>
          </AStr>
        </Description>
        <Abbreviation>
          <AUni ws=""en"">C</AUni>
        </Abbreviation>
        <Segments dst=""136"" />
        <Segments dst=""4583"" />
        <Segments dst=""5849"" />
        <Segments dst=""9618"" />
        <Segments dst=""12056"" />
        <Segments dst=""12057"" />
        <Segments dst=""12059"" />
        <Segments dst=""12060"" />
        <Segments dst=""12062"" />
        <Segments dst=""12063"" />
        <Segments dst=""12064"" />
        <Segments dst=""12065"" />
        <Segments dst=""12066"" />
        <Segments dst=""12068"" />
        <Segments dst=""12069"" />
        <Segments dst=""12070"" />
        <Segments dst=""12087"" />
        <Segments dst=""12089"" />
        <Segments dst=""12090"" />
        <Segments dst=""12091"" />
        <Segments dst=""12092"" />
        <Segments dst=""12093"" />
        <Segments dst=""11391"" />
      </PhNCSegments>
      <PhNCSegments Id=""12044"">
        <Name>
          <AUni ws=""en"">Vowels</AUni>
        </Name>
        <Description>
          <AStr ws=""en"">
            <Run ws=""en"">Vowels</Run>
          </AStr>
        </Description>
        <Abbreviation>
          <AUni ws=""en"">V</AUni>
        </Abbreviation>
        <Segments dst=""12055"" />
        <Segments dst=""12058"" />
        <Segments dst=""12061"" />
        <Segments dst=""12067"" />
        <Segments dst=""12088"" />
      </PhNCSegments>
    </NaturalClasses>
    <Contexts />
    <PhonemeSets>
      <PhPhonemeSet Id=""11848"">
        <Name>
          <AUni ws=""en"">Main phoneme set</AUni>
        </Name>
        <Description>
          <AStr ws=""en"">
            <Run ws=""en"">Main phoneme set</Run>
          </AStr>
        </Description>
        <Phonemes>
          <PhPhoneme Id=""136"">
            <Name>
              <AUni ws=""es"">ñ</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">Voiced palatal nasal</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""1399"">
                <Representation>
                  <AUni ws=""es"">ñ</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en"">ɲ</Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""4583"">
            <Name>
              <AUni ws=""es"">rr</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">Voiced alveolar trill</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""8552"">
                <Representation>
                  <AUni ws=""es"">rr</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en"">r</Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""5849"">
            <Name>
              <AUni ws=""es"">ch</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">Voiceless alveolar affricate</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""7516"">
                <Representation>
                  <AUni ws=""es"">ch</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en"">tʃ</Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""9618"">
            <Name>
              <AUni ws=""es"">y</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">Voiced palatal approximant</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""6345"">
                <Representation>
                  <AUni ws=""es"">y</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en"">j</Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12055"">
            <Name>
              <AUni ws=""en"">a</AUni>
              <AUni ws=""es"">a</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">low central unrounded vowel</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11853"">
                <Representation>
                  <AUni ws=""en"">a</AUni>
                  <AUni ws=""es"">a</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12056"">
            <Name>
              <AUni ws=""en"">b</AUni>
              <AUni ws=""es"">b</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">voiced bilabial stop</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11857"">
                <Representation>
                  <AUni ws=""en"">b</AUni>
                  <AUni ws=""es"">b</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12057"">
            <Name>
              <AUni ws=""en"">d</AUni>
              <AUni ws=""es"">d</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">voiced alveolar stop</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11859"">
                <Representation>
                  <AUni ws=""en"">d</AUni>
                  <AUni ws=""es"">d</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12058"">
            <Name>
              <AUni ws=""en"">e</AUni>
              <AUni ws=""es"">e</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">mid front unrounded vowel</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11852"">
                <Representation>
                  <AUni ws=""en"">e</AUni>
                  <AUni ws=""es"">e</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12059"">
            <Name>
              <AUni ws=""en"">f</AUni>
              <AUni ws=""es"">f</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">voiceless labiodental fricative</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11862"">
                <Representation>
                  <AUni ws=""en"">f</AUni>
                  <AUni ws=""es"">f</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12060"">
            <Name>
              <AUni ws=""en"">g</AUni>
              <AUni ws=""es"">g</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">voiced velar stop</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11861"">
                <Representation>
                  <AUni ws=""en"">g</AUni>
                  <AUni ws=""es"">g</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12061"">
            <Name>
              <AUni ws=""en"">i</AUni>
              <AUni ws=""es"">i</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">high front unrounded vowel</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11851"">
                <Representation>
                  <AUni ws=""en"">i</AUni>
                  <AUni ws=""es"">i</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12062"">
            <Name>
              <AUni ws=""en"">j</AUni>
              <AUni ws=""es"">j</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">palatal approximant</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11873"">
                <Representation>
                  <AUni ws=""en"">j</AUni>
                  <AUni ws=""es"">j</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12063"">
            <Name>
              <AUni ws=""en"">k</AUni>
              <AUni ws=""es"">k</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">voiceless velar stop</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11860"">
                <Representation>
                  <AUni ws=""en"">k</AUni>
                  <AUni ws=""es"">k</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12064"">
            <Name>
              <AUni ws=""en"">l</AUni>
              <AUni ws=""es"">l</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">alveolar lateral</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11870"">
                <Representation>
                  <AUni ws=""en"">l</AUni>
                  <AUni ws=""es"">l</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12065"">
            <Name>
              <AUni ws=""en"">m</AUni>
              <AUni ws=""es"">m</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">bilabial nasal</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11867"">
                <Representation>
                  <AUni ws=""en"">m</AUni>
                  <AUni ws=""es"">m</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12066"">
            <Name>
              <AUni ws=""en"">n</AUni>
              <AUni ws=""es"">n</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">alveolar nasal</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11868"">
                <Representation>
                  <AUni ws=""en"">n</AUni>
                  <AUni ws=""es"">n</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12067"">
            <Name>
              <AUni ws=""en"">o</AUni>
              <AUni ws=""es"">o</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">mid back rounded vowel</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11854"">
                <Representation>
                  <AUni ws=""en"">o</AUni>
                  <AUni ws=""es"">o</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12068"">
            <Name>
              <AUni ws=""en"">p</AUni>
              <AUni ws=""es"">p</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">voiceless bilabial stop</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11856"">
                <Representation>
                  <AUni ws=""en"">p</AUni>
                  <AUni ws=""es"">p</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12069"">
            <Name>
              <AUni ws=""en"">r</AUni>
              <AUni ws=""es"">r</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">alveolar flap</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11871"">
                <Representation>
                  <AUni ws=""en"">r</AUni>
                  <AUni ws=""es"">r</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12070"">
            <Name>
              <AUni ws=""en"">s</AUni>
              <AUni ws=""es"">s</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">voiceless alveolar fricative</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11864"">
                <Representation>
                  <AUni ws=""en"">s</AUni>
                  <AUni ws=""es"">s</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12087"">
            <Name>
              <AUni ws=""en"">t</AUni>
              <AUni ws=""es"">t</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">voiceless alveolar stop</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11858"">
                <Representation>
                  <AUni ws=""en"">t</AUni>
                  <AUni ws=""es"">t</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12088"">
            <Name>
              <AUni ws=""en"">u</AUni>
              <AUni ws=""es"">u</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">high back rounded vowel</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11855"">
                <Representation>
                  <AUni ws=""en"">u</AUni>
                  <AUni ws=""es"">u</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12089"">
            <Name>
              <AUni ws=""en"">v</AUni>
              <AUni ws=""es"">v</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">voiced labiodental fricative</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11863"">
                <Representation>
                  <AUni ws=""en"">v</AUni>
                  <AUni ws=""es"">v</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12090"">
            <Name>
              <AUni ws=""en"">w</AUni>
              <AUni ws=""es"">w</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">labiovelar approximant</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11872"">
                <Representation>
                  <AUni ws=""en"">w</AUni>
                  <AUni ws=""es"">w</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12091"">
            <Name>
              <AUni ws=""en"">x</AUni>
              <AUni ws=""es"">x</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">voiceless velar fricative</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11866"">
                <Representation>
                  <AUni ws=""en"">x</AUni>
                  <AUni ws=""es"">x</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12092"">
            <Name>
              <AUni ws=""en"">z</AUni>
              <AUni ws=""es"">z</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">voiced alveolar fricative</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11865"">
                <Representation>
                  <AUni ws=""en"">z</AUni>
                  <AUni ws=""es"">z</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en""></Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""12093"">
            <Name>
              <AUni ws=""en"">ŋ</AUni>
              <AUni ws=""es"">ŋ</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">velar nasal</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""11869"">
                <Representation>
                  <AUni ws=""en"">ŋ</AUni>
                  <AUni ws=""es"">ng</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""es"">ŋ</Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
          <PhPhoneme Id=""11391"">
            <Name>
              <AUni ws=""es"">q</AUni>
            </Name>
            <Description>
              <AStr ws=""en"">
                <Run ws=""en"">Voiceless velar plosive</Run>
              </AStr>
            </Description>
            <Codes>
              <PhCode Id=""2380"">
                <Representation>
                  <AUni ws=""es"">q</AUni>
                </Representation>
              </PhCode>
            </Codes>
            <BasicIPASymbol>
              <Str>
                <Run ws=""en"">k</Run>
              </Str>
            </BasicIPASymbol>
            <PhonologicalFeatures />
          </PhPhoneme>
        </Phonemes>
        <BoundaryMarkers>
          <PhBdryMarker Id=""2622"" Guid=""3bde17ce-e39a-4bae-8a5c-a8d96fd4cb56"">
            <Name>
              <AUni ws=""en"">+</AUni>
              <AUni ws=""es"">+</AUni>
            </Name>
            <Codes>
              <PhCode Id=""11850"">
                <Representation>
                  <AUni ws=""en"">+</AUni>
                  <AUni ws=""es"">+</AUni>
                </Representation>
              </PhCode>
            </Codes>
          </PhBdryMarker>
          <PhBdryMarker Id=""5736"" Guid=""7db635e0-9ef3-4167-a594-12551ed89aaa"">
            <Name>
              <AUni ws=""en"">#</AUni>
              <AUni ws=""es"">#</AUni>
            </Name>
            <Codes>
              <PhCode Id=""11849"">
                <Representation>
                  <AUni ws=""en"">#</AUni>
                  <AUni ws=""es"">#</AUni>
                </Representation>
              </PhCode>
            </Codes>
          </PhBdryMarker>
        </BoundaryMarkers>
      </PhPhonemeSet>
    </PhonemeSets>
    <FeatureConstraints />
    <PhonRules />
    <PhIters />
    <PhIters />
    <PhIters />
    <PhIters />
    <PhIters />
    <PhIters />
  </PhPhonData>
  <PhFeatureSystem Id=""12049"">
    <Types />
    <Features />
  </PhFeatureSystem>
</Phonology>";

		[Test]
		public void TestSpanish()
		{
			TestXml(SpanishPhonology, "es");
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
				TestXml(xdoc, xdoc2);
			}
		}
	}
}
