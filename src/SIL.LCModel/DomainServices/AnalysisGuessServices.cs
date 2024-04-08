// Copyright (c) 2009-2022 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//
// File: AnalysisGuessServices.cs
// Responsibility: pyle
//
// <remarks>
// </remarks>

using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Icu;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.Core.WritingSystems;
using SIL.LCModel.DomainImpl;
using SIL.LCModel.Infrastructure;


namespace SIL.LCModel.DomainServices
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	///
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class AnalysisGuessServices
	{
		/// <summary>
		///
		/// </summary>
		/// <param name="cache"></param>
		public AnalysisGuessServices(LcmCache cache)
		{
			Cache = cache;
			var wfFactory = Cache.ServiceLocator.GetInstance<IWfiWordformFactory>();
			var wsVern = Cache.DefaultVernWs;
			m_emptyWAG = wfFactory.Create(TsStringUtils.MakeString("", wsVern));
			m_nullWAG = new NullWAG();
		}

		/// <summary>
		///
		/// </summary>
		public enum OpinionAgent
		{
			/// <summary>
			///
			/// </summary>
			Computer = -1,
			/// <summary>
			///
			/// </summary>
			Parser = 0,
			/// <summary>
			///
			/// </summary>
			Human = 1,
		}

		LcmCache Cache { get; set; }

		private IDictionary<IWfiAnalysis, ICmAgentEvaluation> m_analysisApprovalTable;
		/// <summary>
		/// Table that has user opinions about analyses.
		/// </summary>
		IDictionary<IWfiAnalysis, ICmAgentEvaluation> AnalysisApprovalTable
		{
			get
			{
				if (m_analysisApprovalTable == null)
					LoadAnalysisApprovalTable();
				return m_analysisApprovalTable;
			}
			set { m_analysisApprovalTable = value; }
		}

		private HashSet<IWfiAnalysis> m_computerApprovedTable;
		/// <summary>
		/// Table for which analyses have been approved by a computer (i.e. for matching words to Entries)
		/// </summary>
		HashSet<IWfiAnalysis> ComputerApprovedTable
		{
			get
			{
				if (m_computerApprovedTable == null)
					LoadComputerApprovedTable();
				return m_computerApprovedTable;
			}
			set { m_computerApprovedTable = value; }
		}

		private HashSet<IWfiAnalysis> m_parserApprovedTable;
		/// <summary>
		/// Table for which analyses have been approved by grammatical parser
		/// </summary>
		HashSet<IWfiAnalysis> ParserApprovedTable
		{
			get
			{
				if (m_parserApprovedTable == null)
					LoadParserApprovedTable();
				return m_parserApprovedTable;
			}
			set { m_parserApprovedTable = value; }
		}

		class PriorityCount
		{
			public int priority = 0;
			public int count = 0;
		}

		private IDictionary<IAnalysis, Dictionary<IAnalysis, IAnalysis>> m_guessTable;
		IDictionary<IAnalysis, Dictionary<IAnalysis, IAnalysis>> GuessTable
		{
			get
			{
				if (m_guessTable == null)
					GuessTable = new Dictionary<IAnalysis, Dictionary<IAnalysis, IAnalysis>>();
				return m_guessTable;
			}
			set { m_guessTable = value; }
		}

		private readonly IAnalysis m_emptyWAG;
		private readonly IAnalysis m_nullWAG;

		/// <summary>
		/// Informs the guess service that the indicated occurrence is being replaced with the specified new
		/// analysis. If necessary clear the GuessTable. If possible update it. The most common and
		/// performance-critical case is confirming a guess. Return true if the cache was changed.
		/// </summary>
		public bool UpdatingOccurrence(IAnalysis oldAnalysis, IAnalysis newAnalysis)
		{
			if (m_guessTable == null)
				return false; // already cleared, forget it.
			if (oldAnalysis == newAnalysis)
				return false; // nothing changed, no problem.
			if (!(oldAnalysis is IWfiWordform))
			{
				// In general, no predicting what effect it has on the guess for the
				// wordform or analysis that owns the old analysis. If the old analysis is
				// not the default for its owner or owner's owner, we are OK, but that's too rare
				// to worry about.
				ClearGuessData();
				return true;
			}
			if (newAnalysis is IWfiWordform || newAnalysis.Wordform == null)
				return false; // unlikely but doesn't mess up our guesses.
			var result = false;
			// if the new analysis is NOT the guess for one of its owners, one more occurrence might
			// make it the guess, so we need to regenerate.
			IAnalysis currentDefault;
			if (!TryGetConditionedGuess(newAnalysis.Wordform, m_nullWAG, out currentDefault))
			{
				// No need to clear the cache.
				result = true;
			}
			else if (currentDefault != newAnalysis)
			{
				// Some other analysis just became more common...maybe now the default?
				ClearGuessData();
				return true;
			}
			if (newAnalysis is IWfiAnalysis)
				return result;
			if (!TryGetConditionedGuess(newAnalysis.Analysis, m_nullWAG, out currentDefault))
			{
				// No need to clear the cache.
				result = true;
			}
			else if (currentDefault != newAnalysis)
			{
				// Some other analysis just became more common...maybe now the default?
				ClearGuessData();
				return true;
			}
			// We haven't messed up any guesses so the guess table can survive.
			return result;
		}

		bool IsNotDisapproved(IWfiAnalysis wa)
		{
			ICmAgentEvaluation cae;
			if (AnalysisApprovalTable.TryGetValue(wa, out cae))
				return cae.Approves;
			return true; // no opinion
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="wa"></param>
		/// <returns>by default, returns Human agent (e.g. could be approved in text)</returns>
		public OpinionAgent GetOpinionAgent(IWfiAnalysis wa)
		{
			if (IsHumanApproved(wa))
				return OpinionAgent.Human;
			if (IsParserApproved(wa))
				return OpinionAgent.Parser;
			if (IsComputerApproved(wa))
				return OpinionAgent.Computer;
			return OpinionAgent.Human;

		}
		/// <summary>
		///
		/// </summary>
		/// <param name="wa"></param>
		/// <returns></returns>
		public bool IsHumanApproved(IWfiAnalysis wa)
		{
			ICmAgentEvaluation cae;
			if (AnalysisApprovalTable.TryGetValue(wa, out cae))
				return cae.Approves;
			return false; // no opinion
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="candidate"></param>
		/// <returns></returns>
		public bool IsComputerApproved(IWfiAnalysis candidate)
		{
			return ComputerApprovedTable.Contains(candidate);
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="candidate"></param>
		/// <returns></returns>
		public bool IsParserApproved(IWfiAnalysis candidate)
		{
			return ParserApprovedTable.Contains(candidate);
		}

		void LoadAnalysisApprovalTable()
		{
			var dictionary = new Dictionary<IWfiAnalysis, ICmAgentEvaluation>();
			foreach(var analysis in Cache.ServiceLocator.GetInstance<IWfiAnalysisRepository>().AllInstances())
				foreach (var ae in analysis.EvaluationsRC)
					if (((ICmAgent) ae.Owner).Human)
						dictionary[analysis] = ae;
			AnalysisApprovalTable = dictionary;
		}

		void LoadComputerApprovedTable()
		{
			IEnumerable<IWfiAnalysis> list = GetAgentApprovedList(Cache.LangProject.DefaultComputerAgent);
			ComputerApprovedTable = new HashSet<IWfiAnalysis>(list);
		}

		/// <summary>
		/// Get all the analyses approved by the specified agent.
		/// </summary>
		/// <param name="agent"></param>
		/// <returns></returns>
		private IEnumerable<IWfiAnalysis> GetAgentApprovedList(ICmAgent agent)
		{
			return Cache.ServiceLocator.GetInstance<IWfiAnalysisRepository>().AllInstances().Where(
				analysis => analysis.GetAgentOpinion(agent) == Opinions.approves);
		}

		void LoadParserApprovedTable()
		{
			IEnumerable<IWfiAnalysis> list = GetAgentApprovedList(Cache.LangProject.DefaultParserAgent);
			ParserApprovedTable = new HashSet<IWfiAnalysis>(list);
		}

		/// <summary>
		/// Try to get the default analysis for form conditioned on the previous word form.
		/// If form is an analysis,then the default analysis is a gloss.
		/// If form is a wordform, then try to get the default gloss of the default analysis if it exists.
		/// Use m_emptyWAG as previous word form for the first analysis in a segment.
		/// Use m_nullWAG as previous word form for backoff.
		/// </summary>
		/// <param name="form">the form that you want an analysis for</param>
		/// <param name="previous">the form to be conditioned on</param>
		/// <param name="analysis">the resulting analysis</param>
		/// <returns>bool</returns>
		private bool TryGetConditionedGuess(IAnalysis form, IAnalysis previous, out IAnalysis analysis)
		{
			if (!GuessTable.ContainsKey(form))
			{
				// Fill in GuessTable.
				var default_analyses = GetDefaultAnalyses(form);
				m_guessTable[form] = default_analyses;
			}
			if (!GuessTable[form].ContainsKey(previous))
			{
				// back off to all forms.
				previous = m_nullWAG;
				if (!GuessTable[form].ContainsKey(previous))
				{
					// form doesn't occur in the interlinear texts.
					analysis = m_nullWAG;
					return false;
				}
			}
			analysis = GuessTable[form][previous];
			if (analysis != null && analysis is IWfiAnalysis)
				{
				// Get the best gloss for analysis.
				if (TryGetConditionedGuess(analysis, previous, out IAnalysis gloss))
				{
					analysis = gloss;
				}
			}
			return true;
		}

		/// <summary>
		/// Get the default analyses for the given form conditioned on the previous word forms.
		/// If form is an analysis,then the default analyses are glosses.
		/// Use m_emptyWAG as previous word form for the first analysis in a segment.
		/// Use m_nullWAG as previous word form for backoff.
		/// </summary>
		/// <param name="form">the form that you want analyses for</param>
		/// <returns>Dictionary<IAnalysis, IAnalysis></returns>
		private Dictionary<IAnalysis, IAnalysis> GetDefaultAnalyses(IAnalysis form)
		{
			Dictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>> counts;
			// Get counts.
			if (form is IWfiWordform wordform)
			{
				counts = GetAnalysisCounts(wordform);
			} else if (form is IWfiAnalysis analysis)
			{
				counts = GetGlossCounts(analysis);
			} else
			{
				return null;
			}
			Dictionary<IAnalysis, IAnalysis> defaults = new Dictionary<IAnalysis, IAnalysis>();
			foreach (IAnalysis previous in counts.Keys)
			{
				// Get highest scoring analysis.
				int max_count = 0;
				int max_priority = 0;
				IAnalysis best = null;
				foreach (IAnalysis analysis in counts[previous].Keys)
				{
					int count = counts[previous][analysis].count;
					int priority = counts[previous][analysis].priority;
					if (priority > max_priority ||
						priority == max_priority && count > max_count)
					{
						// This is a better analysis.
						max_priority = priority;
						max_count = count;
						best = analysis;
					}
				}
				defaults[previous] = best;
			}
			return defaults;
		}

		/// <summary>
		/// Get analysis counts for the given word form conditioned on the previous word form.
		/// Use m_emptyWAG as previous word form for the first analysis in a segment.
		/// Use m_nullWAG as previous word form for backoff.
		/// </summary>
		/// <param name="wordform">the form that you want an analysis for</param>
		/// <returns>Dictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>></returns>
		private Dictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>> GetAnalysisCounts(IWfiWordform wordform)
		{
			var counts = new Dictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>>();
			var segs = new HashSet<ISegment>();
			foreach (ISegment seg in wordform.OccurrencesInTexts)
			{
				if (segs.Contains(seg)) continue;
				segs.Add(seg);
				for (int i = 0; i < seg.AnalysesRS.Count; i++)
				{
					IAnalysis analysis = seg.AnalysesRS[i];
					if (analysis.Wordform != wordform) continue;
					IAnalysis previous = GetPreviousWordform(seg, i);
					if (analysis is IWfiGloss)
					{
						// Get analysis for gloss.
						analysis = analysis.Analysis;
					}
					if (analysis is IWfiAnalysis)
					{
						// Add high priority count to analysis.
						AddAnalysisCount(previous, analysis, 4, counts);
					}
					if (analysis is IWfiWordform wordform2)
					{
						// Add lower priority counts to the word form's analyses.
						// (These have not been confirmed.)
						foreach (IWfiAnalysis analysis2 in wordform2.AnalysesOC)
						{
							if (IsNotDisapproved(analysis2))
							{
								int priority = IsHumanApproved(analysis2) ? 3 : IsParserApproved(analysis2) ? 2 : 1;
								AddAnalysisCount(previous, analysis2, priority, counts);
							}
						}

					}
				}
			}
			return counts;
		}

		/// <summary>
		/// Get gloss counts for the given analysis conditioned on the previous word form.
		/// If form is an analysis,then the analysis counts are for glosses.
		/// Use m_emptyWAG as previous word form for the first analysis in a segment.
		/// Use m_nullWAG as previous word form for backoff.
		/// </summary>
		/// <returns>Dictionary<IAnalysis, Dictionary<IAnalysis, int>></returns>
		private Dictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>> GetGlossCounts(IWfiAnalysis analysis)
		{
			var counts = new Dictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>>();
			var segs = new HashSet<ISegment>();
			foreach (ISegment seg in analysis.Wordform.OccurrencesInTexts)
			{
				if (segs.Contains(seg)) continue;
				segs.Add(seg);
				for (int i = 0; i < seg.AnalysesRS.Count; i++)
				{
					// Get gloss for analysis.
					IAnalysis previous = GetPreviousWordform(seg, i);
					IAnalysis gloss = seg.AnalysesRS[i];
					if (gloss is IWfiGloss)
					{
						if (gloss.Analysis == analysis)
						{
							// Add high priority count to gloss.
							AddAnalysisCount(previous, gloss, 5, counts);
						}
					}
					else if (gloss is IWfiAnalysis analysis2)
					{
						if (analysis2 == analysis)
						{
							foreach (IWfiGloss gloss2 in analysis2.MeaningsOC)
							{
								// Add lower priority count to gloss.
								AddAnalysisCount(previous, gloss2, 4, counts);
							}
						}
					}
					else if (gloss is IWfiWordform wordform)
					{
						foreach (IWfiAnalysis analysis3 in wordform.AnalysesOC)
						{
							if (analysis3 == analysis && IsNotDisapproved(analysis3))
							{
								int priority = IsHumanApproved(analysis3) ? 3 : IsParserApproved(analysis3) ? 2 : 1;
								foreach (IWfiGloss gloss3 in analysis3.MeaningsOC)
								{
									// Add lower priority count to gloss.
									AddAnalysisCount(previous, gloss3, priority, counts);
								}

							}
						}
					}
				}
			}
			return counts;
		}

		private IAnalysis GetPreviousWordform(ISegment seg, int i)
		{
			if (i == 0)
				return m_emptyWAG;
			IAnalysis previous = seg.AnalysesRS[i - 1];
			if (previous is IWfiAnalysis || previous is IWfiGloss)
			{
				previous = previous.Wordform;
			}
			return previous;
		}

		private void AddAnalysisCount(IAnalysis previous, IAnalysis analysis, int priority, Dictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>> counts)
		{
			if (previous != m_nullWAG)
			{
				// Record count for backoff.
				AddAnalysisCount(m_nullWAG, analysis, priority, counts);
			}
			if (!counts.ContainsKey(previous))
			{
				counts[previous] = new Dictionary<IAnalysis, PriorityCount>();
			}
			if (!counts[previous].ContainsKey(analysis))
			{
				counts[previous][analysis] = new PriorityCount();
			}
			if (counts[previous][analysis].priority > priority)
			{
				// Ignore this count because its priority is too low.
				return;
			}
			if (counts[previous][analysis].priority < priority)
			{
				// Start a new priority count.
				counts[previous][analysis].priority = priority;
				counts[previous][analysis].count = 0;
			}
			counts[previous][analysis].count += 1;
		}

		/// <summary>
		/// This class stores the relevant database ids for information which can generate a
		/// default analysis for a WfiWordform that has no analyses, but whose form exactly
		/// matches a LexemeForm or an AlternateForm of a LexEntry.
		/// </summary>
		private struct EmptyWwfInfo
		{
			public readonly IMoForm Form;
			public readonly IMoMorphSynAnalysis Msa;
			public readonly IPartOfSpeech Pos;
			public readonly ILexSense Sense;
			public EmptyWwfInfo(IMoForm form, IMoMorphSynAnalysis msa, IPartOfSpeech pos, ILexSense sense)
			{
				Form = form;
				Msa = msa;
				Pos = pos;
				Sense = sense;
			}
		}

		// Allows comparing strings in dictionary.
		class TsStringEquator : IEqualityComparer<ITsString>
		{
			public bool Equals(ITsString x, ITsString y)
			{
				return x.Equals(y); // NOT ==
			}

			public int GetHashCode(ITsString obj)
			{
				return(obj.Text ?? "").GetHashCode() ^ obj.get_WritingSystem(0);
			}
		}

		/// <summary>
		/// For a given text, go through all the paragraph Segment.Analyses looking for words
		/// to be candidates for guesses provided by matching to entries in the Lexicon.
		/// For a word to be a candidate for a computer guess it must have
		/// 1) no analyses that have human approved/disapproved evaluations and
		/// 2) no analyses that have computer approved/disapproved evaluations
		/// </summary>
		IDictionary<IWfiWordform, EmptyWwfInfo> MapWordsForComputerGuessesToBestMatchingEntry(IStText stText)
		{
			var wordsToMatch = new Dictionary<IWfiWordform, ITsString>();
			foreach (IStTxtPara para in stText.ParagraphsOS)
			{
				foreach (
					var analysisOccurrence in
						SegmentServices.StTextAnnotationNavigator.GetWordformOccurrencesAdvancingInPara(para)
							.Where(ao => ao.Analysis is IWfiWordform))
				{
					var word = analysisOccurrence.Analysis.Wordform;
					if (!wordsToMatch.ContainsKey(word) && !HasAnalysis(word))
					{
						var tssWord = analysisOccurrence.BaselineText;
						wordsToMatch[word] = tssWord;
					}
				}
			}
			return MapWordsForComputerGuessesToBestMatchingEntry(wordsToMatch);
		}

		/// <summary>
		/// This overload finds defaults for a specific wordform
		/// </summary>
		private Dictionary<IWfiWordform, EmptyWwfInfo> MapWordsForComputerGuessesToBestMatchingEntry(IWfiWordform wf, int ws)
		{
			var wordsToMatch = new Dictionary<IWfiWordform, ITsString>();
			wordsToMatch[wf] = wf.Form.get_String(ws);
			return MapWordsForComputerGuessesToBestMatchingEntry(wordsToMatch);
		}


		/// <summary>
		/// This overload finds guesses for wordforms specified in a dictionary that maps a wordform to the
		/// string that we want to match (might be a different case form).
		/// </summary>
		/// <param name="wordsToMatch"></param>
		/// <returns></returns>
		private Dictionary<IWfiWordform, EmptyWwfInfo> MapWordsForComputerGuessesToBestMatchingEntry(Dictionary<IWfiWordform, ITsString> wordsToMatch)
		{
			var matchingMorphs = new Dictionary<ITsString, IMoStemAllomorph>(new TsStringEquator());
			foreach (var tssWord in wordsToMatch.Values)
				matchingMorphs[tssWord] = null;
			MorphServices.GetMatchingMonomorphemicMorphs(Cache, matchingMorphs);
			var mapEmptyWfInfo = new Dictionary<IWfiWordform, EmptyWwfInfo>();
			foreach (var kvp in wordsToMatch)
			{
				var word = kvp.Key;
				var tssWord = kvp.Value;
				var bestMatchingMorph = matchingMorphs[tssWord];
				if (bestMatchingMorph != null)
				{
					var entryOrVariant = bestMatchingMorph.OwnerOfClass<ILexEntry>();
					ILexEntry mainEntry;
					ILexSense sense;
					GetMainEntryAndSense(entryOrVariant, out mainEntry, out sense);
					if (sense == null && mainEntry.SensesOS.Count > 0)
					{
						sense = mainEntry.SensesOS.Where(s => s.MorphoSyntaxAnalysisRA is IMoStemMsa)
							.FirstOrDefault();
					}
					IMoStemMsa msa = null;
					IPartOfSpeech pos = null;
					if (sense != null)
					{
						msa = (IMoStemMsa) sense.MorphoSyntaxAnalysisRA;
						pos = msa.PartOfSpeechRA;
					}
					// map the word to its best entry.
					var entryInfo = new EmptyWwfInfo(bestMatchingMorph, msa, pos, sense);
					mapEmptyWfInfo.Add(word, entryInfo);
				}

			}
			return mapEmptyWfInfo;
		}

		private void GetMainEntryAndSense(ILexEntry entryOrVariant, out ILexEntry mainEntry, out ILexSense sense)
		{
			sense = null;
			// first see if this is a variant of another entry.
			var entryRef = DomainObjectServices.GetVariantRef(entryOrVariant, true);
			if (entryRef != null)
			{
				// get the main entry or sense.
				var component = entryRef.ComponentLexemesRS[0] as IVariantComponentLexeme;
				if (component is ILexSense)
				{
					sense = component as ILexSense;
					mainEntry = sense.Entry;
				}
				else
				{
					mainEntry = component as ILexEntry;
					// consider using the sense of the variant, if it has one. (LT-9681)
				}
			}
			else
			{
				mainEntry = entryOrVariant;
			}
		}

		private bool HasAnalysis(IWfiWordform word)
		{
			return word.AnalysesOC.Count > 0;
		}

		/// <summary>
		/// For the given text, find words for which we can generate analyses that match lexical entries.
		/// </summary>
		/// <param name="stText"></param>
		public void GenerateEntryGuesses(IStText stText)
		{
			GenerateEntryGuesses(MapWordsForComputerGuessesToBestMatchingEntry(stText));
		}

		/// <summary>
		/// For the given wordform, find words for which we can generate analyses that match lexical entries.
		/// </summary>
		internal void GenerateEntryGuesses(IWfiWordform wf, int ws)
		{
			GenerateEntryGuesses(MapWordsForComputerGuessesToBestMatchingEntry(wf, ws));
		}

		/// <summary>
		/// For the given sequence of correspondences, generate analyses.
		/// </summary>
		private void GenerateEntryGuesses(IDictionary<IWfiWordform, EmptyWwfInfo> map)
		{
			var waFactory = Cache.ServiceLocator.GetInstance<IWfiAnalysisFactory>();
			var wgFactory = Cache.ServiceLocator.GetInstance<IWfiGlossFactory>();
			var wmbFactory = Cache.ServiceLocator.GetInstance<IWfiMorphBundleFactory>();
			var computerAgent = Cache.LangProject.DefaultComputerAgent;
			foreach (var keyPair in map)
			{
				var ww = keyPair.Key;
				var info = keyPair.Value;
				if (!HasAnalysis(ww))
				{
					NonUndoableUnitOfWorkHelper.DoUsingNewOrCurrentUowOrSkip(Cache.ActionHandlerAccessor,
						"Trying to generate guesses during PropChanged when we can't save them.",
						() =>
							{
								var newAnalysis = waFactory.Create(ww, wgFactory);
								newAnalysis.CategoryRA = info.Pos;
								// Not all entries have senses.
								if (info.Sense != null)
								{
									// copy all the gloss alternatives from the sense into the word gloss.
									IWfiGloss wg = newAnalysis.MeaningsOC.First();
									wg.Form.MergeAlternatives(info.Sense.Gloss);
								}
								var wmb = wmbFactory.Create();
								newAnalysis.MorphBundlesOS.Add(wmb);
								if (info.Form != null)
									wmb.MorphRA = info.Form;
								if (info.Msa != null)
									wmb.MsaRA = info.Msa;
								if (info.Sense != null)
									wmb.SenseRA = info.Sense;

								// Now, set up an approved "Computer" evaluation of this generated analysis
								computerAgent.SetEvaluation(newAnalysis, Opinions.approves);
								// Clear GuessTable entry.
								if (GuessTable.ContainsKey(ww))
									GuessTable.Remove(ww);
							});
				}
			}
		}

		/// <summary>
		/// Whenever the data we depend upon changes, use this to make sure we load the latest Guess data.
		/// </summary>
		public void ClearGuessData()
		{
			GuessTable = null;
			ParserApprovedTable = null;
			ComputerApprovedTable = null;
			AnalysisApprovalTable = null;
		}

		/// <summary>
		/// Given a wordform, provide the best analysis guess for it (using the default vernacular WS).
		/// </summary>
		/// <param name="wf"></param>
		/// <returns></returns>
		public IAnalysis GetBestGuess(IWfiWordform wf)
		{
			return GetBestGuess(wf, wf.Cache.DefaultVernWs);
		}

		/// <summary>
		/// Given a wf provide the best guess based on the user-approved analyses (in or outside of texts).
		/// If we don't already have a guess, this will try to create one from the lexicon, based on the
		/// form in the specified WS.
		/// </summary>
		public IAnalysis GetBestGuess(IWfiWordform wf, int ws)
		{
			IAnalysis wag;
			if (TryGetConditionedGuess(wf, m_nullWAG, out wag))
				return wag;
			if (wf.AnalysesOC.Count == 0)
			{
				GenerateEntryGuesses(wf, ws);
				if (TryGetConditionedGuess(wf, m_nullWAG, out wag))
					return wag;
			}
			return new NullWAG();
		}

		/// <summary>
		/// Given a wa provide the best guess based on glosses for that analysis (made in or outside of texts).
		/// </summary>
		/// <param name="wa"></param>
		/// <returns></returns>
		public IAnalysis GetBestGuess(IWfiAnalysis wa)
		{
			IAnalysis wag;
			if (TryGetConditionedGuess(wa, m_nullWAG, out wag))
				return wag;
			return new NullWAG();
		}

		/// <summary>
		/// This guess factors in the placement of an occurrence in its segment for making other
		/// decisions like matching lowercase alternatives for sentence initial occurrences.
		/// </summary>
		/// <param name="onlyIndexZeroLowercaseMatching">
		/// True: Do lowercase matching only if the occurrence index is zero.
		/// False: Do lowercase matching regardless of the occurrence index.
		/// </param>
		public IAnalysis GetBestGuess(AnalysisOccurrence occurrence, bool onlyIndexZeroLowercaseMatching = true)
		{
			// first see if we can make a guess based on the lowercase form of a sentence initial (non-lowercase) wordform
			// TODO: make it look for the first word in the sentence...may not be at Index 0!
			IAnalysis previous = GetPreviousWordform(occurrence.Segment, occurrence.Index);
			IAnalysis bestGuess;
			if (occurrence.Analysis is IWfiWordform && (!onlyIndexZeroLowercaseMatching || occurrence.Index == 0))
			{
				ITsString tssWfBaseline = occurrence.BaselineText;
				var cf = new CaseFunctions(Cache.ServiceLocator.WritingSystemManager.Get(tssWfBaseline.get_WritingSystemAt(0)));
				string sLower = cf.ToLower(tssWfBaseline.Text);
				// don't bother looking up the lowercased wordform if the instanceOf is already in lowercase form.
				if (sLower != tssWfBaseline.Text)
				{
					ITsString tssLower = TsStringUtils.MakeString(sLower, TsStringUtils.GetWsAtOffset(tssWfBaseline, 0));
					IWfiWordform lowercaseWf;
					if (Cache.ServiceLocator.GetInstance<IWfiWordformRepository>().TryGetObject(tssLower, out lowercaseWf))
					{
						// Try conditioned guess first.
						if (TryGetConditionedGuess(lowercaseWf, previous, out bestGuess))
							return bestGuess;
						// Try generating an entry from the lexicon.
						if (TryGetBestGuess(lowercaseWf, occurrence.BaselineWs, out bestGuess))
							return bestGuess;
					}
				}
			}
			if (occurrence.BaselineWs == -1)
				return null; // happens with empty translation lines
			// Try conditioned guess first.
			if (TryGetConditionedGuess(occurrence.Analysis, previous, out bestGuess))
				return bestGuess;
			// Try generating an entry from the lexicon.
			return GetBestGuess(occurrence.Analysis, occurrence.BaselineWs);
		}

		private IAnalysis GetBestGuess(IAnalysis wag, int ws)
		{
			if (wag is IWfiWordform)
				return GetBestGuess(wag.Wordform, ws);
			if (wag is IWfiAnalysis)
				return GetBestGuess(wag.Analysis);
			return new NullWAG();
		}

		/// <summary>
		///
		/// </summary>
		public bool TryGetBestGuess(IAnalysis wag, int ws, out IAnalysis bestGuess)
		{
			bestGuess = GetBestGuess(wag, ws);
			return !(bestGuess is NullWAG);
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="onlyIndexZeroLowercaseMatching">
		/// True: Do lowercase matching only if the occurrence index is zero.
		/// False: Do lowercase matching regardless of the occurrence index.
		/// </param>
		public bool TryGetBestGuess(AnalysisOccurrence occurrence, out IAnalysis bestGuess, bool onlyIndexZeroLowercaseMatching = true)
		{
			bestGuess = GetBestGuess(occurrence, onlyIndexZeroLowercaseMatching);
			return !(bestGuess is NullWAG);
		}

		/// <summary>
		/// Get analyses for the wordform in occurrence sorted by priority.
		/// </summary>
		public List<IWfiAnalysis> GetSortedAnalyses(AnalysisOccurrence occurrence)
		{
			if (occurrence.HasWordform)
			{
				IWfiWordform wordform = occurrence.Analysis.Wordform;
				IAnalysis previous = GetPreviousWordform(occurrence.Segment, occurrence.Index);
				var counts = GetAnalysisCounts(wordform);
				List<IWfiAnalysis> analyses = wordform.AnalysesOC.ToList();
				analyses.Sort((x, y) => ComparePriorityCounts(x, y, previous, counts));
				return analyses;
			}
			return new List<IWfiAnalysis>();
		}

		/// <summary>
		/// Get analyses for the wordform sorted by priority.
		/// </summary>
		public List<IWfiAnalysis> GetSortedAnalyses(IWfiWordform wordform)
		{
			var counts = GetAnalysisCounts(wordform);
			List<IWfiAnalysis> analyses = wordform.AnalysesOC.ToList();
			analyses.Sort((x, y) => ComparePriorityCounts(x, y, m_nullWAG, counts));
			return analyses;
		}

		/// <summary>
		/// Compare the priority counts for a1 and a2
		/// based on the previous wordform and a dictionary of counts.
		/// </summary>
		private int ComparePriorityCounts(IWfiAnalysis a1, IWfiAnalysis a2, IAnalysis previous,
			Dictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>> counts)
		{
			// Get priority counts for a1 and a2.
			PriorityCount pc1 = null;
			PriorityCount pc2 = null;
			if (counts.ContainsKey(previous) && counts[previous].ContainsKey(a1))
				pc1 = counts[previous][a1];
			if (counts.ContainsKey(previous) && counts[previous].ContainsKey(a2))
				pc2 = counts[previous][a2];
			if (pc1 == null && pc2 == null && previous != m_nullWAG)
			{
				// Try backoff for both.
				previous = m_nullWAG;
				if (counts.ContainsKey(previous) && counts[previous].ContainsKey(a1))
					pc1 = counts[previous][a1];
				if (counts.ContainsKey(previous) && counts[previous].ContainsKey(a2))
					pc2 = counts[previous][a2];
			}
			// Compare priority counts.
			if (pc1 == null)
			{
				if (pc2 == null)
					return 0;
				return -1;
			}
			if (pc2 == null)
				return 1;
			if (pc1.priority < pc2.priority)
				return -1;
			if (pc1.priority > pc2.priority)
				return 1;
			if (pc1.count < pc2.count)
				return -1;
			if (pc1.priority > pc2.priority)
				return 1;
			return 0;
		}

	}

}
