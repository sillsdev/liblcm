// Copyright (c) 2009-2022 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//
// File: AnalysisGuessServices.cs
// Responsibility: pyle
//
// <remarks>
// </remarks>

using System;
using System.Collections.Generic;
using System.Linq;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
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

		public AnalysisOccurrence IgnoreOccurrence { get; set; }

		LcmCache Cache { get; set; }

		// PriorityCount provides a count of the number of times an analysis
		// appears with the given priority (= human approved, parser approved, etc.).
		// It is used to determine which analysis has higher priority.
		class PriorityCount
		{
			public bool lowercased = false; // whether the word form of the analysis was lowercased
			public int priority = 0; // the priority of the count
			public int count = 0;
		}

		class ContextCount
		{
			// First key is the previous/next wordform.
			// Second key is an analysis that occurs in that context.
			// The PriorityCount is the count for the analysis.
			public IDictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>> previousWordform;
			public IDictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>> nextWordform;
			public IDictionary<IAnalysis, PriorityCount> wordform;
		}

		// Key of m_guessTable = word form (or analysis).
		// Value of m_guessTable = context counts for word form.
		private IDictionary<IAnalysis, ContextCount> m_guessTable;
		IDictionary<IAnalysis, ContextCount> GuessTable
		{
			get
			{
				if (m_guessTable == null)
					GuessTable = new Dictionary<IAnalysis, ContextCount>();
				return m_guessTable;
			}
			set { m_guessTable = value; }
		}

		// CaselessGuessTable is like GuessTable, but for uppercase word forms that can have lowercase analyses.
		private IDictionary<IAnalysis, ContextCount> m_caselessGuessTable;
		IDictionary<IAnalysis, ContextCount> CaselessGuessTable
		{
			get
			{
				if (m_caselessGuessTable == null)
					CaselessGuessTable = new Dictionary<IAnalysis, ContextCount>();
				return m_caselessGuessTable;
			}
			set { m_caselessGuessTable = value; }
		}

		private readonly IAnalysis m_emptyWAG;  // Represents an empty word form.
		private readonly IAnalysis m_nullWAG;   // Represents an unknown word form.

		/// <summary>
		/// an empty object for a WAG modelled after NullWAG
		/// EmptyWAG represents the previous word form of the first word of a sentence.
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
		/// analysis. If necessary clear the GuessTable. If possible update it.
		/// Return true if the cache was changed.
		/// </summary>
		public bool UpdatingOccurrence(IAnalysis oldAnalysis, IAnalysis newAnalysis)
		{
			if (m_guessTable == null && m_caselessGuessTable == null)
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
			if (newAnalysis.Wordform != oldAnalysis)
			{
				// The wordform changed, probably because a lowercase analysis was used.
				// This changes the previous word form of the next word, which is unknown to us.
				ClearGuessData();
				return true;
			}
			var result = false;
			// Remove the word form from the guess tables.
			if (GuessTable.ContainsKey(oldAnalysis))
			{
				GuessTable.Remove(oldAnalysis);
				result = true;
			}
			if (CaselessGuessTable.ContainsKey(oldAnalysis))
			{
				CaselessGuessTable.Remove(oldAnalysis);
				result = true;
			}
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
		/// <param name="wa"></param>
		/// <returns></returns>
		public bool IsHumanDisapproved(IWfiAnalysis wa)
		{
			ICmAgentEvaluation cae = null;
			foreach (var ae in wa.EvaluationsRC)
				if (((ICmAgent)ae.Owner).Human)
					cae = ae;
			if (cae != null)
				return !cae.Approves;
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
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="candidate"></param>
		/// <returns></returns>
		public bool IsParserDisapproved(IWfiAnalysis candidate)
		{
			var agent = Cache.LangProject.DefaultParserAgent;
			return candidate.GetAgentOpinion(agent) == Opinions.disapproves;
		}

		/// <summary>
		/// Try to get the default analysis for form in the context of its occurrence.
		/// If form is an analysis,then the result is a gloss.
		/// If form is a wordform, then try to get the default gloss of the default analysis if it exists.
		/// Use m_emptyWAG as the previous word form for the first analysis in a segment.
		/// Use m_nullWAG as the previous word form if the previous word form is unknown.
		/// </summary>
		/// <param name="form">the form that you want an analysis for</param>
		/// <param name="lowercaseForm">the lowercase version of form if its analyses should be included</param>
		/// <param name="occurrence">where the form occurs (used for context)</param>
		/// <param name="analysis">the resulting analysis</param>
		/// <returns>bool</returns>
		private bool TryGetContextAwareGuess(IAnalysis form, IWfiWordform lowercaseForm, AnalysisOccurrence occurrence, out IAnalysis analysis)
		{
			IDictionary<IAnalysis, ContextCount> guessTable = GetGuessTable(form, lowercaseForm);
			analysis = GetBestAnalysis(guessTable[form], occurrence);
			if (analysis == null)
				return false;
			if (analysis is IWfiAnalysis)
			{
				// Get the best gloss for analysis.
				if (TryGetContextAwareGuess(analysis, null, occurrence, out IAnalysis gloss))
				{
					analysis = gloss;
				}
			}
			return true;
		}

		private IDictionary<IAnalysis, ContextCount> GetGuessTable(IAnalysis form, IWfiWordform lowercaseForm)
		{
			IDictionary<IAnalysis, ContextCount> guessTable = lowercaseForm != null ? CaselessGuessTable : GuessTable;

			if (!guessTable.ContainsKey(form))
			{
				// Fill in GuessTable.
				guessTable[form] = GetContextCounts(form);
				if (lowercaseForm != null)
					GetContextCounts(lowercaseForm, true, guessTable[form]);
				// Move uncontexted counts from previousWordform to wordform.
				if (guessTable[form].previousWordform.ContainsKey(m_nullWAG))
				{
					guessTable[form].wordform = guessTable[form].previousWordform[m_nullWAG];
					guessTable[form].previousWordform.Remove(m_nullWAG);
					guessTable[form].nextWordform.Remove(m_nullWAG);
				} else
				{
					guessTable[form].wordform = new Dictionary<IAnalysis, PriorityCount>();
				}
			}
			return guessTable;
		}

		/// <summary>
		/// Get the best analysis from counts given context of occurrence.
		/// </summary>
		/// <param name="counts"></param>
		/// <param name="occurrence"></param>
		/// <returns></returns>
		private IAnalysis GetBestAnalysis(ContextCount counts, AnalysisOccurrence occurrence)
		{
			IAnalysis best = null;
			foreach (IAnalysis key in counts.wordform.Keys)
			{
				if (best == null || ComparePriorityCounts(key, best, occurrence, counts) < 0)
				{
					best = key;
				}
			}
			return best;
		}

		/// <summary>
		/// Get the context counts for form.
		/// </summary>
		/// <param name="form"></param>
		/// <param name="lowercased">whether form is lowercased</param>
		/// <param name="counts">existing context counts</param>
		/// <returns></returns>
		private ContextCount GetContextCounts(IAnalysis form, bool lowercased = false, ContextCount counts = null)
		{
			if (counts == null)
				counts = new ContextCount();
			if (form is IWfiWordform wordform)
			{
				counts.previousWordform = GetAnalysisCounts(wordform, lowercased, true, counts.previousWordform);
				counts.nextWordform = GetAnalysisCounts(wordform, lowercased, false, counts.nextWordform);
			}
			else if (form is IWfiAnalysis analysis)
			{
				// Get default glosses.
				counts.previousWordform = GetGlossCounts(analysis, true);
				counts.nextWordform = GetGlossCounts(analysis, false);
			}
			return counts;
		}

		/// <summary>
		/// Get analysis counts for the given word form in its context.
		/// Uses m_emptyWAG as previous word form for the first analysis in a segment.
		/// Uses m_nullWAG as previous word form when unknown.
		/// This is used by GetBestGuess for word forms and GetSortedAnalysisGuesses.
		/// </summary>
		private IDictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>> GetAnalysisCounts(IWfiWordform wordform, bool lowercased = false,
			bool previous = true, IDictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>> counts = null)
		{
			if (counts == null)
				counts = new Dictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>>();
			var segs = new HashSet<ISegment>();
			foreach (ISegment seg in wordform.OccurrencesInTexts)
			{
				if (segs.Contains(seg)) continue;
				segs.Add(seg);
				for (int i = 0; i < seg.AnalysesRS.Count; i++)
				{
					IAnalysis analysis = seg.AnalysesRS[i];
					if (analysis.Wordform != wordform) continue;
					if (IgnoreOccurrence != null && IgnoreOccurrence.Segment == seg && IgnoreOccurrence.Index == i)
					{
						// Leave this occurrence out.
						continue;
					}
					IAnalysis adjacent = GetAdjacentWordform(seg, i, previous);
					if (analysis is IWfiGloss)
					{
						// Get analysis for gloss.
						analysis = analysis.Analysis;
					}
					if (analysis is IWfiAnalysis)
					{
						// Add high priority count to analysis.
						AddAnalysisCount(adjacent, analysis, 7, lowercased, counts);
					}
				}
			}
			if (IgnoreOccurrence != null)
				// Only include selected analyses.
				return counts;
			// Include analyses that may not have been selected.
			foreach (IWfiAnalysis analysis in wordform.AnalysesOC)
			{
				if (IsNotDisapproved(analysis))
				{
					// Human takes priority over parser which takes priority over computer.
					// Approved takes priority over disapproved.
					// More counts take priority over fewer counts within a priority.
					int priority = IsHumanApproved(analysis) ? 6 : IsHumanDisapproved(analysis) ? 1 :
						IsParserApproved(analysis) ? 5 : IsParserDisapproved(analysis) ? 2 :
						IsComputerApproved(analysis) ? 4 : 3;
					AddAnalysisCount(m_nullWAG, analysis, priority, lowercased, counts);
				}
			}
			return counts;
		}

		/// <summary>
		/// Get gloss counts for the given analysis in its context.
		/// Uses m_emptyWAG as previous word form for the first analysis in a segment.
		/// Uses m_nullWAG as previous word form when unknown.
		/// This is used by GetBestGuess for analyses and GetSortedGlossGuesses.
		/// </summary>
		/// <param name="analysis">the analysis that you want a gloss for</param>
		/// <returns>Dictionary<IAnalysis, Dictionary<IAnalysis, int>></returns>
		private Dictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>> GetGlossCounts(IWfiAnalysis analysis, bool previous)
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
					IAnalysis adjacent = GetAdjacentWordform(seg, i, previous);
					IAnalysis gloss = seg.AnalysesRS[i];
					if (gloss is IWfiGloss)
					{
						if (gloss.Analysis == analysis)
						{
							// Add high priority count to gloss.
							AddAnalysisCount(adjacent, gloss, 2, false, counts);
						}
					}
				}
			}
			// Include glosses that may not have been selected.
			foreach (IWfiGloss gloss in analysis.MeaningsOC)
			{
				AddAnalysisCount(m_nullWAG, gloss, 1, false, counts);
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
			// Should be IWfiWordform or IPunctuationForm.
			return previous;
		}

		/// <summary>
		/// Get the adjacent word form of a location.
		/// </summary>
		/// <param name="seg">the segment of the location</param>
		/// <param name="i">the index of the location</param>
		/// <returns>IAnalysis</returns>
		private IAnalysis GetAdjacentWordform(ISegment seg, int i, bool previous)
		{
			if (previous && i == 0)
				return m_emptyWAG;
			if (!previous && i == seg.AnalysesRS.Count - 1)
				return m_emptyWAG;
			int j = previous ? i - 1 : i + 1;
			IAnalysis adjacent = seg.AnalysesRS[j];
			if (adjacent is IWfiAnalysis || adjacent is IWfiGloss)
			{
				adjacent = adjacent.Wordform;
			}
			// Should be IWfiWordform or IPunctuationForm.
			return adjacent;
		}

		/// <summary>
		/// Add a count to counts for analysis with the given context word form and the given priority.
		/// </summary>
		/// <param name="context">the context word form</param>
		/// <param name="analysis">the analysis being counted</param>
		/// <param name="priority">the priority of the count</param>
		/// <param name="lowercased">whether the word form of the analysis was lowercased</param>
		/// <param name="counts">the dictionary of counts being incremented</param>
		/// <returns>void</returns>
		private void AddAnalysisCount(IAnalysis context, IAnalysis analysis, int priority, bool lowercased,
			IDictionary<IAnalysis, Dictionary<IAnalysis, PriorityCount>> counts)
		{
			if (context != m_nullWAG)
			{
				// Record count for unknown/backoff.
				AddAnalysisCount(m_nullWAG, analysis, priority, lowercased, counts);
			}
			if (!counts.ContainsKey(context))
			{
				counts[context] = new Dictionary<IAnalysis, PriorityCount>();
			}
			if (!counts[context].ContainsKey(analysis))
			{
				counts[context][analysis] = new PriorityCount();
			}
			if (counts[context][analysis].priority > priority)
			{
				// Ignore this count because its priority is too low.
				return;
			}
			if (counts[context][analysis].priority < priority)
			{
				// Start a new priority count.
				counts[context][analysis].priority = priority;
				counts[context][analysis].lowercased = lowercased;
				counts[context][analysis].count = 0;
			}
			// Increment count.
			counts[context][analysis].count += 1;
		}

		/// <summary>
		/// Compare the priority counts for a1 and a2 based on the context of the occurrence.
		/// Sort in descending order.
		/// </summary>
		private int ComparePriorityCounts(IAnalysis a1, IAnalysis a2, AnalysisOccurrence occurrence, ContextCount contextCount)
		{
			// Compare contexted counts.
			if (occurrence != null)
			{
				float score1 = GetContextScore(a1, occurrence, contextCount);
				float score2 = GetContextScore(a2, occurrence, contextCount);
				if (score1 < score2)
					return 1;
				if (score1 > score2)
					return -1;
			}
			// Compare non-contexted counts.
			IDictionary<IAnalysis, PriorityCount> counts = contextCount.wordform;
			// Prefer higher priority counts.
			int priority1 = counts.ContainsKey(a1) ? counts[a1].priority : 0;
			int priority2 = counts.ContainsKey(a2) ? counts[a2].priority : 0;
			if (priority1 < priority2)
				return 1;
			if (priority1 > priority2)
				return -1;
			// Prefer higher counts.
			int count1 = counts.ContainsKey(a1) ? counts[a1].count : 0;
			int count2 = counts.ContainsKey(a2) ? counts[a2].count : 0;
			if (count1 < count2)
				return 1;
			if (count1 > count2)
				return -1;
			// Prefer analyses that haven't been lowercased.
			bool lowercased1 = counts.ContainsKey(a1) && counts[a1].lowercased;
			bool lowercased2 = counts.ContainsKey(a2) && counts[a2].lowercased;
			if (lowercased1 && !lowercased2)
				return 1;
			if (lowercased2 && !lowercased1)
				return -1;
			// Maintain a complete order to avoid non-determinism.
			// This means that GetBestGuess and GetSortedAnalyses[0] should have the same analysis.
			return a1.Guid.CompareTo(a2.Guid);

		}

		float GetContextScore(IAnalysis analysis, AnalysisOccurrence occurrence, ContextCount contextCount)
		{
			float previousScore = GetContextScore(analysis, occurrence, true, contextCount);
			float nextScore = GetContextScore(analysis, occurrence, false, contextCount);
			return previousScore + nextScore;
		}

		float GetContextScore(IAnalysis analysis, AnalysisOccurrence occurrence, bool previous, ContextCount contextCount)
		{
			IAnalysis context = GetAdjacentWordform(occurrence.Segment, occurrence.Index, previous);
			IDictionary < IAnalysis, Dictionary < IAnalysis, PriorityCount >> counts = previous
				? contextCount.previousWordform
				: contextCount.nextWordform;
			if (counts.ContainsKey(context) &&
				counts[context].ContainsKey(analysis))
			{
				float count = counts[context][analysis].count;
				float total = 0;
				foreach (IAnalysis anal in counts[context].Keys)
					total += counts[context][anal].count;
				if (total > 0)
					return count / total;
			}
			return 0;
		}

		/// <summary>
		/// Whenever the data we depend upon changes, use this to make sure we load the latest Guess data.
		/// </summary>
		public void ClearGuessData()
		{
			GuessTable = null;
			CaselessGuessTable = null;
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
			if (!EntryGenerated(wf))
				GenerateEntryGuesses(wf, ws);
			IAnalysis wag;
			if (TryGetContextAwareGuess(wf, null, null, out wag))
				return wag;
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
			if (TryGetContextAwareGuess(wa, null, null, out wag))
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
		/// <param name="includeContext">
		/// True: Consider context when getting best guess.
		/// False: Ignore context when getting best guess.
		/// </param>
		public IAnalysis GetBestGuess(AnalysisOccurrence occurrence, bool onlyIndexZeroLowercaseMatching = true, bool includeContext = true)
		{
			int ws = occurrence.BaselineWs;
			if (ws == -1)
				return new NullWAG(); // happens with empty translation lines

			IWfiWordform lowercaseWf = null;
			if (occurrence.Analysis is IWfiWordform wordform)
			{
				lowercaseWf = GetLowercaseWordform(occurrence, ws, onlyIndexZeroLowercaseMatching, wordform);
				// Generate entries if necessary.
				if (!EntryGenerated(wordform))
					GenerateEntryGuesses(wordform, ws);
				if (lowercaseWf != null && !EntryGenerated(lowercaseWf))
					GenerateEntryGuesses(lowercaseWf, ws);
			}
			IAnalysis bestGuess;
			if (TryGetContextAwareGuess(occurrence.Analysis, lowercaseWf, includeContext ? occurrence : null, out bestGuess))
				return bestGuess;
			return new NullWAG();
		}

		/// <summary>
		/// Get the lowercase word form if the occurrence is uppercase.
		/// </summary>
		private IWfiWordform GetLowercaseWordform(AnalysisOccurrence occurrence, int ws,
			bool onlyIndexZeroLowercaseMatching, IWfiWordform wordform)
		{
			// TODO: make it look for the first word in the sentence...may not be at Index 0!
			if (occurrence == null)
				return null;
			if (onlyIndexZeroLowercaseMatching && occurrence.Index != 0)
				return null;
			ITsString tssWfBaseline = occurrence.BaselineText;
			var cf = new CaseFunctions(Cache.ServiceLocator.WritingSystemManager.Get(ws));
			string sLower = cf.ToLower(tssWfBaseline.Text);
			// don't bother looking up the lowercased wordform if the instanceOf is already in lowercase form.
			if (sLower != wordform.ShortName)
			{
				return GetWordformIfNeeded(sLower, ws);
			}
			return null;
		}

		// <summary>
		// Get a wordform with the original case of occurrence's text
		// if it lowercases to the given wordform.
		// Otherwise, return null.
		// </summary>
		private IWfiWordform GetOriginalCaseWordform(AnalysisOccurrence occurrence, IWfiWordform wordform, int ws)
		{
			ITsString tssWfBaseline = occurrence.BaselineText;
			var cf = new CaseFunctions(Cache.ServiceLocator.WritingSystemManager.Get(ws));
			string sLower = cf.ToLower(tssWfBaseline.Text);
			if (sLower == wordform.GetForm(ws).Text)
			{
				return GetWordformIfNeeded(tssWfBaseline.Text, ws);
			}
			return null;
		}

		/// <summary>
		/// Get a wordform for word if it already exists or
		/// if it has an entry in the lexicon.
		/// </summary>
		private IWfiWordform GetWordformIfNeeded(string word, int ws)
		{
			ITsString tssWord = TsStringUtils.MakeString(word, ws);
			IWfiWordform wf;
			// Look for an existing wordform.
			if (Cache.ServiceLocator.GetInstance<IWfiWordformRepository>().TryGetObject(tssWord, out wf))
				return wf;
			// Only create a wordform if there is an entry for it in the lexicon.
			var morphs = MorphServices.GetMatchingMonomorphemicMorphs(Cache, tssWord);
			if (morphs.Count() > 0)
			{
				NonUndoableUnitOfWorkHelper.DoUsingNewOrCurrentUowOrSkip(Cache.ActionHandlerAccessor,
					"Trying to generate wordforms during PropChanged when we can't save them.",
					() =>
					{
						wf = Cache.ServiceLocator.GetInstance<IWfiWordformFactory>().Create(tssWord);
					});
				return wf;
			}
			return null;
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
		/// Get possible analyses for the wordform sorted by priority.
		/// <param name="wordform">wordform to get analyses for</param>
		/// <param name="occurrence">the location of the wordform</param>
		/// <param name="onlyIndexZeroLowercaseMatching">
		/// True: Do lowercase matching only if the occurrence index is zero.
		/// False: Do lowercase matching regardless of the occurrence index.
		/// </param>
		/// </summary>
		public List<IWfiAnalysis> GetSortedAnalysisGuesses(IWfiWordform wordform, AnalysisOccurrence occurrence, bool onlyIndexZeroLowercaseMatching = true)
		{
			int ws = occurrence != null ? occurrence.BaselineWs : wordform.Cache.DefaultVernWs;
			return GetSortedAnalysisGuesses(wordform, ws, occurrence, onlyIndexZeroLowercaseMatching);
		}

		/// <summary>
		/// Get possible analyses for the wordform sorted by priority.
		/// <param name="wordform">wordform to get analyses for</param>
		/// <param name="ws">the writing system for wordform</param>
		/// </summary>
		public List<IWfiAnalysis> GetSortedAnalysisGuesses(IWfiWordform wordform, int ws)
		{
			return GetSortedAnalysisGuesses(wordform, ws, null);
		}

		/// <summary>
		/// Get possible analyses for the wordform sorted by priority.
		/// <param name="wordform">wordform to get analyses for</param>
		/// <param name="ws">the writing system for wordform</param>
		/// <param name="occurrence">the location of wordform</param>
		/// <param name="onlyIndexZeroLowercaseMatching">
		/// True: Do lowercase matching only if the occurrence index is zero.
		/// False: Do lowercase matching regardless of the occurrence index.
		/// </param>
		/// </summary>
		private List<IWfiAnalysis> GetSortedAnalysisGuesses(IWfiWordform wordform, int ws, AnalysisOccurrence occurrence, bool onlyIndexZeroLowercaseMatching = true)
		{
			if (occurrence != null && (!onlyIndexZeroLowercaseMatching || occurrence.Index == 0))
			{
				// Sometimes the user selects a lowercase wordform for an uppercase word.
				// Get the original case so that we can include uppercase analyses.
				var originalCaseWf = GetOriginalCaseWordform(occurrence, wordform, ws);
				if (originalCaseWf != null)
					wordform = originalCaseWf;
			}
			IWfiWordform lowercaseWf = GetLowercaseWordform(occurrence, ws, onlyIndexZeroLowercaseMatching, wordform);

			// Generate entries if necessary.
			if (!EntryGenerated(wordform))
				GenerateEntryGuesses(wordform, ws);
			if (lowercaseWf != null && !EntryGenerated(lowercaseWf))
				GenerateEntryGuesses(lowercaseWf, ws);

			// Get analyses to sort.
			List<IWfiAnalysis> analyses = wordform.AnalysesOC.ToList();
			if (lowercaseWf != null)
				analyses.AddRange(lowercaseWf.AnalysesOC);

			// Sort analyses.
			IDictionary<IAnalysis, ContextCount> guessTable = GetGuessTable(wordform, lowercaseWf);
			analyses.Sort((x, y) => ComparePriorityCounts(x, y, occurrence, guessTable[wordform]));
			return analyses;
		}

		/// <summary>
		/// Get possible glosses for the analysis sorted by priority.
		/// </summary>
		public List<IWfiGloss> GetSortedGlossGuesses(IWfiAnalysis analysis, AnalysisOccurrence occurrence = null)
		{
			List<IWfiGloss> glosses = analysis.MeaningsOC.ToList();
			IDictionary<IAnalysis, ContextCount> guessTable = GetGuessTable(analysis, null);
			glosses.Sort((x, y) => ComparePriorityCounts(x, y, occurrence, guessTable[analysis]));
			return glosses;
		}

		#region GenerateEntryGuesses
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
				return (obj.Text ?? "").GetHashCode() ^ obj.get_WritingSystem(0);
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
						msa = (IMoStemMsa)sense.MorphoSyntaxAnalysisRA;
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
				if (component is ILexSense sense1)
				{
					sense = sense1;
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
		/// Has GenerateEntryGuesses already been called for wordform?
		/// </summary>
		/// <param name="wordform"></param>
		private bool EntryGenerated(IWfiWordform wordform)
		{
			// NB: This is a hack.  It assumes that analyses
			// aren't created before GenerateEntryGuesses is called.
			return wordform.AnalysesOC.Count > 0;
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
							if (info.Sense != null && !IsEmptyMultiUnicode(info.Sense.Gloss))
							{
								// copy all the gloss alternatives from the sense into the word gloss.
								IWfiGloss wg = newAnalysis.MeaningsOC.First();
								wg.Form.MergeAlternatives(info.Sense.Gloss);
							}
							else
							{
								// Eliminate the dummy gloss (LT-21815).
								newAnalysis.MeaningsOC.Clear();
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
							// Clear GuessTable entries.
							if (GuessTable.ContainsKey(ww))
								GuessTable.Remove(ww);
							if (CaselessGuessTable.ContainsKey(ww))
								CaselessGuessTable.Remove(ww);
						});
				}
			}
		}

		bool IsEmptyMultiUnicode(IMultiUnicode multiUnicode)
		{
			foreach (var ws in multiUnicode.AvailableWritingSystemIds)
			{
				if (multiUnicode.get_String(ws).Length > 0)
					return false;
			}
			return true;
		}
		#endregion GenerateEntryGuesses
	}
}
