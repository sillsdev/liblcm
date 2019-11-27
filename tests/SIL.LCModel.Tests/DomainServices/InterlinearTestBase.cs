// Copyright (c) 2015-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml;
using NUnit.Framework;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;
using SIL.Xml;

namespace SIL.LCModel.DomainServices
{
	/// <inheritdoc />
	public abstract class InterlinearTestBase : MemoryOnlyBackendProviderRestoredForEachTestTestBase
	{
		/// <summary>
		/// This is used to record the information we (may) want to verify about a part of a paragraph.
		/// </summary>
		internal interface IMockStTxtParaAnnotation
		{
			int Hvo { get; set; }
			int BeginOffset { get; set; }
		}

		/// <summary />
		protected IText LoadTestText(string textsDefinitionsPath, int index, XmlDocument textsDefn)
		{
			TextBuilder tb;
			return LoadTestText(textsDefinitionsPath, index, textsDefn, out tb);
		}

		/// <summary />
		protected IText LoadTestText(string textsDefinitionsPath, int index, XmlDocument textsDefn, out TextBuilder tb)
		{
			textsDefn.Load(textsDefinitionsPath);
			XmlNode text1Defn = textsDefn.SelectSingleNode("/Texts6001/Text[" + index + "]");
			tb = new TextBuilder(Cache);
			return tb.BuildText(text1Defn);
		}

		/// <summary>
		/// Given the internal kind of node list that XmlDocuments use, convert it to a regular list of XmlNodes.
		/// </summary>
		internal static List<XmlNode> NodeListToNodes(XmlNodeList nl)
		{
			List<XmlNode> nodes = new List<XmlNode>();
			foreach (XmlNode node in nl)
				nodes.Add(node);
			return nodes;
		}

		internal static void MoveSiblingNodes(XmlNode srcStartNode, XmlNode targetParentNode, XmlNode limChildNode)
		{
			XmlNode srcParentNode = srcStartNode.ParentNode;
			List<XmlNode> siblingNodes = NodeListToNodes(srcParentNode.ChildNodes);
			bool fStartFound = false;
			foreach (XmlNode siblingNode in siblingNodes)
			{
				if (!fStartFound && siblingNode != srcStartNode)
					continue;
				fStartFound = true;
				// break after we've added the limiting segment form node.
				XmlNode orphan = srcParentNode.RemoveChild(siblingNode);
				targetParentNode.AppendChild(orphan);
				if (siblingNode == limChildNode)
					break;
			}
		}

		private static int GetIndexAmongSiblings(XmlNode node)
		{
			XmlNode parent = node.ParentNode;
			if (parent != null)
				return node.SelectNodes("./preceding-sibling::" + node.LocalName)?.Count ?? -1;
			return -1;
		}

		/// <summary>
		/// This abstract class can be used to validate any paragraph structure against an existing IStTxtPara structure.
		/// </summary>
		public abstract class ParagraphValidator
		{
			/// <summary>
			///
			/// </summary>
			protected ParagraphValidator()
			{
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="expectedParaInfo"></param>
			/// <param name="actualParaInfo"></param>
			public virtual void ValidateParagraphs(XmlNode expectedParaInfo, IStTxtPara actualParaInfo)
			{
				string paraContext = String.Format("Para({0})", GetParagraphContext(expectedParaInfo));
				ValidateParagraphSegments(expectedParaInfo, actualParaInfo, paraContext);
			}

			/// <summary>
			/// Validate the actual analysis of the paragraph against what is expected, indicated by the expectedParaInfo,
			/// which is an XmlNode from our test data file for an StTxtPara.
			/// </summary>
			/// <param name="expectedParaInfo"></param>
			/// <param name="actualParaInfo"></param>
			/// <param name="paraContext"></param>
			protected virtual void ValidateParagraphSegments(XmlNode expectedParaInfo, IStTxtPara actualParaInfo, string paraContext)
			{
				var expectedSegments = GetExpectedSegments(expectedParaInfo);
				var actualSegments = actualParaInfo.SegmentsOS.ToList();
				// Validate Segments
				Assert.AreEqual(expectedSegments.Count, actualSegments.Count,
					String.Format("Expect the same Segment count in {0}.", paraContext));
				using (var actualSegEnumerator = actualSegments.GetEnumerator())
				{
					int iSegment = 0;
					foreach (XmlNode expectedSegment in expectedSegments)
					{
						string segmentContext = String.Format(paraContext + "/Segment({0})", iSegment);
						actualSegEnumerator.MoveNext();
						ISegment actualSegment = actualSegEnumerator.Current;
						ValidateSegmentOuterElements(expectedSegment, actualSegment, segmentContext);
						ValidateSegmentSegForms(expectedSegment, actualSegment, segmentContext);
						iSegment++;
					}
				}
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="expectedSegment"></param>
			/// <param name="actualSegment"></param>
			/// <param name="segmentContext"></param>
			protected virtual void ValidateSegmentOuterElements(object expectedSegment, ISegment actualSegment, string segmentContext)
			{
				// base override
				return;
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="expectedSegment"></param>
			/// <param name="actualSegment"></param>
			/// <param name="segmentContext"></param>
			protected virtual void ValidateSegmentSegForms(object expectedSegment, ISegment actualSegment, string segmentContext)
			{
				ArrayList expectedSegForms = GetExpectedSegmentForms(expectedSegment);
				IList<IAnalysis> actualSegForms = GetActualSegmentForms(actualSegment);
				// Validate Segments
				Assert.AreEqual(expectedSegForms.Count, actualSegForms.Count,
					String.Format("Expect the same SegmentForm count in {0}.", segmentContext));
				using (IEnumerator<IAnalysis> actualSegFormEnumerator = actualSegForms.GetEnumerator())
				{
					int iSegForm = 0;
					foreach (object expectedSegForm in expectedSegForms)
					{
						string segFormContext = String.Format(segmentContext + "/SegForm({0})", iSegForm);
						actualSegFormEnumerator.MoveNext();
						IAnalysis actualSegForm = actualSegFormEnumerator.Current;
						ValidateSegFormOuterElements(expectedSegForm, actualSegForm, segFormContext);
						ValidateSegForms(expectedSegForm, actualSegForm, segFormContext);
						iSegForm++;
					}
				}
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="expectedSegForm"></param>
			/// <param name="actualSegForm"></param>
			/// <param name="segFormContext"></param>
			protected virtual void ValidateSegFormOuterElements(object expectedSegForm, IAnalysis actualSegForm, string segFormContext)
			{
				return;
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="expectedSegForm"></param>
			/// <param name="actualSegForm"></param>
			/// <param name="segFormContext"></param>
			protected virtual void ValidateSegForms(object expectedSegForm, IAnalysis actualSegForm, string segFormContext)
			{
				return;
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="expectedParaInfo"></param>
			/// <returns></returns>
			protected abstract string GetParagraphContext(object expectedParaInfo);

			/// <summary>
			///
			/// </summary>
			/// <param name="expectedParaInfo"></param>
			/// <returns></returns>
			protected abstract List<XmlNode> GetExpectedSegments(XmlNode expectedParaInfo);
			/// <summary>
			///
			/// </summary>
			/// <param name="expectedSegment"></param>
			/// <returns></returns>
			protected abstract ArrayList GetExpectedSegmentForms(object expectedSegment);
			/// <summary>
			///
			/// </summary>
			/// <param name="actualSegment"></param>
			/// <returns></returns>
			protected abstract IList<IAnalysis> GetActualSegmentForms(ISegment actualSegment);
		}

		/// <summary>
		///
		/// </summary>
		protected class LcmValidator : ParagraphValidator
		{
			/// <summary>
			///
			/// </summary>
			protected LcmCache m_cache = null;
			/// <summary>
			///
			/// </summary>
			protected IStTxtPara m_para = null;

			internal LcmValidator(IStTxtPara para)
			{
				m_para = para;
				m_cache = m_para.Cache;
			}

			/// <summary>
			/// Verify that the text actually in the paragraph for the indicated segment and form
			/// is what is expected.
			/// </summary>
			/// <param name="tapb"></param>
			/// <param name="iSegment"></param>
			/// <param name="iSegForm"></param>
			internal static void ValidateCbaWordToBaselineWord(ParagraphAnnotatorForParagraphBuilder tapb, int iSegment, int iSegForm)
			{
				int ws;
				ITsString tssStringValue = GetTssStringValue(tapb, iSegment, iSegForm, out ws);
				IAnalysis analysis = tapb.GetAnalysis(iSegment, iSegForm);
				IWfiWordform wfInstanceOf = analysis.Wordform;
				ITsString tssWf = wfInstanceOf.Form.get_String(ws);
				string locale = wfInstanceOf.Services.WritingSystemManager.Get(ws).IcuLocale;
				var cf = new CaseFunctions(locale);
				string context = String.Format("[{0}]", tssStringValue);
				const string msg = "{0} cba mismatch in {1}.";
				Assert.AreEqual(cf.ToLower(tssStringValue.Text), cf.ToLower(tssWf.Text),
					String.Format(msg, "underlying wordform for InstanceOf", context));
			}

			internal static ITsString GetTssStringValue(ParagraphAnnotatorForParagraphBuilder tapb, int iSegment, int iSegForm, out int ws)
			{
				ITsString tssStringValue = tapb.GetSegment(iSegment).GetBaselineText(iSegForm);
				ws = TsStringUtils.GetWsAtOffset(tssStringValue, 0);
				return tssStringValue;
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="expectedParaInfo"></param>
			/// <returns></returns>
			protected override string GetParagraphContext(object expectedParaInfo)
			{
				throw new Exception("The method or operation is not implemented.");
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="expectedParaInfo"></param>
			/// <returns></returns>
			protected override List<XmlNode> GetExpectedSegments(XmlNode expectedParaInfo)
			{
				throw new Exception("The method or operation is not implemented.");
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="expectedSegment"></param>
			/// <returns></returns>
			protected override ArrayList GetExpectedSegmentForms(object expectedSegment)
			{
				throw new Exception("The method or operation is not implemented.");
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="actualSegment"></param>
			/// <returns></returns>
			protected override IList<IAnalysis> GetActualSegmentForms(ISegment actualSegment)
			{
				throw new Exception("The method or operation is not implemented.");
			}
		}

		/// <summary>
		/// This validates the actual IStTxtPara against an expected Xml paragraph structure that is based on the conceptual model.
		/// </summary>
		protected class ConceptualModelXmlParagraphValidator : LcmValidator
		{
			ParagraphBuilder m_pb = null;

			internal ConceptualModelXmlParagraphValidator(ParagraphBuilder pb) : base (pb.ActualParagraph)
			{
				m_pb = pb;
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="expectedParaInfo"></param>
			/// <returns></returns>
			protected override string GetParagraphContext(object expectedParaInfo)
			{
				XmlNode expectedParaDefn = expectedParaInfo as XmlNode;
				StringBuilder sb = new StringBuilder();
				sb.Append("[");
				sb.Append(GetIndexAmongSiblings(expectedParaDefn));
				sb.Append("] ");
				sb.Append(XmlUtils.GetMandatoryAttributeValue(expectedParaInfo as XmlNode, "id"));
				return sb.ToString();
			}


			/// <summary>
			/// Get a list of the XmlNodes (CmBaseAnnotations in the Segments16 property) of the input XmlNode,
			/// which represents an StTxtPara in our test data.
			/// </summary>
			protected override List<XmlNode> GetExpectedSegments(XmlNode expectedParaInfo)
			{
				return ParagraphBuilder.SegmentNodes(expectedParaInfo);
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="expectedSegment"></param>
			/// <returns></returns>
			protected override ArrayList GetExpectedSegmentForms(object expectedSegment)
			{
				return new ArrayList(ParagraphBuilder.SegmentFormNodes(expectedSegment as XmlNode).ToArray());
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="actualSegment"></param>
			/// <returns></returns>
			protected override IList<IAnalysis> GetActualSegmentForms(ISegment actualSegment)
			{
				return actualSegment.AnalysesRS.ToList();
			}

			private void CompareAnalyses(object expectedNode, IAnalysis actualAnalysis, string context)
			{
				var node = expectedNode as XmlNode;
				int hvoAnalysisExpected = ParagraphBuilder.GetAnalysisId(node);
				if (hvoAnalysisExpected == -1)
				{
					// default: no special analysis set, should have made a wordform or punctform with the same form as the node.
					IAnalysis analysis = (IAnalysis)m_cache.ServiceLocator.GetObject(actualAnalysis.Hvo);
					string expectedText = node.SelectSingleNode("StringValue37").InnerText;
					Assert.AreEqual(expectedText, analysis.GetForm(TsStringUtils.GetWsAtOffset(m_para.Contents, 0)).Text, context);
					string guid = ParagraphBuilder.GetAnnotationTypeGuid(node);
					if (guid == ParagraphBuilder.WficGuid)
						Assert.IsTrue(analysis is IWfiWordform, "default parse should produce wordform here " + context);
					else
						Assert.IsTrue(analysis is IPunctuationForm, "default parse should produce  punctuation form here " + context);
				}
				else
				{
					Assert.AreEqual(hvoAnalysisExpected, actualAnalysis.Hvo, context);
				}
			}

			/// <summary>
			/// Validate the "outer" information of an XmlNode representing the expected segment.
			/// That is basically its attributes, which currently just means its beginOffset.
			/// Note that these offsets are not stored in the file, but (todo!) generated while assembling the paragraph.
			/// </summary>
			protected override void ValidateSegmentOuterElements(object expectedSegment, ISegment actualSegment, string segmentContext)
			{
				XmlNode expected = expectedSegment as XmlNode; // generic argument, but this subclass always uses XmlNode for expected.
				int beginOffset = XmlUtils.GetMandatoryIntegerAttributeValue(expected, "beginOffset");
				Assert.AreEqual(beginOffset, actualSegment.BeginOffset);

			}

			/// <summary>
			/// This awkwardly named method fits into a pattern of names for validating the outer (XML attributes) and inner
			/// (XML content) parts of an XML representation of an expected value. In this case, for an IAnalysis, the only
			/// thing we can verify is that it is the right object.
			/// </summary>
			protected override void ValidateSegFormOuterElements(object expectedSegForm, IAnalysis actualSegForm, string segFormContext)
			{
				CompareAnalyses(expectedSegForm, actualSegForm, segFormContext);
			}

			internal void ValidateActualParagraphAgainstDefn()
			{
				ITsString paraContents = m_pb.GenerateParaContentFromAnnotations();
				Assert.AreEqual(paraContents.Text, m_pb.ActualParagraph.Contents.Text,
					"Expected edited text to be the same as text built from defn.");
				base.ValidateParagraphs(m_pb.ParagraphDefinition, m_pb.ActualParagraph);
			}
		}

#pragma warning disable 1591
		/// <summary>
		/// This class allows annotating wordforms in a paragraph.
		/// </summary>
		public class ParagraphAnnotator
		{
			protected IStTxtPara m_para = null;
			protected LcmCache m_cache = null;
			protected bool m_fNeedReparseParagraph = false;

			public ParagraphAnnotator(IStTxtPara para)
			{
				m_para = para;
				m_cache = m_para.Cache;
			}

			/// <summary>
			/// If the annotations have changed (e.g. due to a merge), other annotations might also be affected by parsing the
			/// paragraph again.
			/// </summary>
			internal bool NeedReparseParagraph
			{
				get { return m_fNeedReparseParagraph; }
				set { m_fNeedReparseParagraph = value; }
			}

			public void ReparseParagraph()
			{
				ParagraphParser.ParseParagraph(m_para, true, true);
				NeedReparseParagraph = false;
			}

			/// <summary>
			/// create a variant and link it to the given leMain entry
			/// and confirm this analysis on the given (monomorphemic) cba.
			/// </summary>
			/// <param name="iSegment"></param>
			/// <param name="iSegForm"></param>
			/// <param name="leMain"></param>
			/// <param name="variantType"></param>
			/// <returns>hvo of the resulting LexEntryRef</returns>
			public virtual ILexEntryRef SetVariantOf(int iSegment, int iSegForm, ILexEntry leMain, ILexEntryType variantType)
			{
				if (variantType == null)
					throw new ArgumentNullException("requires non-null variantType parameter.");
				// for now, just create the variant entry and the variant of target, treating the wordform as monomorphemic.
				ITsString tssVariantLexemeForm = GetBaselineText(iSegment, iSegForm);
				ILexEntryRef ler = leMain.CreateVariantEntryAndBackRef(variantType, tssVariantLexemeForm);
				ILexEntry variant = ler.Owner as ILexEntry;
				ArrayList morphs = new ArrayList(1);
				morphs.Add(variant.LexemeFormOA);
				BreakIntoMorphs(iSegment, iSegForm, morphs);
				ILexEntry mainEntry;
				ILexSense mainSense;
				MorphServices.GetMainEntryAndSenseStack(ler.ComponentLexemesRS.First() as IVariantComponentLexeme, out mainEntry, out mainSense);
				SetMorphSense(iSegment, iSegForm, 0, mainSense);
				return ler;
			}

			internal virtual IWfiWordform SetAlternateCase(string wordform, int iOccurrenceInParagraph, StringCaseStatus targetState)
			{
				return null; // override
			}

			public virtual IWfiWordform SetAlternateCase(int iSegment, int iSegForm, StringCaseStatus targetState, out string alternateCaseForm)
			{
				// Get actual segment form.
				var analysisActual = GetAnalysis(iSegment, iSegForm);
				int hvoActualInstanceOf;
				IWfiWordform actualWordform;
				GetRealWordformInfo(analysisActual, out hvoActualInstanceOf, out actualWordform);
				ITsString tssWordformBaseline = GetBaselineText(iSegment, iSegForm);
				// Add any relevant 'other case' forms.
				int nvar;
				int ws = tssWordformBaseline.get_Properties(0).GetIntPropValues((int)FwTextPropType.ktptWs, out nvar);
				string locale = m_cache.ServiceLocator.WritingSystemManager.Get(ws).IcuLocale;
				var cf = new CaseFunctions(locale);
				switch (targetState)
				{
					case StringCaseStatus.allLower:
						alternateCaseForm = cf.ToLower(actualWordform.Form.get_String(ws).Text);
						break;
					default:
						throw new ArgumentException("target StringCaseStatus(" + targetState + ") not yet supported.");
				}

				// Find or create the new wordform.
				IWfiWordform wfAlternateCase = WfiWordformServices.FindOrCreateWordform(m_cache, TsStringUtils.MakeString(alternateCaseForm, ws));

				// Set the annotation to this wordform.
				SetAnalysis(iSegment, iSegForm, wfAlternateCase);
				return wfAlternateCase;
			}

			internal virtual IWfiGloss SetDefaultWordGloss(string wordform, int iOccurrenceInParagraph)
			{
				return null;	// override
			}

			internal virtual IWfiGloss SetDefaultWordGloss(int iSegment, int iSegForm, out string gloss)
			{
				return SetDefaultWordGloss(iSegment, iSegForm, null, out gloss);
			}


			internal virtual IWfiGloss SetDefaultWordGloss(int iSegment, int iSegForm, IWfiAnalysis actualWfiAnalysis, out string gloss)
			{
				gloss = "";
				// Get actual segment form.
				var analysisActual = GetAnalysis(iSegment, iSegForm);

				// Get the wordform for this segmentForm
				ITsString tssWordformBaseline = GetBaselineText(iSegment, iSegForm);

				// Find or create the current analysis of the actual annotation.
				if (actualWfiAnalysis == null)
				{
					actualWfiAnalysis = FindOrCreateWfiAnalysis(analysisActual);
				}
				// Make a new gloss based upon the wordform and segmentForm path.
				var newGloss = m_cache.ServiceLocator.GetInstance<IWfiGlossFactory>().Create();
				// Add the gloss to the WfiAnalysis.
				actualWfiAnalysis.MeaningsOC.Add(newGloss);
				gloss = String.Format("{0}.{1}.{2}", iSegment, iSegForm, tssWordformBaseline.Text);
				newGloss.Form.set_String(m_cache.DefaultAnalWs, gloss);

				// Set the new expected and actual analysis.
				SetAnalysis(iSegment, iSegForm, newGloss);
				return newGloss;
			}

			/// <summary>
			/// Set the analysis at the specified segment, index.
			/// Enhance JohnT: may need it to fill in prior segments and/or annotations.
			/// </summary>
			private void SetAnalysis(int iSegment, int iSegForm, IAnalysis newGloss)
			{
				m_para.SegmentsOS[iSegment].AnalysesRS.Replace(iSegForm, 1, new ICmObject[] {newGloss});
			}


			/// <summary>
			/// Walk through the paragraph and create word glosses (formatted: wordform.segNum.segFormNum) and segment annotations.
			/// </summary>
			/// <returns>list of wordglosses for each analysis in the paragraph, including non-wordforms (ie. hvo == 0).</returns>
			protected internal virtual IList<IWfiGloss> SetupDefaultWordGlosses()
			{
				int iseg = 0;
				IList<IWfiGloss> wordGlosses = new List<IWfiGloss>();
				foreach (var seg in m_para.SegmentsOS)
				{
					int isegform = 0;
					foreach (var analysis in seg.AnalysesRS)
					{
						IWfiGloss wordGloss = null;
						if (!(analysis is IPunctuationForm))
						{
							string gloss;
							wordGloss = SetDefaultWordGloss(iseg, isegform, out gloss);
						}
						wordGlosses.Add(wordGloss);
						isegform++;
					}
					// create freeform annotations for each of the segments
					// TODO: multiple writing systems.
					ITsString tssComment;
					SetDefaultFreeTranslation(iseg, out tssComment);
					SetDefaultLiteralTranslation(iseg, out tssComment);
					SetDefaultNote(iseg, out tssComment);

					iseg++;
				}
				return wordGlosses;
			}

			/// <summary>
			/// Make up a phony Free Translation for the segment.
			/// </summary>
			public virtual void SetDefaultFreeTranslation(int iSegment, out ITsString tssComment)
			{
				var seg = GetSegment(iSegment);
				ITsString tssSegment = seg.BaselineText;
				string comment = String.Format("{0}.Type({1}).{2}", iSegment, "free", tssSegment.Text);
				seg.FreeTranslation.set_String(m_cache.DefaultAnalWs, comment);
				tssComment = seg.FreeTranslation.AnalysisDefaultWritingSystem;
			}

			/// <summary>
			/// Make up a phony Literal Translation for the segment.
			/// </summary>
			public virtual void SetDefaultLiteralTranslation(int iSegment, out ITsString tssComment)
			{
				var seg = GetSegment(iSegment);
				ITsString tssSegment = seg.BaselineText;
				string comment = String.Format("{0}.Type({1}).{2}", iSegment, "literal", tssSegment.Text);
				seg.LiteralTranslation.set_String(m_cache.DefaultAnalWs, comment);
				tssComment = seg.LiteralTranslation.AnalysisDefaultWritingSystem;
			}

			/// <summary>
			/// Make up a phony Note for the segment.
			/// </summary>
			public virtual void SetDefaultNote(int iSegment, out ITsString tssComment)
			{
				var seg = GetSegment(iSegment);
				ITsString tssSegment = seg.BaselineText;
				string comment = String.Format("{0}.Type({1}).{2}", iSegment, "note", tssSegment.Text);
				var note = seg.Services.GetInstance<INoteFactory>().Create();
				seg.NotesOS.Add(note);
				note.Content.set_String(m_cache.DefaultAnalWs, comment);
				tssComment = note.Content.AnalysisDefaultWritingSystem;
			}
			/// <summary>
			/// Finds an existing wfiAnalysis related to the given cba.InstanceOf.
			/// If none exists, we'll create one.
			/// </summary>
			private IWfiAnalysis FindOrCreateWfiAnalysis(IAnalysis analysisActual)
			{
				IWfiAnalysis actualWfiAnalysis = analysisActual.Analysis;
				if (actualWfiAnalysis == null)
					actualWfiAnalysis = CreateWfiAnalysisForAnalysis(analysisActual);
				return actualWfiAnalysis;
			}

			private IWfiAnalysis CreateWfiAnalysisForAnalysis(IAnalysis actualAnalysis)
			{
				int hvoActualInstanceOf;
				IWfiWordform actualWordform;
				GetRealWordformInfo(actualAnalysis, out hvoActualInstanceOf, out actualWordform);

				// Create a new WfiAnalysis for the wordform.
				IWfiAnalysis actualWfiAnalysis = m_cache.ServiceLocator.GetInstance<IWfiAnalysisFactory>().Create();
				actualWordform.AnalysesOC.Add(actualWfiAnalysis);
				return actualWfiAnalysis;
			}

			/// <summary>
			/// Creates a new analysis with the given MoForms belonging to the wordform currently
			/// at the given position and inserts it into the segment's analysis in place of the wordform.
			/// </summary>
			public virtual IWfiAnalysis BreakIntoMorphs(int iSegment, int iSegForm, ArrayList moForms)
			{
				var actualAnalysis = GetAnalysis(iSegment, iSegForm);
				// Find or create the current analysis of the actual annotation.
				IWfiAnalysis actualWfiAnalysis = CreateWfiAnalysisForAnalysis(actualAnalysis);

				// Setup WfiMorphBundle(s)
				IWfiMorphBundleFactory factory = m_cache.ServiceLocator.GetInstance<IWfiMorphBundleFactory>();
				foreach (object morphForm in moForms)
				{
					IWfiMorphBundle wmb = factory.Create();
					actualWfiAnalysis.MorphBundlesOS.Add(wmb);
					if (morphForm is string)
					{
						// just set the Form.
						wmb.Form.SetVernacularDefaultWritingSystem((string)morphForm);
					}
					else if (morphForm is ITsString)
					{
						var tssMf = (ITsString)morphForm;
						wmb.Form.set_String(TsStringUtils.GetWsAtOffset(tssMf, 0), tssMf);
					}
					else if (morphForm is IMoForm)
					{
						wmb.MorphRA = morphForm as IMoForm;
					}
					else
					{
						throw new ArgumentException("Unexpected class for morphForm.");
					}
				}
				SetAnalysis(iSegment, iSegForm, actualWfiAnalysis);
				return actualWfiAnalysis;
			}

			public virtual IWfiMorphBundle SetMorphSense(int iSegment, int iSegForm, int iMorphBundle, ILexSense sense)
			{
				var analysis = GetAnalysis(iSegment, iSegForm);
				// Find or create the current analysis of the actual annotation.
				return SetMorphBundle(analysis, iMorphBundle, sense);
			}

			protected IWfiMorphBundle SetMorphBundle(IAnalysis analysis, int iMorphBundle, ILexSense sense)
			{
				IWfiAnalysis actualWfiAnalysis = analysis.Analysis;
				if (actualWfiAnalysis != null)
				{
					actualWfiAnalysis.MorphBundlesOS[iMorphBundle].SenseRA = sense;
					return actualWfiAnalysis.MorphBundlesOS[iMorphBundle];
				}
				return null;
			}


			protected internal virtual int MergeAdjacentAnnotations(int iSegment, int iSegForm)
			{
				var analysisOccurrence = new AnalysisOccurrence(m_para.SegmentsOS[iSegment], iSegForm);
				analysisOccurrence.MakePhraseWithNextWord();
				NeedReparseParagraph = true;
				return analysisOccurrence.Analysis.Hvo;
			}

			protected internal virtual void BreakPhrase(int iSegment, int iSegForm)
			{
				var analysisOccurrence = new AnalysisOccurrence(m_para.SegmentsOS[iSegment], iSegForm);
				analysisOccurrence.BreakPhrase();
			}

			ITsString GetBaselineText(int iSegment, int iSegForm)
			{
				return m_para.SegmentsOS[iSegment].GetBaselineText(iSegForm);
			}

			private void GetRealWordformInfo(IAnalysis actualAnalysis, out int hvoActualInstanceOf, out IWfiWordform realWordform)
			{
				hvoActualInstanceOf = actualAnalysis.Hvo;
				realWordform = actualAnalysis.Wordform;
			}

			internal IAnalysis GetAnalysis(int iSegment, int iAnalysis)
			{
				var segmentForms = GetSegment(iSegment).AnalysesRS;
				Debug.Assert(iAnalysis >= 0 && iAnalysis < segmentForms.Count);
				return segmentForms[iAnalysis];
			}

			internal int GetSegmentHvo(int iSegment)
			{
				return GetSegment(iSegment).Hvo;
			}

			internal ISegment GetSegment(int iSegment)
			{
				Debug.Assert(m_para != null);
				var segments = m_para.SegmentsOS;
				Debug.Assert(iSegment >= 0 && iSegment < segments.Count);
				return segments[iSegment];
			}
		}
#pragma warning restore 1591

		internal class ParagraphAnnotatorForParagraphBuilder : ParagraphAnnotator
		{
			ParagraphBuilder m_pb = null;

			internal ParagraphAnnotatorForParagraphBuilder(ParagraphBuilder pb) : base(pb.ActualParagraph)
			{
				m_pb = pb;
			}

			internal override IWfiWordform SetAlternateCase(string wordform, int iOccurrenceInParagraph, StringCaseStatus targetState)
			{
				int iSegment = -1;
				int iSegForm = -1;
				m_pb.GetSegmentFormInfo(wordform, iOccurrenceInParagraph, out iSegment, out iSegForm);
				string alternateWordform;
				return SetAlternateCase(iSegment, iSegForm, targetState, out alternateWordform);
			}

			internal override IWfiGloss SetDefaultWordGloss(string wordform, int iOccurrenceInParagraph)
			{
				int iSegment = -1;
				int iSegForm = -1;
				m_pb.GetSegmentFormInfo(wordform, iOccurrenceInParagraph, out iSegment, out iSegForm);
				string gloss;
				return SetDefaultWordGloss(iSegment, iSegForm, out gloss);
			}

			public override IWfiWordform SetAlternateCase(int iSegment, int iSegForm, StringCaseStatus targetState, out string alternateCaseForm)
			{
				IWfiWordform wfAlternateCase = base.SetAlternateCase(iSegment, iSegForm, targetState, out alternateCaseForm);
				m_pb.SetExpectedValuesForAnalysis(m_pb.SegmentFormNode(iSegment, iSegForm), wfAlternateCase.Hvo);
				return wfAlternateCase;
			}

			public override IWfiAnalysis BreakIntoMorphs(int iSegment, int iSegForm, ArrayList moForms)
			{
				IWfiAnalysis wfiAnalysis = base.BreakIntoMorphs(iSegment, iSegForm, moForms);
				m_pb.SetExpectedValuesForAnalysis(m_pb.SegmentFormNode(iSegment, iSegForm), wfiAnalysis.Hvo);
				return wfiAnalysis;
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="iSegFormPrimary">primary segment form index to merge to adjacent segform</param>
			/// <param name="iSegmentPrimary"></param>
			/// <param name="secondaryPathsToJoinWords">list containing Segment/SegmentForm pairs in paragraph to also join.</param>
			/// <param name="secondaryPathsToBreakPhrases">list containint Segment/SegmentForm pairs to break into annotations.</param>
			/// <returns></returns>
			internal int MergeAdjacentAnnotations(int iSegmentPrimary, int iSegFormPrimary,
				IList<int[]> secondaryPathsToJoinWords, IList<int[]> secondaryPathsToBreakPhrases)
			{
				// first do the primary merge in the paragraph definition (the base class will do it to the paragraph definition).
				string formerMergedStringValue = m_pb.SegmentFormNode(iSegmentPrimary, iSegFormPrimary).InnerText;
				m_pb.MergeAdjacentAnnotations(iSegmentPrimary, iSegFormPrimary);
				string mainMergedStringValue = m_pb.SegmentFormNode(iSegmentPrimary, iSegFormPrimary).InnerText;

				// next do secondary merges which will result from the real analysis.
				MergeSecondaryPhrases(secondaryPathsToJoinWords, mainMergedStringValue);

				// now remove other secondary merges that are no longer valid, since we have destroyed the former one.
				BreakSecondaryPhrases(secondaryPathsToBreakPhrases, formerMergedStringValue);

				// physically change the paragraph (SegmentForms) to match the paragraph definition.
				// after a reparsing the paragraph, the secondary merges will also be reflected.
				return base.MergeAdjacentAnnotations(iSegmentPrimary, iSegFormPrimary);
			}

			private void MergeSecondaryPhrases(IList<int[]> secondaryPathsToJoinWords, string mainMergedStringValue)
			{
				foreach (int[] segmentFormPath in secondaryPathsToJoinWords)
				{
					Debug.Assert(segmentFormPath.Length == 2, "SegmentForm paths should have a segment index followed by a segment form index.");
					int iSecondarySegment = segmentFormPath[0];
					int iSecondarySegForm = segmentFormPath[1];
					m_pb.MergeAdjacentAnnotations(iSecondarySegment, iSecondarySegForm);
					// validate string values match main merge.
					string secondaryStringValue = m_pb.SegmentFormNode(iSecondarySegment, iSecondarySegForm).InnerText;
					Debug.Equals(mainMergedStringValue, secondaryStringValue);
				}
			}

			private void BreakSecondaryPhrases(IList<int[]> secondaryPathsToBreakPhrases, string formerMergedStringValue)
			{
				foreach (int[] segmentFormPath in secondaryPathsToBreakPhrases)
				{
					Debug.Assert(segmentFormPath.Length == 2, "SegmentForm paths should have a segment index followed by a segment form index.");
					int iSecondarySegment = segmentFormPath[0];
					int iSecondarySegForm = segmentFormPath[1];
					string secondaryStringValue = m_pb.SegmentFormNode(iSecondarySegment, iSecondarySegForm).InnerText;
					// validate the phrase we are destroying equals the previous merged phrase.
					Debug.Equals(formerMergedStringValue, secondaryStringValue);
					m_pb.BreakPhraseAnnotation(iSecondarySegment, iSecondarySegForm);
				}
			}

			internal void BreakPhrase(int iSegmentPrimary, int iSegFormPrimary,
				IList<int[]> secondaryPathsToBreakPhrases, IList<int[]> secondaryPathsToJoinWords, string mainMergedStringValue)
			{
				// first do the primary break in the paragraph definition (the base class will actually do it the paragraph).
				string formerMergedStringValue = m_pb.SegmentFormNode(iSegmentPrimary, iSegFormPrimary).InnerText;
				m_pb.BreakPhraseAnnotation(iSegmentPrimary, iSegFormPrimary);

				// now remove other secondary merges that are no longer valid, since we have destroyed the former one.
				BreakSecondaryPhrases(secondaryPathsToBreakPhrases, formerMergedStringValue);

				// merge together any remaining secondary phrases.
				MergeSecondaryPhrases(secondaryPathsToJoinWords, mainMergedStringValue);

				// physically change the paragraph (SegmentForms) to match the paragraph definition.
				// after a reparsing the paragraph, the secondary breaks will also be reflected.
				base.BreakPhrase(iSegmentPrimary, iSegFormPrimary);
			}

			/// <summary>
			/// </summary>
			/// <returns>hvo of new analysis.</returns>
			internal override IWfiGloss SetDefaultWordGloss(int iSegment, int iSegForm, out string gloss)
			{
				return SetDefaultWordGloss(iSegment, iSegForm, null, out gloss);
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="iSegment"></param>
			/// <param name="iSegForm"></param>
			/// <param name="wfiAnalysis">wfi analysis to add gloss to.</param>
			/// <param name="gloss"></param>
			/// <returns></returns>
			internal override IWfiGloss SetDefaultWordGloss(int iSegment, int iSegForm, IWfiAnalysis wfiAnalysis, out string gloss)
			{
				IWfiGloss wfiGloss = base.SetDefaultWordGloss(iSegment, iSegForm, wfiAnalysis, out gloss);
				m_pb.SetExpectedValuesForAnalysis(m_pb.SegmentFormNode(iSegment, iSegForm), wfiGloss.Hvo);
				return wfiGloss;
			}

			/// <summary>
			/// In the ID attribute of the XmlNode we store the ID of the analysis we expect for that occurrence.
			/// </summary>
			private int GetIdOfExpectedAnalysis(int iSegment, int iSegForm)
			{
				XmlNode cbaWordformNode = m_pb.SegmentFormNode(iSegment, iSegForm);
				return XmlUtils.GetMandatoryIntegerAttributeValue(cbaWordformNode, "id");
			}

			/// <summary>
			/// Walk through the paragraph and create word glosses, and segment annotations.
			/// </summary>
			/// <returns>list of wordglosses, including 0's for nonwordforms</returns>
			protected internal override IList<IWfiGloss> SetupDefaultWordGlosses()
			{
				IList<IWfiGloss> wordGlosses = new List<IWfiGloss>();
				int iseg = 0;
				foreach (XmlNode segNode in m_pb.SegmentNodes())
				{
					int isegform = 0;
					foreach (XmlNode segFormNode in ParagraphBuilder.SegmentFormNodes(segNode))
					{
						IWfiGloss wordGloss = null;
						string annTypeGuid = ParagraphBuilder.GetAnnotationTypeGuid(segFormNode);
						if (annTypeGuid == ParagraphBuilder.WficGuid)
						{
							string gloss;
							wordGloss = SetDefaultWordGloss(iseg, isegform, out gloss);
						}
						if (annTypeGuid == ParagraphBuilder.PunctGuid)
						{
							m_pb.ExportCbaNodeToReal(iseg, isegform);
						}
						wordGlosses.Add(wordGloss);
						isegform++;
					}
					// create freeform annotations for each of the segments
					// TODO: multiple writing systems.
					ITsString tssComment;
					SetDefaultFreeTranslation(iseg, out tssComment);
					SetDefaultLiteralTranslation(iseg, out tssComment);
					SetDefaultNote(iseg, out tssComment);

					iseg++;
				}
				return wordGlosses;
			}

			/// <summary>
			///
			/// </summary>
			internal void ValidateAnnotations()
			{
				ValidateAnnotations(false);
			}

			/// <summary>
			/// Validate the annotation information stored in the xml configuration against
			/// the annotation information stored in memory from ParagraphParser.
			/// </summary>
			/// <param name="fSkipForceParse">skips parsing if the text has not changed.</param>
			internal void ValidateAnnotations(bool fSkipForceParse)
			{
				Debug.Assert(m_para != null);
				Debug.Assert(m_para == m_pb.ActualParagraph);
				if (fSkipForceParse)
				{
					// just sync the defn with expected valid ids.
					m_pb.ResyncExpectedAnnotationIds();
				}
				else if (NeedReparseParagraph)
				{
					ReparseParagraph();
				}
				if (m_pb.NeedToRebuildParagraphContentFromAnnotations)
				{
					m_pb.RebuildParagraphContentFromAnnotations();
				}
				ConceptualModelXmlParagraphValidator validator = new ConceptualModelXmlParagraphValidator(m_pb);
				validator.ValidateParagraphs(m_pb.ParagraphDefinition, m_pb.ActualParagraph);
			}
		}

		/// <summary>
		/// This class can be used to build the contents of a paragraph through an xml specification.
		/// Note: the xml specification will be modified to save the expected state of the paragraph.
		/// </summary>
		internal class ParagraphBuilder
		{
			LcmCache m_cache = null;
			ILcmOwningSequence<IStPara> m_owner = null;
			IStTxtPara m_para = null;
			XmlNode m_paraDefn = null;
			bool m_fNeedToRebuildParagraphContentFromAnnotations = false;
			Dictionary<string, int> m_expectedWordformsAndOccurrences;
			//bool m_fNeedToRebuildParagraphContentFromStrings = false;

			internal ParagraphBuilder(LcmCache cache, ILcmOwningSequence<IStPara> owner)
			{
				m_owner = owner;
				m_cache = cache;
			}

			internal ParagraphBuilder(LcmCache cache, ILcmOwningSequence<IStPara> owner, XmlNode paraDefn)
				: this(cache, owner)
			{
				m_paraDefn = paraDefn;
			}

			internal ParagraphBuilder(IStTxtPara para, XmlNode paraDefn)
			{
				m_para = para;
				m_cache = para.Cache;
				Debug.Assert(paraDefn != null &&
							XmlUtils.GetMandatoryIntegerAttributeValue(paraDefn, "id") == para.Hvo);
				m_paraDefn = paraDefn;
			}

			internal Dictionary<string, int> ExpectedWordformsAndOccurrences
			{
				get
				{
					if (m_expectedWordformsAndOccurrences == null)
						GenerateParaContentFromAnnotations();
					return m_expectedWordformsAndOccurrences;
				}
			}

			internal ParagraphBuilder(XmlNode textsDefn, IText text, int iPara)
			{
				IStTxtPara para;
				m_paraDefn = GetStTxtParaDefnNode(text, textsDefn, iPara, out para);
				m_para = para;
				m_cache = para.Cache;
			}

			internal XmlNode ParagraphDefinition
			{
				get { return m_paraDefn; }
				set { m_paraDefn = value; }
			}

			private XmlNode ParentStTextDefinition
			{
				get
				{
					if (m_paraDefn != null)
						return m_paraDefn.SelectSingleNode("ancestor::StText");
					else
						return null;
				}
			}
			/// <summary>
			/// Copies out the anaysis info in the paragraph definition cba node into the appropriate position in the real segment.
			/// Will replace the current one at that position, or may be used to add to the segment, by passing
			/// an index one too large.
			/// Returns the analysis.
			/// </summary>
			internal IAnalysis ExportCbaNodeToReal(int iSegment, int iSegForm)
			{
				int hvoDesiredTarget = GetExpectedAnalysis(iSegment, iSegForm);
				var seg = m_para.SegmentsOS[iSegment];
				IAnalysis analysis;
				if (hvoDesiredTarget == -1)
				{
					string word = StringValue(iSegment, iSegForm);
					int wsWord = GetWsFromStringNode(StringValueNode(iSegment, iSegForm));
					var wfFactory =
						m_cache.ServiceLocator.GetInstance<IWfiWordformFactory>();
					analysis = wfFactory.Create(TsStringUtils.MakeString(word, wsWord));
				}
				else
				{
					analysis = (IAnalysis) seg.Services.GetObject(hvoDesiredTarget);
				}
				if (iSegForm == seg.AnalysesRS.Count)
					seg.AnalysesRS.Add(analysis);
				else
					seg.AnalysesRS[iSegForm] = analysis;
				return analysis;
			}

			internal int GetExpectedAnalysis(int iSegment, int iSegForm)
			{
				XmlNode fileItem = SegmentFormNode(iSegment, iSegForm);
				return int.Parse(fileItem.Attributes["id"].Value);
			}

			internal XmlNode SnapshotOfStText()
			{
				return Snapshot(ParentStTextDefinition);
			}

			internal XmlNode SnapshotOfPara()
			{
				return Snapshot(ParagraphDefinition);
			}

			/// <summary>
			/// get a clone of a node (and its owning document)
			/// </summary>
			internal static XmlNode Snapshot(XmlNode node)
			{
				if (node == null)
					return null;

				// get the xpath of the node in its document
				if (node.NodeType != XmlNodeType.Document)
				{
					string xpath = GetXPathInDocument(node);
					XmlNode clonedOwner = node.OwnerDocument?.CloneNode(true);
					return clonedOwner?.SelectSingleNode(xpath);
				}

				return node.CloneNode(true);
			}

			/// <summary>
			/// build an xpath to the given node in its document.
			/// </summary>
			private static string GetXPathInDocument(XmlNode node)
			{
				if (node == null || node.NodeType != XmlNodeType.Element)
					return "";
				//XmlNode parent = node.ParentNode;
				// start with the name of the node, and tentatively guess it to be the root element.
				string xpath = $"/{node.LocalName}";
				// append the index of the node amongst any preceding siblings.
				int index = GetIndexAmongSiblings(node);
				if (index != -1)
				{
					index = index + 1; // add one for an xpath index.
					xpath += $"[{index}]";
				}
				return string.Concat(GetXPathInDocument(node.ParentNode), xpath);
			}

			internal IStTxtPara ActualParagraph
			{
				get { return m_para; }
			}

			protected static XmlNode GetStTxtParaDefnNode(IText text, XmlNode textsDefn, int iPara, out IStTxtPara para)
			{
				para = text.ContentsOA.ParagraphsOS[iPara] as IStTxtPara;
				Debug.Assert(para != null);
				if (textsDefn == null)
					return null;
				XmlNode paraNode = textsDefn.SelectSingleNode("//StTxtPara[@id='" + para.Hvo + "']");
				if (paraNode == null)
				{
					// see if we can find a new node that for which we haven't determined its hvo.
					paraNode = textsDefn.SelectSingleNode("//StTxtPara[" + (iPara + 1) + "]");
					// the paragraph shouldn't have an hvo yet.
					if (paraNode.Attributes["id"].Value == "")
					{
						// go ahead and set the hvo now.
						paraNode.Attributes["id"].Value = para.Hvo.ToString();
					}
					else
					{
						paraNode = null;
					}
				}
				return paraNode;
			}

			internal IStTxtPara BuildParagraphContent(XmlNode paraDefn)
			{
				if (paraDefn.Name != "StTxtPara")
					return null;
				// 1. Create a new paragraph.
				CreateNewParagraph(paraDefn);
				// Build Contents
				XmlNodeList segments = m_paraDefn.SelectNodes("Segments16/CmBaseAnnotation");
				if (segments.Count > 0)
				{
					RebuildParagraphContentFromAnnotations();
				}
				else
				{
					XmlNodeList runs = m_paraDefn.SelectNodes("Contents16/Str/Run");
					if (runs.Count > 0)
						RebuildParagraphContentFromStrings();
				}
				if (m_para.Contents == null)
				{
					// make sure it has an empty content
					m_para.Contents = TsStringUtils.MakeString("", m_cache.DefaultVernWs);
				}
				return m_para;
			}

			internal ITsString RebuildParagraphContentFromStrings()
			{
				try
				{
					m_expectedWordformsAndOccurrences = null;
					//<Contents16> <Str> <Run ws="">
					XmlNodeList runs = m_paraDefn.SelectNodes("Contents16/Str/Run");
					if (runs == null)
						return null;

					StTxtParaBldr bldr = new StTxtParaBldr(m_cache);
					bldr.StringBuilder.Clear();

					foreach (XmlNode run in runs)
					{
						int ws = GetWsFromStringNode(run);
						bldr.AppendRun(run.InnerText, MakeTextProps(ws));
					}
					SetContents(bldr.StringBuilder.GetString(), false);
					return m_para.Contents;
				}
				finally
				{
					//m_fNeedToRebuildParagraphContentFromStrings = false;
				}
			}

			internal IStTxtPara CreateNewParagraph(XmlNode paraDefn)
			{
				int iInsertAt = GetIndexAmongSiblings(paraDefn);
				m_para = this.CreateParagraph(iInsertAt);
				m_paraDefn = paraDefn;
				m_paraDefn.Attributes["id"].Value = m_para.Hvo.ToString();
				return m_para;
			}

			public static void CopyContents(IStTxtPara paraSrc, IStTxtPara paraTarget)
			{
				paraTarget.Contents = paraSrc.Contents;
			}

			/// <summary>
			/// (after setting paragraph contents, we'll reparse the text, creating dummy wordforms.)
			/// </summary>
			/// <returns></returns>
			internal ITsString RebuildParagraphContentFromAnnotations()
			{
				return RebuildParagraphContentFromAnnotations(false);
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="fResetConcordance"></param>
			/// <returns></returns>
			internal ITsString RebuildParagraphContentFromAnnotations(bool fResetConcordance)
			{
				try
				{
					ITsString content = GenerateParaContentFromAnnotations();
					if (content == null)
						return null;

					// Set the content of text for the paragraph.
					this.SetContents(content, fResetConcordance);
					return m_para.Contents;
				}
				finally
				{
					m_fNeedToRebuildParagraphContentFromAnnotations = false;
				}
			}

			/// <summary>
			/// Currently, this ensures that the real analysis is set for all segments and wordforms up to the
			/// specified one. We can't have an isolated item any more, so we create earlier ones
			/// as necessary, also.
			/// </summary>
			/// <param name="iSegment"></param>
			/// <param name="iSegForm"></param>
			/// <returns></returns>
			internal IWfiWordform SetDefaultWord(int iSegment, int iSegForm)
			{
				// Make sure we have enough segments
				if (iSegment >= m_para.SegmentsOS.Count)
				{
					for (int i = m_para.SegmentsOS.Count; i < iSegment; iSegForm++)
					{
						var newSeg = m_para.Services.GetInstance<ISegmentFactory>().Create();
						m_para.SegmentsOS.Add(newSeg);
					}
				}

				// Make sure that any earlier wordforms for this segment exist. Otherwise, we can't set the
				// one at the specified index. Note that this is a recursive call; it will terminate because
				// we always make the recursive call with a smaller value of iSegForm. In fact, it will only
				// recurse once, because we make sure to create the first one needed first.
				var seg = m_para.SegmentsOS[iSegment];
				for (int i = seg.AnalysesRS.Count; i < iSegForm; i++)
					SetDefaultWord(iSegment, i);

				// Now make the one we actually want, even if it already had an item in the Analyses list.
				string stringValue = StringValue(iSegment, iSegForm);
				int ws = GetWsFromStringNode(StringValueNode(iSegment, iSegForm));
				IWfiWordform actualWordform = WfiWordformServices.FindOrCreateWordform(m_cache, TsStringUtils.MakeString(stringValue, ws));
				XmlNode fileItem = SegmentFormNode(iSegment, iSegForm);
				fileItem.Attributes["id"].Value = actualWordform.Hvo.ToString();
				ExportCbaNodeToReal(iSegment, iSegForm);
				return actualWordform;
			}

			/// <summary>
			/// Sync the expected occurrences with the database.
			/// </summary>
			internal void ResyncExpectedAnnotationIds()
			{
				GenerateParaContentFromAnnotations();
			}

			internal static string WficGuid = "eb92e50f-ba96-4d1d-b632-057b5c274132";
			internal static string PunctGuid = "cfecb1fe-037a-452d-a35b-59e06d15f4df";
			/// <summary>
			/// Create the content of a translation property of a segment so as to be consistent with the
			/// analysis glosses.
			/// </summary>
			/// <returns></returns>
			internal ITsString GenerateParaContentFromAnnotations()
			{
				m_expectedWordformsAndOccurrences = new Dictionary<string, int>();
				Debug.Assert(m_paraDefn != null);
				XmlNodeList segments = m_paraDefn.SelectNodes("Segments16/CmBaseAnnotation");
				if (segments == null)
					return null;

				int ichMinSeg = 0;
				int ichLimSeg = 0;
				// Create TsString for Paragraph Contents.
				ITsStrBldr contentsBldr = TsStringUtils.MakeStrBldr();
				contentsBldr.Clear();
				foreach (XmlNode segment in segments)
				{
					// Build the segment by its SegmentForms annotations.
					XmlNodeList forms = segment.SelectNodes("SegmentForms37/CmBaseAnnotation");
					if (forms == null)
						break;
					XmlNode lastForm = forms[forms.Count - 1];
					string prevAnnType = null;
					foreach (XmlNode form in forms)
					{
						// Get the string form for the annotation.
						XmlNode strValue = form.SelectSingleNode("StringValue37");
						string strForm = strValue.InnerText;
						// Build the text based upon this annotation.
						// if we are given a ws for the writing system, otherwise use the default.
						int ws = GetWsFromStringNode(strValue);
						Debug.Assert(!String.IsNullOrEmpty(strForm), "Can't build paragraphs based on annotations without a StringValue.");
						string guid = GetAnnotationTypeGuid(form);

						// Build leading whitespace.
						int cchLeadingWhitespace = XmlUtils.GetOptionalIntegerValue(strValue, "leadingWhitespace", 0);
						string leadingWhitespace = GenerateWhitespace(cchLeadingWhitespace);
						if (prevAnnType != null && prevAnnType == guid && prevAnnType == WficGuid)
						{
							// add standard form separator between common annotation types.
							leadingWhitespace += " ";
						}
						int ichMinForm = ichLimSeg + leadingWhitespace.Length;
						int ichLimForm = ichMinForm + strForm.Length;
						// if the 'id' is valid, then use it.
						// Create a dummy annotation for the appropriate type.
						if (guid == WficGuid)
						{
							// If this is the first time we've built the contents of this paragraph,
							// the id attribute will be empty, and should be set to -1 to indicate
							// we just expect a wordform. Otherwise, it has previously been set to some
							// specific expected thing, so leave it alone.
							if (string.IsNullOrEmpty(form.Attributes[kstAnalysisAttr].Value))
								SetExpectedValuesForAnalysis(form, -1);
							// update our expected occurences of this wordform.
							UpdateExpectedWordformsAndOccurrences(strForm, ws);
						}
						else
						{
							if (guid == PunctGuid)
							{
								SetExpectedValuesForAnalysis(form, -1); // -1 indicates a plain punctform.
							}
							else
							{
								Debug.Fail("AnnotationType " + guid + " not supported in SegmentForms.");
							}
						}

						// Calculate any trailing whitespace to be added to text.
						int extraTrailingWhitespace = XmlUtils.GetOptionalIntegerValue(strValue, "extraTrailingWhitespace", 0);
						string trailingWhitespace = GenerateWhitespace(extraTrailingWhitespace);

						// Make a TsTextProps specifying just the writing system.
						ITsTextProps props = MakeTextProps(ws);
						// Add the Form to the text.
						contentsBldr.Replace(contentsBldr.Length, contentsBldr.Length, leadingWhitespace + strForm + trailingWhitespace, props);
						// setup for the next form.
						prevAnnType = guid;
						ichLimSeg = ichLimForm + trailingWhitespace.Length;
						Debug.Assert(ichLimSeg == contentsBldr.Length, "The current length of the text we're building (" + contentsBldr.Length +
																		") should match the current segment Lim (" + ichLimSeg + ").");
					}
					// Create the segment annotation.
					SetExpectedValuesForSegment(segment, ichMinSeg);
					ichMinSeg = ichLimSeg;
				}

				return contentsBldr.GetString();
			}

			internal static string GetAnnotationTypeGuid(XmlNode segFormNode)
			{
				XmlNode annotationType = segFormNode.SelectSingleNode("AnnotationType34/Link");
				Debug.Assert(annotationType != null, "SegmentForm needs to specify an AnnotationType.");
				string guid = XmlUtils.GetMandatoryAttributeValue(annotationType, "guid");
				return guid;
			}

			private static string GenerateWhitespace(int cchWhitespace)
			{
				string whitespace = "";
				// add extra whitespace to our text.
				for (int i = 0; i < cchWhitespace; i++)
					whitespace += " ";
				return whitespace;
			}

			private void UpdateExpectedWordformsAndOccurrences(string strForm, int ws)
			{
				if (strForm.Length == 0)
					return;
				string key = strForm + ws.ToString();
				if (m_expectedWordformsAndOccurrences.ContainsKey(key))
				{
					// note another occurrence
					m_expectedWordformsAndOccurrences[key] += 1;
				}
				else
				{
					// first occurrence
					m_expectedWordformsAndOccurrences[key] = 1;
				}
			}

			private static ITsTextProps MakeTextProps(int ws)
			{
				ITsPropsBldr propBldr = TsStringUtils.MakePropsBldr();
				propBldr.SetIntPropValues((int)FwTextPropType.ktptWs, (int)FwTextPropVar.ktpvDefault, ws);
				ITsTextProps props = propBldr.GetTextProps();
				return props;
			}

			private int GetWsFromStringNode(XmlNode strValue)
			{
				int ws = 0;
				string wsAbbr = XmlUtils.GetOptionalAttributeValue(strValue, "ws");
				if (wsAbbr != null)
				{
					var wsObj = (CoreWritingSystemDefinition) m_cache.WritingSystemFactory.get_Engine(wsAbbr);
					ws = wsObj.Handle;
					Debug.Assert(ws != 0, "Don't recognize ws (" + wsAbbr + ") for StringValue");
					// add it to the vernacular writing system list, if it's not already there.
					if (!m_cache.ServiceLocator.WritingSystems.CurrentVernacularWritingSystems.Contains(wsObj))
						m_cache.ServiceLocator.WritingSystems.AddToCurrentVernacularWritingSystems(wsObj);
				}
				if (ws == 0)
					ws = m_cache.DefaultVernWs;
				return ws;
			}

			private const string kstAnalysisAttr = "id";
			/// <summary>
			/// Set the expected values that we computer or modify rather than reading from the file, for an
			/// XmlNode that represents an analysis.
			/// </summary>
			internal void SetExpectedValuesForAnalysis(XmlNode annotationDefn, int hvoInstanceOf)
			{
				annotationDefn.Attributes[kstAnalysisAttr].Value = hvoInstanceOf.ToString();
			}

			/// <summary>
			/// Set the expected values that we computer or modify rather than reading from the file, for an
			/// XmlNode that represents an analysis.
			/// </summary>
			internal void SetExpectedValuesForSegment(XmlNode annotationDefn, int ichMin)
			{
				Assert.IsNotNull(annotationDefn.Attributes, "annotationDefn.Attributes is null");
				if (annotationDefn.Attributes["beginOffset"] == null)
				{
					Assert.IsNotNull(annotationDefn.OwnerDocument, "OwnerDocument is null");
					var xa = annotationDefn.OwnerDocument.CreateAttribute("beginOffset");
					xa.Value = ichMin.ToString();
					annotationDefn.Attributes.Append(xa);
				}
				else
				{
					annotationDefn.Attributes["beginOffset"].Value = ichMin.ToString();
				}
			}

			public IStTxtPara AppendParagraph()
			{
				return CreateParagraph(m_owner.Count);
			}

			public IStTxtPara CreateParagraph(int iInsertNewParaAt)
			{
				Debug.Assert(m_owner != null);
				IStTxtPara para = m_cache.ServiceLocator.GetInstance<IStTxtParaFactory>().Create();
				m_owner.Insert(iInsertNewParaAt, para);
				return para;
			}

			void SetContents(ITsString tssContents, bool fResetConcordance)
			{
				Debug.Assert(m_para != null);
				if (m_para.Contents != null && tssContents.Equals(m_para.Contents))
					return;
				m_para.Contents = tssContents;
				//ParseParagraph(fResetConcordance, fCreateRealWordforms);
			}

			bool HaveParsed { get; set; }

			internal void ParseParagraph()
			{
				ParseParagraph(false, false);
			}

			internal void ParseParagraph(bool fResetConcordance)
			{
				// Reparse the paragraph to recompute annotations.
				ParseParagraph(fResetConcordance, true);
			}

			internal void ParseParagraph(bool fResetConcordance, bool fBuildConcordance)
			{
				ParagraphParser.ParseParagraph(m_para, fBuildConcordance, fResetConcordance);
				HaveParsed = true;
			}

			XmlNode SegmentNode(int iSegment)
			{
				XmlNodeList cbaSegmentNodes = SegmentNodeList();
				Debug.Assert(cbaSegmentNodes != null);
				Debug.Assert(iSegment < cbaSegmentNodes.Count);
				return cbaSegmentNodes[iSegment];
			}

			XmlNodeList SegmentFormNodeList(int iSegment)
			{
				XmlNode segmentNode = SegmentNode(iSegment);
				return SegmentFormNodeList(segmentNode);
			}

			/// <summary>
			/// In the XML representation of expected data, each Segment has a SegmentForms37 property containing
			/// a sequence of CmBaseAnnotations. Given the parent node, answer the CmBaseAnnotation nodes.
			/// </summary>
			/// <param name="segmentNode"></param>
			/// <returns></returns>
			static XmlNodeList SegmentFormNodeList(XmlNode segmentNode)
			{
				XmlNodeList cbaSegFormNodes = segmentNode.SelectNodes("SegmentForms37/CmBaseAnnotation");
				return cbaSegFormNodes;
			}

			internal XmlNode SegmentFormNode(int iSegment, int iSegForm)
			{
				XmlNodeList cbaSegFormNodes = SegmentFormNodeList(iSegment);
				Debug.Assert(cbaSegFormNodes != null);
				if (cbaSegFormNodes.Count == 0)
					return null;
				Debug.Assert(iSegForm < cbaSegFormNodes.Count);
				XmlNode cbaSegFormNode = cbaSegFormNodes[iSegForm];
				return cbaSegFormNode;
			}

			/// <summary>
			/// Get a list of the XmlNodes (CmBaseAnnotations in the Segments16 property) of the input XmlNode,
			/// which represents an StTxtPara in our test data.
			/// </summary>
			internal static List<XmlNode> SegmentNodes(XmlNode paraDefn)
			{
				return NodeListToNodes(SegmentNodeList(paraDefn));
			}

			internal List<XmlNode> SegmentNodes()
			{
				return NodeListToNodes(SegmentNodeList());
			}

			internal static List<XmlNode> SegmentFormNodes(XmlNode segment)
			{
				return NodeListToNodes(SegmentFormNodeList(segment));
			}

			internal XmlNode GetSegmentFormInfo(string wordform, int iOccurrenceInParagraph, out int iSegment, out int iSegForm)
			{
				XmlNodeList cbaSegFormNodes = m_paraDefn.SelectNodes("./Segments16/CmBaseAnnotation/SegmentForms37/CmBaseAnnotation[StringValue37='" + wordform + "']");
				Debug.Assert(cbaSegFormNodes != null);
				Debug.Assert(iOccurrenceInParagraph >= 0 && iOccurrenceInParagraph < cbaSegFormNodes.Count);
				XmlNode cbaSegForm = cbaSegFormNodes[iOccurrenceInParagraph];
				iSegForm = GetIndexAmongSiblings(cbaSegForm);
				iSegment = GetIndexAmongSiblings(cbaSegForm.ParentNode.ParentNode);
				return cbaSegForm;
			}

			internal bool ReplaceTrailingWhitepace(int iSegment, int iSegForm, int newExtraWhitespaceCount)
			{
				XmlNode stringValueNode = StringValueNode(iSegment, iSegForm);
				ReplaceAttributeValue(stringValueNode, "extraTrailingWhitespace", newExtraWhitespaceCount.ToString());
				return NeedToRebuildParagraphContentFromAnnotations;
			}

			internal bool ReplaceLeadingWhitepace(int iSegment, int iSegForm, int newExtraWhitespaceCount)
			{
				XmlNode stringValueNode = StringValueNode(iSegment, iSegForm);
				ReplaceAttributeValue(stringValueNode, "leadingWhitespace", newExtraWhitespaceCount.ToString());
				return NeedToRebuildParagraphContentFromAnnotations;
			}

			internal string ReplaceAttributeValue(XmlNode node, string attrName, string newValue)
			{
				XmlAttribute attr = node.Attributes[attrName];
				if (attr == null)
				{
					attr = node.OwnerDocument.CreateAttribute(attrName);
					node.Attributes.Append(attr);
				}
				attr.Value = newValue;
				InvalidateParagraphInfo();
				return attr.Value;
			}

			private XmlNode StringValueNode(int iSegment, int iSegForm)
			{
				XmlNode segFormNode = SegmentFormNode(iSegment, iSegForm);
				XmlNode stringValueNode = segFormNode.SelectSingleNode("StringValue37");
				return stringValueNode;
			}

			private string StringValue(int iSegment, int iSegForm)
			{
				return StringValueNode(iSegment, iSegForm).InnerText;
			}

			internal bool ReplaceSegmentForm(string segmentForm, int iOccurrenceInParagraph, string newValue)
			{
				int iSegment = -1;
				int iSegForm = -1;
				GetSegmentFormInfo(segmentForm, iOccurrenceInParagraph, out iSegment, out iSegForm);
				return ReplaceSegmentForm(iSegment, iSegForm, newValue, 0);
			}

			/// <summary>
			/// Inserts (before iSegment, iSegForm) a new SegmentForm with format:
			///		<CmBaseAnnotation id="">
			///			<AnnotationType34>
			///				<Link ws="en" name="Wordform In Context" guid="eb92e50f-ba96-4d1d-b632-057b5c274132" />
			///			</AnnotationType34>
			///			<StringValue37>xxxpus</StringValue37>
			///		</CmBaseAnnotation>
			/// </summary>
			/// <returns></returns>
			internal bool InsertSegmentForm(int iSegment, int iSegForm, string annTypeGuid, string stringValue)
			{
				// assume this segment already has a segment form.

				XmlNode segmentForms = m_paraDefn.SelectSingleNode("Segments16/CmBaseAnnotation[" + (iSegment + 1) + "]/SegmentForms37");
				XmlElement newSegmentForm = segmentForms.OwnerDocument.CreateElement("CmBaseAnnotation");
				newSegmentForm.SetAttribute("id", "");
				XmlElement newAnnotationType = segmentForms.OwnerDocument.CreateElement("AnnotationType34");
				XmlElement newLink = segmentForms.OwnerDocument.CreateElement("Link");
				Debug.Assert(annTypeGuid == WficGuid ||
							annTypeGuid == PunctGuid,
					String.Format("annTypeGuid {0} parameter should be either Wordform or Punctuation In Context.", annTypeGuid));
				newLink.SetAttribute("guid", annTypeGuid);
				newAnnotationType.AppendChild(newLink);
				newSegmentForm.AppendChild(newAnnotationType);
				XmlElement newStringValue = segmentForms.OwnerDocument.CreateElement("StringValue37");
				Debug.Assert(stringValue.Length != 0, "stringValue parameter should be nonzero.");
				newStringValue.InnerText = stringValue; // default ws.
				newSegmentForm.AppendChild(newStringValue);

				XmlNode segmentFormNode = SegmentFormNode(iSegment, iSegForm);
				if (segmentFormNode == null)
					segmentForms.AppendChild(newSegmentForm);
				else
					segmentForms.InsertBefore(newSegmentForm, segmentFormNode);

				// Invalidate real paragraph cache info.
				InvalidateParagraphInfo();
				return NeedToRebuildParagraphContentFromAnnotations;
			}

			internal XmlElement CreateSegmentNode()
			{
				XmlNode segments = m_paraDefn.SelectSingleNode(".//Segments16");
				XmlElement newSegment = segments.OwnerDocument.CreateElement("CmBaseAnnotation");
				newSegment.SetAttribute("id", "");
				segments.AppendChild(newSegment);
				return newSegment;
			}

			internal XmlElement CreateSegmentForms()
			{
				Debug.Assert(m_paraDefn.SelectNodes(".//Segments16/CmBaseAnnotation").Count == 1);
				Debug.Assert(m_paraDefn.SelectSingleNode(".//Segments16/CmBaseAnnotation/SegmentForms37") == null);
				XmlNode segment = m_paraDefn.SelectSingleNode(".//Segments16/CmBaseAnnotation");
				XmlElement newSegmentForms = segment.OwnerDocument.CreateElement("SegmentForms37");
				segment.AppendChild(newSegmentForms);
				return newSegmentForms;
			}

			internal bool InsertSegmentBreak(int iSegment, int iSegForm, string stringValue)
			{
				// First insert a punctuation segment form.
				InsertSegmentForm(iSegment, iSegForm, PunctGuid, stringValue);

				// Next insert a new preceding segment
				XmlNode segmentBreakNode = SegmentFormNode(iSegment, iSegForm);
				XmlNode segmentForms = segmentBreakNode.ParentNode;
				XmlNode segments = segmentForms.ParentNode.ParentNode;
				XmlElement newSegment = segments.OwnerDocument.CreateElement("CmBaseAnnotation");
				newSegment.SetAttribute("id", "");
				XmlElement newSegmentForms = segments.OwnerDocument.CreateElement("SegmentForms37");
				newSegment.AppendChild(newSegmentForms);
				segments.InsertBefore(newSegment, segmentForms.ParentNode);

				// then move all children through the period to the new segment forms
				MoveSiblingNodes(segmentForms.FirstChild, newSegmentForms, segmentBreakNode);

				// Invalidate real paragraph cache info.
				InvalidateParagraphInfo();
				return NeedToRebuildParagraphContentFromAnnotations;
			}

			internal bool DeleteSegmentBreak(int iSegment, int iSegForm)
			{
				// get first segment
				XmlNode segmentBreakNode = SegmentFormNode(iSegment, iSegForm);
				XmlNode segmentForms = segmentBreakNode.ParentNode;
				XmlNode segments = segmentForms.ParentNode.ParentNode;

				Debug.Assert(iSegment < SegmentNodeList().Count - 1,
					String.Format("Can't merge segments in paragraph, since iSegment {0} is last segment.", iSegment));

				// first remove segment break node.
				segmentForms.RemoveChild(segmentBreakNode);

				// then move all segment forms from following segment to the new segment forms
				MoveSiblingNodes(SegmentFormNode(iSegment + 1, 0), segmentForms, null);

				// finally remove following segment
				RemoveSegment(iSegment + 1);

				// Invalidate real paragraph cache info.
				InvalidateParagraphInfo();
				return NeedToRebuildParagraphContentFromAnnotations;
			}

			internal bool ReplaceSegmentForm(int iSegment, int iSegForm, string newValue, int newWs)
			{
				XmlNode segFormNode = SegmentFormNode(iSegment, iSegForm);
				XmlNode stringValueNode = segFormNode.SelectSingleNode("StringValue37");
				if (newValue.Length > 0)
				{
					//if (stringValueNode.InnerText != newValue)
					//{
					// modify the current form.
					stringValueNode.InnerText = newValue;
					// invalidate the segment form id
					segFormNode.Attributes["id"].Value = "";	// we need to recreate the dummy object for this node.
					//}
					// modify the current ws
					if (newWs != 0 && newWs != GetWsFromStringNode(stringValueNode))
					{
						XmlAttribute xaWs = stringValueNode.Attributes["ws"];
						if (xaWs == null)
						{
							xaWs = stringValueNode.OwnerDocument.CreateAttribute("ws");
							stringValueNode.Attributes.Append(xaWs);
						}
						xaWs.Value = m_cache.ServiceLocator.WritingSystemManager.GetStrFromWs(newWs);
						segFormNode.Attributes["id"].Value = "";	// we need to recreate the dummy object for this node.
					}
				}
				else
				{
					// delete the segment form.
					XmlNode segmentList = segFormNode.ParentNode;
					// Review: handle special cases
					// 1) removing final item in the list. Adjust trailing whitespace for the new final item.
					// 2) if removing item results in an empty list, remove the parent Segment,
					// if it no longer has any forms.
					Debug.Assert(segmentList.ChildNodes.Count != 1,
						"We need to enhance ParagraphBuilder to cleanup an empty SegmentForm list.");
					segmentList.RemoveChild(segFormNode);
				}
				// Invalidate real paragraph cache info.
				InvalidateParagraphInfo();
				return NeedToRebuildParagraphContentFromAnnotations;
			}

			internal bool RemoveSegment(int iSegment)
			{
				XmlNode segmentNode = SegmentNode(iSegment);
				XmlNode segmentsNode = segmentNode.SelectSingleNode("..");
				segmentsNode.RemoveChild(segmentNode);
				InvalidateParagraphInfo();
				return NeedToRebuildParagraphContentFromAnnotations;
			}

			internal bool RemoveSegmentForm(int iSegment, int iSegForm)
			{
				ReplaceSegmentForm(iSegment, iSegForm, "", 0);
				InvalidateParagraphInfo();
				return NeedToRebuildParagraphContentFromAnnotations;
			}

			internal virtual bool MergeAdjacentAnnotations(int iSegment, int iSegForm)
			{
				// change the paragraph definition spec.
				this.ReplaceSegmentForm(iSegment, iSegForm + 1, String.Format("{0} {1}",
					StringValue(iSegment, iSegForm), StringValue(iSegment, iSegForm + 1)), 0);
				this.RemoveSegmentForm(iSegment, iSegForm);

				// The surface text will not change, but we'll need to
				// reparse paragraph to identify matching phrases, and to sync with the ids.
				InvalidateParagraphInfo();
				return NeedToRebuildParagraphContentFromAnnotations;
			}

			internal virtual bool BreakPhraseAnnotation(int iSegment, int iSegForm)
			{
				string[] words = StringValue(iSegment, iSegForm).Split(new char[] { ' ' });
				// insert these in reverse order.
				Array.Reverse(words);
				foreach (string word in words)
				{
					this.InsertSegmentForm(iSegment, iSegForm + 1, WficGuid, word);
				}
				this.RemoveSegmentForm(iSegment, iSegForm);

				InvalidateParagraphInfo();
				return NeedToRebuildParagraphContentFromAnnotations;
			}

			/// <summary>
			/// If we change the xml definition for the paragraph, we need to rebuild the real paragraph content at some point.
			/// </summary>
			void InvalidateParagraphInfo()
			{
				m_fNeedToRebuildParagraphContentFromAnnotations = true;
			}

			internal bool NeedToRebuildParagraphContentFromAnnotations
			{
				get { return m_fNeedToRebuildParagraphContentFromAnnotations; }
			}

			/// <summary>
			/// Given an XmlNode from our test data representing an StTxtPara, get the (obsolete structure) CmBaseAnnotation
			/// nodes representing its segments.
			/// </summary>
			static XmlNodeList SegmentNodeList(XmlNode paraDefn)
			{
				Debug.Assert(paraDefn != null);
				XmlNodeList cbaSegmentNodes = paraDefn.SelectNodes("Segments16/CmBaseAnnotation");
				return cbaSegmentNodes;
			}

			XmlNodeList SegmentNodeList()
			{
				return SegmentNodeList(m_paraDefn);
			}

			/// <summary>
			/// Given an XmlNode representing a CmBaseAnnotation, find the id of the associated analysis object.
			/// </summary>
			/// <param name="cbaNode"></param>
			/// <returns></returns>
			internal static int GetAnalysisId(XmlNode cbaNode)
			{
				return XmlUtils.GetMandatoryIntegerAttributeValue(cbaNode, "id");
			}
		}

		/// <summary>
		/// This can be used to build a Text based upon an xml specification.
		/// Note: the xml specification will be modified to save the expected state of the text.
		/// </summary>
		protected internal class TextBuilder
		{
			LcmCache m_cache = null;
			IText m_text = null;
			XmlNode m_textDefn = null;

			internal TextBuilder(TextBuilder tbToClone)
				: this(tbToClone.m_cache)
			{
				m_text = tbToClone.m_text;
				this.SelectedNode = ParagraphBuilder.Snapshot(tbToClone.SelectedNode);
			}

			internal TextBuilder(LcmCache cache)
			{
				//m_owner = owner;
				m_cache = cache;
			}

			internal IText BuildText(XmlNode textDefn)
			{
				if (textDefn.Name != "Text")
					return null;
				m_textDefn = textDefn;
				// 1. Create the new text and give it an owner.
				m_text = this.CreateText();
				textDefn.Attributes["id"].Value = m_text.Hvo.ToString();

				XmlNode name = textDefn.SelectSingleNode("Name5/AUni");
				// 2. If we have a name, set it.
				if (name != null && name.InnerText.Length > 0)
				{
					string wsAbbr = XmlUtils.GetOptionalAttributeValue(name, "ws");
					int ws = 0;
					if (wsAbbr != null)
						ws = m_cache.ServiceLocator.WritingSystemManager.GetWsFromStr(wsAbbr);
					if (ws == 0)
						ws = m_cache.DefaultVernWs;
					this.SetName(name.InnerText, ws);
				}

				// 3. Create a body for the text;
				XmlNode contents = textDefn.SelectSingleNode("Contents5054/StText");
				if (contents == null)
					return m_text;
				this.CreateContents(m_text);

				// 4. Create each paragraph for the text.
				XmlNodeList paragraphs = contents.SelectNodes("Paragraphs14/StTxtPara");
				if (paragraphs != null)
				{
					foreach (XmlNode paraDef in paragraphs)
					{
						ParagraphBuilder pb = new ParagraphBuilder(m_cache, m_text.ContentsOA.ParagraphsOS);
						pb.BuildParagraphContent(paraDef);
					}
				}

				return m_text;
			}

			/// <summary>
			///
			/// </summary>
			/// <returns></returns>
			public void GenerateAnnotationIdsFromDefn()
			{
				foreach (IStTxtPara para in m_text.ContentsOA.ParagraphsOS)
				{
					ParagraphBuilder pb = GetParagraphBuilder(para);
					pb.GenerateParaContentFromAnnotations();
				}
			}

			/// <summary>
			/// Creates the text to be used by this TextBuilder
			/// </summary>
			/// <param name="fCreateContents"></param>
			/// <returns></returns>
			public IText CreateText(bool fCreateContents)
			{
				Debug.Assert(m_text == null);
				m_text = CreateText();
				if (fCreateContents)
					CreateContents(m_text);
				return m_text;
			}

			private IText CreateText()
			{
				Debug.Assert(m_cache != null);
				IText newText = m_cache.ServiceLocator.GetInstance<ITextFactory>().Create();
				//Debug.Assert(m_owner != null);
				//m_owner.Add(newText);
				return newText;
			}


			IStText CreateContents(IText text)
			{
				Debug.Assert(text != null);
				IStText body1 = m_cache.ServiceLocator.GetInstance<IStTextFactory>().Create();
				text.ContentsOA = body1;
				return body1;
			}

			/// <summary>
			///
			/// </summary>
			/// <returns></returns>
			public IStTxtPara AppendNewParagraph()
			{
				XmlNode lastParagraph = null;
				if (m_textDefn != null)
				{
					// Append insert a new paragraph node.
					XmlNode paragraphs = m_textDefn.SelectSingleNode("//Paragraphs14");
					lastParagraph = paragraphs.LastChild;
				}
				return InsertNewParagraphAfter(lastParagraph);
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="paragraphDefnToInsertAfter"></param>
			/// <returns></returns>
			public IStTxtPara InsertNewParagraphAfter(XmlNode paragraphDefnToInsertAfter)
			{
				ParagraphBuilder pb = new ParagraphBuilder(m_cache, m_text.ContentsOA.ParagraphsOS);
				IStTxtPara newPara = null;
				if (m_textDefn != null)
				{
					// Append insert a new paragraph node.
					XmlElement newParaDefn = InsertNewParagraphDefn(paragraphDefnToInsertAfter);
					newPara = pb.CreateNewParagraph(newParaDefn);
				}
				else
				{
					newPara = pb.AppendParagraph();
				}
				return newPara;
			}

			internal void DeleteText()
			{
				//m_owner.Remove(m_text);
				HvoActualStText = 0;
			}

			internal void DeleteParagraphDefn(IStTxtPara para)
			{
				ParagraphBuilder pb = GetParagraphBuilder(para);
				DeleteParagraphDefn(pb.ParagraphDefinition);
			}

			internal void DeleteParagraphDefn(XmlNode paraDefn)
			{
				if (paraDefn != null)
				{
					XmlNode paragraphs = paraDefn.ParentNode;
					paragraphs.RemoveChild(paraDefn);
				}
			}


			/// <summary>
			///
			/// </summary>
			/// <param name="para"></param>
			/// <param name="startingNodeToMoveToNewPara"></param>
			/// <returns>the XmlNode corresponding to the new paragraph defn</returns>
			internal XmlNode InsertParagraphBreak(IStTxtPara para, XmlNode startingNodeToMoveToNewPara)
			{
				// IStTxtPara/Segments/CmBaseAnnotation[]
				ParagraphBuilder pb = GetParagraphBuilder(para);
				// make a new paragraph node after the current one
				if (pb.ParagraphDefinition != null)
				{
					XmlNode newParaDefn = InsertNewParagraphDefn(pb.ParagraphDefinition);
					ParagraphBuilder pbNew = new ParagraphBuilder(m_cache, m_text.ContentsOA.ParagraphsOS, newParaDefn);
					// if we are breaking at a segment form, create new segment and move the rest of the segment forms
					XmlNode startingSegNode = null;
					if (startingNodeToMoveToNewPara.ParentNode.Name == "SegmentForms37")
					{
						pbNew.CreateSegmentNode();
						XmlNode newSegForms = pbNew.CreateSegmentForms();
						MoveSiblingNodes(startingNodeToMoveToNewPara, newSegForms, null);

						// get the next segment if any.
						int iSeg = GetIndexAmongSiblings(startingNodeToMoveToNewPara.ParentNode.ParentNode);
						List<XmlNode> segmentNodes = pb.SegmentNodes();
						startingSegNode = segmentNodes[iSeg].NextSibling;
					}
					else
					{
						Debug.Assert(startingNodeToMoveToNewPara.ParentNode.Name == "Segments16");
						startingSegNode = startingNodeToMoveToNewPara;
					}
					if (startingSegNode != null)
					{
						// move all the remaining segments beginning at startingSegNode into the new paragraph.
						MoveSiblingNodes(startingSegNode, pbNew.ParagraphDefinition.LastChild, null);
					}
					return pbNew.ParagraphDefinition;
				}
				return null;
			}

			private static XmlElement InsertNewParagraphDefn(XmlNode paraDefnToInsertAfter)
			{
				XmlNode paragraphs = paraDefnToInsertAfter.ParentNode;
				XmlElement newParaDefn = paragraphs.OwnerDocument.CreateElement("StTxtPara");
				newParaDefn.SetAttribute("id", "");
				paragraphs.InsertAfter(newParaDefn, paraDefnToInsertAfter);
				XmlElement newSegments = newParaDefn.OwnerDocument.CreateElement("Segments16");
				newParaDefn.AppendChild(newSegments);
				return newParaDefn;
			}

			internal void DeleteParagraphBreak(XmlNode paraNodeToDelBreak)
			{
				int iParaToDeleteBreak = GetIndexAmongSiblings(paraNodeToDelBreak);
				XmlNodeList paragraphs = paraNodeToDelBreak.ParentNode.SelectNodes("StTxtPara");
				XmlNode nextParaNode = paragraphs[iParaToDeleteBreak + 1];
				// IStTxtPara/Segments/CmBaseAnnotation[]
				MoveSiblingNodes(nextParaNode.FirstChild.FirstChild, paraNodeToDelBreak.FirstChild, null);
				DeleteParagraphDefn(nextParaNode);
			}
			internal void DeleteParagraphBreak(IStTxtPara para)
			{
				ParagraphBuilder pb1 = GetParagraphBuilder(para);
				DeleteParagraphBreak(pb1.ParagraphDefinition);
			}

			internal int HvoActualStText
			{
				get
				{
					if (m_text != null)
						return m_text.ContentsOA.Hvo;
					else
						return 0;
				}

				set
				{
					if (value == 0)
					{
						m_text = null;
						m_textDefn = null;
					}
					else if (HvoActualStText != value)
					{
						IStText stText = m_cache.ServiceLocator.GetInstance<IStTextRepository>().GetObject(value);
						m_text = stText.Owner as IText;
						m_textDefn = null;
					}
				}

			}

			internal XmlNode SelectNode(IStTxtPara para)
			{
				return SelectNode(para, -1);
			}

			internal XmlNode SelectNode(IStTxtPara para, int iSeg)
			{
				return SelectNode(para, iSeg, -1);
			}

			internal XmlNode SelectNode(IStTxtPara para, int iSeg, int iSegForm)
			{
				if (para == null)
				{
					SelectedNode = m_textDefn;
				}
				else
				{
					ParagraphBuilder pb = this.GetParagraphBuilder(para);
					if (iSegForm >= 0)
						SelectedNode = pb.SegmentFormNode(iSeg, iSegForm);
					else if (iSeg >= 0)
						SelectedNode = pb.SegmentNodes()[iSeg];
					else
						SelectedNode = pb.ParagraphDefinition;
				}
				return SelectedNode;
			}

			/// <summary>
			/// The node we have selected as a "cursor" position for editing.
			/// </summary>
			XmlNode m_selectedNode = null;

			internal XmlNode SelectedNode
			{
				get
				{
					if (m_selectedNode != null && m_textDefn != null &&
						m_selectedNode != m_textDefn &&
						m_textDefn.OwnerDocument == m_selectedNode.OwnerDocument)
					{
						// make sure the selected node is owned by the current Text defn
						XmlNodeList decendants = m_textDefn.SelectNodes(String.Format("//{0}", m_selectedNode.LocalName));
						bool fFound = false;
						foreach (XmlNode node in decendants)
						{
							if (m_selectedNode == node)
							{
								fFound = true;
								break;
							}
						}
						if (!fFound)
							m_selectedNode = null;  // reset an invalidated node.
					}
					if (m_selectedNode == null || m_textDefn == null)
						m_selectedNode = m_textDefn;
					return m_selectedNode;
				}

				set
				{
					m_selectedNode = value;
					SetTextDefnFromSelectedNode(value);
				}
			}

			internal XmlNode SetTextDefnFromSelectedNode(XmlNode node)
			{
				// try to find an ancestor StText
				XmlNode stTextNode = null;
				if (node != null)
				{
					if (node.LocalName == "StText")
						stTextNode = node;
					else
						stTextNode = node.SelectSingleNode("ancestor::StText");
					if (stTextNode == null)
						stTextNode = node.SelectSingleNode("//StText");
					this.StTextDefinition = stTextNode;
				}
				return stTextNode;
			}

			internal IText ActualText
			{
				get
				{
					if (m_text == null)
						return null;
					return m_text as IText;
				}
			}

			internal IStText ActualStText
			{
				get
				{
					if (m_text == null || m_text.ContentsOA.Hvo == 0)
						return null;
					return m_text.ContentsOA;
				}
			}

			internal XmlNode TextDefinition
			{
				get { return m_textDefn; }
			}

			internal XmlNode StTextDefinition
			{
				get
				{
					if (m_textDefn == null)
						return null;
					return m_textDefn.SelectSingleNode("//StText");
				}
				set
				{
					if (value == null)
						m_textDefn = null;
					else
						m_textDefn = value.SelectSingleNode("ancestor::Text");
				}
			}

			/// <summary>
			/// get the paragraph builder for the given hvoPara
			/// </summary>
			/// <param name="para"></param>
			/// <returns></returns>
			internal ParagraphBuilder GetParagraphBuilder(IStTxtPara para)
			{
				int iPara = para.IndexInOwner;
				return new ParagraphBuilder(m_textDefn, m_text, iPara);
			}

			/// <summary>
			/// Note: paraDefn may not yet be mapped to a real paragraph Hvo.
			/// </summary>
			/// <param name="paraDefn"></param>
			/// <returns></returns>
			internal ParagraphBuilder GetParagraphBuilderForNotYetExistingParagraph(XmlNode paraDefn)
			{
				// Paragraphs14/StTxtPara/Segments/
				return new ParagraphBuilder(m_cache, m_text.ContentsOA.ParagraphsOS, paraDefn);
			}

			void SetName(string name, int ws)
			{
				m_text.Name.set_String(ws, name);
			}
		}

	}
}