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
using System.Diagnostics;
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
			m_emptyWAG = new EmptyWAG();
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

		class PriorityCount
		{
			public int priority = 0; // the priority of the count
			public int count = 0;
		}

		// First key of m_guessTable = word form (or analysis).
		// Second key of m_guessTable = previous word form (including m_nullWAG for backoff).
		// Final value of m_guessTable = default analysis (or gloss).
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

		private readonly IAnalysis m_emptyWAG;  // Represents an empty word form.
		private readonly IAnalysis m_nullWAG;   // Represents the absence of a word form.

		/// <summary>
		/// an empty object for a WAG modelled after NullWAG
		/// </summary>
		public class EmptyWAG : NullCmObject, IAnalysis
		{
			#region IAnalysis Members

			/// <summary>
			///
			/// </summary>
			public IWfiWordform Wordform
			{
				get { return null; }
			}

			/// <summary>
			///
			/// </summary>
			public bool HasWordform
			{
				get { return false; }
			}

			/// <summary>
			///
			/// </summary>
			public IWfiAnalysis Analysis
			{
				get { return null; }
			}

			/// <summary>
			///
			/// </summary>
			/// <param name="ws"></param>
			/// <returns></returns>
			public ITsString GetForm(int ws)
			{
				return null;
			}

			#endregion
		}

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
			ICmAgentEvaluation cae = null;
			foreach (var ae in wa.EvaluationsRC)
				if (((ICmAgent)ae.Owner).Human)
					cae = ae;
			if (cae != null)
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
			ICmAgentEvaluation cae = null;
			foreach (var ae in wa.EvaluationsRC)
				if (((ICmAgent)ae.Owner).Human)
					cae = ae;
			if (cae != null)
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
			var agent = Cache.LangProject.DefaultComputerAgent;
			return candidate.GetAgentOpinion(agent) == Opinions.approves;
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="candidate"></param>
		/// <returns></returns>
		public bool IsParserApproved(IWfiAnalysis candidate)
		{
			var agent = Cache.LangProject.DefaultParserAgent;
			return candidate.GetAgentOpinion(agent) == Opinions.approves;
			// return ParserApprovedTable.Contains(candidate);
		}

		/// <summary>
		/// Try to get the default analysis for form conditioned on the previous word form.
		/// If form is an analysis,then the default analysis is a gloss.
		/// If form is a wordform, then try to get the default gloss of the default analysis if it exists.
		/// Use m_emptyWAG as the previous word form for the first analysis in a segment.
		/// Use m_nullWAG as the previous word form if the previous word form is unknown.
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
				m_guessTable[form] = GetDefaultAnalyses(form);
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
			if (analysis == null)
				return false;
			if (analysis is IWfiAnalysis)
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
			Dictionary<IAnalysis, IAnalysis> defaults = new Dictionary<IAnalysis, IAnalysis>();
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
				return defaults;
			}
			foreach (IAnalysis previous in counts.Keys)
			{
				// Get the highest scoring analysis.
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
						AddAnalysisCount(previous, analysis, 5, counts);
					}
				}
			}
			// Include analyses that may not have been selected.
			foreach (IWfiAnalysis analysis in wordform.AnalysesOC)
			{
				if (IsNotDisapproved(analysis))
				{
					int priority = IsHumanApproved(analysis) ? 4 : IsParserApproved(analysis) ? 3 : IsComputerApproved(analysis) ? 2 : 1;
					AddAnalysisCount(m_nullWAG, analysis, priority, counts);
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
		/// <param name="analysis">the analysis that you want a gloss for</param>
		/// <returns>Dictionary<IAnalysis, Dictionary<IAnalysis, int>></returns>
		private Dictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>> GetGlossCounts(IWfiAnalysis analysis)
		{
			var counts = new Dictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>>();
			var segs = new HashSet<ISegment>();
			if (!IsNotDisapproved(analysis))
				return counts;
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
							AddAnalysisCount(previous, gloss, 2, counts);
						}
					}
				}
			}
			// Include glosses that may not have been selected.
			foreach (IWfiGloss gloss in analysis.MeaningsOC)
			{
				AddAnalysisCount(m_nullWAG, gloss, 1, counts);
			}
			return counts;
		}

		/// <summary>
		/// Get the previous word form given a location.
		/// </summary>
		/// <param name="seg">the segment of the location</param>
		/// <param name="i">the index of the location</param>
		/// <returns>IAnalysis</returns>
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

		/// <summary>
		/// Add a count to counts for analysis with the given previous word form and the given priority.
		/// </summary>
		/// <param name="previous">the previous word form</param>
		/// <param name="analysis">the analysis being counted</param>
		/// <param name="priority">the priority of the count</param>
		/// <param name="counts">the dictionary of counts being incremented</param>
		/// <returns>void</returns>
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
			// Increment count.
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
		/// Get the analyses for the wordform sorted by priority.
		/// </summary>
		public List<IWfiAnalysis> GetSortedAnalyses(IWfiWordform wordform, AnalysisOccurrence occurrence = null)
		{
			var counts = GetAnalysisCounts(wordform);
			var previous = occurrence == null ? m_nullWAG : GetPreviousWordform(occurrence.Segment, occurrence.Index);
			List<IWfiAnalysis> analyses = wordform.AnalysesOC.ToList();
			analyses.Sort((x, y) => ComparePriorityCounts(x, y, previous, counts));
			return analyses;
		}

		/// <summary>
		/// Get glosses for the analysis sorted by priority.
		/// </summary>
		public List<IWfiGloss> GetSortedGlosses(IWfiAnalysis analysis, AnalysisOccurrence occurrence = null)
		{
			var counts = GetGlossCounts(analysis);
			var previous = occurrence == null ? m_nullWAG : GetPreviousWordform(occurrence.Segment, occurrence.Index);
			List<IWfiGloss> glosses = analysis.MeaningsOC.ToList();
			glosses.Sort((x, y) => ComparePriorityCounts(x, y, previous, counts));
			return glosses;
		}

		/// <summary>
		/// Compare the priority counts for a1 and a2 based on
		/// the previous wordform and a dictionary of counts.
		/// Sort in descending order.
		/// </summary>
		private int ComparePriorityCounts(IAnalysis a1, IAnalysis a2, IAnalysis previous,
			Dictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>> counts)
		{
			// Check for existence of previous.
			if (!counts.ContainsKey(previous))
			{
				previous = m_nullWAG;
				if (!counts.ContainsKey(previous))
					return 0;
			}
			// See if we should back off.
			if (!counts[previous].ContainsKey(a1) && !counts[previous].ContainsKey(a2))
				previous = m_nullWAG;
			// Get priority counts for a1 and a2.
			int count1 = counts[previous].ContainsKey(a1) ? counts[previous][a1].count : 0;
			int count2 = counts[previous].ContainsKey(a2) ? counts[previous][a2].count : 0;
			int priority1 = counts[previous].ContainsKey(a1) ? counts[previous][a1].priority : 0; ;
			int priority2 = counts[previous].ContainsKey(a2) ? counts[previous][a2].priority : 0;
			// Compare priority counts.
			if (priority1 < priority2)
				return 1;
			if (priority1 > priority2)
				return -1;
			if (count1 < count2)
				return 1;
			if (count1 > count2)
				return -1;
			return 0;
		}
	}
}
